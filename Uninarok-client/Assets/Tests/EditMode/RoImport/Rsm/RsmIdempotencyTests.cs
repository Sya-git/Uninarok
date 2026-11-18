using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Rsm;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T6.5 (SPEC-0002 FR-5, FR-16): reruns skip, forced reruns change no data file and no GUID, a deleted
    // prefab comes back, a changed texture makes the model stale. Uses a copy of 프론테라/시계탑.rsm (animated, three
    // textures) in a temporary data folder with generated textures, and writes under Assets/__RoImportTests.
    [Category(RsmTestData.Category)]
    public class RsmIdempotencyTests
    {
        const string Model = "model/프론테라/시계탑.rsm";
        static readonly string[] Textures = { "pron-ctwr1.bmp", "pron-ctwr2.bmp", "pron-ctwr3.bmp" };

        string _data;
        string _ledgerFolder;
        RoOutputRoots _roots;

        [SetUp]
        public void SetUp()
        {
            byte[] model = RsmTestData.ModelBytes("프론테라/시계탑.rsm");
            _data = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "data");
            Directory.CreateDirectory(Path.Combine(_data, "model", "프론테라"));
            Directory.CreateDirectory(Path.Combine(_data, "texture", "프론테라"));
            File.WriteAllBytes(Path.Combine(_data, "model", "프론테라", "시계탑.rsm"), model);
            foreach (string t in Textures) WriteTexture(t, keyed: false);
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _roots = RoImportTestRoot.Create();
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            foreach (string folder in new[] { Path.GetDirectoryName(_data), _ledgerFolder })
                if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        /// <summary>A 2 x 1 24-bit BMP: two white pixels (opaque), or white and magenta (keyed).</summary>
        void WriteTexture(string name, bool keyed)
        {
            var bmp = new List<byte>();
            void U16(int v) { bmp.Add((byte)v); bmp.Add((byte)(v >> 8)); }
            void U32(int v) { U16(v & 0xFFFF); U16(v >> 16); }
            bmp.Add((byte)'B'); bmp.Add((byte)'M'); U32(54 + 8); U32(0); U32(54);
            U32(40); U32(2); U32(1); U16(1); U16(24); U32(0); U32(8); U32(2835); U32(2835); U32(0); U32(0);
            bmp.AddRange(new byte[] { 255, 255, 255 });                                    // white (BGR)
            bmp.AddRange(keyed ? new byte[] { 255, 0, 255 } : new byte[] { 255, 255, 255 }); // magenta or white
            bmp.AddRange(new byte[] { 0, 0 });                                              // row padding to 4 bytes
            File.WriteAllBytes(Path.Combine(_data, "texture", "프론테라", name), bmp.ToArray());
        }

        RoResult<UnityEngine.Object> Run(bool force = false)
        {
            var ctx = new RoImportContext(new RoFolderSource(_data), _roots, _ledgerFolder) { Png = new RoPngCodec(), Force = force };
            var result = new RoRsmConverter().Convert(ctx, RoPath.Parse(Model));
            ctx.SaveLedgers();
            AssetDatabase.SaveAssets();
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            return result;
        }

        /// <summary>Every data file of the model (meshes, materials, clips, controller) with its bytes and GUID.</summary>
        Dictionary<string, (byte[] bytes, string guid)> DataFiles()
        {
            string art = RoImportTestRoot.FullPath(_roots.Art);
            return Directory.GetFiles(art, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta") && !f.EndsWith(".prefab"))
                .Where(f => f.EndsWith(".asset") || f.EndsWith(".mat") || f.EndsWith(".anim") || f.EndsWith(".controller"))
                .ToDictionary(f => f, f => (File.ReadAllBytes(f), AssetDatabase.AssetPathToGUID(ProjectPath(f))));
        }

        static string ProjectPath(string full) =>
            "Assets" + full.Replace('\\', '/').Substring(Application.dataPath.Length);

        string PrefabPath => RsmOutputPaths.For(_roots, RoPath.Parse(Model), Array.Empty<string>()).Prefab;

        [Test]
        public void SecondConversion_IsSkipped()
        {
            Assert.AreEqual(RoStatus.Created, Run().Status);
            Assert.AreEqual(RoStatus.Skipped, Run().Status);
        }

        [Test]
        public void ForcedSecondConversion_ChangesNoDataFileAndNoGuid()
        {
            Run();
            var before = DataFiles();
            string prefabGuid = AssetDatabase.AssetPathToGUID(PrefabPath);
            string prefabText = File.ReadAllText(RoImportTestRoot.FullPath(PrefabPath));
            Assert.Greater(before.Count, 5, "meshes, materials, clips and a controller were written");
            Assert.IsTrue(before.Keys.Any(k => k.EndsWith(".anim")) && before.Keys.Any(k => k.EndsWith(".controller")));

            Assert.AreNotEqual(RoStatus.Failed, Run(force: true).Status);
            var after = DataFiles();
            CollectionAssert.AreEquivalent(before.Keys, after.Keys);
            foreach (var kv in before)
            {
                CollectionAssert.AreEqual(kv.Value.bytes, after[kv.Key].bytes, $"bytes of {kv.Key}");
                Assert.AreEqual(kv.Value.guid, after[kv.Key].guid, $"guid of {kv.Key}");
            }
            Assert.AreEqual(prefabGuid, AssetDatabase.AssetPathToGUID(PrefabPath), "prefab GUID");
            Assert.AreEqual(prefabText, File.ReadAllText(RoImportTestRoot.FullPath(PrefabPath)),
                "prefab text (if this differs, record it in PLAN-0003 §9 and ask the owner, T6.5)");
        }

        [Test]
        public void DeletedPrefab_IsRestored_WithUnchangedMeshes()
        {
            Run();
            var before = DataFiles();
            AssetDatabase.DeleteAsset(PrefabPath);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));

            Assert.AreNotEqual(RoStatus.Skipped, Run().Status, "a missing output makes the model stale");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab);
            Assert.IsTrue(prefab.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh != null));
            var after = DataFiles();
            foreach (var kv in before.Where(k => k.Key.EndsWith(".asset")))
                CollectionAssert.AreEqual(kv.Value.bytes, after[kv.Key].bytes, kv.Key);
        }

        [Test]
        public void ChangedTextureContent_MakesTheModelStale_AndUpdatesItsMaterialMode()
        {
            Run();
            Assert.IsTrue(Surfaces().All(s => s.EndsWith("opaque") || s.EndsWith("opaque_2s")), string.Join(", ", Surfaces()));

            WriteTexture(Textures[0], keyed: true);
            Assert.AreNotEqual(RoStatus.Skipped, Run().Status, "a changed texture makes the model stale");
            Assert.IsTrue(Surfaces().Any(s => s.Contains("pron-ctwr1__cutout")), string.Join(", ", Surfaces()));

            WriteTexture(Textures[0], keyed: false);
            Assert.AreNotEqual(RoStatus.Skipped, Run().Status);
            Assert.IsFalse(Surfaces().Any(s => s.Contains("cutout")), string.Join(", ", Surfaces()));
        }

        IEnumerable<string> Surfaces()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials)
                .Select(m => Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(m))).Distinct();
        }
    }
}
