using System;
using System.Collections.Generic;
using Uninarok.Effects;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>The flat runtime form of one STR (PLAN-0012 4.2): what <see cref="EffectAsset"/> holds for the Str kind.</summary>
    public sealed class RoCompiledStr
    {
        public float Fps;
        public int FrameCount;
        public EffectAsset.LayerData[] Layers;
        /// <summary>Flat, by layer.</summary>
        public EffectAsset.KeyData[] Keys;
        /// <summary>Flat, by layer, as the file names them.</summary>
        public string[] TextureNames;
    }

    /// <summary>
    /// Flattens a <see cref="RoStr"/> into the runtime arrays: every key kept raw, stable-sorted by frame inside its layer,
    /// texture names kept (PLAN-0012 4.2, SPEC-0012 FR-2). No value is converted. Pure.
    /// </summary>
    public static class RoStrCompiler
    {
        public static RoCompiledStr Compile(RoStr str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            var layers = new List<EffectAsset.LayerData>();
            var keys = new List<EffectAsset.KeyData>();
            var names = new List<string>();
            foreach (RoStrLayer layer in str.Layers)
            {
                var layerData = new EffectAsset.LayerData
                {
                    FirstKey = keys.Count, KeyCount = layer.Keys.Count,
                    FirstTexture = names.Count, TextureCount = layer.TextureNames.Count,
                };
                // stable: a key moves left only past strictly greater frames
                var sorted = new List<RoStrKey>(layer.Keys.Count);
                foreach (RoStrKey k in layer.Keys)
                {
                    int i = sorted.Count;
                    while (i > 0 && sorted[i - 1].Frame > k.Frame) i--;
                    sorted.Insert(i, k);
                }
                foreach (RoStrKey k in sorted)
                    keys.Add(new EffectAsset.KeyData
                    {
                        Frame = k.Frame, Type = (byte)k.Type, AniType = (byte)k.AniType,
                        SrcBlend = (byte)k.SrcBlend, DstBlend = (byte)k.DstBlend, MtPreset = k.MtPreset,
                        Position = k.Position, Uv0 = k.Uv0, Uv1 = k.Uv1, Uv2 = k.Uv2, Uv3 = k.Uv3,
                        Xy0 = k.Xy0, Xy1 = k.Xy1, Xy2 = k.Xy2, Xy3 = k.Xy3,
                        AniFrame = k.AniFrame, Delay = k.Delay, Angle = k.Angle, Color = k.Color,
                    });
                foreach (string n in layer.TextureNames) names.Add(n);
                layers.Add(layerData);
            }
            return new RoCompiledStr
            {
                Fps = str.Fps, FrameCount = str.FrameCount,
                Layers = layers.ToArray(), Keys = keys.ToArray(), TextureNames = names.ToArray(),
            };
        }
    }
}
