using NUnit.Framework;

namespace Uninarok.Effects.Tests
{
    /// <summary>Pins EffectPath normalisation (SPEC-0012 FR-5, FR-10, plan 4.4 / 6). Enabled by T1.7.</summary>
    public class EffectPathTests
    {
        const string Pending = "pending T1.7";

        [Test, Ignore(Pending)]
        public void Prefixes_AreTheFourPlanEntries()
        {
            CollectionAssert.AreEqual(new[] { "", "texture/effect/", "texture/", "sprite/" }, EffectPath.Prefixes);
        }

        // Normalisation table (FR-5): every form reaches one key.
        [Test, Ignore(Pending)]
        [TestCase("texture/effect/firewall.str", "texture/effect/firewall.str")]
        [TestCase("TEXTURE/EFFECT/FIREWALL.STR", "texture/effect/firewall.str")]
        [TestCase("texture\\effect\\firewall.str", "texture/effect/firewall.str")]
        [TestCase("data\\texture\\effect\\firewall.str", "texture/effect/firewall.str")]
        [TestCase("DATA/texture/effect/firewall.str", "texture/effect/firewall.str")]
        [TestCase("/texture/effect/firewall.str", "texture/effect/firewall.str")]
        [TestCase("\\\\texture/effect/firewall.str", "texture/effect/firewall.str")]
        [TestCase("texture//effect\\\\firewall.str", "texture/effect/firewall.str")]
        [TestCase("  texture/effect/firewall.str  ", "texture/effect/firewall.str")]
        [TestCase("effect/firewall.str", "effect/firewall.str")]
        [TestCase("FIREWALL.STR", "firewall.str")]
        [TestCase("sprite/이팩트/fireball.act", "sprite/이팩트/fireball.act")]
        [TestCase("data\\sprite\\이팩트\\Fireball.ACT", "sprite/이팩트/fireball.act")]
        [TestCase("texture/effect/_authored/warp_portal/ring_outer.cylinder", "texture/effect/_authored/warp_portal/ring_outer.cylinder")]
        public void Normalize_GivesOneKey(string raw, string expected)
        {
            Assert.IsTrue(EffectPath.TryNormalize(raw, out string key), raw);
            Assert.AreEqual(expected, key);
        }

        [Test, Ignore(Pending)]
        public void Normalize_DecomposedKorean_EqualsComposed()
        {
            // "이팩트" typed with decomposed jamo (NFD) must reach the NFC key.
            string composed = "sprite/이팩트/fireball.act";
            string decomposed = composed.Normalize(System.Text.NormalizationForm.FormD);
            Assert.AreNotEqual(composed, decomposed);
            Assert.IsTrue(EffectPath.TryNormalize(decomposed, out string key));
            Assert.AreEqual(composed, key);
        }

        [Test, Ignore(Pending)]
        public void Normalize_AllFormsOfOneFile_ReachOneKey()
        {
            string[] forms =
            {
                "texture/effect/firewall.str", "TEXTURE\\EFFECT\\FIREWALL.STR", "data\\texture\\effect\\firewall.str",
                "\\data/texture//effect/firewall.str", " /Data/Texture/Effect/Firewall.str ",
            };
            foreach (string f in forms)
            {
                Assert.IsTrue(EffectPath.TryNormalize(f, out string key), f);
                Assert.AreEqual("texture/effect/firewall.str", key, f);
            }
        }

        [Test, Ignore(Pending)]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("/")]
        [TestCase("\\")]
        [TestCase(".")]
        [TestCase("..")]
        [TestCase("texture/../effect/firewall.str")]
        [TestCase("texture/./effect/firewall.str")]
        [TestCase("../texture/effect/firewall.str")]
        [TestCase("C:\\data\\texture\\effect\\firewall.str")]
        [TestCase("c:/texture/effect/firewall.str")]
        public void Normalize_Invalid_ReturnsFalse(string raw)
        {
            Assert.IsFalse(EffectPath.TryNormalize(raw, out string key));
            Assert.IsNull(key);
        }
    }
}
