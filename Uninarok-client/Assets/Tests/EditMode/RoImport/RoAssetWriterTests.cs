using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Imaging;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-4 (standalone assets), FR-5 (idempotent, references kept), FR-16 (no rewrite of identical output).
    // Plan 4.7. Writes only under Assets/__RoImportTests, removed in TearDown. TASKS T4.3.
    public class RoAssetWriterTests
    {
        static readonly DateTime Old = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        RoOutputRoots _roots;
        RoAssetWriter _writer;

        [SetUp]
        public void SetUp()
        {
            _roots = RoImportTestRoot.Create();
            _writer = new RoAssetWriter(_roots);
        }

        [TearDown]
        public void TearDown()
        {
            while (_writer != null && _writer.InBatch) _writer.EndBatch();
            RoImportTestRoot.Delete();
        }

        string ArtPath(string relative) => _roots.Art + "/" + relative;

        static byte[] Png(Color32 color)
        {
            var image = new RoImage(2, 2);
            for (int i = 0; i < 4; i++) image.SetPixel(i % 2, i / 2, color);
            return new RoPngCodec().Encode(image);
        }

        static void SetWrap(AssetImporter importer, TextureWrapMode mode) => ((TextureImporter)importer).wrapMode = mode;

        // ---- WriteBytes ----

        [Test]
        public void WriteBytes_New_IsCreatedAndImported()
        {
            string path = ArtPath("texture/a.png");

            Assert.AreEqual(RoStatus.Created, _writer.WriteBytes(path, Png(Color.red)));

            Assert.IsTrue(_writer.Exists(path));
            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(path), "Unity assigns the GUID (D4)");
            Assert.IsNotNull(_writer.Load<Texture2D>(path));
        }

        [Test]
        public void WriteBytes_IdenticalBytes_IsSkipped_AndTouchesNoFile()
        {
            string path = ArtPath("texture/a.png");
            byte[] png = Png(Color.red);
            _writer.WriteBytes(path, png);
            File.SetLastWriteTimeUtc(RoImportTestRoot.FullPath(path), Old);

            Assert.AreEqual(RoStatus.Skipped, _writer.WriteBytes(path, png));

            Assert.AreEqual(Old, File.GetLastWriteTimeUtc(RoImportTestRoot.FullPath(path)));
        }

        [Test]
        public void WriteBytes_DifferentBytes_IsUpdated_AndKeepsTheGuid()
        {
            string path = ArtPath("texture/a.png");
            _writer.WriteBytes(path, Png(Color.red));
            string guid = AssetDatabase.AssetPathToGUID(path);

            Assert.AreEqual(RoStatus.Updated, _writer.WriteBytes(path, Png(Color.green)));

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            CollectionAssert.AreEqual(Png(Color.green), File.ReadAllBytes(RoImportTestRoot.FullPath(path)));
        }

        [Test]
        public void WriteBytes_AppliesImporterSettings_OnlyWhenTheyDiffer()
        {
            string path = ArtPath("texture/a.png");
            byte[] png = Png(Color.red);

            Assert.AreEqual(RoStatus.Created, _writer.WriteBytes(path, png, i => SetWrap(i, TextureWrapMode.Clamp)));
            Assert.AreEqual(TextureWrapMode.Clamp, ((TextureImporter)AssetImporter.GetAtPath(path)).wrapMode);

            string meta = RoImportTestRoot.FullPath(path) + ".meta";
            File.SetLastWriteTimeUtc(RoImportTestRoot.FullPath(path), Old);
            File.SetLastWriteTimeUtc(meta, Old);
            Assert.AreEqual(RoStatus.Skipped, _writer.WriteBytes(path, png, i => SetWrap(i, TextureWrapMode.Clamp)));
            Assert.AreEqual(Old, File.GetLastWriteTimeUtc(meta), "same settings: no reimport, .meta untouched");

            Assert.AreEqual(RoStatus.Updated, _writer.WriteBytes(path, png, i => SetWrap(i, TextureWrapMode.Repeat)),
                "same bytes but new settings is an update");
            Assert.AreEqual(TextureWrapMode.Repeat, ((TextureImporter)AssetImporter.GetAtPath(path)).wrapMode);
        }

        // ---- UpsertObject ----

        static Material NewMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            material.color = color;
            return material;
        }

        [Test]
        public void UpsertObject_New_IsCreated()
        {
            string path = ArtPath("_materials/a.mat");

            Assert.AreEqual(RoStatus.Created, _writer.UpsertObject(path, NewMaterial(Color.red)));

            Assert.IsNotEmpty(AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(Color.red, _writer.Load<Material>(path).color);
        }

        [Test]
        public void UpsertObject_Existing_IsUpdatedInPlace_AndReferencesSurvive()
        {
            string path = ArtPath("_materials/a.mat");
            _writer.UpsertObject(path, NewMaterial(Color.red));
            string guid = AssetDatabase.AssetPathToGUID(path);
            var original = _writer.Load<Material>(path);

            // Something that references the material: a prefab with a renderer using it.
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.GetComponent<MeshRenderer>().sharedMaterial = original;
            string prefabPath = _roots.Art + "/user.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);

            var fresh = NewMaterial(Color.green);
            Assert.AreEqual(RoStatus.Updated, _writer.UpsertObject(path, fresh));

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path), "never deleted and re-created (FR-5)");
            var updated = _writer.Load<Material>(path);
            // Unity may hand out a new managed wrapper per load; identity is the native object.
            Assert.IsTrue(original == updated, "the same object, updated in place");
            Assert.AreEqual(original.GetEntityId(), updated.GetEntityId());
            Assert.AreEqual(Color.green, updated.color);
            Assert.IsTrue(fresh == null, "the fresh copy is destroyed after the copy");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsTrue(updated == prefab.GetComponent<MeshRenderer>().sharedMaterial, "the prefab still references it");
        }

        [Test]
        public void UpsertObject_IdenticalContent_IsSkipped_AndTouchesNoFile()
        {
            string path = ArtPath("_materials/a.mat");
            _writer.UpsertObject(path, NewMaterial(Color.red));
            AssetDatabase.SaveAssets();
            File.SetLastWriteTimeUtc(RoImportTestRoot.FullPath(path), Old);

            // A copy of the saved material: URP adds tags and pass settings on save that a bare new Material lacks
            // (the material factory sets those explicitly, plan 4.6).
            var fresh = new Material(_writer.Load<Material>(path));
            Assert.AreEqual(RoStatus.Skipped, _writer.UpsertObject(path, fresh));
            AssetDatabase.SaveAssets();

            Assert.AreEqual(Old, File.GetLastWriteTimeUtc(RoImportTestRoot.FullPath(path)));
            Assert.IsTrue(fresh == null, "the fresh copy is destroyed when skipped too");
        }

        [Test]
        public void UpsertObject_Mesh_KeepsItsGuid()
        {
            string path = ArtPath("model/a/mesh.asset");
            var first = new Mesh { vertices = new[] { Vector3.zero, Vector3.right, Vector3.up }, triangles = new[] { 0, 1, 2 } };
            _writer.UpsertObject(path, first);
            string guid = AssetDatabase.AssetPathToGUID(path);

            var second = new Mesh
            {
                vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one },
                triangles = new[] { 0, 1, 2, 1, 3, 2 }
            };
            Assert.AreEqual(RoStatus.Updated, _writer.UpsertObject(path, second));

            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(4, _writer.Load<Mesh>(path).vertexCount);
        }

        // ---- Allowed paths ----

        [Test]
        public void PathsOutsideTheOutputRoots_Throw()
        {
            byte[] png = Png(Color.red);

            Assert.Throws<ArgumentException>(() => _writer.WriteBytes("Assets/Elsewhere/a.png", png));
            Assert.Throws<ArgumentException>(() => _writer.WriteBytes(RoImportTestRoot.Folder + "/ArtX/a.png", png), "a prefix is not a root");
            Assert.Throws<ArgumentException>(() => _writer.WriteBytes(_roots.Art + "/../../Elsewhere/a.png", png), "no way out with ..");
            Assert.Throws<ArgumentException>(() => _writer.UpsertObject("Assets/Elsewhere/a.mat", NewMaterial(Color.red)));
        }

        [Test]
        public void TheHandMadeAdditiveScene_IsNeverWritten()
        {
            Assert.Throws<ArgumentException>(() =>
                _writer.WriteBytes(RoOutputPaths.AdditiveScene(_roots, "prontera"), new byte[] { 1 }));
        }

        [Test]
        public void AllThreeRoots_AreWritable()
        {
            Assert.AreEqual(RoStatus.Created, _writer.WriteBytes(ArtPath("a.png"), Png(Color.red)));
            Assert.AreEqual(RoStatus.Created, _writer.UpsertObject(RoOutputPaths.Data(_roots, "Map", "x", ".asset"), ScriptableObject.CreateInstance<ScriptableObject>()));
            Assert.AreEqual(RoStatus.Created, _writer.WriteBytes(_roots.Scenes + "/notes.txt", new byte[] { 65 }));
        }

        // ---- Batching ----

        [Test]
        public void BeginAndEndBatch_Nest()
        {
            _writer.BeginBatch();
            _writer.BeginBatch();
            Assert.IsTrue(_writer.InBatch);

            _writer.EndBatch();
            Assert.IsTrue(_writer.InBatch, "still inside the outer batch");

            _writer.EndBatch();
            Assert.IsFalse(_writer.InBatch);
            Assert.Throws<InvalidOperationException>(() => _writer.EndBatch());
        }

        [Test]
        public void WritesInsideABatch_AreAvailableAfterIt()
        {
            string path = ArtPath("texture/a.png");

            _writer.BeginBatch();
            Assert.AreEqual(RoStatus.Created, _writer.WriteBytes(path, Png(Color.red)));
            _writer.EndBatch();

            Assert.IsNotNull(_writer.Load<Texture2D>(path));
        }
    }
}
