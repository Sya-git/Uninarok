using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Ground items (SPEC-0008 FR-5, FR-10; PLAN-0008 4.3.8): one record per distinct resource name of the item table
    /// (identified and unidentified), keyed by the name, pair <c>아이템/&lt;resource&gt;</c>.
    /// </summary>
    public sealed class RoGroundItemPlanner : IRoCatalogPlanner
    {
        public const string Folder = "아이템";
        public const string Role = "ground";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.GroundItems;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Items;

        /// <summary>The record key of a resource name: its canonical stem under <c>아이템/</c>, or null when it is not a valid name.</summary>
        public static string KeyOf(string resource) =>
            RoStaticFiles.TryFiles(Folder, resource, out _, out var spr) ? RoFileNames.StemUnder(spr, RoFileNames.SpriteFolder(Folder)) : null;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var items = ctx.Tables.Items;
            if (items == null) return;
            string prefix = RoFileNames.SpriteFolder(Folder);
            var files = ctx.Files(prefix);

            // Distinct resource names, identified and unidentified, in ordinal order of their key.
            var resources = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var invalid = new SortedSet<string>(StringComparer.Ordinal);
            foreach (int id in items.ItemIds)
            {
                if (!items.TryGet(id, out var info)) continue;
                Collect(info.IdentifiedResource, resources, invalid);
                Collect(info.UnidentifiedResource, resources, invalid);
            }

            var claimed = new HashSet<RoPath>();
            foreach (var r in resources)
            {
                RoStaticFiles.TryFiles(Folder, r.Value, out var act, out var spr);
                claimed.Add(act);
                claimed.Add(spr);
                bool hasAct = files.Contains(act), hasSpr = files.Contains(spr);
                if (hasAct && hasSpr)
                    plan.Add(Category, r.Key, new[] { new RoCatalogPair(act, spr, Role) }, new[] { RoCatalogValue.Of("resource", r.Value) });
                else if (hasAct) plan.Gap(RoIssueCode.CatalogOrphanAct, Category, act.Canonical, $"item resource {r.Value}: the ACT has no SPR");
                else if (hasSpr) plan.Gap(RoIssueCode.CatalogOrphanSpr, Category, spr.Canonical, $"item resource {r.Value}: the SPR has no ACT");
                else plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, spr.Canonical, $"item resource {r.Value} has no sprite in {prefix}");
            }
            foreach (string name in invalid)
                plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, prefix + name, $"item resource {name} is not a valid file name");

            // Halves of a pair in the folder that no resource names (whole pairs may be skill icons: that planner's).
            RoStaticFiles.ReportOrphans(files, claimed, Category, plan);
        }

        static void Collect(string name, SortedDictionary<string, string> resources, SortedSet<string> invalid)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            string key = KeyOf(name);
            if (key == null) invalid.Add(name);
            else if (!resources.ContainsKey(key)) resources.Add(key, name);
        }
    }

    /// <summary>
    /// Skill icons (SPEC-0008 FR-7, FR-10; PLAN-0008 4.3.8): one record per skill id whose lower-case <c>SKID</c> constant
    /// has a file in <c>아이템/</c>, keyed by the id. Monster skills (<c>NPC_*</c>) have none: one summary gap, not one each.
    /// </summary>
    public sealed class RoSkillIconPlanner : IRoCatalogPlanner
    {
        public const string MonsterSkillPrefix = "NPC_";
        public const string Role = "icon";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.SkillIcons;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Skills;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var skills = ctx.Tables.Skills;
            if (skills == null) return;
            string prefix = RoFileNames.SpriteFolder(RoGroundItemPlanner.Folder);
            var files = ctx.Files(prefix);
            int monsterSkills = 0;

            foreach (int id in skills.SkillIds.OrderBy(i => i))
            {
                var constants = skills.ConstantsOf(id);
                string found = null;
                RoCatalogPair pair = default;
                RoPath firstMissing = default;
                foreach (string constant in constants)
                {
                    if (!RoStaticFiles.TryFiles(RoGroundItemPlanner.Folder, RoSkillTable.IconName(constant), out var act, out var spr)) continue;
                    if (files.Contains(spr))
                    {
                        found = constant;
                        pair = files.Contains(act) ? new RoCatalogPair(act, spr, Role) : new RoCatalogPair(default, spr, Role);
                        break;
                    }
                    if (firstMissing.IsEmpty && !constant.StartsWith(MonsterSkillPrefix, StringComparison.Ordinal)) firstMissing = spr;
                }

                if (found != null)
                    plan.Add(Category, id.ToString(CultureInfo.InvariantCulture), new[] { pair },
                        new[] { RoCatalogValue.Of("skillId", id), RoCatalogValue.Of("constant", found) });
                else if (firstMissing.IsEmpty) monsterSkills++;
                else plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, firstMissing.Canonical,
                    $"skill {id} ({string.Join(", ", constants)}) has no icon in {prefix}");
            }

            if (monsterSkills > 0)
                plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, MonsterSkillPrefix + "*",
                    $"{monsterSkills} monster skills ({MonsterSkillPrefix}*) have no icon; they are not listed one by one");
        }
    }

    /// <summary>
    /// Effects (SPEC-0008 FR-6, FR-10; PLAN-0008 4.3.9, OQ-5): one record per sprite in <c>이팩트/</c> by its name relative
    /// to the folder; <c>emotion</c> also carries the emote ids (action = <c>ET_*</c> id).
    /// </summary>
    public sealed class RoEffectPlanner : IRoCatalogPlanner
    {
        public const string Folder = "이팩트";
        public const string EmoteSet = "emotion";
        /// <summary>The value name prefix of an emote id on the emote set record (<c>emote:ET_SURPRISE</c> = 0).</summary>
        public const string EmoteValuePrefix = "emote:";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Effects;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Emotes;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan) => throw new NotImplementedException("pending T4.5");
    }

    /// <summary>File helpers shared by the static planners (PLAN-0008 4.3.8, 4.3.9).</summary>
    static class RoStaticFiles
    {
        /// <summary>The ACT and SPR of <paramref name="name"/> in <paramref name="folder"/>; false when it is not a valid file name.</summary>
        public static bool TryFiles(string folder, string name, out RoPath act, out RoPath spr)
        {
            act = spr = default;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (!RoPath.TryParse(RoFileNames.SpriteRoot + folder + "/" + name + RoFileNames.SprExtension, out spr)) return false;
            act = spr.WithExtension(RoFileNames.ActExtension);
            return true;
        }

        /// <summary>An ACT without its SPR, or an SPR without its ACT, that nothing claimed.</summary>
        public static void ReportOrphans(RoCatalogContext.FileSet files, HashSet<RoPath> claimed, SpriteCatalogCategory category, RoCatalogPlan.Builder plan)
        {
            foreach (var path in files.All)
            {
                if (claimed.Contains(path)) continue;
                if (path.Extension == RoFileNames.ActExtension && !files.Contains(path.WithExtension(RoFileNames.SprExtension)))
                    plan.Gap(RoIssueCode.CatalogOrphanAct, category, path.Canonical, "the ACT has no SPR");
                else if (path.Extension == RoFileNames.SprExtension && !files.Contains(path.WithExtension(RoFileNames.ActExtension)))
                    plan.Gap(RoIssueCode.CatalogOrphanSpr, category, path.Canonical, "the SPR has no ACT");
            }
        }
    }
}
