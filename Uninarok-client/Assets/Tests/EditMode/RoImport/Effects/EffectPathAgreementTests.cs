using NUnit.Framework;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Tests.Effects
{
    /// <summary>
    /// The runtime EffectPath and the importer's RoPath must give one key for one file, so the catalog index built at
    /// import time is found at run time (SPEC-0012 FR-5, FR-10, plan 4.4 D21).
    /// </summary>
    public class EffectPathAgreementTests
    {
        [TestCase("texture/effect/firewall.str")]
        [TestCase("TEXTURE/EFFECT/FIREWALL.STR")]
        [TestCase("texture\\effect\\firewall.str")]
        [TestCase("data\\texture\\effect\\firewall.str")]
        [TestCase("DATA/texture/effect/firewall.str")]
        [TestCase("/texture/effect/firewall.str")]
        [TestCase("\\\\texture/effect/firewall.str")]
        [TestCase("texture//effect\\\\firewall.str")]
        [TestCase("  texture/effect/firewall.str  ")]
        [TestCase("effect/firewall.str")]
        [TestCase("FIREWALL.STR")]
        [TestCase("sprite/이팩트/fireball.act")]
        [TestCase("data\\sprite\\이팩트\\Fireball.ACT")]
        [TestCase("texture/effect/_authored/warp_portal/ring_outer.cylinder")]
        [TestCase("data/texture/effect/a/b/c/d/e/deep.tga")]
        public void Normalize_EqualsRoPathCanonical(string raw)
        {
            Assert.IsTrue(RoPath.TryParse(raw, out RoPath ro), raw);
            Assert.IsTrue(EffectPath.TryNormalize(raw, out string key), raw);
            Assert.AreEqual(ro.Canonical, key, raw);
        }

        [Test]
        public void Normalize_DecomposedKorean_EqualsRoPathCanonical()
        {
            string raw = "sprite/이팩트/fireball.act".Normalize(System.Text.NormalizationForm.FormD);
            Assert.IsTrue(RoPath.TryParse(raw, out RoPath ro));
            Assert.IsTrue(EffectPath.TryNormalize(raw, out string key));
            Assert.AreEqual(ro.Canonical, key);
        }

        [TestCase("")]
        [TestCase("/")]
        [TestCase("data/")]
        [TestCase("texture/../a.str")]
        [TestCase("C:\\data\\a.str")]
        public void Invalid_BothReject(string raw)
        {
            Assert.IsFalse(RoPath.TryParse(raw, out _), raw);
            Assert.IsFalse(EffectPath.TryNormalize(raw, out _), raw);
        }
    }
}
