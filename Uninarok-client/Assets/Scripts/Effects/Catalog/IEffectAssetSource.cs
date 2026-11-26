using System;

namespace Uninarok.Effects
{
    /// <summary>
    /// Where the catalog gets effect assets from (Addressables in the game, AssetDatabase in the editor, a fake in tests).
    /// SPEC-0012 plan 4.4, D8. <c>done</c> receives null when the asset could not be loaded.
    /// </summary>
    public interface IEffectAssetSource
    {
        void Load(EffectAssetRef r, Action<EffectAsset> done);
        void Release(EffectAssetRef r);
    }
}
