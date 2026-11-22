using System;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;

namespace Uninarok.Units
{
    /// <summary>
    /// Where units get their loaded assets (PLAN-0009 4.2, D10; SPEC-0009 FR-14): Addressables in the game, the AssetDatabase
    /// in the editor. Assets are shared and reference-counted: every <see cref="Acquire{T}"/> needs one
    /// <see cref="Release"/> with the same reference. Main thread only.
    /// </summary>
    public interface IUnitAssetSource
    {
        SpriteCatalog Catalog { get; }
        CompositionTables Tables { get; }

        /// <summary>Calls <paramref name="done"/> with the asset, or null when it cannot be loaded; at once when it is already loaded.</summary>
        void Acquire<T>(AssetReference reference, Action<T> done) where T : UnityEngine.Object;

        void Release(AssetReference reference);
    }
}
