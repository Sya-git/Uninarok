using UnityEngine.Rendering;

namespace Uninarok.Effects
{
    /// <summary>
    /// Direct3D blend code (STR keys and authored assets) to Unity <see cref="BlendMode"/> (SPEC-0012 FR-3,
    /// plan 4.5). Table pinned by EffectBlendTests; documented in str-reference.md section 11.
    /// </summary>
    public static class EffectBlend
    {
        /// <summary>Unity blend factor for one Direct3D code; an unknown code gives SrcAlpha (the source half of the
        /// SrcAlpha / OneMinusSrcAlpha fallback pair).</summary>
        public static BlendMode ToUnity(byte code) => ToUnity(code, out _);

        /// <summary>As <see cref="ToUnity(byte)"/>; <paramref name="known"/> is false for codes outside 1..11,
        /// so the caller can log one warning per asset.</summary>
        public static BlendMode ToUnity(byte code, out bool known)
        {
            known = true;
            switch (code)
            {
                case 1: return BlendMode.Zero;
                case 2: return BlendMode.One;
                case 3: return BlendMode.SrcColor;
                case 4: return BlendMode.OneMinusSrcColor;
                case 5: return BlendMode.SrcAlpha;
                case 6: return BlendMode.OneMinusSrcAlpha;
                case 7: return BlendMode.DstAlpha;
                case 8: return BlendMode.OneMinusDstAlpha;
                case 9: return BlendMode.DstColor;
                case 10: return BlendMode.OneMinusDstColor;
                case 11: return BlendMode.SrcAlphaSaturate;
                default:
                    known = false;
                    return BlendMode.SrcAlpha;
            }
        }

        /// <summary>
        /// Factors for a source / destination code pair. If either code is unknown the whole pair becomes
        /// SrcAlpha / OneMinusSrcAlpha and <paramref name="known"/> is false (caller logs one warning per asset).
        /// </summary>
        public static void ToUnityPair(byte srcCode, byte dstCode, out BlendMode src, out BlendMode dst, out bool known)
        {
            src = ToUnity(srcCode, out bool srcKnown);
            dst = ToUnity(dstCode, out bool dstKnown);
            known = srcKnown && dstKnown;
            if (!known)
            {
                src = BlendMode.SrcAlpha;
                dst = BlendMode.OneMinusSrcAlpha;
            }
        }
    }
}
