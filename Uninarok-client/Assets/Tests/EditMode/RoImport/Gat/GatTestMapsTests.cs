using NUnit.Framework;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T2.1 (FR-1): the synthetic maps have the sizes and cells their builders describe.
    public class GatTestMapsTests
    {
        static void AssertCounts(RoGat gat, int width, int height)
        {
            Assert.AreEqual(width, gat.Width);
            Assert.AreEqual(height, gat.Height);
            Assert.AreEqual(width * height, gat.CellCount);
            Assert.AreEqual(width * height, gat.Types.Length);
            Assert.AreEqual(width * height * 4, gat.Heights.Length);
        }

        [Test]
        public void EachSampleMap_HasItsSize()
        {
            AssertCounts(GatTestMaps.Flat4x4(), 4, 4);
            AssertCounts(GatTestMaps.Ramp4x4(), 4, 4);
            AssertCounts(GatTestMaps.Cliff4x4(), 4, 4);
            AssertCounts(GatTestMaps.Water4x4(), 4, 4);
            AssertCounts(GatTestMaps.Asymmetric8x6(), 8, 6);
            AssertCounts(GatTestMaps.Asymmetric6x8(), 6, 8);
        }

        [Test]
        public void Asymmetric_HasItsMarkedCells_AtRowTimesWidthPlusColumn()
        {
            var gat = GatTestMaps.Asymmetric8x6();

            Assert.AreEqual(1u, gat.Types[gat.IndexOf(7, 0)]);
            Assert.AreEqual(5u, gat.Types[gat.IndexOf(0, 5)]);
            Assert.AreEqual(3u, gat.Types[gat.IndexOf(1, 2)]);
            Assert.AreEqual(5u, gat.Types[5 * 8 + 0]);
            // cell (3, 2): corners -(3 + 20), -(4 + 20), -(3 + 30), -(4 + 30)
            int i = gat.IndexOf(3, 2) * 4;
            Assert.AreEqual(new[] { -23f, -24f, -33f, -34f }, new[] { gat.Heights[i], gat.Heights[i + 1], gat.Heights[i + 2], gat.Heights[i + 3] });
        }

        [Test]
        public void Cliff_HasOneRaisedCorner()
        {
            var gat = GatTestMaps.Cliff4x4();

            Assert.AreEqual(5u, gat.Types[gat.IndexOf(2, 1)]);
            Assert.AreEqual(-20f, gat.Heights[gat.IndexOf(2, 1) * 4 + 3]);
            Assert.AreEqual(0f, gat.Heights[gat.IndexOf(3, 2) * 4 + 0], "the neighbour does not share the raised corner");
        }
    }
}
