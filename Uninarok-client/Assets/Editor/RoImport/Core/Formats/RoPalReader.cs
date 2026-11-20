using System;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;
using GrfPal = GRF.FileFormats.PalFormat.Pal;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's PAL reader (PLAN-0007 4.1, SPEC-0007 FR-7, FR-8). A PAL is exactly 256 entries of R, G, B and an
    /// unused byte that the files leave 0 (TASKS-0007 T0.1); the adapter sets alpha 255 on every entry, and index 0 is made
    /// transparent later by rule. GRF accepts any length, so the adapter requires exactly 1,024 bytes. It never throws.
    /// </summary>
    public static class RoPalReader
    {
        public const int FileSize = RoPal.Size * 4;

        public static bool TryRead(RoPath path, byte[] bytes, out RoPal pal, out string reason)
        {
            pal = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length != FileSize) return Fail($"a palette is {FileSize} bytes, this file has {bytes.Length}", out reason);

            try
            {
                var grf = new GrfPal(bytes, GrfPal.FormatMode.PreserveOriginal);
                var table = grf.BytePalette;
                if (table == null || table.Length < FileSize) return Fail("GRF returned no palette", out reason);
                pal = new RoPal { Colors = RoSprReader.Colors(table, true) };
            }
            catch (Exception e)
            {
                pal = null;
                return Fail($"GRF could not read the palette: {e.GetType().Name}: {e.Message}", out reason);
            }

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
