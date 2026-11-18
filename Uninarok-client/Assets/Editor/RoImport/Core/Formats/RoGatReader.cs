using System;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Paths;
using GrfGat = GRF.FileFormats.GatFormat.Gat;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's GAT reader (PLAN-0005 4.1): the only GAT code that touches GRF types. It copies the GRF members
    /// that correspond to file fields (the mapping verified by TASKS-0005 T0.2) into an <see cref="RoGat"/>. GRF does not
    /// expose the version, so the adapter takes it from bytes 4 and 5 next to its magic check (D10); GRF's preview and
    /// analysis members are never used, and <c>Gat.AutomaticallyFixNegativeGatTypes</c> is left at its default
    /// (<c>false</c>) so the raw types arrive unchanged. It never throws for bad input: every failure is <c>false</c>
    /// with a reason.
    /// </summary>
    public static class RoGatReader
    {
        const int HeaderSize = 14;
        const int CellSize = 20;

        public static bool TryRead(RoPath path, byte[] bytes, out RoGat gat, out string reason)
        {
            gat = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length < HeaderSize || bytes[0] != 'G' || bytes[1] != 'R' || bytes[2] != 'A' || bytes[3] != 'T')
                return Fail("not a GAT file (no 'GRAT' header)", out reason);

            GrfGat grf;
            try
            {
                grf = new GrfGat((GRF.MultiType)bytes);
            }
            catch (Exception e)
            {
                return Fail($"GRF could not read the cells: {e.GetType().Name}: {e.Message}", out reason);
            }

            int width = grf.Header.Width, height = grf.Header.Height;
            if (width <= 0 || height <= 0) return Fail($"invalid size {width} x {height}", out reason);
            long cells = (long)width * height;
            if (grf.Cells == null || grf.Cells.Length != cells)
                return Fail($"the cell table has {grf.Cells?.Length ?? 0} entries, expected {width} x {height}", out reason);
            // GRF reads past the end of a short array without always failing; the file must hold every cell.
            if (bytes.Length < HeaderSize + cells * CellSize)
                return Fail($"the file has {bytes.Length} bytes, {width} x {height} cells need {HeaderSize + cells * CellSize}", out reason);

            var heights = new float[cells * 4];
            var types = new uint[cells];
            for (int i = 0; i < cells; i++)
            {
                var c = grf.Cells[i];
                heights[i * 4 + 0] = c.H0;
                heights[i * 4 + 1] = c.H1;
                heights[i * 4 + 2] = c.H2;
                heights[i * 4 + 3] = c.H3;
                types[i] = unchecked((uint)(int)c.Type);
            }

            gat = new RoGat
            {
                VersionMajor = bytes[4],
                VersionMinor = bytes[5],
                Width = width,
                Height = height,
                Heights = heights,
                Types = types
            };
            reason = null;
            return true;
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
