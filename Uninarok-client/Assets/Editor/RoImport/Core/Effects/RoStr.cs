using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>
    /// One STR effect file as read through GRF (PLAN-0012 4.1, SPEC-0012 FR-2). Pure data, values raw as the file holds them;
    /// rules (angle to degrees, colour / 255, interpolation) live in the sampler and settings. Built by
    /// <see cref="Formats.RoStrReader"/>.
    /// </summary>
    public sealed class RoStr
    {
        /// <summary>The header version (0x94 for every file of the data, str-reference.md §5).</summary>
        public int Version;
        public float Fps;
        /// <summary>The header's frame count ("max key", GRF <c>Str.MaxKeyFrame</c>).</summary>
        public int FrameCount;
        /// <summary>File order.</summary>
        public IReadOnlyList<RoStrLayer> Layers;
    }

    public sealed class RoStrLayer
    {
        /// <summary>Names as stored, decoded from CP949.</summary>
        public IReadOnlyList<string> TextureNames;
        /// <summary>File order (base and delta keys together).</summary>
        public IReadOnlyList<RoStrKey> Keys;
    }

    /// <summary>One STR keyframe, raw (str-reference.md §3).</summary>
    public readonly struct RoStrKey
    {
        /// <summary>The key's frame; <see cref="Type"/> 0 is a base key, 1 a delta key.</summary>
        public readonly int Frame, Type;
        /// <summary>Pixels, y down.</summary>
        public readonly Vector2 Position;
        /// <summary>GRF <c>UVs</c> pairs as stored: <c>Uv0</c> top-left and <c>Uv1</c> bottom-right of the uv rectangle;
        /// <c>Uv2</c>, <c>Uv3</c> repeat them in the data and are kept raw (str-reference.md §3, §6).</summary>
        public readonly Vector2 Uv0, Uv1, Uv2, Uv3;
        /// <summary>Corner i = (x[i], y[i]): 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left; pixels relative to
        /// <see cref="Position"/>, y down.</summary>
        public readonly Vector2 Xy0, Xy1, Xy2, Xy3;
        public readonly float AniFrame;
        public readonly int AniType;
        /// <summary>The file's delay, except that a <c>+inf</c> in the file reads as 0 through GRF (str-reference.md §3, O4).</summary>
        public readonly float Delay;
        /// <summary>Raw file units (1024 per turn, str-reference.md §3).</summary>
        public readonly float Angle;
        /// <summary>R, G, B, A raw: 0..255 in base keys, signed per-frame deltas in delta keys.</summary>
        public readonly Vector4 Color;
        /// <summary>Raw Direct3D blend codes and the mtpreset value.</summary>
        public readonly int SrcBlend, DstBlend, MtPreset;

        public RoStrKey(int frame, int type, Vector2 position,
                        Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3,
                        Vector2 xy0, Vector2 xy1, Vector2 xy2, Vector2 xy3,
                        float aniFrame, int aniType, float delay, float angle, Vector4 color,
                        int srcBlend, int dstBlend, int mtPreset)
        {
            Frame = frame; Type = type; Position = position;
            Uv0 = uv0; Uv1 = uv1; Uv2 = uv2; Uv3 = uv3;
            Xy0 = xy0; Xy1 = xy1; Xy2 = xy2; Xy3 = xy3;
            AniFrame = aniFrame; AniType = aniType; Delay = delay; Angle = angle; Color = color;
            SrcBlend = srcBlend; DstBlend = dstBlend; MtPreset = mtPreset;
        }
    }
}
