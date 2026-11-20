using System;
using UnityEngine;
using Uninarok.Networking.Data;

namespace Uninarok.Catalog
{
    /// <summary>
    /// One headgear, garment, weapon or shield (SPEC-0008 FR-3, FR-4; PLAN-0008 4.4, D9, D16): its animation per class and
    /// sex, already paired with the right sheet at import. Keyed by view id, or by item id when the client names the file by
    /// item. Written by the importer, read-only at run time.
    /// </summary>
    public sealed class EquipmentEntry : ScriptableObject
    {
        public EquipmentKind Kind;
        /// <summary>-1 when keyed by item id only.</summary>
        public int ViewId = -1;
        /// <summary>0 when keyed by view id.</summary>
        public int ItemId;
        public string Name;
        public EquipmentSlot[] Slots = Array.Empty<EquipmentSlot>();
        /// <summary>Weapons only, parallel to <see cref="Slots"/>; empty for other kinds. A slot without a trail has an empty reference.</summary>
        public SpriteAnimationReference[] Trails = Array.Empty<SpriteAnimationReference>();

        [Serializable]
        public struct EquipmentSlot
        {
            /// <summary>The slot is for every class of <see cref="Race"/> and <see cref="Sex"/>; a flag, because <c>JOB_NOVICE</c> is 0.</summary>
            public bool AnyClass;
            public PlayerJob Job;
            public AccountSex Sex;
            public SpriteRace Race;
            /// <summary>The view id this slot stands in for, -1 when none (D9).</summary>
            public int FallbackOf;
            /// <summary>Every equipment sprite is animated, so no still reference is stored (D16).</summary>
            public SpriteAnimationReference Animation;
        }
    }
}
