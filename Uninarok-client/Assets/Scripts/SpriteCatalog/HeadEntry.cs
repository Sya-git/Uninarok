using System;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog
{
    /// <summary>
    /// One head style (SPEC-0008 FR-2; PLAN-0008 4.4): its sprite and hair palettes per sex and race. Written by the importer,
    /// read-only at run time.
    /// </summary>
    public sealed class HeadEntry : ScriptableObject
    {
        public int Style;
        public HeadVariant[] Variants = Array.Empty<HeadVariant>();

        /// <summary>The variant for exactly (<paramref name="sex"/>, <paramref name="race"/>); false when the style has none.</summary>
        public bool TryGetVariant(AccountSex sex, SpriteRace race, out HeadVariant variant)
        {
            var variants = Variants ?? Array.Empty<HeadVariant>();
            for (int i = 0; i < variants.Length; i++)
            {
                if (variants[i].Sex != sex || variants[i].Race != race) continue;
                variant = variants[i];
                return true;
            }
            variant = default;
            return false;
        }

        [Serializable]
        public struct HeadVariant
        {
            public AccountSex Sex;
            public SpriteRace Race;
            public SpriteRef Sprite;
            public PaletteSet Palettes;
        }
    }
}
