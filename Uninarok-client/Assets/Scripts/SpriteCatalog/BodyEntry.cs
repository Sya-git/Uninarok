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
