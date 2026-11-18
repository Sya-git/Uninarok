using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Gnd;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T5.1 (FR-6; SPEC-0002 FR-5, FR-16): reruns skip; a forced rerun changes no mesh byte and no GUID; the
    // prefab and a scene that reference a chunk mesh keep their references when the ground changes. The NeedsData tests
    // use a copy of izlude.gnd in a temporary data folder with generated textures; all write under Assets/__RoImportTests.
    public class RoGndIdempotencyTests
    {
        const string Map = "izlude";

        string _data;
        string _ledgerFolder;
        RoOutputRoots _roots;

        [SetUp]
        public void SetUp()
        {
            _data = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "data");
            Directory.CreateDirectory(_data);
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _roots = RoImportTestRoot.Create();
        }

        [TearDown]
        public void TearDown()
        {
            // Leave an untitled scene open rather than one under the test folder, which is about to be deleted.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path.StartsWith(RoImportTestRoot.Folder))
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    break;
                }
            RoImportTestRoot.Delete();
            foreach (string folder in new[] { Path.GetDirectoryName(_data), _ledgerFolder })
                if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        /// <summary>Copies izlude.gnd into the temporary data folder with a 2 x 1 white BMP for each of its textures.</summary>
        void CopyIzlude()
        {
            byte[] bytes = GndTestData.MapBytes(Map);
            File.WriteAllBytes(Path.Combine(_data, Map + ".gnd"), bytes);
            Assert.IsTrue(RoGndReader.TryRead(RoPath.Parse(Map + ".gnd"), bytes, null, out var gnd, out var reason), reason);
            foreach (string texture in gnd.Textures)
            {
                string file = Path.Combine(_data, "texture", texture.Replace('\\', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, WhiteBmp());
            }
        }

        static byte[] WhiteBmp()
        {
            var bmp = new List<byte>();
            void U16(int v) { bmp.Add((byte)v); bmp.Add((byte)(v >> 8)); }
            void U32(int v) { U16(v & 0xFFFF); U16(v >> 16); }
            bmp.Add((byte)'B'); bmp.Add((byte)'M'); U32(54 + 8); U32(0); U32(54);
            U32(40); U32(2); U32(1); U16(1); U16(24); U32(0); U32(8); U32(2835); U32(2835); U32(0); U32(0);
            bmp.AddRange(new byte[] { 255, 255, 255, 255, 255, 255, 0, 0 });
            return bmp.ToArray();
        }

        RoImportContext Context(bool force = false, string data = null) =>
            new RoImportContext(new RoFolderSource(data ?? _data), _roots, _ledgerFolder) { Png = new RoPngCodec(), Force = force };

        RoResult<UnityEngine.Object> Run(bool force = false)
        {
            var ctx = Context(force);
            var result = new RoGndConverter().Convert(ctx, RoPath.Parse(Map + ".gnd"));
            ctx.SaveLedgers();
            AssetDatabase.SaveAssets();
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            return result;
        }

        RoGndOutputPaths Paths => RoGndOutputPaths.For(_roots, RoPath.Parse(Map + ".gnd"));

        /// <summary>Every chunk mesh with its bytes and GUID.</summary>
        Dictionary<string, (byte[] bytes, string guid)> Meshes()
        {
            string folder = RoImportTestRoot.FullPath(Paths.Folder);
            return Directory.GetFiles(folder, "*.asset")
                .ToDictionary(f => f, f => (File.ReadAllBytes(f), AssetDatabase.AssetPathToGUID(ProjectPath(f))));
        }

        static string ProjectPath(string full) => "Assets" + full.Replace('\\', '/').Substring(Application.dataPath.Length);

        [Test, Category(GndTestData.Category)]
        public void SecondConversion_IsSkipped()
        {
            CopyIzlude();
            Assert.AreEqual(RoStatus.Created, Run().Status);
            Assert.AreEqual(RoStatus.Skipped, Run().Status);
        }

        [Test, Category(GndTestData.Category)]
        public void ForcedSecondConversion_ChangesNoMeshByte_AndNoGuid()
        {
            CopyIzlude();
            Run();
            var before = Meshes();
            Assert.AreEqual(9 * 10, before.Count, "izlude is 134 x 150 cubes: 9 x 10 chunks");
            string prefabGuid = AssetDatabase.AssetPathToGUID(Paths.Prefab);
            string prefabText = File.ReadAllText(RoImportTestRoot.FullPath(Paths.Prefab));

            Assert.AreNotEqual(RoStatus.Failed, Run(force: true).Status);
            var after = Meshes();
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (var kv in before)
            {
                CollectionAssert.AreEqual(kv.Value.bytes, after[kv.Key].bytes, $"bytes of {kv.Key}");
                Assert.AreEqual(kv.Value.guid, after[kv.Key].guid, $"guid of {kv.Key}");
            }
            Assert.AreEqual(prefabGuid, AssetDatabase.AssetPathToGUID(Paths.Prefab), "prefab GUID");
            Assert.AreEqual(prefabText, File.ReadAllText(RoImportTestRoot.FullPath(Paths.Prefab)), "prefab text");
        }

        [Test]
        public void PrefabAndScene_KeepTheirReferences_WhenTheGroundChanges()
        {
            // A map built in code, converted, referenced from a saved scene, then converted again with other heights:
            // the chunk meshes are updated in place, so the prefab and the scene still point at them (SPEC-0002 FR-16).
            var source = RoPath.Parse("sample.gnd");
            var paths = RoGndOutputPaths.For(_roots, source);
            RoGnd Sample(float high)
            {
                var gnd = GndTestMaps.Build(20, 4, (c, r, dx, dz) => c < 16 ? 0f : high);
                gnd.Textures = new[] { "a.bmp", "KEY8.BMP" };
                return gnd;
            }

            var ctx = Context(data: RoFolderSourceTests.FixtureRoot);
            Assert.AreNotEqual(RoStatus.Failed, RoGndConverter.Write(ctx, source, Sample(-10f), "one").Status);
            ctx.SaveLedgers();
            string chunk = paths.Chunk(1, 0);
            string meshGuid = AssetDatabase.AssetPathToGUID(chunk);
            string prefabGuid = AssetDatabase.AssetPathToGUID(paths.Prefab);
            var meshBefore = File.ReadAllBytes(RoImportTestRoot.FullPath(chunk));

            string scenePath = RoImportTestRoot.Folder + "/reference.unity";
            // Single mode: the runner's own untitled scene cannot stay beside an additive new one; the EditMode runner
            // restores the scene setup after the run, and TearDown leaves an untitled scene again.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var holder = new GameObject("holder");
            holder.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(chunk);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths.Prefab), scene);
            Assert.IsNotNull(instance);
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);   // nothing holds the meshes in memory

            ctx = Context(data: RoFolderSourceTests.FixtureRoot);
            Assert.AreNotEqual(RoStatus.Failed, RoGndConverter.Write(ctx, source, Sample(-30f), "two").Status);
            ctx.SaveLedgers();
            AssetDatabase.SaveAssets();
            CollectionAssert.AreNotEqual(meshBefore, File.ReadAllBytes(RoImportTestRoot.FullPath(chunk)), "the mesh did change");
            Assert.AreEqual(meshGuid, AssetDatabase.AssetPathToGUID(chunk), "mesh GUID kept");
            Assert.AreEqual(prefabGuid, AssetDatabase.AssetPathToGUID(paths.Prefab), "prefab GUID kept");

            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);   // as saved on disk
            var roots = scene.GetRootGameObjects();
            var filter = roots.Single(g => g.name == "holder").GetComponent<MeshFilter>();
            Assert.AreEqual(chunk, AssetDatabase.GetAssetPath(filter.sharedMesh), "the scene's mesh reference survives");
            var placed = roots.Single(g => g != filter.gameObject);
            Assert.AreEqual(paths.Prefab, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(placed), "the scene's prefab instance survives");
            Assert.IsTrue(placed.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null), "and its chunks resolve");
        }
    }
}
