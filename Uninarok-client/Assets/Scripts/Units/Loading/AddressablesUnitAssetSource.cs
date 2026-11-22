using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Uninarok.Catalog;
using Object = UnityEngine.Object;

namespace Uninarok.Units
{
    /// <summary>
    /// The game's asset source (PLAN-0009 4.2; SPEC-0009 FR-14): one Addressables handle per asset with a count, so 300 units
    /// share each animation and palette once. An instance owned by the scene's bootstrap, not static (§5); the caller keeps
    /// the catalog's and tables' handles through <see cref="LoadAsync"/> and frees them with <see cref="Dispose"/>.
    /// </summary>
    public sealed class AddressablesUnitAssetSource : IUnitAssetSource, IDisposable
    {
        sealed class Slot
        {
            public AsyncOperationHandle Handle;
            public int Count;
        }

        readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        AsyncOperationHandle<SpriteCatalog> _catalogHandle;
        AsyncOperationHandle<CompositionTables> _tablesHandle;
        CompositionTables _ownTables;
        bool _disposed;

        public AddressablesUnitAssetSource(SpriteCatalog catalog, CompositionTables tables)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Tables = tables;
        }

        public SpriteCatalog Catalog { get; }
        public CompositionTables Tables { get; private set; }

        /// <summary>The catalog categories units need.</summary>
        static SpriteCatalogCategory[] Categories => new[]
        {
            SpriteCatalogCategory.Monsters, SpriteCatalogCategory.Npcs, SpriteCatalogCategory.Homunculi, SpriteCatalogCategory.Bodies, SpriteCatalogCategory.Heads,
            SpriteCatalogCategory.Headgears, SpriteCatalogCategory.Garments, SpriteCatalogCategory.Weapons, SpriteCatalogCategory.Shields,
        };

        /// <summary>
        /// Loads the catalog and the composition tables, then calls <paramref name="done"/> with the source, or with null and a
        /// reason when there is no catalog. Missing tables are one warning: the composer's defaults apply.
        /// </summary>
        public static void LoadAsync(Action<AddressablesUnitAssetSource, string> done)
        {
            var catalog = SpriteCatalog.LoadAsync(Categories);
            catalog.Completed += c =>
            {
                if (c.Status != AsyncOperationStatus.Succeeded)
                {
                    string reason = c.OperationException?.Message ?? "the catalog could not be loaded";
                    Addressables.Release(c);
                    done(null, reason);
                    return;
                }
                var tables = Addressables.LoadAssetAsync<CompositionTables>(CompositionTablesKey.Address);
                tables.Completed += t =>
                {
                    bool ok = t.Status == AsyncOperationStatus.Succeeded && t.Result != null;
                    var source = new AddressablesUnitAssetSource(c.Result, ok ? t.Result : null) { _catalogHandle = c };
                    if (ok) source._tablesHandle = t;
                    else
                    {
                        if (t.IsValid()) Addressables.Release(t);
                        source.Tables = source._ownTables = ScriptableObject.CreateInstance<CompositionTables>();
                        Debug.LogWarning("[Units] no composition tables (" + CompositionTablesKey.Address + "): run Tools/Ragnarok/Catalog/Composition; default rules are used");
                    }
                    done(source, null);
                };
            };
        }

        /// <summary>References held in total: 0 when everything acquired was released.</summary>
        public int HeldCount
        {
            get
            {
                int n = 0;
                foreach (var s in _slots.Values) n += s.Count;
                return n;
            }
        }

        /// <summary>Assets with an open handle.</summary>
        public int OpenHandles => _slots.Count;

        public void Acquire<T>(AssetReference reference, Action<T> done) where T : Object
        {
            string key = Key(reference);
            if (!_slots.TryGetValue(key, out var slot))
            {
                slot = new Slot { Handle = Addressables.LoadAssetAsync<T>(reference.RuntimeKey) };
                _slots.Add(key, slot);
            }
            slot.Count++;
            var handle = slot.Handle;
            if (handle.IsDone) done(handle.Status == AsyncOperationStatus.Succeeded ? handle.Result as T : null);
            else handle.Completed += h => done(!_disposed && h.IsValid() && h.Status == AsyncOperationStatus.Succeeded ? h.Result as T : null);
        }

        public void Release(AssetReference reference)
        {
            if (_disposed) return;
            string key = Key(reference);
            if (!_slots.TryGetValue(key, out var slot)) throw new InvalidOperationException($"released more often than acquired: {key}");
            if (--slot.Count > 0) return;
            _slots.Remove(key);
            if (slot.Handle.IsValid()) Addressables.Release(slot.Handle);
        }

        /// <summary>Releases everything, the catalog included.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var slot in _slots.Values)
                if (slot.Handle.IsValid()) Addressables.Release(slot.Handle);
            _slots.Clear();
            Catalog.Release();
            if (_catalogHandle.IsValid()) Addressables.Release(_catalogHandle);
            if (_tablesHandle.IsValid()) Addressables.Release(_tablesHandle);
            if (_ownTables != null) Object.Destroy(_ownTables);
        }

        static string Key(AssetReference reference) => reference.RuntimeKey.ToString();
    }
}
