using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uninarok.RoImport.Core.Output;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Rsm;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;

namespace Uninarok.RoImport.Tests.Rsm
{
    // TASKS-0003 T7.1, RsmReferenceTests part 3 (FR-2, FR-8): the real models convert through RoImport.Model and in one
    // batch over the whole model folder. Separate class because it needs a throwaway output root and ledger for every
    // test. Writes under Assets/__RoImportTests; unexpected LogError fails a test (Unity Test Framework).
    [Category(RsmTestData.Category)]
    public class RsmReferenceConversionTests
    {
        RoOutputRoots _roots;
        string _ledgerFolder;

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists(Path.Combine(RoSourceLocation.Resolve(), "model"))) Assert.Ignore("source data folder not found");
            _roots = RoImportTestRoot.Create();
            _ledgerFolder = Path.Combine(Path.GetTempPath(), "RoImportTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            RoImportTestRoot.Delete();
            if (_ledgerFolder != null && Directory.Exists(_ledgerFolder)) Directory.Delete(_ledgerFolder, recursive: true);
        }

        RoImportOptions Options => new RoImportOptions { Roots = _roots, LedgerFolder = _ledgerFolder, ShowProgress = false };

        static string[] AllReferenceModels() => RsmTestData.ReferenceModels().ToArray();

        [Test]
        public void EveryReferenceModel_ConvertsThroughRoImportModel()
        {
            foreach (string model in AllReferenceModels())
            {
                var result = RoImportApi.Model("model/" + model, Options);
                Assert.AreNotEqual(RoStatus.Failed, result.Status, $"{model}: {result.Failure}");
                Assert.IsNotNull(result.Value, model);
                Assert.AreEqual(Path.GetFileNameWithoutExtension(model), result.Value.name);
            }
        }

        // The four reported models and the control group (reference-models.md §1, §2): the converted prefab's root node
        // carries the recorded rest transform of PLAN-0003 4.4 ("R1 reference values"), converted by RoSpace.
        [TestCase("나무잡초꽃/버섯02.rsm", 0.046991f, 1.665913f, -0.145184f)]
        [TestCase("나무잡초꽃/버섯01.rsm", 0.046991f, 1.665913f, -0.145184f)]
        [TestCase("나무잡초꽃/넝쿨01.rsm", 0.226999f, -40.2408f, -0.000001f)]
        [TestCase("나무잡초꽃/덤불02.rsm", -8.305627f, 17.98348f, -3.509289f)]
        [TestCase("나무잡초꽃/덤불01.rsm", -8.305627f, 19.50137f, -1.240438f)]
        [TestCase("나무잡초꽃/야자수01.rsm", 4.419601f, 0f, -8.387604f)]
        [TestCase("프론테라/성_기둥.rsm", 0.155977f, -193.0966f, 3.696754f)]
        [TestCase("프론테라/휘장.rsm", 2.689514f, -7.367931f, -0.000003f)]
        [TestCase("외부소품/이정표01.rsm", -0.183999f, -15.70174f, 6.458787f)]
        public void ReportedAndControlModels_HaveTheRecordedRootTransform(string model, float x, float y, float z)
        {
            var result = RoImportApi.Model("model/" + model, Options);
            Assert.AreNotEqual(RoStatus.Failed, result.Status, result.Failure?.ToString());
            var prefab = result.Value;
            Assert.AreEqual(Vector3.zero, prefab.transform.localPosition, "the prefab root has no transform of its own");
            Assert.AreEqual(1, prefab.transform.childCount);
            var expected = Core.Space.RoSpace.Point(new Vector3(x, y, z));
            Assert.That((prefab.transform.GetChild(0).localPosition - expected).magnitude, Is.LessThan(1e-4f),
                $"expected {expected:F5}, got {prefab.transform.GetChild(0).localPosition:F5}");
        }

        [Test]
        public void BatchOverTheWholeModelFolder_ConvertsEveryFileOrListsEachFailureOnce()
        {
            var source = new RoFolderSource(RoSourceLocation.Resolve());
            var jobs = RoImportJobs.Expand("model/", RoJobKind.Model, source).Jobs;
            Assert.Greater(jobs.Count, 0);

            var report = RoImportApi.Run(jobs, Options);
            TestContext.Out.WriteLine(report.Summarize());

            var failed = report.Failures.Select(f => f.Item).ToList();
            CollectionAssert.AllItemsAreUnique(failed, "each failure listed once");
            Assert.AreEqual(jobs.Count, report.Created + report.Updated + report.Skipped + report.Failed, "every file accounted for");

            // Any version other than 1.4 is converted or reported once as unsupported (T0.3: none in the working data).
            int other = 0;
            foreach (var job in jobs)
            {
                Assert.IsTrue(source.TryGetFile(job.Path, out var file), job.Path.Canonical);
                var bytes = file.ReadAllBytes();
                if (bytes.Length > 5 && bytes[4] == 1 && bytes[5] == 4) continue;
                other++;
                string item = job.Path.Canonical;
                if (failed.Contains(item))
                    Assert.AreEqual(1, report.Issues.Count(i => i.Item == item && i.Code == RoIssueCode.UnsupportedRsmVersion), item);
            }
            TestContext.Out.WriteLine($"{jobs.Count} models, {other} not version 1.4");
            CollectionAssert.IsEmpty(failed, "the working data converts completely:\n" + report.Summarize());
        }
    }
}
