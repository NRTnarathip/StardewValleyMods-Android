using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using SpaceShared;
using StardewValley;
using static SpaceCore.SpaceCore;
using Microsoft.Xna.Framework;
using SpaceCore.VanillaAssetExpansion;

namespace SpaceCore.Patches
{
    [HarmonyPatch(typeof(AnimatedSprite), "draw", new Type[] { typeof(SpriteBatch), typeof(Vector2), typeof(float) })]
    public static class AnimatedSpriteDrawExtrasPatch1
    {
        public static bool Prefix(AnimatedSprite __instance, SpriteBatch b, Vector2 screenPosition, float layerDepth)
        {
            if (__instance.Texture != null)
            {
                var extras = SpaceCore.spriteExtras.GetOrCreateValue(__instance);
                b.Draw(__instance.Texture, screenPosition, __instance.sourceRect, extras.grad == null ? Color.White : extras.grad[extras.currGradInd], 0f, Vector2.Zero, 4f * extras.scale, (__instance.CurrentAnimation != null && __instance.CurrentAnimation[__instance.currentAnimationIndex].flip) ? SpriteEffects.FlipHorizontally : SpriteEffects.None, layerDepth);
            }
            return false;
        }
    }
    [HarmonyPatch(typeof(AnimatedSprite), "draw", new Type[] { typeof(SpriteBatch), typeof(Vector2), typeof(float), typeof(int), typeof(int), typeof(Color), typeof(bool), typeof(float), typeof(float), typeof(bool) })]
    public static class AnimatedSpriteDrawExtrasPatch2
    {
        public static bool Prefix(AnimatedSprite __instance, SpriteBatch b, Vector2 screenPosition, float layerDepth, int xOffset, int yOffset, Color c, bool flip = false, float scale = 1f, float rotation = 0f, bool characterSourceRectOffset = false)
        {
            if (__instance.Texture != null)
            {
                var extras = SpaceCore.spriteExtras.GetOrCreateValue(__instance);
                b.Draw(__instance.Texture, screenPosition, new Rectangle(__instance.sourceRect.X + xOffset, __instance.sourceRect.Y + yOffset, __instance.sourceRect.Width, __instance.sourceRect.Height), Color.Lerp(c, (extras.grad == null ? Color.White : extras.grad[extras.currGradInd]), 0.5f), rotation, characterSourceRectOffset ? new Vector2(__instance.SpriteWidth / 2, (float)__instance.SpriteHeight * 3f / 4f) : Vector2.Zero, scale * extras.scale, (flip || (__instance.CurrentAnimation != null && __instance.CurrentAnimation[__instance.currentAnimationIndex].flip)) ? SpriteEffects.FlipHorizontally : SpriteEffects.None, layerDepth);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(NPC), "draw", new Type[] { typeof(SpriteBatch), typeof(float) })]
    public static class AnimatedSpriteDrawExtrasPatch3
    {
        public static void getExtraValues(NPC who, ref Vector2 scale, ref Color grad)
        {
            var extras = SpaceCore.spriteExtras.GetOrCreateValue(who.Sprite);
            scale = extras.scale;
            grad = (extras.grad != null ? extras.grad[extras.currGradInd] : Color.White);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
        {
            LocalBuilder scale = generator.DeclareLocal(typeof(Vector2));
            LocalBuilder gradColor = generator.DeclareLocal(typeof(Color));

            var orig = new List<CodeInstruction>(instructions);
            var ret = new List<CodeInstruction>(){
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldloca, scale),
                new(OpCodes.Ldloca, gradColor),
                new(OpCodes.Call, typeof(AnimatedSpriteDrawExtrasPatch3).GetMethod(nameof(getExtraValues), BindingFlags.Public | BindingFlags.Static)),
            };

            int whiteCount = 0;
            int whiteSkip = 0;
            int vecCount = 0;
            int vecSkip = 1;
            int drawCount = 0;
            int drawSkip = 2;
            for (int i = 0; i < orig.Count; ++i)
            {
                // replace uses of Color.White with extras.grad
                // but only the first and third ones; skip #2
                if (whiteCount < 3 && orig[i].opcode == OpCodes.Call && orig[i].operand.Equals(typeof(Microsoft.Xna.Framework.Color).GetMethod("get_White", BindingFlags.Public | BindingFlags.Static)))
                {
                    ++whiteCount;
                    if (whiteSkip > 0)
                    {
                        --whiteSkip;
                        ret.Add(orig[i]);
                    }
                    else
                    {
                        whiteSkip = 1;
                        Log.Trace($"NPC.draw: replacing Color.White at {i}");
                        ret.Add(new CodeInstruction(OpCodes.Ldloc, gradColor));
                    }
                    continue;
                }
                // replace a call to SpriteBatch.Draw (use a different overload).
                // it's the third one
                if (drawCount < 3 && orig[i].opcode == OpCodes.Callvirt && (orig[i].operand as MethodInfo)?.Name == nameof(SpriteBatch.Draw))
                {
                    ++drawCount;
                    if (drawSkip > 0)
                    {
                        --drawSkip;
                        ret.Add(orig[i]);
                    }
                    else
                    {
                        Log.Trace($"NPC.draw: replacing SpriteBatch.Draw at {i}");
                        ret.Add(new CodeInstruction(OpCodes.Callvirt, typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch).GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Microsoft.Xna.Framework.Graphics.Texture2D), typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Rectangle), typeof(Microsoft.Xna.Framework.Color), typeof(float), typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Graphics.SpriteEffects), typeof(float) }, null)));
                    }
                    continue;
                }
                ret.Add(orig[i]);
                // append a multiply by the vector2 extras.scale. this is why
                // we had to change the Draw overload
                if (i > 0 && vecCount < 2 && orig[i - 1].opcode == OpCodes.Ldc_R4 && orig[i - 1].operand.Equals(4f) && orig[i].opcode == OpCodes.Mul)
                {
                    ++vecCount;
                    if (vecSkip > 0)
                    {
                        --vecSkip;
                    }
                    else
                    {
                        Log.Trace($"NPC.draw: inserting vec2 mul at {i}");
                        ret.AddRange(new List<CodeInstruction>(){
                            new(OpCodes.Ldloc, scale),
                            new(OpCodes.Call, typeof(Microsoft.Xna.Framework.Vector2).GetMethod("op_Multiply", BindingFlags.Public | BindingFlags.Static, null, new Type[]{typeof(float), typeof(Microsoft.Xna.Framework.Vector2)}, null)),
                        });
                    }
                }
            }

            if (whiteCount < 3 || vecCount < 2 || drawCount < 3)
            {
                Log.Error($"NPC.draw: some transpiler targets were not found. Aborting edit.");
                return orig;
            }
            return ret;
        }

    }

    [HarmonyPatch(typeof(NPC), "DrawBreathing", new Type[] { typeof(SpriteBatch), typeof(float) })]
    public static class AnimatedSpriteDrawExtrasPatch4
    {
        public static void getExtraValues(NPC who, ref Vector2 scale, ref Color grad)
        {
            var extras = SpaceCore.spriteExtras.GetOrCreateValue(who.Sprite);
            scale = extras.scale;
            grad = (extras.grad != null ? extras.grad[extras.currGradInd] : Color.White);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
        {
            LocalBuilder scale = generator.DeclareLocal(typeof(Vector2));
            LocalBuilder gradColor = generator.DeclareLocal(typeof(Color));

            var orig = new List<CodeInstruction>(instructions);
            var ret = new List<CodeInstruction>(){
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldloca, scale),
                new(OpCodes.Ldloca, gradColor),
                new(OpCodes.Call, typeof(AnimatedSpriteDrawExtrasPatch4).GetMethod(nameof(getExtraValues), BindingFlags.Public | BindingFlags.Static)),
            };

            int whiteCount = 0;
            int scaleCount = 0;
            int drawCount = 0;
            for (int i = 0; i < orig.Count; ++i)
            {
                // replace one use of Color.White with extras.grad
                if (whiteCount < 1 && orig[i].opcode == OpCodes.Call && orig[i].operand.Equals(typeof(Microsoft.Xna.Framework.Color).GetMethod("get_White", BindingFlags.Public | BindingFlags.Static)))
                {
                    ++whiteCount;
                    Log.Trace($"NPC.DrawBreathing: replacing Color.White at {i}");
                    ret.Add(new CodeInstruction(OpCodes.Ldloc, gradColor));
                }
                // add an extra vec2 multiply and vec2 add after applying
                // breathScale
                else if (i > 1
                    && scaleCount < 1
                    && orig[i - 2].opcode == OpCodes.Ldc_R4
                    && Equals(orig[i - 2].operand, 4f)
                    && orig[i - 1].opcode == OpCodes.Mul
                    && orig[i].opcode == OpCodes.Ldloc_S
                    && orig[i].operand is LocalBuilder breathScale
                    && breathScale.LocalType == typeof(float))
                {
                    ++scaleCount;
                    Log.Trace($"NPC.DrawBreathing: inserting vec2 mul/add at {i}");
                    ret.AddRange(new List<CodeInstruction>(){
                        new(OpCodes.Ldloc, scale),
                        new(OpCodes.Call, typeof(Microsoft.Xna.Framework.Vector2).GetMethod("op_Multiply", BindingFlags.Public | BindingFlags.Static, null, new Type[]{typeof(float), typeof(Microsoft.Xna.Framework.Vector2)}, null)),
                        new(OpCodes.Ldloc_S, orig[i].operand),
                        new(OpCodes.Ldloc_S, orig[i].operand),
                        new(OpCodes.Newobj, typeof(Microsoft.Xna.Framework.Vector2).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, CallingConventions.HasThis, new Type[]{typeof(float), typeof(float)}, null)),
                        new(OpCodes.Call, typeof(Microsoft.Xna.Framework.Vector2).GetMethod("op_Addition", BindingFlags.Public | BindingFlags.Static, null, new Type[]{typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Vector2)}, null)),
                    });
                    ++i; // also omit following add instruction
                }
                // scale param is a vec2 now, so use a different overload for
                // SpriteBatch.Draw
                else if (drawCount < 1 && orig[i].opcode == OpCodes.Callvirt && (orig[i].operand as MethodInfo)?.Name == nameof(SpriteBatch.Draw))
                {
                    ++drawCount;
                    Log.Trace($"NPC.DrawBreathing: replacing Draw at {i}");
                    ret.Add(new CodeInstruction(OpCodes.Callvirt, typeof(Microsoft.Xna.Framework.Graphics.SpriteBatch).GetMethod("Draw", BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(Microsoft.Xna.Framework.Graphics.Texture2D), typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Rectangle), typeof(Microsoft.Xna.Framework.Color), typeof(float), typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Vector2), typeof(Microsoft.Xna.Framework.Graphics.SpriteEffects), typeof(float) }, null)));
                }
                else
                {
                    ret.Add(orig[i]);
                }
            }

            if (whiteCount < 1 || scaleCount < 1 || drawCount < 1)
            {
                Log.Error($"NPC.DrawBreathing: some transpiler targets were not found. Aborting edit.");
                return orig;
            }

            return ret;
        }
    }

    [HarmonyPatch(typeof(NPC), nameof(NPC.isMarried))]
    public static class NpcIsMarriedNotReallyInSomeCasesPatch
    {
        public static void Postfix(NPC __instance, ref bool __result)
        {
            if (!NpcMarriageScheduleContextPatch.IsActive)
                return;

            var dict = Game1.content.Load<Dictionary<string, NpcExtensionData>>("spacechase0.SpaceCore/NpcExtensionData");
            if (!dict.TryGetValue(__instance.Name, out var npcEntry))
                return;

            if (npcEntry.IgnoreMarriageSchedule)
                __result = false;
        }
    }

    [HarmonyPatch]
    public static class NpcMarriageScheduleContextPatch
    {
        [ThreadStatic]
        private static int depth;

        public static bool IsActive => depth > 0;

        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.reloadData), Type.EmptyTypes);
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.reloadSprite), new[] { typeof(bool) });
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.getHome), Type.EmptyTypes);
            yield return AccessTools.Method(typeof(NPC), "prepareToDisembarkOnNewSchedulePath", Type.EmptyTypes);
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.parseMasterSchedule), new[] { typeof(string), typeof(string) });
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.TryLoadSchedule), Type.EmptyTypes);
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.resetForNewDay), new[] { typeof(int) });
            yield return AccessTools.Method(typeof(NPC), nameof(NPC.dayUpdate), new[] { typeof(int) });
        }

        public static void Prefix()
        {
            depth++;
        }

        public static Exception Finalizer(Exception __exception)
        {
            depth--;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(NPC), "loadCurrentDialogue")]
    public static class NpcLoadCurrentDialogueFakeNotMarriedPatch
    {
        public static void Prefix(NPC __instance, ref string __state)
        {
            var dict = Game1.content.Load<Dictionary<string, NpcExtensionData>>("spacechase0.SpaceCore/NpcExtensionData");
            if (!dict.TryGetValue(__instance.Name, out var npcEntry))
                return;

            if (!npcEntry.IgnoreMarriageSchedule)
                return;

            __state = null;
            if (Game1.player.spouse == __instance.Name)
            {
                __state = Game1.player.spouse;
                Game1.player.spouse = "";
            }
        }
        public static Exception Finalizer(ref string __state, Exception __exception)
        {
            if (__state != null)
                Game1.player.spouse = __state;

            return __exception;
        }
    }
}
