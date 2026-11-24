using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Uninarok.Effects
{
    /// <summary>
    /// One material per (texture, src code, dst code, facing, wrap), shared by STR layers and authored parts; created on
    /// first use and kept, so a repeated <see cref="Get(Texture2D, byte, byte, EffectFacing, bool)"/> allocates nothing
    /// (SPEC-0012 FR-3, FR-9; plan 4.5). Main thread only (creates UnityEngine.Material).
    /// </summary>
    public sealed class EffectMaterials : IDisposable
    {
        readonly struct Key : IEquatable<Key>
        {
            readonly EntityId texture;
            readonly byte src, dst, facing, wrap;

            public Key(EntityId texture, byte src, byte dst, EffectFacing facing, bool repeatWrap)
            {
                this.texture = texture; this.src = src; this.dst = dst; this.facing = (byte)facing; wrap = repeatWrap ? (byte)1 : (byte)0;
            }

            public bool Equals(Key o) => texture == o.texture && src == o.src && dst == o.dst && facing == o.facing && wrap == o.wrap;
            public override bool Equals(object obj) => obj is Key k && Equals(k);
            public override int GetHashCode() => HashCode.Combine(texture, src, dst, facing, wrap);
        }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        static readonly int DepthBiasId = Shader.PropertyToID("_DepthBias");

        readonly EffectSettings settings;
        readonly Dictionary<Key, Material> materials = new Dictionary<Key, Material>();

        public EffectMaterials(EffectSettings settings) => this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

        /// <summary>Number of materials created so far.</summary>
        public int Count => materials.Count;

        public Material Get(Texture2D texture, byte srcCode, byte dstCode, EffectFacing facing, bool repeatWrap)
            => Get(texture, srcCode, dstCode, facing, repeatWrap, out _);

        /// <summary>
        /// A null texture uses <see cref="EffectSettings.PlaceholderTexture"/>. <paramref name="blendKnown"/> is false when
        /// either code is outside 1..11 (the material then uses SrcAlpha / OneMinusSrcAlpha); the caller logs one warning per asset.
        /// </summary>
        public Material Get(Texture2D texture, byte srcCode, byte dstCode, EffectFacing facing, bool repeatWrap, out bool blendKnown)
        {
            if (texture == null) texture = settings.PlaceholderTexture;
            var key = new Key(texture != null ? texture.GetEntityId() : default, srcCode, dstCode, facing, repeatWrap);
            if (materials.TryGetValue(key, out var material) && material != null)
            {
                blendKnown = KnownPair(srcCode, dstCode);
                return material;
            }

            EffectBlend.ToUnityPair(srcCode, dstCode, out var src, out var dst, out blendKnown);
            material = new Material(settings.Shader)
            {
                name = "EffectMaterial",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent,
            };
            material.SetTexture(BaseMapId, texture);
            material.SetFloat(SrcBlendId, (float)src);
            material.SetFloat(DstBlendId, (float)dst);
            material.SetFloat(DepthBiasId, settings.DepthBias);
            if (facing == EffectFacing.Camera) material.EnableKeyword("_FACING_CAMERA");
            if (repeatWrap) material.EnableKeyword("_UV_REPEAT");
            materials[key] = material;
            return material;
        }

        static bool KnownPair(byte srcCode, byte dstCode) => srcCode >= 1 && srcCode <= 11 && dstCode >= 1 && dstCode <= 11;

        /// <summary>Destroys every created material.</summary>
        public void Clear()
        {
            foreach (var material in materials.Values)
                if (material != null) DestroyMaterial(material);
            materials.Clear();
        }

        public void Dispose() => Clear();

        static void DestroyMaterial(Material material)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(material);
            else UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
