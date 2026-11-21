using System;
using UnityEngine.AddressableAssets;
using Uninarok.Sprites;

namespace Uninarok.Catalog
{
    /// <summary>A typed Addressables reference to a converted ACT (PLAN-0008 4.4, D3). Holding it loads nothing.</summary>
    [Serializable]
    public sealed class SpriteAnimationReference : AssetReferenceT<SpriteAnimation>
    {
        public SpriteAnimationReference(string guid) : base(guid) { }
    }
}
