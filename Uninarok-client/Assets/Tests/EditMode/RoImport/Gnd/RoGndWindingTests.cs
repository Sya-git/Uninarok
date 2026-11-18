using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using static Uninarok.RoImport.Tests.Gnd.GndMeshTestUtil;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T4.1 (FR-1, FR-2, FR-3): after the Y flip and the one winding reversal (PLAN-0004 4.4), every top faces
    // up and every wall faces its lower side (T0.3), so nothing is inside out. Unity's front face is the one the
    // winding's cross product points toward.
    public class RoGndWindingTests
    {
        static bool IsWall(GndMeshTestUtil.WorldTriangle t) => Mathf.Abs(t.Normal.y) <= 1e-6f;

        [Test]
        public void EveryTop_FacesUp()
        {
            foreach (var gnd in new[] { Terrain(), GndTestMaps.Ramp(), GndTestMaps.Wide(), GndTestMaps.Tall() })
            {
                var triangles = WorldTriangles(Build(gnd));
                var tops = triangles.Where(t => !IsWall(t)).ToList();
                Assert.AreEqual(2 * gnd.Cubes.Count(c => c.TileUp >= 0), tops.Count, $"{gnd}: top triangles");
                foreach (var t in tops) Assert.Greater(t.Normal.y, 0f, $"{gnd}: {t}");
            }
        }

        [Test]
        public void StepUp_SideWallsFaceTheLowerColumns()
        {
            // Columns 0-1 low, 2-3 high: the wall on column edge 2 is seen from the low side, -x.
            AssertWalls(GndTestMaps.Step(), Vector3.left, 8);
        }

        [Test]
        public void StepDown_SideWallsFaceTheLowerColumns()
        {
            // Columns 0-1 high, 2-3 low: the owning cube is the high one and the wall faces +x.
            AssertWalls(GndTestMaps.Build(4, 4, (c, r, dx, dz) => c < 2 ? -10f : 0f), Vector3.right, 8);
        }

        [Test]
        public void CliffUp_FrontWallsFaceTheLowerRows()
        {
            // Rows 0-1 low, 2-3 high: the wall on row edge 2 faces -z.
            AssertWalls(GndTestMaps.Build(4, 4, (c, r, dx, dz) => r < 2 ? 0f : -20f), Vector3.back, 8);
        }

        [Test]
        public void CliffDown_FrontWallsFaceTheLowerRows()
        {
            AssertWalls(GndTestMaps.Build(4, 4, (c, r, dx, dz) => r < 2 ? -20f : 0f), Vector3.forward, 8);
        }

        [Test]
        public void TerrainWalls_AllFaceTheirLowerSide()
        {
            // The terrain's only walls are on its cliff, which crosses the chunk border; each must face the side whose
            // ground is lower, checked against the cube heights on both sides.
            var gnd = Terrain();
            foreach (var t in WorldTriangles(Build(gnd)).Where(IsWall))
            {
                var n = t.Normal.normalized;
                var centre = (t.A + t.B + t.C) / 3f;
                // The ground just in front of the wall must be lower than the ground just behind it.
                float front = TopHeightNear(gnd, centre + n * 0.25f), back = TopHeightNear(gnd, centre - n * 0.25f);
                Assert.Less(front, back, $"wall {t} faces {n}");
            }
        }

        static void AssertWalls(RoGnd gnd, Vector3 facing, int expected)
        {
            var walls = WorldTriangles(Build(gnd)).Where(IsWall).ToList();
            Assert.AreEqual(expected, walls.Count, "two triangles per wall, four walls");
            foreach (var w in walls)
                Assert.AreEqual(1f, Vector3.Dot(w.Normal.normalized, facing), 1e-5f, w.ToString());
        }

        /// <summary>The Unity height of the top of the cube under a world position (its mean corner height).</summary>
        static float TopHeightNear(RoGnd gnd, Vector3 unity)
        {
            var space = new RoMapSpace(gnd.Width, gnd.Height, gnd.Zoom);
            Assert.IsTrue(space.TryCubeAtUnity(unity, out int column, out int row), unity.ToString("F3"));
            var c = gnd.CubeAt(column, row);
            return space.CornerUnity(0, 0, (c.H00 + c.H10 + c.H01 + c.H11) / 4f).y;
        }
    }
}
