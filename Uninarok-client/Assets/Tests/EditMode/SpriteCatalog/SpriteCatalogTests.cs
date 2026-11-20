using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.Sprites;
using static Uninarok.Catalog.Tests.CatalogFixtures;
using Object = UnityEngine.Object;
using UnityConstraints = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Uninarok.Catalog.Tests
{
    // TASKS-0008 T3.2 (FR-1 to FR-7; PLAN-0008 4.5, D10, D16): the facade over in-memory indices, no Addressables load.
    public class SpriteCatalogTests
    {
        readonly List<Object> _created = new List<Object>();
        SpriteCatalog _catalog;
        ClassTable _classes;

        [SetUp]
        public void BuildCatalog()
        {
            var monsters = Make<UnitIndex>(_created);
            monsters.Category = SpriteCatalogCategory.Monsters;
            monsters.JobIds = new[] { 1002, 2245 };
            monsters.Records = new[]
            {
                new UnitRecord { JobId = 1002, Name = "Poring", Sprite = AnimationRef(1002), Scale = 1f },
                new UnitRecord { JobId = 2245, Name = "8W_SOLDIER", Sprite = AnimationRef(2245), Scale = 1.5f },
            };
            var npcs = Make<UnitIndex>(_created);
            npcs.Category = SpriteCatalogCategory.Npcs;
            npcs.JobIds = new[] { 105 };
            npcs.Records = new[] { new UnitRecord { JobId = 105, Name = "8W_SOLDIER", Sprite = AnimationRef(105), Scale = 1f } };

            var bodies = Make<BodyIndex>(_created);
            bodies.Jobs = new[] { PlayerJob.JOB_KNIGHT, PlayerJob.JOB_KNIGHT2 };
            bodies.Entries = new[] { new BodyEntryReference(Guid(7)), new BodyEntryReference(Guid(7)) };

            var heads = Make<HeadIndex>(_created);
            heads.Styles = new[] { 1, 2 };
            heads.Entries = new[] { new HeadEntryReference(Guid(101)), new HeadEntryReference(Guid(102)) };

            var weapons = Make<EquipmentIndex>(_created);
            weapons.Kind = EquipmentKind.Weapon;
            weapons.ViewIds = new[] { 1 };
            weapons.ByView = new[] { new EquipmentEntryReference(Guid(3001)) };
            weapons.ItemIds = new[] { 1117 };
            weapons.ByItem = new[] { new EquipmentEntryReference(Guid(31117)) };

            var headgears = Make<EquipmentIndex>(_created);
            headgears.Kind = EquipmentKind.Headgear;
            headgears.ViewIds = new[] { 16 };
            headgears.ByView = new[] { new EquipmentEntryReference(Guid(1016)) };

            var ground = Make<GroundItemIndex>(_created);
            ground.ResourceNames = new[] { "단검", "검", "빨간포션" };
            ground.Resources = new[] { StillRef(50), StillRef(51), StillRef(52) };

            var skills = Make<SkillIconIndex>(_created);
            skills.SkillIds = new[] { 5 };
            skills.Icons = new[] { StillRef(605) };

            var effects = Make<EffectIndex>(_created);
            effects.Names = new[] { "cart", "torch" };
            effects.Effects = new[] { AnimationRef(801), AnimationRef(802) };
            effects.EmoteSet = AnimationRef(900);
            effects.EmoteIds = new[] { 0, 1 };
            effects.EmoteActions = new[] { 0, 1 };

            var items = Make<ItemSpriteTable>(_created);
            items.ItemIds = new[] { 501, 1117, 1201, 2220 };
            items.ViewIds = new[] { 0, 1, 1, 16 };
            items.Kinds = new[] { (byte)EquipmentKind.None, (byte)EquipmentKind.Weapon, (byte)EquipmentKind.Weapon, (byte)EquipmentKind.Headgear };
            items.GroundSlot = new[] { 2, 1, 0, -1 };
            items.UnidentifiedGroundSlot = new[] { 2, 1, 1, -1 };

            _classes = Classes(_created, (PlayerJob.JOB_KNIGHT, SpriteRace.Human, PlayerJob.JOB_KNIGHT));

            _catalog = SpriteCatalog.Create(new ScriptableObject[] { monsters, npcs, bodies, heads, weapons, headgears, ground, skills, effects }, items, _classes);
        }

        [TearDown]
        public void Destroy() => DestroyAll(_created);

        // ---- units (FR-1) ----

        [Test]
        public void AUnitIsFoundInWhicheverUnitIndexHoldsIt()
        {
            Assert.That(_catalog.TryGetUnit(1002, out var poring), Is.True);
            Assert.That(poring.Name, Is.EqualTo("Poring"));
            Assert.That(_catalog.TryGetUnit(105, out var npc) ? npc.Sprite.Animation.AssetGUID : null, Is.EqualTo(Guid(105)));
            Assert.That(_catalog.TryGetUnit(2245, out var monster) ? monster.Scale : 0f, Is.EqualTo(1.5f));
        }

        [Test]
        public void UnitsFallBack_EverythingElseDrawsNothing()
        {
            foreach (SpriteCatalogCategory c in Enum.GetValues(typeof(SpriteCatalogCategory)))
            {
                bool unit = c == SpriteCatalogCategory.Monsters || c == SpriteCatalogCategory.Npcs || c == SpriteCatalogCategory.Homunculi;
                Assert.That(SpriteCatalog.MissingActionOf(c), Is.EqualTo(unit ? SpriteMissingAction.Fallback : SpriteMissingAction.DrawNothing), c.ToString());
            }
        }

        // ---- equipment (FR-3, FR-4) ----

        [Test]
        public void AnItemId_GivesItsKindAndViewIdsEntry()
        {
            Assert.That(_catalog.TryFindEquipmentByItem(1201, out var kind, out var knife), Is.True, "Faca, ClassNum 1");
            Assert.That(kind, Is.EqualTo(EquipmentKind.Weapon));
            Assert.That(knife.AssetGUID, Is.EqualTo(Guid(3001)));
            Assert.That(_catalog.TryFindEquipmentByItem(2220, out var hatKind, out var hat) ? hat.AssetGUID : null, Is.EqualTo(Guid(1016)));
            Assert.That(hatKind, Is.EqualTo(EquipmentKind.Headgear));
        }

        [Test]
        public void TheViewIdRecordComesFirst_ThenTheItemIdRecord()
        {
            Assert.That(_catalog.TryFindEquipment(EquipmentKind.Weapon, 1, 1117, out var byView) ? byView.AssetGUID : null, Is.EqualTo(Guid(3001)), "D10");
            Assert.That(_catalog.TryFindEquipment(EquipmentKind.Weapon, 99, 1117, out var byItem) ? byItem.AssetGUID : null, Is.EqualTo(Guid(31117)));
            Assert.That(_catalog.TryFindEquipmentByItem(1117, out _, out var own) ? own.AssetGUID : null, Is.EqualTo(Guid(3001)),
                "item 1117 has view id 1, which has a record");
        }

        [Test]
        public void AnItemThatDrawsNoEquipment_AndAKindWithNoIndex_AreMisses()
        {
            Assert.That(_catalog.TryFindEquipmentByItem(501, out var kind, out var entry), Is.False);
            Assert.That(kind, Is.EqualTo(EquipmentKind.None));
            Assert.That(entry, Is.Null);
            Assert.That(_catalog.TryFindEquipment(EquipmentKind.Shield, 1, 2189, out _), Is.False);
            Assert.That(_catalog.TryFindEquipment(EquipmentKind.Weapon, 99, 99, out _), Is.False);
            Assert.That(_catalog.TryFindEquipmentByItem(424242, out _, out _), Is.False);
        }

        // ---- bodies and heads (FR-2) ----

        [Test]
        public void BodiesAndHeads_AreFoundByJobAndStyle()
        {
            Assert.That(_catalog.TryFindBody(PlayerJob.JOB_KNIGHT2, out var peco) ? peco.AssetGUID : null, Is.EqualTo(Guid(7)), "a mount job finds its class entry");
            Assert.That(_catalog.TryFindBody(PlayerJob.JOB_NOVICE, out _), Is.False);
            Assert.That(_catalog.TryFindHead(2, out var head) ? head.AssetGUID : null, Is.EqualTo(Guid(102)));
            Assert.That(_catalog.TryFindHead(30, out _), Is.False);
            Assert.That(_catalog.Classes, Is.SameAs(_classes));
        }

        // ---- ground items, skill icons, effects, emotes (FR-5, FR-6, FR-7) ----

        [Test]
        public void AGroundItemIsItsResourcesSlot()
        {
            Assert.That(_catalog.TryGetGroundItem(501, true, out var potion) ? potion.Still.AssetGUID : null, Is.EqualTo(Guid(52)), "빨간포션");
            Assert.That(_catalog.TryGetGroundItem(1201, true, out var knife) ? knife.Still.AssetGUID : null, Is.EqualTo(Guid(50)));
            Assert.That(_catalog.TryGetGroundItem(1201, false, out var unknown) ? unknown.Still.AssetGUID : null, Is.EqualTo(Guid(51)), "unidentified");
            Assert.That(_catalog.TryGetGroundItem(2220, true, out _), Is.False, "no ground sprite");
            Assert.That(_catalog.TryGetGroundItem(424242, true, out _), Is.False);
        }

        [Test]
        public void SkillIconsEffectsAndEmotes()
        {
            Assert.That(_catalog.TryGetSkillIcon(5, out var bash) ? bash.Still.AssetGUID : null, Is.EqualTo(Guid(605)));
            Assert.That(_catalog.TryGetSkillIcon(6, out _), Is.False);
            Assert.That(_catalog.TryGetEffect("cart", out var cart) ? cart.Animation.AssetGUID : null, Is.EqualTo(Guid(801)));
            Assert.That(_catalog.TryGetEffect("nope", out _), Is.False);
            Assert.That(_catalog.TryGetEmote(1, out var set, out int action), Is.True);
            Assert.That(action, Is.EqualTo(1), "the action is the emote id");
            Assert.That(set.Animation.AssetGUID, Is.EqualTo(Guid(900)));
            Assert.That(_catalog.TryGetEmote(77, out _, out _), Is.False);
        }

        [Test]
        public void WithoutTheItemTable_ItemLookupsMiss()
        {
            var bare = SpriteCatalog.Create(Array.Empty<ScriptableObject>(), null, null);

            Assert.That(bare.TryFindEquipmentByItem(1201, out _, out _), Is.False);
            Assert.That(bare.TryGetGroundItem(501, true, out _), Is.False);
            Assert.That(bare.TryGetUnit(1002, out _), Is.False);
        }

        // ---- cost (D16) ----

        [Test]
        public void LookupsAfterLoad_AllocateNothing()
        {
            void Lookups()
            {
                _catalog.TryGetUnit(1002, out _);
                _catalog.TryGetUnit(9999, out _);
                _catalog.TryFindEquipmentByItem(1201, out _, out _);
                _catalog.TryFindEquipment(EquipmentKind.Weapon, 99, 1117, out _);
                _catalog.TryFindBody(PlayerJob.JOB_KNIGHT2, out _);
                _catalog.TryFindHead(1, out _);
                _catalog.TryGetGroundItem(501, true, out _);
                _catalog.TryGetSkillIcon(5, out _);
                _catalog.TryGetEffect("cart", out _);
                _catalog.TryGetEmote(0, out _, out _);
            }

            Lookups();
            Assert.That(Lookups, UnityConstraints.AllocatingGCMemory(Is.Not));
        }

        // ---- keys (4.5) ----

        [Test]
        public void TheKeysAreTheIndexPaths()
        {
            Assert.That(SpriteCatalogKeys.Index(SpriteCatalogCategory.Monsters), Is.EqualTo("Assets/Art/Imported/catalog/monsters/_index.asset"));
            Assert.That(SpriteCatalogKeys.Index(SpriteCatalogCategory.GroundItems), Is.EqualTo("Assets/Art/Imported/catalog/ground-items/_index.asset"));
            Assert.That(SpriteCatalogKeys.Index(SpriteCatalogCategory.SkillIcons), Is.EqualTo("Assets/Art/Imported/catalog/skill-icons/_index.asset"));
            Assert.That(SpriteCatalogKeys.Items(), Is.EqualTo("Assets/Art/Imported/catalog/items/_index.asset"));
            Assert.That(SpriteCatalogKeys.Classes(), Is.EqualTo("Assets/Art/Imported/catalog/classes/_index.asset"));
            Assert.Throws<ArgumentException>(() => SpriteCatalogKeys.Index(SpriteCatalogCategory.Palettes));
        }
    }
}
