using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Sprites
{
    /// <summary>
    /// Where each frame of an SPR goes in its sheet (PLAN-0007 4.2, SPEC-0007 FR-1, FR-9). Our own deterministic shelf
    /// packer: frames sorted by height (then slot), shelves filled left to right, every frame with
    /// <see cref="Padding"/> pixels around it, the sheet a power of two in each direction and at most <see cref="MaxSize"/>.
    /// Every power-of-two width is tried and the smallest sheet wins (then the squarest, then the narrowest), so the same
    /// sizes always give the same layout. <c>Texture2D.PackTextures</c> is not used: its layout is not stable.
    /// </summary>
    public static class RoSpriteSheetPacker
    {
        public const int Padding = 2, MaxSize = 4096;

        /// <summary>Packs one rectangle per size; false only when the frames do not fit one <see cref="MaxSize"/> sheet.</summary>
        public static bool TryPack(IReadOnlyList<Vector2Int> sizes, out RoSheetLayout layout)
        {
            layout = null;
            if (sizes == null) return false;

            var order = new List<int>(sizes.Count);
            int widest = 0;
            for (int i = 0; i < sizes.Count; i++)
            {
                var s = sizes[i];
                if (s.x < 0 || s.y < 0) return false;
                if (s.x == 0 || s.y == 0) continue;   // takes no space
                if (s.x + 2 * Padding > MaxSize || s.y + 2 * Padding > MaxSize) return false;
                widest = Math.Max(widest, s.x + 2 * Padding);
                order.Add(i);
            }
            order.Sort((a, b) =>
            {
                int byHeight = sizes[b].y.CompareTo(sizes[a].y);
                return byHeight != 0 ? byHeight : a.CompareTo(b);
            });

            RectInt[] best = null;
            int bestW = 0, bestH = 0;
            for (int width = NextPowerOfTwo(Math.Max(1, widest)); width <= MaxSize; width *= 2)
            {
                var rects = new RectInt[sizes.Count];
                int used = Shelve(sizes, order, width, rects);
                int height = NextPowerOfTwo(Math.Max(1, used));
                if (height > MaxSize) continue;
                if (best == null || Better(width, height, bestW, bestH))
                {
                    best = rects;
                    bestW = width;
                    bestH = height;
                }
            }

            if (best == null) return false;
            layout = new RoSheetLayout { Width = bestW, Height = bestH, Rects = best };
            return true;
        }

        /// <summary>Places the frames on shelves of <paramref name="width"/>; returns the height used.</summary>
        static int Shelve(IReadOnlyList<Vector2Int> sizes, List<int> order, int width, RectInt[] rects)
        {
            int x = 0, y = 0, shelf = 0;
            foreach (int i in order)
            {
                int w = sizes[i].x + 2 * Padding, h = sizes[i].y + 2 * Padding;
                if (x + w > width)
                {
                    y += shelf;
                    x = 0;
                    shelf = 0;
                }
                rects[i] = new RectInt(x + Padding, y + Padding, sizes[i].x, sizes[i].y);
                x += w;
                shelf = Math.Max(shelf, h);
            }
            return y + shelf;
        }

        static bool Better(int w, int h, int bestW, int bestH)
        {
            long area = (long)w * h, bestArea = (long)bestW * bestH;
            if (area != bestArea) return area < bestArea;
            int side = Math.Max(w, h), bestSide = Math.Max(bestW, bestH);
            if (side != bestSide) return side < bestSide;
            return w < bestW;
        }

        static int NextPowerOfTwo(int v)
        {
            int p = 1;
            while (p < v) p <<= 1;
            return p;
        }
    }

    /// <summary>A sheet layout: size and one rectangle per slot, in pixels with the origin at the <b>top-left</b> (rows top first).</summary>
    public sealed class RoSheetLayout
    {
        public int Width, Height;
        /// <summary>Rects[slot]: indexed frames first, then true colour (plan 4.2); a zero-size frame has a zero rect.</summary>
        public RectInt[] Rects;
    }
}
