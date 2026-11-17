using System;
using UnityEngine;

namespace Uninarok.RoImport.Core.Imaging
{
    /// <summary>
    /// A decoded image in our own form (plan 4.4): RGBA, 8 bits per channel, <b>top row first</b>. The format adapters fill
    /// it from GRF (or from <see cref="IRoPngCodec"/> for PNG); everything after them works on this type only.
    /// </summary>
    public sealed class RoImage
    {
        public int Width;
        public int Height;

        /// <summary>Width * Height * 4 bytes, R G B A per pixel, top row first.</summary>
        public byte[] Rgba32;

        /// <summary>True when the source file had a real alpha channel (32-bit TGA, PNG with alpha); false for BMP etc.</summary>
        public bool SourceHadAlpha;

        public RoImage() { }

        public RoImage(int width, int height, bool sourceHadAlpha = false)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), $"{width} x {height}");
            Width = width;
            Height = height;
            Rgba32 = new byte[width * height * 4];
            SourceHadAlpha = sourceHadAlpha;
        }

        public Color32 GetPixel(int x, int y)
        {
            int i = Index(x, y);
            return new Color32(Rgba32[i], Rgba32[i + 1], Rgba32[i + 2], Rgba32[i + 3]);
        }

        public void SetPixel(int x, int y, Color32 color)
        {
            int i = Index(x, y);
            Rgba32[i] = color.r;
            Rgba32[i + 1] = color.g;
            Rgba32[i + 2] = color.b;
            Rgba32[i + 3] = color.a;
        }

        int Index(int x, int y)
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside {Width} x {Height}");
            return (y * Width + x) * 4;
        }
    }
}
