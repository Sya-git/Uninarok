using UnityEngine;
using Uninarok.RoImport.Core.Imaging;

namespace Uninarok.RoImport.Editor.Writing
{
    /// <summary>
    /// PNG through Unity's <c>ImageConversion</c> (plan 4.4, D7, D16): encodes every output texture, and decodes PNG sources,
    /// which GRF cannot decode inside Unity. Unity textures hold rows bottom first; <see cref="RoImage"/> holds them top
    /// first, so both directions flip rows. Main thread only (it creates a temporary <see cref="Texture2D"/>).
    /// </summary>
    public sealed class RoPngCodec : IRoPngCodec
    {
        public bool TryDecode(byte[] png, out RoImage image)
        {
            image = null;
            if (png == null || png.Length == 0) return false;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(png, false)) return false;
                int w = texture.width, h = texture.height;
                Color32[] pixels = texture.GetPixels32();
                image = new RoImage(w, h);
                byte[] rgba = image.Rgba32;
                for (int y = 0; y < h; y++)
                {
                    int src = (h - 1 - y) * w, dst = y * w * 4;
                    for (int x = 0; x < w; x++, dst += 4)
                    {
                        Color32 c = pixels[src + x];
                        rgba[dst] = c.r;
                        rgba[dst + 1] = c.g;
                        rgba[dst + 2] = c.b;
                        rgba[dst + 3] = c.a;
                    }
                }
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public byte[] Encode(RoImage image)
        {
            int w = image.Width, h = image.Height;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[w * h];
                byte[] rgba = image.Rgba32;
                for (int y = 0; y < h; y++)
                {
                    int dst = (h - 1 - y) * w, src = y * w * 4;
                    for (int x = 0; x < w; x++, src += 4)
                        pixels[dst + x] = new Color32(rgba[src], rgba[src + 1], rgba[src + 2], rgba[src + 3]);
                }
                texture.SetPixels32(pixels);
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
