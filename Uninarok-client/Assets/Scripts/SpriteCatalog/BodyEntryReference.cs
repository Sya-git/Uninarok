using System;
using UnityEngine.AddressableAssets;

namespace Uninarok.Catalog
{
    /// <summary>A typed Addressables reference to a <see cref="BodyEntry"/> asset (PLAN-0008 4.4, 4.5, D3). Holding it loads nothing.</summary>
    [Serializable]
    public sealed class BodyEntryReference : AssetReferenceT<BodyEntry>
    {
        public BodyEntryReference(string guid) : base(guid) { }
    }
}
