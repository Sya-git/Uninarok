using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using GrfGnd = GRF.FileFormats.GndFormat.Gnd;
using EncodingService = Utilities.Services.EncodingService;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's GND reader (PLAN-0004 4.1, D1): the only GND code that touches GRF types. It copies the GRF
    /// members that correspond to file fields (the mapping verified by TASKS-0004 T0.2) into an <see cref="RoGnd"/>;
    /// GRF's derived, lightmap and water members are never used. It never throws for bad input: every failure is
    /// <c>false</c> with a reason. A tile or texture index outside its table is reported once per map as
    /// <see cref="RoIssueCode.GndBadIndex"/> and replaced by <see cref="RoGndCube.None"/> (D13).
    /// </summary>
    public static class RoGndReader
    {
        const string Magic = "GRGN";

        public static bool TryRead(RoPath path, byte[] bytes, RoReport report, out RoGnd gnd, out string reason)
        {
            gnd = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length < 6 || bytes[0] != 'G' || bytes[1] != 'R' || bytes[2] != 'G' || bytes[3] != 'N')
                return Fail($"not a GND file (no '{Magic}' header)", out reason);

            GrfGnd grf;
            int badIndices;
            // GRF decodes strings with its display encoding, UTF-8 in the editor, which loses the Korean texture names
            // (CP949 in the file). Read with the Korean encoding and restore the previous one (PLAN-0004 4.1, as RSM).
            var previous = EncodingService.DisplayEncoding;
            try
            {
                EncodingService.DisplayEncoding = EncodingService.Korean;
                grf = new GrfGnd((GRF.MultiType)bytes);
                if (!TryConvert(grf, out gnd, out badIndices, out reason)) return false;
            }
            catch (Exception e)
            {
                gnd = null;
                return Fail($"GRF could not read the ground: {e.GetType().Name}: {e.Message}", out reason);
            }
            finally
            {
                EncodingService.DisplayEncoding = previous;
            }

            if (badIndices > 0)
                report?.Warn(RoIssueCode.GndBadIndex, path.Canonical, null,
                    $"{badIndices} tile or texture indices outside their tables, treated as none");
            reason = null;
            return true;
        }

        static bool TryConvert(GrfGnd grf, out RoGnd gnd, out int badIndices, out string reason)
        {
            gnd = null;
            badIndices = 0;
            var header = grf.Header;
            int width = header.Width, height = header.Height;
            if (width <= 0 || height <= 0) return Fail($"invalid size {width} x {height}", out reason);
            if (grf.Cubes == null || grf.Cubes.Count != width * height)
                return Fail($"the cube table has {grf.Cubes?.Count ?? 0} entries, expected {width} x {height}", out reason);
            if (grf.Textures == null || grf.Textures.Count != header.TextureCount)
                return Fail($"the texture table has {grf.Textures?.Count ?? 0} entries, expected {header.TextureCount}", out reason);
            if (grf.Tiles == null) return Fail("the tile table is missing", out reason);

            int textureCount = grf.Textures.Count;
            var tiles = new RoGndTile[grf.Tiles.Count];
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = grf.Tiles[i];
                if (t.TexCoords == null || t.TexCoords.Length < 4) return Fail($"tile {i} has no texture coordinates", out reason);
                tiles[i] = new RoGndTile(Uv(t.TexCoords[0]), Uv(t.TexCoords[1]), Uv(t.TexCoords[2]), Uv(t.TexCoords[3]),
                    CheckIndex(t.TextureIndex, textureCount, ref badIndices));
            }

            var cubes = new RoGndCube[grf.Cubes.Count];
            for (int i = 0; i < cubes.Length; i++)
            {
                var c = grf.Cubes[i];
                cubes[i] = new RoGndCube(c.BottomLeft, c.BottomRight, c.TopLeft, c.TopRight,
                    CheckIndex(c.TileUp, tiles.Length, ref badIndices),
                    CheckIndex(c.TileFront, tiles.Length, ref badIndices),
                    CheckIndex(c.TileSide, tiles.Length, ref badIndices));
            }

            gnd = new RoGnd
            {
                VersionMajor = header.MajorVersion,
                VersionMinor = header.MinorVersion,
                Width = width,
                Height = height,
                Zoom = header.ProportionRatio,
                Textures = new List<string>(grf.Textures),
                Tiles = tiles,
                Cubes = cubes
            };
            reason = null;
            return true;
        }

        static Vector2 Uv(GRF.Graphics.TkVector2 v) => new Vector2(v.X, v.Y);

        /// <summary>An index into a table of <paramref name="count"/> entries; none stays none, anything else outside is counted and becomes none.</summary>
        static int CheckIndex(int index, int count, ref int bad)
        {
            if (index == RoGndCube.None) return RoGndCube.None;
            if (index >= 0 && index < count) return index;
            bad++;
            return RoGndCube.None;
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
