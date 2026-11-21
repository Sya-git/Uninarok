using System;
using System.Diagnostics;
using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Formats;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.6 (FR-4, FR-5, FR-12; PLAN-0008 4.2, 4.8): the item table, three fields kept.
    public class RoItemTableTests
    {
        const string Items =
            "tbl = {\n" +
            "\t[1201] = {\n\t\tunidentifiedDisplayName = \"Faca\",\n\t\tunidentifiedResourceName = \"나이프\",\n" +
            "\t\tunidentifiedDescriptionName = { \"a\", \"b\" },\n\t\tidentifiedResourceName = \"나이프\",\n\t\tslotCount = 3,\n\t\tClassNum = 1,\n\t\tcostume = false\n\t},\n" +
            "\t[501] = {\n\t\tunidentifiedResourceName = \"빨간포션\",\n\t\tidentifiedResourceName = \"빨간포션\",\n\t\tClassNum = 0\n\t},\n" +
            "\t[9] = { identifiedDisplayName = \"no sprite\" }\n" +
            "}";

        [Test]
        public void KeepsTheThreeFieldsAndSortsTheIds()
        {
            Assert.That(RoItemTable.TryRead(Items, "iteminfo_new.lub", out var t, out string reason), Is.True, reason);
            CollectionAssert.AreEqual(new[] { 9, 501, 1201 }, t.ItemIds);
            Assert.That(t.TryGet(1201, out var knife), Is.True);
            Assert.That(knife.IdentifiedResource, Is.EqualTo("나이프"));
            Assert.That(knife.ClassNum, Is.EqualTo(1));
            Assert.That(ReferenceEquals(knife.IdentifiedResource, knife.UnidentifiedResource), Is.True, "resource names are shared");
            Assert.That(t.TryGet(9, out var none) && none.IdentifiedResource == null && none.ClassNum == 0, Is.True);
            Assert.That(t.TryGet(12345, out _), Is.False);
        }

        [TestCase(RoItemTableLanguage.Portuguese, "iteminfo_new.lub")]
        [TestCase(RoItemTableLanguage.English, "english/iteminfo_new.lub")]
        [TestCase(RoItemTableLanguage.Spanish, "spanish/iteminfo_new.lub")]
        public void EachLanguageHasItsFile(RoItemTableLanguage language, string file)
        {
            Assert.That(RoItemTable.FileName(language), Is.EqualTo(file));
        }

        [Test]
        public void TheTableMustBeNamedTbl()
        {
            Assert.That(RoItemTable.TryRead("itemInfo = {}", "x.lub", out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("tbl"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ReadsThePortugueseTable()
        {
            string lua = CatalogTestFiles.SystemLua(RoItemTable.FileName(RoItemTableLanguage.Portuguese), RoLubEncoding.Utf8);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            long before = GC.GetTotalMemory(true);
            var watch = Stopwatch.StartNew();
            Assert.That(RoItemTable.TryRead(lua, "iteminfo_new.lub", out var t, out string reason), Is.True, reason);
            watch.Stop();
            long after = GC.GetTotalMemory(true);

            Assert.That(t.Count, Is.EqualTo(17482));
            Assert.That(t.TryGet(501, out var potion) ? potion.IdentifiedResource : null, Is.EqualTo("빨간포션"));
            Assert.That(t.TryGet(1201, out var knife) ? knife.ClassNum : -1, Is.EqualTo(1));
            Assert.That(t.TryGet(2220, out var hat) ? hat.ClassNum : -1, Is.EqualTo(16));
            TestContext.WriteLine($"Portuguese: {t.Count} items, parse {watch.ElapsedMilliseconds} ms, retained {(after - before) / 1024} KB");
            GC.KeepAlive(t);
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ReadsTheEnglishTable()
        {
            string lua = CatalogTestFiles.SystemLua(RoItemTable.FileName(RoItemTableLanguage.English), RoLubEncoding.Utf8);
            Assert.That(RoItemTable.TryRead(lua, "english/iteminfo_new.lub", out var t, out string reason), Is.True, reason);
            Assert.That(t.Count, Is.EqualTo(17906));
            Assert.That(t.TryGet(501, out var potion) ? potion.IdentifiedResource : null, Is.EqualTo("빨간포션"));
        }
    }
}
