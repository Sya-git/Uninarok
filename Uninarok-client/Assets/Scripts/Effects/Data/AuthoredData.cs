using System;
using UnityEngine;

namespace Uninarok.Effects
{
    // values no file holds, from the client data and leads (FR-4, FR-5); curves over t in [0, 1] of one cycle
    [Serializable]
    public sealed class AuthoredData
    {
        public EffectFacing Facing;                              // Quad: Camera or Ground; Cylinder: always Ground (object space)
        public int Texture = -1;                                 // index into EffectAsset.Textures; -1 or missing -> placeholder texture
        public byte SrcBlend = 5, DstBlend = 6;                  // D3D codes, the one EffectBlend table
        public Color Tint = Color.white; public float MaxAlpha = 1f;
        public AnimationCurve Alpha;                             // x MaxAlpha; null = constant 1
        public AnimationCurve Rotation;                          // degrees (quad: in its plane; cylinder: about the vertical axis); null = 0
        // Quad
        public float SizePixels = 35f;                           // STR pixels, -> units by EffectSettings.PixelsPerCell
        public AnimationCurve SizeScale;                         // null = 1
        public Vector3 StartMin, StartMax, EndMin, EndMax;       // offset box in cells (x, y on the ground, z up), drawn once per play
        public AnimationCurve Motion;                            // 0 = start offset, 1 = end offset; null = linear
        // Cylinder
        public float BottomRadius, TopRadius, Height;            // cells
        public int Sides = 20; public float RepeatU = 1f;        // lead defaults
        public AnimationCurve RadiusScale, HeightScale;          // x both radii, x height; null = 1
        // Procedural
        public string Procedure;                                 // name in EffectProcedures; null or unknown -> placeholder, one warning
    }
}
