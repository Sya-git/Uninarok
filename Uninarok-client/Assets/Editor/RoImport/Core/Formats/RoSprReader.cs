using System;
using System.Collections.Generic;
using UnityEngine;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Sprites;
using GrfSpr = GRF.FileFormats.SprFormat.Spr;
using GrfImageType = GRF.Image.GrfImageType;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over GRF's SPR reader (PLAN-0007 4.1, SPEC-0007 FR-1, FR-8): the only SPR code that touches GRF types. It
    /// copies the members recorded by TASKS-0007 T0.1: frames split by type in file order, indices as stored, true-colour
    /// pixels reordered from GRF's B, G, R, A to R, G, B, A, both top row first, and the embedded palette. It never throws:
    /// every failure is <c>false</c> with a reason. Values are raw; no rule of PLAN-0007 4.3 is applied.
    /// </summary>
    public static class RoSprReader
    {
        public static bool TryRead(RoPath path, byte[] bytes, out RoSpr spr, out string reason)
        {
            spr = null;
            if (bytes == null || bytes.Length == 0) return Fail("the file is empty", out reason);
            if (bytes.Length < 6 || bytes[0] != 'S' || bytes[1] != 'P') return Fail("not an SPR file (no 'SP' header)", out reason);

            // D11: GRF accepts a truncated SPR and keeps the frames that fit, so compare with the counts the header declares
            // (minor, major, then the indexed count and, from version 2.0, the true-colour count, little-endian).
            int major = bytes[3];
            int declaredIndexed = bytes[4] | bytes[5] << 8;
            int declaredTrueColor = 0;
            if (major >= 2)
            {
                if (bytes.Length < 8) return Fail("the header is cut", out reason);
                declaredTrueColor = bytes[6] | bytes[7] << 8;
            }

            GrfSpr grf;
            try
            {
                grf = new GrfSpr((GRF.MultiType)bytes);
            }
            catch (Exception e)
            {
                return Fail($"GRF could not read the sprite: {e.GetType().Name}: {e.Message}", out reason);
            }

            try
            {
                var indexed = new List<RoSprFrame>();
                var trueColor = new List<RoSprFrame>();
                foreach (var image in grf.Images)
                {
                    if (image == null) return Fail("a frame is missing", out reason);
                    int w = image.Width, h = image.Height;
                    var pixels = image.Pixels;
                    switch (image.GrfImageType)
                    {
                        case GrfImageType.Indexed8:
                            if (pixels == null || pixels.Length != w * h) return Fail($"indexed frame {indexed.Count} has the wrong size", out reason);
                            indexed.Add(RoSprFrame.FromIndices(w, h, pixels));
                            break;
                        case GrfImageType.Bgra32:
                            if (pixels == null || pixels.Length != w * h * 4) return Fail($"true-colour frame {trueColor.Count} has the wrong size", out reason);
                            trueColor.Add(RoSprFrame.FromRgba(w, h, BgraToRgba(pixels)));
                            break;
                        default:
                            return Fail($"a frame has the unexpected type {image.GrfImageType}", out reason);
                    }
                }

                if (indexed.Count != declaredIndexed || trueColor.Count != declaredTrueColor)
                    return Fail($"the file is cut: the header declares {declaredIndexed} indexed and {declaredTrueColor} true-colour " +
                                $"frames, {indexed.Count} and {trueColor.Count} were read", out reason);

                RoPal palette = null;
                var table = grf.Palette?.BytePalette;
                if (table != null && table.Length >= RoPal.Size * 4) palette = new RoPal { Colors = Colors(table, false) };
                if (palette == null && indexed.Count > 0) return Fail("the file has indexed frames but no palette", out reason);

                spr = new RoSpr
                {
                    VersionMajor = grf.Header.MajorVersion,
                    VersionMinor = grf.Header.MinorVersion,
                    Indexed = indexed,
                    TrueColor = trueColor,
                    Palette = palette
                };
            }
            catch (Exception e)
            {
                spr = null;
                return Fail($"GRF could not read the sprite: {e.GetType().Name}: {e.Message}", out reason);
            }

            reason = null;
            return true;
        }

        static byte[] BgraToRgba(byte[] bgra)
        {
            var rgba = new byte[bgra.Length];
            for (int i = 0; i < bgra.Length; i += 4)
            {
                rgba[i] = bgra[i + 2];
                rgba[i + 1] = bgra[i + 1];
                rgba[i + 2] = bgra[i];
                rgba[i + 3] = bgra[i + 3];
            }
            return rgba;
        }

        /// <summary>A GRF palette table (R, G, B, A per entry) as colours; <paramref name="opaque"/> sets alpha 255 on every entry.</summary>
        internal static Color32[] Colors(byte[] table, bool opaque)
        {
            var colors = new Color32[RoPal.Size];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32(table[i * 4], table[i * 4 + 1], table[i * 4 + 2], opaque ? (byte)255 : table[i * 4 + 3]);
            return colors;
        }

        static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
