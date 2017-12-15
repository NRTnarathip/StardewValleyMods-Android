﻿using JsonAssets.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonAssets
{
    public class ContentInjector : IAssetEditor
    {
        public bool CanEdit<T>(IAssetInfo asset)
        {
            if (asset.AssetNameEquals("Data\\ObjectInformation"))
                return true;
            if (asset.AssetNameEquals("Data\\Crops"))
                return true;
            if (asset.AssetNameEquals("Data\\fruitTrees"))
                return true;
            if (asset.AssetNameEquals("Data\\CookingRecipes"))
                return true;
            if (asset.AssetNameEquals("Data\\CraftingRecipes"))
                return true;
            if (asset.AssetNameEquals("Data\\BigCraftablesInformation"))
                return true;
            if (asset.AssetNameEquals("Maps\\springobjects"))
                return true;
            if (asset.AssetNameEquals("TileSheets\\crops"))
                return true;
            if (asset.AssetNameEquals("TileSheets\\fruitTrees"))
                return true;
            if (asset.AssetNameEquals("TileSheets\\Craftables"))
                return true;
            return false;
        }

        public void Edit<T>(IAssetData asset)
        {
            if (asset.AssetNameEquals("Data\\ObjectInformation"))
            {
                var data = asset.AsDictionary<int, string>().Data;
                foreach (var obj in Mod.instance.objects)
                {
                    try
                    {
                        Log.trace($"Injecting to objects: {obj.GetObjectId()}: {obj.GetObjectInformation()}");
                        data.Add(obj.GetObjectId(), obj.GetObjectInformation());
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting object information for " + obj.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("Data\\Crops"))
            {
                var data = asset.AsDictionary<int, string>().Data;
                foreach (var crop in Mod.instance.crops)
                {
                    try
                    {
                        Log.trace($"Injecting to crops: {crop.GetSeedId()}: {crop.GetCropInformation()}");
                        data.Add(crop.GetSeedId(), crop.GetCropInformation());
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting crop for " + crop.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("Data\\fruitTrees"))
            {
                var data = asset.AsDictionary<int, string>().Data;
                foreach (var fruitTree in Mod.instance.fruitTrees)
                {
                    try
                    {
                        Log.trace($"Injecting to fruit trees: {fruitTree.GetSaplingId()}: {fruitTree.GetFruitTreeInformation()}");
                        data.Add(fruitTree.GetSaplingId(), fruitTree.GetFruitTreeInformation());
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting fruit tree for " + fruitTree.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("Data\\CookingRecipes"))
            {
                var data = asset.AsDictionary<string, string>().Data;
                foreach (var obj in Mod.instance.objects)
                {
                    try
                    {
                        if (obj.Recipe == null)
                            continue;
                        if (obj.Category != ObjectData.Category_.Cooking)
                            continue;
                        Log.trace($"Injecting to cooking recipes: {obj.Name}: {obj.Recipe.GetRecipeString(obj)}");
                        data.Add(obj.Name, obj.Recipe.GetRecipeString(obj));
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting cooking recipe for " + obj.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("Data\\CraftingRecipes"))
            {
                var data = asset.AsDictionary<string, string>().Data;
                foreach (var obj in Mod.instance.objects)
                {
                    try
                    {
                        if (obj.Recipe == null)
                            continue;
                        if (obj.Category == ObjectData.Category_.Cooking)
                            continue;
                        Log.trace($"Injecting to crafting recipes: {obj.Name}: {obj.Recipe.GetRecipeString(obj)}");
                        data.Add(obj.Name, obj.Recipe.GetRecipeString(obj));
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting crafting recipe for " + obj.Name + ": " + e);
                    }
                }
                foreach (var big in Mod.instance.bigCraftables)
                {
                    try
                    {
                        if (big.Recipe == null)
                            continue;
                        Log.trace($"Injecting to crafting recipes: {big.Name}: {big.Recipe.GetRecipeString(big)}");
                        data.Add(big.Name, big.Recipe.GetRecipeString(big));
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting crafting recipe for " + big.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("Data\\BigCraftablesInformation"))
            {
                var data = asset.AsDictionary<int, string>().Data;
                foreach (var big in Mod.instance.bigCraftables)
                {
                    try
                    {
                        Log.trace($"Injecting to big craftables: {big.GetCraftableId()}: {big.GetCraftableInformation()}");
                        data.Add(big.GetCraftableId(), big.GetCraftableInformation());
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting object information for " + big.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("Maps\\springobjects"))
            {
                var oldTex = asset.AsImage().Data;
                Texture2D newTex = new Texture2D(Game1.graphics.GraphicsDevice, oldTex.Width, Math.Max(oldTex.Height, 4096));
                asset.ReplaceWith(newTex);
                asset.AsImage().PatchImage(oldTex);

                foreach (var obj in Mod.instance.objects)
                {
                    try
                    {
                        Log.trace($"Injecting {obj.Name} sprites");
                        asset.AsImage().PatchImage(obj.texture, null, objectRect(obj.GetObjectId()));
                        if (obj.IsColored)
                            asset.AsImage().PatchImage(obj.textureColor, null, objectRect(obj.GetObjectId() + 1));
                    }
                    catch ( Exception e )
                    {
                        Log.error("Exception injecting sprite for " + obj.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("TileSheets\\crops"))
            {
                var oldTex = asset.AsImage().Data;
                Texture2D newTex = new Texture2D(Game1.graphics.GraphicsDevice, oldTex.Width, Math.Max(oldTex.Height, 4096));
                asset.ReplaceWith(newTex);
                asset.AsImage().PatchImage(oldTex);

                foreach (var crop in Mod.instance.crops)
                {
                    try
                    {
                        Log.trace($"Injecting {crop.Name} crop images");
                        asset.AsImage().PatchImage(crop.texture, null, cropRect(crop.GetCropSpriteIndex()));
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting crop sprite for " + crop.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("TileSheets\\fruitTrees"))
            {
                var oldTex = asset.AsImage().Data;
                Texture2D newTex = new Texture2D(Game1.graphics.GraphicsDevice, oldTex.Width, Math.Max(oldTex.Height, 4096));
                asset.ReplaceWith(newTex);
                asset.AsImage().PatchImage(oldTex);

                foreach (var fruitTree in Mod.instance.fruitTrees)
                {
                    try
                    {
                        Log.trace($"Injecting {fruitTree.Name} fruit tree images");
                        asset.AsImage().PatchImage(fruitTree.texture, null, fruitTreeRect(fruitTree.GetFruitTreeIndex()));
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting fruit tree sprite for " + fruitTree.Name + ": " + e);
                    }
                }
            }
            else if (asset.AssetNameEquals("TileSheets\\Craftables"))
            {
                var oldTex = asset.AsImage().Data;
                Texture2D newTex = new Texture2D(Game1.graphics.GraphicsDevice, oldTex.Width, Math.Max(oldTex.Height, 4096));
                asset.ReplaceWith(newTex);
                asset.AsImage().PatchImage(oldTex);

                foreach (var big in Mod.instance.bigCraftables)
                {
                    try
                    {
                        Log.trace($"Injecting {big.Name} sprites");
                        asset.AsImage().PatchImage(big.texture, null, bigCraftableRect(big.GetCraftableId()));
                    }
                    catch (Exception e)
                    {
                        Log.error("Exception injecting sprite for " + big.Name + ": " + e);
                    }
                }
            }
        }
        private Rectangle objectRect(int index)
        {
            return new Rectangle(index % 24 * 16, index / 24 * 16, 16, 16);
        }
        private Rectangle cropRect(int index)
        {
            return new Rectangle(index % 2 * 128, index / 2 * 32, 128, 32);
        }
        private Rectangle fruitTreeRect(int index)
        {
            return new Rectangle(0, index * 80, 432, 80);
        }
        private Rectangle bigCraftableRect(int index)
        {
            return new Rectangle(index % 8 * 16, index / 8 * 32, 16, 32);
        }
    }
}
