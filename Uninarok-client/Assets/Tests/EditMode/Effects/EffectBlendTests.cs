using NUnit.Framework;
using UnityEngine.Rendering;

namespace Uninarok.Effects.Tests
{
    /// <summary>Pins the Direct3D blend code table (SPEC-0012 FR-3, plan 4.5).</summary>
    public class EffectBlendTests
    {
        static readonly BlendMode[] Table =
        {
            BlendMode.Zero, BlendMode.One, BlendMode.SrcColor, BlendMode.OneMinusSrcColor,
            BlendMode.SrcAlpha, BlendMode.OneMinusSrcAlpha, BlendMode.DstAlpha, BlendMode.OneMinusDstAlpha,
            BlendMode.DstColor, BlendMode.OneMinusDstColor, BlendMode.SrcAlphaSaturate,
        };

        [Test]
        public void Table_IsPinnedForCodes1To11()
        {
            for (int code = 1; code <= 11; code++)
            {
                Assert.AreEqual(Table[code - 1], EffectBlend.ToUnity((byte)code, out bool known), "code " + code);
                Assert.IsTrue(known, "code " + code);
                Assert.AreEqual(Table[code - 1], EffectBlend.ToUnity((byte)code), "code " + code);
            }
        }

        [TestCase((byte)0)]
        [TestCase((byte)12)]
        [TestCase((byte)255)]
        public void UnknownCode_FallsBackAndReportsUnknown(byte code)
        {
            Assert.AreEqual(BlendMode.SrcAlpha, EffectBlend.ToUnity(code, out bool known));
            Assert.IsFalse(known);
            Assert.AreEqual(BlendMode.SrcAlpha, EffectBlend.ToUnity(code));
        }

        [TestCase((byte)12, (byte)12)]
        [TestCase((byte)5, (byte)0)]
        [TestCase((byte)255, (byte)7)]
        public void UnknownPair_BecomesSrcAlphaOneMinusSrcAlpha(byte s, byte d)
        {
            EffectBlend.ToUnityPair(s, d, out var src, out var dst, out bool known);
            Assert.IsFalse(known);
            Assert.AreEqual(BlendMode.SrcAlpha, src);
            Assert.AreEqual(BlendMode.OneMinusSrcAlpha, dst);
        }

        [Test]
        public void MostCommonStrPair_5_7_IsSrcAlphaDstAlpha()
        {
            EffectBlend.ToUnityPair(5, 7, out var src, out var dst, out bool known);
            Assert.IsTrue(known);
            Assert.AreEqual(BlendMode.SrcAlpha, src);
            Assert.AreEqual(BlendMode.DstAlpha, dst);
        }
    }
}
