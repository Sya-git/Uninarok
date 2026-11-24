using System;
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
        public static RoCompiledStr Compile(RoStr str) => throw new NotImplementedException();
    }
}
