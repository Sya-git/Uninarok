using System;
using System.Collections.Generic;
using System.Text;
using Uninarok.Data;
using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    internal class AssetPostProcessor : AssetPostprocessor
    {
        private const string MAP_ASSET_PATH = "Assets/Data/Map";
        private void OnPostprocessPrefab(UnityEngine.GameObject gameObject)
        {
            // Verifica se o prefab é um "MapData" e se o nome do prefab corresponde ao nome do arquivo GAT
            if (gameObject.name.EndsWith(".gat", StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Processing prefab: {gameObject.name}");
                // Obtém o caminho do arquivo GAT correspondente
                string gatFilePath = System.IO.Path.ChangeExtension(assetPath, ".gat");
                
            }
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach(var assetPath in importedAssets)
            {
                if (assetPath.EndsWith(".gat", StringComparison.OrdinalIgnoreCase))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    var assetSoGuid = AssetDatabase.FindAssets($"{asset.name} t:MapData");
                    MapData assetSo = null;
                    if (assetSoGuid.Length > 0)
                    {
                        assetSo = AssetDatabase.LoadAssetByGUID<MapData>(new GUID(assetSoGuid[0]));
                    }
                    else
                    {
                        var folders = MAP_ASSET_PATH.Split('/');
                        var currentPath = folders[0];
                        for (var i = 1; i < folders.Length; i++)
                        {
                            var folderPath = $"{currentPath}/{folders[i]}";
                            if (!AssetDatabase.IsValidFolder(folderPath))
                            {
                                AssetDatabase.CreateFolder(currentPath, folders[i]);
                            }

                            currentPath = folderPath;
                        }

                        assetSo = ScriptableObject.CreateInstance<MapData>();
                        var mapAssetPath = $"{MAP_ASSET_PATH}/{System.IO.Path.GetFileNameWithoutExtension(assetPath)}.asset";
                        AssetDatabase.CreateAsset(assetSo, mapAssetPath);
                    }
                    
                }
            }
        }
    }
}
