using System;

namespace Uninarok.Effects
{
    /// <summary>Stateless hash random of the instance seed, drawn once per play (SPEC-0012 plan D19). Stub: implemented in T2.4.</summary>
    public static class EffectRandom
    {
        public static float Range(uint seed, int channel, float min, float max)
            => throw new NotImplementedException("T2.4");
    }
}
