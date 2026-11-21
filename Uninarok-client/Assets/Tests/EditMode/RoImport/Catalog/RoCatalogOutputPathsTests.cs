using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Catalog;
using Uninarok.RoImport.Editor.Ledger;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.3 (FR-8; PLAN-0008 §3, 4.6): the catalog output paths, the reserved folder and the collision rule.
    public class RoCatalogOutputPathsTests
    {
        static RoOutputRoots Default => new RoOutputRoots();

        [Test]
        public void ThePathsOfPlanSection3()
        {
            Assert.That(RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Headgears, "headgear-0016"), Is.EqualTo("Assets/Art/Imported/catalog/headgears/headgear-0016.asset"));
            Assert.That(RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Bodies, "body-0007"), Is.EqualTo("Assets/Art/Imported/catalog/bodies/body-0007.asset"));
            Assert.That(RoCatalogOutputPaths.Index(Default, SpriteCatalogCategory.GroundItems), Is.EqualTo("Assets/Art/Imported/catalog/ground-items/_index.asset"));
            Assert.That(RoCatalogOutputPaths.Items(Default), Is.EqualTo("Assets/Art/Imported/catalog/items/_index.asset"));
            Assert.That(RoCatalogOutputPaths.Classes(Default), Is.EqualTo("Assets/Art/Imported/catalog/classes/_index.asset"));
            Assert.That(RoCatalogOutputPaths.IndexedCategories.Count, Is.EqualTo(12));
        }

        [Test]
        public void WithTheDefaultRoots_ThePathsAreTheRuntimeKeys()
        {
            foreach (var c in RoCatalogOutputPaths.IndexedCategories)
                Assert.That(RoCatalogOutputPaths.Index(Default, c), Is.EqualTo(SpriteCatalogKeys.Index(c)), c.ToString());
            Assert.That(RoCatalogOutputPaths.Items(Default), Is.EqualTo(SpriteCatalogKeys.Items()));
            Assert.That(RoCatalogOutputPaths.Classes(Default), Is.EqualTo(SpriteCatalogKeys.Classes()));
        }

        [Test]
        public void OtherRootsMoveEverything()
        {
            var roots = RoOutputRoots.Under("Assets/__T");
            Assert.That(RoCatalogOutputPaths.Index(roots, SpriteCatalogCategory.Monsters), Does.StartWith(roots.Art.TrimEnd('/') + "/catalog/monsters/"));
        }

        [Test]
        public void InlineCategoriesHaveNoEntryAssets_AndKeysAreNotPaths()
        {
            Assert.Throws<ArgumentException>(() => RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Monsters, "1002"), "units are inline (D5)");
            Assert.Throws<ArgumentException>(() => RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Heads, "_index"));
            Assert.Throws<ArgumentException>(() => RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Heads, "../x"));
            Assert.Throws<ArgumentException>(() => RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Heads, "머리"));
            Assert.Throws<ArgumentException>(() => RoCatalogOutputPaths.Index(Default, SpriteCatalogCategory.Palettes));
        }

        [Test]
        public void TheCatalogFolderIsReserved()
        {
            Assert.That(RoCatalogOutputPaths.IsReserved(Default, "Assets/Art/Imported/catalog/monsters/_index.asset"), Is.True);
            Assert.That(RoCatalogOutputPaths.IsReserved(Default, "Assets/Art/Imported/catalogue/x.asset"), Is.False);
            Assert.That(RoCatalogOutputPaths.IsReserved(Default, "Assets/Art/Imported/sprite/npc/a.asset"), Is.False);
        }

        [Test]
        public void TwoRecordsCannotOwnOneEntry()
        {
            string folder = Path.Combine(Path.GetTempPath(), "RoCatalogOutputPathsTests", Guid.NewGuid().ToString("N"));
            try
            {
                var ledger = RoLedger.Load(RoCatalogEntryConverter.LedgerCategory, folder);
                string output = RoCatalogOutputPaths.Entry(Default, SpriteCatalogCategory.Headgears, "headgear-0016");
                var report = new RoReport();

                Assert.That(ledger.Record(new RoLedgerEntry { Source = "catalog/headgears/headgear-0016", SourceHash = "a", Outputs = new[] { output } }, report), Is.True);
                Assert.That(ledger.Record(new RoLedgerEntry { Source = "catalog/headgears/other", SourceHash = "b", Outputs = new[] { output } }, report), Is.False);
                Assert.That(report.Issues.Single().Code, Is.EqualTo(RoIssueCode.OutputCollision));
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }
    }
}
