using System;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog
{
    /// <summary>What a body variant is to its class (PLAN-0008 4.2, 4.4): the class body, a mount, an outfit or a costume.</summary>
    public enum BodyVariantKind : byte
    {
        Base,
        Mount,
        Outfit,
        Costume1,
        Costume2,
        Costume3,
        Costume4,
    }

    /// <summary>
    /// The bodies of one class (SPEC-0008 FR-2; PLAN-0008 4.4): every variant of its base job (mounts, outfits, costumes), per
    /// sex and race, with their palettes. Written by the importer, read-only at run time.
    /// </summary>
    public sealed class BodyEntry : ScriptableObject
    {
        public PlayerJob BaseJob;
        public BodyVariant[] Variants = Array.Empty<BodyVariant>();

        /// <summary>The import's numbers for this entry (PLAN-0008 4.7, D12): stored actions of its animations, distinct sheets.</summary>
        public int StoredActions;
        public int SheetCount;

        /// <summary>
        /// The variant of <paramref name="job"/> (mount, outfit) for <paramref name="sex"/>, else the base variant for that sex.
        /// When several variants carry the job (the novice's base body, its poring mount and its costume all are
        /// <c>JOB_NOVICE</c>), the one of the lowest kind wins: the base one.
        /// </summary>
        public bool TryGetVariant(PlayerJob job, AccountSex sex, out BodyVariant variant)
        {
            // A few dozen variants at most: a linear scan (D16).
            int own = -1, fallback = -1;
            var variants = Variants ?? Array.Empty<BodyVariant>();
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i].Sex != sex) continue;
                if (variants[i].Job == job)
                {
                    if (own < 0 || variants[i].Kind < variants[own].Kind) own = i;
                }
                else if (fallback < 0 && variants[i].Kind == BodyVariantKind.Base) fallback = i;
            }
            int chosen = own >= 0 ? own : fallback;
            variant = chosen >= 0 ? variants[chosen] : default;
            return chosen >= 0;
        }

        [Serializable]
        public struct BodyVariant
        {
            /// <summary>The client's body name (<c>기사</c>, <c>페코페코_기사</c>).</summary>
            public string Name;
            public PlayerJob Job;
            public SpriteRace Race;
            public BodyVariantKind Kind;
            public AccountSex Sex;
            public SpriteRef Sprite;
            public PaletteSet Palettes;
        }
    }
}
