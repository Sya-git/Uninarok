using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using static Uninarok.RoImport.Tests.Gnd.GndMeshTestUtil;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T4.1 (FR-1, FR-2, FR-3): no cracks and no duplicate faces in the built meshes, across chunk borders.
    public class RoGndCrackTests
    {
        static RoGnd[] Maps() => new[] { Terrain(), GndTestMaps.Step(), GndTestMaps.CliffWithGap(), GndTestMaps.Wide(), GndTestMaps.Tall() };

        [Test]
        public void EveryTopCorner_IsAMeshVertex_BitForBit()
        {
            // Neighbouring cubes with equal shared-corner heights must give exactly one position there: each top corner
            // of the map, converted once by RoMapSpace, is found bit for bit among the world-space vertices.
            var gnd = Terrain();
            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);
            var vertices = new HashSet<(int, int, int)>(WorldTriangles(Build(gnd)).SelectMany(t => new[] { t.A, t.B, t.C }).Select(Bits));
            for (int row = 0; row < gnd.Height; row++)
            for (int column = 0; column < gnd.Width; column++)
            {
                var cube = gnd.CubeAt(column, row);
                foreach (var corner in new[]
                         {
                             space.CornerUnity(column, row, cube.H00), space.CornerUnity(column + 1, row, cube.H10),
                             space.CornerUnity(column, row + 1, cube.H01), space.CornerUnity(column + 1, row + 1, cube.H11)
                         })
                    Assert.IsTrue(vertices.Contains(Bits(corner)), $"cube {column},{row} corner {corner:F6}");
            }
        }

        [Test]
        public void NoTwoVertexPositions_AreNearlyButNotExactlyEqual()
        {
            // A hairline crack is two positions that should be one: distinct positions closer than 1e-4 units.
            foreach (var gnd in Maps())
            {
                var positions = WorldTriangles(Build(gnd)).SelectMany(t => new[] { t.A, t.B, t.C })
                    .GroupBy(Bits).Select(g => g.First()).ToList();
                var cells = positions.GroupBy(p => (Mathf.RoundToInt(p.x * 100), Mathf.RoundToInt(p.y * 100), Mathf.RoundToInt(p.z * 100)));
                foreach (var cell in cells)
                {
                    var list = cell.ToList();
                    for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                        Assert.Greater((list[i] - list[j]).sqrMagnitude, 1e-8f, $"{gnd}: {list[i]:F7} and {list[j]:F7}");
                }
            }
        }

        [Test]
        public void Walls_MeetTheTopEdgesExactly()
        {
            // Every cube of these maps has a top, so every wall corner is also a top corner, bit for bit.
            foreach (var gnd in new[] { Terrain(), GndTestMaps.Step(), GndTestMaps.Wide(), GndTestMaps.Tall() })
            {
                var triangles = WorldTriangles(Build(gnd));
                var tops = new HashSet<(int, int, int)>(triangles.Where(t => Mathf.Abs(t.Normal.y) > 1e-6f).SelectMany(t => new[] { t.A, t.B, t.C }).Select(Bits));
                var walls = triangles.Where(t => Mathf.Abs(t.Normal.y) <= 1e-6f).ToList();
                Assert.IsNotEmpty(walls, gnd.ToString());
                foreach (var w in walls)
                foreach (var p in new[] { w.A, w.B, w.C })
                    Assert.IsTrue(tops.Contains(Bits(p)), $"{gnd}: wall corner {p:F6} is not on a top edge");
            }
        }

        [Test]
        public void TriangleCount_EqualsTheIndependentOracle()
        {
            foreach (var gnd in Maps().Concat(new[] { EqualAtOneEnd() }))
                Assert.AreEqual(Oracle(gnd), WorldTriangles(Build(gnd)).Count, gnd.ToString());
        }

        [Test]
        public void NoTwoTriangles_HaveTheSameVertexSet()
        {
            foreach (var gnd in Maps())
            {
                var seen = new HashSet<string>();
                foreach (var t in WorldTriangles(Build(gnd)))
                {
                    var key = string.Join("|", new[] { Bits(t.A), Bits(t.B), Bits(t.C) }.OrderBy(b => b));
                    Assert.IsTrue(seen.Add(key), $"{gnd}: duplicate triangle {t}");
                }
            }
        }

        [Test]
        public void NoTriangle_HasZeroArea()
        {
            foreach (var gnd in Maps().Concat(new[] { EqualAtOneEnd() }))
            foreach (var t in WorldTriangles(Build(gnd)))
                Assert.Greater(t.Normal.sqrMagnitude, 0f, $"{gnd}: degenerate triangle {t}");
        }

        /// <summary>Two cubes whose side wall closes at one end (a triangle, PLAN-0004 4.3).</summary>
        static RoGnd EqualAtOneEnd() => GndTestMaps.Build(2, 1, (c, r, dx, dz) => c == 1 && dz == 1 ? -10f : 0f);

        /// <summary>
        /// Triangles expected from the cube table alone, written apart from the builder (PLAN-0004 4.3):
        /// 2 per top, 2 per wall whose heights differ at both ends, 1 per wall that differs at one end only.
        /// </summary>
        static int Oracle(RoGnd gnd)
        {
            int count = 0;
            for (int row = 0; row < gnd.Height; row++)
            for (int column = 0; column < gnd.Width; column++)
            {
                var cube = gnd.Cubes[row * gnd.Width + column];
                if (cube.TileUp >= 0) count += 2;
                if (cube.TileFront >= 0 && row + 1 < gnd.Height)
                {
                    var next = gnd.Cubes[(row + 1) * gnd.Width + column];
                    count += Ends(cube.H01 != next.H00, cube.H11 != next.H10);
                }
                if (cube.TileSide >= 0 && column + 1 < gnd.Width)
                {
                    var next = gnd.Cubes[row * gnd.Width + column + 1];
                    count += Ends(cube.H10 != next.H00, cube.H11 != next.H01);
                }
            }
            return count;
        }

        static int Ends(bool first, bool second) => (first ? 1 : 0) + (second ? 1 : 0);
    }
}
