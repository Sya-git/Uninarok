using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsw;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Gat;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Tests.Gat;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T2.1 / T2.2 (FR-7): the map data asset holds the RSW's map-wide values, converted to Unity units; a map
    // without an RSW converts without settings (SPEC-0005 stands alone).
    [Ignore("pending T2.2")]
    public class RoMapSettingsTests
    {
        const string Map = "sample";
        const float Eps = 1e-5f;

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
            RoImportTestRoot.Delete();
            foreach (string folder in new[] { Path.GetDirectoryName(_data), _ledgerFolder })
                if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        RoResult<MapData> Convert()
        {
            var ctx = new RoImportContext(new RoFolderSource(_data), _roots, _ledgerFolder);
            var result = ctx.MapData(RoPath.Parse(Map + ".gat"));
            ctx.SaveLedgers();
            AssetDatabase.SaveAssets();
            return result;
        }

        static void AssertSettings(MapSettings s, RoRsw rsw, int width, int height, float zoom)
        {
            Assert.AreEqual((width, height), (s.WidthCubes, s.HeightCubes), "size in cubes");
            Assert.AreEqual(zoom, s.Zoom, Eps);
            Assert.AreEqual(-rsw.Water.Level * 0.1f, s.WaterLevel, Eps, "Ragnarok Y points down; 10 units per Unity unit");
            Assert.AreEqual(rsw.Water.Type, s.WaterType);
            Assert.AreEqual(rsw.Water.WaveHeight * 0.1f, s.WaveHeight, Eps);
            Assert.AreEqual(rsw.Water.WaveSpeed, s.WaveSpeed, Eps);
            Assert.AreEqual(rsw.Water.WavePitch, s.WavePitch, Eps);
            Assert.AreEqual(rsw.Light.Ambient, s.Ambient);
            Assert.AreEqual(rsw.Light.Diffuse, s.SunColor);
            Assert.AreEqual((rsw.Light.Longitude, rsw.Light.Latitude), (s.Longitude, s.Latitude));
            Assert.AreEqual(rsw.Light.ShadowOpacity, s.ShadowOpacity, Eps);
            Assert.AreEqual("2.1", s.RswVersion);
        }

        [Test]
        public void Build_ConvertsTheRswValues()
        {
            var rsw = RswTestMaps.Full().Rsw;
            var settings = RoMapSettingsBuilder.Build(rsw, 10, 12, 5f, "1.7", "1.2");

            AssertSettings(settings, rsw, 10, 12, 5f);
            Assert.AreEqual(("1.7", "1.2"), (settings.GndVersion, settings.GatVersion));
        }

        [Test]
        public void AMapWithAnRsw_HasSettingsEqualToTheRsw()
        {
            var rsw = RswTestMaps.Full().Rsw;
            RswTestFiles.Write(_data, Map, rsw);

            var result = Convert();

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.Message);
            Assert.IsTrue(result.Value.HasSettings);
            AssertSettings(result.Value.Settings, rsw, 4, 3, 10f);
            Assert.AreEqual(("1.7", "1.2"), (result.Value.Settings.GndVersion, result.Value.Settings.GatVersion));
        }

        [Test]
        public void AMapWithoutAnRsw_ConvertsWithoutSettings()
        {
            var result = Convert();

            Assert.AreEqual(RoStatus.Created, result.Status, result.Failure?.Message);
            Assert.IsFalse(result.Value.HasSettings);
        }

        [Test]
        public void ChangingTheRsw_ConvertsTheMapDataAgain()
        {
            var rsw = RswTestMaps.Full().Rsw;
            RswTestFiles.Write(_data, Map, rsw);
            Assert.AreEqual(RoStatus.Created, Convert().Status);
            Assert.AreEqual(RoStatus.Skipped, Convert().Status, "unchanged sources are skipped");

            rsw.Water = new RoRswWater(12f, 0, 1f, 2f, 50f);
            RswTestFiles.Write(_data, Map, rsw);
            var result = Convert();

            Assert.AreEqual(RoStatus.Updated, result.Status, "the RSW is a dependency of the map data");
            Assert.AreEqual(-1.2f, result.Value.Settings.WaterLevel, Eps);
        }

        [Test]
        public void AnUnreadableRsw_IsReported_AndTheMapDataConvertsWithoutSettings()
        {
            File.WriteAllBytes(Path.Combine(_data, Map + ".rsw"), new byte[] { 1, 2, 3 });
            var ctx = new RoImportContext(new RoFolderSource(_data), _roots, _ledgerFolder);

            var result = ctx.MapData(RoPath.Parse(Map + ".gat"));

            Assert.AreEqual(RoStatus.Created, result.Status);
            Assert.IsFalse(result.Value.HasSettings);
            Assert.IsTrue(ctx.Report.Issues.Any(i => i.Code == RoIssueCode.CorruptFile && i.Item.EndsWith(".rsw")),
                "one warning names the RSW");
        }
    }
}
