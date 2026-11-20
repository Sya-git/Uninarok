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
        public bool TryGetVariant(AccountSex sex, SpriteRace race, out HeadVariant variant) => throw new NotImplementedException("pending T3.3");

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
