using GRF.FileFormats.RsmFormat; // Namespace do Tokeiburu para RSM
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor.AssetImporters;
using UnityEngine;
using static UnityEngine.Audio.GeneratorInstance;
using Mesh = UnityEngine.Mesh;
using System.Text;
using GRF;
using GRF.Core;
using Utilities.Services;

namespace UnityEditor
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
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
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

            // Construção geométrica dos Submeshes
            foreach (var rsmMesh in rsmData.Meshes)
            {
                GameObject subMeshGo = new GameObject(rsmMesh.Name);
                rsmMesh.GlobalPosition.Deconstruct(out var pX, out var pY, out var pZ);
                subMeshGo.transform.SetParent(modelRoot.transform);
                
                // Setup mesh's position, rotation and scale
                var subMeshLocalPos = rsmMesh.GlobalPosition * 0.1f;
                var subMeshRotationAxis = new Vector3(rsmMesh.GlobalRotationAxis.X, rsmMesh.GlobalRotationAxis.Y, rsmMesh.GlobalRotationAxis.Z);
                var subMeshAngleInDegrees = rsmMesh.GlobalRotationAngle * Mathf.Rad2Deg;
                subMeshGo.transform.position = new Vector3(subMeshLocalPos.X, -subMeshLocalPos.Y, -subMeshLocalPos.Z);
                subMeshGo.transform.rotation = Quaternion.AngleAxis(subMeshAngleInDegrees, subMeshRotationAxis);
                subMeshGo.transform.localScale = new Vector3(rsmMesh.GlobalScale.X, rsmMesh.GlobalScale.Y, rsmMesh.GlobalScale.Z);

                Mesh unityMesh = new Mesh();
                unityMesh.name = rsmMesh.Name;

                // Listas finais que serão enviadas ao Unity Mesh
                List<Vector3> finalVertices = new List<Vector3>();
                List<Vector2> finalUvs = new List<Vector2>();

                // Dicionário para separar os triângulos baseado no ID do Material correspondente
                Dictionary<int, List<int>> subMeshTriangles = new Dictionary<int, List<int>>();

                // Aplica matriz de transformacao
                var meshTransformedVertices = new Vector3[rsmMesh.Vertices.Count];
                var mat = rsmMesh.TransformationMatrix;
                Debug.LogError($"Matrix: {mat}\nRotation: {mat.ExtractRotation()}\nScale: {mat.ExtractScale()}");

                var angles = mat.ExtractRotation().ToEulerAngles() * Mathf.Rad2Deg;
                var scale = mat.ExtractScale();
                //TODO: Try to apply rotation directly in vertices
                //subMeshGo.transform.localRotation *= Quaternion.Euler(angles.X, angles.Y, angles.Z);
                var eulerRotation = Quaternion.Euler(angles.X, -angles.Y, angles.Z);
                //TODO: Scale differs from previous one
                //subMeshGo.transform.localScale = new Vector3(scale.X, scale.Y, scale.Z);
                for (var i = 0; i < meshTransformedVertices.Length; ++i)
                {
                    var originalVertice = rsmMesh.Vertices[i];

                    //var tx = originalVertice.X * mat.M11 + originalVertice.Y * mat.M12 + originalVertice.Z * mat.M13;
                    //var ty = originalVertice.X * mat.M21 + originalVertice.Y * mat.M22 + originalVertice.Z * mat.M23;
                    //var tz = originalVertice.X * mat.M31 + originalVertice.Y * mat.M32 + originalVertice.Z * mat.M33;
                    var tx = originalVertice.X * scale.X;
                    var ty = originalVertice.Y * scale.Y;
                    var tz = originalVertice.Z * scale.Z;

                    // 2. Mudança de Base do Ragnarok (Z-Up) para Unity (Y-Up):
                    // Unity X = Ragnarok X (tx)
                    // Unity Y = Ragnarok Z (tz)
                    // Unity Z = Ragnarok Y (ty)
                    // Aplicamos o fator de escala padrão (0.1f)
                    meshTransformedVertices[i] = eulerRotation * (new Vector3(tx, -ty, -tz) * 0.1f);
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
                        var vertexOrder = new int[] { 0, 1, 2 };
                        var mappedIndex = vertexOrder[i];

                        int vIndex = face.VertexIds[mappedIndex];
                        int uvIndex = face.TextureVertexIds[mappedIndex];

                        // Pega a posição 3D original do RSM
                        var rsmVert = meshTransformedVertices[vIndex];
                        Vector3 unityPos = new Vector3(rsmVert.x, rsmVert.y, rsmVert.z);

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
                unityMesh.vertices = finalVertices.ToArray();
                unityMesh.uv = finalUvs.ToArray();

                // Configura os Submeshes de geometria baseados na quantidade de texturas distintas
                unityMesh.subMeshCount = subMeshTriangles.Count;
                List<Material> meshRendererMaterials = new List<Material>();

                int subMeshIndex = 0;
                var meshTextureIndexes = rsmMesh.TextureIndexes;
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
                        var newMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
                        meshRendererMaterials.Add(newMat);
                    }
                    subMeshIndex++;
                }

                // Recalcula dados físicos do modelo
                unityMesh.RecalculateNormals();
                unityMesh.RecalculateBounds();

                MeshFilter filter = subMeshGo.AddComponent<MeshFilter>();
                filter.sharedMesh = unityMesh;

                MeshRenderer renderer = subMeshGo.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = meshRendererMaterials.ToArray();

                ctx.AddObjectToAsset($"mesh_{rsmMesh.Name}", unityMesh);
            }

            ctx.AddObjectToAsset("main_model", modelRoot);
            ctx.SetMainObject(modelRoot);
        }
    }

}