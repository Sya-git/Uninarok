using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;
using static Uninarok.RoImport.Tests.Catalog.PlannerFixtures;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T4.2 (FR-5, FR-7, FR-10; PLAN-0008 4.3.8): ground items and skill icons from synthetic tables.
    public class RoStaticPlannerTests
    {
        static readonly string[] ItemFiles = Pairs("sprite/아이템/빨간포션", "sprite/아이템/단검", "sprite/아이템/sm_bash", "sprite/아이템/al_heal")
            .Concat(new[] { "sprite/아이템/외톨이.act", "sprite/아이템/그림만.spr" }).ToArray();

        static RoCatalogTables ItemTables() => RoCatalogTables.Create(items: Items(
            "[501] = { identifiedResourceName = \"빨간포션\", unidentifiedResourceName = \"빨간포션\", ClassNum = 0 }, " +
            "[502] = { identifiedResourceName = \"빨간포션\", unidentifiedResourceName = \"빨간포션\", ClassNum = 0 }, " +
            "[1201] = { identifiedResourceName = \"단검\", unidentifiedResourceName = \"검\", ClassNum = 1 }"));

        static RoCatalogPlan GroundItems() => Plan(new RoGroundItemPlanner(), ItemTables(), ItemFiles);

        [Test]
        public void ItemsSharingAResource_ShareOneRecord()
        {
            var plan = GroundItems();

            Assert.That(plan.Records.Count(r => r.Key == "빨간포션"), Is.EqualTo(1), "501 and 502");
            Assert.That(plan.Records.Single(r => r.Key == "빨간포션").Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/아이템/빨간포션.spr"));
        }

        [Test]
        public void IdentifiedAndUnidentified_AreBothPlanned()
        {
            var plan = GroundItems();

            Assert.That(plan.Records.Any(r => r.Key == "단검"), Is.True, "identified");
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("검.spr"), "unidentified 검 has no file");
            Assert.That(plan.Records.Count, Is.EqualTo(2));
        }

        [Test]
        public void OrphansInTheItemFolder_AreReported()
        {
            var plan = GroundItems();

            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanAct).Item, Is.EqualTo("sprite/아이템/외톨이.act"));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanSpr).Item, Is.EqualTo("sprite/아이템/그림만.spr"));
        }

        [Test]
        public void ASkillIcon_IsTheLowerCaseSkid()
        {
            var tables = RoCatalogTables.Create(skills: Skills("SM_BASH = 5, AL_HEAL = 28, MG_NOICON = 10, NPC_PIERCINGATT = 158, NPC_COMBOATTACK = 171"));
            var plan = Plan(new RoSkillIconPlanner(), tables, ItemFiles);

            Assert.That(plan.Records.Select(r => r.Key), Is.EquivalentTo(new[] { "5", "28" }));
            Assert.That(plan.Records.Single(r => r.Key == "5").Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/아이템/sm_bash.spr"));
            Assert.That(plan.Records.Single(r => r.Key == "5").Value("constant"), Is.EqualTo("SM_BASH"));
            Assert.That(plan.Gaps.Count(g => g.Item.Contains("mg_noicon")), Is.EqualTo(1), "a player skill without an icon is warned");
            Assert.That(plan.Gaps.Count(g => g.Item.Contains("npc_")), Is.EqualTo(0), "monster skills are not warned one by one");
            Assert.That(plan.Gaps.Single(g => g.Item == RoSkillIconPlanner.MonsterSkillPrefix + "*").Message, Does.Contain("2"));
        }

        // T7.2 (FR-10, D22): an icon whose SPR has no ACT is an orphan warning, not a record SPEC-0007 could not convert.
        [Test]
        public void ASkillIcon_WithoutItsAct_IsAnOrphanWarning_NotARecord()
        {
            var tables = RoCatalogTables.Create(skills: Skills("SM_BASH = 5, BS_SKINTEMPER = 109"));
            var plan = Plan(new RoSkillIconPlanner(), tables, ItemFiles.Concat(new[] { "sprite/아이템/bs_skintemper.spr" }));

            Assert.That(plan.Records.Select(r => r.Key), Is.EquivalentTo(new[] { "5" }));
            var orphan = plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanSpr);
            Assert.That(orphan.Item, Is.EqualTo("sprite/아이템/bs_skintemper.spr"));
            Assert.That(orphan.Message, Does.Contain("109"));
        }

        // T4.4 real-data run (Portuguese client, 2026-11-20): 17,079 of 17,482 items have a ground sprite; 1,295 skill icons.
        [Test, Category(CatalogTestFiles.Category)]
        public void TheClientData_GroundItemsAndSkillIcons()
        {
            var data = new Uninarok.RoImport.Core.Source.RoFolderSource(CatalogTestFiles.DataRoot);
            var report = new RoReport();
            var tables = RoCatalogTables.Load(data, new Uninarok.RoImport.Core.Source.RoFolderSource(CatalogTestFiles.SystemRoot),
                RoCatalogNeeds.Items | RoCatalogNeeds.Skills, RoItemTableLanguage.Portuguese, report);
            var ctx = new RoCatalogContext(data, tables);
            var b = new RoCatalogPlan.Builder();
            new RoGroundItemPlanner().Plan(ctx, b);
            new RoSkillIconPlanner().Plan(ctx, b);
            var plan = b.Build();

            var ground = new System.Collections.Generic.HashSet<string>(plan.RecordsOf(Uninarok.Catalog.SpriteCatalogCategory.GroundItems).Select(r => r.Key));
            int withSprite = tables.Items.ItemIds.Count(id =>
                tables.Items.TryGet(id, out var info) && info.IdentifiedResource != null && ground.Contains(RoGroundItemPlanner.KeyOf(info.IdentifiedResource) ?? ""));

            Assert.That(report.Issues, Is.Empty);
            Assert.That(withSprite, Is.EqualTo(17079));
            Assert.That(ground.Count, Is.EqualTo(7196), "distinct resources with a sprite");
            // 1,295 icon files, of which 2 SPRs have no ACT (bs_skintemper, ht_falcon): orphans since T7.2 (D22).
            Assert.That(plan.RecordsOf(Uninarok.Catalog.SpriteCatalogCategory.SkillIcons).Count(), Is.EqualTo(1293));
            // Both planners scan 아이템/ and a gap is kept once per (code, item), so the ground items may list them first.
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogOrphanSpr && (g.Item.EndsWith("/bs_skintemper.spr") || g.Item.EndsWith("/ht_falcon.spr"))), Is.EqualTo(2));
        }

        [Test]
        public void EffectsAreByName_AndOrphansAreWarnings()
        {
            var files = Pairs("sprite/이팩트/cart", "sprite/이팩트/sub/glow", "sprite/이팩트/emotion")
                .Concat(new[] { "sprite/이팩트/bluemsg.spr", "sprite/이팩트/emotion_index.act" });
            var plan = Plan(new RoEffectPlanner(), RoCatalogTables.Create(emotes: Emotes("ET_SURPRISE = 0\nET_QUESTION = 1")), files);

            Assert.That(plan.Records.Select(r => r.Key), Is.EquivalentTo(new[] { "cart", "emotion", "sub/glow" }));
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanSpr).Item, Is.EqualTo("sprite/이팩트/bluemsg.spr"), "FR-10, D22");
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogOrphanAct).Item, Is.EqualTo("sprite/이팩트/emotion_index.act"));
        }

        [Test]
        public void TheEmoteSet_CarriesTheEmoteIds()
        {
            var emotes = RoCatalogTables.Create(emotes: Emotes("ET_SURPRISE = 0\nET_QUESTION = 1"));
            var plan = Plan(new RoEffectPlanner(), emotes, Pairs("sprite/이팩트/emotion"));
            var set = plan.Records.Single(r => r.Key == RoEffectPlanner.EmoteSet);

            Assert.That(set.Value(RoEffectPlanner.EmoteValuePrefix + "ET_SURPRISE"), Is.EqualTo("0"));
            Assert.That(set.Value(RoEffectPlanner.EmoteValuePrefix + "ET_QUESTION"), Is.EqualTo("1"));

            var missing = Plan(new RoEffectPlanner(), emotes, Pairs("sprite/이팩트/cart"));
            Assert.That(missing.Gaps.Single(g => g.Code == RoIssueCode.CatalogNameWithoutFile).Item, Does.Contain("emotion"));
        }

        // TASKS-0008 T4.12 (FR-2, FR-3, FR-9; PLAN-0008 4.3.10): every palette, keyed by its stem.
        [Test]
        public void EveryPalette_IsARecord_AndOtherFilesAreStray()
        {
            var plan = Plan(new RoPalettePlanner(), RoCatalogTables.Create(),
                new[] { "palette/몸/기사_남_1.pal", "palette/머리/머리1_남_0.pal", "palette/도람족/body/summoner_남_0.pal", "palette/몸/readme.txt" });

            Assert.That(plan.Records.Select(r => r.Key), Is.EqualTo(new[] { "도람족/body/summoner_남_0", "머리/머리1_남_0", "몸/기사_남_1" }));
            Assert.That(plan.Records.Last().Value("palette"), Is.EqualTo("palette/몸/기사_남_1.pal"));
            Assert.That(plan.Records.All(r => r.Pairs.Count == 0), Is.True);
            Assert.That(plan.Gaps.Single(g => g.Code == RoIssueCode.CatalogStrayFile).Item, Is.EqualTo("palette/몸/readme.txt"));
        }

        // T4.12 real-data run (2026-11-20): 4,464 palettes (749 + 3,375 + 340), no stray.
        [Test, Category(CatalogTestFiles.Category)]
        public void TheClientData_Palettes()
        {
            var data = new Uninarok.RoImport.Core.Source.RoFolderSource(CatalogTestFiles.DataRoot);
            var plan = PlanData(new RoPalettePlanner(), RoCatalogTables.Create(), data);

            Assert.That(plan.Records.Count, Is.EqualTo(4464));
            Assert.That(plan.Gaps, Is.Empty);
        }

        // T4.5 real-data run (2026-11-20): 225 effect sprites (as the survey of sprite-catalog.md §2), 88 emotes, 10 orphan ACTs; 1 orphan SPR (T7.2).
        [Test, Category(CatalogTestFiles.Category)]
        public void TheClientData_Effects()
        {
            var data = new Uninarok.RoImport.Core.Source.RoFolderSource(CatalogTestFiles.DataRoot);
            var report = new RoReport();
            var tables = RoCatalogTables.Load(data, null, RoCatalogNeeds.Emotes, RoItemTableLanguage.Portuguese, report);
            var plan = PlanData(new RoEffectPlanner(), tables, data);

            Assert.That(report.Issues, Is.Empty);
            Assert.That(plan.Records.Count, Is.EqualTo(224), "225 sprites less bluemsg, an SPR without ACT (D22)");
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogOrphanSpr), Is.EqualTo(1));
            Assert.That(plan.Records.Single(r => r.Key == RoEffectPlanner.EmoteSet).Values.Count(v => v.Name.StartsWith(RoEffectPlanner.EmoteValuePrefix)), Is.EqualTo(88));
            Assert.That(plan.Records.Any(r => r.Key == "손수레") && plan.Records.Any(r => r.Key == "매"), Is.True, "the cart and the falcon by name");
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogOrphanAct), Is.EqualTo(10));
        }

        static RoCatalogPlan PlanData(IRoCatalogPlanner planner, RoCatalogTables tables, Uninarok.RoImport.Core.Source.IRoSource data)
        {
            var b = new RoCatalogPlan.Builder();
            planner.Plan(new RoCatalogContext(data, tables), b);
            return b.Build();
        }
    }
}
