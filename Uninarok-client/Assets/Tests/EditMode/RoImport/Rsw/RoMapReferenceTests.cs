using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Uninarok.Map;
using Uninarok.RoImport.Core.Formats;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Map;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using Uninarok.RoImport.Tests.Gnd;
using ImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Rsw
{
    // TASKS-0006 T7.1 (FR-1 to FR-12): the three reference maps convert in one batch into a throwaway root, with no
    // failure, one instance per object of the file in each group, every model converted (no placeholder), and a second
    // batch skips everything. Converts every model the maps use, so it is slow.
    [Category(GndTestData.Category)]
    public class RoMapReferenceTests
    {
        static readonly string[] Maps = { "izlude", "prontera", "prt_fild08" };

        RoOutputRoots _roots;
        string _ledgerFolder;

        [SetUp]
        public void SetUp()
        {
            foreach (string map in Maps) RswTestData.MapBytes(map);   // ignored when the data is absent
            _roots = RoImportTestRoot.Create();
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RoImportTestRoot.Delete();
            if (Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        RoImportOptions Options => new RoImportOptions { Roots = _roots, LedgerFolder = _ledgerFolder, ShowProgress = false };

        [Test]
        public void TheReferenceMaps_ConvertInOneBatch_CompleteAndThenSkipped()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = Maps.Select(m => new RoJob(RoJobKind.Map, RoPath.Parse(m + ".rsw"))).ToList();

            var report = ImportApi.Run(jobs, Options);
            TestContext.Out.WriteLine(report.Summarize());
            Assert.AreEqual(0, report.Failed, string.Join("\n", report.Failures.Select(f => f.ToString())));
            Assert.AreEqual(Maps.Length, report.Created);

            foreach (string map in Maps)
            {
                Assert.IsTrue(source.TryGetFile(RoPath.Parse(map + ".rsw"), out var file));
                Assert.IsTrue(RoRswReader.TryRead(RoPath.Parse(map + ".rsw"), file.ReadAllBytes(), out var rsw, out var reason), reason);

                var scene = EditorSceneManager.OpenScene(RoMapOutputPaths.For(_roots, RoPath.Parse(map + ".rsw")).Scene, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single();
                Assert.AreEqual(rsw.Models.Count, RoMapNames.FindGroup(root.transform, RoMapGroup.Models).childCount, map + " models");
                Assert.AreEqual(rsw.Lights.Count, RoMapNames.FindGroup(root.transform, RoMapGroup.Lights).childCount, map + " lights");
                Assert.AreEqual(rsw.Sounds.Count, RoMapNames.FindGroup(root.transform, RoMapGroup.Sounds).childCount, map + " sounds");
                Assert.AreEqual(rsw.Effects.Count, RoMapNames.FindGroup(root.transform, RoMapGroup.Effects).childCount, map + " effects");
                Assert.AreEqual(0, root.GetComponentsInChildren<MapMissingModelMarker>().Length, map + ": every distinct model converted");
                Assert.IsNotNull(root.GetComponent<MapRoot>().Data, map + " map data");
            }

            var second = ImportApi.Run(jobs, Options);
            TestContext.Out.WriteLine(second.Summarize());
            Assert.AreEqual(0, second.Created + second.Updated + second.Failed, "a second batch changes nothing");
            Assert.AreEqual(Maps.Length, second.Skipped);
        }
    }
}
