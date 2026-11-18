using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-8: BMP, PNG and TGA convert; anything else is reported. Plan 4.4 and D16 (T0.4 findings).
    // Expected pixels are the ones listed in Fixtures~/README.md (top row first). TASKS T3.1.
    public class RoImageReaderTests
    {
        static readonly Color32 Magenta = new Color32(255, 0, 255, 255);
        static readonly Color32 NearKey = new Color32(250, 5, 250, 255);
        static readonly Color32 Red = new Color32(255, 0, 0, 255);
        static readonly Color32 Green = new Color32(0, 255, 0, 255);
        static readonly Color32 Blue = new Color32(0, 0, 255, 255);
        static readonly Color32 White = new Color32(255, 255, 255, 255);
        static readonly Color32 Black = new Color32(0, 0, 0, 255);
        static readonly Color32 Yellow = new Color32(255, 255, 0, 255);

        static readonly string[] GoodFixtures =
        {
            "texture/a.bmp", "texture/KEY8.BMP", "texture/alpha32.tga", "texture/rgba.png",
            "texture/나무잡초꽃/leaf.bmp", "texture/유저인터페이스/button.bmp",
        };

        static byte[] Fixture(string relative) =>
            File.ReadAllBytes(Path.Combine(RoFolderSourceTests.FixtureRoot, relative.Replace('/', Path.DirectorySeparatorChar)));

        static RoImage Read(string relative)
        {
            bool ok = RoImageReader.TryRead(RoPath.Parse(relative), Fixture(relative), new TestPngCodec(), out var image, out string reason);
            Assert.IsTrue(ok, $"{relative}: {reason}");
            Assert.IsNull(reason);
            Assert.AreEqual(image.Width * image.Height * 4, image.Rgba32.Length);
            return image;
        }

        static void AssertPixels(RoImage image, int width, int height, params Color32[] topRowFirst)
        {
            Assert.AreEqual(width, image.Width, "width");
            Assert.AreEqual(height, image.Height, "height");
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                Assert.AreEqual(topRowFirst[y * width + x], image.GetPixel(x, y), $"pixel ({x}, {y})");
        }

        [Test]
        public void Bmp24_DecodesTopRowFirst_WithRowPadding()
        {
            var image = Read("texture/a.bmp");

            AssertPixels(image, 3, 2, Magenta, Red, Magenta, Green, NearKey, Blue);
            Assert.IsFalse(image.SourceHadAlpha);
        }

        [Test]
        public void Bmp8Paletted_Decodes_AndPaletteIndexZeroStaysOpaque()
        {
            var image = Read("texture/KEY8.BMP");

            AssertPixels(image, 4, 4,
                Magenta, Magenta, Magenta, Magenta,
                Magenta, White, Black, Magenta,
                Magenta, Yellow, White, Magenta,
                Magenta, Magenta, Magenta, Magenta);
            Assert.IsFalse(image.SourceHadAlpha, "transparency is decided by the color key, not by GRF's sprite rule (D16)");
        }

        [Test]
        public void Tga32_DecodesTopRowFirst_WithItsAlpha()
        {
            var image = Read("texture/alpha32.tga");

            AssertPixels(image, 2, 2,
                new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 128),
                new Color32(0, 0, 255, 0), new Color32(255, 0, 255, 255));
            Assert.IsTrue(image.SourceHadAlpha);
        }

        [Test]
        public void PngWithAlpha_DecodesThroughTheCodec()
        {
            var codec = new TestPngCodec();

            Assert.IsTrue(RoImageReader.TryRead(RoPath.Parse("texture/rgba.png"), Fixture("texture/rgba.png"), codec, out var image, out _));

            AssertPixels(image, 2, 2,
                new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 64),
                new Color32(255, 0, 255, 255), new Color32(10, 20, 30, 200));
            Assert.IsTrue(image.SourceHadAlpha);
            Assert.AreEqual(1, codec.Decodes, "PNG goes through IRoPngCodec, not GRF (D16)");
        }

        [Test]
        public void OpaquePng_HasNoSourceAlpha()
        {
            var codec = new TestPngCodec();
            var opaque = new RoImage(2, 1);
            opaque.SetPixel(0, 0, Magenta);
            opaque.SetPixel(1, 0, Green);

            Assert.IsTrue(RoImageReader.TryRead(RoPath.Parse("texture/opaque.png"), codec.Encode(opaque), codec, out var image, out _));

            AssertPixels(image, 2, 1, Magenta, Green);
            Assert.IsFalse(image.SourceHadAlpha, "a PNG with alpha 255 everywhere is keyed like a BMP");
        }

        [Test]
        public void BmpsInKoreanAndUiFolders_Decode()
        {
            AssertPixels(Read("texture/나무잡초꽃/leaf.bmp"), 2, 2, Green, Magenta, Magenta, Green);
            AssertPixels(Read("texture/유저인터페이스/button.bmp"), 2, 2, White, Magenta, Magenta, Black);
        }

        [Test]
        public void CorruptBmp_ReturnsFalseWithAReason()
        {
            bool ok = true;
            string reason = null;
            Assert.DoesNotThrow(() => ok = RoImageReader.TryRead(RoPath.Parse("texture/corrupt.bmp"), Fixture("texture/corrupt.bmp"),
                new TestPngCodec(), out _, out reason));

            Assert.IsFalse(ok);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }

        public static IEnumerable<string> Good() => GoodFixtures;

        // Cut to half: GRF does not detect files missing only their last bytes (plan 4.4), so the check uses half length.
        [TestCaseSource(nameof(Good))]
        public void HalfLengthCopy_ReturnsFalseWithAReason_AndNeverThrows(string relative)
        {
            byte[] bytes = Fixture(relative);
            byte[] half = bytes.Take(bytes.Length / 2).ToArray();
            bool ok = true;
            string reason = null;

            Assert.DoesNotThrow(() => ok = RoImageReader.TryRead(RoPath.Parse(relative), half, new TestPngCodec(), out _, out reason));

            Assert.IsFalse(ok, relative);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }

        [TestCase("texture/a.bmp")]
        [TestCase("texture/alpha32.tga")]
        [TestCase("texture/rgba.png")]
        public void EmptyOrNullBytes_ReturnFalse(string relative)
        {
            Assert.IsFalse(RoImageReader.TryRead(RoPath.Parse(relative), new byte[0], new TestPngCodec(), out _, out var r1));
            Assert.IsFalse(RoImageReader.TryRead(RoPath.Parse(relative), null, new TestPngCodec(), out _, out var r2));
            Assert.That(r1, Is.Not.Empty);
            Assert.That(r2, Is.Not.Empty);
        }

        [TestCase(".bmp", true)]
        [TestCase(".png", true)]
        [TestCase(".tga", true)]
        [TestCase(".BMP", true)]
        [TestCase(".jpg", true)]
        [TestCase(".jpeg", true)]
        [TestCase(".JPG", true)]
        [TestCase(".dds", false)]
        [TestCase(".gif", false)]
        [TestCase("bmp", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsSupportedExtension_IsBmpPngTgaOnly(string extension, bool expected)
        {
            Assert.AreEqual(expected, RoImageReader.IsSupportedExtension(extension));
        }

        // ---- JPG (spec FR-8 since 2026-11-18, plan 4.4): decoded by IRoPngCodec, no alpha, keyed like a BMP ----

        /// <summary>16 x 16: left half magenta, right half green, as a quality-100 JPG from Unity's encoder.</summary>
        internal static byte[] MagentaGreenJpg()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGB24, false);
            try
            {
                var pixels = new Color32[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = i % 16 < 8 ? Magenta : Green;
                texture.SetPixels32(pixels);
                return texture.EncodeToJPG(100);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Jpg_DecodesThroughTheCodec_WithoutSourceAlpha()
        {
            var codec = new TestPngCodec();

            Assert.IsTrue(RoImageReader.TryRead(RoPath.Parse("texture/basepic/a.jpg"), MagentaGreenJpg(), codec, out var image, out var reason), reason);

            Assert.AreEqual(16, image.Width);
            Assert.AreEqual(16, image.Height);
            Assert.IsFalse(image.SourceHadAlpha, "a JPG has no alpha channel");
            Assert.AreEqual(1, codec.Decodes, "JPG goes through IRoPngCodec, not GRF");
            var green = image.GetPixel(13, 8);
            Assert.That(green.g, Is.GreaterThan(240));
            Assert.That(green.r, Is.LessThan(15));
        }

        [Test]
        public void Jpg_MagentaIsKeyedDespiteCompressionNoise()
        {
            Assert.IsTrue(RoImageReader.TryRead(RoPath.Parse("texture/a.jpeg"), MagentaGreenJpg(), new TestPngCodec(), out var image, out _));

            Assert.AreEqual(RoTextureAlpha.Keyed, RoColorKey.Apply(image));
            Assert.AreEqual(0, image.GetPixel(2, 8).a, "magenta, a little off after compression, is still within the tolerance");
            Assert.AreEqual(255, image.GetPixel(13, 8).a);
        }

        [Test]
        public void HalfLengthJpg_ReturnsFalseWithAReason()
        {
            byte[] jpg = MagentaGreenJpg();
            bool ok = true;
            string reason = null;

            Assert.DoesNotThrow(() => ok = RoImageReader.TryRead(RoPath.Parse("texture/a.jpg"), jpg.Take(jpg.Length / 2).ToArray(), new TestPngCodec(), out _, out reason));

            Assert.IsFalse(ok);
            Assert.That(reason, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void UnsupportedExtension_ReturnsFalseWithAReason()
        {
            Assert.IsFalse(RoImageReader.TryRead(RoPath.Parse("texture/unsupported.dds"), Fixture("texture/unsupported.dds"),
                new TestPngCodec(), out var image, out string reason));

            Assert.IsNull(image);
            StringAssert.Contains(".dds", reason);
        }

        [Test]
        public void PngWithoutACodec_ReturnsFalseWithAReason()
        {
            Assert.IsFalse(RoImageReader.TryRead(RoPath.Parse("texture/rgba.png"), Fixture("texture/rgba.png"), null, out _, out string reason));
            Assert.That(reason, Is.Not.Empty);
        }
    }
}
