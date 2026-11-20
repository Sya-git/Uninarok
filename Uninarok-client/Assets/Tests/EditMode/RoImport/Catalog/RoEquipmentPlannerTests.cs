using System.Linq;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;
using static Uninarok.RoImport.Tests.Catalog.PlannerFixtures;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T4.6 (FR-4, FR-10; PLAN-0008 4.3.4–4.3.7, 4.3.11, OQ-3): headgears, garments, weapons and shields.
    public class RoEquipmentPlannerTests
    {
        const AccountSex M = AccountSex.SEX_MALE, F = AccountSex.SEX_FEMALE;
        const SpriteRace H = SpriteRace.Human, D = SpriteRace.Doram;

        static RoCatalogRecord Record(RoCatalogPlan plan, string key) => plan.Records.Single(r => r.Key == key);
        static RoCatalogPair PairOf(RoCatalogRecord r, string role) => r.Pairs.Single(p => p.Role == role);

        // ---- headgears (4.3.4) ----

        static RoCatalogPlan Headgears() => Plan(new RoHeadgearPlanner(),
            RoCatalogTables.Create(viewIds: ViewIds(
                accIds: "ACCESSORY_HAT = 16, ACCESSORY_BAND = 17, ACCESSORY_LOST = 18",
                accNames: "[ACCESSORY_IDs.ACCESSORY_HAT] = \"_햇\", [ACCESSORY_IDs.ACCESSORY_BAND] = \"_밴드\", [ACCESSORY_IDs.ACCESSORY_LOST] = \"_없음\"")),
            Pairs("sprite/악세사리/남/남_햇", "sprite/악세사리/여/여_햇", "sprite/악세사리/남_doram/남_햇", "sprite/악세사리/여/여_밴드",
                "sprite/악세사리/남/belka_남"));

        [Test, Ignore("pending T4.8")]
        public void AHeadgear_HasASlotPerSex_AndADoramSlotWhereItsFileExists()
        {
            var hat = Record(Headgears(), RoCatalogKeys.Headgear(16));

            Assert.That(hat.Pairs.Select(p => p.Role), Is.EquivalentTo(new[]
            {
                RoCatalogRoles.Headgear(M, H), RoCatalogRoles.Headgear(F, H), RoCatalogRoles.Headgear(M, D),
            }));
            Assert.That(PairOf(hat, RoCatalogRoles.Headgear(M, D)).Spr.Canonical, Is.EqualTo("sprite/악세사리/남_doram/남_햇.spr"));
            Assert.That(hat.Value("viewId"), Is.EqualTo("16"));
            Assert.That(Record(Headgears(), RoCatalogKeys.Headgear(17)).Pairs.Single().Role, Is.EqualTo(RoCatalogRoles.Headgear(F, H)));
        }

        [Test, Ignore("pending T4.8")]
        public void AHeadgearWithoutAFile_AndAFileWithoutAName_AreWarned()
        {
            var plan = Headgears();

            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("없음"));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogFileWithoutName).Item, Is.EqualTo("sprite/악세사리/남/belka_남.spr"));
        }

        // ---- garments (4.3.5) ----

        static RoCatalogPlan Garments() => Plan(new RoGarmentPlanner(),
            RoCatalogTables.Create(own: Own(), viewIds: ViewIds(
                robeIds: "ROBE_WINGS = 1, ROBE_NONE = 2",
                robeNames: "[SPRITE_ROBE_IDs.ROBE_WINGS] = \"천사날개\", [SPRITE_ROBE_IDs.ROBE_NONE] = \"없는\"")),
            Pairs("sprite/로브/천사날개/천사날개", "sprite/로브/천사날개/남/기사_남").Concat(new[]
            {
                "sprite/로브/천사날개/천사날개_doram.spr", "sprite/로브/천사날개/남/검사_남.act", "sprite/로브/천사날개/남/summoner_남.act",
                "sprite/로브/천사날개/남/abyss_chaser_남.act",
            }));

        [Test, Ignore("pending T4.9")]
        public void AClassActTakesItsOwnSpr_ElseTheRootSheet_ElseTheDoramSheet()
        {
            var wings = Record(Garments(), RoCatalogKeys.Garment(1));

            Assert.That(PairOf(wings, RoCatalogRoles.Garment(PlayerJob.JOB_KNIGHT, M, H)).Spr.Canonical, Is.EqualTo("sprite/로브/천사날개/남/기사_남.spr"), "own SPR");
            var swordman = PairOf(wings, RoCatalogRoles.Garment(PlayerJob.JOB_SWORDMAN, M, H));
            Assert.That(swordman.Act.Canonical, Is.EqualTo("sprite/로브/천사날개/남/검사_남.act"));
            Assert.That(swordman.Spr.Canonical, Is.EqualTo("sprite/로브/천사날개/천사날개.spr"), "root sheet");
            Assert.That(PairOf(wings, RoCatalogRoles.Garment(PlayerJob.JOB_SUMMONER, M, D)).Spr.Canonical, Is.EqualTo("sprite/로브/천사날개/천사날개_doram.spr"));
        }

        [Test, Ignore("pending T4.9")]
        public void TheRootActIsTheAnyClassSlot()
        {
            var wings = Record(Garments(), RoCatalogKeys.Garment(1));

            Assert.That(PairOf(wings, RoCatalogRoles.GarmentRoot(H)).Act.Canonical, Is.EqualTo("sprite/로브/천사날개/천사날개.act"));
            Assert.That(PairOf(wings, RoCatalogRoles.GarmentRoot(D)).Spr.Canonical, Is.EqualTo("sprite/로브/천사날개/천사날개_doram.spr"));
        }

        [Test, Ignore("pending T4.9")]
        public void AnActOfABodyNotInTheTable_IsStray_AndAGarmentWithoutAFolderIsWarned()
        {
            var plan = Garments();

            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogStrayFile).Item, Is.EqualTo("sprite/로브/천사날개/남/abyss_chaser_남.act"));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("없는"));
        }

        // ---- weapons (4.3.6) ----

        static RoCatalogPlan Weapons() => Plan(new RoWeaponPlanner(),
            RoCatalogTables.Create(own: Own(), viewIds: ViewIds(
                weaponIds: "WEAPONTYPE_SHORTSWORD = 1, WEAPONTYPE_SWORD = 2, WEAPONTYPE_CUSTOM = 50",
                weaponNames: "[Weapon_IDs.WEAPONTYPE_SHORTSWORD] = \"_단검\", [Weapon_IDs.WEAPONTYPE_SWORD] = \"_검\", [Weapon_IDs.WEAPONTYPE_CUSTOM] = \"_커스텀\"",
                expansion: "[Weapon_IDs.WEAPONTYPE_CUSTOM] = Weapon_IDs.WEAPONTYPE_SWORD")),
            Pairs("sprite/인간족/기사/기사_남_단검", "sprite/인간족/기사/기사_남_단검_검광", "sprite/인간족/기사/기사_남_검", "sprite/인간족/기사/기사_남_검광",
                "sprite/인간족/기사/기사_남_1117", "sprite/인간족/검사/검사_남_검", "sprite/인간족/페코페코_기사/페코페코기사_남_1185",
                "sprite/인간족/기사/기사_남_1530_검광", "sprite/인간족/몸통/남/기사_남", "sprite/도람족/summoner/summoner_남_검"));

        [Test, Ignore("pending T4.10")]
        public void AViewIdHasTheSlotsOfTheClassesWithItsFile_AndTheirTrails()
        {
            var plan = Weapons();
            var dagger = Record(plan, RoCatalogKeys.Weapon(1));

            Assert.That(dagger.Pairs.Select(p => p.Role), Is.EquivalentTo(new[]
            {
                RoCatalogRoles.Weapon(PlayerJob.JOB_KNIGHT, M, H), RoCatalogRoles.WeaponTrail(PlayerJob.JOB_KNIGHT, M, H),
            }), "the swordman has no dagger file and no base type: no slot");
            Assert.That(PairOf(dagger, RoCatalogRoles.WeaponTrail(PlayerJob.JOB_KNIGHT, M, H)).Spr.Canonical, Is.EqualTo("sprite/인간족/기사/기사_남_단검_검광.spr"));

            var sword = Record(plan, RoCatalogKeys.Weapon(2));
            Assert.That(PairOf(sword, RoCatalogRoles.WeaponTrail(PlayerJob.JOB_KNIGHT, M, H)).Spr.Canonical, Is.EqualTo("sprite/인간족/기사/기사_남_검광.spr"), "<suffix>광");
            Assert.That(PairOf(sword, RoCatalogRoles.Weapon(PlayerJob.JOB_SUMMONER, M, D)).Spr.Canonical, Is.EqualTo("sprite/도람족/summoner/summoner_남_검.spr"));
        }

        [Test, Ignore("pending T4.10")]
        public void WithoutItsOwnFile_AClassFallsBackToTheBaseType()
        {
            var custom = Record(Weapons(), RoCatalogKeys.Weapon(50));
            string knight = RoCatalogRoles.Weapon(PlayerJob.JOB_KNIGHT, M, H);

            Assert.That(PairOf(custom, knight).Spr.Canonical, Is.EqualTo("sprite/인간족/기사/기사_남_검.spr"));
            Assert.That(custom.Value(RoCatalogRoles.Fallback(knight)), Is.EqualTo("50"));
            Assert.That(PairOf(custom, RoCatalogRoles.Weapon(PlayerJob.JOB_SWORDMAN, M, H)).Spr.Canonical, Is.EqualTo("sprite/인간족/검사/검사_남_검.spr"));
        }

        [Test, Ignore("pending T4.10")]
        public void AnItemIdFile_GetsItsOwnRecord()
        {
            var item = Record(Weapons(), RoCatalogKeys.WeaponItem(1117));

            Assert.That(item.Pairs.Single().Role, Is.EqualTo(RoCatalogRoles.Weapon(PlayerJob.JOB_KNIGHT, M, H)));
            Assert.That(item.Value("itemId"), Is.EqualTo("1117"));
        }

        [Test, Ignore("pending T4.10")]
        public void UnmatchedWeaponFiles_AreReported_AndBodyFoldersAreNotWeapons()
        {
            var unnamed = Weapons().Gaps.Where(g => g.Code == RoIssueCode.CatalogFileWithoutName).Select(g => g.Item).ToArray();

            Assert.That(unnamed, Is.EquivalentTo(new[]
            {
                "sprite/인간족/페코페코_기사/페코페코기사_남_1185.spr", "sprite/인간족/기사/기사_남_1530_검광.spr",
            }), "a prefix that is no body name; a trail of a weapon with no file");
        }

        // ---- shields (4.3.7) ----

        static RoCatalogPlan Shields() => Plan(new RoShieldPlanner(), RoCatalogTables.Create(own: Own()),
            Pairs("sprite/방패/기사/기사_남_가드", "sprite/방패/기사/기사_남_2189_방패", "sprite/방패/기사/기사_남_매드바니").Concat(new[] { "sprite/방패/기사/가드.act" }));

        [Test, Ignore("pending T4.11")]
        public void ShieldsByName_AndByItemId()
        {
            var plan = Shields();

            Assert.That(PairOf(Record(plan, RoCatalogKeys.Shield(1)), RoCatalogRoles.Shield(PlayerJob.JOB_KNIGHT, M, H)).Spr.Canonical,
                Is.EqualTo("sprite/방패/기사/기사_남_가드.spr"));
            Assert.That(Record(plan, RoCatalogKeys.ShieldItem(2189)).Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/방패/기사/기사_남_2189_방패.spr"));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("버클러"));
        }

        [Test, Ignore("pending T4.11")]
        public void UnreferencedShieldFiles_AreReported()
        {
            var unnamed = Shields().Gaps.Where(g => g.Code == RoIssueCode.CatalogFileWithoutName).Select(g => g.Item).ToArray();

            Assert.That(unnamed, Is.EquivalentTo(new[] { "sprite/방패/기사/기사_남_매드바니.spr", "sprite/방패/기사/가드.act" }));
        }
    }
}
