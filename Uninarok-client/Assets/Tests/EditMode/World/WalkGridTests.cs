using NUnit.Framework;
using UnityEditor;
using Uninarok.Map;

namespace Uninarok.World.Tests
{
    // SPEC-0010 FR-6, FR-10 (PLAN-0010 4.3).
    public class WalkGridTests
    {
        internal static MapData LoadMap(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MapData"))
            {
                var map = AssetDatabase.LoadAssetAtPath<MapData>(AssetDatabase.GUIDToAssetPath(guid));
                if (map != null && map.MapName == name) return map;
            }
            Assert.Ignore($"no imported map data of {name}");
            return null;
        }

        [Test]
        public void WalkTiming_200Straight5_Is1000ms()
        {
            int ms = 0;
            for (int i = 0; i < 5; i++) ms += WalkTiming.StepMs(200, false);
            Assert.AreEqual(1000, ms);
        }

        [Test]
        public void WalkTiming_OneDiagonal_Is1080ms()
        {
            int ms = WalkTiming.StepMs(200, true);
            for (int i = 0; i < 4; i++) ms += WalkTiming.StepMs(200, false);
            Assert.AreEqual(1080, ms);
            Assert.AreEqual(210, WalkTiming.StepMs(150, true));
            Assert.AreEqual(190, WalkTiming.StepMs(136, true), "whole milliseconds, rounded down as the server");
        }

        [Test]
        public void WalkTiming_DiagonalDirections() =>
            Assert.AreEqual("01010101", string.Concat(System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, 8), d => WalkTiming.IsDiagonal(d) ? "1" : "0")));

        [Test, Category("NeedsData")]
        public void MapDataWalkGrid_AgreesWithTheMapData_OnEveryCellOfProntera()
        {
            var map = LoadMap("prontera");
            var grid = new MapDataWalkGrid(map);
            Assert.AreEqual((map.CellsX, map.CellsZ), (grid.Width, grid.Height));
            int passable = 0, different = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    if (grid.IsPassable(x, y)) passable++;
                    if (grid.IsPassable(x, y) != map.IsWalkable(x, y)) different++;
                }
            Assert.AreEqual(0, different);
            Assert.Greater(passable, 1000);
            Assert.IsFalse(grid.IsPassable(-1, 0) || grid.IsPassable(0, -1) || grid.IsPassable(grid.Width, 0) || grid.IsPassable(0, grid.Height), "outside");
        }

        [Test]
        public void UnknownWalkGrid_PassesNothing() => Assert.IsFalse(new UnknownWalkGrid().IsPassable(0, 0));
    }
}
