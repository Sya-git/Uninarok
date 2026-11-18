using NUnit.Framework;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T1.1 (FR-1, FR-4, FR-5): the hand-made sample maps build with the expected sizes and counts, so later
    // tests can rely on them.
    public class GndTestMapsTests
    {
        static void AssertMap(RoGnd gnd, int width, int height, int tops, float zoom = GndTestMaps.DefaultZoom)
        {
            Assert.AreEqual(width, gnd.Width, "width");
            Assert.AreEqual(height, gnd.Height, "height");
            Assert.AreEqual(zoom, gnd.Zoom, "zoom");
            Assert.AreEqual(width * height, gnd.Cubes.Count, "cube count");
            Assert.AreEqual(GndTestMaps.ExpectedTiles(width, height, tops), gnd.Tiles.Count, "tile count");
            foreach (var cube in gnd.Cubes)
            {
                Assert.Less(cube.TileUp, gnd.Tiles.Count);
                Assert.Less(cube.TileFront, gnd.Tiles.Count);
                Assert.Less(cube.TileSide, gnd.Tiles.Count);
            }
        }

        [Test] public void Flat_Is4x4() => AssertMap(GndTestMaps.Flat(), 4, 4, 16);
        [Test] public void Step_Is4x4() => AssertMap(GndTestMaps.Step(), 4, 4, 16);
        [Test] public void Ramp_Is4x4() => AssertMap(GndTestMaps.Ramp(), 4, 4, 16);
        [Test] public void CliffWithGap_Is4x4WithOneTopMissing() => AssertMap(GndTestMaps.CliffWithGap(), 4, 4, 15);
        [Test] public void Wide_Is5x3() => AssertMap(GndTestMaps.Wide(), 5, 3, 15);
        [Test] public void Tall_Is3x5() => AssertMap(GndTestMaps.Tall(), 3, 5, 15);
        [Test] public void Zoom5_HasZoom5() => AssertMap(GndTestMaps.Zoom5(), 4, 4, 16, 5f);
        [Test] public void Large_Is40x33() => AssertMap(GndTestMaps.Large(), 40, 33, 40 * 33);

        [Test]
        public void CliffWithGap_GapCubeHasNoTopAndTileZeroIsUsed()
        {
            var gnd = GndTestMaps.CliffWithGap();
            Assert.AreEqual(RoGndCube.None, gnd.CubeAt(GndTestMaps.GapColumn, GndTestMaps.GapRow).TileUp);
            Assert.AreEqual(0, gnd.CubeAt(0, 0).TileUp, "tile id 0 is a valid tile and is used");
        }

        [Test]
        public void Wide_CubeIndexIsRowTimesWidthPlusColumn()
        {
            var gnd = GndTestMaps.Wide();
            Assert.AreEqual(2 * 5 + 4, gnd.IndexOf(4, 2));
            Assert.AreEqual(GndTestMaps.CubeHeight(4, 2), gnd.CubeAt(4, 2).H00);
            Assert.AreEqual(GndTestMaps.CubeHeight(4, 2), gnd.Cubes[2 * 5 + 4].H11);
        }

        [Test]
        public void EdgeCubes_HaveNoWallTowardOutside()
        {
            var gnd = GndTestMaps.Tall();
            for (int column = 0; column < gnd.Width; column++)
                Assert.AreEqual(RoGndCube.None, gnd.CubeAt(column, gnd.Height - 1).TileFront);
            for (int row = 0; row < gnd.Height; row++)
                Assert.AreEqual(RoGndCube.None, gnd.CubeAt(gnd.Width - 1, row).TileSide);
        }
    }
}
