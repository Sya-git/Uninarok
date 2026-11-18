using System;
using UnityEngine;

namespace Uninarok.Map
{
    /// <summary>
    /// The walkability and height data of one map (SPEC-0005, PLAN-0005 4.3), created at import time from the GAT file.
    /// Flat arrays in cell order (<c>z * CellsX + x</c>) and the cell geometry from the importer's map space, so the
    /// runtime has only arithmetic and never needs the source file or importer code (FR-4, FR-5, D3).
    /// </summary>
    public sealed class MapData : ScriptableObject
    {
        // identity and geometry
        [SerializeField] string mapName;
        [SerializeField] int cellsX, cellsZ;
        [SerializeField] float cellSize;
        [SerializeField] Vector2 origin;
        [SerializeField] float rowSign = 1f;
        [SerializeField] bool diagonalFlip;

        // cell data, flat arrays
        [SerializeField] float[] heights = new float[0];
        [SerializeField] byte[] types = new byte[0];
        [SerializeField] byte[] flags = new byte[0];
        [SerializeField] byte[] rawTypes = new byte[0];

        // map-wide settings, filled by PLAN-0006
        [SerializeField] bool hasSettings;
        [SerializeField] MapSettings settings = new MapSettings();

        public string MapName => mapName;
        /// <summary>Cells along Unity x (the GAT width).</summary>
        public int CellsX => cellsX;
        /// <summary>Cells along rows (the GAT height).</summary>
        public int CellsZ => cellsZ;
        /// <summary>Unity units per cell edge (0.5 at the default zoom).</summary>
        public float CellSize => cellSize;
        /// <summary>Unity x and z of the outer corner of cell (0, 0).</summary>
        public Vector2 Origin => origin;
        /// <summary>+1 or -1: the Unity z direction of increasing rows.</summary>
        public float RowSign => rowSign;
        /// <summary>False: cells are split by the diagonal H10 to H01 (the ground's rule); true: H00 to H11.</summary>
        public bool DiagonalFlip => diagonalFlip;
        public bool HasSettings => hasSettings;
        public MapSettings Settings => settings;

        /// <summary>Bytes of cell data held by the asset (FR-5).</summary>
        internal long CellDataBytes => (long)heights.Length * sizeof(float) + types.Length + flags.Length + rawTypes.Length;

        /// <summary>
        /// Fills the asset (the importer and tests). Heights are four per cell (H00, H10, H01, H11) in Unity units;
        /// the other arrays are one entry per cell. Throws on inconsistent input: this is importer code, never gameplay.
        /// </summary>
        internal void Assign(string name, int sizeX, int sizeZ, float edge, Vector2 corner, float rows, bool flip,
            float[] cellHeights, byte[] cellTypes, byte[] cellFlags, byte[] cellRawTypes)
        {
            if (sizeX <= 0 || sizeZ <= 0) throw new ArgumentOutOfRangeException(nameof(sizeX), $"invalid size {sizeX} x {sizeZ}");
            if (!(edge > 0f)) throw new ArgumentOutOfRangeException(nameof(edge), "the cell size must be positive");
            if (rows != 1f && rows != -1f) throw new ArgumentOutOfRangeException(nameof(rows), "the row sign is +1 or -1");
            int count = sizeX * sizeZ;
            if (cellHeights == null || cellHeights.Length != count * 4) throw new ArgumentException($"expected {count * 4} heights", nameof(cellHeights));
            if (cellTypes == null || cellTypes.Length != count) throw new ArgumentException($"expected {count} types", nameof(cellTypes));
            if (cellFlags == null || cellFlags.Length != count) throw new ArgumentException($"expected {count} flags", nameof(cellFlags));
            if (cellRawTypes == null || cellRawTypes.Length != count) throw new ArgumentException($"expected {count} raw types", nameof(cellRawTypes));

            mapName = name;
            cellsX = sizeX;
            cellsZ = sizeZ;
            cellSize = edge;
            origin = corner;
            rowSign = rows;
            diagonalFlip = flip;
            heights = cellHeights;
            types = cellTypes;
            flags = cellFlags;
            rawTypes = cellRawTypes;
        }

        // ---- queries (FR-2): no allocation, no exception ----

        /// <summary>The cell under a Unity position (y ignored); false outside the map.</summary>
        public bool TryGetCell(Vector3 position, out int x, out int z)
        {
            if (TryLocal(position, out float fx, out float fz))
            {
                x = (int)fx;
                z = (int)fz;
                return true;
            }
            x = z = -1;
            return false;
        }

        public bool InBounds(int x, int z) => (uint)x < (uint)cellsX && (uint)z < (uint)cellsZ;

        /// <summary>Outside the map: false.</summary>
        public bool IsWalkable(int x, int z) => (FlagsAt(x, z) & MapCellFlags.Walkable) != 0;

        /// <summary>Outside the map: false.</summary>
        public bool IsWalkable(Vector3 position) => TryGetCell(position, out int x, out int z) && IsWalkable(x, z);

        /// <summary>Outside the map: <see cref="MapCellType.Other"/>.</summary>
        public MapCellType TypeAt(int x, int z) => InBounds(x, z) ? (MapCellType)types[z * cellsX + x] : MapCellType.Other;

        /// <summary>Outside the map: <see cref="MapCellFlags.None"/>.</summary>
        public MapCellFlags FlagsAt(int x, int z) => InBounds(x, z) ? (MapCellFlags)flags[z * cellsX + x] : MapCellFlags.None;

        /// <summary>Walkable with the water flag; outside the map: false.</summary>
        public bool IsWalkableWater(int x, int z)
        {
            const MapCellFlags both = MapCellFlags.Walkable | MapCellFlags.Water;
            return (FlagsAt(x, z) & both) == both;
        }

        /// <summary>The height under a position, interpolated on the cell's triangle (D5); false outside the map.</summary>
        public bool TryGetHeight(Vector3 position, out float y)
        {
            if (!TryLocal(position, out float fx, out float fz))
            {
                y = 0f;
                return false;
            }
            int x = (int)fx, z = (int)fz;
            y = Interpolate(z * cellsX + x, fx - x, fz - z);
            return true;
        }

        /// <summary>
        /// The centre of a cell, y by the diagonal rule (PLAN-0005 4.4, D11). Outside the map the position is still the
        /// centre of that grid cell, at height 0.
        /// </summary>
        public Vector3 CellCenter(int x, int z)
        {
            float y = InBounds(x, z) ? Interpolate(z * cellsX + x, 0.5f, 0.5f) : 0f;
            return new Vector3(origin.x + (x + 0.5f) * cellSize, y, origin.y + (z + 0.5f) * cellSize * rowSign);
        }

        /// <summary>H00, H10, H01, H11 of a cell in Unity units; outside the map: zero.</summary>
        public Vector4 CornerHeights(int x, int z)
        {
            if (!InBounds(x, z)) return Vector4.zero;
            int h = (z * cellsX + x) * 4;
            return new Vector4(heights[h], heights[h + 1], heights[h + 2], heights[h + 3]);
        }

        /// <summary>The raw GAT type's low byte of a cell, for tools and the inspector; outside: 0.</summary>
        internal byte RawTypeAt(int x, int z) => InBounds(x, z) ? rawTypes[z * cellsX + x] : (byte)0;

        /// <summary>Fractional cell coordinates of a position; false outside the map (NaN fails every comparison).</summary>
        bool TryLocal(Vector3 position, out float fx, out float fz)
        {
            fx = (position.x - origin.x) / cellSize;
            fz = (position.z - origin.y) * rowSign / cellSize;
            return fx >= 0f && fz >= 0f && fx < cellsX && fz < cellsZ;
        }

        /// <summary>
        /// The height at (u, v) inside cell <paramref name="cell"/>, on the triangle the cell's diagonal puts it in, the
        /// same split as the ground mesh (PLAN-0004 4.3, D5).
        /// </summary>
        float Interpolate(int cell, float u, float v)
        {
            int h = cell * 4;
            float h00 = heights[h], h10 = heights[h + 1], h01 = heights[h + 2], h11 = heights[h + 3];
            if (!diagonalFlip)
            {
                // diagonal H10 to H01: triangles (00, 10, 01) and (10, 11, 01)
                return u + v <= 1f
                    ? h00 + u * (h10 - h00) + v * (h01 - h00)
                    : h11 + (1f - u) * (h01 - h11) + (1f - v) * (h10 - h11);
            }
            // diagonal H00 to H11: triangles (00, 10, 11) and (00, 11, 01)
            return u >= v
                ? h00 + u * (h10 - h00) + v * (h11 - h10)
                : h00 + v * (h01 - h00) + u * (h11 - h01);
        }
    }
}
