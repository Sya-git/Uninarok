using GRF;
using GRF.FileFormats.GndFormat;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;
using Utilities.Services;

namespace UnityEditor
{
    [ScriptedImporter(1, "gnd")]
    public class GNDImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            EncodingService.DisplayEncoding = EncodingService.Korean;

            var gnd = new Gnd(ctx.assetPath);

            GameObject modelRoot = new GameObject(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath));

            var texMaterials = new Dictionary<int, Material>();

            // TODO: Can be turned into a useful static function
            // TODO: Duplicate in RsmImporter
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit");
            if (defaultShader == null)
            {
                defaultShader = Shader.Find("Standard"); // Fallback caso use a pipeline antiga
            }

            // Dicionário para carregar e reutilizar os materiais das texturas mapeadas no RSM
            Dictionary<int, Material> rsmMaterials = new Dictionary<int, Material>();
            for (int i = 0; i < gnd.Textures.Count; i++)
            {
                // O RSM salva caminhos como: "data\texture\åóææ\casa.bmp" ou "data\texture\grid.bmp"
                string rawBmpPath = gnd.Textures[i];

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
                    //mat.SetFloat("_Cull", 0f);
                    //mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    //mat.doubleSidedGI = true;
                    //mat.SetColor("_SpecColor", Color.clear);
                    //mat.SetFloat("_Smoothness", 0.0f);
                    //mat.EnableKeyword("_DOUBLE_SIDED_NORMALS");
                }
                else
                {
                    Debug.LogWarning($"[RSM Importer] Textura não encontrada no projeto: {unityTexturePath}");
                }

                // Salva o material gerado dentro do contêiner do Asset para não virar lixo de memória
                ctx.AddObjectToAsset($"mat_{i}", mat);
                rsmMaterials.Add(i, mat);
            }

            var tileSize = gnd.Header.ProportionRatio * 0.1f;
            var tileHeightMultiplier = 0.1f;
            var width = gnd.Header.Width;
            var height = gnd.Header.Height;
            for (var i = 0; i < gnd.Tiles.Count; i++)
            {
                var tile = gnd.Tiles[i];
            }

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new Dictionary<int, List<int>>();

            var currentVertexIndex = 0;
            for (var i = 0; i < width * height; i++)
            {
                var cube = gnd.Cubes[i];

                var x = i % width;
                var z = i / width;


                if (cube.TileUp > 0)
                {
                    var tile = gnd.Tiles[cube.TileUp];
                    if (!triangles.ContainsKey(tile.TextureIndex))
                    {
                        triangles.Add(tile.TextureIndex, new List<int>());
                    }

                    var v0 = new Vector3(x, -cube.BottomLeft * tileHeightMultiplier, z);
                    var v1 = new Vector3(x + tileSize, -cube.BottomRight * tileHeightMultiplier, z);
                    var v2 = new Vector3(x + tileSize, -cube.TopRight * tileHeightMultiplier, z + tileSize);
                    var v3 = new Vector3(x, -cube.TopLeft * tileHeightMultiplier, z + tileSize);

                    var uv0 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
                    var uv1 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
                    var uv2 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);
                    var uv3 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);

                    // Face 1
                    vertices.AddRange(new Vector3[] { v3, v2, v0 });
                    uvs.AddRange(new Vector2[] { uv3, uv2, uv0 });
                    triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex, currentVertexIndex + 1, currentVertexIndex + 2 });
                    
                    // Face 2
                    vertices.AddRange(new Vector3[] { v0, v2, v1 });
                    uvs.AddRange(new Vector2[] { uv0, uv2, uv1 });
                    triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex + 3, currentVertexIndex + 4, currentVertexIndex + 5 });
                    
                    currentVertexIndex += 6;
                }

                if (cube.TileFront > 0)
                {
                    var tile = gnd.Tiles[cube.TileFront];
                    if (!triangles.ContainsKey(tile.TextureIndex))
                    {
                        triangles.Add(tile.TextureIndex, new List<int>());
                    }

                    if (i + width < gnd.Cubes.Count)
                    {
                        var frontCube = gnd.Cubes[i + width];

                        var v0 = new Vector3(x, -cube.TopLeft * tileHeightMultiplier, z + tileSize);
                        var v1 = new Vector3(x + tileSize, -cube.TopRight * tileHeightMultiplier, z + tileSize);
                        var v2 = new Vector3(x + tileSize, -cube.BottomRight * tileHeightMultiplier, z + tileSize);
                        var v3 = new Vector3(x, -cube.BottomLeft * tileHeightMultiplier, z + tileSize);

                        var uv0 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
                        var uv1 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
                        var uv2 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);
                        var uv3 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);

                        // Face 1
                        vertices.AddRange(new Vector3[] { v3, v2, v0 });
                        uvs.AddRange(new Vector2[] { uv3, uv2, uv0 });
                        triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex, currentVertexIndex + 1, currentVertexIndex + 2 });

                        // Face 2
                        vertices.AddRange(new Vector3[] { v1, v0, v2 });
                        uvs.AddRange(new Vector2[] { uv1, uv0, uv2 });
                        triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex + 3, currentVertexIndex + 4, currentVertexIndex + 5 });

                        currentVertexIndex += 6;
                    }
                }

                if (cube.TileSide > 0)
                {
                    var tile = gnd.Tiles[cube.TileSide];
                    if (!triangles.ContainsKey(tile.TextureIndex))
                    {
                        triangles.Add(tile.TextureIndex, new List<int>());
                    }

                    int leftCubeIndex;
                    if ((leftCubeIndex = x - 1) >= 0)
                    {
                        var leftCube = gnd.Cubes[i - 1];

                        var v0 = new Vector3(x, -leftCube.BottomRight * tileHeightMultiplier, z);
                        var v1 = new Vector3(x + tileSize, -leftCube.BottomLeft * tileHeightMultiplier, z);
                        var v2 = new Vector3(x + tileSize, -cube.TopLeft * tileHeightMultiplier, z + tileSize);
                        var v3 = new Vector3(x, leftCube.TopRight * tileHeightMultiplier, z + tileSize);

                        var uv0 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
                        var uv1 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
                        var uv2 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);
                        var uv3 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);

                        // Face 1
                        vertices.AddRange(new Vector3[] { v3, v2, v0 });
                        uvs.AddRange(new Vector2[] { uv3, uv2, uv0 });
                        triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex, currentVertexIndex + 1, currentVertexIndex + 2 });

                        // Face 2
                        vertices.AddRange(new Vector3[] { v1, v0, v2 });
                        uvs.AddRange(new Vector2[] { uv1, uv0, uv2 });
                        triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex + 3, currentVertexIndex + 4, currentVertexIndex + 5 });

                        currentVertexIndex += 6;
                    }

                    int rightCubeIndex;
                    if ((rightCubeIndex = x + 1) < width)
                    {
                        var rightCube = gnd.Cubes[i + 1];

                        var v0 = new Vector3(x, -cube.BottomRight * tileHeightMultiplier, z);
                        var v1 = new Vector3(x + tileSize, -cube.BottomLeft * tileHeightMultiplier, z);
                        var v2 = new Vector3(x + tileSize, -rightCube.TopLeft * tileHeightMultiplier, z + tileSize);
                        var v3 = new Vector3(x, -rightCube.TopRight * tileHeightMultiplier, z + tileSize);

                        var uv0 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
                        var uv1 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
                        var uv2 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);
                        var uv3 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);

                        // Face 1
                        vertices.AddRange(new Vector3[] { v3, v2, v0 });
                        uvs.AddRange(new Vector2[] { uv3, uv2, uv0 });
                        triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex, currentVertexIndex + 1, currentVertexIndex + 2 });
                        
                        // Face 2
                        vertices.AddRange(new Vector3[] { v1, v0, v2 });
                        uvs.AddRange(new Vector2[] { uv1, uv0, uv2 });
                        triangles[tile.TextureIndex].AddRange(new int[] { currentVertexIndex + 3, currentVertexIndex + 4, currentVertexIndex + 5 });
                        
                        currentVertexIndex += 6;
                    }
                }
            }
            var mesh = new Mesh
            {
                name = "GroundMesh01",
                vertices = vertices.ToArray(),
                uv = uvs.ToArray(),
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                subMeshCount = triangles.Keys.Count
            };
            var subMeshIndex = 0;
            foreach (var key in triangles.Keys)
            {
                if (triangles[key] == null || triangles[key].Count == 0) continue;
                mesh.SetTriangles(triangles[key], subMeshIndex);
                subMeshIndex++;
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var groundGo = new GameObject("GroundMesh01");
            groundGo.transform.SetParent(modelRoot.transform);

            MeshFilter meshFilter = groundGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer renderer = groundGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = rsmMaterials.Values.ToArray();

            ctx.AddObjectToAsset("mesh_GroundMesh01", mesh);

            ctx.AddObjectToAsset("main_model", modelRoot);
            ctx.SetMainObject(modelRoot);
        }
    }
}