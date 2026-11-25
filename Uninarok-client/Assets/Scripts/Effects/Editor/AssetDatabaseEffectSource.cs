using System;
using UnityEditor;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// <see cref="IEffectAssetSource"/> over the AssetDatabase (SPEC-0012 FR-11, plan 4.4, D8): the editor's preview and the
    /// tests load a generated effect asset by its GUID with no Addressables and no play mode. Loads complete inside
    /// <see cref="Load"/>; nothing is held, so <see cref="Release"/> does nothing.
    /// </summary>
    public sealed class AssetDatabaseEffectSource : IEffectAssetSource
    {
        public void Load(EffectAssetRef r, Action<EffectAsset> done)
        {
            EffectAsset asset = null;
            if (!string.IsNullOrEmpty(r.Guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(r.Guid);
                if (!string.IsNullOrEmpty(path)) asset = AssetDatabase.LoadAssetAtPath<EffectAsset>(path);
            }
            done?.Invoke(asset);
        }

        public void Release(EffectAssetRef r) { }
    }
}
