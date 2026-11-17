// Old import pipeline, compiled out by SPEC-0002 T0.2 (plan D15). Deleted by the plan that replaces it.
#if RO_LEGACY_IMPORTERS
using GRF.FileFormats.GatFormat;
using GRF.FileFormats.RsmFormat;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Uninarok.Data;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Assets.Editor
{
    [ScriptedImporter(1, "gat")]
    internal class GatImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            // 1. Instancia o parser da biblioteca do GRFEditor
            var gatData = new Gat(ctx.assetPath);

            // 2. Cria o GameObject raiz que simulará o comportamento do Prefab/FBX
            GameObject modelRoot = new GameObject(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath));
            ctx.AddObjectToAsset("root", modelRoot);

            var tileData = new int[gatData.Width * gatData.Height];
            foreach (var cell in gatData.Cells)
            {
                if (cell.Type == GatType.NoWalkable)
                {

                }
            }

            var gatImg = gatData.Image;
            gatImg.Flip(GRF.Image.FlipDirection.Vertical);
            var texture2d = new Texture2D(gatImg.Width, gatImg.Height);
            texture2d.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath) + "_Texture";
            var pixels = new Color32[gatImg.Width * gatImg.Height];

            for (var i = 0; i < pixels.Length; ++i)
            {
                var gatPixel = gatImg.Pixels[i];
                var grayScale = gatPixel / 8f;

                if (gatPixel == (byte)GatType.NoWalkable || gatPixel == (byte)GatType.NoWalkableNoSnipable || gatPixel == (byte)GatType.NoWalkableSnipable)
                {
                    var color = UnityEngine.Color.black;
                    pixels[i] = color;
                }
                else if (gatPixel == (byte)GatType.Walkable || gatPixel == (byte)GatType.Walkable2 || gatPixel == (byte)GatType.Walkable3)
                {
                    var color = UnityEngine.Color.blue;
                    pixels[i] = color;
                }
                else
                {
                    var color = UnityEngine.Color.red;
                    pixels[i] = color;
                }

                pixels[i] = new UnityEngine.Color(grayScale, grayScale, grayScale);
            }
            texture2d.SetPixels32(pixels, 0);
            texture2d.Apply();

            ctx.AddObjectToAsset("map_texture", texture2d);

            ctx.SetMainObject(modelRoot);
            
            AssignDataToSO(ctx, tileData, gatData.Width, gatData.Height, texture2d);
        }

        private void AssignDataToSO(AssetImportContext ctx, in int[] tileData, int mapWidth, int mapHeight, Texture gatImage)
        {
            var mapAssetGuids = AssetDatabase.FindAssets("{ctx.assetPath} t:MapData");
            return;
            MapData mapData = null;
            if (mapAssetGuids.Length > 0)
            {
                mapData = AssetDatabase.LoadAssetAtPath<Uninarok.Data.MapData>(AssetDatabase.GUIDToAssetPath(mapAssetGuids[0]));
            }
            else
            {
                // Se não houver um MapData existente, você pode criar um novo
                mapData = ScriptableObject.CreateInstance<Uninarok.Data.MapData>();
                mapData.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath) + "_MapData";
                // Atribua os dados do GAT ao novo ScriptableObject
                // Exemplo: newMapData.SetTileData(gatData.TileData);
                // Salve o novo ScriptableObject como um asset
                //AssetDatabase.CreateAsset(newMapData, $"Assets/Data/Maps/{System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath)}.asset");
                ctx.AddObjectToAsset("map_data", mapData);
            }

            if (mapData != null)
            {
                mapData.SetMapData(tileData, mapWidth, mapHeight, gatImage);

                EditorUtility.SetDirty(mapData);
            }
        }
    }
}
#endif
