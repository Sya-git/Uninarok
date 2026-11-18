using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.Map;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>The toggle groups of the map data gizmos (PLAN-0005 4.6).</summary>
    [Flags]
    public enum RoMapCellGroups
    {
        None = 0,
        /// <summary>Types 0, 2, 4, 6.</summary>
        Walkable = 1,
        /// <summary>Type 3.</summary>
        Water = 2,
        /// <summary>Type 1.</summary>
        Wall = 4,
        /// <summary>Type 5.</summary>
        Cliff = 8,
        Other = 16,
        All = Walkable | Water | Wall | Cliff | Other
    }

    /// <summary>Which cells the map data gizmos draw (PLAN-0005 4.6, FR-7). Implemented by TASKS-0005 T5.2.</summary>
    public static class RoMapDataGizmoSelector
    {
        public const int DefaultRadius = 38;
        public const int DefaultCap = 6000;

        public static RoMapCellGroups GroupOf(MapCellType type) => throw new NotImplementedException();

        public static RectInt RadiusRegion(MapData data, Vector2Int centre, int radius) => throw new NotImplementedException();

        public static bool Select(MapData data, RectInt region, Vector2Int centre, RoMapCellGroups visible, int cap, List<int> cells) =>
            throw new NotImplementedException();
    }
}
