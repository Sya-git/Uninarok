using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.9 (FR-1 to FR-7, FR-10, FR-12; PLAN-0008 4.2): loading only the tables the categories need.
    public class RoCatalogTablesTests
    {
        /// <summary>A source that records every file asked for.</summary>
        sealed class Recording : IRoSource
        {
            readonly IRoSource _inner;
            public readonly List<string> Asked = new List<string>();
            public Recording(IRoSource inner) => _inner = inner;

            public bool TryGetFile(RoPath path, out RoSourceFile file)
            {
                Asked.Add(path.Canonical);
                return _inner.TryGetFile(path, out file);
            }

            public IEnumerable<RoPath> Enumerate(string folderPrefix) => _inner.Enumerate(folderPrefix);
        }

        string _root;
        Recording _data, _system;
        string _own;

        static void Write(string root, string relative, string text, Encoding encoding)
        {
            string file = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? root);
            File.WriteAllBytes(file, encoding.GetBytes(text));
        }

        [SetUp]
        public void CreateSources()
        {
            _root = Path.Combine(Path.GetTempPath(), "RoCatalogTablesTests", Guid.NewGuid().ToString("N"));
            string data = Path.Combine(_root, "data"), system = Path.Combine(_root, "System");
            _own = Path.Combine(_root, "own");
            var cp949 = Encoding.GetEncoding(949);
            const string t = "luafiles514/lua files/";
            Write(data, t + "datainfo/npcidentity.lub", "jobtbl = { JT_PORING = 1002 }", cp949);
            Write(data, t + "datainfo/jobidentity.lub", "JTtbl = { JT_KNIGHT = 7 }", cp949);
            Write(data, t + "datainfo/jobname.lub", "JobNameTable = { [jobtbl.JT_PORING] = \"PORING\" }", cp949);
            Write(data, t + "datainfo/accessoryid.lub", "ACCESSORY_IDs = { ACCESSORY_HAT = 16 }", cp949);
            Write(data, t + "datainfo/accname.lub", "AccNameTable = { [ACCESSORY_IDs.ACCESSORY_HAT] = \"_햇\" }", cp949);
            Write(data, t + "datainfo/spriterobeid.lub", "SPRITE_ROBE_IDs = { ROBE_WINGS = 1 }", cp949);
            Write(data, t + "datainfo/spriterobename.lub", "RobeNameTable = { [SPRITE_ROBE_IDs.ROBE_WINGS] = \"천사날개\" }", cp949);
            Write(data, t + "datainfo/weapontable.lub",
                "Weapon_IDs = { WEAPONTYPE_SHORTSWORD = 1 }\nWeaponNameTable = { [Weapon_IDs.WEAPONTYPE_SHORTSWORD] = \"_단검\" }\nExpansion_Weapon_IDs = {}", cp949);
            Write(data, t + "emotion/emotionlist.lub", "ET_SURPRISE = 0", cp949);
            Write(data, t + "skillinfoz/skillid.lub", "SKID = { SM_BASH = 5 }", cp949);
            Write(system, "monster_size_effect_new.lub", "tbl = { [1002] = { MonsterSize = 1.2 } }", cp949);
            Write(system, "iteminfo_new.lub", "tbl = { [501] = { identifiedResourceName = \"빨간포션\", ClassNum = 0 } }", Encoding.UTF8);
            Write(system, "english/iteminfo_new.lub", "tbl = { [501] = { identifiedResourceName = \"red\", ClassNum = 0 } }", Encoding.UTF8);
            Write(_own, RoOwnTables.ClassBodiesFile, "BodyName,Job,Race,Kind,BaseJob\n기사,JOB_KNIGHT,Human,Base,JOB_KNIGHT\n", Encoding.UTF8);
            Write(_own, RoOwnTables.ShieldNamesFile, "ViewId,Name\n1,가드\n", Encoding.UTF8);
            Write(_own, "item_db.conf", "item_db: (\n{\n\tId: 1201\n\tType: \"IT_WEAPON\"\n\tLoc: \"EQP_WEAPON\"\n},\n)", Encoding.UTF8);
            _data = new Recording(new RoFolderSource(data));
            _system = new Recording(new RoFolderSource(system));
        }

        [TearDown]
        public void DeleteSources()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        RoCatalogTables Load(RoCatalogNeeds needs, RoReport report, RoItemTableLanguage language = RoItemTableLanguage.Portuguese) =>
            RoCatalogTables.Load(_data, _system, needs, language, report, _own, Path.Combine(_own, "item_db.conf"));

        [Test]
        public void LoadsEveryGroup()
        {
            var report = new RoReport();
            var t = Load(RoCatalogNeeds.All, report);
            Assert.That(report.Issues, Is.Empty, string.Join("\n", report.Issues));
            Assert.That(t.Has(RoCatalogNeeds.All), Is.True);
            Assert.That(t.Missing, Is.EqualTo(RoCatalogNeeds.None));
            Assert.That(t.Jobs.SpriteNames[1002], Is.EqualTo("PORING"));
            Assert.That(t.ViewIds.HeadgearStems[16], Is.EqualTo("_햇"));
            Assert.That(t.ViewIds.GarmentFolders[1], Is.EqualTo("천사날개"));
            Assert.That(t.Items.TryGet(501, out var potion) ? potion.IdentifiedResource : null, Is.EqualTo("빨간포션"));
            Assert.That(t.Emotes.Ids["ET_SURPRISE"], Is.EqualTo(0));
            Assert.That(t.Skills.Ids["SM_BASH"], Is.EqualTo(5));
            Assert.That(t.Scales.ScaleOf(1002), Is.EqualTo(1.2f));
            Assert.That(t.Own.ShieldNames[1], Is.EqualTo("가드"));
            Assert.That(t.ItemKinds.KindOf(1201), Is.EqualTo(Uninarok.Catalog.EquipmentKind.Weapon));
        }

        [Test]
        public void JobsAloneReadNoItemTable()
        {
            var t = Load(RoCatalogNeeds.Jobs, new RoReport());
            Assert.That(t.Has(RoCatalogNeeds.Jobs), Is.True);
            Assert.That(t.Items, Is.Null);
            Assert.That(_system.Asked, Is.Empty, "the System source is not touched");
            Assert.That(_data.Asked.All(p => p.Contains("datainfo/") && (p.Contains("identity") || p.Contains("jobname"))), Is.True,
                string.Join(", ", _data.Asked));
        }

        [Test]
        public void TheLanguageChoosesTheItemTable()
        {
            var t = Load(RoCatalogNeeds.Items, new RoReport(), RoItemTableLanguage.English);
            Assert.That(t.ItemLanguage, Is.EqualTo(RoItemTableLanguage.English));
            Assert.That(t.Items.TryGet(501, out var potion) ? potion.IdentifiedResource : null, Is.EqualTo("red"));
            Assert.That(_system.Asked, Is.EqualTo(new[] { "english/iteminfo_new.lub" }));
        }

        [Test]
        public void ADeletedTableIsOneIssueAndOnlyItsGroupIsMissing()
        {
            File.Delete(Path.Combine(_root, "data", "luafiles514", "lua files", "datainfo", "accname.lub"));
            var report = new RoReport();
            var t = Load(RoCatalogNeeds.All, report);
            Assert.That(report.Issues.Count, Is.EqualTo(1), string.Join("\n", report.Issues));
            Assert.That(report.Issues[0].Code, Is.EqualTo(RoIssueCode.SourceMissing));
            Assert.That(report.Issues[0].Item, Does.Contain("accname.lub"));
            Assert.That(t.Missing, Is.EqualTo(RoCatalogNeeds.ViewIds));
            Assert.That(t.Has(RoCatalogNeeds.ViewIds), Is.False, "headgears, garments and weapons are skipped");
            Assert.That(t.Has(RoCatalogNeeds.Jobs | RoCatalogNeeds.Items | RoCatalogNeeds.Own), Is.True, "the other groups load");
        }

        [Test]
        public void ABrokenTableIsACorruptFile()
        {
            Write(Path.Combine(_root, "data"), "luafiles514/lua files/skillinfoz/skillid.lub", "SKID = { SM_BASH = 1 + 1 }", Encoding.ASCII);
            var report = new RoReport();
            var t = Load(RoCatalogNeeds.Skills | RoCatalogNeeds.Emotes, report);
            Assert.That(report.Issues.Single().Code, Is.EqualTo(RoIssueCode.CorruptFile));
            Assert.That(report.Issues.Single().Message, Does.Contain("line 1"));
            Assert.That(t.Missing, Is.EqualTo(RoCatalogNeeds.Skills));
            Assert.That(t.Emotes, Is.Not.Null);
        }

        [Test]
        public void NoSystemFolderMissesOnlyItsTables()
        {
            var report = new RoReport();
            var t = RoCatalogTables.Load(_data, null, RoCatalogNeeds.Jobs | RoCatalogNeeds.Items | RoCatalogNeeds.Scales,
                RoItemTableLanguage.Portuguese, report, _own);
            Assert.That(t.Missing, Is.EqualTo(RoCatalogNeeds.Items | RoCatalogNeeds.Scales));
            Assert.That(report.Issues.Count, Is.EqualTo(2));
            Assert.That(t.Jobs, Is.Not.Null);
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void LoadsTheClientTables()
        {
            var report = new RoReport();
            var t = RoCatalogTables.Load(new RoFolderSource(CatalogTestFiles.DataRoot), new RoFolderSource(CatalogTestFiles.SystemRoot),
                RoCatalogNeeds.All, RoItemTableLanguage.Portuguese, report, null, CatalogTestFiles.ItemDbPath);
            Assert.That(report.Issues, Is.Empty, string.Join("\n", report.Issues));
            Assert.That(t.Jobs.SpriteNames.Count, Is.EqualTo(5300));
            Assert.That(t.Items.Count, Is.EqualTo(17482));
            Assert.That(t.Skills.Ids.Count, Is.EqualTo(1806));
            Assert.That(t.ViewIds.HeadgearConstants.Count, Is.EqualTo(2859));
            Assert.That(t.Own.Bodies.Count, Is.GreaterThan(100));
        }
    }
}
