using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Gat
{
    // TASKS-0005 T4.3 (FR-6, FR-8): the jobs of "Convert All Map Data" are every .gat of the data root; a batch converts
    // them with and without the image, a cancel stops it, and a rerun skips everything.
    public class RoMapDataBatchTests
    {
        string _data;
        string _ledgerFolder;
        RoOutputRoots _roots;

        [SetUp]
        public void SetUp()
        {
            _data = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"), "data");
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
            _roots = RoImportTestRoot.Create();
            GatTestFiles.WriteMap(_data, "alpha", GatTestMaps.Asymmetric8x6(), 4, 3);
            GatTestFiles.WriteMap(_data, "beta", GatTestMaps.Asymmetric6x8(), 3, 4);
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            foreach (string folder in new[] { Path.GetDirectoryName(_data), _ledgerFolder })
                if (folder != null && Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        RoImportOptions Options(bool image = false, Func<RoJob, bool> cancel = null) => new RoImportOptions
        {
            Source = new RoFolderSource(_data), Roots = _roots, LedgerFolder = _ledgerFolder, ShowProgress = false,
            GatImage = image, CancelBefore = cancel
        };

        bool Exists(string projectPath) => File.Exists(RoImportTestRoot.FullPath(projectPath));
        string Asset(string map) => _roots.Data + "/map/" + map + ".asset";
        string Image(string map) => _roots.Art + "/map/" + map + "/" + map + "_cells.png";

        [Test]
        public void TheMenuJobs_AreEveryGatOfTheDataRoot()
        {
            var jobs = RoImportMenu.MapDataJobs(new RoFolderSource(_data));

            CollectionAssert.AreEqual(new[] { "alpha.gat", "beta.gat" }, jobs.Select(j => j.Path.Canonical));
            Assert.IsTrue(jobs.All(j => j.Kind == RoJobKind.MapData));
        }

        [Test]
        public void ABatch_ConvertsEveryMap_WithoutImages_ThenSkipsEverything()
        {
            var jobs = RoImportMenu.MapDataJobs(new RoFolderSource(_data));

            var first = RoImportApi.Run(jobs, Options());
            var second = RoImportApi.Run(jobs, Options());

            Assert.AreEqual(2, first.Created, first.Summarize());
            Assert.IsTrue(Exists(Asset("alpha")) && Exists(Asset("beta")));
            Assert.IsFalse(Exists(Image("alpha")) || Exists(Image("beta")));
            Assert.AreEqual(2, second.Skipped, second.Summarize());
        }

        [Test]
        public void ABatch_WithImages_WritesOnePerMap()
        {
            var report = RoImportApi.Run(RoImportMenu.MapDataJobs(new RoFolderSource(_data)), Options(image: true));

            Assert.AreEqual(2, report.Created, report.Summarize());
            Assert.IsTrue(Exists(Image("alpha")) && Exists(Image("beta")));
        }

        [Test]
        public void ACancel_StopsTheBatch_BeforeTheNextMap()
        {
            var report = RoImportApi.Run(RoImportMenu.MapDataJobs(new RoFolderSource(_data)),
                Options(cancel: job => job.Path.Canonical == "beta.gat"));

            Assert.AreEqual(1, report.Created);
            Assert.IsTrue(Exists(Asset("alpha")));
            Assert.IsFalse(Exists(Asset("beta")));
        }
    }
}
