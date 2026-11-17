using System;
using System.Collections.Generic;
using System.Text;
using Uninarok.RoImport.Core.Paths;
using UnityEngine;

namespace Uninarok.RoImport.Core.Results
{
    /// <summary>
    /// Collects the issues and counts of one run (plan 4.9).
    /// Expected conditions are warnings, logged once per (code, item) for the whole run (SPEC-0002 FR-10, FR-11).
    /// This class never logs an error: only the batch logs an <see cref="RoIssueCode.Unexpected"/> exception.
    /// </summary>
    public sealed class RoReport
    {
        const string LogPrefix = "[RoImport] ";

        readonly List<RoIssue> _issues = new List<RoIssue>();
        readonly HashSet<(RoIssueCode, string)> _warned = new HashSet<(RoIssueCode, string)>();
        readonly List<RoIssue> _failures = new List<RoIssue>();
        readonly HashSet<string> _failedItems = new HashSet<string>(StringComparer.Ordinal);

        public int Created, Updated, Skipped, Failed;

        /// <summary>Every distinct issue of the run, warnings and failures, in the order first raised.</summary>
        public IReadOnlyList<RoIssue> Issues => _issues;

        /// <summary>Each failed item once, with the reason of its first failure.</summary>
        public IReadOnlyList<RoIssue> Failures => _failures;

        /// <summary>Records an expected condition. Repeats of the same (code, item) are ignored.</summary>
        public void Warn(RoIssueCode code, string item, string referrer, string message)
        {
            if (!_warned.Add((code, item ?? string.Empty))) return;
            var issue = new RoIssue(code, item, referrer, message);
            _issues.Add(issue);
            Debug.LogWarning(LogPrefix + issue);
        }

        /// <summary>
        /// Records that an item failed. The item is counted and listed once, however often it fails.
        /// Logging is the caller's: the warning was already raised with <see cref="Warn"/>, or the batch logs the exception.
        /// </summary>
        public void Fail(RoPath item, RoIssue issue) => Fail(item.Canonical, issue);

        /// <inheritdoc cref="Fail(RoPath, RoIssue)"/>
        public void Fail(string item, RoIssue issue)
        {
            if (issue == null) throw new ArgumentNullException(nameof(issue));
            if (!_failedItems.Add(item ?? string.Empty)) return;
            Failed++;
            _failures.Add(issue);
            if (!_issues.Contains(issue)) _issues.Add(issue);
        }

        /// <summary>Counts, then each failed item once with its reason.</summary>
        public string Summarize()
        {
            var sb = new StringBuilder();
            sb.Append(LogPrefix)
              .Append("Created ").Append(Created)
              .Append(", Updated ").Append(Updated)
              .Append(", Skipped ").Append(Skipped)
              .Append(", Failed ").Append(Failed)
              .Append(", Warnings ").Append(_warned.Count);
            foreach (var failure in _failures)
                sb.Append("\n  failed: ").Append(failure);
            return sb.ToString();
        }
    }
}
