using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Gnd;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Gnd;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using Uninarok.RoImport.Editor.Writing;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Gnd
{
    // TASKS-0004 T7.1 (FR-1 to FR-6): the reference maps of reference-models.md §4 convert from the owner's data, with
    // chunk and triangle counts equal to an oracle computed from the cube table GRF reads; the largest map converts in a
    // batch; a second batch skips everything; every .gnd in the data folder reads. Outputs go to Assets/__RoImportTests.
    [Category(GndTestData.Category)]
    public class RoGndReferenceTests
    {
        static readonly string[] ReferenceMaps = { "prontera", "izlude", "prt_fild08" };

        RoOutputRoots _roots;
        string _ledgerFolder;

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(Root)) Assert.Ignore($"source data folder not found: {Root}");
            _roots = RoImportTestRoot.Create();
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_roots != null) RoImportTestRoot.Delete();
            if (_ledgerFolder != null && Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        static string Root => RoSourceLocation.Resolve();

        RoImportOptions Options() => new RoImportOptions
        {
            Source = new RoFolderSource(Root), Roots = _roots, LedgerFolder = _ledgerFolder, ShowProgress = false
        };

        static RoGnd Read(string map)
        {
            byte[] bytes = GndTestData.MapBytes(map);
            Assert.IsTrue(RoGndReader.TryRead(RoPath.Parse(map + ".gnd"), bytes, null, out var gnd, out var reason), $"{map}: {reason}");
            return gnd;
        }

        [TestCase("prontera")]
        [TestCase("izlude")]
        [TestCase("prt_fild08")]
        public void ReferenceMap_Converts_WithTheOracleChunkAndTriangleCounts(string map)
        {
            var gnd = Read(map);
            var (chunks, triangles) = Oracle(gnd);

            var options = Options();
            var result = RoImportApi.Ground(map, options);
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            Assert.IsNotNull(result.Value);

            var paths = RoGndOutputPaths.For(_roots, RoPath.Parse(map + ".gnd"));
            var meshes = Directory.GetFiles(RoImportTestRoot.FullPath(paths.Folder), "*.asset")
                .Select(f => AssetDatabase.LoadAssetAtPath<Mesh>(paths.Folder + "/" + Path.GetFileName(f))).ToList();
            Assert.AreEqual(chunks, meshes.Count, "chunks with at least one face");
            Assert.AreEqual(chunks, result.Value.transform.childCount, "one prefab child per chunk");
            int built = meshes.Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(s => (int)m.GetIndexCount(s) / 3));
            Assert.AreEqual(triangles, built, "triangles");
        }

        [Test]
        public void ReferenceMaps_IncludingTheLargest_ConvertInABatch_AndASecondBatchSkipsEverything()
        {
            var jobs = ReferenceMaps.Select(m => new RoJob(RoJobKind.Ground, RoPath.Parse(m + ".gnd"))).ToList();
            int largest = ReferenceMaps.Max(m => { var g = Read(m); return g.Width * g.Height; });
            Assert.AreEqual(200 * 200, largest, "prt_fild08 is the largest map of the data (T0.1)");

            var first = RoImportApi.Run(jobs, Options());
            Assert.AreEqual(0, first.Failed, first.Summarize());
            foreach (string map in ReferenceMaps)
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(RoGndOutputPaths.For(_roots, RoPath.Parse(map + ".gnd")).Prefab), map);

            var second = RoImportApi.Run(jobs, Options());
            Assert.AreEqual(0, second.Failed, second.Summarize());
            Assert.AreEqual(0, second.Created + second.Updated, second.Summarize());
            Assert.AreEqual(jobs.Count, second.Skipped, second.Summarize());
        }

        [Test]
        public void EveryGndInTheDataFolder_Reads()
        {
            var jobs = RoImportMenu.GroundJobs(new RoFolderSource(Root));
            Assert.IsNotEmpty(jobs);
            var failures = new List<string>();
            foreach (var job in jobs)
            {
                var file = Path.Combine(Root, job.Path.Canonical);
                if (!RoGndReader.TryRead(job.Path, File.ReadAllBytes(file), null, out _, out var reason))
                    failures.Add($"{job.Path.Canonical}: {reason}");
            }
            Assert.IsEmpty(failures, $"{failures.Count} of {jobs.Count} grounds do not read");
        }

        /// <summary>
        /// The chunks and triangles expected from the cube table alone (written apart from the builder, PLAN-0004 4.3, 4.4):
        /// 2 triangles per top, 2 per wall that differs at both ends, 1 per wall that differs at one end; a chunk exists
        /// when one of its cubes has a face. Tiles whose texture index is outside the table draw nothing.
        /// </summary>
        static (int chunks, int triangles) Oracle(RoGnd gnd)
        {
            const float eps = RoGndRules.Epsilon;
            bool Drawn(int tile) => tile >= 0 && tile < gnd.Tiles.Count && gnd.Tiles[tile].Texture >= 0 && gnd.Tiles[tile].Texture < gnd.Textures.Count;
            int Ends(float a, float b, float c, float d)
            {
                bool first = Math.Abs(a - b) > eps, second = Math.Abs(c - d) > eps;
                if (!first && !second) return 0;
                // within Epsilon at one end still draws one triangle unless the two corners are bit-identical
                int n = 0;
                if (a != b) n++;
                if (c != d) n++;
                return n;
            }

            int triangles = 0;
            var chunks = new HashSet<(int, int)>();
            for (int row = 0; row < gnd.Height; row++)
            for (int column = 0; column < gnd.Width; column++)
            {
                var cube = gnd.Cubes[row * gnd.Width + column];
                int before = triangles;
                if (Drawn(cube.TileUp)) triangles += 2;
                if (Drawn(cube.TileFront) && row + 1 < gnd.Height)
                {
                    var next = gnd.Cubes[(row + 1) * gnd.Width + column];
                    triangles += Ends(cube.H01, next.H00, cube.H11, next.H10);
                }
                if (Drawn(cube.TileSide) && column + 1 < gnd.Width)
                {
                    var next = gnd.Cubes[row * gnd.Width + column + 1];
                    triangles += Ends(cube.H10, next.H00, cube.H11, next.H01);
                }
                if (triangles > before) chunks.Add((column / 16, row / 16));
            }
            return (chunks.Count, triangles);
        }
    }
}
