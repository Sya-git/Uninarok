using NUnit.Framework;
using UnityEngine;

namespace Uninarok.Effects.Tests
{
    /// <summary>
    /// Pins the authored sampling rules of plan 4.2 (SPEC-0012 FR-4, FR-7, FR-10; D19) and the warp portal values of plan 4.7.
    /// Cells to units: 1 cell = 0.5 unit (RoUnits.GatCell, 10 * 0.1 / 2; the runtime assembly cannot reference the editor RoUnits,
    /// the same value as the EffectSettings.PixelsPerCell tooltip). AuthoredQuadSample.Size is in cells, as plan 4.2 says
    /// "SizePixels x SizeScale / PixelsPerCell cells". Which AuthoredQuadSample.Offset component is "up" is not pinned
    /// (plan: "x, y on the ground, z up"): box checks do not depend on the axis order.
    /// The cylinder strip (vertex count, uv) is checked in T5.3, not here.
    /// </summary>
    public class AuthoredSamplerTests
    {
        const float Tol = 1e-4f;
        const float UnitsPerCell = 0.5f;

        EffectSettings settings;

        [SetUp] public void SetUp() => settings = ScriptableObject.CreateInstance<EffectSettings>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(settings);

        // Linear curves: Hermite keys with matching slopes are exactly piecewise linear.
        static Keyframe K(float t, float v, float inSlope, float outSlope) => new Keyframe(t, v, inSlope, outSlope);

        // Plan 4.7 fade: 0 -> 1 over t 0..0.25, 1 until 0.75, -> 0 at 1. Slopes 4, 0, -4.
        static AnimationCurve FadeCurve() => new AnimationCurve(K(0f, 0f, 0f, 4f), K(0.25f, 1f, 4f, 0f), K(0.75f, 1f, 0f, -4f), K(1f, 0f, -4f, 0f));

        // Plan 4.7 HeightScale: 0 -> 1 at t 0.5 -> 0 at 1. Slopes 2, -2.
        static AnimationCurve HeightCurve() => new AnimationCurve(K(0f, 0f, 0f, 2f), K(0.5f, 1f, 2f, -2f), K(1f, 0f, -2f, 0f));

        // Plan 4.7 RadiusScale: 1 -> 0 linear over the cycle.
        static AnimationCurve RadiusCurve() => AnimationCurve.Linear(0f, 1f, 1f, 0f);

        /// <summary>The warp portal outer ring of plan 4.7 (ring_outer.cylinder).</summary>
        static AuthoredData WarpOuter() => new AuthoredData
        {
            Facing = EffectFacing.Ground,
            BottomRadius = 2f, TopRadius = 3.3f, Height = 1.1f, Sides = 20, RepeatU = 1f,
            Tint = new Color(0.5f, 0.5f, 1f, 1f), MaxAlpha = 0.4f,
            Alpha = FadeCurve(), RadiusScale = RadiusCurve(), HeightScale = HeightCurve(),
            Rotation = null,
        };

        /// <summary>The warp portal sparkle of plan 4.7 (sparkle.quad).</summary>
        static AuthoredData WarpSparkle() => new AuthoredData
        {
            Facing = EffectFacing.Camera,
            SizePixels = 50f, Tint = new Color(0.9f, 1f, 0.9f, 1f), MaxAlpha = 1f,
            Alpha = FadeCurve(),
            StartMin = new Vector3(-3, -3, 0), StartMax = new Vector3(3, 3, 0),
            EndMin = new Vector3(0, 0, 0), EndMax = new Vector3(0, 0, 4),
            Motion = null, Rotation = null,
        };

        void AssertWarpCylinder(float t, float radiusScale, float heightScale, float alphaScale)
        {
            // units = cells * 0.5; radii x RadiusScale(t) = (1 - t); height x HeightScale(t); alpha = 0.4 x Alpha(t)
            AuthoredSampler.SampleCylinder(WarpOuter(), settings, t, out var c);
            Assert.AreEqual(2f * radiusScale * UnitsPerCell, c.Bottom, Tol, "bottom at t " + t);
            Assert.AreEqual(3.3f * radiusScale * UnitsPerCell, c.Top, Tol, "top at t " + t);
            Assert.AreEqual(1.1f * heightScale * UnitsPerCell, c.Height, Tol, "height at t " + t);
            Assert.AreEqual(0.4f * alphaScale, c.Color.a, Tol, "alpha at t " + t);
            Assert.AreEqual(0.5f, c.Color.r, Tol);
            Assert.AreEqual(0.5f, c.Color.g, Tol);
            Assert.AreEqual(1f, c.Color.b, Tol);
            Assert.AreEqual(0f, c.AngleDegrees, Tol, "Rotation none");
        }

        // t = 0: radius scale 1, height scale 0, alpha 0 (fade start). bottom 2 cells = 1 u, top 3.3 cells = 1.65 u, height 0, alpha 0
        [Test] public void WarpCylinder_AtT0() => AssertWarpCylinder(0f, 1f, 0f, 0f);

        // t = 0.25: radius scale 0.75 (bottom 0.75 u, top 1.2375 u), height scale 0.5 (1.1 x 0.5 cells x 0.5 = 0.275 u), alpha 0.4 x 1
        [Test] public void WarpCylinder_AtT025() => AssertWarpCylinder(0.25f, 0.75f, 0.5f, 1f);

        // t = 0.5: radius scale 0.5 (bottom 0.5 u, top 0.825 u), height scale 1 (1.1 cells = 0.55 u), alpha 0.4
        [Test] public void WarpCylinder_AtT05() => AssertWarpCylinder(0.5f, 0.5f, 1f, 1f);

        // t = 0.75: radius scale 0.25 (bottom 0.25 u, top 0.4125 u), height scale 0.5 (0.275 u), alpha 0.4 x 1 (fade out starts)
        [Test] public void WarpCylinder_AtT075() => AssertWarpCylinder(0.75f, 0.25f, 0.5f, 1f);

        // t = 1: radius scale 0, height scale 0, alpha 0
        [Test] public void WarpCylinder_AtT1() => AssertWarpCylinder(1f, 0f, 0f, 0f);

        [Test]
        public void Cylinder_NullCurves_GiveNeutralValues()
        {
            // RadiusScale / HeightScale null = 1, Alpha null = 1 (colour alpha = MaxAlpha), Rotation null = 0
            var d = new AuthoredData { BottomRadius = 2f, TopRadius = 3f, Height = 1f, MaxAlpha = 0.4f, Tint = new Color(0.2f, 0.4f, 0.6f, 1f) };
            foreach (float t in new[] { 0f, 0.3f, 1f })
            {
                AuthoredSampler.SampleCylinder(d, settings, t, out var c);
                Assert.AreEqual(1f, c.Bottom, Tol);
                Assert.AreEqual(1.5f, c.Top, Tol);
                Assert.AreEqual(0.5f, c.Height, Tol);
                Assert.AreEqual(0f, c.AngleDegrees, Tol);
                Assert.AreEqual(0.4f, c.Color.a, Tol);
                Assert.AreEqual(0.2f, c.Color.r, Tol);
                Assert.AreEqual(0.4f, c.Color.g, Tol);
                Assert.AreEqual(0.6f, c.Color.b, Tol);
            }
        }

        [Test]
        public void Cylinder_RotationFollowsTheCurve()
        {
            var d = WarpOuter();
            d.Rotation = AnimationCurve.Linear(0f, 0f, 1f, 90f);
            AuthoredSampler.SampleCylinder(d, settings, 0.5f, out var c);
            Assert.AreEqual(45f, c.AngleDegrees, Tol);
        }

        [Test]
        public void ColourAlpha_IsClampedToZeroOne()
        {
            var d = new AuthoredData { MaxAlpha = 2f, BottomRadius = 1f, TopRadius = 1f, Height = 1f };
            AuthoredSampler.SampleCylinder(d, settings, 0.5f, out var c);
            Assert.AreEqual(1f, c.Color.a, Tol);
            d.MaxAlpha = -1f;
            AuthoredSampler.SampleCylinder(d, settings, 0.5f, out c);
            Assert.AreEqual(0f, c.Color.a, Tol);
        }

        [Test]
        public void Quad_NullCurves_GiveNeutralValues()
        {
            // SizeScale null = 1, Alpha null = 1, Rotation null = 0, Motion null = linear (t 0.5 = midpoint of the two offsets)
            var d = new AuthoredData
            {
                SizePixels = 70f, MaxAlpha = 0.6f, Tint = new Color(0.1f, 0.2f, 0.3f, 1f),
                StartMin = new Vector3(1, 2, 3), StartMax = new Vector3(1, 2, 3), EndMin = new Vector3(3, 4, 5), EndMax = new Vector3(3, 4, 5),
            };
            AuthoredSampler.SampleQuad(d, settings, 0f, 5u, out var q0);
            AuthoredSampler.SampleQuad(d, settings, 0.5f, 5u, out var qh);
            AuthoredSampler.SampleQuad(d, settings, 1f, 5u, out var q1);
            Assert.AreEqual(2f, qh.Size, Tol);
            Assert.AreEqual(0f, qh.AngleDegrees, Tol);
            Assert.AreEqual(0.6f, qh.Color.a, Tol);
            Assert.AreEqual(0.1f, qh.Color.r, Tol);
            Assert.AreEqual(0.2f, qh.Color.g, Tol);
            Assert.AreEqual(0.3f, qh.Color.b, Tol);
            Assert.AreEqual(0f, ((q0.Offset + q1.Offset) * 0.5f - qh.Offset).magnitude, Tol);
        }

        [Test]
        public void Quad_OffsetIsInsideStartBoxAtT0_AndEndBoxAtT1()
        {
            // Warp sparkle (plan 4.7): start x, y in +-3 cells (+-1.5 u), z 0; end x, y 0, z in 0..4 cells (0..2 u).
            // Which component is up is not pinned: at t 0 every component is within +-1.5 and one (the up axis) is 0;
            // at t 1 two components are 0 and the third is within 0..2.
            for (uint seed = 1; seed <= 50; seed++)
            {
                AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0f, seed, out var q0);
                Assert.LessOrEqual(Mathf.Abs(q0.Offset.x), 3f * UnitsPerCell + Tol);
                Assert.LessOrEqual(Mathf.Abs(q0.Offset.y), 3f * UnitsPerCell + Tol);
                Assert.LessOrEqual(Mathf.Abs(q0.Offset.z), 3f * UnitsPerCell + Tol);
                Assert.IsTrue(Mathf.Abs(q0.Offset.y) < Tol || Mathf.Abs(q0.Offset.z) < Tol, "one axis (up) is 0 at t 0");

                AuthoredSampler.SampleQuad(WarpSparkle(), settings, 1f, seed, out var q1);
                int zeros = 0, rise = 0;
                for (int i = 0; i < 3; i++)
                {
                    float v = q1.Offset[i];
                    if (Mathf.Abs(v) < Tol) zeros++;
                    else { rise++; Assert.GreaterOrEqual(v, -Tol); Assert.LessOrEqual(v, 4f * UnitsPerCell + Tol); }
                }
                Assert.GreaterOrEqual(zeros, 2, "end box has two axes at 0 (seed " + seed + ")");
                Assert.LessOrEqual(rise, 1);
            }
        }

        [Test]
        public void Quad_OffsetAxes_AuthoredZIsUnityUp_AuthoredYIsUnityZ()
        {
            // Authored (x, y on the ground, z up) in cells -> Unity (x, up = y, ground y = z) in units, like RoSpace for ground and up.
            var d = new AuthoredData
            {
                StartMin = new Vector3(2, 4, 6), StartMax = new Vector3(2, 4, 6), EndMin = new Vector3(2, 4, 6), EndMax = new Vector3(2, 4, 6),
            };
            AuthoredSampler.SampleQuad(d, settings, 0.5f, 1u, out var q);
            Assert.AreEqual(1f, q.Offset.x, Tol);
            Assert.AreEqual(3f, q.Offset.y, Tol, "authored z (up) is Unity y");
            Assert.AreEqual(2f, q.Offset.z, Tol, "authored y (ground) is Unity z");
        }

        [Test]
        public void Quad_SizeIsSizePixelsOverPixelsPerCell()
        {
            // 50 px / 35 px per cell, SizeScale null = 1
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.5f, 3u, out var q);
            Assert.AreEqual(50f / 35f, q.Size, Tol);
            settings.PixelsPerCell = 25f;
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.5f, 3u, out q);
            Assert.AreEqual(2f, q.Size, Tol);
        }

        [Test]
        public void Quad_SizeScaleAndRotationFollowTheirCurves()
        {
            var d = WarpSparkle();
            d.SizeScale = AnimationCurve.Linear(0f, 1f, 1f, 2f);
            d.Rotation = AnimationCurve.Linear(0f, 0f, 1f, 90f);
            AuthoredSampler.SampleQuad(d, settings, 0.5f, 3u, out var q);
            Assert.AreEqual(50f * 1.5f / 35f, q.Size, Tol);
            Assert.AreEqual(45f, q.AngleDegrees, Tol);
        }

        [Test]
        public void Quad_AlphaAndColourFollowTheFadeCurve()
        {
            // tint (0.9, 1, 0.9), MaxAlpha 1 x fade: 0 at t 0, 0.5 at t 0.125, 1 at 0.5, 0 at 1
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.125f, 3u, out var q);
            Assert.AreEqual(0.5f, q.Color.a, Tol);
            Assert.AreEqual(0.9f, q.Color.r, Tol);
            Assert.AreEqual(1f, q.Color.g, Tol);
            Assert.AreEqual(0.9f, q.Color.b, Tol);
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 1f, 3u, out q);
            Assert.AreEqual(0f, q.Color.a, Tol);
        }

        [Test]
        public void Quad_SameSeed_GivesIdenticalSample()
        {
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.37f, 42u, out var a);
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.37f, 42u, out var b);
            Assert.AreEqual(a.Offset, b.Offset);
            Assert.AreEqual(a.Size, b.Size);
            Assert.AreEqual(a.AngleDegrees, b.AngleDegrees);
            Assert.AreEqual(a.Color, b.Color);
        }

        [Test]
        public void Quad_StartAndEndAreDrawnOncePerPlay_SoALaterCycleRepeatsThePath()
        {
            // Drawn once per play (D19): the sample depends on t and seed only, so the same t in a later cycle (t is the
            // cycle position) gives the same offset, and t 0.5 lies on the line between the same start and end.
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0f, 42u, out var s0);
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 1f, 42u, out var s1);
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.5f, 42u, out var mid1);
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0.5f, 42u, out var mid2);   // the next cycle's 0.5
            Assert.AreEqual(mid1.Offset, mid2.Offset);
            Assert.AreEqual(0f, ((s0.Offset + s1.Offset) * 0.5f - mid1.Offset).magnitude, Tol);
        }

        [Test]
        public void Quad_DifferentSeeds_Differ()
        {
            AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0f, 1u, out var first);
            bool anyDiffers = false;
            for (uint seed = 2; seed <= 16; seed++)
            {
                AuthoredSampler.SampleQuad(WarpSparkle(), settings, 0f, seed, out var q);
                if (q.Offset != first.Offset) anyDiffers = true;
            }
            Assert.IsTrue(anyDiffers);
        }

        [Test]
        public void Sampling_AllocatesNothing()
        {
            var quad = WarpSparkle();
            var cyl = WarpOuter();
            AuthoredSampler.SampleQuad(quad, settings, 0.1f, 1u, out _);
            AuthoredSampler.SampleCylinder(cyl, settings, 0.1f, out _);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            float sum = 0f;
            for (int i = 0; i < 10000; i++)
            {
                float t = (i % 100) / 100f;
                AuthoredSampler.SampleQuad(quad, settings, t, (uint)i, out var q);
                AuthoredSampler.SampleCylinder(cyl, settings, t, out var c);
                sum += q.Size + c.Height;
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0, after - before, "sum " + sum);
        }
    }
}
