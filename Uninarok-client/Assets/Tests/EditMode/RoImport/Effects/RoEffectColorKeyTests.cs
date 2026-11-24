using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Effects;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// SPEC-0012 FR-2, FR-10 (PLAN-0012 4.3, 6): the colour key of effect textures. The owner's key decision (T0.2b) is not
    /// recorded yet, so every rule value here is test-local: the cases pin the mechanism (which pixels a key set keys, which
    /// key a texture gets), not the real defaults. Images are synthetic and in memory only.
    /// </summary>
    public class RoEffectColorKeyTests
    {
        static readonly Color32 Pink = new Color32(255, 0, 255, 255);
        static readonly Color32 Black = new Color32(0, 0, 0, 255);
        static readonly Color32 Green = new Color32(0, 200, 0, 255);

        static RoImage Image(params Color32[] pixels)
        {
            var image = new RoImage(pixels.Length, 1, sourceHadAlpha: false);
            for (int i = 0; i < pixels.Length; i++) image.SetPixel(i, 0, pixels[i]);
            return image;
        }

        static RoKeySet Keys(RoEffectKey key, byte pink = 10, byte black = 0) => new RoKeySet(key, pink, black);

        static RoPath P(string s) => RoPath.Parse(s);

        [Test]
        public void Pink_IsKeyed_OthersKept()
        {
            var image = Image(Pink, Black, Green);
            var result = RoColorKey.Apply(image, Keys(RoEffectKey.Pink));
            Assert.AreEqual(RoTextureAlpha.Keyed, result);
            CollectionAssert.AreEqual(new byte[] { 0, 255, 255 }, new[] { image.GetPixel(0, 0).a, image.GetPixel(1, 0).a, image.GetPixel(2, 0).a });
        }

        [Test]
        public void Black_IsKeyed_OthersKept()
        {
            var image = Image(Black, Pink, Green);
            var result = RoColorKey.Apply(image, Keys(RoEffectKey.Black));
            Assert.AreEqual(RoTextureAlpha.Keyed, result);
            CollectionAssert.AreEqual(new byte[] { 0, 255, 255 }, new[] { image.GetPixel(0, 0).a, image.GetPixel(1, 0).a, image.GetPixel(2, 0).a });
        }

        [Test]
        public void PinkAndBlack_KeysBoth()
        {
            var image = Image(Pink, Black, Green);
            Assert.AreEqual(RoTextureAlpha.Keyed, RoColorKey.Apply(image, Keys(RoEffectKey.PinkAndBlack)));
            CollectionAssert.AreEqual(new byte[] { 0, 0, 255 }, new[] { image.GetPixel(0, 0).a, image.GetPixel(1, 0).a, image.GetPixel(2, 0).a });
        }

        [Test]
        public void None_KeepsEveryPixel()
        {
            var image = Image(Pink, Black, Green);
            var before = (byte[])image.Rgba32.Clone();
            Assert.AreEqual(RoTextureAlpha.None, RoColorKey.Apply(image, Keys(RoEffectKey.None)));
            CollectionAssert.AreEqual(before, image.Rgba32);
        }

        [Test]
        public void KeySet_WithoutMatch_ReportsNoneAndKeepsPixels()
        {
            var image = Image(Green, Green);
            var before = (byte[])image.Rgba32.Clone();
            Assert.AreEqual(RoTextureAlpha.None, RoColorKey.Apply(image, Keys(RoEffectKey.PinkAndBlack)));
            CollectionAssert.AreEqual(before, image.Rgba32);
        }

        // Each case: the pixel's red/green/blue and whether the key set (pink tolerance 10, black tolerance 5) keys it.
        [TestCase(245, 10, 245, RoEffectKey.Pink, true, TestName = "Tolerance: pink exactly at the pink tolerance is keyed")]
        [TestCase(244, 0, 255, RoEffectKey.Pink, false, TestName = "Tolerance: pink red one past the tolerance is not keyed")]
        [TestCase(255, 11, 255, RoEffectKey.Pink, false, TestName = "Tolerance: pink green one past the tolerance is not keyed")]
        [TestCase(5, 5, 5, RoEffectKey.Black, true, TestName = "Tolerance: black exactly at the black tolerance is keyed")]
        [TestCase(6, 0, 0, RoEffectKey.Black, false, TestName = "Tolerance: black one past the tolerance is not keyed")]
        [TestCase(5, 5, 5, RoEffectKey.Pink, false, TestName = "Tolerance: near-black is not keyed by a pink key")]
        public void Tolerance_PerChannel(int r, int g, int b, RoEffectKey key, bool keyed)
        {
            var image = Image(new Color32((byte)r, (byte)g, (byte)b, 255));
            RoColorKey.Apply(image, Keys(key, pink: 10, black: 5));
            Assert.AreEqual(keyed ? 0 : 255, image.GetPixel(0, 0).a);
        }

        [Test]
        public void ZeroBlackTolerance_KeysOnlyExactBlack()
        {
            var image = Image(Black, new Color32(1, 0, 0, 255));
            RoColorKey.Apply(image, Keys(RoEffectKey.Black, black: 0));
            CollectionAssert.AreEqual(new byte[] { 0, 255 }, new[] { image.GetPixel(0, 0).a, image.GetPixel(1, 0).a });
        }

        [Test]
        public void For_UsesDefaultBySourceKind()
        {
            var rules = new RoEffectKeyRules { DefaultOpaque = RoEffectKey.Pink, DefaultWithAlpha = RoEffectKey.Black };
            Assert.AreEqual(RoEffectKey.Pink, rules.For(P("texture/effect/a.bmp"), sourceHadAlpha: false));
            Assert.AreEqual(RoEffectKey.Black, rules.For(P("texture/effect/a.tga"), sourceHadAlpha: true));
        }

        [Test]
        public void Override_BeatsBothDefaults()
        {
            var rules = new RoEffectKeyRules
            {
                DefaultOpaque = RoEffectKey.Pink,
                DefaultWithAlpha = RoEffectKey.None,
                Overrides = new[] { (P("texture/effect/special.bmp"), RoEffectKey.Black) },
            };
            Assert.AreEqual(RoEffectKey.Black, rules.For(P("texture/effect/special.bmp"), sourceHadAlpha: false));
            Assert.AreEqual(RoEffectKey.Black, rules.For(P("texture/effect/special.bmp"), sourceHadAlpha: true));
            Assert.AreEqual(RoEffectKey.Pink, rules.For(P("texture/effect/other.bmp"), sourceHadAlpha: false));
        }

        [Test]
        public void Override_ToNone_BeatsAKeyingDefault()
        {
            var rules = new RoEffectKeyRules
            {
                DefaultOpaque = RoEffectKey.PinkAndBlack,
                Overrides = new[] { (P("texture/effect/keep.bmp"), RoEffectKey.None) },
            };
            Assert.AreEqual(RoEffectKey.None, rules.For(P("texture/effect/keep.bmp"), sourceHadAlpha: false));
        }

        [Test]
        public void KeySetFor_CarriesKeyAndTolerances_AndAppliesToImage()
        {
            var rules = new RoEffectKeyRules
            {
                DefaultOpaque = RoEffectKey.Black,
                PinkTolerance = 7,
                BlackTolerance = 3,
                Overrides = new[] { (P("texture/effect/pink.bmp"), RoEffectKey.Pink) },
            };
            var keys = rules.KeySetFor(P("texture/effect/pink.bmp"), sourceHadAlpha: false);
            Assert.AreEqual(RoEffectKey.Pink, keys.Key);
            Assert.AreEqual(7, keys.PinkTolerance);
            Assert.AreEqual(3, keys.BlackTolerance);

            var image = Image(Pink, Black);
            RoColorKey.Apply(image, keys);
            CollectionAssert.AreEqual(new byte[] { 0, 255 }, new[] { image.GetPixel(0, 0).a, image.GetPixel(1, 0).a });
        }

        // PLAN-0012 4.3: the applied key is stored (userData key=...) and a rules change re-converts only the textures whose
        // stored key differs from For(). Pinned here: For is deterministic, and its value survives a round trip through its
        // name, which is what a stored key is compared with. The 'alpha=...;key=...' string itself is T3.10.
        [Test]
        public void For_KeyNameRoundTrips_SoAStoredKeyCanBeCompared()
        {
            var rules = new RoEffectKeyRules { DefaultOpaque = RoEffectKey.PinkAndBlack };
            var path = P("texture/effect/a.bmp");
            string stored = rules.For(path, false).ToString();
            Assert.AreEqual(rules.For(path, false), Enum.Parse(typeof(RoEffectKey), stored));

            rules.DefaultOpaque = RoEffectKey.Black;
            Assert.AreNotEqual(stored, rules.For(path, false).ToString(), "a changed rule must differ from the stored key");
        }

        // The JSON mechanism, with a temp file written here. The real Rules/effect-textures.json is owner-gated (T0.2b, T3.8).
        [Test]
        [Ignore("pending T3.8")]
        public void Rules_ReadFromJson()
        {
            string temp = Path.Combine(Path.GetTempPath(), "ro-effect-keys-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(temp,
                    "{\"DefaultOpaque\":\"Pink\",\"DefaultWithAlpha\":\"None\",\"PinkTolerance\":12,\"BlackTolerance\":4," +
                    "\"Overrides\":[{\"Path\":\"texture/effect/dark.bmp\",\"Key\":\"Black\"}]}");
                var rules = RoEffectKeyRules.FromJson(File.ReadAllText(temp));

                Assert.AreEqual(RoEffectKey.Pink, rules.DefaultOpaque);
                Assert.AreEqual(RoEffectKey.None, rules.DefaultWithAlpha);
                Assert.AreEqual(12, rules.PinkTolerance);
                Assert.AreEqual(4, rules.BlackTolerance);
                Assert.AreEqual(RoEffectKey.Black, rules.For(P("texture/effect/dark.bmp"), false));
                Assert.AreEqual(RoEffectKey.Pink, rules.For(P("texture/effect/light.bmp"), false));
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
        }
    }
}
