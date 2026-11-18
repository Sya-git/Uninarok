using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T4.1 / T4.2 (FR-8): the optional flat image of the cell types, one pixel per cell, row 0 at the bottom.
    public class RoGatImageTests
    {
        static byte[] Types(RoGat gat) => gat.Types.Select(t => (byte)RoGatRules.TypeOf(t)).ToArray();

        [Test]
        public void OnePixelPerCell()
        {
            var gat = GatTestMaps.Asymmetric8x6();

            var image = RoGatImage.Build(gat.Width, gat.Height, Types(gat));

            Assert.AreEqual((8, 6), (image.Width, image.Height));
            Assert.AreEqual(8 * 6 * 4, image.Rgba32.Length);
        }

        [Test]
        public void EveryType_HasItsOwnOpaqueColour()
        {
            var types = (MapCellType[])Enum.GetValues(typeof(MapCellType));

            var colours = types.Select(RoGatImage.ColorOf).ToArray();

            Assert.AreEqual(types.Length, colours.Select(c => (c.r, c.g, c.b)).Distinct().Count(), "one colour per type");
            Assert.IsTrue(colours.All(c => c.a == 255));
        }

        [Test]
        public void Row0_IsTheBottomRow_AndColumnsAreNotSwapped()
        {
            var gat = GatTestMaps.Asymmetric8x6();   // (7, 0) wall, (0, 5) cliff, (1, 2) water

            var image = RoGatImage.Build(gat.Width, gat.Height, Types(gat));

            // RoImage is top row first: image row y shows map row Height - 1 - y
            Assert.AreEqual(RoGatImage.ColorOf(MapCellType.Wall), image.GetPixel(7, 5), "cell (7, 0) is bottom right");
            Assert.AreEqual(RoGatImage.ColorOf(MapCellType.Cliff), image.GetPixel(0, 0), "cell (0, 5) is top left");
            Assert.AreEqual(RoGatImage.ColorOf(MapCellType.Water), image.GetPixel(1, 3), "cell (1, 2)");
            Assert.AreEqual(RoGatImage.ColorOf(MapCellType.Walkable), image.GetPixel(4, 4));
        }

        [Test]
        public void TheImage_IsDeterministic()
        {
            var gat = GatTestMaps.Asymmetric6x8();

            var a = RoGatImage.Build(gat.Width, gat.Height, Types(gat));
            var b = RoGatImage.Build(gat.Width, gat.Height, Types(gat));

            CollectionAssert.AreEqual(a.Rgba32, b.Rgba32);
        }
    }
}
