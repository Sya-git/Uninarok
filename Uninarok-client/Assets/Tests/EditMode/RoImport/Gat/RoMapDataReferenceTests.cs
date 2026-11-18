using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gat;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using Uninarok.RoImport.Tests.Gnd;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T7.1 (FR-1, FR-2, FR-3, FR-5): the reference maps converted from the real data. Outputs go under
    // Assets/__RoImportTests; the ledger lives in a temporary folder.
    [Category(GndTestData.Category)]
    public class RoMapDataReferenceTests
    {
        static readonly string[] Maps = { "prontera", "izlude", "prt_fild08" };

        /// <summary>FR-3, plan 4.4 (D11): no walkable centre more than this below the ground, in Unity units.</summary>
        const float GroundTolerance = 0.3f;
        /// <summary>FR-3, plan 4.4: the share of walkable cells within <see cref="OnGroundTolerance"/> on the maps without large model floors.</summary>
        const double OnGroundShare = 0.98;
        const float OnGroundTolerance = 0.05f;

        string _ledgerFolder;
        RoOutputRoots _roots;
        RoFolderSource _source;

        [SetUp]
        public void SetUp()
        {
            string root = RoSourceLocation.Resolve();
            if (!Directory.Exists(root)) Assert.Ignore($"source data folder not found: {root}");
            foreach (string map in Maps)
                if (!File.Exists(Path.Combine(root, map + ".gat")) || !File.Exists(Path.Combine(root, map + ".gnd")))
                    Assert.Ignore($"reference map not found: {map}");
            _source = new RoFolderSource(root);
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _roots = RoImportTestRoot.Create();
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (_ledgerFolder != null && Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        RoImportOptions Options() => new RoImportOptions { Source = _source, Roots = _roots, LedgerFolder = _ledgerFolder, ShowProgress = false };

        MapData Convert(string map)
        {
            var result = RoImportApi.MapData(map, Options());
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            return result.Value;
        }

        static RoGat ReadGat(string map)
        {
            Assert.IsTrue(RoGatReader.TryRead(RoPath.Parse(map + ".gat"), GatTestData.MapBytes(map), out var gat, out var reason), reason);
            return gat;
        }

        static RoGnd ReadGnd(string map)
        {
            Assert.IsTrue(RoGndReader.TryRead(RoPath.Parse(map + ".gnd"), GndTestData.MapBytes(map), null, out var gnd, out var reason), reason);
            return gnd;
        }

        /// <summary>The four corners, the centre and 100 cells of a fixed seed (plan 4.4).</summary>
        static IEnumerable<(int x, int z)> Samples(int w, int h)
        {
            yield return (0, 0);
            yield return (w - 1, 0);
            yield return (0, h - 1);
            yield return (w - 1, h - 1);
            yield return (w / 2, h / 2);
            var random = new System.Random(5005);
            for (int i = 0; i < 100; i++) yield return (random.Next(w), random.Next(h));
        }

        [TestCaseSource(nameof(Maps))]
        public void SampleQueries_EqualTheSourceCells(string map)
        {
            var data = Convert(map);
            var gat = ReadGat(map);

            Assert.AreEqual((gat.Width, gat.Height), (data.CellsX, data.CellsZ));
            foreach (var (x, z) in Samples(gat.Width, gat.Height))
            {
                int i = gat.IndexOf(x, z);
                var type = RoGatRules.TypeOf(gat.Types[i]);
                string at = $"{map} cell ({x}, {z})";
                Assert.AreEqual(type, data.TypeAt(x, z), at);
                Assert.AreEqual(RoGatRules.FlagsOf(type), data.FlagsAt(x, z), at);
                var h = data.CornerHeights(x, z);
                Assert.AreEqual(-gat.Heights[i * 4 + 0] * 0.1f, h.x, 1e-4f, at + " H00");
                Assert.AreEqual(-gat.Heights[i * 4 + 1] * 0.1f, h.y, 1e-4f, at + " H10");
                Assert.AreEqual(-gat.Heights[i * 4 + 2] * 0.1f, h.z, 1e-4f, at + " H01");
                Assert.AreEqual(-gat.Heights[i * 4 + 3] * 0.1f, h.w, 1e-4f, at + " H11");

                // by world position: the cell centre finds the same cell and its walkability
                var centre = data.CellCenter(x, z);
                Assert.IsTrue(data.TryGetCell(centre, out int cx, out int cz), at);
                Assert.AreEqual((x, z), (cx, cz), at);
                Assert.AreEqual((RoGatRules.FlagsOf(type) & MapCellFlags.Walkable) != 0, data.IsWalkable(centre), at);
            }
            Assert.IsFalse(data.IsWalkable(new Vector3(-1f, 0f, -1f)), "outside the map");
        }

        [TestCaseSource(nameof(Maps))]
        public void WalkableCells_LieOnTheGround_OrAboveIt(string map)
        {
            var data = Convert(map);
            var gnd = ReadGnd(map);
            Assert.IsTrue(RoGndRules.TryGet(gnd.VersionMajor, gnd.VersionMinor, out var rules));
            float halfCube = gnd.Zoom / 2f;

            int walkable = 0, onGround = 0, above = 0;
            float worstBelow = 0f;
            for (int z = 0; z < data.CellsZ; z++)
            for (int x = 0; x < data.CellsX; x++)
            {
                if (!data.IsWalkable(x, z)) continue;
                var ro = new Vector3((x + 0.5f) * halfCube, 0f, (z + 0.5f) * halfCube * RoMapSpace.RowDirection);
                if (!RoGroundSampler.HeightAt(gnd, rules, ro, out float groundRo)) continue;   // over a gap
                walkable++;
                float d = data.CellCenter(x, z).y - -groundRo * 0.1f;   // Unity units, + is above the ground
                if (Mathf.Abs(d) <= OnGroundTolerance) onGround++;
                else if (d > GroundTolerance) above++;
                worstBelow = Mathf.Max(worstBelow, -d);
            }

            double share = (double)onGround / walkable;
            TestContext.WriteLine($"{map}: {walkable} walkable cells over the ground, {share:P2} within {OnGroundTolerance}, " +
                                  $"{above} above by more than {GroundTolerance} (model surfaces), worst below {worstBelow:F3}");
            Assert.LessOrEqual(worstBelow, GroundTolerance, "no walkable cell sinks below the ground (FR-3 a)");
            if (map != "izlude") Assert.GreaterOrEqual(share, OnGroundShare, "the grid lies on the ground (FR-3 b)");
        }

        [Test]
        public void EveryGatOfTheDataRoot_Converts_AndASecondBatchSkipsEverything()
        {
            var jobs = RoImportMenu.MapDataJobs(_source);
            Assert.IsNotEmpty(jobs);

            var first = RoImportApi.Run(jobs, Options());
            var second = RoImportApi.Run(jobs, Options());

            CollectionAssert.IsEmpty(first.Failures.Select(f => f.Item + ": " + f.Message), "failures by name");
            Assert.AreEqual(jobs.Count, first.Created + first.Updated);
            Assert.AreEqual(jobs.Count, second.Skipped, second.Summarize());
        }

        [Test]
        public void TheLargestMap_Holds19DataBytesPerCell()
        {
            var data = Convert("prt_fild08");

            double perCell = (double)data.CellDataBytes / (data.CellsX * data.CellsZ);

            Assert.AreEqual(19.0, perCell, "flat arrays, no compression (D14)");
        }
    }
}
