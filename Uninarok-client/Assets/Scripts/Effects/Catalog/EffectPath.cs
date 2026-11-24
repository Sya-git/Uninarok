using System;

namespace Uninarok.Effects
{
    /// <summary>
    /// Runtime path normalisation for effect keys (SPEC-0012 FR-5, plan 4.4, D21). Stub until T1.7.
    /// </summary>
    public static class EffectPath
    {
        /// <summary>Relative forms the client data uses, tried in order by the catalog.</summary>
        public static readonly string[] Prefixes = { "", "texture/effect/", "texture/", "sprite/" };

        /// <summary>Canonical key (relative to the effect data root); false for empty, "." / ".." or drive-letter input.</summary>
        public static bool TryNormalize(string raw, out string key)
        {
            throw new NotImplementedException("pending T1.7");
        }
    }
}
