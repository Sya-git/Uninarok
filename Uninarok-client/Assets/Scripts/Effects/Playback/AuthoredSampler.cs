using System;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>Quad entry at cycle position t (SPEC-0012 plan 4.2). Offset in units; Size in cells; angle in degrees.</summary>
    public struct AuthoredQuadSample { public Vector3 Offset; public float Size, AngleDegrees; public Color Color; }

    /// <summary>Cylinder entry at cycle position t (SPEC-0012 plan 4.2). Radii and height in units.</summary>
    public struct CylinderSample { public float Bottom, Top, Height, AngleDegrees; public Color Color; }

    /// <summary>Authored kinds sampling, pure and allocation-free (SPEC-0012 FR-4, FR-7, FR-9; plan 4.2 "Authored sampling rules"). Stub: implemented in T2.4.</summary>
    public static class AuthoredSampler
    {
        public static void SampleQuad(AuthoredData d, EffectSettings s, float t, uint seed, out AuthoredQuadSample q)
            => throw new NotImplementedException("T2.4");

        public static void SampleCylinder(AuthoredData d, EffectSettings s, float t, out CylinderSample c)
            => throw new NotImplementedException("T2.4");
    }
}
