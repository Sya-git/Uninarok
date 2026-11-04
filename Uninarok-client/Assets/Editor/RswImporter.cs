using GRF.FileFormats.RswFormat; // Namespace do Tokeiburu para RSW
using GRF.FileFormats.RswFormat.RswObjects;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using Utilities.Services;
using UnityEditor;

// Atalho para evitar colisão de escopo com o 'UnityEngine.Model' se houver
using RswModel = GRF.FileFormats.RswFormat.RswObjects.Model;

namespace Uninarok.Editor
{
    [ScriptedImporter(1, "rsw")]
    public class RswImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            EncodingService.DisplayEncoding = EncodingService.Korean;

            Rsw rswWorld = new Rsw(ctx.assetPath);
            GameObject worldRoot = new GameObject("WorldRoot");

            foreach (var rswObject in rswWorld.Objects)
            {
                // O enum correto para checar modelos 3D estáticos é RswObjectType.Model
                if (rswObject.Type == RswObjectType.Model)
                {
                    // Cast correto usando a assinatura nativa da DLL (GRF.FileFormats.RswFormat.Model)
                    RswModel rsmModelData = (RswModel)rswObject;

                    // O arquivo é guardado contendo caminhos estruturados (ex: "data\model\prontera\casa.rsm")
                    // string rsmFileName = Path.GetFileName(rsmModelData.FileName);
                    string rsmFileName = Path.GetFileName(rsmModelData.ModelName);
                    string rsmNameWithoutExt = Path.GetFileNameWithoutExtension(rsmFileName);

                    // Busca o Asset .rsm que já foi importado pelo Unity
                    string[] guids = AssetDatabase.FindAssets($"{rsmNameWithoutExt} t:GameObject");

                    if (guids.Length > 0)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                        GameObject rsmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                        if (rsmPrefab != null)
                        {
                            GameObject spawnedModel = PrefabUtility.InstantiatePrefab(rsmPrefab, worldRoot.transform) as GameObject;
                            spawnedModel.name = rsmModelData.Name;

                            // Posicionamento espacial do mundo do Ragnarok escalonado para proporção da Unity
                            float scaleFactor = 0.1f;
                            spawnedModel.transform.position = new Vector3(rsmModelData.Position.X, -rsmModelData.Position.Y, rsmModelData.Position.Z) * scaleFactor;
                            spawnedModel.transform.rotation = Quaternion.Euler(rsmModelData.Rotation.X, rsmModelData.Rotation.Y, rsmModelData.Rotation.Z);
                            spawnedModel.transform.localScale = new Vector3(rsmModelData.Scale.X, rsmModelData.Scale.Y, rsmModelData.Scale.Z);
                        }
                    }
                }
            }

            ctx.AddObjectToAsset("world_root", worldRoot);
            ctx.SetMainObject(worldRoot);
        }
    }
}