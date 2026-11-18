using System;
using System.IO;
using NUnit.Framework;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Tests.Gnd
{
    /// <summary>
    /// Access to the real source data for tests marked <c>NeedsData</c> (PLAN-0004 §6). When the data folder or a file is
    /// absent the test is ignored with a message, never failed. Map files sit at the data root (PLAN-0004 D11).
    /// </summary>
    public static class GndTestData
    {
        public const string Category = "NeedsData";

        /// <summary>The bytes of <c>&lt;map&gt;.gnd</c>, or an ignored test when the data is not there.</summary>
        public static byte[] MapBytes(string map)
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            string file = Path.Combine(root, map + ".gnd");
            if (!File.Exists(file)) Assert.Ignore($"source file not found: {map}.gnd");
            return File.ReadAllBytes(file);
        }

        /// <summary>
        /// Byte offsets inside a GND 1.7 file, from the layout of the format document (<c>GND.MD</c>), so a test can
        /// patch a field. Test code only: production code never parses GND bytes (PLAN-0002 §1).
        /// </summary>
        public sealed class Layout
        {
            public int TileCount, TilesOffset, CubesOffset;

            public const int TileSize = 40, TileTextureField = 32;       // 8 floats, int16 texture, uint16 lightmap, 4 color bytes
            public const int CubeSize = 28, CubeTileUpField = 16, CubeTileFrontField = 20, CubeTileSideField = 24;

            public static Layout Of(byte[] bytes)
            {
                int textureCount = BitConverter.ToInt32(bytes, 18), textureNameLength = BitConverter.ToInt32(bytes, 22);
                int lightmaps = 26 + textureCount * textureNameLength;
                int count = BitConverter.ToInt32(bytes, lightmaps), w = BitConverter.ToInt32(bytes, lightmaps + 4),
                    h = BitConverter.ToInt32(bytes, lightmaps + 8);
                int tiles = lightmaps + 16 + count * w * h * 4;
                int tileCount = BitConverter.ToInt32(bytes, tiles);
                return new Layout { TileCount = tileCount, TilesOffset = tiles + 4, CubesOffset = tiles + 4 + tileCount * TileSize };
            }

            public void SetCubeTile(byte[] bytes, int cube, int field, int value) =>
                Array.Copy(BitConverter.GetBytes(value), 0, bytes, CubesOffset + cube * CubeSize + field, 4);

            public void SetTileTexture(byte[] bytes, int tile, short value) =>
                Array.Copy(BitConverter.GetBytes(value), 0, bytes, TilesOffset + tile * TileSize + TileTextureField, 2);
        }
    }
}
