using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.4 (FR-1, FR-2; PLAN-0008 4.2): jobtbl, JTtbl and JobNameTable, read in dependency order.
    public class RoJobTablesTests
    {
        const string Npc = "jobtbl = {\n\tJT_WARPNPC = 45,\n\tJT_PORING = 1002\n}";
        const string Player = "JTtbl = {\n\tJT_NOVICE = 0,\n\tJT_KNIGHT = 7\n}";
        const string Names = "JobNameTable = {\n\t[jobtbl.JT_WARPNPC] = \"1_ETC_01\",\n\t[jobtbl.JT_PORING] = \"PORING\"\n}";

        [Test]
        public void ResolvesNamesThroughTheConstantsOfTheEarlierFiles()
        {
            Assert.That(RoJobTables.TryRead(Npc, Player, Names, out var t, out string reason), Is.True, reason);
            Assert.That(t.TryGetSpriteName(1002, out string poring) ? poring : null, Is.EqualTo("PORING"));
            Assert.That(t.SpriteNames[45], Is.EqualTo("1_ETC_01"));
            Assert.That(t.NpcConstants["JT_PORING"], Is.EqualTo(1002));
            Assert.That(t.PlayerConstants["JT_KNIGHT"], Is.EqualTo(7));
        }

        [Test]
        public void TheNameTableNeedsTheIdentityTable()
        {
            // Read in the wrong order (the names before jobtbl exists), the reference cannot resolve.
            Assert.That(RoJobTables.TryRead(Names, Player, Npc, out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain(RoJobTables.NpcIdentityFile));
        }

        [Test]
        public void ABrokenFileIsNamed()
        {
            Assert.That(RoJobTables.TryRead(Npc, "JTtbl = { JT_X = 1 + 1 }", Names, out _, out string reason), Is.False);
            Assert.That(reason, Does.StartWith(RoJobTables.JobIdentityFile).And.Contain("line 1"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ReadsTheClientTables()
        {
            bool ok = RoJobTables.TryRead(
                CatalogTestFiles.TableLua("datainfo/npcidentity.lub"),
                CatalogTestFiles.TableLua("datainfo/jobidentity.lub"),
                CatalogTestFiles.TableLua("datainfo/jobname.lub"),
                out var t, out string reason);
            Assert.That(ok, Is.True, reason);
            Assert.That(t.SpriteNames.Count, Is.EqualTo(5300), "T0.4 count");
            // The table's case varies ("Poring" for 1002); file names are matched lower-cased (RoPath), so compare without case.
            Assert.That(t.SpriteNames[1002], Is.EqualTo("PORING").IgnoreCase);
            Assert.That(t.SpriteNames[105], Is.EqualTo("8W_SOLDIER").IgnoreCase);
            Assert.That(t.SpriteNames[2245], Is.EqualTo("8W_SOLDIER").IgnoreCase);
            Assert.That(t.PlayerConstants["JT_KNIGHT"], Is.EqualTo(7));
            TestContext.WriteLine($"jobtbl {t.NpcConstants.Count}, JTtbl {t.PlayerConstants.Count}, JobNameTable {t.SpriteNames.Count}");
        }
    }
}
