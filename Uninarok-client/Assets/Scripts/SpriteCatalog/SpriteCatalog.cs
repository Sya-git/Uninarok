using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Uninarok.Networking.Data;
using Uninarok.Sprites;

namespace Uninarok.Catalog
{
    /// <summary>
    /// The lookup facade (SPEC-0008 FR-1 to FR-7; PLAN-0008 4.5, D16). <see cref="LoadAsync"/> loads the index assets of the
    /// chosen categories (and the item and class tables when needed) and builds one dictionary per index, once; every
    /// <c>TryGet…</c>/<c>TryFind…</c> is then a synchronous O(1) read with no allocation. A miss is false, never an
    /// exception. Sprites, animations and entries load through Addressables; whoever loads a handle owns and releases it.
    /// An instance holds its handles and no static state (PLAN-0008 5).
    /// </summary>
    public sealed class SpriteCatalog
    {
        readonly Dictionary<int, UnitRecord> _units = new Dictionary<int, UnitRecord>();
        readonly Dictionary<int, BodyEntryReference> _bodies = new Dictionary<int, BodyEntryReference>();
        readonly Dictionary<int, HeadEntryReference> _heads = new Dictionary<int, HeadEntryReference>();
        // By EquipmentKind value; index 0 (None) stays null.
        readonly Dictionary<int, EquipmentEntryReference>[] _byView = new Dictionary<int, EquipmentEntryReference>[5];
        readonly Dictionary<int, EquipmentEntryReference>[] _byItem = new Dictionary<int, EquipmentEntryReference>[5];
        readonly Dictionary<int, int> _items = new Dictionary<int, int>();
        readonly Dictionary<int, SpriteRef> _skillIcons = new Dictionary<int, SpriteRef>();
        readonly Dictionary<string, SpriteRef> _effects = new Dictionary<string, SpriteRef>(StringComparer.Ordinal);
        readonly Dictionary<int, int> _emoteActions = new Dictionary<int, int>();
        ItemSpriteTable _itemTable;
        GroundItemIndex _ground;
        SpriteRef _emoteSet;
        AsyncOperationHandle<IList<ScriptableObject>> _loaded;

        SpriteCatalog() { }

        public ClassTable Classes { get; private set; }

        /// <summary>What an actor of <paramref name="category"/> shows for an action its file lacks: units fall back, everything else draws nothing (FR-4, SPEC-0007 4.5).</summary>
        public static SpriteMissingAction MissingActionOf(SpriteCatalogCategory category) =>
            category == SpriteCatalogCategory.Monsters || category == SpriteCatalogCategory.Npcs || category == SpriteCatalogCategory.Homunculi
                ? SpriteMissingAction.Fallback
                : SpriteMissingAction.DrawNothing;

        /// <summary>
        /// Loads the index assets of <paramref name="categories"/> through Addressables (<see cref="SpriteCatalogKeys"/>), with
        /// the item table for equipment and ground items and the class table for bodies and equipment. A key with no asset is
        /// skipped (that category answers false); the operation fails only when none of the keys loads. The handle is the
        /// caller's; <see cref="Release"/> releases the assets it loaded.
        /// </summary>
        public static AsyncOperationHandle<SpriteCatalog> LoadAsync(params SpriteCatalogCategory[] categories)
        {
            var keys = KeysOf(categories ?? Array.Empty<SpriteCatalogCategory>());
            var load = Addressables.LoadAssetsAsync<ScriptableObject>(keys, null, Addressables.MergeMode.Union, true);
            return Addressables.ResourceManager.StartOperation(new LoadOperation(load), default);
        }

        static List<object> KeysOf(SpriteCatalogCategory[] categories)
        {
            var keys = new List<object>();
            bool items = false, classes = false;
            foreach (var c in categories)
            {
                if (c == SpriteCatalogCategory.Palettes) continue;
                string key = SpriteCatalogKeys.Index(c);
                if (!keys.Contains(key)) keys.Add(key);
                bool equipment = c == SpriteCatalogCategory.Headgears || c == SpriteCatalogCategory.Garments
                                 || c == SpriteCatalogCategory.Weapons || c == SpriteCatalogCategory.Shields;
                items |= equipment || c == SpriteCatalogCategory.GroundItems;
                classes |= equipment || c == SpriteCatalogCategory.Bodies;
            }
            if (items) keys.Add(SpriteCatalogKeys.Items());
            if (classes) keys.Add(SpriteCatalogKeys.Classes());
            return keys;
        }

        /// <summary>Waits for the index assets, then builds the facade; the facade owns the assets' handle.</summary>
        sealed class LoadOperation : AsyncOperationBase<SpriteCatalog>
        {
            readonly AsyncOperationHandle<IList<ScriptableObject>> _load;

            public LoadOperation(AsyncOperationHandle<IList<ScriptableObject>> load) => _load = load;

            protected override void Execute()
            {
                if (_load.IsDone) OnLoaded(_load);
                else _load.Completed += OnLoaded;
            }

            void OnLoaded(AsyncOperationHandle<IList<ScriptableObject>> h)
            {
                if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
                {
                    var error = h.OperationException ?? new InvalidOperationException("no catalog index could be loaded");
                    if (h.IsValid()) Addressables.Release(h);
                    Complete(null, false, error);
                    return;
                }
                ItemSpriteTable items = null;
                ClassTable classes = null;
                var indices = new List<ScriptableObject>(h.Result.Count);
                foreach (var asset in h.Result)
                {
                    if (asset is ItemSpriteTable t) items = items ?? t;
                    else if (asset is ClassTable c) classes = classes ?? c;
                    else if (asset != null) indices.Add(asset);
                }
                var catalog = Create(indices, items, classes);
                catalog._loaded = h;
                Complete(catalog, true, null);
            }
        }

        /// <summary>The facade over indices already in memory (tests, and <see cref="LoadAsync"/> once its assets are loaded).</summary>
        internal static SpriteCatalog Create(IEnumerable<ScriptableObject> indices, ItemSpriteTable items, ClassTable classes)
        {
            var c = new SpriteCatalog { Classes = classes };
            foreach (var asset in indices ?? Array.Empty<ScriptableObject>())
            {
                switch (asset)
                {
                    case UnitIndex u: c.AddUnits(u); break;
                    case BodyIndex b: Fill(c._bodies, b.Jobs, b.Entries, j => (int)j); break;
                    case HeadIndex h: Fill(c._heads, h.Styles, h.Entries, s => s); break;
                    case EquipmentIndex e: c.AddEquipment(e); break;
                    case GroundItemIndex g: c._ground = c._ground ?? g; break;
                    case SkillIconIndex s: Fill(c._skillIcons, s.SkillIds, s.Icons, id => id); break;
                    case EffectIndex f: c.AddEffects(f); break;
                }
            }
            if (items != null)
            {
                c._itemTable = items;
                int n = Math.Min(Len(items.ItemIds), Math.Min(Len(items.ViewIds), Len(items.Kinds)));
                for (int i = 0; i < n; i++) c._items.TryAdd(items.ItemIds[i], i);
            }
            return c;
        }

        static int Len<T>(T[] a) => a?.Length ?? 0;

        // The first index to hold a key wins, as in every merge here.
        static void Fill<TKey, TValue>(Dictionary<int, TValue> into, TKey[] keys, TValue[] values, Func<TKey, int> key)
        {
            int n = Math.Min(Len(keys), Len(values));
            for (int i = 0; i < n; i++) into.TryAdd(key(keys[i]), values[i]);
        }

        void AddUnits(UnitIndex u)
        {
            int n = Math.Min(Len(u.JobIds), Len(u.Records));
            for (int i = 0; i < n; i++) _units.TryAdd(u.JobIds[i], u.Records[i]);
        }

        void AddEquipment(EquipmentIndex e)
        {
            int kind = (int)e.Kind;
            if (kind <= 0 || kind >= _byView.Length) return;
            Fill(_byView[kind] = _byView[kind] ?? new Dictionary<int, EquipmentEntryReference>(), e.ViewIds, e.ByView, id => id);
            Fill(_byItem[kind] = _byItem[kind] ?? new Dictionary<int, EquipmentEntryReference>(), e.ItemIds, e.ByItem, id => id);
        }

        void AddEffects(EffectIndex f)
        {
            int n = Math.Min(Len(f.Names), Len(f.Effects));
            for (int i = 0; i < n; i++)
                if (f.Names[i] != null) _effects.TryAdd(f.Names[i], f.Effects[i]);
            if (_emoteSet.IsEmpty) _emoteSet = f.EmoteSet;
            Fill(_emoteActions, f.EmoteIds, f.EmoteActions, id => id);
        }

        /// <summary>Releases what <see cref="LoadAsync"/> loaded; the lookups then miss. A facade built in memory has nothing to release.</summary>
        public void Release()
        {
            if (_loaded.IsValid()) Addressables.Release(_loaded);
            _loaded = default;
            _units.Clear();
            _bodies.Clear();
            _heads.Clear();
            Array.Clear(_byView, 0, _byView.Length);
            Array.Clear(_byItem, 0, _byItem.Length);
            _items.Clear();
            _skillIcons.Clear();
            _effects.Clear();
            _emoteActions.Clear();
            _itemTable = null;
            _ground = null;
            _emoteSet = default;
            Classes = null;
        }

        // ---- single-sprite categories: the index holds the record ----

        public bool TryGetUnit(int jobId, out UnitRecord unit) => _units.TryGetValue(jobId, out unit);

        public bool TryGetGroundItem(int itemId, bool identified, out SpriteRef sprite)
        {
            if (_ground != null && _items.TryGetValue(itemId, out int i))
            {
                int[] slots = identified ? _itemTable.GroundSlot : _itemTable.UnidentifiedGroundSlot;
                int slot = slots != null && i < slots.Length ? slots[i] : -1;
                if (slot >= 0 && slot < Len(_ground.Resources))
                {
                    sprite = _ground.Resources[slot];
                    return true;
                }
            }
            sprite = default;
            return false;
        }

        public bool TryGetSkillIcon(int skillId, out SpriteRef icon) => _skillIcons.TryGetValue(skillId, out icon);

        /// <summary>The emote sprite and the action to play for <paramref name="emoteId"/> (non-directional; FR-6).</summary>
        public bool TryGetEmote(int emoteId, out SpriteRef emoteSet, out int action)
        {
            if (_emoteActions.TryGetValue(emoteId, out action) && !_emoteSet.IsEmpty)
            {
                emoteSet = _emoteSet;
                return true;
            }
            emoteSet = default;
            action = 0;
            return false;
        }

        public bool TryGetEffect(string name, out SpriteRef effect)
        {
            if (name != null) return _effects.TryGetValue(name, out effect);
            effect = default;
            return false;
        }

        // ---- entry categories: the index finds the entry's reference ----

        public bool TryFindBody(PlayerJob job, out BodyEntryReference entry) => _bodies.TryGetValue((int)job, out entry);

        public bool TryFindHead(int style, out HeadEntryReference entry) => _heads.TryGetValue(style, out entry);

        /// <summary>The record keyed by <paramref name="viewId"/>, else the one keyed by <paramref name="itemId"/> (D10; the base type is folded in at import, D9).</summary>
        public bool TryFindEquipment(EquipmentKind kind, int viewId, int itemId, out EquipmentEntryReference entry)
        {
            int k = (int)kind;
            if (k > 0 && k < _byView.Length)
            {
                if (_byView[k] != null && _byView[k].TryGetValue(viewId, out entry)) return true;
                if (itemId > 0 && _byItem[k] != null && _byItem[k].TryGetValue(itemId, out entry)) return true;
            }
            entry = null;
            return false;
        }

        /// <summary>The kind and view id of <paramref name="itemId"/> from the item table, then <see cref="TryFindEquipment"/> (FR-4).</summary>
        public bool TryFindEquipmentByItem(int itemId, out EquipmentKind kind, out EquipmentEntryReference entry)
        {
            if (_items.TryGetValue(itemId, out int i))
            {
                kind = (EquipmentKind)_itemTable.Kinds[i];
                if (kind != EquipmentKind.None) return TryFindEquipment(kind, _itemTable.ViewIds[i], itemId, out entry);
            }
            else kind = EquipmentKind.None;
            entry = null;
            return false;
        }

        // ---- loading an entry: the caller owns the handle ----

        public AsyncOperationHandle<BodyEntry> LoadBody(BodyEntryReference entry) => Addressables.LoadAssetAsync<BodyEntry>(KeyOf(entry));

        public AsyncOperationHandle<HeadEntry> LoadHead(HeadEntryReference entry) => Addressables.LoadAssetAsync<HeadEntry>(KeyOf(entry));

        public AsyncOperationHandle<EquipmentEntry> LoadEquipment(EquipmentEntryReference entry) => Addressables.LoadAssetAsync<EquipmentEntry>(KeyOf(entry));

        static object KeyOf(AssetReference reference) =>
            (reference ?? throw new ArgumentNullException(nameof(reference))).RuntimeKey;
    }
}
