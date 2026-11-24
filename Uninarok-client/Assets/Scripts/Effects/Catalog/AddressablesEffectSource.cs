using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Uninarok.Effects
{
    /// <summary>
    /// <see cref="IEffectAssetSource"/> over Addressables (SPEC-0012 FR-5, plan 4.4): an effect asset is loaded by its GUID,
    /// which is its Addressables key (as PLAN-0008). The source owns the handles it starts and releases them in
    /// <see cref="Release"/>. Main thread only.
    /// </summary>
    public sealed class AddressablesEffectSource : IEffectAssetSource
    {
        readonly Dictionary<string, AsyncOperationHandle<EffectAsset>> handles = new Dictionary<string, AsyncOperationHandle<EffectAsset>>(StringComparer.Ordinal);

        /// <summary>Handles still held (tests: 0 after every asset was released).</summary>
        public int HandleCount => handles.Count;

        public void Load(EffectAssetRef r, Action<EffectAsset> done)
        {
            string guid = r.Guid;
            if (string.IsNullOrEmpty(guid)) { done?.Invoke(null); return; }
            if (handles.ContainsKey(guid)) return;   // already loading or loaded: the catalog does not ask twice

            var handle = Addressables.LoadAssetAsync<EffectAsset>(guid);
            handles[guid] = handle;
            handle.Completed += h =>
            {
                if (!handles.TryGetValue(guid, out var current) || !current.Equals(h)) return;   // released meanwhile
                if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null)
                {
                    done?.Invoke(h.Result);
                    return;
                }
                handles.Remove(guid);
                if (h.IsValid()) Addressables.Release(h);
                done?.Invoke(null);
            };
        }

        public void Release(EffectAssetRef r)
        {
            if (string.IsNullOrEmpty(r.Guid) || !handles.TryGetValue(r.Guid, out var handle)) return;
            handles.Remove(r.Guid);
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}
