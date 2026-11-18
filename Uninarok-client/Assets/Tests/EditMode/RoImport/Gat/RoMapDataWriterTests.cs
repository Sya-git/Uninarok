using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Uninarok.Map;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Gat;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T4.1 / T4.2 (FR-1, FR-3, FR-6, FR-8; SPEC-0002 FR-5, FR-16): a synthetic map converts to one MapData asset
    // at Data/map/<name>.asset, in place, with the optional type image. Sources are written to a temporary data folder;
    // outputs go under Assets/__RoImportTests.
    [Ignore("pending T4.2")]
    public class RoMapDataWriterTests
    {
        const string Map = "sample";

        string _data;
        string _ledgerFolder;
        RoOutputRoots _roots;

        [SetUp]
        public void SetUp()
        {
            _data = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "data");
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _roots = RoImportTestRoot.Create();
            GatTestFiles.WriteMap(_data, Map, GatTestMaps.Asymmetric8x6(), 4, 3);
        }

        [TearDown]
        public void TearDown()
        {
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

        string AssetPath => _roots.Data + "/map/" + Map + ".asset";
        string ImagePath => _roots.Art + "/map/" + Map + "/" + Map + "_cells.png";

        RoImportContext Context(bool force = false, bool image = false) =>
            new RoImportContext(new RoFolderSource(_data), _roots, _ledgerFolder) { Force = force, GatImage = image };

        RoResult<MapData> Convert(bool force = false, bool image = false)
        {
            var ctx = Context(force, image);
            var result = ctx.MapData(RoPath.Parse(Map + ".gat"));
            ctx.SaveLedgers();
            AssetDatabase.SaveAssets();
            return result;
        }

        [Test]
        public void OutputPaths_FollowThePlan()
        {
            var paths = RoMapDataOutputPaths.For(_roots, RoPath.Parse(Map + ".gat"));

            Assert.AreEqual(AssetPath, paths.Asset);
            Assert.AreEqual(ImagePath, paths.Image);
        }

        [Test]
        public void ASyntheticMap_ConvertsToOneAsset_WithTheRightFields()
        {
            var result = Convert();

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.ToString());
            var data = AssetDatabase.LoadAssetAtPath<MapData>(AssetPath);
            Assert.IsNotNull(data);
            Assert.AreSame(data, result.Value);
            Assert.AreEqual(Map, data.name);
            Assert.AreEqual(Map, data.MapName);
            Assert.AreEqual((8, 6), (data.CellsX, data.CellsZ));
            Assert.AreEqual(0.5f, data.CellSize, 1e-6f);
            Assert.AreEqual(MapCellType.Wall, data.TypeAt(7, 0));
            Assert.AreEqual(MapCellType.Cliff, data.TypeAt(0, 5));
            Assert.IsTrue(data.IsWalkableWater(1, 2));
            // cell (3, 2): corners -(3 + 20) ... in Ragnarok units, Y down: 2.3 Unity units up, etc.
            Assert.AreEqual(new Vector4(2.3f, 2.4f, 3.3f, 3.4f).ToString("F4"), data.CornerHeights(3, 2).ToString("F4"));
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:" + nameof(MapData), new[] { RoImportTestRoot.Folder }).Length, "one asset per map");
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(ImagePath)), "no image unless requested");
        }

        [Test]
        public void TheSecondConversion_IsSkipped()
        {
            Assert.AreEqual(RoStatus.Created, Convert().Status);

            var second = Convert();

            Assert.AreEqual(RoStatus.Skipped, second.Status);
            Assert.IsNotNull(second.Value);
        }

        [Test]
        public void AForcedConversion_LeavesTheAssetByteIdentical_AndItsGuid()
        {
            Convert();
            string guid = AssetDatabase.AssetPathToGUID(AssetPath);
            byte[] before = File.ReadAllBytes(RoImportTestRoot.FullPath(AssetPath));

            var forced = Convert(force: true);

            Assert.AreNotEqual(RoStatus.Failed, forced.Status);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(RoImportTestRoot.FullPath(AssetPath)));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetPath));
        }

        [Test]
        public void AChangedSource_UpdatesTheAssetInPlace_AndAMapRootKeepsItsReference()
        {
            var data = Convert().Value;
            string guid = AssetDatabase.AssetPathToGUID(AssetPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Map").AddComponent<MapRoot>().Set(Map, data);
            string scenePath = RoImportTestRoot.Folder + "/map.unity";
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, scenePath));

            // the wall moves from (7, 0) to (6, 0)
            var changed = GatTestMaps.Build(8, 6, (x, z) => 0f, (x, z) => x == 6 && z == 0 ? 1u : 0u);
            File.WriteAllBytes(Path.Combine(_data, Map + ".gat"), GatTestFiles.Gat(changed));
            var updated = Convert();

            Assert.AreEqual(RoStatus.Updated, updated.Status);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(AssetPath));
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var root = UnityEngine.Object.FindAnyObjectByType<MapRoot>();
            Assert.IsNotNull(root.Data, "the reference survives");
            Assert.AreEqual(AssetPath, AssetDatabase.GetAssetPath(root.Data));
            Assert.AreEqual(MapCellType.Wall, root.Data.TypeAt(6, 0));
            Assert.AreEqual(MapCellType.Walkable, root.Data.TypeAt(7, 0));
        }

        [Test]
        public void AGridThatIsNotTwiceTheGround_FailsWithGatGndMismatch_AndWritesNothing()
        {
            File.WriteAllBytes(Path.Combine(_data, Map + ".gnd"), GatTestFiles.Gnd(4, 4));
            var ctx = Context();

            var result = ctx.MapData(RoPath.Parse(Map + ".gat"));

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.GatGndMismatch, result.Failure.Code);
            Assert.AreEqual(1, ctx.Report.Issues.Count(i => i.Code == RoIssueCode.GatGndMismatch), "reported once");
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(AssetPath)), "no partial output");
            Assert.IsNull(ctx.Ledger(RoMapDataConverter.Category).Find(RoPath.Parse(Map + ".gat")));
        }

        [Test]
        public void AMissingGround_FailsWithSourceMissing()
        {
            File.Delete(Path.Combine(_data, Map + ".gnd"));

            var result = Context().MapData(RoPath.Parse(Map + ".gat"));

            Assert.AreEqual(RoStatus.Failed, result.Status);
            Assert.AreEqual(RoIssueCode.SourceMissing, result.Failure.Code);
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(AssetPath)));
        }

        [Test]
        public void ABrokenGat_FailsWithGatBadHeader()
        {
            File.WriteAllBytes(Path.Combine(_data, Map + ".gat"), new byte[] { 1, 2, 3 });

            var result = Context().MapData(RoPath.Parse(Map + ".gat"));

            Assert.AreEqual(RoIssueCode.GatBadHeader, result.Failure?.Code);
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(AssetPath)));
        }

        [Test]
        public void TheLedger_ListsTheAsset_AndTheGatAndGndAsSourceAndDependency()
        {
            Convert(image: true);

            var entry = RoLedgerOf().Find(RoPath.Parse(Map + ".gat"));

            Assert.IsNotNull(entry);
            CollectionAssert.AreEquivalent(new[] { AssetPath, ImagePath }, entry.Outputs);
            CollectionAssert.AreEqual(new[] { Map + ".gnd" }, entry.Dependencies.Select(d => d.Source));
        }

        Uninarok.RoImport.Editor.Ledger.RoLedger RoLedgerOf() => Context().Ledger(RoMapDataConverter.Category);

        [Test]
        public void AChangedGround_MakesTheMapDataStale()
        {
            Convert();
            File.WriteAllBytes(Path.Combine(_data, Map + ".gnd"), GatTestFiles.Gnd(4, 3, zoom: 5f));

            var result = Convert();

            Assert.AreEqual(RoStatus.Updated, result.Status);
            Assert.AreEqual(0.25f, result.Value.CellSize, 1e-6f, "zoom 5: a cell is a quarter unit");
        }

        // ---- optional image (FR-8) ----

        [Test]
        public void TheImage_IsWrittenOnlyWhenRequested_OnePixelPerCell_PointFiltered()
        {
            Convert();
            Assert.IsFalse(File.Exists(RoImportTestRoot.FullPath(ImagePath)));

            var result = Convert(image: true);

            Assert.AreNotEqual(RoStatus.Failed, result.Status, "an image request on a current map converts it again");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath);
            Assert.IsNotNull(texture);
            Assert.AreEqual((8, 6), (texture.width, texture.height));
            Assert.AreEqual(FilterMode.Point, texture.filterMode);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ImagePath);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        }

        [Test]
        public void TheImage_IsDeterministic_AndKeepsItsGuidOnAForcedConversion()
        {
            Convert(image: true);
            string guid = AssetDatabase.AssetPathToGUID(ImagePath);
            byte[] before = File.ReadAllBytes(RoImportTestRoot.FullPath(ImagePath));

            Convert(force: true, image: true);

            CollectionAssert.AreEqual(before, File.ReadAllBytes(RoImportTestRoot.FullPath(ImagePath)));
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(ImagePath));
        }

        // ---- the API of PLAN-0005 4.5 ----

        [Test]
        public void RoImportMapData_ConvertsByMapName()
        {
            var result = Uninarok.RoImport.Editor.Pipeline.RoImport.MapData(Map, new RoImportOptions
            {
                Source = new RoFolderSource(_data), Roots = _roots, LedgerFolder = _ledgerFolder, ShowProgress = false
            });

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.ToString());
            Assert.AreEqual(AssetPath, AssetDatabase.GetAssetPath(result.Value));
        }

        [Test]
        public void TheConverter_IsRegistered_ForGatFiles()
        {
            var converter = RoConverterRegistry.For(RoJobKind.MapData);

            Assert.IsInstanceOf<RoMapDataConverter>(converter);
            Assert.IsTrue(converter.Handles(RoPath.Parse("prontera.gat")));
            Assert.IsFalse(converter.Handles(RoPath.Parse("prontera.gnd")));
        }
    }
}
