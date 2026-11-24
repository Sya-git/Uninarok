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
        public int Texture;
        public byte Src, Dst;
    }

    /// <summary>STR sampling (plan 4.2, D17); pure, no allocation. Stub until T2.2.</summary>
    public static class EffectSampler
    {
        /// <summary>False = the layer is hidden at this frame. <paramref name="cursor"/> caches the base key index.</summary>
        public static bool Sample(EffectAsset asset, int layer, float frame, ref int cursor, out QuadSample q)
            => throw new NotImplementedException("T2.2");

        /// <summary>FrameCount / Fps seconds.</summary>
        public static float Length(EffectAsset asset) => throw new NotImplementedException("T2.2");
    }
}
