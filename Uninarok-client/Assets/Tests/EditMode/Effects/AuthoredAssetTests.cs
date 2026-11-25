using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// The authored effect assets under Assets/Data/Effects/Authored (SPEC-0012 FR-4, FR-5, FR-7, plan 4.7).
    /// "Every lead value has a Doubtful line" is asserted as: for each authored field the plan lists as a lead value
    /// (plan 4.7), a Doubtful line starts with that field name and names its source ("(lead"); every line names a source.
    /// </summary>
    public class AuthoredAssetTests
    {
        const string Root = "Assets/Data/Effects/Authored/";
        const string Warp = "texture/effect/_authored/warp_portal/";
        const string TextureOutRoot = "Assets/Art/Imported/effect/";

        static readonly string[] WarpKeys =
        {
            Warp + "sparkle.quad", Warp + "ring_outer.cylinder", Warp + "ring_inner.cylinder",
        };
        const string TrapKey = "model/외부소품/트랩01.rsm";

        static string[] AllKeys => WarpKeys.Concat(new[] { TrapKey }).ToArray();

        static EffectAsset Load(string key)
        {
            var a = AssetDatabase.LoadAssetAtPath<EffectAsset>(Root + key + ".asset");
            Assert.IsNotNull(a, "authored asset missing for key " + key);
            return a;
        }

        static bool HasLine(EffectAsset a, string field) =>
            a.Doubtful != null && a.Doubtful.Any(l => l != null && l.StartsWith(field + ":"));

        static bool CoversZeroToOne(AnimationCurve c) =>
            c != null && c.length >= 2 && Mathf.Approximately(c.keys[0].time, 0f) &&
            Mathf.Approximately(c.keys[c.length - 1].time, 1f);

        [Test]
        public void WarpKeys_ArePresent_AndPathEqualsKey()
        {
            foreach (var key in AllKeys)
                Assert.AreEqual(key, Load(key).Path, key);
        }

        [Test]
        public void EveryEntry_HasValidKind_AndPositiveLength()
        {
            foreach (var key in WarpKeys)
            {
                var a = Load(key);
                Assert.IsTrue(a.Kind == EffectKind.Quad || a.Kind == EffectKind.Cylinder, key + " kind " + a.Kind);
                Assert.Greater(a.Length, 0f, key);
            }
            var trap = Load(TrapKey);
            Assert.AreEqual(EffectKind.Model, trap.Kind);
            Assert.Greater(trap.Length, 0f);
            Assert.IsNotNull(trap.Model, "trap Model prefab");
            Assert.AreEqual(EffectKind.Quad, Load(Warp + "sparkle.quad").Kind);
            Assert.AreEqual(EffectKind.Cylinder, Load(Warp + "ring_outer.cylinder").Kind);
            Assert.AreEqual(EffectKind.Cylinder, Load(Warp + "ring_inner.cylinder").Kind);
        }

        [Test]
        public void WarpTextures_IndexValid_NameMatchesConvertedTexture()
        {
            foreach (var key in WarpKeys)
            {
                var a = Load(key);
                Assert.IsNotNull(a.Authored, key);
                int i = a.Authored.Texture;
                Assert.IsNotNull(a.TextureNames, key);
                Assert.IsNotNull(a.Textures, key);
                Assert.AreEqual(a.TextureNames.Length, a.Textures.Length, key + " parallel arrays");
                Assert.That(i, Is.InRange(0, a.Textures.Length - 1), key + " texture index");
                Assert.IsNotNull(a.Textures[i], key + " texture slot");
                string expected = TextureOutRoot + a.TextureNames[i] + ".png";
                Assert.AreEqual(expected, AssetDatabase.GetAssetPath(a.Textures[i]), key);
            }
        }

        [Test]
        public void WarpTextures_RingsOnRingBlue_SparkleOnPok1Tga()
        {
            Assert.AreEqual(new[] { "texture/effect/pok1.tga" }, Load(Warp + "sparkle.quad").TextureNames);
            Assert.AreEqual(new[] { "texture/effect/ring_blue.tga" }, Load(Warp + "ring_outer.cylinder").TextureNames);
            Assert.AreEqual(new[] { "texture/effect/ring_blue.tga" }, Load(Warp + "ring_inner.cylinder").TextureNames);
            Assert.IsFalse(Load(Warp + "sparkle.quad").TextureNames.Any(n => n.EndsWith("pok1.bmp")));
        }

        [Test]
        public void WarpBlendCodes_AreInTheEffectBlendTable()
        {
            foreach (var key in WarpKeys)
            {
                var d = Load(key).Authored;
                EffectBlend.ToUnity(d.SrcBlend, out bool srcKnown);
                EffectBlend.ToUnity(d.DstBlend, out bool dstKnown);
                Assert.IsTrue(srcKnown, key + " SrcBlend " + d.SrcBlend);
                Assert.IsTrue(dstKnown, key + " DstBlend " + d.DstBlend);
                Assert.AreEqual(5, d.SrcBlend, key); // SrcAlpha
                Assert.AreEqual(2, d.DstBlend, key); // One
            }
        }

        [Test]
        public void Cylinders_HaveAtLeast3Sides_AndCurvesCoverZeroToOne()
        {
            foreach (var key in new[] { Warp + "ring_outer.cylinder", Warp + "ring_inner.cylinder" })
            {
                var d = Load(key).Authored;
                Assert.GreaterOrEqual(d.Sides, 3, key);
                Assert.IsTrue(CoversZeroToOne(d.Alpha), key + " Alpha");
                Assert.IsTrue(CoversZeroToOne(d.RadiusScale), key + " RadiusScale");
                Assert.IsTrue(CoversZeroToOne(d.HeightScale), key + " HeightScale");
            }
        }

        [Test]
        public void Sparkle_AlphaFloorIsTenthAndOthersNeutral_FR7()
        {
            Assert.AreEqual(0.1f, Load(Warp + "sparkle.quad").Authored.AlphaFloor, 1e-6f);
            Assert.AreEqual(0f, Load(Warp + "ring_outer.cylinder").Authored.AlphaFloor);
        }

        [Test]
        public void Sparkle_CurvesCoverZeroToOne()
        {
            var d = Load(Warp + "sparkle.quad").Authored;
            Assert.IsTrue(CoversZeroToOne(d.Alpha), "Alpha");
            Assert.IsTrue(CoversZeroToOne(d.Motion), "Motion");
        }

        [Test]
        public void Doubtful_EveryLeadValueHasALine_AndEveryLineNamesItsSource()
        {
            var quadFields = new[] { "SizePixels", "Tint", "MaxAlpha", "Alpha", "StartMin/StartMax", "EndMin/EndMax",
                "Motion", "Length", "SrcBlend/DstBlend" };
            var cylFields = new[] { "BottomRadius", "TopRadius", "Height", "Sides", "RepeatU", "Tint", "MaxAlpha", "Alpha",
                "RadiusScale", "HeightScale", "Length", "SrcBlend/DstBlend" };

            foreach (var f in quadFields)
                Assert.IsTrue(HasLine(Load(Warp + "sparkle.quad"), f), "sparkle.quad Doubtful lacks " + f);
            foreach (var key in new[] { Warp + "ring_outer.cylinder", Warp + "ring_inner.cylinder" })
                foreach (var f in cylFields)
                    Assert.IsTrue(HasLine(Load(key), f), key + " Doubtful lacks " + f);
            Assert.IsTrue(HasLine(Load(TrapKey), "Length"), "trap Doubtful lacks Length");

            foreach (var key in AllKeys)
            {
                var a = Load(key);
                Assert.IsNotNull(a.Doubtful, key);
                Assert.IsNotEmpty(a.Doubtful, key);
                foreach (var line in a.Doubtful)
                    StringAssert.Contains("lead", line, key + " line without a source: " + line);
            }
        }
    }
}
