namespace Uninarok.Effects
{
    /// <summary>Stateless hash random of the instance seed, drawn once per play (SPEC-0012 plan D19, FR-7, FR-9). No state, no allocation.</summary>
    public static class EffectRandom
    {
        /// <summary>A value in [min, max] from the seed and a channel; the same arguments always give the same value. min == max gives min exactly.</summary>
        public static float Range(uint seed, int channel, float min, float max)
        {
            // 32-bit finalizer (murmur3 fmix) over the seed mixed with the channel.
            uint h = seed ^ ((uint)channel * 0x9E3779B9u);
            h ^= h >> 16; h *= 0x85EBCA6Bu;
            h ^= h >> 13; h *= 0xC2B2AE35u;
            h ^= h >> 16;
            float u = (h >> 8) * (1f / 16777216f);   // 24 bits: [0, 1)
            float v = min + (max - min) * u;
            return v > max ? max : v;
        }
    }
}
