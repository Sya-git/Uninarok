using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Core.Sprites;
using Uninarok.RoImport.Editor.Catalog;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Tests.Sprites;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.5 (FR-8, FR-9, FR-10, FR-12; PLAN-0008 4.7, §6): the catalog importer on a small synthetic client, written
    // to a temporary data and System folder and imported into Assets/__RoImportTests; the ledgers go to a temporary folder.
    public class RoCatalogImporterTests
    {
        const string Tables = "luafiles514/lua files/";

        RoOutputRoots _roots;
        string _temp, _data, _system, _own;
        UnityEditor.AddressableAssets.Settings.AddressableAssetSettings _addressables;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _addressables = UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
            _temp = Path.Combine(Path.GetTempPath(), "RoCatalogImporterTests", Guid.NewGuid().ToString("N"));
            _data = Path.Combine(_temp, "data");
            _system = Path.Combine(_temp, "System");
            _own = Path.Combine(_temp, "own");

            var cp949 = Encoding.GetEncoding(949);
            Text(_data, Tables + "datainfo/npcidentity.lub", "jobtbl = { JT_8W_SOLDIER = 105, JT_PORING = 1002, JT_LUNATIC = 1063, JT_BROKEN = 1100 }", cp949);
            Text(_data, Tables + "datainfo/jobidentity.lub", "JTtbl = { JT_NOVICE = 0, JT_KNIGHT = 7 }", cp949);
            Text(_data, Tables + "datainfo/jobname.lub",
                "JobNameTable = { [jobtbl.JT_8W_SOLDIER] = \"8W_SOLDIER\", [jobtbl.JT_PORING] = \"PORING\", [jobtbl.JT_LUNATIC] = \"LUNATIC\", [jobtbl.JT_BROKEN] = \"BROKEN\" }", cp949);
            Text(_data, Tables + "datainfo/accessoryid.lub", "ACCESSORY_IDs = { ACCESSORY_HAT = 16 }", cp949);
            Text(_data, Tables + "datainfo/accname.lub", "AccNameTable = { [ACCESSORY_IDs.ACCESSORY_HAT] = \"_햇\" }", cp949);
            Text(_data, Tables + "datainfo/spriterobeid.lub", "SPRITE_ROBE_IDs = { }", cp949);
            Text(_data, Tables + "datainfo/spriterobename.lub", "RobeNameTable = { }", cp949);
            Text(_data, Tables + "datainfo/weapontable.lub", "Weapon_IDs = { }\nWeaponNameTable = { }\nExpansion_Weapon_IDs = { }", cp949);
            Text(_data, Tables + "emotion/emotionlist.lub", "ET_SURPRISE = 0\nET_QUESTION = 1", cp949);
            Text(_data, Tables + "skillinfoz/skillid.lub", "SKID = { SM_BASH = 5 }", cp949);
            Scales("[1002] = { MonsterSize = 1.5 }");
            const string hat = "[2220] = { identifiedResourceName = \"햇\", unidentifiedResourceName = \"모자\", ClassNum = 16 }";
            Text(_system, "iteminfo_new.lub", "tbl = { [501] = { identifiedResourceName = \"빨간포션\", unidentifiedResourceName = \"빨간포션\", ClassNum = 0 }, " + hat + " }", Encoding.UTF8);
            Text(_system, "english/iteminfo_new.lub", "tbl = { [501] = { identifiedResourceName = \"red_potion\", unidentifiedResourceName = \"red_potion\", ClassNum = 0 }, " + hat + " }", Encoding.UTF8);
            Text(_own, RoOwnTables.ClassBodiesFile, "BodyName,Job,Race,Kind,BaseJob\n초보자,JOB_NOVICE,Human,Base,JOB_NOVICE\n", Encoding.UTF8);
            Text(_own, RoOwnTables.ShieldNamesFile, "ViewId,Name\n1,가드\n", Encoding.UTF8);
            Text(_own, "item_db.conf", "item_db: (\n{\n\tId: 501\n\tType: \"IT_HEALING\"\n},\n{\n\tId: 2220\n\tType: \"IT_ARMOR\"\n\tLoc: \"EQP_HEAD_TOP\"\n},\n)", Encoding.UTF8);

            Pair("sprite/몬스터/poring", SpriteTestData.Monster());
            Pair("sprite/몬스터/lunatic", SpriteTestData.Weapon());
            Pair("sprite/npc/8w_soldier", SpriteTestData.Anchored());
            File(_data, "sprite/몬스터/broken.spr", new byte[] { (byte)'S', (byte)'P', 1, 2, 9, 0, 0, 0, 1, 2, 3 });
            File(_data, "sprite/몬스터/broken.act", SpriteFileWriter.Act(SpriteTestData.Static().act));
            Pair("sprite/아이템/빨간포션", SpriteTestData.Static());
            Pair("sprite/아이템/red_potion", SpriteTestData.Static(1, 1));
            Pair("sprite/아이템/sm_bash", SpriteTestData.Static(2, 2));
            Pair("sprite/이팩트/emotion", SpriteTestData.Mixed());
            // The headgear's sheet is a byte copy of Poring's: imported after monsters it reuses that sheet (D2).
            Pair("sprite/악세사리/남/남_햇", SpriteTestData.Monster());
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_temp)) Directory.Delete(_temp, recursive: true);
        }

        static void Text(string root, string relative, string text, Encoding encoding) => File(root, relative, encoding.GetBytes(text));

        static void File(string root, string relative, byte[] bytes)
        {
            string path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            System.IO.File.WriteAllBytes(path, bytes);
        }

        void Pair(string stem, (RoSpr spr, RoAct act) sample)
        {
            File(_data, stem + ".spr", SpriteFileWriter.Spr(sample.spr));
            File(_data, stem + ".act", SpriteFileWriter.Act(sample.act));
        }

        void Scales(string body) => Text(_system, "monster_size_effect_new.lub", $"tbl = {{ {body} }}", Encoding.GetEncoding(949));

        RoImportOptions Options(bool force = false, RoItemTableLanguage language = RoItemTableLanguage.Portuguese) => new RoImportOptions
        {
            Force = force,
            ShowProgress = false,
            Roots = _roots,
            Source = new RoFolderSource(_data),
            SystemSource = new RoFolderSource(_system),
            LedgerFolder = Path.Combine(_temp, "ledger"),
            ItemTableLanguage = language,
            CatalogOwnTablesFolder = _own,
            ItemDbPath = Path.Combine(_own, "item_db.conf"),
            CatalogAddressables = _addressables,
        };

        RoCatalogReport Import(RoImportOptions options, params SpriteCatalogCategory[] categories) => RoImportApi.Catalog(categories, options);

        string Art => _roots.Art.TrimEnd('/');

        string[] Sheets() =>
            Directory.Exists(RoImportTestRoot.FullPath(Art + "/sprite/_sheets"))
                ? Directory.GetFiles(RoImportTestRoot.FullPath(Art + "/sprite/_sheets"), "*.png", SearchOption.AllDirectories).Where(f => !f.EndsWith("_index.png")).ToArray()
                : Array.Empty<string>();

        bool ExistsInProject(string projectPath) => System.IO.File.Exists(RoImportTestRoot.FullPath(projectPath));

        [Test]
        public void OneCategory_ImportsOnlyItsOwnSheets()
        {
            var result = Import(Options(), SpriteCatalogCategory.Monsters);

            Assert.That(result.Of(SpriteCatalogCategory.Monsters).Entries, Is.EqualTo(2), "poring and lunatic; broken failed");
            Assert.That(Sheets().Length, Is.EqualTo(2));
            Assert.That(ExistsInProject(Art + "/sprite/아이템/빨간포션.asset"), Is.False, "no item sprite");
            Assert.That(ExistsInProject(Art + "/sprite/npc/8w_soldier.asset"), Is.False, "no NPC sprite");
            Assert.That(ExistsInProject(RoCatalogOutputPaths.Index(_roots, SpriteCatalogCategory.Monsters)), Is.True);
        }

        [Test]
        public void TheSecondRun_IsSkipped_AndReportsTheSameNumbers()
        {
            var first = Import(Options(), SpriteCatalogCategory.Monsters, SpriteCatalogCategory.Effects);
            var second = Import(Options(), SpriteCatalogCategory.Monsters, SpriteCatalogCategory.Effects);

            Assert.That(second.Report.Created + second.Report.Updated, Is.EqualTo(0), second.Summarize());
            Assert.That(second.Report.Skipped, Is.GreaterThanOrEqualTo(3));
            foreach (var c in new[] { SpriteCatalogCategory.Monsters, SpriteCatalogCategory.Effects })
            {
                Assert.That(second.Of(c).Entries, Is.EqualTo(first.Of(c).Entries), c.ToString());
                Assert.That(second.Of(c).Sheets, Is.EqualTo(first.Of(c).Sheets), c.ToString());
                Assert.That(second.Of(c).Actions, Is.EqualTo(first.Of(c).Actions), c.ToString());
            }
            Assert.That(first.Of(SpriteCatalogCategory.Monsters).Sheets, Is.EqualTo(2));
            Assert.That(first.Of(SpriteCatalogCategory.Monsters).Actions, Is.GreaterThan(0));
        }

        [Test]
        public void ASecondCategory_ReusesASharedSheet()
        {
            Import(Options(), SpriteCatalogCategory.Monsters);
            int before = Sheets().Length;

            var result = Import(Options(), SpriteCatalogCategory.Headgears);

            Assert.That(result.Of(SpriteCatalogCategory.Headgears).Entries, Is.EqualTo(1));
            Assert.That(Sheets().Length, Is.EqualTo(before), "the hat's SPR is a byte copy of Poring's: no new sheet");
            Assert.That(result.Report.Created, Is.GreaterThan(0), "the hat's animation and entry are new");
        }

        [Test]
        public void AChangedTable_ReimportsOnlyTheRecordsItTouches()
        {
            Import(Options(), SpriteCatalogCategory.Monsters, SpriteCatalogCategory.GroundItems);
            Scales("[1002] = { MonsterSize = 2 }");

            var result = Import(Options(), SpriteCatalogCategory.Monsters, SpriteCatalogCategory.GroundItems);

            Assert.That(result.Report.Updated, Is.EqualTo(2), "poring's record and the monster index; " + result.Summarize());
        }

        [Test]
        public void TheLanguageSetting_ReimportsOnlyTheItemRecords()
        {
            Import(Options(), SpriteCatalogCategory.Monsters, SpriteCatalogCategory.GroundItems);

            var result = Import(Options(language: RoItemTableLanguage.English), SpriteCatalogCategory.Monsters, SpriteCatalogCategory.GroundItems);

            Assert.That(result.Of(SpriteCatalogCategory.GroundItems).Entries, Is.EqualTo(1));
            var ground = AssetDatabase.LoadAssetAtPath<GroundItemIndex>(RoCatalogOutputPaths.Index(_roots, SpriteCatalogCategory.GroundItems));
            Assert.That(ground.ResourceNames, Is.EqualTo(new[] { "red_potion" }), "the English resource");
            Assert.That(result.Report.Created + result.Report.Updated, Is.EqualTo(2),
                "red_potion's record and the ground item index; the monsters stay current; " + result.Summarize());
        }

        [Test]
        public void AFailingPair_FailsOneRecordOnly()
        {
            var result = Import(Options(), SpriteCatalogCategory.Monsters);

            Assert.That(result.Report.Failed, Is.EqualTo(1));
            Assert.That(result.Report.Failures.Single().Referrer, Is.EqualTo("catalog/monsters/1100"));
            Assert.That(result.Of(SpriteCatalogCategory.Monsters).Failed, Is.EqualTo(1));
            var index = AssetDatabase.LoadAssetAtPath<UnitIndex>(RoCatalogOutputPaths.Index(_roots, SpriteCatalogCategory.Monsters));
            Assert.That(index.JobIds, Is.EqualTo(new[] { 1002, 1063 }), "no partial record");
        }

        [Test]
        public void EntriesLandInTheirCategoryFolders()
        {
            var result = Import(Options(), SpriteCatalogCategory.Headgears, SpriteCatalogCategory.SkillIcons);

            Assert.That(ExistsInProject(RoCatalogOutputPaths.Entry(_roots, SpriteCatalogCategory.Headgears, "headgear-0016")), Is.True);
            Assert.That(ExistsInProject(RoCatalogOutputPaths.Index(_roots, SpriteCatalogCategory.Headgears)), Is.True);
            Assert.That(ExistsInProject(RoCatalogOutputPaths.Index(_roots, SpriteCatalogCategory.SkillIcons)), Is.True);
            Assert.That(ExistsInProject(RoCatalogOutputPaths.Items(_roots)), Is.True, "headgears need the item table");
            Assert.That(result.Of(SpriteCatalogCategory.SkillIcons).Entries, Is.EqualTo(1));
        }

        // TASKS-0008 T5.10 (FR-9; PLAN-0008 4.7): the window's Import is the code path (PLAN-0002 4.11).
        // TASKS-0008 T6.1 (FR-11; PLAN-0008 4.9): the lookup preview through the facade over the imported indices.
        [Test]
        public void TheLookup_FindsItem2220ForAMaleKnight_AsTheHatOnItsOwn()
        {
            Import(Options(), SpriteCatalogCategory.Headgears, SpriteCatalogCategory.Monsters, SpriteCatalogCategory.SkillIcons);

            var result = RoCatalogLookupWindow.Resolve(new RoCatalogLookupRequest
            {
                Mode = RoCatalogLookupMode.ItemId, Id = 2220, Job = Uninarok.Networking.Data.PlayerJob.JOB_KNIGHT, Sex = Uninarok.Networking.Data.AccountSex.SEX_MALE,
            }, _roots, out string message);

            Assert.That(result?.Animation, Is.Not.Null, message);
            Assert.That(AssetDatabase.GetAssetPath(result.Animation), Is.EqualTo(Art + "/sprite/악세사리/남/남_햇.asset"), "the hat's animation, on its own");
            Assert.That(message, Does.Contain("Headgear").And.Contain("_햇"));

            // Evidence of T6.1: one frame of the result, as the preview window draws it.
            var window = Uninarok.RoImport.Editor.Sprites.RoSpritePreviewWindow.Open(result.Animation);
            string png = Path.Combine(Path.GetTempPath(), "RoCatalogLookup-item2220-knight-male.png");
            window.SaveFrame(png, 0, 0, 0f);
            window.Close();
            Assert.That(System.IO.File.Exists(png), Is.True);
            TestContext.WriteLine("evidence: " + png);

            var monster = RoCatalogLookupWindow.Resolve(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.UnitJobId, Id = 1002 }, _roots, out _);
            Assert.That(monster?.Animation, Is.Not.Null, "a unit");
            var icon = RoCatalogLookupWindow.Resolve(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.SkillId, Id = 5 }, _roots, out _);
            Assert.That(icon?.Still, Is.Not.Null, "a static result shows the sprite");
            Assert.That(RoCatalogLookupWindow.Resolve(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.UnitJobId, Id = 9999 }, _roots, out string miss), Is.Null);
            Assert.That(miss, Does.Contain("Nothing found"));
        }

        [Test]
        public void TheLookup_SaysSoWhenTheCatalogIsNotImported()
        {
            Assert.That(RoCatalogLookupWindow.Resolve(new RoCatalogLookupRequest { Mode = RoCatalogLookupMode.ItemId, Id = 2220 }, _roots, out string message), Is.Null);
            Assert.That(message, Does.Contain("not imported"));
        }

        [Test]
        public void TheWindowAndTheCodePath_GiveTheSameReport()
        {
            var categories = new[] { SpriteCatalogCategory.Monsters, SpriteCatalogCategory.SkillIcons };
            Import(Options(), categories);

            var fromCode = Import(Options(), categories);
            var fromWindow = RoCatalogWindow.Import(categories, force: false, Options());

            Assert.That(fromWindow.Categories.Select(c => (c.Category, c.Entries, c.Sheets, c.Actions, c.Warnings, c.Failed, c.Skipped)),
                Is.EqualTo(fromCode.Categories.Select(c => (c.Category, c.Entries, c.Sheets, c.Actions, c.Warnings, c.Failed, c.Skipped))));
            Assert.That((fromWindow.Report.Created, fromWindow.Report.Updated, fromWindow.Report.Skipped, fromWindow.Report.Failed),
                Is.EqualTo((fromCode.Report.Created, fromCode.Report.Updated, fromCode.Report.Skipped, fromCode.Report.Failed)));
        }

        [Test]
        public void ImportingOnlyMonsters_WritesNoEquipmentAsset()
        {
            Import(Options(), SpriteCatalogCategory.Monsters);

            foreach (var c in new[] { SpriteCatalogCategory.Headgears, SpriteCatalogCategory.Garments, SpriteCatalogCategory.Weapons, SpriteCatalogCategory.Shields })
                Assert.That(Directory.Exists(RoImportTestRoot.FullPath(RoCatalogOutputPaths.Folder(_roots, c)))
                    && Directory.GetFiles(RoImportTestRoot.FullPath(RoCatalogOutputPaths.Folder(_roots, c)), "*.asset").Length > 0, Is.False, c.ToString());
            Assert.That(ExistsInProject(Art + "/sprite/악세사리/남/남_햇.asset"), Is.False);
        }
    }
}
