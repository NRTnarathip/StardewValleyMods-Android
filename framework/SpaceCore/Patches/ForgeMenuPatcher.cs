using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Spacechase.Shared.Patching;
using SpaceShared;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Objects;

namespace SpaceCore.Patches
{
    /// <summary>Applies Harmony patches to <see cref="ForgeMenu"/>.</summary>
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = DiagnosticMessages.NamedForHarmony)]
    internal class ForgeMenuPatcher : BasePatcher
    {
        /*********
        ** Fields
        *********/
        private static CustomForgeRecipe justCrafted = null;


        /*********
        ** Public methods
        *********/
        /// <inheritdoc />
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.GenerateHighlightDictionary)),
                postfix: this.GetHarmonyMethod(nameof(After_GenerateHighlightDictionary))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.IsValidCraft)),
                prefix: this.GetHarmonyMethod(nameof(Before_IsValidCraft))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.CraftItem)),
                prefix: this.GetHarmonyMethod(nameof(Before_CraftItem))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.SpendLeftItem)),
                prefix: this.GetHarmonyMethod(nameof(Before_SpendLeftItem))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.SpendRightItem)),
                prefix: this.GetHarmonyMethod(nameof(Before_SpendRightItem))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.GetForgeCost)),
                prefix: this.GetHarmonyMethod(nameof(Before_GetForgeCost))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>("_leftIngredientSpotClicked"),
                prefix: this.GetHarmonyMethod(nameof(Before_LeftIngredientSpotClicked))
            );

            harmony.Patch(
                original: this.RequireMethod<ForgeMenu>(nameof(ForgeMenu.draw), new[] { typeof(SpriteBatch) }),
                postfix: this.GetHarmonyMethod(nameof(After_Draw))
            );
        }


        /*********
        ** Private methods
        *********/
        /// <summary>The method to call after <see cref="ForgeMenu.GenerateHighlightDictionary"/>.</summary>
        private static void After_GenerateHighlightDictionary(ForgeMenu __instance)
        {
            var this__highlightDictionary_ = SpaceCore.Instance.Helper.Reflection.GetField<Dictionary<Item, bool>>(__instance, "_highlightDictionary");

            var this__highlightDictionary = this__highlightDictionary_.GetValue();
            List<Item> item_list = new List<Item>(__instance.inventory.actualInventory);
            if (Game1.player.leftRing.Value != null)
            {
                item_list.Add(Game1.player.leftRing.Value);
            }
            if (Game1.player.rightRing.Value != null)
            {
                item_list.Add(Game1.player.rightRing.Value);
            }
            foreach (Item item in item_list)
            {
                if (item == null)
                {
                    continue;
                }
                if (__instance.leftIngredientSpot.item == null && __instance.rightIngredientSpot.item == null)
                {
                    foreach (var recipe in CustomForgeRecipe.Recipes)
                    {
                        if (recipe.BaseItem.HasEnoughFor(item) || recipe.IngredientItem.HasEnoughFor(item))
                            this__highlightDictionary[item] = true;
                    }
                }
            }
        }

        /// <summary>The method to call before <see cref="ForgeMenu.IsValidCraft"/>.</summary>
        /// <returns>Returns whether to run the original method.</returns>
        private static bool Before_IsValidCraft(ForgeMenu __instance, Item left_item, Item right_item, ref bool __result)
        {
            if (left_item == null || right_item == null)
                return true;

            foreach (var recipe in CustomForgeRecipe.Recipes)
            {
                if (recipe.BaseItem.HasEnoughFor(left_item) && recipe.IngredientItem.HasEnoughFor(right_item))
                {
                    __result = true;
                    return false;
                }
            }

            return true;
        }

        /// <summary>The method to call before <see cref="ForgeMenu.SpendLeftItem"/>.</summary>
        /// <returns>Returns whether to run the original method.</returns>
        private static bool Before_SpendLeftItem(ForgeMenu __instance)
        {
            if (ForgeMenuPatcher.justCrafted != null)
            {
                ForgeMenuPatcher.justCrafted.BaseItem.Consume(ref __instance.leftIngredientSpot.item);
                return false;
            }

            return true;
        }

        /// <summary>The method to call before <see cref="ForgeMenu.SpendLeftItem"/>.</summary>
        /// <returns>Returns whether to run the original method.</returns>
        private static bool Before_SpendRightItem(ForgeMenu __instance)
        {
            if (ForgeMenuPatcher.justCrafted != null)
            {
                ForgeMenuPatcher.justCrafted.IngredientItem.Consume(ref __instance.rightIngredientSpot.item);
                ForgeMenuPatcher.justCrafted = null;
                return false;
            }

            return true;
        }

        /// <summary>The method to call before <see cref="ForgeMenu.CraftItem"/>.</summary>
        /// <returns>Returns whether to run the original method.</returns>
        private static bool Before_CraftItem(ForgeMenu __instance, Item left_item, Item right_item, bool forReal, ref Item __result)
        {
            if (left_item == null || right_item == null)
                return true;

            foreach (var recipe in CustomForgeRecipe.Recipes)
            {
                if (recipe.BaseItem.HasEnoughFor(left_item) && recipe.IngredientItem.HasEnoughFor(right_item))
                {
                    if (forReal)
                        ForgeMenuPatcher.justCrafted = recipe;
                    __result = recipe.CreateResult(left_item, right_item);
                    return false;
                }
            }

            return true;
        }

        /// <summary>The method to call before <see cref="ForgeMenu.GetForgeCost"/>.</summary>
        /// <returns>Returns whether to run the original method.</returns>
        private static bool Before_GetForgeCost(ForgeMenu __instance, Item left_item, Item right_item, ref int __result)
        {
            if (left_item == null || right_item == null)
                return true;

            foreach (var recipe in CustomForgeRecipe.Recipes)
            {
                if (recipe.BaseItem.HasEnoughFor(left_item) && recipe.IngredientItem.HasEnoughFor(right_item))
                {
                    __result = recipe.CinderShardCost;
                    return false;
                }
            }

            return true;
        }

        /// <summary>Handles custom left ingredients which the base menu restricts to tools and rings.</summary>
        /// <returns>Returns whether to run the original method.</returns>
        private static bool Before_LeftIngredientSpotClicked(ForgeMenu __instance)
        {
            Item heldItem = __instance.heldItem;
            if (heldItem == null || heldItem is Tool or Ring || !IsLeftCraftIngredient(heldItem))
                return true;

            Item previousItem = __instance.leftIngredientSpot.item;
            int inventoryIndex = __instance.inventory.dragItem != -1
                ? __instance.inventory.dragItem
                : __instance.inventory.currentlySelectedItem;
            if (inventoryIndex != -1)
            {
                Utility.removeItemFromInventory(inventoryIndex, __instance.inventory.actualInventory);
            }

            __instance.inventory.currentlySelectedItem = -1;
            __instance.inventory.dragItem = -1;
            __instance.leftIngredientSpot.item = heldItem;
            __instance.heldItem = previousItem;
            if (previousItem != null)
            {
                Utility.CollectOrDrop(previousItem);
                __instance.heldItem = null;
                __instance.inventory.currentlySelectedItem = -1;
                __instance.inventory.GamePadHideInfoPanel();
            }

            SpaceCore.Instance.Helper.Reflection
                .GetField<Dictionary<Item, bool>>(__instance, "_highlightDictionary")
                .SetValue(null);
            SpaceCore.Instance.Helper.Reflection
                .GetMethod(__instance, "_ValidateCraft")
                .Invoke();
            Game1.playSound("stoneStep");
            return false;
        }

        /// <summary>Draws forge costs which have no built-in texture variant.</summary>
        private static void After_Draw(ForgeMenu __instance, SpriteBatch b)
        {
            Item leftItem = __instance.leftIngredientSpot.item;
            Item rightItem = __instance.rightIngredientSpot.item;
            if (leftItem == null || rightItem == null || !__instance.IsValidCraft(leftItem, rightItem))
                return;

            int cost = __instance.GetForgeCost(leftItem, rightItem);
            if (cost is not (10 or 15 or 20))
            {
                Vector2 position = new(
                    __instance.rightIngredientSpot.bounds.X - 4,
                    __instance.rightIngredientSpot.bounds.Y + 108
                );
                b.DrawString(Game1.dialogueFont, $"x{cost}", position, new Color(226, 124, 65));
            }
        }

        private static bool IsLeftCraftIngredient(Item item)
        {
            if (item == null)
                return false;
            foreach (var recipe in CustomForgeRecipe.Recipes)
            {
                if (recipe.BaseItem.HasEnoughFor(item))
                    return true;
            }

            return false;
        }

        private static bool IsRightCraftIngredient(Item item)
        {
            if (item == null)
                return false;
            foreach (var recipe in CustomForgeRecipe.Recipes)
            {
                if (recipe.IngredientItem.HasEnoughFor(item))
                    return true;
            }

            return false;
        }
    }
}
