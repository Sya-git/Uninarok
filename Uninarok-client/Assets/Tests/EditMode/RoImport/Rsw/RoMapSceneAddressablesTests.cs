using System.Linq;
using NUnit.Framework;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Uninarok.Map;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Map;
using Uninarok.RoImport.Editor.Ui;
using ImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Rsw
{
    // SPEC-0010 FR-1, FR-15 (PLAN-0010 3, D5; T6.1).
    public class RoMapSceneAddressablesTests
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

        void Scene(string name)
        {
            string folder = _roots.Scenes.TrimEnd('/');
            System.IO.Directory.CreateDirectory(folder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(scene, folder + "/" + name + ".unity");
            EditorSceneManager.CloseScene(scene, true);
            UnityEditor.AssetDatabase.Refresh();
        }

        [Test]
        public void Ensure_AddsEveryMapScene_AndTheAdditiveOnes_AndASecondRunChangesNothing()
        {
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path)) EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
            Scene("one");
            Scene("one.additive");
            Scene("lost.additive");

            Assert.Greater(RoMapSceneAddressables.Ensure(_settings, _roots), 0);
            var entries = _settings.FindGroup(RoMapSceneAddressables.Group).entries;
            CollectionAssert.AreEquivalent(new[] { "map/one", "map/one.additive" }, entries.Select(e => e.address));
            Assert.AreEqual(0, RoMapSceneAddressables.Ensure(_settings, _roots), "idempotent");

            entries.First(e => e.address == "map/one").SetAddress("wrong", false);
            Assert.AreEqual(1, RoMapSceneAddressables.Ensure(_settings, _roots), "a wrong address is corrected");
            Assert.IsTrue(entries.Any(e => e.address == MapSceneKeys.Scene("one")));
        }

        // D33: an imported map is loadable without another step, also one that was current already.
        [Test]
        public void AnImport_RegistersItsMaps_OnlyInTheProjectsFolder()
        {
            using (var bed = new RoMapTestBed("sample"))
            {
                var settings = AddressableAssetSettings.Create(RoImportTestRoot.Folder + "/aa", "TestAddressables", false, true);
                var options = bed.Options();
                options.CatalogAddressables = settings;
                Assert.AreNotEqual(RoStatus.Failed, ImportApi.Map("sample", options).Status);
                CollectionAssert.AreEquivalent(new[] { "map/sample" }, settings.FindGroup(RoMapSceneAddressables.Group).entries.Select(e => e.address));

                settings.RemoveAssetEntry(UnityEditor.AssetDatabase.AssetPathToGUID(bed.Paths.Scene));
                Assert.AreEqual(1, ImportApi.Run(RoImportMenu.MapJobs(new RoFolderSource(bed.Data)), options).Skipped);
                CollectionAssert.AreEquivalent(new[] { "map/sample" }, settings.FindGroup(RoMapSceneAddressables.Group).entries.Select(e => e.address));

                bed.Convert(force: true);
                string guid = UnityEditor.AssetDatabase.AssetPathToGUID(bed.Paths.Scene);
                Assert.IsNull(AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(guid), "a test's output is not the game's");
            }
        }

        [Test]
        public void TheProject_HasTheThreeMaps()
        {
            var group = AddressableAssetSettingsDefaultObject.Settings.FindGroup(RoMapSceneAddressables.Group);
            Assert.IsNotNull(group, "run Tools/Ragnarok/Register Map Scenes");
            CollectionAssert.IsSubsetOf(new[] { "map/prontera", "map/izlude", "map/izlude.additive", "map/prt_fild08" }, group.entries.Select(e => e.address));
        }

        [Test]
        public void MapSceneKeys_FromTheServersName()
        {
            Assert.AreEqual("prontera", MapSceneKeys.MapName("prontera.gat"));
            Assert.AreEqual("prontera", MapSceneKeys.MapName("prontera"));
            Assert.AreEqual("map/prt_fild08.additive", MapSceneKeys.Additive("prt_fild08"));
        }
    }
}
