using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>
    /// The pixels of a sheet, top row first (PLAN-0007 4.2, 4.3, SPEC-0007 FR-1, FR-7). Slot <c>s</c> is indexed frame
    /// <c>s</c>, then the true-colour frames. The padding around each frame repeats its edge pixels (edge bleed), so point
    /// sampling at a frame's border never picks up a neighbour.
    /// </summary>
    public static class RoSpriteSheetPixels
    {
        /// <summary>
        /// RGBA32: indexed pixels through the SPR's palette with alpha 255, palette index 0 fully transparent whatever its
        /// stored alpha (act-reference §5); true-colour pixels as stored.
        /// </summary>
        public static byte[] Color(RoSpr spr, RoSheetLayout layout)
        {
            Check(spr, layout);
            if (spr.Indexed.Count > 0 && (spr.Palette?.Colors == null || spr.Palette.Colors.Length < RoPal.Size))
                throw new ArgumentException("an SPR with indexed frames needs its palette", nameof(spr));

            var pixels = new byte[layout.Width * layout.Height * 4];
            for (int slot = 0; slot < spr.SlotCount; slot++)
            {
                var frame = spr.Slot(slot);
                var r = layout.Rects[slot];
                if (r.width == 0 || r.height == 0) continue;
                for (int y = 0; y < frame.Height; y++)
                {
                    int dst = ((r.y + y) * layout.Width + r.x) * 4;
                    if (frame.IsIndexed)
                    {
                        int src = y * frame.Width;
                        for (int x = 0; x < frame.Width; x++, dst += 4)
                        {
                            int k = frame.Indices[src + x];
                            if (k == 0) continue;   // stays 0, 0, 0, 0
                            Color32 c = spr.Palette.Colors[k];
                            pixels[dst] = c.r;
                            pixels[dst + 1] = c.g;
                            pixels[dst + 2] = c.b;
                            pixels[dst + 3] = 255;
                        }
                    }
                    else
                    {
                        Buffer.BlockCopy(frame.Rgba, y * frame.Width * 4, pixels, dst, frame.Width * 4);
                    }
                }
                Bleed(pixels, 4, layout, r);
            }
            return pixels;
        }

        /// <summary>R8: the palette index of indexed pixels; 0 for transparent and true-colour pixels.</summary>
        public static byte[] Index(RoSpr spr, RoSheetLayout layout)
        {
            Check(spr, layout);
            var pixels = new byte[layout.Width * layout.Height];
            for (int slot = 0; slot < spr.Indexed.Count; slot++)
            {
                var frame = spr.Indexed[slot];
                var r = layout.Rects[slot];
                if (r.width == 0 || r.height == 0) continue;
                for (int y = 0; y < frame.Height; y++)
                    Buffer.BlockCopy(frame.Indices, y * frame.Width, pixels, (r.y + y) * layout.Width + r.x, frame.Width);
                Bleed(pixels, 1, layout, r);
            }
            return pixels;
        }

        static void Check(RoSpr spr, RoSheetLayout layout)
        {
            if (spr == null) throw new ArgumentNullException(nameof(spr));
            if (layout?.Rects == null || layout.Rects.Length != spr.SlotCount)
                throw new ArgumentException("the layout does not match the SPR's slots", nameof(layout));
            for (int slot = 0; slot < spr.SlotCount; slot++)
            {
                var f = spr.Slot(slot);
                var r = layout.Rects[slot];
                if ((f.Width > 0 && f.Height > 0) && (r.width != f.Width || r.height != f.Height))
                    throw new ArgumentException($"slot {slot}: the layout's rect is {r.size}, the frame {f.Width} x {f.Height}", nameof(layout));
            }
        }

        /// <summary>Fills the <see cref="RoSpriteSheetPacker.Padding"/> ring around <paramref name="r"/> with its nearest edge pixel.</summary>
        static void Bleed(byte[] pixels, int bytesPerPixel, RoSheetLayout layout, RectInt r)
        {
            int p = RoSpriteSheetPacker.Padding;
            int x0 = Math.Max(0, r.x - p), x1 = Math.Min(layout.Width, r.xMax + p);
            int y0 = Math.Max(0, r.y - p), y1 = Math.Min(layout.Height, r.yMax + p);
            for (int y = y0; y < y1; y++)
            {
                int sy = Mathf.Clamp(y, r.y, r.yMax - 1);
                for (int x = x0; x < x1; x++)
                {
                    if (y >= r.y && y < r.yMax && x >= r.x && x < r.xMax) continue;
                    int sx = Mathf.Clamp(x, r.x, r.xMax - 1);
                    Buffer.BlockCopy(pixels, (sy * layout.Width + sx) * bytesPerPixel, pixels, (y * layout.Width + x) * bytesPerPixel, bytesPerPixel);
                }
            }
        }
    }
}
