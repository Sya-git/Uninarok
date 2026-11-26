using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Effects;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Effects
{
    // TASKS-0012 T4.5 (FR-5, FR-10; PLAN-0012 4.4): the effect catalog index. Assets are made directly under temporary output
    // roots; the data folder holds placeholder files, because only their presence is asked.
    public class EffectCatalogIndexTests
    {
        const string A = "sprite/이팩트/a.act";
        const string B = "sprite/이팩트/b.act";
        const string Gone = "sprite/이팩트/gone.act";
        const string Quad = "texture/effect/_authored/glow.quad";

        string _data, _ledger;
        RoOutputRoots _roots;
        RoAssetWriter _writer;
        AddressableAssetSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _writer = new RoAssetWriter(_roots);
            _settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
            string temp = Path.Combine(Path.GetTempPath(), "EffectCatalogIndexTests", System.Guid.NewGuid().ToString("N"));
            _data = Path.Combine(temp, "data");
            _ledger = Path.Combine(temp, "ledger");
            Directory.CreateDirectory(_ledger);
            Put(A); Put(B);
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            string temp = Path.GetDirectoryName(_data);
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }

        void Put(string relative)
        {
            string full = Path.Combine(_data, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, "placeholder");
        }

        string Generated(string name) => RoEffectOutputPaths.Root(_roots) + "/" + name + ".asset";
        string Authored(string key) => RoEffectOutputPaths.AuthoredRoot(_roots) + "/" + key + ".asset";

        string Make(string assetPath, string key, EffectKind kind = EffectKind.Sprite)
        {
            var e = ScriptableObject.CreateInstance<EffectAsset>();
            e.Path = key; e.Kind = kind;
            _writer.UpsertObject(assetPath, e);
            return AssetDatabase.AssetPathToGUID(assetPath);
        }

        RoReport Run() => RoImportApi.EffectIndex(new RoImportOptions
        {
            Source = new RoFolderSource(_data), Roots = _roots, LedgerFolder = _ledger, ShowProgress = false, CatalogAddressables = _settings,
        });

        EffectCatalogIndex Index() => AssetDatabase.LoadAssetAtPath<EffectCatalogIndex>(RoEffectOutputPaths.Index(_roots));

        [Test]
        public void OneEntryPerAsset_SortedByKey_WithGuidReferences()
        {
            string gb = Make(Generated("sprite/이팩트/b.act"), B);
            string ga = Make(Generated("sprite/이팩트/a.act"), A);
            string gq = Make(Authored(Quad), Quad, EffectKind.Quad);

            var report = Run();

            Assert.That(report.Failed, Is.EqualTo(0), report.Summarize());
            var index = Index();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Keys, Is.EqualTo(new[] { A, B, Quad }));
            Assert.That(index.Assets.Select(r => r.Guid), Is.EqualTo(new[] { ga, gb, gq }));
            Assert.That(index.Assets.Length, Is.EqualTo(index.Keys.Length));
            Assert.That(index.SourceMissing, Is.EqualTo(new[] { false, false, false }), "authored assets have no source");
            Assert.That(report.Issues.Count, Is.EqualTo(0));
        }

        [Test]
        public void DuplicateKey_IsOneWarning_AndTheOrdinalFirstPathWins()
        {
            string first = Make(Generated("sprite/이팩트/a.act"), A);
            Make(Generated("sprite/이팩트/z_copy.act"), A);

            var report = Run();

            Assert.That(report.Issues.Count(i => i.Code == RoIssueCode.EffectKeyDuplicate), Is.EqualTo(1), report.Summarize());
            var index = Index();
            Assert.That(index.Keys, Is.EqualTo(new[] { A }));
            Assert.That(index.Assets[0].Guid, Is.EqualTo(first));
        }

        [Test]
        public void TheIndex_HoldsKeysAndReferencesOnly()
        {
            Make(Generated("sprite/이팩트/a.act"), A);
            Run();

            var fields = typeof(EffectCatalogIndex).GetFields().Select(f => f.Name).OrderBy(n => n).ToArray();
            Assert.That(fields, Is.EqualTo(new[] { "Assets", "Keys", "SourceMissing" }));
            Assert.That(typeof(EffectAssetRef).GetFields().Select(f => f.Name), Is.EqualTo(new[] { "Guid" }));
        }

        [Test]
        public void SourceMissing_IsSetWhenTheSourceFileIsGone()
        {
            Make(Generated("sprite/이팩트/a.act"), A);
            Make(Generated("sprite/이팩트/gone.act"), Gone);

            Run();

            var index = Index();
            Assert.That(index.Keys, Is.EqualTo(new[] { A, Gone }));
            Assert.That(index.SourceMissing, Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void UnchangedIndex_IsNotRewritten_AndTheLedgerIsSkipped()
        {
            Make(Generated("sprite/이팩트/a.act"), A);
            Make(Authored(Quad), Quad, EffectKind.Quad);
            var first = Run();
            Assert.That(first.Created, Is.EqualTo(1), first.Summarize());
            string path = RoEffectOutputPaths.Index(_roots);
            string guid = AssetDatabase.AssetPathToGUID(path);
            var stamp = File.GetLastWriteTimeUtc(RoImportTestRoot.FullPath(path));

            var again = Run();

            Assert.That(again.Skipped, Is.EqualTo(1), again.Summarize());
            Assert.That(again.Created + again.Updated, Is.EqualTo(0));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(File.GetLastWriteTimeUtc(RoImportTestRoot.FullPath(path)), Is.EqualTo(stamp));

            Make(Generated("sprite/이팩트/b.act"), B);
            var changed = Run();
            Assert.That(changed.Updated, Is.EqualTo(1), changed.Summarize());
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid), "rewritten in place");
        }

        [Test]
        public void AnAuthoredAsset_ThatIsNotWhereItsPathSays_IsAMismatchWarning()
        {
            Make(Authored("texture/effect/_authored/elsewhere.quad"), Quad, EffectKind.Quad);

            var report = Run();

            var issue = report.Issues.Single(i => i.Code == RoIssueCode.EffectKeyPathMismatch);
            Assert.That(issue.Item, Is.EqualTo(Authored("texture/effect/_authored/elsewhere.quad")));
            Assert.That(Index().Keys, Is.EqualTo(new[] { Quad }), "still listed under its own key");
        }

        [Test]
        public void TheKind_AndMenuJobArePartOfTheRegistry()
        {
            Assert.That(RoConverterRegistry.For(RoJobKind.EffectIndex), Is.TypeOf<RoEffectCatalogIndexConverter>());
            Assert.That(RoEffectCatalogIndexConverter.JobPath.Canonical, Is.EqualTo("effect/_index"));
        }
    }
}
