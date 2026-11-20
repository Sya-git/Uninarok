using System;
using System.Collections.Generic;
using System.Linq;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>Helpers of the equipment planners (PLAN-0008 4.3.4–4.3.7).</summary>
    static class RoEquipmentFiles
    {
        public static readonly AccountSex[] Sexes = { AccountSex.SEX_MALE, AccountSex.SEX_FEMALE };
        public static readonly SpriteRace[] Races = { SpriteRace.Human, SpriteRace.Doram };

        /// <summary>
        /// The pair of <paramref name="stem"/> in <paramref name="folder"/> (relative to <c>sprite/</c>) when both halves exist;
        /// a lone half is claimed and reported as an orphan. <paramref name="spr"/> is the SPR path either way.
        /// </summary>
        public static bool TryPair(RoCatalogContext.FileSet files, string folder, string stem, string role, SpriteCatalogCategory category,
                                   HashSet<RoPath> claimed, RoCatalogPlan.Builder plan, out RoCatalogPair pair, out RoPath spr)
        {
            pair = default;
            if (!RoPath.TryParse(RoFileNames.SpriteRoot + folder + "/" + stem + RoFileNames.SprExtension, out spr)) return false;
            var act = spr.WithExtension(RoFileNames.ActExtension);
            bool hasAct = files.Contains(act), hasSpr = files.Contains(spr);
            if (hasAct) claimed.Add(act);
            if (hasSpr) claimed.Add(spr);
            if (hasAct && hasSpr)
            {
                pair = new RoCatalogPair(act, spr, role);
                return true;
            }
            if (hasAct) plan.Gap(RoIssueCode.CatalogOrphanAct, category, act.Canonical, "the ACT has no SPR");
            else if (hasSpr) plan.Gap(RoIssueCode.CatalogOrphanSpr, category, spr.Canonical, "the SPR has no ACT");
            return false;
        }

        /// <summary>The class table row of a body name, human first (a body name is unique per race).</summary>
        public static bool TryBody(RoOwnTables own, string bodyName, out RoClassBody body) =>
            own.TryGetBody(bodyName, false, out body) || own.TryGetBody(bodyName, true, out body);

        /// <summary>
        /// Unclaimed files grouped by their first folder under <paramref name="root"/>: one <see cref="RoIssueCode.CatalogFileWithoutName"/>
        /// per folder no table names (a whole garment nobody refers to), one per file for files directly in a named folder.
        /// </summary>
        public static void ReportUnclaimedFolders(RoCatalogContext.FileSet files, HashSet<RoPath> claimed, string root, SpriteCatalogCategory category,
                                                  RoCatalogPlan.Builder plan, string message)
        {
            var claimedFolders = new HashSet<string>(claimed.Select(p => FirstFolder(p, root)), StringComparer.Ordinal);
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var path in files.All)
            {
                if (claimed.Contains(path)) continue;
                string folder = FirstFolder(path, root);
                if (folder == null || claimedFolders.Contains(folder))
                {
                    if (path.Extension == RoFileNames.SprExtension
                        || (path.Extension == RoFileNames.ActExtension && !files.Contains(path.WithExtension(RoFileNames.SprExtension))))
                        plan.Gap(RoIssueCode.CatalogFileWithoutName, category, path.Canonical, "no slot uses this file");
                }
                else if (reported.Add(folder)) plan.Gap(RoIssueCode.CatalogFileWithoutName, category, root + folder + "/", message);
            }
        }

        static string FirstFolder(RoPath path, string root)
        {
            string rest = path.Canonical.Substring(root.Length);
            int slash = rest.IndexOf('/');
            return slash < 0 ? null : rest.Substring(0, slash);
        }

        /// <summary>One <see cref="RoIssueCode.CatalogFileWithoutName"/> per unclaimed sprite stem (its SPR, else its ACT).</summary>
        public static void ReportUnclaimed(RoCatalogContext.FileSet files, HashSet<RoPath> claimed, SpriteCatalogCategory category,
                                           RoCatalogPlan.Builder plan, string message)
        {
            foreach (var path in files.All)
            {
                if (claimed.Contains(path)) continue;
                if (path.Extension == RoFileNames.SprExtension
                    || (path.Extension == RoFileNames.ActExtension && !files.Contains(path.WithExtension(RoFileNames.SprExtension))))
                    plan.Gap(RoIssueCode.CatalogFileWithoutName, category, path.Canonical, message);
            }
        }
    }

    /// <summary>
    /// Body names that share one equipment slot (PLAN-0008 D20): the Base row wins, else the first offered; the files left out
    /// are counted and reported once per category.
    /// </summary>
    sealed class RoSharedSlots
    {
        int _dropped;

        public void Offer<T>(Dictionary<string, (RoClassBody body, T pair)> slots, string role, RoClassBody body, T pair)
        {
            if (!slots.TryGetValue(role, out var held))
            {
                slots.Add(role, (body, pair));
                return;
            }
            _dropped++;
            if (body.Kind == "Base" && held.body.Kind != "Base") slots[role] = (body, pair);
        }

        public void Report(SpriteCatalogCategory category, RoCatalogPlan.Builder plan)
        {
            if (_dropped > 0)
                plan.Gap(RoIssueCode.CatalogStrayFile, category, $"{category}: shared slots",
                    $"{_dropped} files of outfits and all-class mounts share their class's slot and are not used (PLAN-0008 D20)");
        }
    }

    /// <summary>Headgears (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.4): one record per view id of <c>AccNameTable</c>.</summary>
    public sealed class RoHeadgearPlanner : IRoCatalogPlanner
    {
        public const string Folder = "악세사리";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Headgears;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var views = ctx.Tables.ViewIds;
            if (views == null) return;
            string prefix = RoFileNames.SpriteFolder(Folder);
            var files = ctx.Files(prefix);
            var claimed = new HashSet<RoPath>();

            foreach (int id in views.HeadgearConstants.Values.Distinct().OrderBy(i => i))
            {
                if (!views.HeadgearStems.TryGetValue(id, out string stem) || string.IsNullOrWhiteSpace(stem))
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, $"AccNameTable[{id}]", $"headgear view id {id} has no name in AccNameTable");
                    continue;
                }
                var pairs = new List<RoCatalogPair>();
                RoPath first = default;
                foreach (var sex in RoEquipmentFiles.Sexes)
                foreach (var race in RoEquipmentFiles.Races)
                {
                    string marker = RoCatalogRoles.SexMarker(sex);
                    string folder = Folder + "/" + marker + (race == SpriteRace.Doram ? "_doram" : string.Empty);
                    if (!RoEquipmentFiles.TryPair(files, folder, marker + stem, RoCatalogRoles.Headgear(sex, race), Category, claimed, plan, out var pair, out var spr))
                    {
                        if (first.IsEmpty) first = spr;
                        continue;
                    }
                    pairs.Add(pair);
                }
                if (pairs.Count == 0)
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, first.IsEmpty ? prefix + stem : first.Canonical, $"headgear view id {id} ({stem}) has no sprite");
                    continue;
                }
                plan.Add(Category, RoCatalogKeys.Headgear(id), pairs, new[] { RoCatalogValue.Of("viewId", id), RoCatalogValue.Of("name", stem) });
            }

            RoEquipmentFiles.ReportUnclaimed(files, claimed, Category, plan, "no AccNameTable name refers to this headgear");
        }
    }

    /// <summary>
    /// Garments (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.5): one record per view id of <c>RobeNameTable</c>; each class ACT is
    /// paired with its own SPR if it has one, else the root sheet (<c>_doram</c> for a doram body).
    /// </summary>
    public sealed class RoGarmentPlanner : IRoCatalogPlanner
    {
        public const string Folder = "로브";

        public const string DoramSheetSuffix = "_doram";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Garments;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var views = ctx.Tables.ViewIds;
            var own = ctx.Tables.Own;
            if (views == null || own == null) return;
            string root = RoFileNames.SpriteFolder(Folder);
            var files = ctx.Files(root);
            var claimed = new HashSet<RoPath>();
            var shared = new RoSharedSlots();

            foreach (int id in views.GarmentConstants.Values.Distinct().OrderBy(i => i))
            {
                if (!views.GarmentFolders.TryGetValue(id, out string name) || string.IsNullOrWhiteSpace(name)
                    || !RoPath.TryParse(root + name, out var folderPath))
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, $"RobeNameTable[{id}]", $"garment view id {id} has no folder name");
                    continue;
                }
                string prefix = folderPath.Canonical + "/";
                string folder = Folder + "/" + name;
                var rootAct = RoFileNames.Act(folder, name);
                var rootSpr = RoFileNames.Spr(folder, name);
                var doramSpr = RoFileNames.Spr(folder, name + DoramSheetSuffix);
                bool hasRoot = files.Contains(rootSpr), hasDoram = files.Contains(doramSpr);
                var pairs = new List<RoCatalogPair>();
                var slots = new Dictionary<string, (RoClassBody body, RoCatalogPair pair)>(StringComparer.Ordinal);

                if (files.Contains(rootAct) && hasRoot) pairs.Add(new RoCatalogPair(rootAct, rootSpr, RoCatalogRoles.GarmentRoot(SpriteRace.Human)));
                if (files.Contains(rootAct) && hasDoram) pairs.Add(new RoCatalogPair(rootAct, doramSpr, RoCatalogRoles.GarmentRoot(SpriteRace.Doram)));
                foreach (var p in new[] { rootAct, rootSpr, doramSpr }) if (files.Contains(p)) claimed.Add(p);

                foreach (var sex in RoEquipmentFiles.Sexes)
                {
                    string marker = RoCatalogRoles.SexMarker(sex);
                    string sexPrefix = prefix + marker + "/";
                    foreach (var act in files.Under(sexPrefix).Where(p => p.Extension == RoFileNames.ActExtension).ToList())
                    {
                        claimed.Add(act);
                        string stem = RoFileNames.StemUnder(act, sexPrefix);
                        string suffix = "_" + marker;
                        if (!stem.EndsWith(suffix, StringComparison.Ordinal) || stem.Length == suffix.Length
                            || !RoEquipmentFiles.TryBody(own, stem.Substring(0, stem.Length - suffix.Length), out var body))
                        {
                            // Its own SPR goes with it; one warning per body name across every garment (4th classes are in most).
                            claimed.Add(act.WithExtension(RoFileNames.SprExtension));
                            plan.Gap(RoIssueCode.CatalogStrayFile, Category, $"{root}*/{marker}/{stem}",
                                "a garment ACT of a body the class table does not have (first seen: " + act.Canonical + ")");
                            continue;
                        }
                        var race = body.Doram ? SpriteRace.Doram : SpriteRace.Human;
                        var ownSpr = act.WithExtension(RoFileNames.SprExtension);
                        RoPath sheet;
                        if (files.Contains(ownSpr))
                        {
                            sheet = ownSpr;
                            claimed.Add(ownSpr);
                        }
                        else if (race == SpriteRace.Doram && hasDoram) sheet = doramSpr;
                        else if (hasRoot) sheet = rootSpr;
                        else
                        {
                            plan.Gap(RoIssueCode.CatalogOrphanAct, Category, act.Canonical, "the ACT has no SPR of its own and the garment no root sheet");
                            continue;
                        }
                        string role = RoCatalogRoles.Garment(body.Job, sex, race);
                        shared.Offer(slots, role, body, new RoCatalogPair(act, sheet, role));
                    }
                }

                pairs.AddRange(slots.Values.Select(s => s.pair));
                if (pairs.Count == 0)
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, prefix, $"garment view id {id} ({name}) has no sprite");
                    continue;
                }
                plan.Add(Category, RoCatalogKeys.Garment(id), pairs, new[] { RoCatalogValue.Of("viewId", id), RoCatalogValue.Of("name", name) });
            }

            RoEquipmentFiles.ReportUnclaimedFolders(files, claimed, root, Category, plan, "no RobeNameTable name refers to this garment folder");
            shared.Report(Category, plan);
        }
    }

    /// <summary>
    /// Weapons (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.6, 4.3.11, OQ-3): one record per view id, plus one per item id with its
    /// own file; trails are extra pairs of the same slot; a class without the view id's file falls back to the base type.
    /// </summary>
    public sealed class RoWeaponPlanner : IRoCatalogPlanner
    {
        public SpriteCatalogCategory Category => SpriteCatalogCategory.Weapons;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.10");
    }

    /// <summary>
    /// Shields (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.7): one record per view id of <c>shield-names.csv</c>, plus one per
    /// item id with its own <c>&lt;id&gt;_방패</c> file.
    /// </summary>
    public sealed class RoShieldPlanner : IRoCatalogPlanner
    {
        public const string Folder = "방패";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Shields;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.11");
    }
}
