using System;
using UnityEngine.AddressableAssets;

namespace Uninarok.Catalog
{
    /// <summary>A typed Addressables reference to a <see cref="EquipmentEntry"/> asset (PLAN-0008 4.4, 4.5, D3). Holding it loads nothing.</summary>
    [Serializable]
    public sealed class EquipmentEntryReference : AssetReferenceT<EquipmentEntry>
    {
        public EquipmentEntryReference(string guid) : base(guid) { }
    }
}
