using System;
using GRF.Image;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Formats
{
    /// <summary>
    /// Adapter over the GRF image classes (plan 4.4, D7, D16; SPEC-0002 FR-8). Decoding is GRF's (BMP, TGA) or Unity's
    /// (PNG and JPG, through <see cref="IRoPngCodec"/>, which GRF cannot decode inside Unity); this class only converts
    /// their result into an <see cref="RoImage"/>. It never throws for bad input: every failure is <c>false</c> with a reason.
    /// </summary>
    public static class RoImageReader
    {
        public const string Bmp = ".bmp", Png = ".png", Jpg = ".jpg", Jpeg = ".jpeg", Tga = ".tga";

        /// <summary>True for <c>.bmp</c>, <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c> and <c>.tga</c> (with the dot, any case).</summary>
        public static bool IsSupportedExtension(string extension) =>
            string.Equals(extension, Bmp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, Png, StringComparison.OrdinalIgnoreCase)
            || IsJpg(extension)
            || string.Equals(extension, Tga, StringComparison.OrdinalIgnoreCase);

        static bool IsJpg(string extension) =>
            string.Equals(extension, Jpg, StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, Jpeg, StringComparison.OrdinalIgnoreCase);

        public static bool TryRead(RoPath path, byte[] bytes, IRoPngCodec png, out RoImage image, out string reason)
        {
            image = null;
            string extension = path.Extension;

            if (!IsSupportedExtension(extension))
                return Fail($"unsupported texture format '{(extension.Length > 0 ? extension : "(none)")}'", out reason);
            if (bytes == null || bytes.Length == 0)
                return Fail("the file is empty", out reason);

            if (extension == Png) return TryReadWithCodec(bytes, png, "PNG", out image, out reason);
            if (IsJpg(extension)) return TryReadWithCodec(bytes, png, "JPG", out image, out reason);
            return TryReadWithGrf(bytes, extension, out image, out reason);
        }

        /// <summary>PNG and JPG through Unity's decoder (plan 4.4). A JPG has no alpha, so it is keyed like a BMP.</summary>
        static bool TryReadWithCodec(byte[] bytes, IRoPngCodec png, string format, out RoImage image, out string reason)
        {
            image = null;
            if (png == null) return Fail("no PNG/JPG codec was given", out reason);
            try
            {
                if (!png.TryDecode(bytes, out image) || image == null) return Fail($"not a valid {format}", out reason);
            }
            catch (Exception e)
            {
                image = null;
                return Fail($"{format} decode failed: {e.Message}", out reason);
            }

            image.SourceHadAlpha = format == "PNG" && HasNonOpaquePixel(image.Rgba32);
            reason = null;
            return true;
        }

        static bool TryReadWithGrf(byte[] bytes, string extension, out RoImage image, out string reason)
        {
            image = null;
            GrfImage grf;
            bool hadAlpha;
            try
            {
                grf = ImageProvider.GetImage(bytes, extension, true);
                if (grf == null) return Fail("GRF does not read this format", out reason);

                switch (grf.GrfImageType)
                {
                    case GrfImageType.Bgra32:
                        hadAlpha = true;
                        break;
                    case GrfImageType.Indexed8:
                    case GrfImageType.Bgr24:
                    case GrfImageType.Bgr32:
                        // Through Bgr24: a direct Indexed8 -> Bgra32 conversion makes palette index 0 transparent (D16).
                        hadAlpha = false;
                        if (grf.GrfImageType != GrfImageType.Bgr24) grf.Convert(GrfImageType.Bgr24, null);
                        grf.Convert(GrfImageType.Bgra32, null);
                        break;
                    default:
                        return Fail($"GRF returned an image of type {grf.GrfImageType}", out reason);
                }
            }
            catch (Exception e)
            {
                // GRF throws GrfException for a bad header and plain index/range exceptions for truncated data (plan 4.4).
                return Fail($"{e.GetType().Name}: {e.Message}", out reason);
            }

            int width = grf.Width, height = grf.Height;
            byte[] bgra = grf.Pixels;
            if (width <= 0 || height <= 0 || bgra == null || bgra.Length != width * height * 4)
                return Fail($"GRF returned {width} x {height} with {bgra?.Length ?? 0} pixel bytes", out reason);

            image = new RoImage(width, height, hadAlpha);
            byte[] rgba = image.Rgba32;
            for (int i = 0; i < rgba.Length; i += 4)
            {
                rgba[i] = bgra[i + 2];
                rgba[i + 1] = bgra[i + 1];
                rgba[i + 2] = bgra[i];
                rgba[i + 3] = bgra[i + 3];
            }
            reason = null;
            return true;
        }

        static bool HasNonOpaquePixel(byte[] rgba)
        {
            for (int i = 3; i < rgba.Length; i += 4)
                if (rgba[i] != 255) return true;
            return false;
        }

        static bool Fail(string why, out string reason)
        {
            reason = why;
            return false;
        }
    }
}
