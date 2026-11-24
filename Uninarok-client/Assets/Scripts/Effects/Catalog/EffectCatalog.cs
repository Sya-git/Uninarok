using System;
using System.Collections.Generic;
using UnityEngine;

namespace Uninarok.Effects
{
    /// <summary>
    /// Key to asset lookup for effects (SPEC-0012 FR-5, FR-9, plan 4.4). A lookup tries the raw path normalised, then each
    /// <see cref="EffectPath.Prefixes"/> entry in order. The first raw string of each lookup is cached (ordinal), a miss
    /// included, so a repeated <see cref="TryResolve"/> allocates nothing and warns once. Main thread only.
    /// </summary>
    public sealed class EffectCatalog
    {
        readonly EffectCatalogIndex index;
        readonly IEffectAssetSource source;
        readonly Dictionary<string, int> keyToIndex;                          // canonical key -> index entry
        readonly Dictionary<string, int> lookupCache = new Dictionary<string, int>(StringComparer.Ordinal);   // raw -> entry or -1
        readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);
        readonly EffectAssetState[] states;
        readonly EffectAsset[] assets;

        EffectCatalog(EffectCatalogIndex index, IEffectAssetSource source)
        {
            this.index = index;
            this.source = source;
            int n = index != null && index.Keys != null ? index.Keys.Length : 0;
            keyToIndex = new Dictionary<string, int>(n, StringComparer.Ordinal);
            for (int i = 0; i < n; i++)
            {
                if (index.Keys[i] != null) keyToIndex.TryAdd(index.Keys[i], i);   // first wins
            }
            states = new EffectAssetState[n];
            assets = new EffectAsset[n];
        }

        /// <summary>Catalog over an index and an asset source (editor, tests; the game path is LoadAsync, T4.3).</summary>
        public static EffectCatalog Create(EffectCatalogIndex index, IEffectAssetSource source)
        {
            return new EffectCatalog(index, source);
        }

        public int Count => states.Length;

        /// <summary>Resolves a path in any client form; false and one warning per key per session when unknown.</summary>
        public bool TryResolve(string path, out EffectRef entry)
        {
            entry = default;
            if (string.IsNullOrEmpty(path)) { WarnOnce("<empty>", path); return false; }

            if (lookupCache.TryGetValue(path, out int cached))
            {
                if (cached < 0) return false;
                entry = new EffectRef(cached);
                return true;
            }

            int found = -1;
            if (EffectPath.TryNormalize(path, out string key))
            {
                string[] prefixes = EffectPath.Prefixes;
                for (int p = 0; p < prefixes.Length && found < 0; p++)
                {
                    string candidate = prefixes[p].Length == 0 ? key : prefixes[p] + key;
                    if (keyToIndex.TryGetValue(candidate, out int hit)) found = hit;
                }
            }
            else
            {
                key = path;
            }

            lookupCache[path] = found;
            if (found < 0)
            {
                WarnOnce(key, path);
                return false;
            }
            entry = new EffectRef(found);
            return true;
        }

        public EffectAssetState GetState(EffectRef entry)
        {
            return InRange(entry) ? states[entry.Index] : EffectAssetState.Failed;
        }

        /// <summary>The loaded asset of an entry; false while it is not loaded (or failed).</summary>
        public bool TryGetAsset(EffectRef entry, out EffectAsset asset)
        {
            asset = InRange(entry) ? assets[entry.Index] : null;
            return asset != null;
        }

        /// <summary>
        /// Starts loading the entry's asset once (later calls while Loading or Loaded do nothing). <paramref name="done"/>
        /// is called with the asset, or with null on failure; synchronously when the source completes synchronously.
        /// An entry whose source file is gone (SourceMissing) fails with one warning and no source call.
        /// </summary>
        public void RequestAsset(EffectRef entry, Action<EffectAsset> done = null)
        {
            if (!InRange(entry)) { done?.Invoke(null); return; }
            int i = entry.Index;
            switch (states[i])
            {
                case EffectAssetState.Loaded: done?.Invoke(assets[i]); return;
                case EffectAssetState.Failed: done?.Invoke(null); return;
                case EffectAssetState.Loading:
                    // Simplest form: one pending callback per call is not queued; the caller polls GetState/TryGetAsset.
                    return;
            }

            if (index.SourceMissing != null && i < index.SourceMissing.Length && index.SourceMissing[i])
            {
                states[i] = EffectAssetState.Failed;
                WarnOnce(index.Keys[i], index.Keys[i], "source file is missing");
                done?.Invoke(null);
                return;
            }

            states[i] = EffectAssetState.Loading;
            source.Load(index.Assets[i], asset =>
            {
                if (states[i] != EffectAssetState.Loading) return;   // released meanwhile
                assets[i] = asset;
                states[i] = asset != null ? EffectAssetState.Loaded : EffectAssetState.Failed;
                if (asset == null) WarnOnce(index.Keys[i], index.Keys[i], "asset failed to load");
                done?.Invoke(asset);
            });
        }

        /// <summary>Releases every loaded or loading asset through the source and clears the states.</summary>
        public void Release()
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == EffectAssetState.Loading || states[i] == EffectAssetState.Loaded)
                    source.Release(index.Assets[i]);
                states[i] = EffectAssetState.NotLoaded;
                assets[i] = null;
            }
        }

        bool InRange(EffectRef entry) => entry.Index >= 0 && entry.Index < states.Length;

        void WarnOnce(string key, string raw, string why = "unknown path")
        {
            if (!warned.Add(why + "|" + key)) return;
            Debug.LogWarning($"[Effects] Effect catalog: {why} '{raw}' (key '{key}'); the effect is skipped.");
        }
    }
}
