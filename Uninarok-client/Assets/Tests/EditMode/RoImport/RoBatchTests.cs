using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using RoImportApi = Uninarok.RoImport.Editor.Pipeline.RoImport;   // the class name equals our root namespace

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-10 (batch continues, failures listed once), FR-11 (no error for expected conditions), FR-5 (resume).
    // Plan 4.9, 4.10, D18. TASKS T6.5.
    public class RoBatchTests : RoRunFixture
    {
        static readonly Regex Summary = new Regex(@"^\[RoImport\] Created ");

        RoJob[] FixtureFolderPlusAMissingFile(RoImportOptions options)
        {
            var jobs = options.Source.Enumerate("texture/").Select(p => new RoJob(RoJobKind.Texture, p)).ToList();
            jobs.Add(Job("texture/nope.bmp"));
            return jobs.ToArray();
        }

        [Test]
        public void BatchWithBadFiles_FinishesTheRest_ListsEachFailureOnce_AndLogsNoError()
        {
            var options = Options();
            var jobs = FixtureFolderPlusAMissingFile(options);
            Assume.That(jobs.Length, Is.EqualTo(9));
            // In job order, which is canonical path order: corrupt, nope, unsupported.
            LogAssert.Expect(LogType.Warning, new Regex("CorruptFile texture/corrupt.bmp"));
            LogAssert.Expect(LogType.Warning, new Regex("TextureMissing texture/nope.bmp"));
            LogAssert.Expect(LogType.Warning, new Regex("UnsupportedTextureFormat texture/unsupported.dds"));
            LogAssert.Expect(LogType.Log, Summary);

            var report = RoImportApi.Run(jobs, options);

            Assert.AreEqual(6, report.Created, "every good texture converted");
            Assert.AreEqual(3, report.Failed);
            CollectionAssert.AreEquivalent(new[] { "texture/corrupt.bmp", "texture/nope.bmp", "texture/unsupported.dds" },
                report.Failures.Select(f => f.Item));
            string summary = report.Summarize();
            foreach (string bad in new[] { "texture/corrupt.bmp", "texture/nope.bmp", "texture/unsupported.dds" })
                Assert.AreEqual(1, Regex.Matches(summary, Regex.Escape(bad)).Count, $"{bad} listed once");
            LogAssert.NoUnexpectedReceived();      // no LogError: a clean-but-imperfect batch is not an error (FR-11)
        }

        sealed class ThrowingConverter : IRoConverter
        {
            readonly IRoConverter _real = new RoTextureConverter();
            public RoJobKind Kind => RoJobKind.Texture;

            public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
            {
                if (path.Canonical == "texture/key8.bmp") throw new InvalidOperationException("boom");
                return _real.Convert(ctx, path);
            }
        }

        [Test]
        public void ExceptionInAConverter_IsOneUnexpectedFailure_AndOneErrorLog_AndTheBatchContinues()
        {
            var options = Options();
            options.Converters = new IRoConverter[] { new ThrowingConverter() };
            LogAssert.Expect(LogType.Error, new Regex(@"^\[RoImport\] Unexpected error converting Texture texture/key8\.bmp: System\.InvalidOperationException: boom"));
            LogAssert.Expect(LogType.Log, Summary);

            var report = RoImportApi.Run(new[] { Job("texture/a.bmp"), Job("texture/key8.bmp"), Job("texture/rgba.png") }, options);

            Assert.AreEqual(2, report.Created, "the jobs after the exception still ran");
            Assert.AreEqual(1, report.Failed);
            Assert.AreEqual(RoIssueCode.Unexpected, report.Failures.Single().Code);
            StringAssert.Contains("boom", report.Failures.Single().Message);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CancelAfterTheFirstItem_SavesTheLedger_AndARerunConvertsTheRestAndSkipsTheFirst()
        {
            var options = Options();
            int started = 0;
            options.CancelBefore = _ => started++ >= 1;
            LogAssert.Expect(LogType.Log, Summary);

            var cancelled = RoImportApi.Run(GoodJobs(), options);

            Assert.AreEqual(1, cancelled.Created);
            var ledger = RoLedger.Load(RoTextureConverter.Category, options.LedgerFolder);
            Assert.IsTrue(File.Exists(ledger.FilePath), "the ledger is saved on cancel");
            Assert.AreEqual(1, ledger.Entries.Count());
            string first = ledger.Entries.Single().Source;

            LogAssert.Expect(LogType.Log, Summary);
            var rerun = RoImportApi.Run(GoodJobs(), Options());

            Assert.AreEqual(1, rerun.Skipped, "the finished item is skipped");
            Assert.AreEqual(Good.Length - 1, rerun.Created, "the rest is converted");
            Assert.AreEqual(Good.OrderBy(g => g, StringComparer.Ordinal).First(), first, "jobs run in canonical order");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Jobs_RunInDeterministicOrder_AndDuplicatesOnce()
        {
            var ordered = RoBatch.Order(new[] { Job("texture/b.bmp"), Job("texture/a.bmp"), Job(@"DATA\Texture\B.BMP") });

            CollectionAssert.AreEqual(new[] { "texture/a.bmp", "texture/b.bmp" }, ordered.Select(j => j.Path.Canonical));
        }
    }
}
