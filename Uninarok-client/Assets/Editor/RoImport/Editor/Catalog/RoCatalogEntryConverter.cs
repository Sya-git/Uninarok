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
            bool hasEntry = RoCatalogOutputPaths.HasEntryAssets(record.Category);
            string entryPath = hasEntry ? RoCatalogOutputPaths.Entry(ctx.Roots, record.Category, record.Key) : null;

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
            if (hasEntry)
            {
                // Written in place: GUID kept, and an unchanged entry is not touched (PLAN-0002 4.7).
                var status = ctx.Writer.UpsertObject(entryPath, RoCatalogEntryWriter.Build(record, outputs));
                allSkipped &= status == RoStatus.Skipped;
                first = ctx.Writer.Load<ScriptableObject>(entryPath);
            }
            if (ledgerCurrent && allSkipped) return RoResult<UnityEngine.Object>.Skipped(first);

            bool existed = ledger.Find(path) != null;
            ledger.Record(new RoLedgerEntry
            {
                Source = path.Canonical,
                SourceHash = record.Fingerprint,
                Rules = RulesVersion,
                Outputs = hasEntry ? new[] { entryPath } : new string[0],
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

    /// <summary>
    /// The index of one catalog category (PLAN-0008 4.6, 4.7): job path <c>catalog/&lt;category&gt;/_index</c>, run after every
    /// entry job (D6). It makes sure the Addressables layout exists (once per run), writes the index from the run's outputs
    /// (in place, untouched when unchanged), writes the item and class tables the first time a category needs them, and
    /// verifies every reference it and the category's entries hold.
    /// </summary>
    public sealed class RoCatalogIndexConverter : IRoConverter
    {
        public const int RulesVersion = 1;

        public RoJobKind Kind => RoJobKind.CatalogIndex;

        public bool Handles(RoPath path) =>
            path.Canonical.StartsWith(RoCatalogRun.JobRoot, StringComparison.Ordinal) && path.Canonical.EndsWith("/" + RoCatalogRun.IndexKey, StringComparison.Ordinal);

        public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path)
        {
            var run = ctx.CatalogPlan;
            if (run == null)
                return RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.Unexpected, path.Canonical, null, "a catalog job outside a catalog run (use RoImport.Catalog)"));
            if (!RoCatalogRun.TryCategoryOf(path.Canonical, out var category))
                return RoResult<UnityEngine.Object>.Failed(new RoIssue(RoIssueCode.SourceMissing, path.Canonical, null, "not a catalog category"));
            if (category == SpriteCatalogCategory.Palettes) return RoResult<UnityEngine.Object>.Skipped(null);   // palettes have no index

            if (!run.AddressablesEnsured)
            {
                run.Addressables = run.Addressables ?? RoCatalogAddressables.ProjectSettings();
                RoCatalogAddressables.Ensure(run.Addressables, ctx.Roots);
                run.AddressablesEnsured = true;
            }

            var ledger = ctx.Ledger(RoCatalogEntryConverter.LedgerCategory);
            var statuses = new List<RoStatus>();
            var written = new List<UnityEngine.Object>();

            string indexPath = RoCatalogOutputPaths.Index(ctx.Roots, category);
            statuses.Add(Write(ctx, ledger, path.Canonical, indexPath, RoCatalogIndexWriter.Build(run, category, ctx.Roots), Fingerprint(run, category)));
            var index = ctx.Writer.Load<ScriptableObject>(indexPath);
            written.Add(index);

            bool equipment = RoCatalogOutputPaths.HasEntryAssets(category) && category != SpriteCatalogCategory.Bodies && category != SpriteCatalogCategory.Heads;
            if ((equipment || category == SpriteCatalogCategory.GroundItems) && !run.ItemTableWritten && run.Tables?.Items != null)
            {
                string itemsPath = RoCatalogOutputPaths.Items(ctx.Roots);
                var table = RoCatalogIndexWriter.Items(run.Tables.Items, run.Tables.ItemKinds, GroundNames(ctx, run));
                statuses.Add(Write(ctx, ledger, RoCatalogRun.JobRoot + RoCatalogOutputPaths.ItemsFolder + "/" + RoCatalogRun.IndexKey, itemsPath, table,
                    run.Tables.ItemKinds == null ? "no-kinds" : "kinds"));
                written.Add(ctx.Writer.Load<ScriptableObject>(itemsPath));
                run.ItemTableWritten = true;
            }
            if ((equipment || category == SpriteCatalogCategory.Bodies) && !run.ClassTableWritten && run.Tables?.Own != null)
            {
                string classesPath = RoCatalogOutputPaths.Classes(ctx.Roots);
                statuses.Add(Write(ctx, ledger, RoCatalogRun.JobRoot + RoCatalogOutputPaths.ClassesFolder + "/" + RoCatalogRun.IndexKey, classesPath,
                    RoCatalogIndexWriter.Classes(run.Tables.Own), "classes"));
                written.Add(ctx.Writer.Load<ScriptableObject>(classesPath));
                run.ClassTableWritten = true;
            }

            if (RoCatalogOutputPaths.HasEntryAssets(category))
                foreach (var (record, _) in RoCatalogIndexWriter.Converted(run, category))
                    written.Add(ctx.Writer.Load<ScriptableObject>(RoCatalogOutputPaths.Entry(ctx.Roots, category, record.Key)));
            RoCatalogAddressables.Verify(run.Addressables, ctx.Roots, written, ctx.Report);

            if (statuses.All(s => s == RoStatus.Skipped)) return RoResult<UnityEngine.Object>.Skipped(index);
            return statuses.Contains(RoStatus.Created) ? RoResult<UnityEngine.Object>.Created(index) : RoResult<UnityEngine.Object>.Updated(index);
        }

        // In place through the writer (unchanged content is not rewritten), owned by its own ledger entry.
        static RoStatus Write(RoImportContext ctx, RoLedger ledger, string source, string assetPath, ScriptableObject fresh, string hash)
        {
            var status = ctx.Writer.UpsertObject(assetPath, fresh);
            ledger.Record(new RoLedgerEntry { Source = source, SourceHash = hash, Rules = RulesVersion, Outputs = new[] { assetPath } }, ctx.Report);
            return status;
        }

        /// <summary>The fingerprints of the category's converted records, hashed: the index's ledger hash (PLAN-0008 4.6).</summary>
        static string Fingerprint(RoCatalogRun run, SpriteCatalogCategory category)
        {
            var text = string.Join("\n", RoCatalogIndexWriter.Converted(run, category).Select(x => x.record.Key + "\t" + x.record.Fingerprint));
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)).Select(b => b.ToString("x2")));
        }

        // The ground item index's resource names: this run's, else the index already on disk, else none.
        static IReadOnlyList<string> GroundNames(RoImportContext ctx, RoCatalogRun run)
        {
            var mine = RoCatalogIndexWriter.Converted(run, SpriteCatalogCategory.GroundItems);
            if (mine.Count > 0) return mine.Select(x => x.record.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
            var existing = ctx.Writer.Load<GroundItemIndex>(RoCatalogOutputPaths.Index(ctx.Roots, SpriteCatalogCategory.GroundItems));
            return existing != null ? existing.ResourceNames : Array.Empty<string>();
        }
    }
}
