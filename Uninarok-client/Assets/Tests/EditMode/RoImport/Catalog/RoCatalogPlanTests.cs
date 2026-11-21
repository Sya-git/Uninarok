using System.Linq;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T4.1 (FR-10; PLAN-0008 4.3): the plan types, gaps and the record fingerprint.
    public class RoCatalogPlanTests
    {
        static RoCatalogRecord Poring(float scale = 1f, string role = "unit") =>
            new RoCatalogRecord(SpriteCatalogCategory.Monsters, "1002",
                new[] { RoFileNames.Pair("몬스터", "poring", role) },
                new[] { RoCatalogValue.Of("jobId", 1002), RoCatalogValue.Of("name", "PORING"), RoCatalogValue.Of("scale", scale) });

        [Test]
        public void AGapIsKeptOncePerCodeAndItem()
        {
            var b = new RoCatalogPlan.Builder();

            Assert.That(b.Gap(RoIssueCode.CatalogNameWithoutFile, SpriteCatalogCategory.Monsters, "sprite/몬스터/x.spr", "first"), Is.True);
            Assert.That(b.Gap(RoIssueCode.CatalogNameWithoutFile, SpriteCatalogCategory.Monsters, "sprite/몬스터/x.spr", "again"), Is.False);
            Assert.That(b.Gap(RoIssueCode.CatalogOrphanAct, SpriteCatalogCategory.Monsters, "sprite/몬스터/x.spr", "other code"), Is.True);
            Assert.That(b.Gap(RoIssueCode.CatalogNameWithoutFile, SpriteCatalogCategory.Npcs, "sprite/npc/y.spr", "other item"), Is.True);

            var plan = b.Build();
            Assert.That(plan.Gaps.Count, Is.EqualTo(3));
            Assert.That(plan.Gaps[0].Message, Is.EqualTo("first"));
        }

        [Test]
        public void GapsAreGroupedByCategory_InTheOrderFound()
        {
            var b = new RoCatalogPlan.Builder();
            b.Gap(RoIssueCode.CatalogStrayFile, SpriteCatalogCategory.Npcs, "b", "");
            b.Gap(RoIssueCode.CatalogStrayFile, SpriteCatalogCategory.Monsters, "z", "");
            b.Gap(RoIssueCode.CatalogStrayFile, SpriteCatalogCategory.Npcs, "a", "");
            b.Gap(RoIssueCode.CatalogStrayFile, SpriteCatalogCategory.Monsters, "y", "");

            Assert.That(b.Build().Gaps.Select(g => g.Item), Is.EqualTo(new[] { "z", "y", "b", "a" }));
        }

        [Test]
        public void TheGapsGoToTheReport_WithTheCategoryAsReferrer()
        {
            var b = new RoCatalogPlan.Builder();
            b.Gap(RoIssueCode.CatalogOrphanAct, SpriteCatalogCategory.Monsters, "sprite/몬스터/펫.act", "no SPR");
            var report = new RoReport();

            b.Build().ReportGaps(report);

            var issue = report.Issues.Single();
            Assert.That(issue.Code, Is.EqualTo(RoIssueCode.CatalogOrphanAct));
            Assert.That(issue.Referrer, Is.EqualTo("Monsters"));
            Assert.That(issue.Item, Is.EqualTo("sprite/몬스터/펫.act"));
        }

        [Test]
        public void RecordsAreSortedByCategoryThenKey()
        {
            var b = new RoCatalogPlan.Builder();
            b.Add(SpriteCatalogCategory.Npcs, "105", null, null);
            b.Add(SpriteCatalogCategory.Monsters, "2245", null, null);
            b.Add(SpriteCatalogCategory.Monsters, "1002", null, null);
            b.Add(SpriteCatalogCategory.Bodies, "7", null, null);

            Assert.That(b.Build().Records.Select(r => r.ToString()), Is.EqualTo(new[] { "Monsters/1002", "Monsters/2245", "Npcs/105", "Bodies/7" }),
                "category in enum order, key ordinal");
        }

        [Test]
        public void ASecondRecordWithTheSameKey_IsAPlannerBug()
        {
            var b = new RoCatalogPlan.Builder();
            b.Add(Poring());

            Assert.Throws<System.InvalidOperationException>(() => b.Add(Poring()));
            Assert.That(b.Contains(SpriteCatalogCategory.Monsters, "1002"), Is.True);
            Assert.That(b.Contains(SpriteCatalogCategory.Npcs, "1002"), Is.False);
        }

        [Test]
        public void TheFingerprintChangesWithAnyValueOrPair_AndOnlyThen()
        {
            string f = Poring().Fingerprint;

            Assert.That(f, Has.Length.EqualTo(64));
            Assert.That(Poring().Fingerprint, Is.EqualTo(f), "same record, same fingerprint");
            Assert.That(Poring(scale: 1.5f).Fingerprint, Is.Not.EqualTo(f), "a value");
            Assert.That(Poring(role: "other").Fingerprint, Is.Not.EqualTo(f), "a pair's role");
            var otherSheet = new RoCatalogRecord(SpriteCatalogCategory.Monsters, "1002",
                new[] { new RoCatalogPair(RoFileNames.Act("몬스터", "poring"), RoFileNames.Spr("몬스터", "poring2"), "unit") },
                Poring().Values);
            Assert.That(otherSheet.Fingerprint, Is.Not.EqualTo(f), "a pair's file");
            var otherKey = new RoCatalogRecord(SpriteCatalogCategory.Npcs, "1002", Poring().Pairs, Poring().Values);
            Assert.That(otherKey.Fingerprint, Is.Not.EqualTo(f), "the category");
        }

        [Test]
        public void EscapingKeepsFieldsApart()
        {
            var a = new RoCatalogRecord(SpriteCatalogCategory.Effects, "k", null, new[] { RoCatalogValue.Of("a", "b\tc") });
            var b = new RoCatalogRecord(SpriteCatalogCategory.Effects, "k", null, new[] { RoCatalogValue.Of("a\tb", "c") });

            Assert.That(a.Fingerprint, Is.Not.EqualTo(b.Fingerprint));
            Assert.That(a.Value("a"), Is.EqualTo("b\tc"));
            Assert.That(a.Value("missing"), Is.Null);
        }

        [Test]
        public void FileNamesAreCanonical()
        {
            Assert.That(RoFileNames.Spr("몬스터", "PORING").Canonical, Is.EqualTo("sprite/몬스터/poring.spr"));
            Assert.That(RoFileNames.Pal("몸", "기사_남_1").Canonical, Is.EqualTo("palette/몸/기사_남_1.pal"));
            Assert.That(RoFileNames.Still("아이템", "sm_bash", "icon").IsStatic, Is.True);
            Assert.That(RoFileNames.SpriteFolder("몬스터"), Is.EqualTo("sprite/몬스터/"));
            Assert.That(RoFileNames.StemUnder(RoPath.Parse("sprite/npc/a/b.spr"), "sprite/npc/"), Is.EqualTo("a/b"));
        }
    }
}
