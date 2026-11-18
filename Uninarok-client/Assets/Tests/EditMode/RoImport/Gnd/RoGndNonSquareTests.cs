using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T3.1 (FR-4): rows and columns on maps whose width differs from height. Every cube of the NonSquare maps
    // sits at its own height -(column + 10 * row), so a swapped or mis-indexed cube shows up as a wrong height.
    [Ignore("pending T3.2")]
    public class RoGndNonSquareTests
    {
        static float H(int column, int row) => GndTestMaps.CubeHeight(column, row);

        [Test]
        public void Wide_CubeAtColumn4Row2_AndItsNeighbours()
        {
            var gnd = GndTestMaps.Wide();                   // 5 columns, 3 rows
            var quads = RoGndSurfacesTests.Quads(gnd);

            var top = quads.Single(q => q.Face == RoGndFace.Top && q.Column == 4 && q.Row == 2);
            Assert.AreEqual(gnd.Cubes[2 * 5 + 4].TileUp, top.Tile, "index row * Width + column");
            Assert.AreEqual(new Vector3(40, H(4, 2), 20), top.P00);
            Assert.AreEqual(new Vector3(50, H(4, 2), 30), top.P11);

            // the side wall from (3,2) into (4,2): on the column edge 4, from (3,2)'s height down to (4,2)'s
            var side = quads.Single(q => q.Face == RoGndFace.SideWall && q.Column == 3 && q.Row == 2);
            Assert.AreEqual(gnd.CubeAt(3, 2).TileSide, side.Tile);
            Assert.AreEqual(new Vector3(40, H(3, 2), 20), side.P00);
            Assert.AreEqual(new Vector3(40, H(4, 2), 30), side.P11);

            // the front wall from (4,1) into (4,2): on the row edge 2
            var front = quads.Single(q => q.Face == RoGndFace.FrontWall && q.Column == 4 && q.Row == 1);
            Assert.AreEqual(gnd.CubeAt(4, 1).TileFront, front.Tile);
            Assert.AreEqual(new Vector3(40, H(4, 1), 20), front.P00);
            Assert.AreEqual(new Vector3(50, H(4, 2), 20), front.P11);

            // (4,2) is on the last column and the last row: it owns no wall
            Assert.IsFalse(quads.Any(q => q.Face != RoGndFace.Top && q.Column == 4 && q.Row == 2));
        }

        [Test]
        public void Tall_CubeAtColumn2Row4_AndItsNeighbours()
        {
            // The transposed case: a 3 x 5 map has no column 4, so its far corner cube is (2,4).
            var gnd = GndTestMaps.Tall();                   // 3 columns, 5 rows
            var quads = RoGndSurfacesTests.Quads(gnd);
            Assert.IsFalse(quads.Any(q => q.Column >= 3), "no column beyond the width");

            var top = quads.Single(q => q.Face == RoGndFace.Top && q.Column == 2 && q.Row == 4);
            Assert.AreEqual(gnd.Cubes[4 * 3 + 2].TileUp, top.Tile);
            Assert.AreEqual(new Vector3(20, H(2, 4), 40), top.P00);

            var side = quads.Single(q => q.Face == RoGndFace.SideWall && q.Column == 1 && q.Row == 4);
            Assert.AreEqual(new Vector3(20, H(1, 4), 40), side.P00);
            Assert.AreEqual(new Vector3(20, H(2, 4), 50), side.P11);

            var front = quads.Single(q => q.Face == RoGndFace.FrontWall && q.Column == 2 && q.Row == 3);
            Assert.AreEqual(new Vector3(20, H(2, 3), 40), front.P00);
            Assert.AreEqual(new Vector3(30, H(2, 4), 40), front.P11);
        }

        [TestCase(5, 3)]
        [TestCase(3, 5)]
        public void EveryQuad_HasItsOwnersHeightAndTile(int width, int height)
        {
            var gnd = GndTestMaps.NonSquare(width, height);
            var quads = RoGndSurfacesTests.Quads(gnd);
            Assert.AreEqual(width * height, quads.Count(q => q.Face == RoGndFace.Top));
            Assert.AreEqual(width * (height - 1), quads.Count(q => q.Face == RoGndFace.FrontWall));
            Assert.AreEqual((width - 1) * height, quads.Count(q => q.Face == RoGndFace.SideWall));
            foreach (var q in quads)
            {
                var cube = gnd.CubeAt(q.Column, q.Row);
                int tile = q.Face == RoGndFace.Top ? cube.TileUp : q.Face == RoGndFace.FrontWall ? cube.TileFront : cube.TileSide;
                Assert.AreEqual(tile, q.Tile, q.ToString());
                Assert.AreEqual(H(q.Column, q.Row), q.P00.y, q.ToString());
            }
        }

        [Test]
        public void OldIndex_WouldPickAnotherCube()
        {
            // Regression guard (PLAN-0004 4.1): the old importer's column + row * height is wrong when width != height.
            var wide = GndTestMaps.Wide();
            int oldIndex = 4 + 2 * wide.Height;
            Assert.AreNotEqual(wide.IndexOf(4, 2), oldIndex);
            Assert.AreNotEqual(wide.Cubes[oldIndex].H00, H(4, 2), "the old index lands on another cube");
            var top = RoGndSurfacesTests.Quads(wide).Single(q => q.Face == RoGndFace.Top && q.Column == 4 && q.Row == 2);
            Assert.AreEqual(H(4, 2), top.P00.y, "the surfaces use the right one");

            var tall = GndTestMaps.Tall();
            Assert.GreaterOrEqual(2 + 4 * tall.Height, tall.Cubes.Count, "on a tall map the old index reads past the table");
        }
    }
}
