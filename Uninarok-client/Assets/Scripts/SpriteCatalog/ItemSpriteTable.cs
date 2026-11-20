using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>
    /// What each item draws (SPEC-0008 FR-4, FR-5; PLAN-0008 4.5), from the item table of the chosen language. Parallel
    /// arrays sorted by item id: the view id (<c>ClassNum</c>) and its kind, and the ground sprite of the identified and the
    /// unidentified item as positions in <see cref="GroundItemIndex.Resources"/> (-1 none).
    /// </summary>
    public sealed class ItemSpriteTable : ScriptableObject
    {
        public int[] ItemIds = Array.Empty<int>();
        public int[] ViewIds = Array.Empty<int>();
        /// <summary><see cref="EquipmentKind"/> values; <see cref="EquipmentKind.None"/> for an item that draws no equipment.</summary>
        public byte[] Kinds = Array.Empty<byte>();
        public int[] GroundSlot = Array.Empty<int>();
        public int[] UnidentifiedGroundSlot = Array.Empty<int>();
    }
}
