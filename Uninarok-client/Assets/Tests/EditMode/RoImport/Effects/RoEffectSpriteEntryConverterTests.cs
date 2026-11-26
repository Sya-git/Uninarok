using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using Uninarok.Effects;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Effects;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Effects
{
    // TASKS-0012 T4.4 (FR-4, FR-5; PLAN-0012 4.4): one EffectAsset per ACT under sprite/이팩트/. The data folder is synthetic:
    // the files hold placeholder text because the converter only looks at which files exist, never at their content.
    public class RoEffectSpriteEntryConverterTests
    {
        const string A = "sprite/이팩트/a";
        const string B = "sprite/이팩트/sub/b";
        const string Orphan = "sprite/이팩트/orphan";

        string _data, _ledger;
        RoOutputRoots _roots;
        AddressableAssetSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
            string temp = Path.Combine(Path.GetTempPath(), "RoEffectSpriteTests", System.Guid.NewGuid().ToString("N"));
            _data = Path.Combine(temp, "data");
            _ledger = Path.Combine(temp, "ledger");
            Directory.CreateDirectory(_ledger);
            Put(A + ".act"); Put(A + ".spr");
            Put(B + ".act"); Put(B + ".spr");
            Put(Orphan + ".act");
            Put("sprite/몬스터/poring.act"); Put("sprite/몬스터/poring.spr");
            Put("sprite/이팩트/lonely.spr");
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

        RoReport Run(bool force = false) => RoImportApi.EffectSprites(new RoImportOptions
        {
            Source = new RoFolderSource(_data), Roots = _roots, LedgerFolder = _ledger, ShowProgress = false,
            CatalogAddressables = _settings, Force = force,
        });

        string AssetPath(string stem) => RoEffectOutputPaths.Asset(_roots, RoPath.Parse(stem + ".act"));

        [Test]
        public void OnlyTheActsUnderTheEffectFolderAreJobs()
        {
            var jobs = RoEffectSpriteEntryConverter.Jobs(new RoFolderSource(_data));

            Assert.That(jobs.Select(j => j.Path.Canonical), Is.EqualTo(new[] { A + ".act", Orphan + ".act", B + ".act" }));
            Assert.That(jobs.All(j => j.Kind == RoJobKind.EffectSprites), Is.True);
            Assert.That(RoConverterRegistry.For(RoJobKind.EffectSprites), Is.TypeOf<RoEffectSpriteEntryConverter>());
        }

        [Test]
        public void OutputPath_MirrorsTheKeyWithTheActExtensionKept()
        {
            Assert.That(AssetPath(A), Is.EqualTo(_roots.Art.TrimEnd('/') + "/effect/sprite/이팩트/a.act.asset"));
        }

        [Test]
        public void OneAssetPerAct_WithKindNameActionAndKey()
        {
            var report = Run();

            Assert.That(report.Created, Is.EqualTo(2), report.Summarize());
            Assert.That(report.Failed, Is.EqualTo(0));
            var a = AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetPath(A));
            var b = AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetPath(B));
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(EffectKind.Sprite));
            Assert.That(a.SpriteName, Is.EqualTo("a"));
            Assert.That(a.SpriteAction, Is.EqualTo(0));
            Assert.That(a.Path, Is.EqualTo("sprite/이팩트/a.act"), "the canonical catalog key, FR-5");
            Assert.That(b.SpriteName, Is.EqualTo("sub/b"), "the SPEC-0008 name is relative to the folder");
            Assert.That(b.Path, Is.EqualTo("sprite/이팩트/sub/b.act"));
            Assert.That(AssetDatabase.FindAssets("t:EffectAsset", new[] { _roots.Art.TrimEnd('/') + "/effect" }).Length, Is.EqualTo(2),
                "no entry for the orphan, the other folder or the SPR without ACT");
        }

        [Test]
        public void SecondRun_IsSkipped_AndAForcedRunKeepsTheGuids()
        {
            Run();
            string guid = AssetDatabase.AssetPathToGUID(AssetPath(A));

            var again = Run();
            Assert.That(again.Skipped, Is.EqualTo(3), "two entries and the orphan; " + again.Summarize());
            Assert.That(again.Created + again.Updated, Is.EqualTo(0));

            var forced = Run(force: true);
            Assert.That(forced.Created, Is.EqualTo(0), forced.Summarize());
            Assert.That(AssetDatabase.AssetPathToGUID(AssetPath(A)), Is.EqualTo(guid));
        }

        [Test]
        public void AnActWithoutSpr_IsReportedNotFatal()
        {
            var report = Run();

            Assert.That(report.Failed, Is.EqualTo(0));
            var issue = report.Issues.Single(i => i.Code == RoIssueCode.CatalogOrphanAct);
            Assert.That(issue.Item, Is.EqualTo(Orphan + ".act"));
            Assert.That(AssetDatabase.LoadAssetAtPath<EffectAsset>(AssetPath(Orphan)), Is.Null);
        }

        [Test]
        public void TheRun_MakesTheEffectFolderEntry_OnceItExists()
        {
            Run();

            string folder = _roots.Art.TrimEnd('/') + "/effect";
            var entries = _settings.FindGroup("effects").entries.Select(e => e.AssetPath).ToArray();
            Assert.That(entries, Does.Contain(folder), "RoCatalogAddressables.Ensure ran after the folder was written");
            Assert.That(RoCatalogEnsureChanges(), Is.EqualTo(0), "and it left nothing to do");
        }

        int RoCatalogEnsureChanges() => Uninarok.RoImport.Editor.Catalog.RoCatalogAddressables.Ensure(_settings, _roots);
    }
}
