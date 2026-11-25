using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// Draws an authored instance: Quad, Cylinder or Procedural (SPEC-0012 FR-4, FR-7, FR-9; plan 4.5 "AuthoredPart", D19, D20,
    /// D28, D29). A sibling of <see cref="StrPart"/>: same pooled instance object, mesh writer, materials and clock. One
    /// submesh with one material per entry. Per tick: t = (Age mod Length) / Length for a loop, Age / Length clamped for a
    /// one-shot; the cycle index is the runtime's count of completed loops. Random values come from the instance seed only
    /// (<see cref="AuthoredSampler"/>), so every cycle repeats the same path.
    /// <para>Quad: corners (-h, +h), (+h, +h), (+h, -h), (-h, -h) with h = size / 2 in units (Size is in cells × GatCell),
    /// rotated by the angle, then centred on the offset; local y is up in the picture. Camera facing: object-space (x, y), z 0
    /// (the shader expands them along the camera right and up). Ground: flat on the xz plane (local y is object +z) at the
    /// offset's height. uv is the full rectangle, v flipped to Unity's bottom-up convention exactly as in <see cref="StrPart"/>
    /// (top-left (0, 1)). Cylinder: <see cref="EffectMeshWriter.WriteCylinder"/>, ground (object space), RepeatU &gt; 1 enables
    /// repeat wrap. A part that cannot draw (invalid Sides, unknown procedure) writes nothing and says so in
    /// <see cref="CanDraw"/>; the runtime then plays <see cref="PlaceholderPart"/>.</para>
    /// </summary>
    internal sealed class AuthoredPart : IEffectPart
    {
        readonly EffectRuntime runtime;
        EffectKind kind;
        int submesh;
        IEffectProcedure procedure;
        int procedureQuads;
        bool bound;

        public AuthoredPart(EffectRuntime runtime) { this.runtime = runtime; }

        /// <summary>False when the last bind could not draw (the runtime plays the placeholder).</summary>
        public bool CanDraw => bound;

        /// <summary>Why the last bind could not draw; null when it can.</summary>
        public string Problem { get; private set; }

        public float Bind(EffectInstance instance)
        {
            bound = false;
            Problem = null;
            procedure = null;
            var asset = instance.Asset;
            var data = asset != null ? asset.Authored : null;
            var writer = instance.GetWriter();
            writer.BeginLayout();
            kind = asset != null ? asset.Kind : EffectKind.Quad;
            if (data == null) return Fail(writer, "has no authored data");

            Material material;
            var materials = runtime.Materials;
            var textures = asset.Textures;
            Texture2D texture = textures != null && (uint)data.Texture < (uint)textures.Length ? textures[data.Texture] : null;
            Bounds bounds;
            switch (kind)
            {
                case EffectKind.Quad:
                    submesh = writer.AddQuads(1);
                    bounds = QuadBounds(data, runtime.Settings);
                    material = materials.Get(texture, data.SrcBlend, data.DstBlend, data.Facing, false, out bool qk);
                    if (!qk) runtime.WarnBlendOnce(asset);
                    break;
                case EffectKind.Cylinder:
                    submesh = writer.AddCylinder(data.Sides);
                    bounds = CylinderBounds(data);
                    material = materials.Get(texture, data.SrcBlend, data.DstBlend, EffectFacing.Ground, data.RepeatU > 1f, out bool ck);
                    if (!ck) runtime.WarnBlendOnce(asset);
                    break;
                case EffectKind.Procedural:
                    if (!EffectProcedures.TryGet(data.Procedure, out procedure))
                        return Fail(writer, "names an unknown procedure '" + data.Procedure + "'");
                    procedureQuads = procedure.MaxQuads;
                    submesh = writer.AddQuads(procedureQuads);
                    bounds = new Bounds(Vector3.zero, Vector3.one * 100f);       // a procedure's reach is not known: generous
                    material = materials.Get(texture, data.SrcBlend, data.DstBlend, data.Facing, false);
                    break;
                default:
                    return Fail(writer, "is not an authored kind");
            }

            bool laidOut = writer.EndLayout(bounds);     // always closes the layout; false when the add failed (mesh left empty)
            if (!laidOut || submesh < 0)
                return Fail(writer, kind == EffectKind.Cylinder ? "has an invalid Sides count " + data.Sides : "has an invalid layout", false);
            writer.SetMaterial(submesh, material);
            writer.Upload();
            bound = true;
            return asset.Length;
        }

        float Fail(EffectMeshWriter writer, string why, bool endLayout = true)
        {
            Problem = why;
            if (endLayout) writer.EndLayout(new Bounds(Vector3.zero, Vector3.one));   // no submesh added: an empty mesh, nothing drawn
            writer.Upload();
            return 0f;
        }

        public void Tick(EffectInstance instance)
        {
            if (!bound) return;
            var asset = instance.Asset;
            var data = asset.Authored;
            var writer = instance.GetWriter();

            float length = instance.Length;
            float t = instance.Loop ? (instance.Age % length) / length : Mathf.Clamp01(instance.Age / length);
            var settings = runtime.Settings;

            switch (kind)
            {
                case EffectKind.Quad:
                    AuthoredSampler.SampleQuad(data, settings, t, instance.Seed, out var q);
                    WriteQuad(writer, submesh, 0, q, data.Facing);
                    break;
                case EffectKind.Cylinder:
                    AuthoredSampler.SampleCylinder(data, settings, t, out var c);
                    writer.WriteCylinder(submesh, c, data.RepeatU);
                    break;
                case EffectKind.Procedural:
                    var span = new EffectQuadSpan(writer, submesh, procedureQuads, runtime, asset);
                    procedure.Write(data, t, instance.Seed, instance.Cycle, ref span);
                    span.HideRest();
                    break;
            }
            writer.Upload();
        }

        public void Release(EffectInstance instance)
        {
            bound = false;
            procedure = null;
        }

        /// <summary>Writes one quad at <paramref name="index"/> of a quads submesh (see the class comment for the geometry).</summary>
        internal static void WriteQuad(EffectMeshWriter writer, int submesh, int index, in AuthoredQuadSample q, EffectFacing facing)
        {
            float h = q.Size * EffectUnits.GatCell * 0.5f;
            float rad = q.AngleDegrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            Vector3 c0 = Corner(-h, h, cos, sin, q.Offset, facing), c1 = Corner(h, h, cos, sin, q.Offset, facing);
            Vector3 c2 = Corner(h, -h, cos, sin, q.Offset, facing), c3 = Corner(-h, -h, cos, sin, q.Offset, facing);
            writer.WriteQuad(submesh, index, c0, c1, c2, c3,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(0f, 0f), ToColor32(q.Color));
        }

        static Vector3 Corner(float x, float y, float cos, float sin, Vector3 offset, EffectFacing facing)
        {
            float px = x * cos - y * sin, py = x * sin + y * cos;
            return facing == EffectFacing.Ground
                ? new Vector3(offset.x + px, offset.y, offset.z + py)
                : new Vector3(offset.x + px, offset.y + py, 0f);
        }

        static Color32 ToColor32(Color c) => new Color32(Byte(c.r), Byte(c.g), Byte(c.b), Byte(c.a));

        static byte Byte(float v)
        {
            if (!(v > 0f)) return 0;          // also NaN
            if (v >= 1f) return 255;
            return (byte)(v * 255f + 0.5f);
        }

        // ---- bounds (object space, set once at bind; curves sampled) ----

        static float MaxOf(AnimationCurve curve)
        {
            if (curve == null) return 1f;
            float max = 0f;
            for (int i = 0; i <= 32; i++) max = Mathf.Max(max, Mathf.Abs(curve.Evaluate(i / 32f)));
            return max;
        }

        static float MaxAbs(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float m = 0f;
            // two-argument Max only: the params overload allocates an array per call (plan D32)
            for (int i = 0; i < 3; i++)
                m = Mathf.Max(Mathf.Max(m, Mathf.Abs(a[i])), Mathf.Max(Mathf.Max(Mathf.Abs(b[i]), Mathf.Abs(c[i])), Mathf.Abs(d[i])));
            return m;
        }

        static Bounds QuadBounds(AuthoredData d, EffectSettings s)
        {
            float reach = MaxAbs(d.StartMin, d.StartMax, d.EndMin, d.EndMax) * EffectUnits.GatCell;
            float size = d.SizePixels * MaxOf(d.SizeScale) / s.PixelsPerCell * EffectUnits.GatCell;
            float r = reach + size * 0.75f + 0.01f;       // the half diagonal of a rotated quad is 0.707 of its size
            if (float.IsNaN(r) || float.IsInfinity(r)) r = 50f;
            return new Bounds(Vector3.zero, new Vector3(2f * r, 2f * r, 2f * r));
        }

        static Bounds CylinderBounds(AuthoredData d)
        {
            float radius = Mathf.Max(Mathf.Abs(d.BottomRadius), Mathf.Abs(d.TopRadius)) * MaxOf(d.RadiusScale) * EffectUnits.GatCell + 0.01f;
            float height = Mathf.Abs(d.Height) * MaxOf(d.HeightScale) * EffectUnits.GatCell + 0.01f;
            if (float.IsNaN(radius) || float.IsInfinity(radius)) radius = 50f;
            if (float.IsNaN(height) || float.IsInfinity(height)) height = 50f;
            return new Bounds(new Vector3(0f, height * 0.5f, 0f), new Vector3(2f * radius, height, 2f * radius));
        }
    }
}
