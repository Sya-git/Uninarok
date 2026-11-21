using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.8a (FR-4; PLAN-0008 D21): the equipment kind of each item from the server's item_db.conf.
    public class RoItemDbTests
    {
        const string Db = @"//===== Hercules Database ====
/******************************************************************************
 * Type: Item Type (string, defaults to ""IT_ETC"")
 * Loc: ""EQP_HEAD_TOP""
 ******************************************************************************/
item_db: (
{
	Id: 1201
	AegisName: ""Knife""
	Type: ""IT_WEAPON""
	Loc: ""EQP_WEAPON""
	Script: <"" bonus bStr,1; // Id: 9999
	Type: ""IT_ARMOR"" ""&gt;
	Loc: ""EQP_SHIELD"" "">
},
{
	Id: 2101
	Type: ""IT_ARMOR""
	Loc: ""EQP_SHIELD""
	Job: { Novice: true }
},
{
	Id: 2220
	Type: ""IT_ARMOR""
	Loc: ""EQP_HEAD_TOP""
	ViewSprite: 16
},
{
	Id: 5001
	Type: ""IT_ARMOR""
	Loc: [""EQP_HEAD_LOW"", ""EQP_HEAD_MID""]
},
{
	Id: 19500
	Type: ""IT_ARMOR""
	Loc: ""EQP_COSTUME_HEAD_TOP""
},
{
	Id: 2501
	Type: ""IT_ARMOR""
	Loc: ""EQP_GARMENT""
},
{
	Id: 20500
	Type: ""IT_ARMOR""
	Loc: ""EQP_COSTUME_GARMENT""
},
{
	Id: 24000
	Type: ""IT_ARMOR""
	Loc: ""EQP_SHADOW_SHIELD""
},
{
	Id: 2301
	Type: ""IT_ARMOR""
	Loc: ""EQP_ARMOR""
},
{
	Id: 501
	Type: ""IT_HEALING""
},
)";

        static RoItemDb Read()
        {
            Assert.That(RoItemDb.TryRead(Db, out var db, out string reason), Is.True, reason);
            return db;
        }

        [TestCase(1201, EquipmentKind.Weapon)]
        [TestCase(2101, EquipmentKind.Shield)]
        [TestCase(2220, EquipmentKind.Headgear)]
        [TestCase(5001, EquipmentKind.Headgear)]
        [TestCase(19500, EquipmentKind.Headgear)]
        [TestCase(2501, EquipmentKind.Garment)]
        [TestCase(20500, EquipmentKind.Garment)]
        [TestCase(24000, EquipmentKind.None)]
        [TestCase(2301, EquipmentKind.None)]
        [TestCase(501, EquipmentKind.None)]
        [TestCase(424242, EquipmentKind.None)]
        public void TheKindOfAnItem(int id, EquipmentKind kind) => Assert.That(Read().KindOf(id), Is.EqualTo(kind));

        [Test]
        public void CommentsAndScriptsAreSkipped()
        {
            var db = Read();
            Assert.That(db.Count, Is.EqualTo(10), "the comment header and the script's Id: line are not items");
            Assert.That(db.KindOf(9999), Is.EqualTo(EquipmentKind.None));
            Assert.That(db.KindOf(1201), Is.EqualTo(EquipmentKind.Weapon), "the script's Type/Loc lines do not change the item");
        }

        [Test]
        public void EmptyOrItemlessText_IsRejected()
        {
            Assert.That(RoItemDb.TryRead("", out _, out _), Is.False);
            Assert.That(RoItemDb.TryRead("item_db: ( )", out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("Id"));
        }

        [Test]
        public void AMissingFile_IsOneWarning_AndTheGroupIsMissing()
        {
            var report = new RoReport();
            var t = RoCatalogTables.Load(new RoFolderSource(Path.GetTempPath()), null, RoCatalogNeeds.ItemKinds, RoItemTableLanguage.Portuguese, report,
                null, Path.Combine(Path.GetTempPath(), "nope", "item_db.conf"));

            Assert.That(report.Issues.Single().Code, Is.EqualTo(RoIssueCode.SourceMissing));
            Assert.That(t.Missing, Is.EqualTo(RoCatalogNeeds.ItemKinds));
            Assert.That(t.ItemKinds, Is.Null);
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void TheRepositorysHerculesItemDb()
        {
            Assert.That(RoItemDb.TryRead(File.ReadAllText(CatalogTestFiles.ItemDbPath), out var db, out string reason), Is.True, reason);

            Assert.That(db.Count, Is.EqualTo(13182), "every Id entry; the header documents the field with a line of its own");
            Assert.That(db.KindOf(1201), Is.EqualTo(EquipmentKind.Weapon), "Knife");
            Assert.That(db.KindOf(2220), Is.EqualTo(EquipmentKind.Headgear), "Hat");
            Assert.That(db.KindOf(2101), Is.EqualTo(EquipmentKind.Shield), "Guard");
        }
    }
}
