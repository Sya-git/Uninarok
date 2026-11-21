using System;
using UnityEngine;

namespace Uninarok.Catalog
{
    /// <summary>
    /// The entries of one equipment kind (SPEC-0008 FR-3, FR-4; PLAN-0008 4.5, D10): records keyed by view id, and records
    /// keyed by item id (weapon and shield files named by item). Both sorted.
    /// </summary>
    public sealed class EquipmentIndex : ScriptableObject
    {
        public EquipmentKind Kind;
        public int[] ViewIds = Array.Empty<int>();
        public EquipmentEntryReference[] ByView = Array.Empty<EquipmentEntryReference>();
        public int[] ItemIds = Array.Empty<int>();
        public EquipmentEntryReference[] ByItem = Array.Empty<EquipmentEntryReference>();
    }
}
