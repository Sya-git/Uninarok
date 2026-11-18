using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;

namespace Uninarok.RoImport.Tests.Gnd
{
    /// <summary>
    /// Small GND maps built in code (TASKS-0004 T1.1, PLAN-0004 §6): every pure ground rule is tested on these, without GRF
    /// and without data files. Values are in Ragnarok space (Y down: a higher ground has a lower number), as the reader
    /// produces them.
    /// <para>Tiles: every cube with a top gets its own top tile (texture 0), and, when walls are on, every cube that has a
    /// neighbour in the next row gets a front tile and every cube that has one in the next column a side tile (texture 1).
    /// Tiles are added in cube order (top, front, side per cube), so a tile id tells which cube owns it.</para>
    /// </summary>
    public static class GndTestMaps
    {
        public const float DefaultZoom = 10f;
        public const string TopTexture = "test\\top.bmp";
        public const string WallTexture = "test\\wall.bmp";

        /// <summary>The height of corner (<paramref name="dx"/>, <paramref name="dz"/>), each 0 or 1, of the cube at (column, row).</summary>
        public delegate float CubeCorner(int column, int row, int dx, int dz);

        /// <summary>Tile coordinates as the reference maps store them: <c>u = (0, 1, 0, 1)</c>, <c>v = (1, 1, 0, 0)</c>.</summary>
        public static readonly Vector2 Uv00 = new Vector2(0, 1), Uv10 = new Vector2(1, 1), Uv01 = new Vector2(0, 0), Uv11 = new Vector2(1, 0);

        public static RoGnd Build(int width, int height, CubeCorner corner, float zoom = DefaultZoom,
                                  Func<int, int, bool> hasTop = null, bool walls = true)
        {
            var tiles = new List<RoGndTile>();
            var cubes = new RoGndCube[width * height];
            for (int row = 0; row < height; row++)
            for (int column = 0; column < width; column++)
            {
                int up = RoGndCube.None, front = RoGndCube.None, side = RoGndCube.None;
                if (hasTop == null || hasTop(column, row)) up = AddTile(tiles, 0);
                if (walls && row + 1 < height) front = AddTile(tiles, 1);
                if (walls && column + 1 < width) side = AddTile(tiles, 1);
                cubes[row * width + column] = new RoGndCube(
                    corner(column, row, 0, 0), corner(column, row, 1, 0), corner(column, row, 0, 1), corner(column, row, 1, 1),
                    up, front, side);
            }

            return new RoGnd
            {
                VersionMajor = 1,
                VersionMinor = 7,
                Width = width,
                Height = height,
                Zoom = zoom,
                Textures = new[] { TopTexture, WallTexture },
                Tiles = tiles,
                Cubes = cubes
            };
        }

        static int AddTile(List<RoGndTile> tiles, int texture)
        {
            tiles.Add(new RoGndTile(Uv00, Uv10, Uv01, Uv11, texture));
            return tiles.Count - 1;
        }

        /// <summary>Every corner at height 0.</summary>
        public static RoGnd Flat(int width = 4, int height = 4, float zoom = DefaultZoom) =>
            Build(width, height, (c, r, dx, dz) => 0f, zoom);

        /// <summary>4 x 4, columns 0 and 1 at height 0, columns 2 and 3 ten units higher: one step along column edge 2.</summary>
        public static RoGnd Step() => Build(4, 4, (c, r, dx, dz) => c < 2 ? 0f : -10f);

        /// <summary>4 x 4, a continuous slope rising 2.5 units per cube toward the higher columns (shared corners equal).</summary>
        public static RoGnd Ramp() => Build(4, 4, (c, r, dx, dz) => -2.5f * (c + dx));

        /// <summary>
        /// 4 x 4, rows 2 and 3 twenty units higher than rows 0 and 1 (a cliff along row edge 2), and the cube at
        /// (<see cref="GapColumn"/>, <see cref="GapRow"/>) without a top (a gap).
        /// </summary>
        public static RoGnd CliffWithGap() =>
            Build(4, 4, (c, r, dx, dz) => r < 2 ? 0f : -20f, hasTop: (c, r) => !(c == GapColumn && r == GapRow));

        public const int GapColumn = 1, GapRow = 0;

        /// <summary>
        /// A non-square map whose every cube is flat at its own height (<see cref="CubeHeight"/>), so each cube is
        /// recognisable from its height and rows and columns cannot be swapped unnoticed (FR-4).
        /// </summary>
        public static RoGnd NonSquare(int width, int height) => Build(width, height, (c, r, dx, dz) => CubeHeight(c, r));

        /// <summary>The height of every corner of the cube at (column, row) in <see cref="NonSquare"/> maps.</summary>
        public static float CubeHeight(int column, int row) => -(column + 10f * row);

        /// <summary>5 columns, 3 rows.</summary>
        public static RoGnd Wide() => NonSquare(5, 3);

        /// <summary>3 columns, 5 rows.</summary>
        public static RoGnd Tall() => NonSquare(3, 5);

        /// <summary>A flat 4 x 4 map with a cube edge of 5 Ragnarok units (FR-5).</summary>
        public static RoGnd Zoom5() => Flat(4, 4, 5f);

        /// <summary>A flat 40 x 33 map: 3 x 3 chunks of 16 cubes, the last row and column of chunks partial (FR-6).</summary>
        public static RoGnd Large() => Flat(40, 33);

        /// <summary>The tile count <see cref="Build"/> gives with walls on: tops plus front and side wall tiles.</summary>
        public static int ExpectedTiles(int width, int height, int tops) =>
            tops + width * (height - 1) + (width - 1) * height;
    }
}
