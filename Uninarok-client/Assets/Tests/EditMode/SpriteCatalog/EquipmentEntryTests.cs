using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;
using static Uninarok.Catalog.Tests.CatalogFixtures;

namespace Uninarok.Catalog.Tests
{
    // TASKS-0008 T3.2 (FR-4; PLAN-0008 4.4, D9, D16): slot resolution of an equipment entry.
    public class EquipmentEntryTests
    {
        readonly List<Object> _created = new List<Object>();
        ClassTable _classes;

        [SetUp]
        public void CreateClasses() =>
            _classes = Classes(_created,
                (PlayerJob.JOB_NOVICE, SpriteRace.Human, PlayerJob.JOB_NOVICE),
                (PlayerJob.JOB_SWORDMAN, SpriteRace.Human, PlayerJob.JOB_SWORDMAN),
                (PlayerJob.JOB_KNIGHT, SpriteRace.Human, PlayerJob.JOB_KNIGHT),
                (PlayerJob.JOB_SUMMONER, SpriteRace.Doram, PlayerJob.JOB_SUMMONER));

        [TearDown]
        public void Destroy() => DestroyAll(_created);

        EquipmentEntry Entry(EquipmentKind kind, params EquipmentEntry.EquipmentSlot[] slots)
        {
            var e = Make<EquipmentEntry>(_created);
            e.Kind = kind;
            e.ViewId = 1;
            e.Slots = slots;
            return e;
        }

        string Resolved(EquipmentEntry e, PlayerJob job, AccountSex sex) =>
            e.TryResolve(job, sex, _classes, out var animation, out _) ? animation.AssetGUID : null;

        [Test]
        public void ExactClassSlot_BeatsAnyClassDoram_BeatsAnyClassHuman()
        {
            var e = Entry(EquipmentKind.Garment,
                Slot(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, 1),
                AnyClass(SpriteRace.Doram, AccountSex.SEX_MALE, 2),
                AnyClass(SpriteRace.Human, AccountSex.SEX_MALE, 3));

            Assert.That(Resolved(e, PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE), Is.EqualTo(Guid(1)), "exact (job, sex)");
            Assert.That(Resolved(e, PlayerJob.JOB_SUMMONER, AccountSex.SEX_MALE), Is.EqualTo(Guid(2)), "any-class slot of the job's race");
            Assert.That(Resolved(e, PlayerJob.JOB_SWORDMAN, AccountSex.SEX_MALE), Is.EqualTo(Guid(3)), "any-class human");
        }

        [Test]
        public void ADoramWithoutADoramSlot_GetsTheAnyClassHumanSlot()
        {
            var e = Entry(EquipmentKind.Headgear, AnyClass(SpriteRace.Human, AccountSex.SEX_FEMALE, 4));

            Assert.That(Resolved(e, PlayerJob.JOB_SUMMONER, AccountSex.SEX_FEMALE), Is.EqualTo(Guid(4)));
        }

        [Test]
        public void AnyClass_DoesNotCollideWithTheNovice()
        {
            var e = Entry(EquipmentKind.Headgear,
                Slot(PlayerJob.JOB_NOVICE, AccountSex.SEX_MALE, 1),
                AnyClass(SpriteRace.Human, AccountSex.SEX_MALE, 2));

            Assert.That(Resolved(e, PlayerJob.JOB_NOVICE, AccountSex.SEX_MALE), Is.EqualTo(Guid(1)), "JOB_NOVICE is 0, AnyClass is a flag");
            Assert.That(Resolved(e, PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE), Is.EqualTo(Guid(2)));
        }

        [Test]
        public void NoSlotForTheSex_DrawsNothing()
        {
            var e = Entry(EquipmentKind.Headgear,
                Slot(PlayerJob.JOB_KNIGHT, AccountSex.SEX_FEMALE, 1),
                AnyClass(SpriteRace.Human, AccountSex.SEX_FEMALE, 2));

            Assert.That(e.TryResolve(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, _classes, out var animation, out var trail), Is.False);
            Assert.That(animation, Is.Null);
            Assert.That(trail, Is.Null);
        }

        [Test]
        public void AJobTheClassTableDoesNotList_IsHuman_AndNoTableMeansHuman()
        {
            var e = Entry(EquipmentKind.Garment,
                AnyClass(SpriteRace.Doram, AccountSex.SEX_MALE, 2),
                AnyClass(SpriteRace.Human, AccountSex.SEX_MALE, 3));

            Assert.That(Resolved(e, PlayerJob.JOB_ASSASSIN, AccountSex.SEX_MALE), Is.EqualTo(Guid(3)));
            Assert.That(e.TryResolve(PlayerJob.JOB_SUMMONER, AccountSex.SEX_MALE, null, out var animation, out _) ? animation.AssetGUID : null,
                Is.EqualTo(Guid(3)));
        }

        [Test]
        public void TheWeaponTrail_IsTheSlotsOwn()
        {
            var e = Entry(EquipmentKind.Weapon,
                Slot(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, 1),
                Slot(PlayerJob.JOB_SWORDMAN, AccountSex.SEX_MALE, 2));
            e.Trails = new[] { Animation(11), new SpriteAnimationReference(string.Empty) };

            Assert.That(e.TryResolve(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, _classes, out _, out var knightTrail), Is.True);
            Assert.That(knightTrail.AssetGUID, Is.EqualTo(Guid(11)));
            Assert.That(e.TryResolve(PlayerJob.JOB_SWORDMAN, AccountSex.SEX_MALE, _classes, out _, out var swordmanTrail), Is.True);
            Assert.That(swordmanTrail, Is.Null, "an empty trail reference is no trail");
        }

        [Test]
        public void OtherKinds_HaveNoTrail()
        {
            var e = Entry(EquipmentKind.Shield, Slot(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, 1));

            Assert.That(e.TryResolve(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, _classes, out _, out var trail), Is.True);
            Assert.That(trail, Is.Null);
        }

        [Test]
        public void TheDictionaries_AreBuiltOnce()
        {
            var e = Entry(EquipmentKind.Garment, Slot(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, 1), AnyClass(SpriteRace.Human, AccountSex.SEX_MALE, 2));

            Assert.That(e.DictionaryBuilds, Is.EqualTo(0), "nothing is built before the first lookup");
            for (int i = 0; i < 3; i++)
            {
                e.TryResolve(PlayerJob.JOB_KNIGHT, AccountSex.SEX_MALE, _classes, out _, out _);
                e.TryResolve(PlayerJob.JOB_SWORDMAN, AccountSex.SEX_FEMALE, _classes, out _, out _);
            }

            Assert.That(e.DictionaryBuilds, Is.EqualTo(1));
        }
    }
}
