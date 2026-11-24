using System;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// Draws an STR instance (SPEC-0012 FR-2, FR-3, FR-9; plan 4.5 "Parts", "Mesh", "STR placement", D22, D23, D28):
    /// one submesh of one quad per layer in file order, each layer sampled by <see cref="EffectSampler"/> at the frame
    /// <c>Age × Fps</c> and written through <see cref="EffectMeshWriter"/>. A layer's material follows its current base key's
    /// blend and its current texture frame; a hidden layer is a zero-area submesh. Pooled with its instance slot: the cursors
    /// are kept between plays and only grow, so a play after the slot's first use and every tick allocate nothing.
    /// <para>Corners are object space with z 0 (the material faces the camera, so the shader expands x and y along the
    /// camera right and up from the object origin): corner = rotate(Xy, angle) + (Position - Pivot) in pixels, y down,
    /// scaled by GatCell / PixelsPerCell, y flipped, then lowered by VerticalBaseCells × GatCell. The rotation is the
    /// ordinary one on the pixel coordinates (x' = x cos - y sin, y' = x sin + y cos). The uv rectangle of the key
    /// (<c>Uv0</c> top-left, <c>Uv1</c> bottom-right, str-reference.md 6, v from the image's top row) gives the corner uvs;
    /// v is flipped to Unity's bottom-up convention.</para>
    /// </summary>
    internal sealed class StrPart : IEffectPart
    {
        readonly EffectRuntime runtime;
        int[] cursors = new int[4];
        int[] submeshOfLayer = new int[4];
        int layers;
        bool bound;

        public StrPart(EffectRuntime runtime) { this.runtime = runtime; }

        public bool CanDraw => bound;

        public string Problem { get; private set; }

        public float Bind(EffectInstance instance)
        {
            bound = false;
            Problem = null;
            var asset = instance.Asset;
            layers = asset != null && asset.Layers != null ? asset.Layers.Length : 0;
            if (cursors.Length < layers)
            {
                cursors = new int[layers];
                submeshOfLayer = new int[layers];
            }
            Array.Clear(cursors, 0, layers);

            var writer = instance.GetWriter();
            writer.BeginLayout();
            for (int l = 0; l < layers; l++) submeshOfLayer[l] = writer.AddQuads(1);
            float r = ConservativeRadius(asset, runtime.Settings);
            if (!writer.EndLayout(new Bounds(Vector3.zero, new Vector3(2f * r, 2f * r, 2f * r))))
            {
                layers = 0;           // layout refused (too many layers): the runtime plays the placeholder
                Problem = "has too many layers to draw";
                return 0f;
            }

            // Start every submesh with a known material so a layer hidden before its first key never holds a null one.
            var materials = runtime.Materials;
            var start = materials.Get(null, 5, 6, EffectFacing.Camera, false);
            for (int l = 0; l < layers; l++) writer.SetMaterial(submeshOfLayer[l], start);
            writer.Upload();
            bound = true;
            return 0f;
        }

        public void Tick(EffectInstance instance)
        {
            if (!bound) return;
            var asset = instance.Asset;
            var settings = runtime.Settings;
            var writer = instance.GetWriter();
            var materials = runtime.Materials;
            if (instance.Seeked) Array.Clear(cursors, 0, layers);

            float frame = instance.Age * instance.FrameRate;
            float k = EffectUnits.GatCell / settings.PixelsPerCell;
            float lower = settings.VerticalBaseCells * EffectUnits.GatCell;
            var pivot = settings.Pivot;

            for (int l = 0; l < layers; l++)
            {
                int sub = submeshOfLayer[l];
                if (!EffectSampler.Sample(asset, settings, l, frame, ref cursors[l], out var q))
                {
                    writer.HideSubMesh(sub);
                    continue;
                }

                var textures = asset.Textures;
                Texture2D texture = textures != null && (uint)q.Texture < (uint)textures.Length ? textures[q.Texture] : null;
                var material = materials.Get(texture, q.Src, q.Dst, EffectFacing.Camera, false, out bool blendKnown);
                if (!blendKnown) runtime.WarnBlendOnce(asset);
                writer.SetMaterial(sub, material);

                float rad = q.AngleDegrees * Mathf.Deg2Rad, cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
                var offset = q.Position - pivot;
                Vector3 c0 = Corner(q.C0, cos, sin, offset, k, lower), c1 = Corner(q.C1, cos, sin, offset, k, lower);
                Vector3 c2 = Corner(q.C2, cos, sin, offset, k, lower), c3 = Corner(q.C3, cos, sin, offset, k, lower);

                // Uv rectangle: Uv0 top-left, Uv1 bottom-right; v counted from the image's top row, so 1 - v in Unity.
                float u0 = q.Uv0.x, v0 = 1f - q.Uv0.y, u1 = q.Uv1.x, v1 = 1f - q.Uv1.y;
                var tl = new Vector2(u0, v0); var tr = new Vector2(u1, v0);
                var br = new Vector2(u1, v1); var bl = new Vector2(u0, v1);

                writer.WriteQuad(sub, 0, c0, c1, c2, c3, tl, tr, br, bl, ToColor32(q.Color));
            }
            writer.Upload();
        }

        public void Release(EffectInstance instance)
        {
            bound = false;     // the instance object is deactivated by the runtime; the next Bind rewrites everything
        }

        static Vector3 Corner(Vector2 xy, float cos, float sin, Vector2 offset, float k, float lower)
        {
            float px = xy.x * cos - xy.y * sin + offset.x;
            float py = xy.x * sin + xy.y * cos + offset.y;
            return new Vector3(px * k, -py * k - lower, 0f);
        }

        static Color32 ToColor32(Color c) => new Color32(Byte(c.r), Byte(c.g), Byte(c.b), Byte(c.a));

        static byte Byte(float v)
        {
            if (!(v > 0f)) return 0;          // also NaN
            if (v >= 1f) return 255;
            return (byte)(v * 255f + 0.5f);
        }

        /// <summary>Half size of a cube around the object origin that holds every corner of every base key at any angle.</summary>
        static float ConservativeRadius(EffectAsset asset, EffectSettings s)
        {
            if (asset == null || asset.Keys == null) return 1f;
            float k = EffectUnits.GatCell / s.PixelsPerCell;
            float lower = s.VerticalBaseCells * EffectUnits.GatCell;
            float max = 0f;
            var keys = asset.Keys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].Type != 0) continue;
                float shift = (keys[i].Position - s.Pivot).magnitude;
                max = Mathf.Max(max, keys[i].Xy0.magnitude + shift, keys[i].Xy1.magnitude + shift,
                    keys[i].Xy2.magnitude + shift, keys[i].Xy3.magnitude + shift);
            }
            float r = max * k + Mathf.Abs(lower);
            return float.IsNaN(r) || float.IsInfinity(r) ? 1f : r + 0.01f;
        }
    }
}
