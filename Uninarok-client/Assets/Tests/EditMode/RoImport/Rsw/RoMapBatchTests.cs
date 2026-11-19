using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Map;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Rsm;
using Uninarok.RoImport.Editor.Ui;
using ImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T5.4 (FR-11, FR-12): every map of the data root converts in one batch, shared models once, a failing map
    // does not stop the others, a second batch changes nothing; Open Map opens the generated scene and its additive scene.
    public class RoMapBatchTests
    {
        RoMapTestBed _bed;

        [SetUp]
        public void SetUp()
        {
            _bed = new RoMapTestBed("map_a");
            _bed.WriteMap("map_b", RoMapTestBed.Sample());
            File.WriteAllBytes(Path.Combine(_bed.Data, "map_c.rsw"), new byte[] { (byte)'G', (byte)'R', (byte)'S', (byte)'W', 2, 1, 0 });
        }

        [TearDown]
        public void TearDown() => _bed.Dispose();

        RoReport Batch() =>
            ImportApi.Run(RoImportMenu.MapJobs(new RoFolderSource(_bed.Data)), _bed.Options());

        [Test]
        public void TheMenuJobs_AreEveryRswOfTheDataRoot()
        {
            var jobs = RoImportMenu.MapJobs(new RoFolderSource(_bed.Data));

            CollectionAssert.AreEquivalent(new[] { "map_a.rsw", "map_b.rsw", "map_c.rsw" }, jobs.Select(j => j.Path.Canonical));
            Assert.IsTrue(jobs.All(j => j.Kind == RoJobKind.Map));
        }

        [Test]
        public void ABatch_ConvertsEveryMap_SharedModelsOnce_AndListsEveryResult()
        {
            var report = Batch();

            Assert.AreEqual(2, report.Created, "map_a and map_b");
            Assert.AreEqual(1, report.Failed, "map_c is corrupt and does not stop the others");
            Assert.AreEqual("map_c.rsw", report.Failures.Single().Item);
            foreach (string map in new[] { "map_a", "map_b" })
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(RoMapOutputPaths.For(_bed.Roots, RoPath.Parse(map + ".rsw")).Scene), map);

            var models = RoLedger.Load(RoRsmConverter.Category, _bed.LedgerFolder).Entries.Select(e => e.Source).ToArray();
            CollectionAssert.AreEquivalent(new[] { "model/외부소품/box.rsm", "model/외부소품/spin.rsm" }, models, "one entry per shared model");
            // A run reports each (code, item) once (SPEC-0002 FR-11): the missing model once, naming the first map using it.
            var missing = report.Issues.Single(i => i.Code == RoIssueCode.ModelMissing);
            Assert.AreEqual("map_a.rsw", missing.Referrer);
        }

        [Test]
        public void ASecondBatch_ChangesNothing()
        {
            Batch();
            var second = Batch();

            Assert.AreEqual(0, second.Created + second.Updated);
            Assert.AreEqual(2, second.Skipped);
            Assert.AreEqual(1, second.Failed, "the corrupt map is still listed");
        }

        [Test]
        public void OpenMap_OpensTheScene_AndTheAdditiveSceneOnTop()
        {
            Batch();
            var paths = RoMapOutputPaths.For(_bed.Roots, RoPath.Parse("map_a.rsw"));
            var additive = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(EditorSceneManager.SaveScene(additive, paths.AdditiveScene));

            Assert.IsTrue(RoMapOpener.Open("map_a", _bed.Roots));

            Assert.AreEqual(2, SceneManager.sceneCount);
            Assert.AreEqual(paths.Scene, SceneManager.GetSceneAt(0).path);
            Assert.AreEqual(paths.AdditiveScene, SceneManager.GetSceneAt(1).path);
            Assert.IsFalse(RoMapOpener.Open("map_c", _bed.Roots), "no scene for a map that failed");
        }
    }
}
