using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Rsm;
using Uninarok.RoImport.Editor.Writing;
using Object = UnityEngine.Object;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T6.2 (FR-1, FR-7, FR-10): converting hand-made models writes the prefab, one mesh per node and the shared
    // materials as separate assets at the paths of PLAN-0003 §3. Writes under Assets/__RoImportTests.
    [Ignore("pending T6.4")]
    public class RsmWriterTests
    {
        const string Texture = "a.bmp";   // fixture texture/a.bmp (keyed: a cutout material)
        static RoPath Source => RoPath.Parse("model/test/sample.rsm");

        RoOutputRoots _roots;
        string _ledgerFolder;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        RoImportContext Context() =>
            new RoImportContext(new RoFolderSource(RoFolderSourceTests.FixtureRoot), _roots, _ledgerFolder) { Png = new RoPngCodec() };

        static RoRsm WithTexture(RoRsm model)
        {
            model.Textures = new[] { Texture };
            return model;
        }

        GameObject Convert(RoRsm model, RoImportContext ctx = null)
        {
            ctx = ctx ?? Context();
            var result = RoRsmConverter.Write(ctx, Source, model, "hash");
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            ctx.SaveLedgers();
            Assert.IsNotNull(result.Value);
            return result.Value;
        }

        RsmOutputPaths Paths(RoRsm model)
        {
            Assert.IsTrue(RsmHierarchy.TryBuild(model, out var tree, out _));
            return RsmOutputPaths.For(_roots, Source, tree.UniqueNames);
        }

        [Test]
        public void HandMadeModel_YieldsPrefabMeshesAndMaterials_AsSeparateAssets()
        {
            var model = WithTexture(RsmTestModels.ParentAndChild());
            var prefab = Convert(model);
            var paths = Paths(model);

            Assert.AreEqual(paths.Prefab, AssetDatabase.GetAssetPath(prefab));
            foreach (string mesh in paths.Meshes)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Mesh>(mesh);
                Assert.IsNotNull(asset, mesh);
                Assert.IsTrue(AssetDatabase.IsMainAsset(asset), "a mesh is its own asset, not a sub-asset (FR-10)");
                Assert.Greater(asset.triangles.Length, 0);
            }

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            Assert.AreEqual(2, renderers.Length);
            foreach (var r in renderers)
            foreach (var m in r.sharedMaterials)
            {
                Assert.IsNotNull(m);
                string path = AssetDatabase.GetAssetPath(m);
                StringAssert.StartsWith(_roots.Art + "/" + RoOutputPaths.MaterialsFolder + "/", path);
                Assert.IsTrue(AssetDatabase.IsMainAsset(m));
            }
            foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                Assert.AreEqual(paths.Meshes[Array.IndexOf(new[] { "parent", "child" }, f.name)], AssetDatabase.GetAssetPath(f.sharedMesh));
        }

        [Test]
        public void PrefabHierarchy_EqualsTheTree_WithUniqueNames()
        {
            // child listed before its parent, and two nodes with the same name
            var root = RsmTestModels.Cube("root");
            var leafA = RsmTestModels.Cube("leaf");
            leafA.ParentName = "mid";
            var mid = RsmTestModels.Cube("mid");
            mid.ParentName = "root";
            var leafB = RsmTestModels.Cube("leaf");
            leafB.ParentName = "mid";
            var prefab = Convert(WithTexture(RsmTestModels.Model(leafA, root, mid, leafB)));

            Assert.AreEqual("sample", prefab.name);
            Assert.AreEqual(1, prefab.transform.childCount);
            var r = prefab.transform.GetChild(0);
            Assert.AreEqual("root", r.name);
            var m = r.Find("mid");
            Assert.IsNotNull(m);
            CollectionAssert.AreEquivalent(new[] { "leaf", "leaf_2" }, Enumerable.Range(0, m.childCount).Select(i => m.GetChild(i).name));
        }

        [Test]
        public void RootTransform_IsIdentity_AndNodesCarryTheRestPose()
        {
            var model = WithTexture(RsmTestModels.Model(RsmTestModels.RotatedPositioned()));
            var prefab = Convert(model);
            Assert.AreEqual(Vector3.zero, prefab.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, prefab.transform.localRotation);
            Assert.AreEqual(Vector3.one, prefab.transform.localScale);

            Assert.IsTrue(RsmRules.TryGet(1, 4, out var rules));
            Assert.IsTrue(RsmHierarchy.TryBuild(model, out var tree, out _));
            var rest = RsmPose.ComputeRest(model, tree, rules);
            var node = prefab.transform.GetChild(0);
            Assert.That((RsmSpace.LocalPosition(rest.Local[0]) - node.localPosition).magnitude, Is.LessThan(1e-5f));
            Assert.That(Quaternion.Angle(RsmSpace.LocalRotation(rest.Local[0]), node.localRotation), Is.LessThan(1e-3f));
            Assert.That((RsmSpace.LocalScale(rest.Local[0]) - node.localScale).magnitude, Is.LessThan(1e-5f));
        }

        [Test]
        public void NodeWithoutFaces_HasNoRenderer()
        {
            var empty = new RoRsmNode { Name = "empty", ParentName = "root" };
            var prefab = Convert(WithTexture(RsmTestModels.Model(RsmTestModels.Cube("root"), empty)));
            var e = prefab.transform.GetChild(0).Find("empty");
            Assert.IsNotNull(e);
            Assert.IsNull(e.GetComponent<MeshRenderer>());
            Assert.IsNull(e.GetComponent<MeshFilter>());
        }

        [Test]
        public void StaticModel_HasNoAnimatorAndNoScripts()
        {
            var prefab = Convert(WithTexture(RsmTestModels.ParentAndChild()));
            Assert.IsNull(prefab.GetComponentInChildren<Animator>(true));
            Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true));
        }

        [Test]
        public void ThousandNodesWithOneTexture_ReferenceOneMaterial()
        {
            var nodes = new RoRsmNode[1000];
            nodes[0] = RsmTestModels.Cube("n0");
            for (int i = 1; i < nodes.Length; i++)
            {
                nodes[i] = RsmTestModels.Cube("n" + i);
                nodes[i].ParentName = "n0";
            }
            var prefab = Convert(WithTexture(RsmTestModels.Model(nodes)));
            var materials = prefab.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).Distinct().ToList();
            Assert.AreEqual(1, materials.Count);
        }

        [Test]
        public void TwoSidedFaces_UseTheTwoSidedMaterial()
        {
            var cube = RsmTestModels.Cube("root");
            cube.Faces = cube.Faces.Select((f, i) => { f.TwoSided = i < 2; return f; }).ToArray();
            var prefab = Convert(WithTexture(RsmTestModels.Model(cube)));
            var mats = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(2, mats.Length);
            Assert.AreEqual(2f, mats[0].GetFloat("_Cull"), "one-sided: back culled");
            Assert.AreEqual(0f, mats[1].GetFloat("_Cull"), "two-sided: no culling");
        }

        [Test]
        public void MeshAndMaterial_AreEditableWithoutTouchingTheSource()
        {
            var model = WithTexture(RsmTestModels.ParentAndChild());
            var prefab = Convert(model);
            string sourceBefore = Hash(RoFolderSourceTests.FixtureRoot);

            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Paths(model).Meshes[0]);
            var go = new GameObject("collider");
            try
            {
                var collider = go.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                Assert.AreSame(mesh, collider.sharedMesh);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            var material = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Assert.IsFalse(AssetDatabase.IsSubAsset(material));
            material.SetColor("_BaseColor", Color.red);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            Assert.AreEqual(Color.red, AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(material)).GetColor("_BaseColor"));
            Assert.AreEqual(sourceBefore, Hash(RoFolderSourceTests.FixtureRoot), "the source folder is untouched");
        }

        static string Hash(string folder) =>
            string.Join("|", Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => f + ":" + new FileInfo(f).Length + ":" + File.GetLastWriteTimeUtc(f).Ticks));
    }
}
