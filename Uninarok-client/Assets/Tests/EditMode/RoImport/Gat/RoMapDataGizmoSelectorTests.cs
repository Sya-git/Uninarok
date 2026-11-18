using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T5.1 / T5.2 (FR-7): which cells the map data gizmos draw (PLAN-0005 4.6): a radius around the focus or a
    // chosen region, nearest cells first, per-group hiding, and a cap with a notice.
    public class RoMapDataGizmoSelectorTests
    {
        MapData _data;

        [TearDown]
        public void TearDown()
        {
            if (_data != null) Object.DestroyImmediate(_data);
        }

        /// <summary>A map whose cell type is <paramref name="type"/>(x, z).</summary>
        MapData Map(int sx, int sz, System.Func<int, int, MapCellType> type = null)
        {
            type = type ?? ((x, z) => MapCellType.Walkable);
            int n = sx * sz;
            var types = new byte[n];
            var flags = new byte[n];
            for (int z = 0; z < sz; z++)
            for (int x = 0; x < sx; x++)
            {
                var t = type(x, z);
                types[z * sx + x] = (byte)t;
                flags[z * sx + x] = (byte)RoGatRules.FlagsOf(t);
            }
            _data = ScriptableObject.CreateInstance<MapData>();
            _data.Assign("m", sx, sz, 0.5f, Vector2.zero, 1f, false, new float[n * 4], types, flags, (byte[])types.Clone());
            return _data;
        }

        static (int x, int z) Cell(MapData d, int index) => (index % d.CellsX, index / d.CellsX);

        [Test]
        public void RadiusRegion_IsTheSquareAroundTheCentre_ClampedToTheMap()
        {
            var d = Map(100, 60);

            Assert.AreEqual(new RectInt(40, 20, 21, 21), RoMapDataGizmoSelector.RadiusRegion(d, new Vector2Int(50, 30), 10));
            Assert.AreEqual(new RectInt(0, 0, 6, 11), RoMapDataGizmoSelector.RadiusRegion(d, new Vector2Int(-5, 0), 10), "clamped low");
            Assert.AreEqual(new RectInt(95, 55, 5, 5), RoMapDataGizmoSelector.RadiusRegion(d, new Vector2Int(99, 59), 4), "clamped high");
        }

        [Test]
        public void Select_ReturnsEveryCellOfTheRegion_AndNothingOutside()
        {
            var d = Map(20, 10);
            var region = new RectInt(3, 2, 5, 4);
            var cells = new List<int>();

            bool capped = RoMapDataGizmoSelector.Select(d, region, new Vector2Int(5, 3), RoMapCellGroups.All, 1000, cells);

            Assert.IsFalse(capped);
            Assert.AreEqual(20, cells.Count);
            CollectionAssert.AllItemsAreUnique(cells);
            Assert.IsTrue(cells.Select(i => Cell(d, i)).All(c => region.Contains(new Vector2Int(c.x, c.z))));
        }

        [Test]
        public void ARegionPartlyOutsideTheMap_IsClipped()
        {
            var d = Map(10, 10);
            var cells = new List<int>();

            RoMapDataGizmoSelector.Select(d, new RectInt(-3, 8, 6, 6), new Vector2Int(0, 9), RoMapCellGroups.All, 1000, cells);

            Assert.AreEqual(3 * 2, cells.Count);
        }

        [Test]
        public void AHiddenGroup_IsNotSelected()
        {
            // walls on even columns, cliffs on row 0, water at (3, 3)
            var d = Map(8, 8, (x, z) => x == 3 && z == 3 ? MapCellType.Water : z == 0 ? MapCellType.Cliff
                : x % 2 == 0 ? MapCellType.Wall : MapCellType.Walkable);
            var cells = new List<int>();

            RoMapDataGizmoSelector.Select(d, new RectInt(0, 0, 8, 8), new Vector2Int(4, 4),
                RoMapCellGroups.All & ~RoMapCellGroups.Wall & ~RoMapCellGroups.Water, 1000, cells);

            var types = cells.Select(i => d.TypeAt(Cell(d, i).x, Cell(d, i).z)).ToArray();
            CollectionAssert.DoesNotContain(types, MapCellType.Wall);
            CollectionAssert.DoesNotContain(types, MapCellType.Water);
            Assert.AreEqual(8, types.Count(t => t == MapCellType.Cliff));
            Assert.AreEqual(4 * 7 - 1, types.Count(t => t == MapCellType.Walkable), "odd columns of rows 1 to 7, less the water");
        }

        [TestCase(MapCellType.Walkable, RoMapCellGroups.Walkable)]
        [TestCase(MapCellType.Type2, RoMapCellGroups.Walkable)]
        [TestCase(MapCellType.Type4, RoMapCellGroups.Walkable)]
        [TestCase(MapCellType.Type6, RoMapCellGroups.Walkable)]
        [TestCase(MapCellType.Water, RoMapCellGroups.Water)]
        [TestCase(MapCellType.Wall, RoMapCellGroups.Wall)]
        [TestCase(MapCellType.Cliff, RoMapCellGroups.Cliff)]
        [TestCase(MapCellType.Other, RoMapCellGroups.Other)]
        public void EachType_BelongsToItsToggleGroup(MapCellType type, RoMapCellGroups group)
        {
            Assert.AreEqual(group, RoMapDataGizmoSelector.GroupOf(type));
        }

        [Test]
        public void TheCap_KeepsTheNearestCells_AndRaisesTheNotice()
        {
            var d = Map(50, 50);
            var centre = new Vector2Int(25, 25);
            var cells = new List<int>();

            bool capped = RoMapDataGizmoSelector.Select(d, new RectInt(0, 0, 50, 50), centre, RoMapCellGroups.All, 9 + 16 + 3, cells);

            Assert.IsTrue(capped);
            Assert.AreEqual(28, cells.Count);
            // rings 0 and 1 (9 cells) and ring 2 (16) are complete; 3 cells of ring 3 follow
            var rings = cells.Select(i => Cell(d, i)).Select(c => Mathf.Max(Mathf.Abs(c.x - centre.x), Mathf.Abs(c.z - centre.y))).ToArray();
            Assert.AreEqual(9, rings.Count(r => r <= 1));
            Assert.AreEqual(16, rings.Count(r => r == 2));
            Assert.AreEqual(3, rings.Count(r => r == 3));
        }

        [Test]
        public void NoNotice_WhenEverythingFits()
        {
            var d = Map(10, 10);
            var cells = new List<int>();

            Assert.IsFalse(RoMapDataGizmoSelector.Select(d, new RectInt(0, 0, 10, 10), new Vector2Int(5, 5), RoMapCellGroups.All, 100, cells));
            Assert.AreEqual(100, cells.Count);
        }

        [Test]
        public void TheDefaults_FitTheWholeRadiusUnderTheCap()
        {
            int side = 2 * RoMapDataGizmoSelector.DefaultRadius + 1;

            Assert.LessOrEqual(side * side, RoMapDataGizmoSelector.DefaultCap);
        }
    }
}
