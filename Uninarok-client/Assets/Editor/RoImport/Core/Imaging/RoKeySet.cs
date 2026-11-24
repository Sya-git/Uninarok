using Uninarok.RoImport.Core.Effects;

namespace Uninarok.RoImport.Core.Imaging
{
    /// <summary>
    /// Which colors become transparent for one texture (PLAN-0012 4.3): the key (pink, black, both or none) and the
    /// per-channel tolerance of each. Pink is magenta (255, 0, 255); black is (0, 0, 0).
    /// </summary>
    public readonly struct RoKeySet
    {
        public readonly RoEffectKey Key;
        public readonly byte PinkTolerance;
        public readonly byte BlackTolerance;

        public RoKeySet(RoEffectKey key, byte pinkTolerance, byte blackTolerance)
        {
            Key = key;
            PinkTolerance = pinkTolerance;
            BlackTolerance = blackTolerance;
        }
    }
}
