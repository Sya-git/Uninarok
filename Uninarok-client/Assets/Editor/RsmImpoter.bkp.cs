using GRF.FileFormats.RsmFormat; // Namespace do Tokeiburu para RSM
using GRF.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using Mesh = UnityEngine.Mesh;
using UnityEditor;
using Utilities.Services;

namespace Uninarok.Editor
{
    //[ScriptedImporter(1, "rsm")]
    public class RsmImporterBkp : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            EncodingService.DisplayEncoding = EncodingService.Korean;
            BuildMesh1(ctx);
            //BuildMesh2(ctx);
        }

        private void BuildMesh1(AssetImportContext ctx)
        {
            // 1. Instancia o parser da biblioteca do GRFEditor
            Rsm rsmData = new Rsm(ctx.assetPath);

            // 2. Cria o GameObject raiz que simulará o comportamento do Prefab/FBX
            GameObject modelRoot = new GameObject(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath));

            // Determina dinamicamente o Shader correto baseado na Pipeline ativa do projeto (URP vs Built-in)
            // TODO: Can be turned into a useful static function
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
            if (defaultShader == null)
            {
                defaultShader = Shader.Find("Standard"); // Fallback caso use a pipeline antiga
            }

            if (defaultShader == null)
            {
                defaultShader = Shader.Find("Unlit/Texture");
            }

            if (defaultShader == null)
            {
                throw new InvalidOperationException("Nenhum shader compatível foi encontrado para importar o RSM.");
            }

            // Dicionário para carregar e reutilizar os materiais das texturas mapeadas no RSM
            Dictionary<int, Material> rsmMaterials = new Dictionary<int, Material>();

            for (int i = 0; i < rsmData.Textures.Count; i++)
            {
                // O RSM salva caminhos como: "data\texture\åóææ\casa.bmp" ou "data\texture\grid.bmp"
                string rawBmpPath = rsmData.Textures[i];

                // Padroniza as barras invertidas do Windows para o padrão do Unity/Unix
                string normalizedPath = rawBmpPath.Replace('\\', '/').TrimStart('/');

                // Procura a textura relativa à raiz dos recursos do projeto.
                string unityTexturePath = Path.Combine("Assets/Res/data/texture", normalizedPath).Replace('\\', '/');

                // Tenta carregar a textura nativa do projeto
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(unityTexturePath);

                // Cria o material e injeta a textura correspondente para anular o Rosa (Magenta)
                Material mat = new Material(defaultShader);
                mat.name = Path.GetFileNameWithoutExtension(normalizedPath);

                if (texture != null)
                {
                    // No URP o mapa principal chama-se "_BaseMap". No Standard chama-se "_MainTex"
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                    else mat.SetTexture("_MainTex", texture);
                    if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
                    mat.doubleSidedGI = true;
                    if (mat.HasProperty("_SpecColor")) mat.SetColor("_SpecColor", Color.clear);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
                }
                else
                {
                    Debug.LogWarning($"[RSM Importer] Textura não encontrada no projeto: {unityTexturePath}");
                }

                // Salva o material gerado dentro do contêiner do Asset para não virar lixo de memória
                ctx.AddObjectToAsset($"mat_{i}", mat);
                rsmMaterials.Add(i, mat);
            }

            Material missingTextureMaterial = null;
            var meshIndex = 0;
            // Construção geométrica dos Submeshes
            foreach (var rsmMesh in rsmData.Meshes)
            {
                var subMeshGo = new GameObject(rsmMesh.Name);
                subMeshGo.transform.SetParent(modelRoot.transform, false);

                // Listas finais que serão enviadas ao Unity Mesh
                List<Vector3> finalVertices = new List<Vector3>();
                List<Vector2> finalUvs = new List<Vector2>();

                // Dicionário para separar os triângulos baseado no ID do Material correspondente
                Dictionary<int, List<int>> subMeshTriangles = new Dictionary<int, List<int>>();
                var vertexMap = new Dictionary<long, int>();

                var matrix = rsmMesh.TransformationMatrix;

                foreach (var face in rsmMesh.Faces)
                {
                    int texId = face.TextureId;
                    if (texId < 0 || texId >= rsmMesh.TextureIndexes.Count)
                    {
                        Debug.LogWarning($"[RSM Importer] Face com índice de textura inválido ({texId}) em {rsmMesh.Name}.");
                        continue;
                    }

                    bool validFace = true;
                    for (int i = 0; i < 3; i++)
                    {
                        int vertexId = face.VertexIds[i];
                        int textureVertexId = face.TextureVertexIds[i];
                        if (vertexId < 0 || vertexId >= rsmMesh.Vertices.Count || textureVertexId < 0 || textureVertexId >= rsmMesh.TextureVertices.Count)
                        {
                            validFace = false;
                            break;
                        }
                    }

                    if (!validFace)
                    {
                        Debug.LogWarning($"[RSM Importer] Face com índice de vértice/UV inválido em {rsmMesh.Name}.");
                        continue;
                    }

                    if (!subMeshTriangles.ContainsKey(texId))
                    {
                        subMeshTriangles[texId] = new List<int>();
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        var mappedIndex = i == 0 ? 0 : 3 - i;

                        int vIndex = face.VertexIds[mappedIndex];
                        int uvIndex = face.TextureVertexIds[mappedIndex];

                        long vertexKey = ((long)vIndex << 32) | (uint)uvIndex;
                        if (!vertexMap.TryGetValue(vertexKey, out int unityVertexIndex))
                        {
                            var sourceVertex = rsmMesh.Vertices[vIndex];
                            var transformedVertex = new TkVector3(sourceVertex.X, sourceVertex.Y, sourceVertex.Z) * matrix;
                            if (rsmMesh == rsmData.MainMesh)
                            {
                                transformedVertex -= rsmMesh.GlobalPosition;
                            }
                            var unityPosition = new Vector3(transformedVertex.X, -transformedVertex.Y, transformedVertex.Z) * 0.1f;
                            var rsmUv = rsmMesh.TextureVertices[uvIndex];
                            Vector2 unityUv = new Vector2(rsmUv.U, 1f - rsmUv.V);

                            unityVertexIndex = finalVertices.Count;
                            vertexMap.Add(vertexKey, unityVertexIndex);
                            finalVertices.Add(unityPosition);
                            finalUvs.Add(unityUv);
                        }

                        subMeshTriangles[texId].Add(unityVertexIndex);
                    }
                }

                // Atribui os novos arrays reconstruídos com tamanhos idênticos
                Mesh unityMesh = new Mesh
                {
                    name = rsmMesh.Name,
                    vertices = finalVertices.ToArray(),
                    uv = finalUvs.ToArray(),

                    // Configura os Submeshes de geometria baseados na quantidade de texturas distintas
                    subMeshCount = subMeshTriangles.Count
                };
                if (finalVertices.Count > 65535)
                {
                    unityMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                }

                MeshFilter filter = subMeshGo.AddComponent<MeshFilter>();
                filter.sharedMesh = unityMesh;

                var meshRendererMaterials = new List<Material>();
                int subMeshIndex = 0;
                foreach (var kvp in subMeshTriangles)
                {
                    unityMesh.SetTriangles(kvp.Value.ToArray(), subMeshIndex);
                    var texIndex = rsmMesh.TextureIndexes[kvp.Key];
                    if (rsmMaterials.TryGetValue(texIndex, out Material assignedMat))
                    {
                        meshRendererMaterials.Add(assignedMat);
                    }
                    else
                    {
                        if (missingTextureMaterial == null)
                        {
                            missingTextureMaterial = new Material(defaultShader);
                            missingTextureMaterial.name = "Missing RSM Texture";
                            if (missingTextureMaterial.HasProperty("_BaseColor")) missingTextureMaterial.SetColor("_BaseColor", Color.magenta);
                            else if (missingTextureMaterial.HasProperty("_Color")) missingTextureMaterial.SetColor("_Color", Color.magenta);
                            ctx.AddObjectToAsset("missing_texture", missingTextureMaterial);
                        }

                        Debug.LogWarning($"[RSM Importer] Índice de textura {texIndex} inválido em {rsmMesh.Name}.");
                        meshRendererMaterials.Add(missingTextureMaterial);
                    }
                    subMeshIndex++;
                }

                // Recalcula dados físicos do modelo
                unityMesh.RecalculateNormals();
                unityMesh.RecalculateBounds();

                
                MeshRenderer renderer = subMeshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = meshRendererMaterials.ToArray();

                ctx.AddObjectToAsset($"mesh_{meshIndex++}", unityMesh);
            }

            ctx.AddObjectToAsset("main_model", modelRoot);
            ctx.SetMainObject(modelRoot);
        }
    }

}