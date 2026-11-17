using UnityEngine;
using Uninarok.RoImport.Core.Imaging;

namespace Uninarok.RoImport.Tests
{
    /// <summary>
    /// Test stand-in for the editor's <c>RoPngCodec</c> (added by T4.2) so Core tests can decode PNG: the same Unity
    /// <c>ImageConversion</c> calls, with rows flipped from Unity's bottom-first order to <see cref="RoImage"/>'s top-first.
    /// </summary>
    sealed class TestPngCodec : IRoPngCodec
    {
        public int Decodes;

        public bool TryDecode(byte[] png, out RoImage image)
        {
            Decodes++;
            image = null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (png == null || png.Length == 0 || !texture.LoadImage(png, false)) return false;
                var pixels = texture.GetPixels32();
                image = new RoImage(texture.width, texture.height);
                for (int y = 0; y < texture.height; y++)
                for (int x = 0; x < texture.width; x++)
                    image.SetPixel(x, y, pixels[(texture.height - 1 - y) * texture.width + x]);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        public byte[] Encode(RoImage image)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[image.Width * image.Height];
                for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    pixels[(image.Height - 1 - y) * image.Width + x] = image.GetPixel(x, y);
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
