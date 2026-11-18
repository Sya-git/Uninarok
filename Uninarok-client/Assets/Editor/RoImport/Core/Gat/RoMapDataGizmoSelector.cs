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

    /// <summary>
    /// Which cells the map data gizmos draw (PLAN-0005 4.6, FR-7, D13): the cells of a region (a radius around the focus or
    /// a rectangle the user picked), clipped to the map, in rings around the centre so the nearest come first, without the
    /// hidden groups, and at most a cap. Pure: reads the <see cref="MapData"/> arrays only.
    /// </summary>
    public static class RoMapDataGizmoSelector
    {
        public const int DefaultRadius = 38;
        public const int DefaultCap = 6000;

        public static RoMapCellGroups GroupOf(MapCellType type)
        {
            switch (type)
            {
                case MapCellType.Walkable:
                case MapCellType.Type2:
                case MapCellType.Type4:
                case MapCellType.Type6: return RoMapCellGroups.Walkable;
                case MapCellType.Water: return RoMapCellGroups.Water;
                case MapCellType.Wall: return RoMapCellGroups.Wall;
                case MapCellType.Cliff: return RoMapCellGroups.Cliff;
                default: return RoMapCellGroups.Other;
            }
        }

        /// <summary>The square of cells within <paramref name="radius"/> of <paramref name="centre"/>, clamped to the map.</summary>
        public static RectInt RadiusRegion(MapData data, Vector2Int centre, int radius)
        {
            radius = Mathf.Max(0, radius);
            return Clip(data, new RectInt(centre.x - radius, centre.y - radius, 2 * radius + 1, 2 * radius + 1));
        }

        /// <summary>
        /// Fills <paramref name="cells"/> (cleared first) with the indices (<c>z * CellsX + x</c>) of the visible cells of
        /// <paramref name="region"/>, nearest to <paramref name="centre"/> first, at most <paramref name="cap"/>. True when
        /// the cap left visible cells out (the overlay shows a notice).
        /// </summary>
        public static bool Select(MapData data, RectInt region, Vector2Int centre, RoMapCellGroups visible, int cap, List<int> cells)
        {
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            cells.Clear();
            if (data == null) return false;
            var r = Clip(data, region);
            if (r.width <= 0 || r.height <= 0 || visible == RoMapCellGroups.None) return false;

            int cx = Mathf.Clamp(centre.x, r.xMin, r.xMax - 1), cz = Mathf.Clamp(centre.y, r.yMin, r.yMax - 1);
            int rings = Mathf.Max(Mathf.Max(cx - r.xMin, r.xMax - 1 - cx), Mathf.Max(cz - r.yMin, r.yMax - 1 - cz));
            if (!Add(data, r, cx, cz, visible, cap, cells)) return true;
            for (int ring = 1; ring <= rings; ring++)
            {
                for (int x = cx - ring; x <= cx + ring; x++)
                {
                    if (!Add(data, r, x, cz - ring, visible, cap, cells)) return true;
                    if (!Add(data, r, x, cz + ring, visible, cap, cells)) return true;
                }
                for (int z = cz - ring + 1; z <= cz + ring - 1; z++)
                {
                    if (!Add(data, r, cx - ring, z, visible, cap, cells)) return true;
                    if (!Add(data, r, cx + ring, z, visible, cap, cells)) return true;
                }
            }
            return false;
        }

        /// <summary>Adds the cell when it is inside and visible; false when it would go over the cap.</summary>
        static bool Add(MapData data, RectInt r, int x, int z, RoMapCellGroups visible, int cap, List<int> cells)
        {
            if (x < r.xMin || x >= r.xMax || z < r.yMin || z >= r.yMax) return true;
            if ((GroupOf(data.TypeAt(x, z)) & visible) == 0) return true;
            if (cells.Count >= cap) return false;
            cells.Add(z * data.CellsX + x);
            return true;
        }

        static RectInt Clip(MapData data, RectInt region)
        {
            int xMin = Mathf.Max(region.xMin, 0), zMin = Mathf.Max(region.yMin, 0);
            int xMax = Mathf.Min(region.xMax, data.CellsX), zMax = Mathf.Min(region.yMax, data.CellsZ);
            return new RectInt(xMin, zMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, zMax - zMin));
        }
    }
}
