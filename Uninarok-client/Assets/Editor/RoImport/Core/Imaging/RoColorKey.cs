using System;
using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Imaging
{
    /// <summary>How a texture uses transparency (plan 4.4); it picks the material surface (plan 4.6).</summary>
    public enum RoTextureAlpha
    {
        /// <summary>Fully opaque.</summary>
        None,
        /// <summary>Only fully transparent and fully opaque pixels: keyed magenta, or binary real alpha (D17). Cutout.</summary>
        Keyed,
        /// <summary>Real alpha with values between 1 and 254. Blended.</summary>
        Partial
    }

    /// <summary>
    /// The Ragnarok transparency color (magenta) and edge bleeding (plan 4.4, SPEC-0002 FR-8).
    /// <see cref="Apply"/> turns the key color into alpha 0; <see cref="BleedTransparentColor"/> then replaces the RGB of
    /// transparent pixels with their visible neighbors' color, so filtering and mip maps never blend magenta into an edge.
    /// </summary>
    public static class RoColorKey
    {
        public const byte KeyR = 255, KeyG = 0, KeyB = 255, Tolerance = 10;   // carried over from the old importer

        /// <summary>
        /// For an image without real alpha, gives alpha 0 to every pixel within <see cref="Tolerance"/> of the key on each
        /// channel. An image with real alpha is only classified, never keyed.
        /// </summary>
        public static RoTextureAlpha Apply(RoImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            byte[] p = image.Rgba32;

            if (!image.SourceHadAlpha)
            {
                bool keyed = false;
                for (int i = 0; i < p.Length; i += 4)
                {
                    if (!IsKey(p[i], p[i + 1], p[i + 2])) continue;
                    p[i + 3] = 0;
                    keyed = true;
                }
                return keyed ? RoTextureAlpha.Keyed : RoTextureAlpha.None;
            }

            bool transparent = false;
            for (int i = 3; i < p.Length; i += 4)
            {
                byte a = p[i];
                if (a != 0 && a != 255) return RoTextureAlpha.Partial;
                if (a == 0) transparent = true;
            }
            return transparent ? RoTextureAlpha.Keyed : RoTextureAlpha.None;
        }

        /// <summary>
        /// Replaces the RGB of every alpha-0 pixel with the rounded average of its already-filled 8-neighbors, one ring per
        /// pass, until every transparent pixel is filled (at most width + height passes). Alpha and visible pixels are not
        /// changed. A fully transparent image is cleared to black.
        /// </summary>
        public static void BleedTransparentColor(RoImage image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            int w = image.Width, h = image.Height;
            byte[] p = image.Rgba32;

            var filled = new bool[w * h];
            var pending = new List<int>();
            for (int i = 0; i < filled.Length; i++)
            {
                filled[i] = p[i * 4 + 3] != 0;
                if (!filled[i]) pending.Add(i);
            }
            if (pending.Count == 0) return;

            var done = new List<int>();
            for (int pass = 0; pass < w + h && pending.Count > 0; pass++)
            {
                done.Clear();
                foreach (int i in pending)
                {
                    int x = i % w, y = i / w, n = 0, r = 0, g = 0, b = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (!filled[j]) continue;
                        r += p[j * 4];
                        g += p[j * 4 + 1];
                        b += p[j * 4 + 2];
                        n++;
                    }
                    if (n == 0) continue;
                    // Written now, marked filled after the pass: each ring is computed only from the rings before it.
                    p[i * 4] = (byte)((r + n / 2) / n);
                    p[i * 4 + 1] = (byte)((g + n / 2) / n);
                    p[i * 4 + 2] = (byte)((b + n / 2) / n);
                    done.Add(i);
                }
                if (done.Count == 0) break;
                foreach (int i in done) filled[i] = true;
                pending.RemoveAll(i => filled[i]);
            }

            // Nothing visible to bleed from (or past the cap): no key color may remain.
            foreach (int i in pending)
                p[i * 4] = p[i * 4 + 1] = p[i * 4 + 2] = 0;
        }

        static bool IsKey(byte r, byte g, byte b) =>
            Math.Abs(r - KeyR) <= Tolerance && Math.Abs(g - KeyG) <= Tolerance && Math.Abs(b - KeyB) <= Tolerance;
    }
}
