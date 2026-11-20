using System;
using System.Collections.Generic;
using System.Linq;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Monsters, NPCs or homunculi (SPEC-0008 FR-1, FR-10; PLAN-0008 4.3.1): one record per job id of
    /// <c>JobNameTable</c> in the category's id range, keyed by the id, pair <c>&lt;folder&gt;/&lt;name&gt;</c>, values the
    /// id, the name and the display scale (default 1). Gaps: a name with no file (3D <c>.gr2</c>, other regions), an ACT or
    /// SPR without its other half, a file no name refers to, and an id outside every range (not a player class).
    /// </summary>
    public sealed class RoUnitPlanner : IRoCatalogPlanner
    {
        public const string Role = "unit";

        public RoUnitPlanner(SpriteCatalogCategory category)
        {
            if (category != SpriteCatalogCategory.Monsters && category != SpriteCatalogCategory.Npcs && category != SpriteCatalogCategory.Homunculi)
                throw new ArgumentException($"{category} is not a unit category", nameof(category));
            Category = category;
        }

        public SpriteCatalogCategory Category { get; }
        public RoCatalogNeeds Needs => RoCatalogNeeds.Jobs | RoCatalogNeeds.Scales;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var jobs = ctx.Tables.Jobs;
            if (jobs == null) return;
            var scales = ctx.Tables.Scales;
            var players = new HashSet<int>(jobs.PlayerConstants.Values);
            string folder = RoUnitFolders.FolderOf(Category);
            string prefix = RoFileNames.SpriteFolder(folder);
            var files = ctx.Files(prefix);
            var named = new HashSet<RoPath>();

            foreach (var entry in jobs.SpriteNames.OrderBy(e => e.Key))
            {
                int id = entry.Key;
                string name = entry.Value ?? string.Empty;
                bool inRange = RoUnitFolders.TryGetCategory(id, out var category);

                // Every name claims its files in this folder, whatever its range: a file named by any id is not unreferenced.
                bool valid = TryFiles(folder, name, out var act, out var spr);
                if (valid)
                {
                    named.Add(act);
                    named.Add(spr);
                }

                if (!inRange)
                {
                    if (!players.Contains(id))
                        plan.Gap(RoIssueCode.CatalogStrayFile, Category, $"JobNameTable[{id}]", $"job id {id} ({name}) is outside every unit id range");
                    continue;
                }
                if (category != Category) continue;

                if (!valid)
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, prefix + name.ToLowerInvariant(), $"job {id} names '{name}', which is not a sprite");
                    continue;
                }

                bool hasAct = files.Contains(act), hasSpr = files.Contains(spr);
                if (hasAct && hasSpr)
                {
                    plan.Add(Category, id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        new[] { new RoCatalogPair(act, spr, Role) },
                        new[]
                        {
                            RoCatalogValue.Of("jobId", id),
                            RoCatalogValue.Of("name", name),
                            RoCatalogValue.Of("scale", scales?.ScaleOf(id) ?? RoMonsterScaleTable.DefaultScale),
                        });
                }
                else if (hasAct) plan.Gap(RoIssueCode.CatalogOrphanAct, Category, act.Canonical, $"job {id} ({name}): the ACT has no SPR");
                else if (hasSpr) plan.Gap(RoIssueCode.CatalogOrphanSpr, Category, spr.Canonical, $"job {id} ({name}): the SPR has no ACT");
                else plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, spr.Canonical, $"job {id} ({name}) has no sprite in {prefix}");
            }

            ReportUnnamed(files, named, plan);
        }

        // Sprites of the folder that no name of JobNameTable refers to.
        void ReportUnnamed(RoCatalogContext.FileSet files, HashSet<RoPath> named, RoCatalogPlan.Builder plan)
        {
            foreach (var path in files.All)
            {
                if (named.Contains(path)) continue;
                string ext = path.Extension;
                if (ext == RoFileNames.ActExtension)
                {
                    if (files.Contains(path.WithExtension(RoFileNames.SprExtension)))
                        plan.Gap(RoIssueCode.CatalogFileWithoutName, Category, path.Canonical, "no JobNameTable name refers to this sprite");
                    else
                        plan.Gap(RoIssueCode.CatalogOrphanAct, Category, path.Canonical, "the ACT has no SPR");
                }
                else if (ext == RoFileNames.SprExtension && !files.Contains(path.WithExtension(RoFileNames.ActExtension)))
                {
                    plan.Gap(RoIssueCode.CatalogOrphanSpr, Category, path.Canonical, "the SPR has no ACT");
                }
            }
        }

        static bool TryFiles(string folder, string name, out RoPath act, out RoPath spr)
        {
            act = spr = default;
            if (string.IsNullOrWhiteSpace(name) || name.EndsWith(".gr2", StringComparison.OrdinalIgnoreCase)) return false;
            if (!RoPath.TryParse(RoFileNames.SpriteRoot + folder + "/" + name + RoFileNames.SprExtension, out spr)) return false;
            act = spr.WithExtension(RoFileNames.ActExtension);
            return true;
        }
    }
}
