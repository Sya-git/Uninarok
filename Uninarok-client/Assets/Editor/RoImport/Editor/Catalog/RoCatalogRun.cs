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

        readonly Dictionary<string, RoCatalogOutput[]> _outputs = new Dictionary<string, RoCatalogOutput[]>(StringComparer.Ordinal);

        /// <summary>What the entry job of a record produced, as strings, for the index (PLAN-0008 4.7: no asset loaded to build it).</summary>
        public void SetOutputs(string jobPath, RoCatalogOutput[] outputs) => _outputs[jobPath] = outputs;

        public bool TryGetOutputs(string jobPath, out RoCatalogOutput[] outputs) => _outputs.TryGetValue(jobPath ?? string.Empty, out outputs);
    }

    /// <summary>One converted pair (or palette) of a record: its role, the asset and, for a static sprite, the sub-asset name.</summary>
    internal sealed class RoCatalogOutput
    {
        public string Role;
        /// <summary>The animation, the sheet of a static sprite, or the palette texture.</summary>
        public string AssetPath;
        /// <summary>The sprite's name inside its sheet; null for an animation or a palette.</summary>
        public string SubAsset;
        /// <summary>The sheet texture the animation or sprite draws from.</summary>
        public string SheetPath;
        /// <summary>Stored (deduplicated) actions of the animation; 0 for a still or a palette.</summary>
        public int Actions;

        public bool IsStill => SubAsset != null;
    }
}
