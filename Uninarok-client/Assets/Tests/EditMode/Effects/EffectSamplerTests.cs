using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// Pins the STR sampling rule of plan 4.2 (SPEC-0012 FR-3, FR-10; D17) with the owner decisions of 2026-11-25
    /// (str-reference.md section 8): O2 length = FrameCount / Fps, O3 anitype 0 holds the base aniframe, O4 anitypes
    /// 2..4 use the base key's delay, O5 anitypes 5 and 6 hold the base aniframe.
    /// Angle: the sampler takes EffectSettings (plan D23); the tests use its defaults (1024 units per turn).
    /// </summary>
    public class EffectSamplerTests
    {
        const float Tol = 1e-4f;

        static EffectAsset.KeyData Base(int frame, Vector2 pos, float aniFrame = 0f, float angle = 0f,
            Vector4? color = null, byte src = 5, byte dst = 6)
            => new EffectAsset.KeyData
            {
                Frame = frame, Type = 0, SrcBlend = src, DstBlend = dst, Position = pos, AniFrame = aniFrame,
                Angle = angle, Color = color ?? new Vector4(255, 255, 255, 255),
                Uv0 = new Vector2(0, 0), Uv1 = new Vector2(1, 1),
            };

        static EffectAsset.KeyData Delta(int frame, Vector2 pos, byte aniType = 0, float aniFrame = 0f,
            float delay = 0f, float angle = 0f, Vector4 color = default)
            => new EffectAsset.KeyData
            {
                Frame = frame, Type = 1, AniType = aniType, Position = pos, AniFrame = aniFrame, Delay = delay,
                Angle = angle, Color = color,
            };

        /// <summary>One-layer STR asset with the given keys (in order) and textureCount textures.</summary>
        static EffectAsset Make(int textureCount, params EffectAsset.KeyData[] keys)
        {
            var a = ScriptableObject.CreateInstance<EffectAsset>();
            a.Kind = EffectKind.Str;
            a.Fps = 60f;
            a.FrameCount = 100;
            a.Keys = keys;
            a.Layers = new[] { new EffectAsset.LayerData { FirstKey = 0, KeyCount = keys.Length, FirstTexture = 0, TextureCount = textureCount } };
            a.TextureNames = new string[textureCount];
            a.Textures = new Texture2D[textureCount];
            return a;
        }

        EffectSettings settings;

        [SetUp] public void SetUp() => settings = ScriptableObject.CreateInstance<EffectSettings>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);

        bool Sample(EffectAsset a, float frame, out QuadSample q)
        {
            int cursor = 0;
            return Sample(a, frame, ref cursor, out q);
        }

        bool Sample(EffectAsset a, float frame, ref int cursor, out QuadSample q)
            => EffectSampler.Sample(a, settings, 0, frame, ref cursor, out q);

        [Test]
        public void BaseOnly_IsHeldUntilNextBaseKey()
        {
            var a = Make(1,
                Base(0, new Vector2(1, 2), src: 5, dst: 6),
                Base(10, new Vector2(7, 8), src: 2, dst: 3));
            Assert.IsTrue(Sample(a, 4.5f, out var q));
            Assert.AreEqual(1f, q.Position.x, Tol);
            Assert.AreEqual(2f, q.Position.y, Tol);
            Assert.AreEqual(5, q.Src);
            Assert.AreEqual(6, q.Dst);
            Assert.IsTrue(Sample(a, 10f, out q));
            Assert.AreEqual(7f, q.Position.x, Tol);
            Assert.AreEqual(2, q.Src);
            Assert.AreEqual(3, q.Dst);
        }

        [Test]
        public void BasePlusDelta_Morphs_PositionAngleColour()
        {
            var a = Make(1,
                Base(0, new Vector2(10, 20), angle: 0f, color: new Vector4(100, 100, 100, 100)),
                Delta(0, new Vector2(2, -1), angle: 4f, color: new Vector4(10, 0, -10, 20)),
                Base(50, new Vector2(0, 0)));
            Assert.IsTrue(Sample(a, 3f, out var q));
            Assert.AreEqual(16f, q.Position.x, Tol);
            Assert.AreEqual(17f, q.Position.y, Tol);
            Assert.AreEqual(12f * 360f / 1024f, q.AngleDegrees, Tol);
            Assert.AreEqual((100f + 30f) / 255f, q.Color.r, Tol);
            Assert.AreEqual(100f / 255f, q.Color.g, Tol);
            Assert.AreEqual((100f - 30f) / 255f, q.Color.b, Tol);
            Assert.AreEqual((100f + 60f) / 255f, q.Color.a, Tol);
            Assert.IsTrue(Sample(a, 3.5f, out q));   // fractional frame: delta x (f - base.Frame)
            Assert.AreEqual(17f, q.Position.x, Tol);
        }

        [Test]
        public void DeltaNotAdjacent_IsIgnoredAndBaseHeld()
        {
            // The delta sits at another frame than the base key: it is not the base's delta key, base is held.
            var a = Make(1,
                Base(0, new Vector2(10, 20)),
                Base(5, new Vector2(30, 40)),
                Delta(0, new Vector2(2, 2)));
            Assert.IsTrue(Sample(a, 3f, out var q));
            Assert.AreEqual(10f, q.Position.x, Tol);
            Assert.AreEqual(20f, q.Position.y, Tol);
        }

        [Test]
        public void Hidden_BeforeFirstKey_AndAfterLastKey()
        {
            var a = Make(1, Base(5, new Vector2(1, 1)), Base(10, new Vector2(2, 2)));
            Assert.IsFalse(Sample(a, 4.9f, out _), "before the first key");
            Assert.IsTrue(Sample(a, 5f, out _), "on the first key");
            Assert.IsTrue(Sample(a, 10f, out var q), "on the last key's own frame");
            Assert.AreEqual(2f, q.Position.x, Tol);
            Assert.IsFalse(Sample(a, 11f, out _), "after the last key");
        }

        // Anitype cases use base aniframe 0 and the base key's delay (O4); n = 4 textures. d = f - base.Frame.

        [Test]
        public void AniType0_HoldsTheBaseAniFrame_OfTheBaseKey()
        {
            // O3: base aniframe 3, the delta's own aniframe and delay are ignored.
            var a = Make(4, Base(0, Vector2.zero, aniFrame: 3f), Delta(0, Vector2.zero, aniType: 0, aniFrame: 1f, delay: 1f));
            Assert.IsTrue(Sample(a, 5f, out var q));
            Assert.AreEqual(3, q.Texture);
            var zero = Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 0, aniFrame: 3f, delay: 1f));
            Assert.IsTrue(Sample(zero, 5f, out q));
            Assert.AreEqual(0, q.Texture, "base aniframe 0 stays texture 0");
        }

        [Test]
        public void AniTypes2To4_UseTheBaseKeysDelay_NotTheDeltas()
        {
            // O4: base delay 1, delta delay 3 (and +inf); the texture follows the base delay.
            foreach (float deltaDelay in new[] { 3f, float.PositiveInfinity })
            {
                var two = Make(4, BaseWithDelay(1f), Delta(0, Vector2.zero, aniType: 2, delay: deltaDelay));
                Assert.IsTrue(Sample(two, 2f, out var q));
                Assert.AreEqual(2, q.Texture, "anitype 2, base delay 1: min(0 + 1 * 2, 3)");
                var three = Make(4, BaseWithDelay(1f), Delta(0, Vector2.zero, aniType: 3, delay: deltaDelay));
                Assert.IsTrue(Sample(three, 6f, out q));
                Assert.AreEqual(2, q.Texture, "anitype 3: 6 mod 4");
                var four = Make(4, BaseWithDelay(1f), Delta(0, Vector2.zero, aniType: 4, delay: deltaDelay));
                Assert.IsTrue(Sample(four, 1f, out q));
                Assert.AreEqual(3, q.Texture, "anitype 4: -1 wraps to 3");
            }
            var zeroBase = Make(4, BaseWithDelay(0f), Delta(0, Vector2.zero, aniType: 3, delay: 1f));
            Assert.IsTrue(Sample(zeroBase, 6f, out var z));
            Assert.AreEqual(0, z.Texture, "base delay 0 holds the base aniframe whatever the delta says");
        }

        static EffectAsset.KeyData BaseWithDelay(float delay)
        {
            var k = Base(0, Vector2.zero);
            k.Delay = delay;
            return k;
        }

        [Test]
        public void AniTypes5And6_HoldTheBaseAniFrame_AndAreReported()
        {
            foreach (byte type in new byte[] { 5, 6 })
            {
                var a = Make(4, Base(0, Vector2.zero, aniFrame: 2f), Delta(0, Vector2.zero, aniType: type, aniFrame: 1f, delay: 1f));
                Assert.IsTrue(Sample(a, 7f, out var q));
                Assert.AreEqual(2, q.Texture, "anitype " + type + " holds the base aniframe");
                Assert.IsTrue(EffectSampler.HasUnsupportedAniType(a));
            }
            Assert.IsFalse(EffectSampler.HasUnsupportedAniType(Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 4))));
        }

        [Test]
        public void Length_IsFrameCountOverFps_AndLastFrameIsFrameCountMinusOne()
        {
            // O2: 100 frames at 60 fps.
            var a = Make(1, Base(0, Vector2.zero), Base(99, Vector2.one));
            Assert.AreEqual(100f / 60f, EffectSampler.Length(a), Tol);
            Assert.IsTrue(Sample(a, 99f, out var q), "frame count - 1 is drawn");
            Assert.AreEqual(1f, q.Position.x, Tol);
        }

        [Test]
        public void AniType1_BasePlusDeltaAniFrameTimesD()
        {
            var a = Make(8, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 1, aniFrame: 1f));
            Assert.IsTrue(Sample(a, 5f, out var q));
            Assert.AreEqual(5, q.Texture);
        }

        [Test]
        public void AniType2_AdvancesThenClampsToLast()
        {
            var a = Make(4, BaseWithDelay(1f), Delta(0, Vector2.zero, aniType: 2));
            Assert.IsTrue(Sample(a, 2f, out var q));
            Assert.AreEqual(2, q.Texture);
            Assert.IsTrue(Sample(a, 9f, out q));
            Assert.AreEqual(3, q.Texture, "min(.., n - 1)");
        }

        [Test]
        public void AniType3_WrapsModuloCount()
        {
            var a = Make(4, BaseWithDelay(1f), Delta(0, Vector2.zero, aniType: 3));
            Assert.IsTrue(Sample(a, 6f, out var q));
            Assert.AreEqual(2, q.Texture);
        }

        [Test]
        public void AniType4_RunsBackwardsWrappedPositive()
        {
            var a = Make(4, BaseWithDelay(1f), Delta(0, Vector2.zero, aniType: 4));
            Assert.IsTrue(Sample(a, 1f, out var q));
            Assert.AreEqual(3, q.Texture, "0 - 1 wraps to n - 1");
            Assert.IsTrue(Sample(a, 5f, out q));
            Assert.AreEqual(3, q.Texture, "-5 mod 4, positive");
        }

        [Test]
        public void AngleUnits_ConvertToDegreesByTurn()
        {
            var a = Make(1, Base(0, Vector2.zero, angle: 256f));   // 1024 units per turn (lead default)
            Assert.IsTrue(Sample(a, 0f, out var q));
            Assert.AreEqual(90f, q.AngleDegrees, Tol);
        }

        [Test]
        public void Colour_IsDividedBy255()
        {
            var a = Make(1, Base(0, Vector2.zero, color: new Vector4(255, 51, 0, 127.5f)));
            Assert.IsTrue(Sample(a, 0f, out var q));
            Assert.AreEqual(1f, q.Color.r, Tol);
            Assert.AreEqual(0.2f, q.Color.g, Tol);
            Assert.AreEqual(0f, q.Color.b, Tol);
            Assert.AreEqual(0.5f, q.Color.a, Tol);
        }

        [Test]
        public void Seek_EqualsContinuousPlay()
        {
            var a = Make(4,
                Base(0, new Vector2(1, 1), color: new Vector4(200, 200, 200, 255)),
                Delta(0, new Vector2(1, 0.5f), aniType: 3, delay: 0.5f, angle: 2f),
                Base(12, new Vector2(40, 40)),
                Base(20, new Vector2(5, 5)),
                Delta(20, new Vector2(-1, 1), aniType: 1, aniFrame: 0.5f),
                Base(30, new Vector2(9, 9)));
            int played = 0;
            for (float f = 0f; f <= 32f; f += 0.25f)
            {
                bool shownPlay = Sample(a, f, ref played, out var play);
                int fresh = 0;                       // a seek: cursor reset
                bool shownSeek = Sample(a, f, ref fresh, out var seek);
                Assert.AreEqual(shownSeek, shownPlay, "visible at " + f);
                if (!shownPlay) continue;
                Assert.AreEqual(seek.Position, play.Position, "position at " + f);
                Assert.AreEqual(seek.AngleDegrees, play.AngleDegrees, Tol, "angle at " + f);
                Assert.AreEqual(seek.Color, play.Color, "colour at " + f);
                Assert.AreEqual(seek.Texture, play.Texture, "texture at " + f);
            }
        }

        [Test]
        public void Seek_BackwardsWithStaleCursor_EqualsFresh()
        {
            var a = Make(1, Base(0, new Vector2(1, 0)), Base(10, new Vector2(2, 0)), Base(20, new Vector2(3, 0)));
            int cursor = 0;
            Assert.IsTrue(Sample(a, 20f, ref cursor, out var q));
            Assert.AreEqual(3f, q.Position.x, Tol);
            Assert.IsTrue(Sample(a, 4f, ref cursor, out q), "stale cursor ahead of the frame");
            Assert.AreEqual(1f, q.Position.x, Tol);
            cursor = 1000;                           // out-of-range hint is clamped, never indexes out of range
            Assert.IsTrue(Sample(a, 12f, ref cursor, out q));
            Assert.AreEqual(2f, q.Position.x, Tol);
            cursor = -7;
            Assert.IsTrue(Sample(a, 12f, ref cursor, out q));
            Assert.AreEqual(2f, q.Position.x, Tol);
        }

        // FR-3 robustness: malformed assets give "hidden" or a safe texture index, never an exception.
        [Test]
        public void Malformed_LayerRangeOrIndex_IsHidden()
        {
            var a = Make(1, Base(0, Vector2.zero));
            int c = 0;
            Assert.IsFalse(EffectSampler.Sample(a, settings, 1, 0f, ref c, out _), "layer out of range");
            Assert.IsFalse(EffectSampler.Sample(a, settings, -1, 0f, ref c, out _), "negative layer");
            Assert.IsFalse(EffectSampler.Sample(a, settings, 0, float.NaN, ref c, out _), "NaN frame");
            a.Layers[0].KeyCount = 5;                // past the end of Keys
            Assert.IsFalse(EffectSampler.Sample(a, settings, 0, 0f, ref c, out _), "key range past the array");
            a.Layers[0].KeyCount = 1; a.Layers[0].FirstKey = int.MaxValue;
            Assert.IsFalse(EffectSampler.Sample(a, settings, 0, 0f, ref c, out _), "overflowing first key");
        }

        [Test]
        public void NoTextures_GivesMinusOne_AndHugeAniFrameIsClamped()
        {
            var a = Make(0, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 3, delay: 1f));
            Assert.IsTrue(Sample(a, 5f, out var q));
            Assert.AreEqual(-1, q.Texture);
            var b = Make(4, Base(0, Vector2.zero, aniFrame: 1e30f));
            Assert.IsTrue(Sample(b, 0f, out q));
            Assert.AreEqual(3, q.Texture, "held aniframe beyond the layer clamps to the last texture");
            var c = Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 2, delay: float.PositiveInfinity));
            Assert.IsTrue(Sample(c, 0f, out q), "+inf delay at d = 0 (inf x 0) does not give NaN");
            Assert.AreEqual(0, q.Texture);
        }

        // FR-9: sampling allocates nothing (tasks.md T2.2 "Done when").
        [Test]
        public void TenThousandSamples_AllocateZeroBytes()
        {
            var a = Make(4,
                Base(0, new Vector2(1, 1)),
                Delta(0, new Vector2(1, 0.5f), aniType: 3, delay: 0.5f, angle: 2f),
                Base(12, new Vector2(40, 40)),
                Base(20, new Vector2(5, 5)),
                Delta(20, new Vector2(-1, 1), aniType: 4, delay: 1f),
                Base(30, new Vector2(9, 9)));
            int cursor = 0;
            float sum = 0f;
            for (int i = 0; i < 100; i++)            // warm-up (JIT)
                if (EffectSampler.Sample(a, settings, 0, i * 0.33f, ref cursor, out var w)) sum += w.Position.x;
            cursor = 0;
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                if ((i & 1023) == 0) cursor = 0;     // include seeks
                if (EffectSampler.Sample(a, settings, 0, (i % 1300) * 0.025f, ref cursor, out var q)) sum += q.Position.x;
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "bytes allocated by 10,000 samples");
            Assert.IsFalse(float.IsNaN(sum));
        }
    }
}
