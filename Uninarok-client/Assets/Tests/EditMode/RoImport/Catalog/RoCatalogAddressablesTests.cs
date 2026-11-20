using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Catalog;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T5.4 (FR-8; PLAN-0008 4.6, D13): groups and folder entries, and the reference check. The settings are a
    // throwaway asset under the test root, never the project's.
    public class RoCatalogAddressablesTests
    {
        RoOutputRoots _roots;
        AddressableAssetSettings _settings;

        [SetUp]
        public void CreateSettings()
        {
            _roots = RoImportTestRoot.Create();
            _settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
        }

        [TearDown]
        public void Delete() => RoImportTestRoot.Delete();

        string Art => _roots.Art.TrimEnd('/');

        /// <summary>A single-sprite PNG: its sprite sub-asset is named after the file.</summary>
        string Sheet(string projectPath)
        {
            Directory.CreateDirectory(RoImportTestRoot.FullPath(Path.GetDirectoryName(projectPath)));
            var t = new Texture2D(4, 4);
            File.WriteAllBytes(RoImportTestRoot.FullPath(projectPath), t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(projectPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(projectPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            return AssetDatabase.AssetPathToGUID(projectPath);
        }

        [Test]
        public void Ensure_CreatesTheGroupsAndFolderEntries_Once()
        {
            Sheet(Art + "/sprite/_sheets/ab/poring_ab12cd34.png");

            Assert.That(RoCatalogAddressables.Ensure(_settings, _roots), Is.GreaterThan(0));
            Assert.That(RoCatalogAddressables.Ensure(_settings, _roots), Is.EqualTo(0), "idempotent");

            var names = _settings.groups.Where(g => g != null).Select(g => g.Name).ToArray();
            Assert.That(names.Count(n => n == "catalog-monsters"), Is.EqualTo(1));
            Assert.That(names, Does.Contain("catalog-tables").And.Contain("sprite-sheets").And.Contain("palettes").And.Contain("sprites-monster"));
            Assert.That(names.Count(n => n.StartsWith("catalog-") && n != "catalog-tables"), Is.EqualTo(12));
            var monsters = _settings.FindGroup("catalog-monsters").entries.Single();
            Assert.That(monsters.AssetPath, Is.EqualTo(Art + "/catalog/monsters"), "a folder entry, created with its folder");
            Assert.That(monsters.address, Is.EqualTo(monsters.AssetPath), "address = project path");
            Assert.That(_settings.FindGroup("sprite-sheets").entries.Single().AssetPath, Is.EqualTo(Art + "/sprite/_sheets"));
            Assert.That(_settings.FindGroup("sprites-npc").entries, Is.Empty, "a sprite folder not written yet has no entry");
            Assert.That(_settings.groups.Sum(g => g == null ? 0 : g.entries.Count), Is.EqualTo(15), "12 categories, 2 tables, the sheets: folder entries only");
        }

        [Test]
        public void Verify_AcceptsAReferenceInsideAFolderEntry_WithItsSubAsset()
        {
            string guid = Sheet(Art + "/sprite/_sheets/ab/sm_bash_ab12cd34.png");
            RoCatalogAddressables.Ensure(_settings, _roots);
            var index = ScriptableObject.CreateInstance<SkillIconIndex>();
            index.SkillIds = new[] { 5 };
            index.Icons = new[] { new SpriteRef { Still = new AssetReferenceSprite(guid) { SubObjectName = "sm_bash_ab12cd34" } } };
            AssetDatabase.CreateAsset(index, Art + "/catalog/skill-icons/_index.asset");
            var report = new RoReport();

            int checkedRefs = RoCatalogAddressables.Verify(_settings, _roots, new Object[] { index }, report);

            Assert.That(checkedRefs, Is.EqualTo(1), "the empty animation reference is not a reference");
            Assert.That(report.Issues, Is.Empty, string.Join("\n", report.Issues));
            var entry = _settings.FindAssetEntry(guid, true);
            Assert.That(entry.address, Is.EqualTo(Art + "/sprite/_sheets/ab/sm_bash_ab12cd34.png"), "the sheet is addressable by its path");
            Assert.That(index.Icons[0].Still.SubObjectName, Is.EqualTo("sm_bash_ab12cd34"));
        }

        [Test]
        public void Verify_ReportsEachDanglingReferenceOnce()
        {
            string inside = Sheet(Art + "/sprite/_sheets/ab/a_ab12cd34.png");
            string outside = Sheet(RoImportTestRoot.Folder + "/elsewhere/b.png");
            RoCatalogAddressables.Ensure(_settings, _roots);
            var index = ScriptableObject.CreateInstance<EffectIndex>();
            index.Names = new[] { "a", "b", "c" };
            index.Effects = new[]
            {
                new SpriteRef { Still = new AssetReferenceSprite(inside) { SubObjectName = "nope" } },
                new SpriteRef { Still = new AssetReferenceSprite(outside) { SubObjectName = "b" } },
                new SpriteRef { Animation = new SpriteAnimationReference("0123456789abcdef0123456789abcdef") },
            };
            var report = new RoReport();

            RoCatalogAddressables.Verify(_settings, _roots, new Object[] { index }, report);
            Object.DestroyImmediate(index);

            Assert.That(report.Issues.Count, Is.EqualTo(3), string.Join("\n", report.Issues));
            Assert.That(report.Issues.All(i => i.Code == RoIssueCode.CatalogStrayFile), Is.True);
            Assert.That(report.Issues.Select(i => i.Message), Has.Some.Contains("no sub-asset nope").And.Some.Contains("outside").And.Some.Contains("no asset"));
        }
    }
}
