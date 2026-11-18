using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T3.1 (FR-1, FR-2): tops and walls of PLAN-0004 4.3, on the hand-made maps. Positions are in Ragnarok
    // space (Y down), cube edge 10.
    [Ignore("pending T3.2")]
    public class RoGndSurfacesTests
    {
        internal static List<RoGndQuad> Quads(RoGnd gnd, RoReport report = null)
        {
            Assert.IsTrue(RoGndRules.TryGet(gnd.VersionMajor, gnd.VersionMinor, out var rules), "rules row for 1.7");
            return RoGndSurfaces.Enumerate(gnd, rules, report ?? new RoReport()).ToList();
        }

        static List<RoGndQuad> Of(IEnumerable<RoGndQuad> quads, RoGndFace face) => quads.Where(q => q.Face == face).ToList();

        static int DistinctCorners(RoGndQuad q) => new HashSet<Vector3> { q.P00, q.P10, q.P01, q.P11 }.Count;

        /// <summary>A copy of <paramref name="gnd"/> with the cube at (column, row) replaced.</summary>
        static RoGnd WithCube(RoGnd gnd, int column, int row, RoGndCube cube)
        {
            var cubes = gnd.Cubes.ToArray();
            cubes[gnd.IndexOf(column, row)] = cube;
            return new RoGnd
            {
                VersionMajor = gnd.VersionMajor, VersionMinor = gnd.VersionMinor, Width = gnd.Width, Height = gnd.Height,
                Zoom = gnd.Zoom, Textures = gnd.Textures, Tiles = gnd.Tiles, Cubes = cubes
            };
        }

        [Test]
        public void FlatMap_HasTopsOnly_EvenWithWallTiles()
        {
            var quads = Quads(GndTestMaps.Flat());
            Assert.AreEqual(16, quads.Count);
            Assert.IsTrue(quads.All(q => q.Face == RoGndFace.Top));
        }

        [Test]
        public void Tops_UseTheCubesTileAndCorners()
        {
            var gnd = GndTestMaps.Ramp();
            foreach (var q in Of(Quads(gnd), RoGndFace.Top))
            {
                var cube = gnd.CubeAt(q.Column, q.Row);
                Assert.AreEqual(cube.TileUp, q.Tile, q.ToString());
                float x = q.Column * 10f, z = q.Row * 10f;
                Assert.AreEqual(new Vector3(x, cube.H00, z), q.P00, "H00 at the low column and low row");
                Assert.AreEqual(new Vector3(x + 10, cube.H10, z), q.P10, "H10 at the next column");
                Assert.AreEqual(new Vector3(x, cube.H01, z + 10), q.P01, "H01 at the next row");
                Assert.AreEqual(new Vector3(x + 10, cube.H11, z + 10), q.P11, "H11 at both");
            }
        }

        [Test]
        public void SingleCube_CornersLandOnTheirGridCorners()
        {
            var gnd = GndTestMaps.Build(1, 1, (c, r, dx, dz) => 1 + dx + 2 * dz);   // H00 1, H10 2, H01 3, H11 4
            var top = Quads(gnd).Single();
            Assert.AreEqual(new Vector3(0, 1, 0), top.P00);
            Assert.AreEqual(new Vector3(10, 2, 0), top.P10);
            Assert.AreEqual(new Vector3(0, 3, 10), top.P01);
            Assert.AreEqual(new Vector3(10, 4, 10), top.P11);
        }

        [Test]
        public void Step_HasOneSideWallPerDifferingEdge_WithTheOwningCubesTile()
        {
            var gnd = GndTestMaps.Step();                     // columns 0-1 at 0, columns 2-3 at -10
            var quads = Quads(gnd);
            Assert.AreEqual(16, Of(quads, RoGndFace.Top).Count);
            Assert.IsEmpty(Of(quads, RoGndFace.FrontWall), "rows are level: no front wall");
            var walls = Of(quads, RoGndFace.SideWall).OrderBy(q => q.Row).ToList();
            Assert.AreEqual(4, walls.Count, "one wall per row on the column edge 2");
            for (int row = 0; row < 4; row++)
            {
                var w = walls[row];
                Assert.AreEqual((1, row), (w.Column, w.Row), "owned by the cube before the edge");
                Assert.AreEqual(gnd.CubeAt(1, row).TileSide, w.Tile);
                Assert.AreEqual(new Vector3(20, 0, row * 10), w.P00, "this cube's H10");
                Assert.AreEqual(new Vector3(20, 0, row * 10 + 10), w.P10, "this cube's H11");
                Assert.AreEqual(new Vector3(20, -10, row * 10), w.P01, "next cube's H00");
                Assert.AreEqual(new Vector3(20, -10, row * 10 + 10), w.P11, "next cube's H01");
            }
        }

        [Test]
        public void Cliff_HasOneFrontWallPerColumn_WithTheOwningCubesTile()
        {
            var gnd = GndTestMaps.CliffWithGap();             // rows 0-1 at 0, rows 2-3 at -20
            var walls = Of(Quads(gnd), RoGndFace.FrontWall).OrderBy(q => q.Column).ToList();
            Assert.AreEqual(4, walls.Count);
            Assert.IsEmpty(Of(Quads(gnd), RoGndFace.SideWall));
            for (int column = 0; column < 4; column++)
            {
                var w = walls[column];
                Assert.AreEqual((column, 1), (w.Column, w.Row));
                Assert.AreEqual(gnd.CubeAt(column, 1).TileFront, w.Tile);
                Assert.AreEqual(new Vector3(column * 10, 0, 20), w.P00, "this cube's H01");
                Assert.AreEqual(new Vector3(column * 10 + 10, 0, 20), w.P10, "this cube's H11");
                Assert.AreEqual(new Vector3(column * 10, -20, 20), w.P01, "next row's H00");
                Assert.AreEqual(new Vector3(column * 10 + 10, -20, 20), w.P11, "next row's H10");
            }
        }

        [Test]
        public void WallTileNone_ProducesNoWall()
        {
            var quads = Quads(GndTestMaps.Build(4, 4, (c, r, dx, dz) => c < 2 ? 0f : -10f, walls: false));
            Assert.AreEqual(16, quads.Count);
            Assert.IsTrue(quads.All(q => q.Face == RoGndFace.Top));
        }

        [Test]
        public void EqualHeightsAtOneEnd_GiveATriangle()
        {
            // Two cubes in a row; the second rises only at its next-row edge, so the side wall closes at the low-row end.
            var gnd = GndTestMaps.Build(2, 1, (c, r, dx, dz) => c == 1 && dz == 1 ? -10f : 0f);
            var wall = Of(Quads(gnd), RoGndFace.SideWall).Single();
            Assert.AreEqual(3, DistinctCorners(wall), wall.ToString());
            Assert.AreEqual(wall.P00, wall.P01, "equal at the low-row end");
        }

        [Test]
        public void HeightsWithinEpsilon_AreEqual()
        {
            var gnd = GndTestMaps.Build(2, 1, (c, r, dx, dz) => c == 1 ? RoGndRules.Epsilon / 2f : 0f);
            Assert.IsEmpty(Of(Quads(gnd), RoGndFace.SideWall));
        }

        [Test]
        public void CubeWithoutTop_IsAGap()
        {
            var quads = Of(Quads(GndTestMaps.CliffWithGap()), RoGndFace.Top);
            Assert.AreEqual(15, quads.Count);
            Assert.IsFalse(quads.Any(q => q.Column == GndTestMaps.GapColumn && q.Row == GndTestMaps.GapRow));
        }

        [Test]
        public void TileZero_IsAValidTile()
        {
            var top = Of(Quads(GndTestMaps.Flat()), RoGndFace.Top).Single(q => q.Column == 0 && q.Row == 0);
            Assert.AreEqual(0, top.Tile);
        }

        [Test]
        public void EdgeCubes_ReadNoNeighbourOutside_AndGiveNoWarning()
        {
            // Wall tiles that point out of the map, on cubes that differ in height from everything, at all four edges.
            var gnd = GndTestMaps.Build(3, 2, (c, r, dx, dz) => -(c + 1) * 5f, walls: false);
            foreach (var (c, r) in new[] { (2, 0), (2, 1), (0, 1), (1, 1) })
            {
                var cube = gnd.CubeAt(c, r);
                gnd = WithCube(gnd, c, r, new RoGndCube(cube.H00, cube.H10, cube.H01, cube.H11, cube.TileUp, 0, 0));
            }

            var report = new RoReport();
            List<RoGndQuad> quads = null;
            Assert.DoesNotThrow(() => quads = Quads(gnd, report));
            Assert.IsEmpty(report.Issues, "an edge is an expected condition, not a warning");
            // Only (0,1) and (1,1) have a neighbour toward the next column; nothing has one toward the next row.
            Assert.IsEmpty(Of(quads, RoGndFace.FrontWall));
            CollectionAssert.AreEquivalent(new[] { (0, 1), (1, 1) },
                Of(quads, RoGndFace.SideWall).Select(q => (q.Column, q.Row)));
        }
    }
}
