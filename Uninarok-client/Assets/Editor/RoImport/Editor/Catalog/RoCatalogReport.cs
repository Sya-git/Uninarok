using System;
using System.Collections.Generic;
using System.Text;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>The numbers of one category in one catalog run (SPEC-0008 FR-9; PLAN-0008 4.7).</summary>
    public sealed class RoCategoryReport
    {
        public SpriteCatalogCategory Category;
        /// <summary>Records planned and imported (or current).</summary>
        public int Entries;
        /// <summary>Distinct sheet textures among the category's animations and sprites.</summary>
        public int Sheets;
        /// <summary>Stored (deduplicated) actions of the category's animations.</summary>
        public int Actions;
        /// <summary>The category's gaps plus its issues.</summary>
        public int Warnings;
        public int Failed;
        public TimeSpan Time;
        /// <summary>True when the category could not run (a table it needs is missing).</summary>
        public bool Skipped;

        public override string ToString() =>
            Skipped ? $"{Category}: skipped"
                    : $"{Category}: {Entries} entries, {Sheets} sheets, {Actions} actions, {Warnings} warnings, {Failed} failed, {Time.TotalSeconds:0.0} s";
    }

    /// <summary>What a catalog run did (SPEC-0008 FR-9, FR-10; PLAN-0008 4.7): one block per category and the run's report.</summary>
    public sealed class RoCatalogReport
    {
        public RoCatalogReport(IReadOnlyList<RoCategoryReport> categories, RoReport report)
        {
            Categories = categories ?? Array.Empty<RoCategoryReport>();
            Report = report ?? new RoReport();
        }

        public IReadOnlyList<RoCategoryReport> Categories { get; }
        public RoReport Report { get; }

        public RoCategoryReport Of(SpriteCatalogCategory category)
        {
            foreach (var c in Categories)
                if (c.Category == category) return c;
            return null;
        }

        public string Summarize()
        {
            var sb = new StringBuilder("[RoImport] Catalog\n");
            foreach (var c in Categories) sb.Append("  ").Append(c).Append('\n');
            sb.Append(Report.Summarize());
            return sb.ToString();
        }
    }
}
