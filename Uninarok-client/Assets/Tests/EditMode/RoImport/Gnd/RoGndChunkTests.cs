using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using static Uninarok.RoImport.Tests.Gnd.GndMeshTestUtil;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T4.2 (FR-3, FR-6): the map cut into 16 x 16 cube chunks (PLAN-0004 4.4).
    public class RoGndChunkTests
    {
        const int N = RoGndMeshBuilder.ChunkSize;

        static bool IsWall(WorldTriangle t) => Mathf.Abs(t.Normal.y) <= 1e-6f;

        [Test]
        public void Large_Gives3x3Chunks_WithTheRightCubeRanges_AndOrigins()
        {
            var gnd = GndTestMaps.Large();                  // 40 x 33
            var meshes = Build(gnd);
            CollectionAssert.AreEquivalent(
                from cz in Enumerable.Range(0, 3) from cx in Enumerable.Range(0, 3) select (cx, cz),
                meshes.Select(m => (m.ChunkColumn, m.ChunkRow)));

            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);
            foreach (var m in meshes)
            {
                int x0 = m.ChunkColumn * N, z0 = m.ChunkRow * N;
                int x1 = Mathf.Min(x0 + N, gnd.Width), z1 = Mathf.Min(z0 + N, gnd.Height);
                Assert.AreEqual(space.CornerUnity(x0, z0, 0f), m.Origin, "origin: first grid corner at height 0");
                var world = m.Positions.Select(p => m.Origin + p).ToList();
                Assert.AreEqual(x0, world.Min(p => p.x), 1e-5f);
                Assert.AreEqual(x1, world.Max(p => p.x), 1e-5f);
                Assert.AreEqual(z0, world.Min(p => p.z), 1e-5f);
                Assert.AreEqual(z1, world.Max(p => p.z), 1e-5f);
            }
        }

        [Test]
        public void EachTop_IsInTheChunkOfItsCube()
        {
            var gnd = GndTestMaps.Large();
            var meshes = Build(gnd);
            var triangles = WorldTriangles(meshes);
            for (int m = 0; m < meshes.Count; m++)
            {
                var mesh = meshes[m];
                int cubes = (Mathf.Min((mesh.ChunkColumn + 1) * N, gnd.Width) - mesh.ChunkColumn * N)
                          * (Mathf.Min((mesh.ChunkRow + 1) * N, gnd.Height) - mesh.ChunkRow * N);
                var own = triangles.Where(t => t.Chunk == m).ToList();
                Assert.AreEqual(2 * cubes, own.Count, $"chunk {mesh.ChunkColumn},{mesh.ChunkRow}");
                foreach (var t in own)
                {
                    var centre = (t.A + t.B + t.C) / 3f;
                    Assert.AreEqual(mesh.ChunkColumn, Mathf.FloorToInt(centre.x) / N, t.ToString());
                    Assert.AreEqual(mesh.ChunkRow, Mathf.FloorToInt(centre.z) / N, t.ToString());
                }
            }
        }

        [Test]
        public void WallOnAChunkBorder_IsInItsOwnersChunk_AndMeetsTheNeighboursTop()
        {
            // 20 x 4: columns 0-15 at 0, columns 16-19 ten units higher; the walls are owned by column 15 (chunk 0).
            var meshes = Build(GndTestMaps.Build(20, 4, (c, r, dx, dz) => c < 16 ? 0f : -10f));
            var triangles = WorldTriangles(meshes);
            int first = meshes.ToList().FindIndex(m => m.ChunkColumn == 0), second = meshes.ToList().FindIndex(m => m.ChunkColumn == 1);
            var walls = triangles.Where(IsWall).ToList();
            Assert.AreEqual(8, walls.Count, "four walls, two triangles each");
            Assert.IsTrue(walls.All(w => w.Chunk == first), "in the owner's chunk");

            var topsOfSecond = new HashSet<(int, int, int)>(triangles.Where(t => t.Chunk == second && !IsWall(t))
                .SelectMany(t => new[] { t.A, t.B, t.C }).Select(Bits));
            var topsOfFirst = new HashSet<(int, int, int)>(triangles.Where(t => t.Chunk == first && !IsWall(t))
                .SelectMany(t => new[] { t.A, t.B, t.C }).Select(Bits));
            foreach (var w in walls)
            foreach (var p in new[] { w.A, w.B, w.C })
                Assert.IsTrue(p.y > 0.5f ? topsOfSecond.Contains(Bits(p)) : topsOfFirst.Contains(Bits(p)),
                    $"wall corner {p:F6} meets a top exactly");
        }

        [Test]
        public void Indices_Fit16Bits_InTheWorstCaseChunk()
        {
            // A full chunk where every cube has its own height: every top and both walls of every cube are drawn.
            foreach (var m in Build(GndTestMaps.NonSquare(N, N)).Concat(Build(GndTestMaps.Large())))
            {
                Assert.LessOrEqual(m.Positions.Length, 65535);
                foreach (var s in m.Submeshes) Assert.IsTrue(s.Triangles.All(i => i >= 0 && i < m.Positions.Length));
            }
        }

        [Test]
        public void Submeshes_AreOnePerTexture_InAscendingOrder()
        {
            var gnd = WithCheckerTops(Terrain());           // textures 0 and 2 on tops, 1 on walls
            var meshes = Build(gnd);
            var all = new HashSet<int>();
            foreach (var m in meshes)
            {
                var textures = m.Submeshes.Select(s => s.Texture).ToList();
                CollectionAssert.IsOrdered(textures);
                CollectionAssert.AllItemsAreUnique(textures);
                Assert.IsTrue(m.Submeshes.All(s => s.Triangles.Length > 0), "no empty submesh");
                all.UnionWith(textures);
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, all);
            Assert.IsTrue(meshes.All(m => m.Submeshes.All(s => s.Texture != 1) || m.ChunkRow * N <= 9 && 9 <= (m.ChunkRow + 1) * N),
                "only chunks crossed by the cliff have walls");
        }

        [Test]
        public void SecondBuild_GivesTheSameArrays()
        {
            var a = Build(Terrain());
            var b = Build(Terrain());
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual((a[i].ChunkColumn, a[i].ChunkRow, a[i].Origin), (b[i].ChunkColumn, b[i].ChunkRow, b[i].Origin));
                CollectionAssert.AreEqual(a[i].Positions, b[i].Positions);
                CollectionAssert.AreEqual(a[i].Normals, b[i].Normals);
                CollectionAssert.AreEqual(a[i].Uvs, b[i].Uvs);
                Assert.AreEqual(a[i].Submeshes.Count, b[i].Submeshes.Count);
                for (int s = 0; s < a[i].Submeshes.Count; s++)
                {
                    Assert.AreEqual(a[i].Submeshes[s].Texture, b[i].Submeshes[s].Texture);
                    CollectionAssert.AreEqual(a[i].Submeshes[s].Triangles, b[i].Submeshes[s].Triangles);
                }
            }
        }
    }
}
