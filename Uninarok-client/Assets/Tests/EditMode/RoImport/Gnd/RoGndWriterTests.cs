using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Materials;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Gnd;
using Uninarok.RoImport.Editor.Materials;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Writing;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T5.1 (FR-1, FR-6; SPEC-0002 FR-5, FR-16): converting a map built in code writes the chunk meshes and the
    // ground prefab at the paths of PLAN-0004 §3, with shared materials and the setup of §4.6. Writes under
    // Assets/__RoImportTests; textures come from the test fixture folder.
    public class RoGndWriterTests
    {
        static RoPath Source => RoPath.Parse("sample.gnd");

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

        /// <summary>
        /// 20 x 4 cubes (two chunks): columns 0-15 at height 0, 16-19 ten units higher, so a wall stands on the chunk
        /// border. Tops use the fixture's <c>a.bmp</c>, walls <c>KEY8.BMP</c>.
        /// </summary>
        static RoGnd Sample(params string[] textures)
        {
            var gnd = GndTestMaps.Build(20, 4, (c, r, dx, dz) => c < 16 ? 0f : -10f);
            gnd.Textures = textures.Length > 0 ? textures : new[] { "a.bmp", "KEY8.BMP" };
            return gnd;
        }

        GameObject Convert(RoGnd gnd, RoImportContext ctx = null)
        {
            ctx = ctx ?? Context();
            var result = RoGndConverter.Write(ctx, Source, gnd, "hash");
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            ctx.SaveLedgers();
            Assert.IsNotNull(result.Value);
            return result.Value;
        }

        string PrefabPath => _roots.Art + "/map/sample/sample_ground.prefab";
        string ChunkPath(int cx, int cz) => $"{_roots.Art}/map/sample/ground/chunk_{cx}_{cz}.asset";

        [Test]
        public void OutputPaths_FollowThePlan()
        {
            var paths = RoGndOutputPaths.For(_roots, Source);
            Assert.AreEqual(PrefabPath, paths.Prefab);
            Assert.AreEqual(_roots.Art + "/map/sample/ground", paths.Folder);
            Assert.AreEqual(ChunkPath(1, 0), paths.Chunk(1, 0));
        }

        [Test]
        public void SyntheticMap_YieldsThePrefabAndOneMeshPerChunk_AtThePlanPaths()
        {
            var prefab = Convert(Sample());
            Assert.AreEqual(PrefabPath, AssetDatabase.GetAssetPath(prefab));
            foreach (string path in new[] { ChunkPath(0, 0), ChunkPath(1, 0) })
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(mesh, path);
                Assert.IsTrue(AssetDatabase.IsMainAsset(mesh), "a chunk mesh is its own asset (SPEC-0002 FR-10)");
                Assert.Greater(mesh.triangles.Length, 0);
                Assert.AreEqual(IndexFormat.UInt16, mesh.indexFormat, "16-bit indices (PLAN-0004 4.4)");
            }
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(ChunkPath(2, 0))), "no third chunk");
        }

        [Test]
        public void Prefab_HasARootWithoutTransform_AndOneChildPerChunk_PlacedInMapSpace()
        {
            var gnd = Sample();
            var prefab = Convert(gnd);
            Assert.AreEqual("sample_ground", prefab.name, "the root is named after the file (D14)");
            Assert.AreEqual(Vector3.zero, prefab.transform.localPosition);
            Assert.AreEqual(Quaternion.identity, prefab.transform.localRotation);
            Assert.AreEqual(Vector3.one, prefab.transform.localScale);
            Assert.IsNull(prefab.GetComponent<MeshRenderer>(), "the root draws nothing itself");

            Assert.IsTrue(RoGndRules.TryGet(1, 7, out var rules));
            var built = RoGndMeshBuilder.Build(gnd, rules, new RoReport());
            CollectionAssert.AreEquivalent(built.Select(m => $"Chunk_{m.ChunkColumn}_{m.ChunkRow}"),
                Enumerable.Range(0, prefab.transform.childCount).Select(i => prefab.transform.GetChild(i).name));
            foreach (var m in built)
            {
                var chunk = prefab.transform.Find($"Chunk_{m.ChunkColumn}_{m.ChunkRow}");
                Assert.AreEqual(m.Origin, chunk.localPosition, "chunk placed at its origin");
                Assert.AreEqual(ChunkPath(m.ChunkColumn, m.ChunkRow), AssetDatabase.GetAssetPath(chunk.GetComponent<MeshFilter>().sharedMesh));
            }
        }

        [Test]
        public void Chunks_AreStatic_AndCastAndReceiveShadows()   // no occlusion flag: obsolete in Unity 6.7 (D15)
        {
            var prefab = Convert(Sample());
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode, "casts real-time shadows");
                Assert.IsTrue(renderer.receiveShadows, "receives real-time shadows");
                Assert.AreEqual(LightProbeUsage.Off, renderer.lightProbeUsage);
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                Assert.IsTrue(flags.HasFlag(StaticEditorFlags.ContributeGI), flags.ToString());
                Assert.IsTrue(flags.HasFlag(StaticEditorFlags.BatchingStatic), flags.ToString());
            }
        }

        [Test]
        public void Materials_AreTheSharedOnes_OnePerTexture_InSubmeshOrder()
        {
            var prefab = Convert(Sample());
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                Assert.AreEqual(r.GetComponent<MeshFilter>().sharedMesh.subMeshCount, r.sharedMaterials.Length);
                foreach (var m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m);
                    StringAssert.StartsWith(_roots.Art + "/" + RoOutputPaths.MaterialsFolder + "/", AssetDatabase.GetAssetPath(m));
                }
            }
            // tops (a.bmp) in both chunks, walls (KEY8.BMP, keyed) in chunk 0: two materials in all, shared by the chunks
            var all = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToList();
            Assert.AreEqual(2, all.Count, string.Join(", ", all.Select(AssetDatabase.GetAssetPath)));
            var first = prefab.transform.Find("Chunk_0_0").GetComponent<MeshRenderer>().sharedMaterials;
            var second = prefab.transform.Find("Chunk_1_0").GetComponent<MeshRenderer>().sharedMaterials;
            Assert.AreEqual(2, first.Length, "submesh 0 = texture 0 (tops), submesh 1 = texture 1 (walls)");
            Assert.AreSame(first[0], second[0], "one material per texture across chunks (FR-6)");
            StringAssert.Contains("key8", AssetDatabase.GetAssetPath(first[1]), "paths are canonical, lower case");
        }

        // PLAN-0004 D17, TASKS-0004 T8.6: the ground draws with Clamp copies of its textures, so a tile that ends on the
        // texture edge does not blend in the opposite edge.
        [Test]
        public void GroundMaterials_AreGroundOnes_OnClampCopiesOfTheTextures()
        {
            var prefab = Convert(Sample());
            var materials = prefab.transform.Find("Chunk_0_0").GetComponent<MeshRenderer>().sharedMaterials;
            StringAssert.IsMatch(@"/_materials/texture/a__[a-z]+_ground\.mat$", AssetDatabase.GetAssetPath(materials[0]));
            StringAssert.IsMatch(@"/_materials/texture/key8__[a-z]+_ground\.mat$", AssetDatabase.GetAssetPath(materials[1]));
            foreach (var m in materials)
            {
                var texture = m.GetTexture("_BaseMap");
                StringAssert.EndsWith("__ground.png", AssetDatabase.GetAssetPath(texture));
                Assert.AreEqual(TextureWrapMode.Clamp, texture.wrapMode, AssetDatabase.GetAssetPath(texture));
            }
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(RoOutputPaths.Texture(_roots, RoPath.Parse("texture/a.bmp")))),
                "the ground does not make the model's Repeat texture");
        }

        [Test]
        public void AModelAndAGround_SharingATexture_GetDifferentMaterialsAndTextures()
        {
            var ctx = Context();
            var prefab = Convert(Sample(), ctx);
            var ground = prefab.transform.Find("Chunk_0_0").GetComponent<MeshRenderer>().sharedMaterials[0];
            var model = ctx.Material(new RoMaterialKey(RoPath.Parse("texture/a.bmp"), RoSurface.Cutout, false), "model/a.rsm").Value;

            Assert.IsNotNull(model);
            Assert.AreNotEqual(AssetDatabase.GetAssetPath(ground), AssetDatabase.GetAssetPath(model));
            var modelTexture = model.GetTexture("_BaseMap");
            Assert.AreEqual(RoOutputPaths.Texture(_roots, RoPath.Parse("texture/a.bmp")), AssetDatabase.GetAssetPath(modelTexture));
            Assert.AreEqual(TextureWrapMode.Repeat, modelTexture.wrapMode, "the model keeps its repeat");
            Assert.AreEqual(TextureWrapMode.Clamp, ground.GetTexture("_BaseMap").wrapMode);
        }

        [TestCase("model/a.rsm")]
        [TestCase("model/b.rsm2")]
        public void AGroundMaterial_IsNeverGivenToAModel(string model)
        {
            var key = new RoMaterialKey(RoPath.Parse("texture/a.bmp"), RoSurface.Opaque, false, ground: true);
            Assert.Throws<ArgumentException>(() => RoMaterialFactory.GetOrCreate(Context(), key, model));
            Assert.IsFalse(Directory.Exists(RoImportTestRoot.FullPath(_roots.Art + "/_materials")), "no material written");
        }

        [Test]
        public void MissingTexture_GivesThePlaceholder_AndOneWarningNamingTheTextureAndTheMap()
        {
            var ctx = Context();
            var prefab = Convert(Sample("a.bmp", "none.bmp"), ctx);
            var walls = prefab.transform.Find("Chunk_0_0").GetComponent<MeshRenderer>().sharedMaterials[1];
            Assert.AreEqual(RoOutputPaths.Placeholder(_roots, RoPlaceholders.TextureName),
                AssetDatabase.GetAssetPath(walls.GetTexture("_BaseMap")));

            var missing = ctx.Report.Issues.Where(i => i.Code == RoIssueCode.TextureMissing).ToList();
            Assert.AreEqual(1, missing.Count, string.Join("; ", ctx.Report.Issues));
            StringAssert.Contains("none.bmp", missing[0].Item);
            Assert.AreEqual(Source.Canonical, missing[0].Referrer);
        }

        [Test]
        public void UnsupportedVersion_FailsWithOneWarning_AndLeavesNoPartialOutput()
        {
            var gnd = Sample();
            gnd.VersionMinor = 5;                           // no rules row (PLAN-0004 4.6)
            var ctx = Context();
            var result = RoGndConverter.Write(ctx, Source, gnd, "hash");
            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.UnsupportedGndVersion, result.Failure.Code);
            Assert.AreEqual(1, ctx.Report.Issues.Count(i => i.Code == RoIssueCode.UnsupportedGndVersion));
            Assert.IsFalse(Directory.Exists(RoImportTestRoot.FullPath(_roots.Art + "/map")), "no ground output at all");
        }

        [Test]
        public void FailingConversion_LeavesThePreviousOutputsUnchanged()
        {
            Convert(Sample());
            var files = new[] { PrefabPath, ChunkPath(0, 0), ChunkPath(1, 0) };
            var before = files.ToDictionary(p => p, p => File.ReadAllBytes(RoImportTestRoot.FullPath(p)));

            var unsupported = Sample();
            unsupported.VersionMinor = 9;
            var result = RoGndConverter.Write(Context(), Source, unsupported, "other");
            Assert.AreEqual(RoStatus.Failed, result.Status);
            foreach (var kv in before)
                CollectionAssert.AreEqual(kv.Value, File.ReadAllBytes(RoImportTestRoot.FullPath(kv.Key)), kv.Key);
        }

        [Test]
        public void Ledger_ListsThePrefabAndMeshes_AndTheTexturesAsDependencies()
        {
            var ctx = Context();
            Convert(Sample(), ctx);
            var entry = ctx.Ledger(RoGndConverter.Category).Find(Source);
            Assert.IsNotNull(entry);
            CollectionAssert.AreEquivalent(new[] { PrefabPath, ChunkPath(0, 0), ChunkPath(1, 0) }, entry.Outputs);
            CollectionAssert.AreEquivalent(new[] { "texture/a.bmp", "texture/key8.bmp" }, entry.Dependencies.Select(d => d.Source));
            Assert.IsTrue(entry.Dependencies.All(d => !string.IsNullOrEmpty(d.Hash)));
        }
    }
}
