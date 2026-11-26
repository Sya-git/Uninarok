using System;
using UnityEngine;

namespace Uninarok.Effects
{
    public enum EffectKind : byte { Str, Sprite, Quad, Cylinder, Model, Procedural }
    public enum EffectFacing : byte { Camera, Ground }

    public sealed class EffectAsset : ScriptableObject
    {
        public string Path;                 // canonical catalog key
        public EffectKind Kind;
        public float Fps; public int FrameCount;                 // length = FrameCount / Fps seconds
        public LayerData[] Layers;
        public KeyData[] Keys;                                   // flat, by layer (STR)
        public string[] TextureNames;                            // flat, by layer, as the file names them
        public Texture2D[] Textures;                             // parallel to TextureNames; the placeholder where missing
        public AuthoredData Authored;                            // Quad / Cylinder / Procedural: our own authored data (FR-4)
        public float Length;                                     // authored kinds and Model: seconds of one cycle at speed 1
        public string SpriteName; public int SpriteAction;       // Sprite kind: SPEC-0008 name, action
        public GameObject Model;                                 // Model kind: SPEC-0003 prefab
        public string[] Doubtful;                                // "field: reason (lead / unverified)", authored only

        [Serializable] public struct LayerData { public int FirstKey, KeyCount, FirstTexture, TextureCount; }

        // what the file holds, raw; blend per key (FR-2)
        [Serializable] public struct KeyData
        {
            public int Frame; public byte Type, AniType, SrcBlend, DstBlend; public int MtPreset;
            public Vector2 Position, Uv0, Uv1, Uv2, Uv3, Xy0, Xy1, Xy2, Xy3;
            public float AniFrame, Delay, Angle; public Vector4 Color;
        }
    }
}
