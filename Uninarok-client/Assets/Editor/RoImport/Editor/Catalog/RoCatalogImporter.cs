using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Planning;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;
using Uninarok.RoImport.Core.Source;
using Uninarok.RoImport.Editor.Pipeline;
using Uninarok.RoImport.Editor.Ui;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// A catalog run (SPEC-0008 FR-9, FR-10, FR-12; PLAN-0008 4.7): the tables for the union of the chosen categories' needs,
    /// the plan of those categories only, its gaps reported once, then one batch per category (its entry jobs, then its index
    /// job) adding to one report, and the numbers of each category. A category whose tables are missing is skipped with one
    /// warning; the others run. Categories never depend on each other's outputs.
    /// </summary>
    public static class RoCatalogImporter
    {
        /// <summary>The planner of each category (the hand-written list of PLAN-0008 4.3).</summary>
        public static IRoCatalogPlanner PlannerOf(SpriteCatalogCategory category)
        {
            switch (category)
            {
                case SpriteCatalogCategory.Monsters:
                case SpriteCatalogCategory.Npcs:
                case SpriteCatalogCategory.Homunculi: return new RoUnitPlanner(category);
                case SpriteCatalogCategory.Bodies: return new RoBodyPlanner();
                case SpriteCatalogCategory.Heads: return new RoHeadPlanner();
                case SpriteCatalogCategory.Headgears: return new RoHeadgearPlanner();
                case SpriteCatalogCategory.Garments: return new RoGarmentPlanner();
                case SpriteCatalogCategory.Weapons: return new RoWeaponPlanner();
                case SpriteCatalogCategory.Shields: return new RoShieldPlanner();
                case SpriteCatalogCategory.GroundItems: return new RoGroundItemPlanner();
                case SpriteCatalogCategory.SkillIcons: return new RoSkillIconPlanner();
                case SpriteCatalogCategory.Effects: return new RoEffectPlanner();
                case SpriteCatalogCategory.Palettes: return new RoPalettePlanner();
                default: throw new ArgumentOutOfRangeException(nameof(category), category, null);
            }
        }

        /// <summary>The table groups a category needs, the shared tables its index step writes included (item table, class table).</summary>
        public static RoCatalogNeeds NeedsOf(SpriteCatalogCategory category)
        {
            var needs = PlannerOf(category).Needs;
            bool equipment = category == SpriteCatalogCategory.Headgears || category == SpriteCatalogCategory.Garments
                             || category == SpriteCatalogCategory.Weapons || category == SpriteCatalogCategory.Shields;
            if (equipment || category == SpriteCatalogCategory.GroundItems) needs |= RoCatalogNeeds.Items | RoCatalogNeeds.ItemKinds;
            if (equipment || category == SpriteCatalogCategory.Bodies) needs |= RoCatalogNeeds.Own;
            return needs;
        }

        /// <summary>The composition tables run with bodies and every equipment category, or alone when asked (PLAN-0009 4.1, D1).</summary>
        static bool WantsComposition(IEnumerable<SpriteCatalogCategory> chosen, RoImportOptions options) =>
            options.Composition || chosen.Any(c => c == SpriteCatalogCategory.Bodies || c == SpriteCatalogCategory.Headgears || c == SpriteCatalogCategory.Garments
                                                   || c == SpriteCatalogCategory.Weapons || c == SpriteCatalogCategory.Shields);

        public static RoCatalogReport Run(IEnumerable<SpriteCatalogCategory> categories, RoImportOptions options = null)
        {
            options = (options ?? new RoImportOptions()).Clone();
            // Ground items first, so the item table their index step writes sees this run's ground slots; then enum order.
            var chosen = (categories ?? Enumerable.Empty<SpriteCatalogCategory>()).Distinct()
                .OrderBy(c => c == SpriteCatalogCategory.GroundItems ? -1 : (int)c).ToList();
            var report = new RoReport();
            RoHashCache hashes = null;
            if (options.Source == null)
            {
                hashes = new RoHashCache(RoHashCache.DefaultFile);
                hashes.Load();
                options.Source = new RoFolderSource(RoSourceLocation.Resolve(), report, hashes);
            }
            var system = options.SystemSource ?? (Directory.Exists(RoSourceLocation.ResolveSystem()) ? new RoFolderSource(RoSourceLocation.ResolveSystem()) : null);
            var language = RoImportSettings.LanguageFor(options);

            bool composition = WantsComposition(chosen, options);
            var needs = chosen.Aggregate(RoCatalogNeeds.None, (n, c) => n | NeedsOf(c));
            if (composition) needs |= RoCompositionTablesConverter.Needs;
            var tables = RoCatalogTables.Load(options.Source, system, needs, language, report, options.CatalogOwnTablesFolder,
                (needs & RoCatalogNeeds.ItemKinds) != 0 ? RoImportSettings.ItemDbPathFor(options) : null);

            // Plan the categories whose tables loaded; the others are skipped with one warning each.
            var ctx = new RoCatalogContext(options.Source, tables);
            var builder = new RoCatalogPlan.Builder();
            var numbers = new Dictionary<SpriteCatalogCategory, RoCategoryReport>();
            var planTime = new Dictionary<SpriteCatalogCategory, TimeSpan>();
            foreach (var c in chosen)
            {
                numbers[c] = new RoCategoryReport { Category = c };
                var planner = PlannerOf(c);
                if (!tables.Has(planner.Needs))
                {
                    numbers[c].Skipped = true;
                    report.Warn(RoIssueCode.SourceMissing, c.ToString(), c.ToString(), $"{c} is skipped: tables missing ({planner.Needs & tables.Missing})");
                    continue;
                }
                var sw = Stopwatch.StartNew();
                planner.Plan(ctx, builder);
                planTime[c] = sw.Elapsed;
            }
            var plan = builder.Build();
            plan.ReportGaps(report);
            var run = new RoCatalogRun(plan, tables) { Addressables = options.CatalogAddressables };
            options.CatalogPlan = run;

            foreach (var c in chosen.Where(c => !numbers[c].Skipped))
            {
                var jobs = plan.RecordsOf(c).Select(r => new RoJob(RoJobKind.CatalogEntry, RoPath.Parse(RoCatalogRun.JobPath(c, r.Key)))).ToList();
                if (c != SpriteCatalogCategory.Palettes) jobs.Add(new RoJob(RoJobKind.CatalogIndex, RoPath.Parse(RoCatalogRun.IndexJobPath(c))));
                int issuesBefore = report.Issues.Count, failedBefore = report.Failed;
                var sw = Stopwatch.StartNew();
                RoBatch.Run(jobs, options, report);
                Fill(numbers[c], run, plan, c);
                numbers[c].Time = sw.Elapsed + (planTime.TryGetValue(c, out var t) ? t : TimeSpan.Zero);
                numbers[c].Failed = report.Failed - failedBefore;
                numbers[c].Warnings = plan.GapsOf(c).Count() + (report.Issues.Count - issuesBefore);
            }
            if (composition) RoBatch.Run(new[] { new RoJob(RoJobKind.Composition, RoPath.Parse(RoCompositionTablesConverter.Job)) }, options, report);
            hashes?.Save();
            return new RoCatalogReport(chosen.Select(c => numbers[c]).ToArray(), report);
        }

        // Entries, sheets and stored actions from what the records produced in this run (a skipped record produces them too).
        static void Fill(RoCategoryReport n, RoCatalogRun run, RoCatalogPlan plan, SpriteCatalogCategory c)
        {
            var outputs = plan.RecordsOf(c).Select(r => run.TryGetOutputs(RoCatalogRun.JobPath(c, r.Key), out var o) ? o : null).Where(o => o != null).ToList();
            n.Entries = outputs.Count;
            var sprites = outputs.SelectMany(o => o).Where(o => o.Role == null || !o.Role.StartsWith(RoCatalogRoles.PalettePrefix, StringComparison.Ordinal) && o.Role != "palette").ToList();
            n.Sheets = sprites.Select(o => o.SheetPath).Where(s => s != null).Distinct(StringComparer.Ordinal).Count();
            n.Actions = sprites.GroupBy(o => o.AssetPath, StringComparer.Ordinal).Sum(g => g.First().Actions);
        }
    }
}
