using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Editor.Pipeline
{
    /// <summary>
    /// A batch of conversions (plan 4.10, D18; SPEC-0002 FR-5, FR-10, FR-11). Deterministic order; assembly reload locked;
    /// cancelable progress; every <see cref="ChunkSize"/> jobs the assets, ledgers and hash cache are saved and unused
    /// assets unloaded, so a cancelled or crashed run resumes where it stopped. An expected problem is a warning and a
    /// failed item; an exception escaping a converter is a bug, logged once as an error, and the batch continues.
    /// </summary>
    public static class RoBatch
    {
        public const int ChunkSize = 200;

        public static RoReport Run(IEnumerable<RoJob> jobs, RoImportOptions options = null) => Run(jobs, options, null);

        /// <summary>As <see cref="Run(IEnumerable{RoJob}, RoImportOptions)"/>, adding to <paramref name="into"/> (several batches, one report: PLAN-0008 4.7).</summary>
        internal static RoReport Run(IEnumerable<RoJob> jobs, RoImportOptions options, RoReport into)
        {
            options = options ?? new RoImportOptions();
            var ordered = Order(jobs);
            var converters = options.Converters ?? RoConverterRegistry.All;

            using (var session = new RoImport.Session(options, into))
            {
                var ctx = session.Context;
                var report = ctx.Report;
                EditorApplication.LockReloadAssemblies();
                try
                {
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var job = ordered[i];
                        if (options.CancelBefore != null && options.CancelBefore(job)) break;
                        if (options.ShowProgress && EditorUtility.DisplayCancelableProgressBar(
                                "Ragnarok import", $"{i + 1} / {ordered.Count}  {job.Path}", (float)i / ordered.Count))
                            break;

                        RunOne(ctx, converters, job);

                        if ((i + 1) % ChunkSize == 0)
                        {
                            session.Save();
                            EditorUtility.UnloadUnusedAssetsImmediate();
                        }
                    }
                }
                finally
                {
                    if (options.ShowProgress) EditorUtility.ClearProgressBar();
                    EditorApplication.UnlockReloadAssemblies();
                }

                Debug.Log(report.Summarize());
                return report;
            }
        }

        static void RunOne(RoImportContext ctx, IReadOnlyList<IRoConverter> converters, RoJob job)
        {
            var report = ctx.Report;
            try
            {
                var converter = Find(converters, job.Kind);
                var result = converter.Convert(ctx, job.Path);
                switch (result.Status)
                {
                    case RoStatus.Created: report.Created++; break;
                    case RoStatus.Updated: report.Updated++; break;
                    case RoStatus.Skipped: report.Skipped++; break;
                    case RoStatus.Failed:
                        report.Fail(job.Path, result.Failure ?? new RoIssue(RoIssueCode.Unexpected, job.Path.Canonical, null, "failed without a reason"));
                        break;
                }
            }
            catch (Exception e)
            {
                // A bug, not an expected condition (plan 4.9): one error with the stack, one failed item, and on to the next.
                Debug.LogError($"[RoImport] Unexpected error converting {job}: {e}");
                report.Fail(job.Path, new RoIssue(RoIssueCode.Unexpected, job.Path.Canonical, null, e.GetType().Name + ": " + e.Message));
            }
        }

        static IRoConverter Find(IReadOnlyList<IRoConverter> converters, RoJobKind kind)
        {
            foreach (var c in converters)
                if (c.Kind == kind) return c;
            throw new InvalidOperationException($"No converter is registered for {kind}.");
        }

        /// <summary>Distinct jobs sorted by (kind, canonical path).</summary>
        internal static List<RoJob> Order(IEnumerable<RoJob> jobs)
        {
            var list = new List<RoJob>();
            var seen = new HashSet<(RoJobKind, string)>();
            foreach (var job in jobs ?? Array.Empty<RoJob>())
                if (!job.Path.IsEmpty && seen.Add((job.Kind, job.Path.Canonical))) list.Add(job);
            list.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : a.Path.CompareTo(b.Path));
            return list;
        }
    }
}
