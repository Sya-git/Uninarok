using System;
using System.Collections.Generic;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// What the catalog converters of one run share (PLAN-0008 4.7): the plan, the tables it was built from, and the records by
    /// job path. Built by the catalog importer and handed to the batch through <c>RoImportOptions.CatalogPlan</c>.
    /// </summary>
    internal sealed class RoCatalogRun
    {
        public const string JobRoot = "catalog/";
        public const string IndexKey = "_index";

        readonly Dictionary<string, RoCatalogRecord> _byJob = new Dictionary<string, RoCatalogRecord>(StringComparer.Ordinal);

        public RoCatalogRun(RoCatalogPlan plan, RoCatalogTables tables)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Tables = tables;
            foreach (var r in plan.Records) _byJob[JobPath(r.Category, r.Key)] = r;
        }

        public RoCatalogPlan Plan { get; }
        public RoCatalogTables Tables { get; }

        /// <summary>The job path of a record: <c>catalog/&lt;category folder&gt;/&lt;key&gt;</c> (palettes: <c>catalog/palettes/…</c>).</summary>
        public static string JobPath(SpriteCatalogCategory category, string key) => JobRoot + FolderOf(category) + "/" + key;

        public static string IndexJobPath(SpriteCatalogCategory category) => JobPath(category, IndexKey);

        public static string FolderOf(SpriteCatalogCategory category) =>
            category == SpriteCatalogCategory.Palettes ? "palettes" : SpriteCatalogKeys.Folder(category);

        public bool TryGetRecord(string jobPath, out RoCatalogRecord record) => _byJob.TryGetValue(jobPath ?? string.Empty, out record);
    }
}
