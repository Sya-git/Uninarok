using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using static Uninarok.RoImport.Tests.Gnd.GndMeshTestUtil;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T4.2 (FR-3, FR-7): normals and vertex merging of PLAN-0004 4.4 (D12). Tops are one smoothing group
    // merged by position across texture seams and chunk borders; walls are flat, grouped by their face normal.
    public class RoGndNormalsTests
    {
        const float Eps = 1e-5f;

        static bool IsWall(WorldTriangle t) => Mathf.Abs(t.Normal.y) <= 1e-6f;

        static void AssertNear(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less((expected - actual).magnitude, Eps, $"{message}: expected {expected:F6}, got {actual:F6}");
        }

        /// <summary>Every top-triangle corner's vertex normal, keyed by its exact world position.</summary>
        static Dictionary<(int, int, int), List<Vector3>> TopNormalsByPosition(IReadOnlyList<RoGndMeshData> meshes)
        {
            var result = new Dictionary<(int, int, int), List<Vector3>>();
            foreach (var t in WorldTriangles(meshes).Where(t => !IsWall(t)))
                for (int k = 0; k < 3; k++)
                {
                    var p = k == 0 ? t.A : k == 1 ? t.B : t.C;
                    if (!result.TryGetValue(Bits(p), out var list)) result[Bits(p)] = list = new List<Vector3>();
                    list.Add(NormalAt(meshes, t, k));
                }
            return result;
        }

        [Test]
        public void FlatGround_IsUpAtEveryVertex_AcrossChunkBorders()
        {
            var meshes = Build(GndTestMaps.Large());               // 3 x 3 chunks
            Assert.Greater(meshes.Count, 1);
            foreach (var m in meshes)
            foreach (var n in m.Normals)
                AssertNear(Vector3.up, n, $"chunk {m.ChunkColumn},{m.ChunkRow}");
        }

        [Test]
        public void PlanarRamp_HasThePlaneNormalEverywhere()
        {
            // Ramp: 2.5 Ragnarok units higher per cube toward +x, i.e. 0.25 Unity units per 1-unit cube.
            var expected = new Vector3(-0.25f, 1f, 0f).normalized;
            var meshes = Build(GndTestMaps.Ramp());
            foreach (var t in WorldTriangles(meshes).Where(t => !IsWall(t)))
                for (int k = 0; k < 3; k++)
                    AssertNear(expected, NormalAt(meshes, t, k), t.ToString());
        }

        [Test]
        public void UnevenGround_HasOneNormalPerPosition_AcrossCubeAndChunkBorders()
        {
            var meshes = Build(Terrain());                          // 2 x 2 chunks, non-planar tops
            var groups = TopNormalsByPosition(meshes);
            Assert.IsTrue(groups.Values.Any(l => l.Count >= 4), "inner corners are shared by several triangles");
            foreach (var g in groups.Values)
                foreach (var n in g)
                    AssertNear(g[0], n, "a top position has one normal");
            Assert.IsTrue(groups.Values.Any(g => Vector3.Angle(g[0], Vector3.up) > 5f), "the field is not flat");
        }

        [Test]
        public void SmoothNormal_IsTheAngleWeightedSum()
        {
            // A 2 x 2 map whose centre corner is one Unity unit higher. The triangulation (diagonal H10-H01) is symmetric
            // under a half turn about the centre, so the centre normal is straight up.
            var gnd = GndTestMaps.Build(2, 2, (c, r, dx, dz) => c + dx == 1 && r + dz == 1 ? -10f : 0f);
            var groups = TopNormalsByPosition(Build(gnd));
            var space = new RoMapSpace(2, 2, 10f);
            AssertNear(Vector3.up, groups[Bits(space.CornerUnity(1, 1, -10f))][0], "centre");

            // The edge corner (1, 0) touches three triangles (Unity space, centre at (1, 1, 1)):
            //   cube (0,0) triangle (00,10,01): flat, normal up, angle 45 degrees at the corner;
            //   cube (0,0) triangle (10,11,01): normal (-1, 1, -1)/sqrt3, angle 60 degrees;
            //   cube (1,0) triangle (00,10,01): normal (0, 1, -1)/sqrt2, angle 90 degrees.
            // An unweighted sum would give a different direction.
            var expected = (Mathf.PI / 4f * Vector3.up
                            + Mathf.PI / 3f * new Vector3(-1, 1, -1).normalized
                            + Mathf.PI / 2f * new Vector3(0, 1, -1).normalized).normalized;
            AssertNear(expected, groups[Bits(space.CornerUnity(1, 0, 0f))][0], "edge corner, angle weighted");
        }

        [Test]
        public void TopsWithDifferentTextures_ShareTheNormalAtACommonCorner()
        {
            var meshes = Build(WithCheckerTops(Terrain()));
            var byPosition = new Dictionary<(int, int, int), (HashSet<int> textures, List<Vector3> normals)>();
            foreach (var t in WorldTriangles(meshes).Where(t => !IsWall(t)))
                for (int k = 0; k < 3; k++)
                {
                    var key = Bits(k == 0 ? t.A : k == 1 ? t.B : t.C);
                    if (!byPosition.TryGetValue(key, out var e)) byPosition[key] = e = (new HashSet<int>(), new List<Vector3>());
                    e.textures.Add(t.Texture);
                    e.normals.Add(NormalAt(meshes, t, k));
                }

            var seams = byPosition.Values.Where(e => e.textures.Count > 1).ToList();
            Assert.IsNotEmpty(seams, "the checker puts texture seams at inner corners");
            foreach (var e in seams)
                foreach (var n in e.normals)
                    AssertNear(e.normals[0], n, "one normal across the texture seam");
        }

        [Test]
        public void CliffTopCorner_IsNotSmoothedIntoTheLowerGround()
        {
            // Two flat levels joined by a cliff: every top normal stays straight up, every wall normal horizontal.
            var meshes = Build(GndTestMaps.Build(4, 4, (c, r, dx, dz) => r < 2 ? 0f : -20f));
            foreach (var t in WorldTriangles(meshes))
                for (int k = 0; k < 3; k++)
                {
                    var n = NormalAt(meshes, t, k);
                    if (IsWall(t)) Assert.AreEqual(0f, n.y, Eps, $"wall {t}");
                    else AssertNear(Vector3.up, n, $"top {t}");
                }
        }

        [Test]
        public void WallNormals_AreTheFlatFaceNormal()
        {
            foreach (var gnd in new[] { Terrain(), GndTestMaps.Wide(), GndTestMaps.Tall() })
            {
                var meshes = Build(gnd);
                var walls = WorldTriangles(meshes).Where(IsWall).ToList();
                Assert.IsNotEmpty(walls);
                foreach (var t in walls)
                    for (int k = 0; k < 3; k++)
                        AssertNear(t.Normal.normalized, NormalAt(meshes, t, k), $"{gnd} wall {t}");
            }
        }

        [Test]
        public void CoplanarWalls_ShareTheirCommonCorners()
        {
            // The cliff of four front walls in one plane, with one texture coordinate everywhere: 5 top and 5 bottom
            // corners, not 4 per wall (16).
            var meshes = Build(WithUniformWallUv(GndTestMaps.Build(4, 4, (c, r, dx, dz) => r < 2 ? 0f : -20f)));
            var wallVertices = new HashSet<(int, int)>();
            foreach (var t in WorldTriangles(meshes).Where(IsWall))
            {
                wallVertices.Add((t.Chunk, t.IndexA));
                wallVertices.Add((t.Chunk, t.IndexB));
                wallVertices.Add((t.Chunk, t.IndexC));
            }
            Assert.AreEqual(10, wallVertices.Count);
        }

        [Test]
        public void VertexCount_IsTheDistinctPositionUvAndNormalGroup()
        {
            // D12: a vertex per distinct (position, uv, group), group = "top" for tops or the face normal rounded to 1e-4
            // for walls; every vertex is used.
            foreach (var gnd in new[] { Terrain(), WithCheckerTops(Terrain()), GndTestMaps.Step(), GndTestMaps.Wide(), GndTestMaps.Large() })
            {
                var meshes = Build(gnd);
                var triangles = WorldTriangles(meshes);
                for (int m = 0; m < meshes.Count; m++)
                {
                    var mesh = meshes[m];
                    var keys = new HashSet<((int, int, int), (int, int), (int, int, int))>();
                    var used = new HashSet<int>();
                    foreach (var t in triangles.Where(t => t.Chunk == m))
                    {
                        var group = IsWall(t) ? Rounded(t.Normal.normalized) : (int.MinValue, 0, 0);
                        foreach (int i in new[] { t.IndexA, t.IndexB, t.IndexC })
                        {
                            used.Add(i);
                            keys.Add((Bits(mesh.Positions[i]), UvBits(mesh.Uvs[i]), group));
                        }
                    }
                    Assert.AreEqual(mesh.Positions.Length, used.Count, $"{gnd} chunk {m}: every vertex is used");
                    Assert.AreEqual(keys.Count, mesh.Positions.Length, $"{gnd} chunk {m}: one vertex per distinct key");
                }
            }
        }

        static (int, int, int) Rounded(Vector3 n) =>
            ((int)Math.Round(n.x * 1e4), (int)Math.Round(n.y * 1e4), (int)Math.Round(n.z * 1e4));

        static (int, int) UvBits(Vector2 uv) =>
            (uv.x == 0f ? 0 : BitConverter.SingleToInt32Bits(uv.x), uv.y == 0f ? 0 : BitConverter.SingleToInt32Bits(uv.y));
    }
}
