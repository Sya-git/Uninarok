using System;
using System.Collections.Generic;
using UnityEngine;
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
    /// </summary>
    public sealed class SpriteCatalog
    {
        SpriteCatalog() { }

        /// <summary>What an actor of <paramref name="category"/> shows for an action its file lacks: units fall back, everything else draws nothing (FR-4, SPEC-0007 4.5).</summary>
        public static SpriteMissingAction MissingActionOf(SpriteCatalogCategory category) => throw new NotImplementedException("pending T3.4");

        public static AsyncOperationHandle<SpriteCatalog> LoadAsync(params SpriteCatalogCategory[] categories) => throw new NotImplementedException("pending T3.4");

        /// <summary>The facade over indices already in memory (tests, and <see cref="LoadAsync"/> once its assets are loaded).</summary>
        internal static SpriteCatalog Create(IEnumerable<ScriptableObject> indices, ItemSpriteTable items, ClassTable classes) =>
            throw new NotImplementedException("pending T3.4");

        /// <summary>Releases what <see cref="LoadAsync"/> loaded.</summary>
        public void Release() => throw new NotImplementedException("pending T3.4");

        public ClassTable Classes { get; private set; }

        public bool TryGetUnit(int jobId, out UnitRecord unit) => throw new NotImplementedException("pending T3.4");
        public bool TryGetGroundItem(int itemId, bool identified, out SpriteRef sprite) => throw new NotImplementedException("pending T3.4");
        public bool TryGetSkillIcon(int skillId, out SpriteRef icon) => throw new NotImplementedException("pending T3.4");
        public bool TryGetEmote(int emoteId, out SpriteRef emoteSet, out int action) => throw new NotImplementedException("pending T3.4");
        public bool TryGetEffect(string name, out SpriteRef effect) => throw new NotImplementedException("pending T3.4");

        public bool TryFindBody(PlayerJob job, out BodyEntryReference entry) => throw new NotImplementedException("pending T3.4");
        public bool TryFindHead(int style, out HeadEntryReference entry) => throw new NotImplementedException("pending T3.4");
        public bool TryFindEquipment(EquipmentKind kind, int viewId, int itemId, out EquipmentEntryReference entry) => throw new NotImplementedException("pending T3.4");
        public bool TryFindEquipmentByItem(int itemId, out EquipmentKind kind, out EquipmentEntryReference entry) => throw new NotImplementedException("pending T3.4");

        public AsyncOperationHandle<BodyEntry> LoadBody(BodyEntryReference entry) => throw new NotImplementedException("pending T3.4");
        public AsyncOperationHandle<HeadEntry> LoadHead(HeadEntryReference entry) => throw new NotImplementedException("pending T3.4");
        public AsyncOperationHandle<EquipmentEntry> LoadEquipment(EquipmentEntryReference entry) => throw new NotImplementedException("pending T3.4");
    }
}
