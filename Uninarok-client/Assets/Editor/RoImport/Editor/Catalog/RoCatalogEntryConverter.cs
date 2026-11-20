using System;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Pipeline;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>One catalog record (PLAN-0008 4.7): job path <c>catalog/&lt;category&gt;/&lt;key&gt;</c>.</summary>
    public sealed class RoCatalogEntryConverter : IRoConverter
    {
        /// <summary>The ledger of catalog records and indices (PLAN-0008 3).</summary>
        public const string LedgerCategory = "catalog";

        public RoJobKind Kind => RoJobKind.CatalogEntry;

        public bool Handles(RoPath path) => path.Canonical.StartsWith(RoCatalogRun.JobRoot, StringComparison.Ordinal);

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.Unexpected, path.Canonical, null, "the catalog entry converter is pending T5.6"));
    }

    /// <summary>The index of one catalog category (PLAN-0008 4.7): job path <c>catalog/&lt;category&gt;/_index</c>.</summary>
    public sealed class RoCatalogIndexConverter : IRoConverter
    {
        public RoJobKind Kind => RoJobKind.CatalogIndex;

        public bool Handles(RoPath path) =>
            path.Canonical.StartsWith(RoCatalogRun.JobRoot, StringComparison.Ordinal) && path.Canonical.EndsWith("/" + RoCatalogRun.IndexKey, StringComparison.Ordinal);

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.Unexpected, path.Canonical, null, "the catalog index converter is pending T5.8"));
    }
}
