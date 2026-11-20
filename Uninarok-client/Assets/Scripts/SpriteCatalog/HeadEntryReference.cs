using System;
using UnityEngine.AddressableAssets;

namespace Uninarok.Catalog
{
    /// <summary>A typed Addressables reference to a <see cref="HeadEntry"/> asset (PLAN-0008 4.4, 4.5, D3). Holding it loads nothing.</summary>
    [Serializable]
    public sealed class HeadEntryReference : AssetReferenceT<HeadEntry>
    {
        public HeadEntryReference(string guid) : base(guid) { }
    }
}
