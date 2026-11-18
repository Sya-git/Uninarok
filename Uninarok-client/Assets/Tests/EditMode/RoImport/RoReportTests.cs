using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Tests
{
    // SPEC-0002 FR-10 (failures listed once), FR-11 (expected conditions are not errors). Plan 4.9, TASKS T1.1.
    public class RoReportTests
    {
        [Test]
        public void SameWarningRaisedThreeTimes_IsListedAndLoggedOnce()
        {
            var report = new RoReport();
            LogAssert.Expect(LogType.Warning, "[RoImport] TextureMissing texture/a.bmp (referenced by model/b.rsm): not found");

            for (int i = 0; i < 3; i++)
                report.Warn(RoIssueCode.TextureMissing, "texture/a.bmp", "model/b.rsm", "not found");

            LogAssert.NoUnexpectedReceived();       // a second identical warning would be unexpected
            Assert.AreEqual(1, report.Issues.Count);
        }

        [Test]
        public void SameItemWithDifferentCodes_AreDistinctWarnings()
        {
            var report = new RoReport();
            LogAssert.Expect(LogType.Warning, "[RoImport] TextureMissing texture/a.bmp: x");
            LogAssert.Expect(LogType.Warning, "[RoImport] CorruptFile texture/a.bmp: y");

            report.Warn(RoIssueCode.TextureMissing, "texture/a.bmp", null, "x");
            report.Warn(RoIssueCode.CorruptFile, "texture/a.bmp", null, "y");

            Assert.AreEqual(2, report.Issues.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Summarize_ListsEachFailedItemOnceWithItsReason()
        {
            var report = new RoReport { Created = 3, Skipped = 2 };
            var corrupt = new RoIssue(RoIssueCode.CorruptFile, "texture/bad.bmp", null, "Unable to parse");
            var missing = new RoIssue(RoIssueCode.SourceMissing, "model/gone.rsm", "prontera.rsw", "no such file");

            report.Fail("texture/bad.bmp", corrupt);
            report.Fail("texture/bad.bmp", corrupt);
            report.Fail("model/gone.rsm", missing);
            report.Fail("texture/bad.bmp", new RoIssue(RoIssueCode.Unexpected, "texture/bad.bmp", null, "second reason"));

            string summary = report.Summarize();
            StringAssert.StartsWith("[RoImport] Created 3, Updated 0, Skipped 2, Failed 2", summary);
            Assert.AreEqual(1, Occurrences(summary, "texture/bad.bmp"));
            Assert.AreEqual(1, Occurrences(summary, "model/gone.rsm"));
            StringAssert.Contains("Unable to parse", summary);
            StringAssert.Contains("referenced by prontera.rsw", summary);
            StringAssert.DoesNotContain("second reason", summary);
            Assert.AreEqual(2, report.Failed);
            Assert.AreEqual(2, report.Failures.Count);
        }

        [Test]
        public void WarningsAndFailures_NeverProduceAnErrorLog()
        {
            var report = new RoReport();
            LogAssert.Expect(LogType.Warning, "[RoImport] CorruptFile texture/bad.bmp: Unable to parse");

            report.Warn(RoIssueCode.CorruptFile, "texture/bad.bmp", null, "Unable to parse");
            report.Fail("texture/bad.bmp", report.Issues[0]);
            report.Fail("x", new RoIssue(RoIssueCode.Unexpected, "x", null, "boom"));
            report.Summarize();

            Assert.AreEqual(2, report.Issues.Count, "a failure already warned about is not listed twice");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void FailByRoPath_IsTheSameItemAsItsCanonicalString()
        {
            var report = new RoReport();
            var issue = new RoIssue(RoIssueCode.CorruptFile, "texture/a.bmp", null, "bad");

            report.Fail(RoPath.Parse(@"DATA\Texture\A.BMP"), issue);
            report.Fail("texture/a.bmp", issue);

            Assert.AreEqual(1, report.Failed);
        }

        static int Occurrences(string text, string value) =>
            Enumerable.Range(0, text.Length - value.Length + 1).Count(i => string.CompareOrdinal(text, i, value, 0, value.Length) == 0);
    }
}
