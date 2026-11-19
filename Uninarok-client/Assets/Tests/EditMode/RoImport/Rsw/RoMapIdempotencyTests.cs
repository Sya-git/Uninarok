using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T5.1 / T5.3 (FR-11; SPEC-0002 FR-5, FR-16): an unchanged map is skipped; a forced conversion rewrites the
    // scene in place, identical by structure, with the GUIDs of the scene and its dependencies unchanged, so links and the
    // build settings entry survive.
    public class RoMapIdempotencyTests
    {
        RoMapTestBed _bed;

        [SetUp]
        public void SetUp() => _bed = new RoMapTestBed();

        [TearDown]
        public void TearDown() => _bed.Dispose();

        string[] Guids() => new[]
        {
            _bed.Paths.Scene,
            _bed.Roots.Data + "/map/sample.asset",
            _bed.Roots.Art + "/map/sample/sample_ground.prefab",
            _bed.Roots.Art + "/model/외부소품/box.prefab",
            _bed.Roots.Art + "/model/외부소품/spin.prefab"
        }.Select(AssetDatabase.AssetPathToGUID).ToArray();

        [Test]
        public void ASecondConversion_IsSkipped()
        {
            Assert.AreEqual(RoStatus.Created, _bed.Convert().Status);
            var second = _bed.Convert();

            Assert.AreEqual(RoStatus.Skipped, second.Status);
            Assert.AreEqual(_bed.Paths.Scene, AssetDatabase.GetAssetPath(second.Value));
        }

        [Test]
        public void AForcedConversion_GivesTheSameScene_AndKeepsEveryGuid()
        {
            _bed.Convert();
            string before = _bed.DumpScene();
            var guids = Guids();
            CollectionAssert.DoesNotContain(guids, string.Empty);

            var forced = _bed.Convert(force: true);

            Assert.AreNotEqual(RoStatus.Failed, forced.Status, forced.Failure?.Message);
            Assert.AreEqual(before, _bed.DumpScene(), "same structure");
            CollectionAssert.AreEqual(guids, Guids(), "scene, map data, ground and models keep their GUIDs");
        }

        [Test]
        public void TheBuildSettingsEntry_SurvivesAForcedConversion()
        {
            _bed.Convert();
            string guid = AssetDatabase.AssetPathToGUID(_bed.Paths.Scene);
            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(_bed.Paths.Scene, true)).ToArray();

            _bed.Convert(force: true);

            var entry = EditorBuildSettings.scenes.SingleOrDefault(s => s.path == _bed.Paths.Scene);
            Assert.IsNotNull(entry);
            Assert.AreEqual(guid, entry.guid.ToString());
            Assert.IsTrue(entry.enabled);
        }

        [Test]
        public void ChangingTheRsw_ConvertsTheSceneAgain()
        {
            _bed.Convert();
            var rsw = RoMapTestBed.Sample();
            rsw.Models = rsw.Models.Take(3).ToList();
            _bed.WriteSources(rsw);

            Assert.AreEqual(RoStatus.Updated, _bed.Convert().Status);
            StringAssert.Contains("Models (3)", _bed.DumpScene());
        }
    }
}
