using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Catalog;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Tests.Sprites;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.6 (FR-1, FR-5, FR-6, FR-7, FR-9, FR-10; PLAN-0008 4.7): entry jobs of the inline categories over a
    // hand-built plan and synthetic sprites.
    public class RoCatalogEntryConverterTests
    {
        RoOutputRoots _roots;
        string _temp, _data;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _temp = Path.Combine(Path.GetTempPath(), "RoCatalogEntryConverterTests", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_temp, "data");
            Pair("sprite/몬스터/poring", SpriteTestData.Monster());
            Pair("sprite/아이템/빨간포션", SpriteTestData.Static());
            Write("sprite/몬스터/broken.spr", new byte[] { (byte)'S', (byte)'P', 1, 2, 9, 0, 0, 0, 1, 2, 3 });
            Write("sprite/몬스터/broken.act", SpriteFileWriter.Act(SpriteTestData.Static().act));
            Write("palette/몸/초보자_남_1.pal", SpriteFileWriter.Pal(SpriteTestData.Palette(3)));
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        void Write(string relative, byte[] bytes)
        {
            string path = Path.Combine(_data, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        void Pair(string stem, (RoSpr spr, RoAct act) sample)
        {
            Write(stem + ".spr", SpriteFileWriter.Spr(sample.spr));
            Write(stem + ".act", SpriteFileWriter.Act(sample.act));
        }

        static RoCatalogRecord Unit(string key, string stem, float scale = 1f) =>
            new RoCatalogRecord(SpriteCatalogCategory.Monsters, key, new[] { RoFileNames.Pair("몬스터", stem, RoUnitPlanner.Role) },
                new[] { RoCatalogValue.Of("jobId", int.Parse(key)), RoCatalogValue.Of("scale", scale) });

        static RoCatalogRecord Ground() =>
            new RoCatalogRecord(SpriteCatalogCategory.GroundItems, "빨간포션", new[] { RoFileNames.Pair("아이템", "빨간포션", RoGroundItemPlanner.Role) },
                new[] { RoCatalogValue.Of("resource", "빨간포션") });

        static RoCatalogRecord Palette() =>
            new RoCatalogRecord(SpriteCatalogCategory.Palettes, "몸/초보자_남_1", null, new[] { RoCatalogValue.Of("palette", "palette/몸/초보자_남_1.pal") });

        (RoReport report, RoCatalogRun run) Run(bool force = false, params RoCatalogRecord[] records)
        {
            var b = new RoCatalogPlan.Builder();
            foreach (var r in records) b.Add(r);
            var run = new RoCatalogRun(b.Build(), RoCatalogTables.Create());
            var options = new RoImportOptions
            {
                Force = force, ShowProgress = false, Roots = _roots, Source = new RoFolderSource(_data),
                LedgerFolder = Path.Combine(_temp, "ledger"), CatalogPlan = run,
            };
            var jobs = records.Select(r => new RoJob(RoJobKind.CatalogEntry, RoPath.Parse(RoCatalogRun.JobPath(r.Category, r.Key))));
            return (RoImportApi.Run(jobs, options), run);
        }

        [Test]
        public void AUnitRecord_ConvertsItsPair_AndKeepsItsOutputsForTheIndex()
        {
            var (report, run) = Run(false, Unit("1002", "poring"));

            Assert.That(report.Created, Is.EqualTo(1), report.Summarize());
            Assert.That(run.TryGetOutputs("catalog/monsters/1002", out var outputs), Is.True);
            var o = outputs.Single();
            Assert.That(o.Role, Is.EqualTo(RoUnitPlanner.Role));
            Assert.That(o.AssetPath, Is.EqualTo(_roots.Art.TrimEnd('/') + "/sprite/몬스터/poring.asset"));
            Assert.That(o.SheetPath, Does.Contain("/sprite/_sheets/"));
            Assert.That(o.Actions, Is.GreaterThan(0));
            Assert.That(o.IsStill, Is.False);
        }

        [Test]
        public void RunningTheSameJobTwice_IsSkippedTheSecondTime_WithItsOutputs()
        {
            Run(false, Unit("1002", "poring"));
            var (report, run) = Run(false, Unit("1002", "poring"));

            Assert.That(report.Skipped, Is.EqualTo(1), report.Summarize());
            Assert.That(report.Created + report.Updated, Is.EqualTo(0));
            Assert.That(run.TryGetOutputs("catalog/monsters/1002", out var outputs) && outputs.Length == 1, Is.True, "a skipped record still gives the index its outputs");
        }

        [Test]
        public void AChangedValue_ReimportsTheRecord()
        {
            Run(false, Unit("1002", "poring"));
            var (report, _) = Run(false, Unit("1002", "poring", scale: 2f));

            Assert.That(report.Updated, Is.EqualTo(1), report.Summarize());
        }

        [Test]
        public void AStaticSprite_IsKeptAsItsSheetAndSubAsset()
        {
            var (report, run) = Run(false, Ground());

            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());
            run.TryGetOutputs("catalog/ground-items/빨간포션", out var outputs);
            var o = outputs.Single();
            Assert.That(o.IsStill, Is.True);
            Assert.That(o.AssetPath, Is.EqualTo(o.SheetPath));
            Assert.That(AssetDatabase.LoadAllAssetRepresentationsAtPath(o.AssetPath).Any(s => s.name == o.SubAsset), Is.True);
        }

        [Test]
        public void AFailingPair_FailsItsRecordOnly_WithNoOutputs()
        {
            var (report, run) = Run(false, Unit("1002", "poring"), Unit("1100", "broken"));

            Assert.That(report.Failed, Is.EqualTo(1));
            Assert.That(report.Failures.Single().Item, Does.Contain("broken"), "SPEC-0007's issue for the file");
            Assert.That(report.Failures.Single().Referrer, Is.EqualTo("catalog/monsters/1100"), "the record that asked for it");
            Assert.That(report.Created, Is.EqualTo(1));
            Assert.That(run.TryGetOutputs("catalog/monsters/1100", out _), Is.False, "no partial record");
        }

        [Test]
        public void APaletteRecord_ConvertsThePalette()
        {
            var (report, run) = Run(false, Palette());

            Assert.That(report.Created, Is.EqualTo(1), report.Summarize());
            run.TryGetOutputs("catalog/palettes/몸/초보자_남_1", out var outputs);
            Assert.That(outputs.Single().AssetPath, Is.EqualTo(_roots.Art.TrimEnd('/') + "/palette/몸/초보자_남_1.png"));
            Assert.That(Run(false, Palette()).report.Skipped, Is.EqualTo(1));
        }

        // ---- T5.7: entry assets (FR-2, FR-3, FR-4, FR-8, FR-9; PLAN-0008 4.4, 4.6) ----

        const Uninarok.Networking.Data.AccountSex Male = Uninarok.Networking.Data.AccountSex.SEX_MALE;

        void EntryFiles()
        {
            Pair("sprite/인간족/몸통/남/초보자_남", SpriteTestData.Monster());
            Pair("sprite/인간족/머리통/남/1_남", SpriteTestData.Anchored());
            Write("palette/머리/머리1_남_0.pal", SpriteFileWriter.Pal(SpriteTestData.Palette(5)));
            Pair("sprite/악세사리/남/남_햇", SpriteTestData.Anchored());
            Pair("sprite/인간족/초보자/초보자_남_단검", SpriteTestData.Weapon());
            Pair("sprite/인간족/초보자/초보자_남_단검_검광", SpriteTestData.Weapon());
            Pair("sprite/로브/천사날개/천사날개", SpriteTestData.Anchored());
        }

        static RoCatalogRecord Body()
        {
            string role = RoCatalogRoles.Body(Uninarok.Networking.Data.PlayerJob.JOB_NOVICE, Male, SpriteRace.Human, BodyVariantKind.Base);
            return new RoCatalogRecord(SpriteCatalogCategory.Bodies, "body-0000", new[] { RoFileNames.Pair("인간족/몸통/남", "초보자_남", role) }, new[]
            {
                RoCatalogValue.Of("baseJob", "JOB_NOVICE"), RoCatalogValue.Of(RoCatalogRoles.Name(role), "초보자"),
                RoCatalogValue.Of(RoCatalogRoles.Palette(role, 1), "palette/몸/초보자_남_1.pal"),
            });
        }

        static RoCatalogRecord Head()
        {
            string role = RoCatalogRoles.Head(Male, SpriteRace.Human);
            return new RoCatalogRecord(SpriteCatalogCategory.Heads, "head-01", new[] { RoFileNames.Pair("인간족/머리통/남", "1_남", role) },
                new[] { RoCatalogValue.Of("style", 1), RoCatalogValue.Of(RoCatalogRoles.Palette(role, 0), "palette/머리/머리1_남_0.pal") });
        }

        static RoCatalogRecord Hat() =>
            new RoCatalogRecord(SpriteCatalogCategory.Headgears, "headgear-0016", new[] { RoFileNames.Pair("악세사리/남", "남_햇", RoCatalogRoles.Headgear(Male, SpriteRace.Human)) },
                new[] { RoCatalogValue.Of("viewId", 16), RoCatalogValue.Of("name", "_햇") });

        static RoCatalogRecord Dagger()
        {
            var job = Uninarok.Networking.Data.PlayerJob.JOB_NOVICE;
            string role = RoCatalogRoles.Weapon(job, Male, SpriteRace.Human);
            return new RoCatalogRecord(SpriteCatalogCategory.Weapons, "weapon-0001", new[]
            {
                RoFileNames.Pair("인간족/초보자", "초보자_남_단검", role),
                RoFileNames.Pair("인간족/초보자", "초보자_남_단검_검광", RoCatalogRoles.WeaponTrail(job, Male, SpriteRace.Human)),
            }, new[] { RoCatalogValue.Of("viewId", 1), RoCatalogValue.Of("name", "_단검"), RoCatalogValue.Of(RoCatalogRoles.Fallback(role), 50) });
        }

        static RoCatalogRecord Wings() =>
            new RoCatalogRecord(SpriteCatalogCategory.Garments, "garment-0001", new[] { RoFileNames.Pair("로브/천사날개", "천사날개", RoCatalogRoles.GarmentRoot(SpriteRace.Human)) },
                new[] { RoCatalogValue.Of("viewId", 1), RoCatalogValue.Of("name", "천사날개") });

        T Entry<T>(SpriteCatalogCategory c, string key) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(RoCatalogOutputPaths.Entry(_roots, c, key));

        static string GuidOf(string path) => AssetDatabase.AssetPathToGUID(path);

        [Test]
        public void ABodyEntry_HasItsVariantWithTypedReferences_AndItsPalettes()
        {
            EntryFiles();
            var (report, _) = Run(false, Body());

            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());
            var body = Entry<BodyEntry>(SpriteCatalogCategory.Bodies, "body-0000");
            Assert.That(body.BaseJob, Is.EqualTo(Uninarok.Networking.Data.PlayerJob.JOB_NOVICE));
            var v = body.Variants.Single();
            Assert.That(v.Name, Is.EqualTo("초보자"));
            Assert.That(v.Sex, Is.EqualTo(Male));
            Assert.That(v.Sprite.Animation.AssetGUID, Is.EqualTo(GuidOf(_roots.Art.TrimEnd('/') + "/sprite/인간족/몸통/남/초보자_남.asset")));
            Assert.That(v.Palettes.Colours.Single().Colour, Is.EqualTo(1));
            Assert.That(v.Palettes.Colours.Single().Palette.AssetGUID, Is.EqualTo(GuidOf(_roots.Art.TrimEnd('/') + "/palette/몸/초보자_남_1.png")), "its own palette, converted");
            Assert.That(body.StoredActions, Is.GreaterThan(0));
            Assert.That(body.SheetCount, Is.EqualTo(1));
        }

        [Test]
        public void AHeadEntry_HasItsStyleAndHairPalette()
        {
            EntryFiles();
            Run(false, Head());

            var head = Entry<HeadEntry>(SpriteCatalogCategory.Heads, "head-01");
            Assert.That(head.Style, Is.EqualTo(1));
            Assert.That(head.Variants.Single().Palettes.Colours.Single().Colour, Is.EqualTo(0));
        }

        [Test]
        public void EquipmentEntries_HaveTheirSlots_TrailsAndFallbacks()
        {
            EntryFiles();
            var (report, _) = Run(false, Hat(), Dagger(), Wings());
            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());

            var hat = Entry<EquipmentEntry>(SpriteCatalogCategory.Headgears, "headgear-0016");
            Assert.That(hat.Kind, Is.EqualTo(EquipmentKind.Headgear));
            Assert.That(hat.ViewId, Is.EqualTo(16));
            Assert.That(hat.Slots.Single().AnyClass, Is.True);
            Assert.That(hat.Trails, Is.Empty, "only weapons have trails");

            var dagger = Entry<EquipmentEntry>(SpriteCatalogCategory.Weapons, "weapon-0001");
            var slot = dagger.Slots.Single();
            Assert.That(slot.AnyClass, Is.False);
            Assert.That(slot.Job, Is.EqualTo(Uninarok.Networking.Data.PlayerJob.JOB_NOVICE));
            Assert.That(slot.FallbackOf, Is.EqualTo(50));
            Assert.That(dagger.Trails.Single().AssetGUID, Is.EqualTo(GuidOf(_roots.Art.TrimEnd('/') + "/sprite/인간족/초보자/초보자_남_단검_검광.asset")));

            var wings = Entry<EquipmentEntry>(SpriteCatalogCategory.Garments, "garment-0001");
            Assert.That(wings.Slots.Select(s => (s.AnyClass, s.Sex)), Is.EquivalentTo(new[] { (true, Male), (true, Uninarok.Networking.Data.AccountSex.SEX_FEMALE) }),
                "the root ACT is an any-class slot for both sexes");
        }

        [Test]
        public void AForcedRerun_KeepsTheEntrysGuidAndBytes()
        {
            EntryFiles();
            Run(false, Hat(), Body());
            string hatPath = RoCatalogOutputPaths.Entry(_roots, SpriteCatalogCategory.Headgears, "headgear-0016");
            string bodyPath = RoCatalogOutputPaths.Entry(_roots, SpriteCatalogCategory.Bodies, "body-0000");
            string hatGuid = GuidOf(hatPath), bodyGuid = GuidOf(bodyPath);
            byte[] hatBytes = File.ReadAllBytes(RoImportTestRoot.FullPath(hatPath)), bodyBytes = File.ReadAllBytes(RoImportTestRoot.FullPath(bodyPath));

            var (report, _) = Run(true, Hat(), Body());

            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());
            Assert.That(GuidOf(hatPath), Is.EqualTo(hatGuid));
            Assert.That(GuidOf(bodyPath), Is.EqualTo(bodyGuid));
            Assert.That(File.ReadAllBytes(RoImportTestRoot.FullPath(hatPath)), Is.EqualTo(hatBytes));
            Assert.That(File.ReadAllBytes(RoImportTestRoot.FullPath(bodyPath)), Is.EqualTo(bodyBytes));
            Assert.That(Run(false, Hat(), Body()).report.Skipped, Is.EqualTo(2), "and current afterwards");
        }
    }
}
