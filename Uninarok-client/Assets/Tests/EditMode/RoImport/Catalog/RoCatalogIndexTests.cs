using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
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
using static Uninarok.RoImport.Tests.Catalog.PlannerFixtures;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.8 (FR-1 to FR-8; PLAN-0008 4.5, 4.6, 4.7): the index step over a hand-built plan and synthetic sprites, with
    // a throwaway Addressables settings asset.
    public class RoCatalogIndexTests
    {
        const AccountSex Male = AccountSex.SEX_MALE;
        RoOutputRoots _roots;
        string _temp, _data;
        AddressableAssetSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
            _temp = Path.Combine(Path.GetTempPath(), "RoCatalogIndexTests", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_temp, "data");
            Pair("sprite/몬스터/poring", SpriteTestData.Monster());
            Pair("sprite/몬스터/lunatic", SpriteTestData.Weapon());
            Pair("sprite/아이템/빨간포션", SpriteTestData.Static());
            Pair("sprite/이팩트/emotion", SpriteTestData.Mixed());
            Pair("sprite/악세사리/남/남_햇", SpriteTestData.Anchored());
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        void Pair(string stem, (RoSpr spr, RoAct act) sample)
        {
            foreach (var (ext, bytes) in new[] { (".spr", SpriteFileWriter.Spr(sample.spr)), (".act", SpriteFileWriter.Act(sample.act)) })
            {
                string path = Path.Combine(_data, stem + ext);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, bytes);
            }
        }

        static RoCatalogRecord Unit(int id, string stem, float scale) =>
            new RoCatalogRecord(SpriteCatalogCategory.Monsters, id.ToString(), new[] { RoFileNames.Pair("몬스터", stem, RoUnitPlanner.Role) },
                new[] { RoCatalogValue.Of("jobId", id), RoCatalogValue.Of("name", stem.ToUpperInvariant()), RoCatalogValue.Of("scale", scale) });

        static RoCatalogRecord[] Records() => new[]
        {
            Unit(1063, "lunatic", 1f), Unit(1002, "poring", 1.5f),
            new RoCatalogRecord(SpriteCatalogCategory.GroundItems, "빨간포션", new[] { RoFileNames.Pair("아이템", "빨간포션", RoGroundItemPlanner.Role) }, new[] { RoCatalogValue.Of("resource", "빨간포션") }),
            new RoCatalogRecord(SpriteCatalogCategory.Effects, "emotion", new[] { RoFileNames.Pair("이팩트", "emotion", RoEffectPlanner.Role) },
                new[] { RoCatalogValue.Of("name", "emotion"), RoCatalogValue.Of(RoEffectPlanner.EmoteValuePrefix + "ET_QUESTION", 1), RoCatalogValue.Of(RoEffectPlanner.EmoteValuePrefix + "ET_SURPRISE", 0) }),
            new RoCatalogRecord(SpriteCatalogCategory.Headgears, "headgear-0016", new[] { RoFileNames.Pair("악세사리/남", "남_햇", RoCatalogRoles.Headgear(Male, SpriteRace.Human)) },
                new[] { RoCatalogValue.Of("viewId", 16), RoCatalogValue.Of("name", "_햇") }),
        };

        static RoCatalogTables Tables()
        {
            Assert.That(RoItemDb.TryRead("item_db: (\n{\n\tId: 2220\n\tType: \"IT_ARMOR\"\n\tLoc: \"EQP_HEAD_TOP\"\n},\n{\n\tId: 1201\n\tType: \"IT_WEAPON\"\n},\n)",
                out var kinds, out string reason), Is.True, reason);
            return RoCatalogTables.Create(own: Own(), itemKinds: kinds, items: PlannerFixtures.Items(
                "[501] = { identifiedResourceName = \"빨간포션\", unidentifiedResourceName = \"빨간포션\", ClassNum = 0 }, " +
                "[2220] = { identifiedResourceName = \"햇\", unidentifiedResourceName = \"모자\", ClassNum = 16 }, " +
                "[1201] = { identifiedResourceName = \"단검\", unidentifiedResourceName = \"단검\", ClassNum = 1 }"));
        }

        RoReport Run(params SpriteCatalogCategory[] categories)
        {
            var b = new RoCatalogPlan.Builder();
            foreach (var r in Records().Where(r => categories.Contains(r.Category))) b.Add(r);
            var plan = b.Build();
            var run = new RoCatalogRun(plan, Tables()) { Addressables = _settings };
            var options = new RoImportOptions
            {
                ShowProgress = false, Roots = _roots, Source = new RoFolderSource(_data), LedgerFolder = Path.Combine(_temp, "ledger"), CatalogPlan = run,
            };
            var jobs = plan.Records.Select(r => new RoJob(RoJobKind.CatalogEntry, RoPath.Parse(RoCatalogRun.JobPath(r.Category, r.Key))))
                .Concat(categories.Select(c => new RoJob(RoJobKind.CatalogIndex, RoPath.Parse(RoCatalogRun.IndexJobPath(c)))));
            return RoImportApi.Run(jobs, options);
        }

        T Index<T>(SpriteCatalogCategory c) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(RoCatalogOutputPaths.Index(_roots, c));

        static string GuidOf(string path) => AssetDatabase.AssetPathToGUID(path);

        string Art => _roots.Art.TrimEnd('/');

        [Test]
        public void TheUnitIndex_IsSortedByJobId_WithItsRecords()
        {
            var report = Run(SpriteCatalogCategory.Monsters);

            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());
            var index = Index<UnitIndex>(SpriteCatalogCategory.Monsters);
            Assert.That(index.Category, Is.EqualTo(SpriteCatalogCategory.Monsters));
            Assert.That(index.JobIds, Is.EqualTo(new[] { 1002, 1063 }));
            Assert.That(index.Records[0].Name, Is.EqualTo("PORING"));
            Assert.That(index.Records[0].Scale, Is.EqualTo(1.5f));
            Assert.That(index.Records[0].Sprite.Animation.AssetGUID, Is.EqualTo(GuidOf(Art + "/sprite/몬스터/poring.asset")));
            Assert.That(report.Issues.Where(i => i.Code == RoIssueCode.CatalogStrayFile), Is.Empty, "every reference verified");
        }

        [Test]
        public void TheEffectIndex_HasTheEmoteSet_AndActionEqualsId()
        {
            Run(SpriteCatalogCategory.Effects);

            var index = Index<EffectIndex>(SpriteCatalogCategory.Effects);
            Assert.That(index.Names, Is.EqualTo(new[] { "emotion" }));
            Assert.That(index.EmoteIds, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(index.EmoteActions, Is.EqualTo(index.EmoteIds));
            Assert.That(index.EmoteSet.IsEmpty, Is.False);
        }

        [Test]
        public void TheItemTable_HasKindsViewIdsAndGroundSlots()
        {
            Run(SpriteCatalogCategory.GroundItems, SpriteCatalogCategory.Headgears);

            var items = AssetDatabase.LoadAssetAtPath<ItemSpriteTable>(RoCatalogOutputPaths.Items(_roots));
            Assert.That(items.ItemIds, Is.EqualTo(new[] { 501, 1201, 2220 }));
            Assert.That(items.Kinds, Is.EqualTo(new[] { (byte)EquipmentKind.None, (byte)EquipmentKind.Weapon, (byte)EquipmentKind.Headgear }), "kinds from the item database (D21)");
            Assert.That(items.ViewIds, Is.EqualTo(new[] { 0, 1, 16 }));
            Assert.That(items.GroundSlot, Is.EqualTo(new[] { 0, -1, -1 }), "only 빨간포션 has a ground sprite");
            var ground = Index<GroundItemIndex>(SpriteCatalogCategory.GroundItems);
            Assert.That(ground.ResourceNames, Is.EqualTo(new[] { "빨간포션" }));
            Assert.That(ground.Resources[0].Still.SubObjectName, Is.Not.Empty);
        }

        [Test]
        public void TheEquipmentIndex_AndTheClassTable()
        {
            Run(SpriteCatalogCategory.Headgears);

            var index = Index<EquipmentIndex>(SpriteCatalogCategory.Headgears);
            Assert.That(index.Kind, Is.EqualTo(EquipmentKind.Headgear));
            Assert.That(index.ViewIds, Is.EqualTo(new[] { 16 }));
            Assert.That(index.ByView[0].AssetGUID, Is.EqualTo(GuidOf(RoCatalogOutputPaths.Entry(_roots, SpriteCatalogCategory.Headgears, "headgear-0016"))));
            var classes = AssetDatabase.LoadAssetAtPath<ClassTable>(RoCatalogOutputPaths.Classes(_roots));
            Assert.That(classes.RaceOf(PlayerJob.JOB_SUMMONER), Is.EqualTo(SpriteRace.Doram));
            Assert.That(classes.TryGet(PlayerJob.JOB_KNIGHT2, out _, out var baseJob) && baseJob == PlayerJob.JOB_KNIGHT, Is.True);
        }

        [Test]
        public void ASecondRun_RewritesNoIndex()
        {
            var all = new[] { SpriteCatalogCategory.Monsters, SpriteCatalogCategory.GroundItems, SpriteCatalogCategory.Effects, SpriteCatalogCategory.Headgears };
            Run(all);
            var files = all.Select(c => RoCatalogOutputPaths.Index(_roots, c)).Concat(new[] { RoCatalogOutputPaths.Items(_roots), RoCatalogOutputPaths.Classes(_roots) })
                .Select(RoImportTestRoot.FullPath).ToArray();
            var before = files.Select(f => (File.GetLastWriteTimeUtc(f), File.ReadAllBytes(f))).ToArray();

            var report = Run(all);

            Assert.That(report.Created + report.Updated, Is.EqualTo(0), report.Summarize());
            for (int i = 0; i < files.Length; i++)
            {
                Assert.That(File.GetLastWriteTimeUtc(files[i]), Is.EqualTo(before[i].Item1), files[i]);
                Assert.That(File.ReadAllBytes(files[i]), Is.EqualTo(before[i].Item2), files[i]);
            }
        }
    }
}
