using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// Pins the STR sampling rule of plan 4.2 (SPEC-0012 FR-3, FR-10; lead D17). Owner questions of
    /// str-reference.md section 8 are not pinned: O2 (last frame / length), O3 (anitype 0 with a non-zero base
    /// aniframe), O4 (whose delay anitypes 2..4 use), O5 (anitypes 5 and 6). Cases below avoid them.
    /// Angle: Sample has no settings parameter (plan 4.2), so the tests assume the lead default of 1024 units per
    /// turn (EffectSettings.AngleUnitsPerTurn); how the sampler gets the setting is open (reported with T2.1).
    /// </summary>
    public class EffectSamplerTests
    {
        const string Pending = "pending T2.2";
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

        static bool Sample(EffectAsset a, float frame, out QuadSample q)
        {
            int cursor = 0;
            return Sample(a, frame, ref cursor, out q);
        }

        static bool Sample(EffectAsset a, float frame, ref int cursor, out QuadSample q)
            => EffectSampler.Sample(a, 0, frame, ref cursor, out q);

        [Test, Ignore(Pending)]
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

        [Test, Ignore(Pending)]
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

        [Test, Ignore(Pending)]
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

        [Test, Ignore(Pending)]
        public void Hidden_BeforeFirstKey_AndAfterLastKey()
        {
            var a = Make(1, Base(5, new Vector2(1, 1)), Base(10, new Vector2(2, 2)));
            Assert.IsFalse(Sample(a, 4.9f, out _), "before the first key");
            Assert.IsTrue(Sample(a, 5f, out _), "on the first key");
            Assert.IsTrue(Sample(a, 10f, out var q), "on the last key's own frame");
            Assert.AreEqual(2f, q.Position.x, Tol);
            Assert.IsFalse(Sample(a, 11f, out _), "after the last key");
        }

        // Anitype cases use base aniframe 0 and the delta key's delay (O3, O4 undecided); n = 4 textures.
        // d = f - base.Frame.

        [Test, Ignore(Pending)]
        public void AniType0_ShowsTextureZero()
        {
            var a = Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 0, aniFrame: 3f, delay: 1f));
            Assert.IsTrue(Sample(a, 5f, out var q));
            Assert.AreEqual(0, q.Texture);
        }

        [Test, Ignore(Pending)]
        public void AniType1_BasePlusDeltaAniFrameTimesD()
        {
            var a = Make(8, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 1, aniFrame: 1f));
            Assert.IsTrue(Sample(a, 5f, out var q));
            Assert.AreEqual(5, q.Texture);
        }

        [Test, Ignore(Pending)]
        public void AniType2_AdvancesThenClampsToLast()
        {
            var a = Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 2, delay: 1f));
            Assert.IsTrue(Sample(a, 2f, out var q));
            Assert.AreEqual(2, q.Texture);
            Assert.IsTrue(Sample(a, 9f, out q));
            Assert.AreEqual(3, q.Texture, "min(.., n - 1)");
        }

        [Test, Ignore(Pending)]
        public void AniType3_WrapsModuloCount()
        {
            var a = Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 3, delay: 1f));
            Assert.IsTrue(Sample(a, 6f, out var q));
            Assert.AreEqual(2, q.Texture);
        }

        [Test, Ignore(Pending)]
        public void AniType4_RunsBackwardsWrappedPositive()
        {
            var a = Make(4, Base(0, Vector2.zero), Delta(0, Vector2.zero, aniType: 4, delay: 1f));
            Assert.IsTrue(Sample(a, 1f, out var q));
            Assert.AreEqual(3, q.Texture, "0 - 1 wraps to n - 1");
            Assert.IsTrue(Sample(a, 5f, out q));
            Assert.AreEqual(3, q.Texture, "-5 mod 4, positive");
        }

        [Test, Ignore(Pending)]
        public void AngleUnits_ConvertToDegreesByTurn()
        {
            var a = Make(1, Base(0, Vector2.zero, angle: 256f));   // 1024 units per turn (lead default)
            Assert.IsTrue(Sample(a, 0f, out var q));
            Assert.AreEqual(90f, q.AngleDegrees, Tol);
        }

        [Test, Ignore(Pending)]
        public void Colour_IsDividedBy255()
        {
            var a = Make(1, Base(0, Vector2.zero, color: new Vector4(255, 51, 0, 127.5f)));
            Assert.IsTrue(Sample(a, 0f, out var q));
            Assert.AreEqual(1f, q.Color.r, Tol);
            Assert.AreEqual(0.2f, q.Color.g, Tol);
            Assert.AreEqual(0f, q.Color.b, Tol);
            Assert.AreEqual(0.5f, q.Color.a, Tol);
        }

        [Test, Ignore(Pending)]
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
    }
}
