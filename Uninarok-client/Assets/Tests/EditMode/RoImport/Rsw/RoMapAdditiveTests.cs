using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Uninarok.RoImport.Editor.Map;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T5.1 / T5.3 (FR-11, PLAN-0006 4.7): custom work lives in a hand-made additive scene beside the generated
    // one; re-conversion never touches it, and its objects still find the map objects under the stable names.
    [Ignore("pending T5.3")]
    public class RoMapAdditiveTests
    {
        RoMapTestBed _bed;

        [SetUp]
        public void SetUp() => _bed = new RoMapTestBed();

        [TearDown]
        public void TearDown() => _bed.Dispose();

        void MakeAdditiveScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var custom = new GameObject("Custom");
            custom.transform.position = new Vector3(1, 2, 3);
            // The path a custom object would use to find a map object (root, group base name, instance name), kept in the
            // name of a child so the scene needs no custom component.
            new GameObject(RoMapNames.Root("sample") + "/" + RoMapNames.Group(RoMapGroup.Models) + "/box_turned").transform.SetParent(custom.transform);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, _bed.Paths.AdditiveScene));
        }

        [Test]
        public void TheAdditiveScene_IsByteIdentical_AfterAForcedConversion()
        {
            _bed.Convert();
            MakeAdditiveScene();
            string file = RoImportTestRoot.FullPath(_bed.Paths.AdditiveScene);
            byte[] before = File.ReadAllBytes(file);

            _bed.Convert(force: true);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(file));
        }

        [Test]
        public void ACustomObject_StillFindsItsMapObject_ByTheStableNames()
        {
            _bed.Convert();
            MakeAdditiveScene();
            _bed.Convert(force: true);

            var root = _bed.OpenScene();
            var additive = EditorSceneManager.OpenScene(_bed.Paths.AdditiveScene, OpenSceneMode.Additive);
            var custom = additive.GetRootGameObjects()[0];
            Assert.AreEqual("Custom", custom.name);
            Assert.AreEqual(new Vector3(1, 2, 3), custom.transform.position);

            string[] parts = custom.transform.GetChild(0).name.Split('/');
            Assert.AreEqual(root.name, parts[0]);
            var group = RoMapNames.FindGroup(root.transform, RoMapGroup.Models);
            Assert.AreEqual(parts[1], RoMapNames.Group(RoMapGroup.Models));
            Assert.IsNotNull(group.Find(parts[2]), "the instance is still there under its name");
            EditorSceneManager.CloseScene(additive, true);
        }

        [Test]
        public void TheConverter_NeverCreatesTheAdditiveScene()
        {
            _bed.Convert();
            _bed.Convert(force: true);

            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(_bed.Paths.AdditiveScene)));
        }
    }
}
