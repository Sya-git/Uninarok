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

        /// <summary>The raw GAT type's low byte of a cell, for tools and the inspector; outside: 0.</summary>
        internal byte RawTypeAt(int x, int z) => InBoundsInternal(x, z) ? rawTypes[z * cellsX + x] : (byte)0;

        bool InBoundsInternal(int x, int z) => x >= 0 && z >= 0 && x < cellsX && z < cellsZ;
    }
}
