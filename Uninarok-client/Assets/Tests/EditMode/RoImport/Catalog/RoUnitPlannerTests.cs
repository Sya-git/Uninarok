using System.Linq;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;
using static Uninarok.RoImport.Tests.Catalog.PlannerFixtures;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T4.2 (FR-1, FR-10; PLAN-0008 4.3.1, D11): monsters, NPCs and homunculi from synthetic tables.
    public class RoUnitPlannerTests
    {
        static RoCatalogTables Tables() => RoCatalogTables.Create(
            jobs: Jobs(
                "JT_8W_SOLDIER = 105, JT_PORING = 1002, JT_GR2 = 1003, JT_MISSING = 1004, JT_HALF = 1005, JT_8W_SOLDIER2 = 2245, JT_LIF = 6001, JT_ODD = 7000",
                "JT_NOVICE = 0, JT_KNIGHT = 7",
                "[jobtbl.JT_8W_SOLDIER] = \"8W_SOLDIER\", [jobtbl.JT_PORING] = \"PORING\", [jobtbl.JT_GR2] = \"lord.gr2\", " +
                "[jobtbl.JT_MISSING] = \"MISSING\", [jobtbl.JT_HALF] = \"HALF\", [jobtbl.JT_8W_SOLDIER2] = \"8W_SOLDIER\", " +
                "[jobtbl.JT_LIF] = \"LIF\", [jobtbl.JT_ODD] = \"ODD\", [JTtbl.JT_KNIGHT] = \"KNIGHT\""),
            scales: Scales("[1002] = { MonsterSize = 1.5 }"));

        static readonly string[] Files = Pairs("sprite/npc/8w_soldier", "sprite/몬스터/8w_soldier", "sprite/몬스터/poring", "sprite/homun/lif", "sprite/몬스터/odd")
            .Concat(new[] { "sprite/몬스터/half.act", "sprite/몬스터/펫악세.act" }).ToArray();

        static RoCatalogPlan Plan(SpriteCatalogCategory c) => PlannerFixtures.Plan(new RoUnitPlanner(c), Tables(), Files);

        [Test]
        public void IdRangesChooseTheFolder()
        {
            Assert.That(RoUnitFolders.TryGetCategory(105, out var npc) && npc == SpriteCatalogCategory.Npcs, Is.True);
            Assert.That(RoUnitFolders.TryGetCategory(15000, out var npc2) && npc2 == SpriteCatalogCategory.Npcs, Is.True);
            Assert.That(RoUnitFolders.TryGetCategory(1002, out var mob) && mob == SpriteCatalogCategory.Monsters, Is.True);
            Assert.That(RoUnitFolders.TryGetCategory(20100, out var mob2) && mob2 == SpriteCatalogCategory.Monsters, Is.True);
            Assert.That(RoUnitFolders.TryGetCategory(6001, out var homun) && homun == SpriteCatalogCategory.Homunculi, Is.True);
            Assert.That(RoUnitFolders.TryGetCategory(7000, out _), Is.False);
            Assert.That(RoUnitFolders.TryGetCategory(44, out _), Is.False);
            Assert.That(RoUnitFolders.FolderOf(SpriteCatalogCategory.Monsters), Is.EqualTo("몬스터"));
        }

        [Test]
        public void ANameInBothFolders_ResolvesByRange()
        {
            var npc = Plan(SpriteCatalogCategory.Npcs).Records.Single();
            Assert.That(npc.Key, Is.EqualTo("105"));
            Assert.That(npc.Pairs.Single().Spr.Canonical, Is.EqualTo("sprite/npc/8w_soldier.spr"));

            var monster = Plan(SpriteCatalogCategory.Monsters).Records.Single(r => r.Key == "2245");
            Assert.That(monster.Pairs.Single().Act.Canonical, Is.EqualTo("sprite/몬스터/8w_soldier.act"));

            Assert.That(Plan(SpriteCatalogCategory.Homunculi).Records.Single().Key, Is.EqualTo("6001"));
        }

        [Test]
        public void AGr2OrMissingName_IsOneWarningEach()
        {
            var gaps = Plan(SpriteCatalogCategory.Monsters).Gaps.Where(g => g.Code == RoIssueCode.CatalogNameWithoutFile).ToArray();

            Assert.That(gaps.Length, Is.EqualTo(2), string.Join("\n", gaps));
            Assert.That(gaps.Any(g => g.Item.Contains("lord.gr2")), Is.True);
            Assert.That(gaps.Any(g => g.Item.Contains("missing")), Is.True);
        }

        [Test]
        public void AnActWithoutSpr_IsAnOrphan()
        {
            var plan = Plan(SpriteCatalogCategory.Monsters);
            var orphans = plan.Gaps.Where(g => g.Code == RoIssueCode.CatalogOrphanAct).Select(g => g.Item).ToArray();

            Assert.That(orphans, Is.EquivalentTo(new[] { "sprite/몬스터/half.act", "sprite/몬스터/펫악세.act" }));
            Assert.That(plan.Records.Any(r => r.Key == "1005"), Is.False, "no record without its SPR");
        }

        [Test]
        public void TheScaleDefaultsTo1()
        {
            var plan = Plan(SpriteCatalogCategory.Monsters);

            Assert.That(plan.Records.Single(r => r.Key == "1002").Value("scale"), Is.EqualTo("1.5"));
            Assert.That(plan.Records.Single(r => r.Key == "2245").Value("scale"), Is.EqualTo("1"));
            Assert.That(plan.Records.Single(r => r.Key == "1002").Value("name"), Is.EqualTo("PORING"));
        }

        [Test]
        public void AnIdOutsideEveryRange_IsStray_AndPlayerIdsAreNotUnits()
        {
            var plan = Plan(SpriteCatalogCategory.Monsters);

            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogStrayFile && g.Item.Contains("7000")), Is.EqualTo(1));
            Assert.That(plan.Gaps.Any(g => g.Item.Contains("KNIGHT") || g.Item.Contains(" 7 ")), Is.False, "player job 7 is not a unit");
            Assert.That(plan.Records.Any(r => r.Key == "7000"), Is.False);
        }

        // T4.3 real-data run (Portuguese client, 2026-11-20): records, names without a file, orphan ACTs, unnamed files.
        [TestCase(SpriteCatalogCategory.Monsters, 3856, 104, 29, 78)]
        [TestCase(SpriteCatalogCategory.Npcs, 1097, 88, 0, 42)]
        [TestCase(SpriteCatalogCategory.Homunculi, 21, 3, 0, 0)]
        [Category(CatalogTestFiles.Category)]
        public void TheClientData(SpriteCatalogCategory category, int records, int withoutFile, int orphanActs, int unnamed)
        {
            var data = new Uninarok.RoImport.Core.Source.RoFolderSource(CatalogTestFiles.DataRoot);
            var report = new RoReport();
            var tables = RoCatalogTables.Load(data, new Uninarok.RoImport.Core.Source.RoFolderSource(CatalogTestFiles.SystemRoot),
                RoCatalogNeeds.Jobs | RoCatalogNeeds.Scales, RoItemTableLanguage.Portuguese, report);
            var b = new RoCatalogPlan.Builder();
            new RoUnitPlanner(category).Plan(new RoCatalogContext(data, tables), b);
            var plan = b.Build();

            Assert.That(report.Issues, Is.Empty);
            Assert.That(plan.Records.Count, Is.EqualTo(records));
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogNameWithoutFile), Is.EqualTo(withoutFile));
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogOrphanAct), Is.EqualTo(orphanActs));
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogFileWithoutName), Is.EqualTo(unnamed));
            Assert.That(plan.Gaps.Count(g => g.Code == RoIssueCode.CatalogStrayFile), Is.EqualTo(0));
        }

        [Test]
        public void WithoutTheJobTable_NothingIsPlanned()
        {
            var plan = PlannerFixtures.Plan(new RoUnitPlanner(SpriteCatalogCategory.Monsters), RoCatalogTables.Create(), Files);

            Assert.That(plan.Records, Is.Empty);
        }
    }
}
