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

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Garments;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.9");
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
