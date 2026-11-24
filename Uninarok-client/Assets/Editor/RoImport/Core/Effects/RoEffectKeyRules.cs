using System;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Effects
{
    /// <summary>Which color an effect texture's background is keyed out by (PLAN-0012 4.3).</summary>
    public enum RoEffectKey : byte { None, Pink, Black, PinkAndBlack }

    /// <summary>
    /// The colour key rules of the effect textures (PLAN-0012 4.3, D6): defaults by source kind, tolerances and per-texture
    /// overrides. Built in code (tests) or loaded from <c>Rules/effect-textures.json</c>. STUB (PLAN-0012 T3.6): T3.7 implements it.
    /// </summary>
    public sealed class RoEffectKeyRules
    {
        /// <summary>BMP, 24-bit TGA, JPG.</summary>
        public RoEffectKey DefaultOpaque;

        /// <summary>32-bit TGA, PNG with alpha.</summary>
        public RoEffectKey DefaultWithAlpha;

        /// <summary>Per channel.</summary>
        public byte PinkTolerance = 10, BlackTolerance;

        /// <summary>Per texture; beats the defaults.</summary>
        public (RoPath Path, RoEffectKey Key)[] Overrides;

        /// <summary>The key of one texture: its override, else the default for its source kind.</summary>
        public RoEffectKey For(RoPath texture, bool sourceHadAlpha) => throw new NotImplementedException("T3.7");

        /// <summary>The key and tolerances of one texture, ready for <see cref="RoColorKey.Apply(RoImage, RoKeySet)"/>.</summary>
        public RoKeySet KeySetFor(RoPath texture, bool sourceHadAlpha) => throw new NotImplementedException("T3.7");

        /// <summary>Reads rules from JSON text (the shape of <c>effect-textures.json</c>).</summary>
        public static RoEffectKeyRules FromJson(string json) => throw new NotImplementedException("T3.7");
    }
}
