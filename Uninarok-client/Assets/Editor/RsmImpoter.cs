// Old import pipeline, compiled out by SPEC-0002 T0.2 (plan D15). Deleted by the plan that replaces it.
#if RO_LEGACY_IMPORTERS
using GRF.FileFormats.RsmFormat; // Namespace do Tokeiburu para RSM
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using Mesh = UnityEngine.Mesh;
using GRF;
using GRF.Core;
using UnityEditor;
using Utilities.Services;
using GRF.Graphics;

namespace Uninarok.Editor
{
    [ScriptedImporter(1, "rsm")]
    public class RsmImporter : ScriptedImporter
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
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (defaultShader == null)
            {
                defaultShader = Shader.Find("Standard"); // Fallback caso use a pipeline antiga
            }

            // Dicionário para carregar e reutilizar os materiais das texturas mapeadas no RSM
            Dictionary<int, Material> rsmMaterials = new Dictionary<int, Material>();

            for (int i = 0; i < rsmData.Textures.Count; i++)
            {
                // O RSM salva caminhos como: "data\texture\åóææ\casa.bmp" ou "data\texture\grid.bmp"
                string rawBmpPath = rsmData.Textures[i];

                // Padroniza as barras invertidas do Windows para o padrão do Unity/Unix
                string normalizedPath = rawBmpPath.Replace("\\", "/");

                // Procura a textura assumindo que a pasta raiz do GRF está dentro de "Assets" (ex: Assets/data/texture/...)
                string unityTexturePath = Path.Combine("Assets/Res/data/texture", normalizedPath);

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

                    // Alpha
                    mat.SetFloat("_AlphaClip", 1);
                    // Enable drawing on both sides of vertices
                    //TODO: Enable when drawing in both faces, figure out where it is in the rsm
                    mat.SetFloat("_Cull", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.doubleSidedGI = true;
                    mat.SetColor("_SpecColor", Color.clear);
                    mat.SetFloat("_Smoothness", 0.0f);
                    mat.EnableKeyword("_DOUBLE_SIDED_NORMALS");
                }
                else
                {
                    Debug.LogWarning($"[RSM Importer] Textura não encontrada no projeto: {unityTexturePath}");
                }

                // Salva o material gerado dentro do contêiner do Asset para não virar lixo de memória
                ctx.AddObjectToAsset($"mat_{i}", mat);
                rsmMaterials.Add(i, mat);
            }

            var meshesGameObjects = new Dictionary<string, GameObject>();
            // Construção geométrica dos Submeshes
            foreach (var rsmMesh in rsmData.Meshes)
            {
                var parentName = rsmMesh.ParentName;
                var subMeshGo = new GameObject(rsmMesh.Name);
                meshesGameObjects[rsmMesh.Name] = subMeshGo;

                // Setup mesh's position, rotation and scale
                var subMeshLocalPos = rsmMesh.LocalPosition;
                var globalRotationMatrix = TkMatrix3.Identity;
                if (rsmMesh.GlobalRotationAxis.Length != 0 && rsmMesh.GlobalRotationAngle != 0)
                {
                    TkMatrix3.CreateFromAxisAngle(rsmMesh.GlobalRotationAxis, rsmMesh.GlobalRotationAngle, out globalRotationMatrix);
                }

                var tfMatrix = rsmMesh.TransformationMatrix;
                var localRotation = tfMatrix.ExtractRotation().ToEulerAngles() * Mathf.Rad2Deg;
                var localScale = tfMatrix.ExtractScale();
                if (!string.IsNullOrEmpty(parentName) && meshesGameObjects.TryGetValue(parentName, out var parent))
                {
                    subMeshGo.transform.SetParent(parent.transform, false);
                }
                else
                {
                    subMeshGo.transform.SetParent(modelRoot.transform, false);
                }
                //subMeshGo.transform.localPosition = subMeshLocalPos * 0.1f;
                //subMeshGo.transform.localRotation = Quaternion.Euler(localRotation.ToVector3());
                //subMeshGo.transform.localScale = localScale.ToVector3();

                var globalRotation = rsmMesh.GlobalRotationAxis * (rsmMesh.GlobalRotationAngle * Mathf.Rad2Deg);
                Debug.LogError($"GP:{rsmMesh.GlobalPosition} | LP:{subMeshLocalPos} | GR:{globalRotation} | LR:{localRotation} | GS:{rsmMesh.GlobalScale} | LS:{localScale}");

                // Listas finais que serão enviadas ao Unity Mesh
                List<Vector3> finalVertices = new List<Vector3>();
                List<Vector2> finalUvs = new List<Vector2>();

                // Dicionário para separar os triângulos baseado no ID do Material correspondente
                Dictionary<int, List<int>> subMeshTriangles = new Dictionary<int, List<int>>();

                // Aplica matriz de transformacao
                var meshTransformedVertices = new Vector3[rsmMesh.Vertices.Count];
                
                //TODO: Try to apply rotation directly in vertices
                var eulerRotation = Quaternion.Euler(localRotation.X, -localRotation.Y, localRotation.Z);
                for (var i = 0; i < meshTransformedVertices.Length; ++i)
                {
                    var originalVertice = rsmMesh.Vertices[i];

                    // Apply local rotation and scale from the transformation matrix
                    originalVertice = originalVertice * tfMatrix;

                    if (string.IsNullOrEmpty(parentName))
                    {
                        // Apply position
                        var globalPos = new TkVector3(rsmMesh.GlobalPosition.X, -rsmMesh.GlobalPosition.Y, rsmMesh.GlobalPosition.Z);

                        originalVertice = originalVertice * globalRotationMatrix; // Rotation
                        originalVertice *= rsmMesh.GlobalScale;
                        originalVertice -= globalPos;
                        originalVertice += subMeshLocalPos;
                    }

                    var unityVertice = new Vector3(originalVertice.X, -originalVertice.Y, originalVertice.Z) * 0.1f;

                    meshTransformedVertices[i] = unityVertice;
                    //meshTransformedVertices[i] = originalVertice.ToVector3();
                }

                int currentVertexIndex = 0;
                
                // Em vez de iterar pelos vértices soltos, iteramos diretamente pelas FACES (Triângulos)
                foreach (var face in rsmMesh.Faces)
                {
                    int texId = face.TextureId;
                    if (!subMeshTriangles.ContainsKey(texId))
                    {
                        subMeshTriangles[texId] = new List<int>();
                    }

                    // Cada face no RSM possui 3 índices de vértices e 3 índices de texturas (UV)
                    // face.VertexIds -> índices de 0 a 2
                    // face.TextureVertexIds -> os índices de mapeamento de textura equivalentes
                    for (int i = 0; i < 3; i++)
                    {
                        var vertexOrder = new int[] { 0, 2, 1 };
                        var mappedIndex = vertexOrder[i];

                        int vIndex = face.VertexIds[mappedIndex];
                        int uvIndex = face.TextureVertexIds[mappedIndex];

                        // Pega a posição 3D transformada do RSM
                        Vector3 unityPos = meshTransformedVertices[vIndex];

                        // Pega a coordenada UV original do RSM
                        var rsmUv = rsmMesh.TextureVertices[uvIndex];
                        Vector2 unityUv = new Vector2(rsmUv.U, 1f - rsmUv.V);

                        // Adiciona como um novo par único de vértice/uv
                        finalVertices.Add(unityPos);
                        finalUvs.Add(unityUv);

                        // Adiciona o índice gerado sequencialmente para montar o triângulo
                        subMeshTriangles[texId].Add(currentVertexIndex);
                        currentVertexIndex++;
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
                    subMeshIndex++;
                }

                // Recalcula dados físicos do modelo
                unityMesh.RecalculateNormals();
                unityMesh.RecalculateBounds();

                
                MeshRenderer renderer = subMeshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = meshRendererMaterials.ToArray();

                ctx.AddObjectToAsset($"mesh_{rsmMesh.Name}", unityMesh);
            }

            ctx.AddObjectToAsset("main_model", modelRoot);
            ctx.SetMainObject(modelRoot);
        }
    }

}
#endif
