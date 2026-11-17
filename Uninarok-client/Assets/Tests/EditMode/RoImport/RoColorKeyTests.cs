using System.IO;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-8: the transparency color becomes real transparency, with no colored fringe when filtered.
    // Plan 4.4 (color key, bleed), D17. TASKS T3.3.
    [Ignore("pending T3.4")]
    public class RoColorKeyTests
    {
        static readonly Color32 Magenta = new Color32(255, 0, 255, 255);
        static readonly Color32 Green = new Color32(0, 255, 0, 255);

        static RoImage Image(int width, int height, bool sourceHadAlpha, params Color32[] topRowFirst)
        {
            var image = new RoImage(width, height, sourceHadAlpha);
            for (int i = 0; i < topRowFirst.Length; i++) image.SetPixel(i % width, i / width, topRowFirst[i]);
            return image;
        }

        static RoImage Fixture(string relative)
        {
            string disk = Path.Combine(RoFolderSourceTests.FixtureRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(RoImageReader.TryRead(RoPath.Parse(relative), File.ReadAllBytes(disk), new TestPngCodec(), out var image, out var reason), reason);
            return image;
        }

        static bool NearKey(Color32 c) =>
            Mathf.Abs(c.r - RoColorKey.KeyR) <= RoColorKey.Tolerance
            && Mathf.Abs(c.g - RoColorKey.KeyG) <= RoColorKey.Tolerance
            && Mathf.Abs(c.b - RoColorKey.KeyB) <= RoColorKey.Tolerance;

        [Test]
        public void KeyConstants_AreMagentaWithTolerance10()
        {
            Assert.AreEqual((255, 0, 255, 10), (RoColorKey.KeyR, RoColorKey.KeyG, RoColorKey.KeyB, RoColorKey.Tolerance));
        }

        // Each case: a pixel and whether it is within the tolerance of the key on every channel.
        [TestCase(255, 0, 255, true, TestName = "Key: exact magenta is keyed")]
        [TestCase(250, 5, 250, true, TestName = "Key: near-key is keyed")]
        [TestCase(245, 10, 245, true, TestName = "Key: exactly at the tolerance is keyed")]
        [TestCase(244, 0, 255, false, TestName = "Key: red one past the tolerance is not keyed")]
        [TestCase(255, 11, 255, false, TestName = "Key: green one past the tolerance is not keyed")]
        [TestCase(255, 0, 244, false, TestName = "Key: blue one past the tolerance is not keyed")]
        [TestCase(0, 255, 0, false, TestName = "Key: green is not keyed")]
        public void KeyColorWithinTolerance_GetsAlphaZero(int r, int g, int b, bool keyed)
        {
            var image = Image(2, 1, false, new Color32((byte)r, (byte)g, (byte)b, 255), Green);

            var alpha = RoColorKey.Apply(image);

            Assert.AreEqual(keyed ? 0 : 255, image.GetPixel(0, 0).a);
            Assert.AreEqual(255, image.GetPixel(1, 0).a, "other pixels keep alpha 255");
            Assert.AreEqual(keyed ? RoTextureAlpha.Keyed : RoTextureAlpha.None, alpha);
        }

        [Test]
        public void RealAlphaImage_IsNotKeyed_AndIsPartialWithMidAlpha()
        {
            var image = Image(2, 1, true, Magenta, new Color32(0, 255, 0, 128));

            var alpha = RoColorKey.Apply(image);

            Assert.AreEqual(RoTextureAlpha.Partial, alpha);
            Assert.AreEqual(Magenta, image.GetPixel(0, 0), "an opaque magenta in a real-alpha image is a real color");
            Assert.AreEqual(128, image.GetPixel(1, 0).a);
        }

        [Test]
        public void RealAlphaImage_WithOnlyZeroAnd255_IsKeyed()
        {
            var image = Image(2, 1, true, new Color32(10, 20, 30, 0), Green);

            Assert.AreEqual(RoTextureAlpha.Keyed, RoColorKey.Apply(image), "binary alpha is a cutout (D17)");
        }

        [Test]
        public void RealAlphaImage_FullyOpaque_IsNone()
        {
            Assert.AreEqual(RoTextureAlpha.None, RoColorKey.Apply(Image(2, 1, true, Magenta, Green)));
        }

        [Test]
        public void Fixtures_AreClassifiedAsInPlan()
        {
            var bmp24 = Fixture("texture/a.bmp");
            Assert.AreEqual(RoTextureAlpha.Keyed, RoColorKey.Apply(bmp24));
            Assert.AreEqual(new byte[] { 0, 255, 0, 255, 0, 255 },
                new[] { bmp24.GetPixel(0, 0).a, bmp24.GetPixel(1, 0).a, bmp24.GetPixel(2, 0).a,
                        bmp24.GetPixel(0, 1).a, bmp24.GetPixel(1, 1).a, bmp24.GetPixel(2, 1).a },
                "magenta and near-key pixels are keyed");

            var paletted = Fixture("texture/KEY8.BMP");
            Assert.AreEqual(RoTextureAlpha.Keyed, RoColorKey.Apply(paletted));
            Assert.AreEqual(0, paletted.GetPixel(0, 0).a);
            Assert.AreEqual(255, paletted.GetPixel(1, 1).a);

            var tga = Fixture("texture/alpha32.tga");
            Assert.AreEqual(RoTextureAlpha.Partial, RoColorKey.Apply(tga));
            Assert.AreEqual(255, tga.GetPixel(1, 1).a, "the TGA's opaque magenta is not keyed");

            Assert.AreEqual(RoTextureAlpha.Partial, RoColorKey.Apply(Fixture("texture/rgba.png")));
        }

        [Test]
        public void AfterBleed_NoTransparentPixelKeepsTheKeyColor()
        {
            var image = Fixture("texture/KEY8.BMP");
            RoColorKey.Apply(image);

            RoColorKey.BleedTransparentColor(image);

            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                var c = image.GetPixel(x, y);
                if (c.a == 0) Assert.IsFalse(NearKey(c), $"pixel ({x}, {y}) is still {c}");
            }
        }

        [Test]
        public void Bleed_FillsFarPixelsOverSeveralPasses()
        {
            var image = Image(8, 1, false, Green, Magenta, Magenta, Magenta, Magenta, Magenta, Magenta, Magenta);
            RoColorKey.Apply(image);

            RoColorKey.BleedTransparentColor(image);

            for (int x = 1; x < 8; x++)
                Assert.AreEqual(new Color32(0, 255, 0, 0), image.GetPixel(x, 0), $"pixel {x}");
        }

        [Test]
        public void Bleed_AveragesTheNearestNeighbors()
        {
            // A transparent center between red (left) and blue (right) takes their average.
            var image = Image(3, 1, false, new Color32(200, 0, 0, 255), Magenta, new Color32(0, 0, 100, 255));
            RoColorKey.Apply(image);

            RoColorKey.BleedTransparentColor(image);

            Assert.AreEqual(new Color32(100, 0, 50, 0), image.GetPixel(1, 0));
        }

        [Test]
        public void Bleed_ChangesNeitherAlphaNorVisiblePixels()
        {
            var image = Fixture("texture/a.bmp");
            RoColorKey.Apply(image);
            var before = (byte[])image.Rgba32.Clone();

            RoColorKey.BleedTransparentColor(image);

            for (int i = 0; i < before.Length; i += 4)
            {
                Assert.AreEqual(before[i + 3], image.Rgba32[i + 3], "alpha unchanged");
                if (before[i + 3] != 0)
                    Assert.AreEqual(new[] { before[i], before[i + 1], before[i + 2] },
                        new[] { image.Rgba32[i], image.Rgba32[i + 1], image.Rgba32[i + 2] }, "visible pixel unchanged");
            }
        }

        [Test]
        public void Bleed_OfAFullyTransparentImage_ClearsTheKeyColor()
        {
            var image = Image(2, 2, false, Magenta, Magenta, Magenta, Magenta);
            RoColorKey.Apply(image);

            RoColorKey.BleedTransparentColor(image);

            for (int i = 0; i < 4; i++) Assert.AreEqual(new Color32(0, 0, 0, 0), image.GetPixel(i % 2, i / 2));
        }

        // What a bilinear filter (and each mip level) does: a weighted average of neighboring texels' RGB and alpha.
        // Before bleeding, the keyed texel still holds magenta, so the halfway sample is pink. After, it holds green.
        [Test]
        public void HalfwayBilinearSampleOfGreenOnMagenta_HasNoMagentaAfterBleed()
        {
            var image = Image(2, 1, false, Green, Magenta);
            RoColorKey.Apply(image);
            var unbled = Color32.Lerp(image.GetPixel(0, 0), image.GetPixel(1, 0), 0.5f);
            Assume.That(unbled.r, Is.GreaterThan(100), "without bleeding the sample is pink (the fringe FR-8 forbids)");

            RoColorKey.BleedTransparentColor(image);
            var sample = Color32.Lerp(image.GetPixel(0, 0), image.GetPixel(1, 0), 0.5f);

            Assert.AreEqual(0, sample.r, "no red from the key");
            Assert.AreEqual(0, sample.b, "no blue from the key");
            Assert.AreEqual(255, sample.g);
        }
    }
}
