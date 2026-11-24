using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>Cell and unit constants of the runtime effect code. Uninarok.Effects cannot reference the editor assemblies.</summary>
    public static class EffectUnits
    {
        /// <summary>Unity units per GAT cell. Source: RoUnits.GatCell (editor, Uninarok.RoImport.Core.Space), half a ground cube.</summary>
        public const float GatCell = 0.5f;
    }

    /// <summary>Quad entry at cycle position t (SPEC-0012 plan 4.2). Offset in units; Size in cells (the part converts to units); angle in degrees.</summary>
    public struct AuthoredQuadSample { public Vector3 Offset; public float Size, AngleDegrees; public Color Color; }

    /// <summary>Cylinder entry at cycle position t (SPEC-0012 plan 4.2). Radii and height in units.</summary>
    public struct CylinderSample { public float Bottom, Top, Height, AngleDegrees; public Color Color; }

    /// <summary>Authored kinds sampling, pure and allocation-free (SPEC-0012 FR-4, FR-7, FR-9; plan 4.2 "Authored sampling rules").</summary>
    public static class AuthoredSampler
    {
        // EffectRandom channels of the offset boxes: start x, y, z then end x, y, z.
        const int ChannelStart = 0, ChannelEnd = 3;

        public static void SampleQuad(AuthoredData d, EffectSettings s, float t, uint seed, out AuthoredQuadSample q)
        {
            // Start and end are drawn from the seed only (D19), so every cycle repeats the same path.
            Vector3 start = DrawBox(d.StartMin, d.StartMax, seed, ChannelStart);
            Vector3 end = DrawBox(d.EndMin, d.EndMax, seed, ChannelEnd);
            float m = d.Motion != null ? d.Motion.Evaluate(t) : t;
            Vector3 cells = start + (end - start) * m;
            // Authored offsets are x, y on the ground and z up (RO cells). Unity is y up, so ground y -> Unity z, up z -> Unity y
            // (the same sign as RoSpace: x and the row axis keep their sign, only "up" is the vertical axis).
            q.Offset = new Vector3(cells.x, cells.z, cells.y) * EffectUnits.GatCell;
            q.Size = d.SizePixels * Scale(d.SizeScale, t) / s.PixelsPerCell;
            q.AngleDegrees = d.Rotation != null ? d.Rotation.Evaluate(t) : 0f;
            q.Color = ColorAt(d, t);
        }

        public static void SampleCylinder(AuthoredData d, EffectSettings s, float t, out CylinderSample c)
        {
            float r = Scale(d.RadiusScale, t) * EffectUnits.GatCell;
            c.Bottom = d.BottomRadius * r;
            c.Top = d.TopRadius * r;
            c.Height = d.Height * Scale(d.HeightScale, t) * EffectUnits.GatCell;
            c.AngleDegrees = d.Rotation != null ? d.Rotation.Evaluate(t) : 0f;
            c.Color = ColorAt(d, t);
        }

        static float Scale(AnimationCurve curve, float t) => curve != null ? curve.Evaluate(t) : 1f;

        static Color ColorAt(AuthoredData d, float t)
        {
            float a = Mathf.Clamp01(d.MaxAlpha * Scale(d.Alpha, t));
            return new Color(d.Tint.r, d.Tint.g, d.Tint.b, a);
        }

        static Vector3 DrawBox(Vector3 min, Vector3 max, uint seed, int channel) => new Vector3(
            EffectRandom.Range(seed, channel, min.x, max.x),
            EffectRandom.Range(seed, channel + 1, min.y, max.y),
            EffectRandom.Range(seed, channel + 2, min.z, max.z));
    }
}
