using System;

namespace Uninarok.RoImport.Core.Gat
{
    /// <summary>
    /// A GAT cell grid with the file's values as GRF reads them, unconverted (PLAN-0005 4.1). Every rule after the reader
    /// works on this type, so the rules are testable without the library and without data files.
    /// </summary>
    public sealed class RoGat
    {
        public int VersionMajor, VersionMinor;
        /// <summary>Cells along x (columns) and along rows, from the header; never assumed equal.</summary>
        public int Width, Height;
        /// <summary>
        /// Four per cell, <c>H00, H10, H01, H11</c> (south-west, south-east, north-west, north-east), in Ragnarok units,
        /// unconverted (Y points down). Cell <c>i</c> starts at <c>4 * i</c>.
        /// </summary>
        public float[] Heights = Array.Empty<float>();
        /// <summary>The raw 32-bit terrain type per cell, unchanged (PLAN-0005 4.2, D2).</summary>
        public uint[] Types = Array.Empty<uint>();

        /// <summary><c>Width * Height</c>.</summary>
        public int CellCount => Width * Height;

        /// <summary>The cell index of (column, row): <c>row * Width + column</c>.</summary>
        public int IndexOf(int column, int row) => row * Width + column;

        public override string ToString() => $"RoGat {VersionMajor}.{VersionMinor} ({Width} x {Height})";
    }
}
