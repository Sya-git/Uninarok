using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Editor.Ledger;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.Sprites;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// One catalog record (PLAN-0008 4.7): job path <c>catalog/&lt;category&gt;/&lt;key&gt;</c>, its record taken from the run's
    /// plan. Each pair goes through SPEC-0007 (<c>ctx.Sprite</c>, which skips what is current and shares sheets) and each
    /// palette through <c>RoPaletteConverter</c>; a failed pair fails the record with SPEC-0007's issue and leaves no partial
    /// output. The outputs are kept as strings in the run for the index. The ledger (category <see cref="LedgerCategory"/>)
    /// keys the record by its job path with the plan fingerprint as hash and the ACT/SPR/PAL hashes as dependencies; the
    /// record is <see cref="RoStatus.Skipped"/> when that entry is current and every sprite was skipped too.
    /// </summary>
    public sealed class RoCatalogEntryConverter : IRoConverter
    {
        /// <summary>The ledger of catalog records and indices (PLAN-0008 3).</summary>
        public const string LedgerCategory = "catalog";

        /// <summary>Raised when what an entry job writes changes.</summary>
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.CatalogEntry;

        public bool Handles(RoPath path) => path.Canonical.StartsWith(RoCatalogRun.JobRoot, StringComparison.Ordinal);

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            var run = ctx.CatalogPlan;
            if (run == null)
                return Fail(RoIssueCode.Unexpected, path, "a catalog job outside a catalog run (use RoImport.Catalog)");
            if (!run.TryGetRecord(path.Canonical, out var record))
                return Fail(RoIssueCode.SourceMissing, path, "the run's plan has no record for this job");
            if (RoCatalogOutputPaths.HasEntryAssets(record.Category))
                return Fail(RoIssueCode.Unexpected, path, "the entry categories are pending T5.7");

            var ledger = ctx.Ledger(LedgerCategory);
            bool ledgerCurrent = !ctx.Force && ledger.IsCurrent(path, record.Fingerprint, RulesVersion, ctx.Source, ctx.Writer);

            var outputs = new List<RoCatalogOutput>();
            bool allSkipped = true;
            UnityEngine.Object first = null;
            foreach (var pair in record.Pairs)
            {
                if (pair.IsStatic)
                    return Fail(RoIssueCode.CatalogOrphanSpr, path, $"{pair.Spr.Canonical}: an SPR without ACT cannot be converted (SPEC-0007 converts ACT/SPR pairs)");
                var result = ctx.Sprite(pair.Act, pair.Spr);
                if (result.Status == RoStatus.Failed || result.Value == null)
                    return Failed(result.Failure, path, pair.Act.Canonical);
                allSkipped &= result.Status == RoStatus.Skipped;
                first = first ?? result.Value;
                outputs.Add(OutputOf(pair.Role, result.Value));
            }
            foreach (var value in record.Values.Where(v => v.Name == "palette" || v.Name.StartsWith(RoCatalogRoles.PalettePrefix, StringComparison.Ordinal)))
            {
                if (!RoPath.TryParse(value.Text, out var pal)) return Fail(RoIssueCode.SourceMissing, path, $"not a palette path: {value.Text}");
                var result = Sprites.RoPaletteConverter.ConvertPalette(ctx, pal);
                if (result.Status == RoStatus.Failed || result.Value == null)
                    return Failed(result.Failure, path, pal.Canonical);
                allSkipped &= result.Status == RoStatus.Skipped;
                first = first ?? result.Value;
                outputs.Add(new RoCatalogOutput { Role = value.Name, AssetPath = AssetDatabase.GetAssetPath(result.Value) });
            }

            run.SetOutputs(path.Canonical, outputs.ToArray());
            if (ledgerCurrent && allSkipped) return RoResult<UnityEngine.Object>.Skipped(first);

            bool existed = ledger.Find(path) != null;
            ledger.Record(new RoLedgerEntry
            {
                Source = path.Canonical,
                SourceHash = record.Fingerprint,
                Rules = RulesVersion,
                Dependencies = Dependencies(ctx, record),
            }, ctx.Report);
            return existed ? RoResult<UnityEngine.Object>.Updated(first) : RoResult<UnityEngine.Object>.Created(first);
        }

        /// <summary>The source files of a record (its ACT, SPR and PAL), each with its current hash.</summary>
        static RoLedgerDependency[] Dependencies(RoImportContext ctx, RoCatalogRecord record)
        {
            var files = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var p in record.Pairs)
            {
                if (!p.Act.IsEmpty) files.Add(p.Act.Canonical);
                files.Add(p.Spr.Canonical);
            }
            foreach (var v in record.Values)
                if ((v.Name == "palette" || v.Name.StartsWith(RoCatalogRoles.PalettePrefix, StringComparison.Ordinal)) && RoPath.TryParse(v.Text, out var pal))
                    files.Add(pal.Canonical);
            return files.Select(f => new RoLedgerDependency(f, ctx.Source.TryGetFile(RoPath.Parse(f), out var file) ? file.ContentHash() : string.Empty)).ToArray();
        }

        internal static RoCatalogOutput OutputOf(string role, UnityEngine.Object value)
        {
            switch (value)
            {
                case SpriteAnimation animation:
                    var sheet = animation.Sprites.FirstOrDefault(s => s != null)?.texture;
                    return new RoCatalogOutput
                    {
                        Role = role,
                        AssetPath = AssetDatabase.GetAssetPath(animation),
                        SheetPath = sheet == null ? null : AssetDatabase.GetAssetPath(sheet),
                        Actions = animation.Actions?.Length ?? 0,
                    };
                case Sprite sprite:
                    string sheetPath = AssetDatabase.GetAssetPath(sprite);
                    return new RoCatalogOutput { Role = role, AssetPath = sheetPath, SubAsset = sprite.name, SheetPath = sheetPath };
                default:
                    return new RoCatalogOutput { Role = role, AssetPath = AssetDatabase.GetAssetPath(value) };
            }
        }

        // SPEC-0007's issue for the file, with the record as the one that asked for it (FR-10: one issue per failed record).
        static RoResult<UnityEngine.Object> Failed(RoIssue issue, RoPath job, string item) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(issue?.Code ?? RoIssueCode.Unexpected, issue?.Item ?? item, job.Canonical,
                issue?.Message ?? "the conversion gave nothing"));

        static RoResult<UnityEngine.Object> Fail(RoIssueCode code, RoPath path, string message) =>
            RoResult<UnityEngine.Object>.Failed(new RoIssue(code, path.Canonical, null, message));
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
