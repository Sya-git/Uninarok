// Old import pipeline, compiled out by SPEC-0002 T0.2 (plan D15). Deleted by the plan that replaces it.
#if RO_LEGACY_IMPORTERS
using GRF;
using GRF.FileFormats.GndFormat;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;
using Utilities.Services;
using UnityEditor;

namespace Uninarok.Editor
{
    [ScriptedImporter(1, "gnd")]
    public class GNDImporter : ScriptedImporter
    {
        private const float TILE_HEIGHT_MULTIPLIER = .1f;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            EncodingService.DisplayEncoding = EncodingService.Korean;

            var gnd = new Gnd(ctx.assetPath);

            GameObject modelRoot = new GameObject(System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath));

            var texMaterials = new Dictionary<int, Material>();

            // TODO: Can be turned into a useful static function
            // TODO: Duplicate in RsmImporter
            Shader defaultShader = Shader.Find("Universal Render Pipeline/Unlit");
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
                    UnityEngine.Debug.LogWarning($"[RSM Importer] Textura não encontrada no projeto: {unityTexturePath}");
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

                    DrawUpTile(cube, tile, new Vector2(x, z), tileSize, vertices, uvs, triangles, ref currentVertexIndex);
                }

                if (cube.TileFront > 0)
                {
                    var tile = gnd.Tiles[cube.TileFront];
                    if (!triangles.ContainsKey(tile.TextureIndex))
                    {
                        triangles.Add(tile.TextureIndex, new List<int>());
                    }

                    var frontCubeIndex = i + width;
                    if (frontCubeIndex < gnd.Cubes.Count)
                    {
                        var frontCube = gnd.Cubes[frontCubeIndex];
                        DrawFrontWall(cube, frontCube, tile, new Vector2(x, z), tileSize, vertices, uvs, triangles, ref currentVertexIndex);
                    }

                    //var backCubeIndex = i - width;
                    //if (backCubeIndex > 0)
                    //{
                    //    var backCube = gnd.Cubes[backCubeIndex];
                    //    DrawFrontWall(backCube, cube, tile, new Vector2(x, z - tileSize), tileSize, vertices, uvs, triangles, ref currentVertexIndex);
                    //}
                }

                if (cube.TileSide > 0)
                {
                    var tile = gnd.Tiles[cube.TileSide];
                    if (!triangles.ContainsKey(tile.TextureIndex))
                    {
                        triangles.Add(tile.TextureIndex, new List<int>());
                    }

                    //var leftCubeIndex = x - 1;
                    //if (leftCubeIndex > 0)
                    //{
                    //    var leftCube = gnd.Cubes[leftCubeIndex + z * height];
                    //    DrawLeftWall(leftCube, cube, tile, new Vector2(x, z), tileSize, vertices, uvs, triangles, ref currentVertexIndex);
                    //}

                    var rightCubeIndex = x + 1;
                    if (rightCubeIndex < width)
                    {
                        var rightCube = gnd.Cubes[rightCubeIndex + z * height];
                        DrawLeftWall(cube, rightCube, tile, new Vector2(x + tileSize, z), tileSize, vertices, uvs, triangles, ref currentVertexIndex);
                    }
                }
            }

            var groundGo = new GameObject("GroundMesh01");
            groundGo.transform.SetParent(modelRoot.transform);

            var mesh = new Mesh
            {
                name = "GroundMesh01",
                vertices = vertices.ToArray(),
                uv = uvs.ToArray(),
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                subMeshCount = triangles.Keys.Count
            };
            MeshFilter meshFilter = groundGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;
            MeshRenderer renderer = groundGo.AddComponent<MeshRenderer>();

            var subMeshIndex = 0;
            var materials = new List<Material>();
            foreach (var key in triangles.Keys)
            {
                if (triangles[key] == null || triangles[key].Count == 0) continue;
                mesh.SetTriangles(triangles[key], subMeshIndex);
                materials.Add(rsmMaterials[key]);
                subMeshIndex++;
            }
            renderer.sharedMaterials = materials.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ctx.AddObjectToAsset("mesh_GroundMesh01", mesh);

            ctx.AddObjectToAsset("main_model", modelRoot);
            ctx.SetMainObject(modelRoot);
        }

        private void DrawFrontWall(Cube back,
                                   Cube front, 
                                   Tile tile, 
                                   in Vector2 pos, 
                                   in float tileSize, 
                                   List<Vector3> vertices, 
                                   List<Vector2> uvs, 
                                   Dictionary<int, List<int>> triangles, 
                                   ref int currentVertexIndex)
        {
            //TODO: Check if any texture or face is upside down when drawing bottom->top or top->bottom

            if (Mathf.Approximately(0.0001f, Mathf.Abs(back.TopLeft) - Mathf.Abs(front.BottomLeft))
                && Mathf.Approximately(0.0001f, Mathf.Abs(back.TopRight) - Mathf.Abs(front.BottomRight)))
            {
                return;
            }

            var v0 = new Vector3(pos.x, -back.TopLeft * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);
            var v1 = new Vector3(pos.x + tileSize, -back.TopRight * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);
            var v2 = new Vector3(pos.x + tileSize, -front.BottomRight * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);
            var v3 = new Vector3(pos.x, -front.BottomLeft * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);

            var uv0 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
            var uv1 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
            var uv2 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);
            var uv3 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);

            // Face 1
            vertices.AddRange(new Vector3[] { v3, v2, v0 });
            uvs.AddRange(new Vector2[] { uv3, uv2, uv0 });

            // Face 2
            vertices.AddRange(new Vector3[] { v1, v0, v2 });
            uvs.AddRange(new Vector2[] { uv1, uv0, uv2 });
            
            // Triangles
            triangles[tile.TextureIndex].AddRange(Enumerable.Range(currentVertexIndex, 6));
            currentVertexIndex += 6;
        }

        private void DrawLeftWall(Cube left,
                                  Cube right,
                                  Tile tile,
                                  in Vector2 pos,
                                  in float tileSize,
                                  List<Vector3> vertices,
                                  List<Vector2> uvs,
                                  Dictionary<int, List<int>> triangles,
                                  ref int currentVertexIndex)
        {
            // TODO: Maybe check for overlapping faces with TileUp

            var isRightBelow = -right.BottomLeft < -left.BottomRight && -right.TopLeft < -left.TopRight;

            var v0 = new Vector3(pos.x, -left.BottomRight * TILE_HEIGHT_MULTIPLIER, pos.y);
            var v1 = new Vector3(pos.x, -left.TopRight * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);
            var v2 = new Vector3(pos.x, -right.TopLeft * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);
            var v3 = new Vector3(pos.x, -right.BottomLeft * TILE_HEIGHT_MULTIPLIER, pos.y);

            var uv0 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
            var uv1 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
            var uv2 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);
            var uv3 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);

            // Face 1
            if (isRightBelow)
            {
                vertices.AddRange(new Vector3[] { v1, v3, v0 });
                uvs.AddRange(new Vector2[] { uv1, uv3, uv0 });
            }
            else
            {
                vertices.AddRange(new Vector3[] { v2, v3, v1 });
                uvs.AddRange(new Vector2[] { uv2, uv3, uv1 });
            }
            

            // Face 2
            if (isRightBelow)
            {
                vertices.AddRange(new Vector3[] { v3, v1, v2 });
                uvs.AddRange(new Vector2[] { uv3, uv1, uv2 });
            }
            else
            {
                vertices.AddRange(new Vector3[] { v1, v3, v0 });
                uvs.AddRange(new Vector2[] { uv1, uv3, uv0 });
            }

            // Triangles
            triangles[tile.TextureIndex].AddRange(Enumerable.Range(currentVertexIndex, 6));
            currentVertexIndex += 6;
        }

        private void DrawUpTile(Cube cube,
                                Tile tile,
                                in Vector2 pos,
                                in float tileSize,
                                List<Vector3> vertices,
                                List<Vector2> uvs,
                                Dictionary<int, List<int>> triangles,
                                ref int currentVertexIndex)
        {
            var v0 = new Vector3(pos.x, -cube.BottomLeft * TILE_HEIGHT_MULTIPLIER, pos.y);
            var v1 = new Vector3(pos.x + tileSize, -cube.BottomRight * TILE_HEIGHT_MULTIPLIER, pos.y);
            var v2 = new Vector3(pos.x + tileSize, -cube.TopRight * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);
            var v3 = new Vector3(pos.x, -cube.TopLeft * TILE_HEIGHT_MULTIPLIER, pos.y + tileSize);

            var uv0 = new Vector2(tile.TexCoords[0].X, 1 - tile.TexCoords[0].Y);
            var uv1 = new Vector2(tile.TexCoords[1].X, 1 - tile.TexCoords[1].Y);
            var uv2 = new Vector2(tile.TexCoords[3].X, 1 - tile.TexCoords[3].Y);
            var uv3 = new Vector2(tile.TexCoords[2].X, 1 - tile.TexCoords[2].Y);

            // Face 1
            vertices.AddRange(new Vector3[] { v3, v2, v0 });
            uvs.AddRange(new Vector2[] { uv3, uv2, uv0 });

            // Face 2
            vertices.AddRange(new Vector3[] { v0, v2, v1 });
            uvs.AddRange(new Vector2[] { uv0, uv2, uv1 });

            // Triangles
            triangles[tile.TextureIndex].AddRange(Enumerable.Range(currentVertexIndex, 6));
            currentVertexIndex += 6;
        }
    }
}
#endif
