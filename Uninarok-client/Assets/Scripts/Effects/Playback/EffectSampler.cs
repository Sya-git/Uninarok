using System;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>One STR layer at one frame (SPEC-0012 plan 4.2). Corners and uv are as the writer needs them.</summary>
    public struct QuadSample
    {
        public Vector2 C0, C1, C2, C3, Uv0, Uv1, Uv2, Uv3, Position;
        public float AngleDegrees;
        public Color Color;
        /// <summary>Index into <see cref="EffectAsset.Textures"/> (layer's FirstTexture + texture frame); -1 when the layer has no texture.</summary>
        public int Texture;
        public byte Src, Dst;
    }

    /// <summary>
    /// STR sampling (SPEC-0012 FR-3, FR-9; plan 4.2 "Sampling semantics", lead rule D17, settings D23).
    /// Pure and allocation-free: the result depends only on the asset, settings, layer and frame; the cursor is a hint
    /// (the cached base key index) that makes continuous play O(1) amortised and never changes the result.
    /// Keys of a layer are sorted by frame (RoStrCompiler contract); unsorted keys give a defined result, not a crash.
    /// </summary>
    public static class EffectSampler
    {
        /// <summary>
        /// Samples <paramref name="layer"/> at fractional <paramref name="frame"/>. False = the layer is hidden at this
        /// frame (q is default). <paramref name="cursor"/> caches the base key index relative to the layer; 0 (or any
        /// value) is a valid start, a seek may reset it to 0.
        /// </summary>
        public static bool Sample(EffectAsset asset, EffectSettings settings, int layer, float frame, ref int cursor,
            out QuadSample q)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            q = default;
            if (asset == null || asset.Layers == null || asset.Keys == null) return false;
            if ((uint)layer >= (uint)asset.Layers.Length) return false;
            if (float.IsNaN(frame)) return false;

            var l = asset.Layers[layer];
            var keys = asset.Keys;
            int first = l.FirstKey, count = l.KeyCount;
            if (count <= 0 || first < 0 || first > keys.Length - count) return false;

            // Last key with Frame <= frame (sorted keys): walk back from the hint, then forward.
            int c = cursor;
            if (c < 0) c = 0; else if (c >= count) c = count - 1;
            while (c > 0 && keys[first + c].Frame > frame) c--;
            while (c + 1 < count && keys[first + c + 1].Frame <= frame) c++;
            int last = c;

            // Base key = the last type-0 key at or before it.
            int b = last;
            while (b >= 0 && keys[first + b].Type != 0) b--;
            if (b < 0 || keys[first + b].Frame > frame)
            {
                cursor = c;
                return false;                                    // before the first base key
            }
            cursor = b;

            ref readonly var bk = ref keys[first + b];
            bool hasDelta = b + 1 < count && keys[first + b + 1].Type == 1 && keys[first + b + 1].Frame == bk.Frame;

            // After the last key of the layer, hidden unless a delta key carries the base on.
            if (!hasDelta && last == count - 1 && frame > keys[first + last].Frame) return false;

            int n = l.TextureCount;
            float aniFrame;
            if (hasDelta)
            {
                ref readonly var dk = ref keys[first + b + 1];
                float d = frame - bk.Frame;
                q.Position = bk.Position + dk.Position * d;
                q.Uv0 = bk.Uv0 + dk.Uv0 * d; q.Uv1 = bk.Uv1 + dk.Uv1 * d;
                q.Uv2 = bk.Uv2 + dk.Uv2 * d; q.Uv3 = bk.Uv3 + dk.Uv3 * d;
                q.C0 = bk.Xy0 + dk.Xy0 * d; q.C1 = bk.Xy1 + dk.Xy1 * d;
                q.C2 = bk.Xy2 + dk.Xy2 * d; q.C3 = bk.Xy3 + dk.Xy3 * d;
                float angle = bk.Angle + dk.Angle * d;
                q.AngleDegrees = ToDegrees(angle, settings);
                var col = bk.Color + dk.Color * d;
                q.Color = new Color(col.x / 255f, col.y / 255f, col.z / 255f, col.w / 255f);
                aniFrame = AniFrame(bk, dk, d, n);
            }
            else
            {
                q.Position = bk.Position;
                q.Uv0 = bk.Uv0; q.Uv1 = bk.Uv1; q.Uv2 = bk.Uv2; q.Uv3 = bk.Uv3;
                q.C0 = bk.Xy0; q.C1 = bk.Xy1; q.C2 = bk.Xy2; q.C3 = bk.Xy3;
                q.AngleDegrees = ToDegrees(bk.Angle, settings);
                q.Color = new Color(bk.Color.x / 255f, bk.Color.y / 255f, bk.Color.z / 255f, bk.Color.w / 255f);
                aniFrame = bk.AniFrame;                           // held
            }

            q.Texture = TextureIndex(asset, l, aniFrame);
            q.Src = bk.SrcBlend;                                  // blend = the base key's codes
            q.Dst = bk.DstBlend;
            return true;
        }

        /// <summary>FrameCount / Fps seconds (plan 4.2, owner decision O2: the last frame is FrameCount - 1); 0 when Fps is not positive.</summary>
        public static float Length(EffectAsset asset)
        {
            if (asset == null || !(asset.Fps > 0f) || asset.FrameCount <= 0) return 0f;
            return asset.FrameCount / asset.Fps;
        }

        static float ToDegrees(float units, EffectSettings s)
        {
            float perTurn = s.AngleUnitsPerTurn;
            if (!(perTurn > 0f) || float.IsInfinity(perTurn)) return 0f;   // invalid setting: no rotation rather than inf / NaN
            return units * 360f / perTurn;
        }

        /// <summary>Texture frame (unclamped, may be fractional) by the delta key's anitype; d = frame - base.Frame.</summary>
        static float AniFrame(in EffectAsset.KeyData bk, in EffectAsset.KeyData dk, float d, int n)
        {
            // Delay: the BASE key's (owner decision O4); a non-finite delay reads as 0, as GRF reads the +inf of delta keys.
            float delay = float.IsNaN(bk.Delay) || float.IsInfinity(bk.Delay) ? 0f : bk.Delay;
            switch (dk.AniType)
            {
                case 0: return bk.AniFrame;                       // hold the base key's aniframe (owner decision O3)
                case 1: return bk.AniFrame + dk.AniFrame * d;
                case 2: return Math.Min(bk.AniFrame + delay * d, n - 1);
                case 3: return Wrap(bk.AniFrame + delay * d, n);
                case 4: return Wrap(bk.AniFrame - delay * d, n);  // reverse, wrapped positive (owner)
                default: return bk.AniFrame;                      // 5, 6 and unknown: hold the base aniframe (owner decision O5; StrPart warns once)
            }
        }

        /// <summary>True when a key's anitype is outside 0..4 (5, 6 or unknown): <see cref="Sample"/> holds the base aniframe for it.</summary>
        public static bool HasUnsupportedAniType(EffectAsset asset)
        {
            if (asset == null || asset.Keys == null) return false;
            var keys = asset.Keys;
            for (int i = 0; i < keys.Length; i++)
                if (keys[i].AniType > 4) return true;
            return false;
        }

        static float Wrap(float v, int n)
        {
            if (n <= 0 || float.IsNaN(v) || float.IsInfinity(v)) return 0f;
            double f = Math.Floor((double)v);
            double r = f - n * Math.Floor(f / n);
            return (float)r;
        }

        /// <summary>Absolute texture index: FirstTexture + floor(aniFrame), clamped to the layer's textures and the asset's array; -1 if none.</summary>
        static int TextureIndex(EffectAsset asset, in EffectAsset.LayerData l, float aniFrame)
        {
            int n = l.TextureCount;
            int total = asset.Textures != null ? asset.Textures.Length : 0;
            if (n <= 0 || l.FirstTexture < 0 || l.FirstTexture >= total) return -1;
            if (n > total - l.FirstTexture) n = total - l.FirstTexture;
            int i;
            if (float.IsNaN(aniFrame) || aniFrame <= 0f) i = 0;
            else if (aniFrame >= n - 1) i = n - 1;
            else i = (int)aniFrame;                               // floor, aniFrame in (0, n - 1)
            return l.FirstTexture + i;
        }
    }
}
