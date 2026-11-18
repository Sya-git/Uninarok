using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Gnd
{
    /// <summary>
    /// A GND ground with the file's values as GRF reads them, in Ragnarok space, unconverted (PLAN-0004 4.1, D1). Every
    /// rule after the reader works on this type, so the rules are testable without the library and without data files.
    /// </summary>
    public sealed class RoGnd
    {
        public int VersionMajor, VersionMinor;
        /// <summary>Cubes along x (columns) and along rows, from the header; never assumed equal (FR-4).</summary>
        public int Width, Height;
        /// <summary>Cube edge in Ragnarok units (GRF <c>ProportionRatio</c>; 10 for the reference maps, FR-5).</summary>
        public float Zoom;
        /// <summary>Texture names as stored, relative to <c>texture/</c>.</summary>
        public IReadOnlyList<string> Textures = Array.Empty<string>();
        public IReadOnlyList<RoGndTile> Tiles = Array.Empty<RoGndTile>();
        /// <summary><c>Width * Height</c> entries; the cube at (column, row) is at <see cref="IndexOf"/>.</summary>
        public IReadOnlyList<RoGndCube> Cubes = Array.Empty<RoGndCube>();

        /// <summary>The cube index of (column, row): <c>row * Width + column</c> (PLAN-0004 4.1, FR-4).</summary>
        public int IndexOf(int column, int row) => row * Width + column;

        public RoGndCube CubeAt(int column, int row) => Cubes[IndexOf(column, row)];

        public override string ToString() => $"RoGnd {VersionMajor}.{VersionMinor} ({Width} x {Height}, zoom {Zoom})";
    }
}
