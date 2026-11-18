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
    // materials as separate assets at the paths of PLAN-0003 §3. T6.4 (FR-8, FR-9): animated models get clips, a controller
    // and an animator; a failing model writes nothing. Writes under Assets/__RoImportTests.
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

        // ---- T6.4 ----

        [Test]
        public void AnimatedModel_GetsAClipAControllerAndAnAnimator()
        {
            var model = WithTexture(RsmTestModels.TwoNodeAnimated());
            var prefab = Convert(model);
            var paths = Paths(model);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(paths.Clip(0));
            Assert.IsNotNull(clip, paths.Clip(0));
            Assert.IsTrue(AnimationUtility.GetAnimationClipSettings(clip).loopTime);
            Assert.AreEqual(1f, clip.length, 1e-4f);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            CollectionAssert.AreEquivalent(new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" },
                bindings.Select(b => b.propertyName));
            Assert.IsTrue(bindings.All(b => b.path == "base/hand"));

            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(paths.Controller);
            Assert.IsNotNull(controller);
            Assert.AreEqual(1, controller.layers.Length);
            Assert.AreEqual("Play", controller.layers[0].stateMachine.defaultState.name);
            Assert.AreSame(clip, controller.layers[0].stateMachine.defaultState.motion);
            Assert.IsEmpty(controller.parameters);

            var animator = prefab.GetComponent<Animator>();
            Assert.IsNotNull(animator, "the animator is on the prefab root");
            Assert.AreSame(controller, animator.runtimeAnimatorController);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, animator.cullingMode);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.IsEmpty(prefab.GetComponentsInChildren<MonoBehaviour>(true), "no runtime script");
        }

        [Test]
        public void TwoLoopLengths_GiveTwoClipsAndTwoLayers()
        {
            var model = WithTexture(RsmTestModels.TwoNodeAnimated());
            var slow = RsmTestModels.Cube("slow");
            slow.ParentName = "base";
            slow.RotKeys = new[] { new RoRsmRotKey(0, Quaternion.identity), new RoRsmRotKey(3000, Quaternion.AngleAxis(90f, Vector3.up)) };
            model.Nodes = model.Nodes.Concat(new[] { slow }).ToArray();
            Convert(model);
            var paths = Paths(model);

            Assert.AreEqual(1f, AssetDatabase.LoadAssetAtPath<AnimationClip>(paths.Clip(0)).length, 1e-4f);
            Assert.AreEqual(3f, AssetDatabase.LoadAssetAtPath<AnimationClip>(paths.Clip(1)).length, 1e-4f);
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(paths.Controller);
            Assert.AreEqual(2, controller.layers.Length);
            Assert.AreEqual(1f, controller.layers[1].defaultWeight);
        }

        [Test]
        public void FailingModel_LeavesNoPartialOutput()
        {
            // a cycle fails before anything is written
            var a = RsmTestModels.Cube("a");
            a.ParentName = "b";
            var b = RsmTestModels.Cube("b");
            b.ParentName = "a";
            var ctx = Context();
            var result = RoRsmConverter.Write(ctx, Source, WithTexture(RsmTestModels.Model(a, b)), "hash");
            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.RsmCycle, result.Failure.Code);
            Assert.IsFalse(Directory.Exists(RoImportTestRoot.FullPath(_roots.Art + "/model")), "no model output at all");
        }

        [Test]
        public void FailingModel_LeavesThePreviousOutputsUnchanged()
        {
            var good = WithTexture(RsmTestModels.ParentAndChild());
            Convert(good);
            var paths = Paths(good);
            var before = paths.Meshes.Concat(new[] { paths.Prefab }).ToDictionary(p => p, p => File.ReadAllBytes(RoImportTestRoot.FullPath(p)));

            var unsupported = WithTexture(RsmTestModels.ParentAndChild());
            unsupported.VersionMinor = 5;   // no verified rules (D9)
            var ctx = Context();
            var result = RoRsmConverter.Write(ctx, Source, unsupported, "other");
            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.UnsupportedRsmVersion, result.Failure.Code);
            Assert.AreEqual(1, ctx.Report.Issues.Count(i => i.Code == RoIssueCode.UnsupportedRsmVersion), "one warning naming the file");
            foreach (var kv in before)
                CollectionAssert.AreEqual(kv.Value, File.ReadAllBytes(RoImportTestRoot.FullPath(kv.Key)), kv.Key);
        }

        [Test]
        public void TextureNamedAsTgaWithABmpBeside_SharesTheBmpsMaterial_WithoutACollision()
        {
            // T7.1 batch: izlude/iz_enchant names iz_rookie_06.tga while the data has .tga and .bmp; the texture pipeline
            // uses the .bmp (PLAN-0002 D19), so the material key must use the .bmp too, or two keys own one material file.
            string data = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "data");
            try
            {
                Directory.CreateDirectory(Path.Combine(data, "texture"));
                File.Copy(Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", "a.bmp"), Path.Combine(data, "texture", "a.bmp"));
                File.Copy(Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", "alpha32.tga"), Path.Combine(data, "texture", "a.tga"));
                var ctx = new RoImportContext(new RoFolderSource(data), _roots, _ledgerFolder) { Png = new RoPngCodec() };

                var first = RsmTestModels.Cube("first");
                var second = RsmTestModels.Cube("second");
                second.ParentName = "first";
                second.TextureSlots = new[] { 1 };
                var model = RsmTestModels.Model(first, second);
                model.Textures = new[] { "a.tga", "a.bmp" };
                var prefab = Convert(model, ctx);

                var materials = prefab.GetComponentsInChildren<MeshRenderer>().Select(r => r.sharedMaterial).Distinct().ToList();
                Assert.AreEqual(1, materials.Count, "one shared material");
                Assert.IsEmpty(ctx.Report.Issues.Where(i => i.Code == RoIssueCode.OutputCollision && i.Item.Contains("__")),
                    "no material collision: " + string.Join("; ", ctx.Report.Issues));
                // T7.2: the .tga is not a dependency (it was recorded with an empty hash, so the model was never current).
                var entry = ctx.Ledger(RoRsmConverter.Category).Find(Source);
                CollectionAssert.AreEqual(new[] { "texture/a.bmp" }, entry.Dependencies.Select(d => d.Source));
                Assert.IsFalse(string.IsNullOrEmpty(entry.Dependencies[0].Hash));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(data), recursive: true);
            }
        }

        [Test]
        public void TextureNamedAsTgaWithAPngBeside_UsesThePng()
        {
            // D12 (owner rule): anything over a .tga.
            string data = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "data");
            try
            {
                Directory.CreateDirectory(Path.Combine(data, "texture"));
                File.Copy(Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", "rgba.png"), Path.Combine(data, "texture", "b.png"));
                File.Copy(Path.Combine(RoFolderSourceTests.FixtureRoot, "texture", "alpha32.tga"), Path.Combine(data, "texture", "b.tga"));
                var ctx = new RoImportContext(new RoFolderSource(data), _roots, _ledgerFolder) { Png = new RoPngCodec() };
                var model = RsmTestModels.Model(RsmTestModels.Cube("only"));
                model.Textures = new[] { "b.tga" };
                Convert(model, ctx);
                var entry = ctx.Ledger(RoRsmConverter.Category).Find(Source);
                CollectionAssert.AreEqual(new[] { "texture/b.png" }, entry.Dependencies.Select(d => d.Source));
            }
            finally
            {
                Directory.Delete(Path.GetDirectoryName(data), recursive: true);
            }
        }

        [Test]
        public void Converter_IsRegistered_ForRsmAndRsm2()
        {
            var converter = RoConverterRegistry.For(RoJobKind.Model);
            Assert.IsInstanceOf<RoRsmConverter>(converter);
            Assert.IsTrue(converter.Handles(RoPath.Parse("model/a.rsm")));
            Assert.IsTrue(converter.Handles(RoPath.Parse("model/a.rsm2")));
            Assert.IsFalse(converter.Handles(RoPath.Parse("model/a.bmp")));
        }

        [Test]
        public void MissingSource_FailsWithOneWarning()
        {
            var ctx = Context();
            var result = new RoRsmConverter().Convert(ctx, RoPath.Parse("model/none.rsm"));
            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.SourceMissing, result.Failure.Code);
            Assert.AreEqual(1, ctx.Report.Issues.Count);
        }

        static string Hash(string folder) =>
            string.Join("|", Directory.GetFiles(folder, "*", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => f + ":" + new FileInfo(f).Length + ":" + File.GetLastWriteTimeUtc(f).Ticks));
    }
}
