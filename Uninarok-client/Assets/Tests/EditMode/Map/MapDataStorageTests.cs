using NUnit.Framework;

namespace Uninarok.Map.Tests
{
    // TASKS-0005 T1.2 / T1.3 (FR-5): flat storage of 19 data bytes per cell (PLAN-0005 4.3, D14), linear in cells.
    public class MapDataStorageTests
    {
        [Test]
        public void TheData_Is19BytesPerCell()
        {
            var d = MapTestData.Create(40, 30);
            try
            {
                double perCell = (double)d.CellDataBytes / (d.CellsX * d.CellsZ);

                Assert.AreEqual(19.0, perCell, "4 heights x 4 bytes + type + flags + raw type");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(d);
            }
        }

        [Test]
        public void Storage_GrowsLinearly_WithTheCellCount()
        {
            var small = MapTestData.Create(20, 10);
            var large = MapTestData.Create(80, 40);
            try
            {
                Assert.AreEqual(16L * small.CellDataBytes, large.CellDataBytes);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(small);
                UnityEngine.Object.DestroyImmediate(large);
            }
        }
    }
}
