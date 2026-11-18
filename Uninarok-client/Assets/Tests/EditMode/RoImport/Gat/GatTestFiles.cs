using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Uninarok.RoImport.Core.Gat;

namespace Uninarok.RoImport.Tests.Gat
{
    /// <summary>
    /// Writes synthetic <c>.gat</c> and <c>.gnd</c> files for the converter tests (TASKS-0005 T4.1), following the layouts
    /// of the format documents. Test code only: production code never writes or parses these bytes (PLAN-0002 §1).
    /// </summary>
    public static class GatTestFiles
    {
        /// <summary>A GAT 1.2 file: <c>GRAT</c>, version, width, height, then 20 bytes per cell.</summary>
        public static byte[] Gat(RoGat gat)
        {
            var b = new List<byte>(14 + gat.CellCount * 20);
            b.AddRange(Encoding.ASCII.GetBytes("GRAT"));
            b.Add((byte)gat.VersionMajor);
            b.Add((byte)gat.VersionMinor);
            b.AddRange(BitConverter.GetBytes(gat.Width));
            b.AddRange(BitConverter.GetBytes(gat.Height));
            for (int i = 0; i < gat.CellCount; i++)
            {
                for (int k = 0; k < 4; k++) b.AddRange(BitConverter.GetBytes(gat.Heights[i * 4 + k]));
                b.AddRange(BitConverter.GetBytes(gat.Types[i]));
            }
            return b.ToArray();
        }

        /// <summary>A flat GND 1.7 file without textures, lightmaps or tiles: only the size and zoom matter here.</summary>
        public static byte[] Gnd(int width, int height, float zoom = 10f)
        {
            var b = new List<byte>();
            void I(int v) => b.AddRange(BitConverter.GetBytes(v));
            void F(float v) => b.AddRange(BitConverter.GetBytes(v));
            b.AddRange(Encoding.ASCII.GetBytes("GRGN"));
            b.Add(1);
            b.Add(7);
            I(width);
            I(height);
            F(zoom);
            I(0);    // textures
            I(80);   // texture name length
            I(0);    // lightmaps: count, width, height, cells per grid
            I(8);
            I(8);
            I(1);
            I(0);    // tiles
            for (int i = 0; i < width * height; i++)
            {
                F(0); F(0); F(0); F(0);
                I(-1); I(-1); I(-1);
            }
            return b.ToArray();
        }

        /// <summary>Writes <c>&lt;name&gt;.gat</c> and <c>&lt;name&gt;.gnd</c> into a data folder.</summary>
        public static void WriteMap(string dataFolder, string name, RoGat gat, int gndWidth, int gndHeight)
        {
            Directory.CreateDirectory(dataFolder);
            File.WriteAllBytes(Path.Combine(dataFolder, name + ".gat"), Gat(gat));
            File.WriteAllBytes(Path.Combine(dataFolder, name + ".gnd"), Gnd(gndWidth, gndHeight));
        }
    }
}
