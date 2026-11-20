using System;
using UnityEngine.AddressableAssets;

namespace Uninarok.Catalog
{
    /// <summary>A typed Addressables reference to a converted palette texture (PLAN-0008 4.4, D3). Holding it loads nothing.</summary>
    [Serializable]
    public sealed class PaletteReference : AssetReferenceTexture2D
    {
        public PaletteReference(string guid) : base(guid) { }
    }
}
