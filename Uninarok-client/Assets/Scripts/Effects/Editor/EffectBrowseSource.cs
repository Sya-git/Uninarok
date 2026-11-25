using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Uninarok.Effects.Editor
{
    /// <summary>
    /// The key list the preview window browses (SPEC-0012 FR-11, plan 4.11). It is the generated
    /// <see cref="EffectCatalogIndex"/> when the asset exists at <see cref="EffectCatalog.IndexKey"/>; until that index has
    /// been built (T6.9) it is an index made in memory by scanning every <see cref="EffectAsset"/> in the AssetDatabase,
    /// keyed by the asset's own <see cref="EffectAsset.Path"/>. Either way the window plays through an
    /// <see cref="EffectCatalog"/> over <see cref="AssetDatabaseEffectSource"/>.
    /// </summary>
    public sealed class EffectBrowseSource : IDisposable
    {
        readonly bool ownsIndex;

        EffectBrowseSource(EffectCatalogIndex index, bool fromIndex, bool ownsIndex)
        {
            Index = index;
            FromIndex = fromIndex;
            this.ownsIndex = ownsIndex;
        }

        public EffectCatalogIndex Index { get; }
        /// <summary>True when the generated index asset was used, false when the list comes from scanning the effect assets.</summary>
        public bool FromIndex { get; }
        public IReadOnlyList<string> Keys => Index.Keys ?? Array.Empty<string>();

        public bool IsSourceMissing(int keyIndex)
        {
            var missing = Index.SourceMissing;
            return missing != null && (uint)keyIndex < (uint)missing.Length && missing[keyIndex];
        }

        public EffectCatalog CreateCatalog() => EffectCatalog.Create(Index, new AssetDatabaseEffectSource());

        /// <summary>The generated index when present, else a scan of the effect assets.</summary>
        public static EffectBrowseSource Load()
        {
            var index = AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(EffectCatalog.IndexKey);
            if (index != null && index.Keys != null) return new EffectBrowseSource(index, true, false);
            return new EffectBrowseSource(Scan(), false, true);
        }

        /// <summary>An in-memory index of every <see cref="EffectAsset"/> in the project (keys sorted ordinal, none marked missing).</summary>
        public static EffectCatalogIndex Scan()
        {
            var pairs = new List<KeyValuePair<string, string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:EffectAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EffectAsset>(path);
                if (asset != null && !string.IsNullOrEmpty(asset.Path)) pairs.Add(new KeyValuePair<string, string>(asset.Path, guid));
            }
            return BuildIndex(pairs);
        }

        /// <summary>Sorted, de-duplicated (first wins) in-memory index over (key, guid) pairs; the caller owns the object.</summary>
        public static EffectCatalogIndex BuildIndex(List<KeyValuePair<string, string>> pairs)
        {
            pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var keys = new List<string>(pairs.Count);
            var assets = new List<EffectAssetRef>(pairs.Count);
            for (int i = 0; i < pairs.Count; i++)
            {
                if (keys.Count > 0 && keys[keys.Count - 1] == pairs[i].Key) continue;
                keys.Add(pairs[i].Key);
                assets.Add(new EffectAssetRef { Guid = pairs[i].Value });
            }
            var index = ScriptableObject.CreateInstance<EffectCatalogIndex>();
            index.hideFlags = HideFlags.HideAndDontSave;
            index.Keys = keys.ToArray();
            index.Assets = assets.ToArray();
            index.SourceMissing = new bool[keys.Count];
            return index;
        }

        public void Dispose()
        {
            if (ownsIndex && Index != null) Object.DestroyImmediate(Index);
        }
    }
}
