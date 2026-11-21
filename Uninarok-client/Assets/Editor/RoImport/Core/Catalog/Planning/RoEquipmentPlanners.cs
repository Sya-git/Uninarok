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
    /// The class-prefixed files of the weapon and shield folders (PLAN-0008 4.3.11): every <c>&lt;BodyName&gt;_&lt;sex&gt;&lt;rest&gt;</c>
    /// file whatever its folder, grouped by (body, sex) in class table order, with the rests each group has as a full pair.
    /// </summary>
    sealed class RoClassFiles
    {
        readonly List<RoCatalogContext.FileSet> _sets = new List<RoCatalogContext.FileSet>();
        readonly HashSet<string> _excluded = new HashSet<string>(StringComparer.Ordinal);

        public List<Group> Groups { get; } = new List<Group>();
        public IEnumerable<string> Rests => Groups.SelectMany(g => g.Rests).Distinct();

        public static RoClassFiles Collect(RoCatalogContext ctx, IEnumerable<string> folders, IEnumerable<string> excludedSubfolders, RoOwnTables own)
        {
            var result = new RoClassFiles();
            var bodies = own.Bodies.Select(b => (canonical: b.BodyName.ToLowerInvariant(), body: b))
                .GroupBy(b => b.canonical).Select(g => g.First()).OrderByDescending(b => b.canonical.Length).ToList();
            var groups = new Dictionary<(string, AccountSex), Group>();
            foreach (string folder in folders)
            {
                string prefix = RoFileNames.SpriteFolder(folder);
                var set = ctx.Files(prefix);
                result._sets.Add(set);
                var excluded = (excludedSubfolders ?? Enumerable.Empty<string>()).Select(e => RoFileNames.SpriteFolder(folder + "/" + e)).ToArray();
                foreach (var spr in set.All)
                {
                    if (excluded.Any(e => spr.Canonical.StartsWith(e, StringComparison.Ordinal)))
                    {
                        result._excluded.Add(spr.Canonical);
                        continue;
                    }
                    if (spr.Extension != RoFileNames.SprExtension) continue;
                    var act = spr.WithExtension(RoFileNames.ActExtension);
                    if (!set.Contains(act)) continue;
                    string stem = spr.FileNameWithoutExtension;
                    foreach (var (canonical, body) in bodies)
                    {
                        AccountSex sex;
                        if (stem.StartsWith(canonical + "_남", StringComparison.Ordinal)) sex = AccountSex.SEX_MALE;
                        else if (stem.StartsWith(canonical + "_여", StringComparison.Ordinal)) sex = AccountSex.SEX_FEMALE;
                        else continue;
                        string rest = stem.Substring(canonical.Length + 2);
                        if (rest.Length > 0 && rest[0] != '_' && !char.IsDigit(rest[0]) && rest[0] != '광') continue;   // 기사_남자… is not 기사_남
                        if (!groups.TryGetValue((canonical, sex), out var group))
                            groups.Add((canonical, sex), group = new Group(body, sex));
                        group.Add(rest, act, spr);
                        break;
                    }
                }
            }
            // Class table order, so the first row of a shared slot is the table's first (D20).
            foreach (var b in own.Bodies)
                foreach (var sex in RoEquipmentFiles.Sexes)
                    if (groups.TryGetValue((b.BodyName.ToLowerInvariant(), sex), out var g) && !result.Groups.Contains(g))
                        result.Groups.Add(g);
            return result;
        }

        /// <summary>Every sprite stem of the folders that no record claimed (excluded subfolders aside).</summary>
        public void ReportUnclaimed(HashSet<RoPath> claimed, SpriteCatalogCategory category, RoCatalogPlan.Builder plan, string message)
        {
            foreach (var set in _sets)
            foreach (var path in set.All)
            {
                if (claimed.Contains(path) || _excluded.Contains(path.Canonical)) continue;
                if (path.Extension == RoFileNames.SprExtension
                    || (path.Extension == RoFileNames.ActExtension && !set.Contains(path.WithExtension(RoFileNames.SprExtension))))
                    plan.Gap(RoIssueCode.CatalogFileWithoutName, category, path.Canonical, message);
            }
        }

        public sealed class Group
        {
            readonly Dictionary<string, (RoPath act, RoPath spr)> _files = new Dictionary<string, (RoPath, RoPath)>(StringComparer.Ordinal);

            public Group(RoClassBody body, AccountSex sex)
            {
                Body = body;
                Sex = sex;
            }

            public RoClassBody Body { get; }
            public AccountSex Sex { get; }
            public IEnumerable<string> Rests => _files.Keys;

            public void Add(string rest, RoPath act, RoPath spr)
            {
                if (!_files.ContainsKey(rest)) _files.Add(rest, (act, spr));
            }

            public bool Has(string rest) => _files.ContainsKey(rest);

            public RoCatalogPair Pair(string rest, string role, HashSet<RoPath> claimed)
            {
                var f = _files[rest];
                claimed.Add(f.act);
                claimed.Add(f.spr);
                return new RoCatalogPair(f.act, f.spr, role);
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
        static readonly string[] TrailSuffixes = { "_검광", "광" };
        static readonly string[] RaceFolders = { "인간족", "도람족" };
        static readonly string[] NotWeaponFolders = { "몸통", "머리통" };

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Weapons;
        public RoCatalogNeeds Needs => RoCatalogNeeds.ViewIds | RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var views = ctx.Tables.ViewIds;
            var own = ctx.Tables.Own;
            if (views == null || own == null) return;
            var weaponFiles = RoClassFiles.Collect(ctx, RaceFolders, NotWeaponFolders, own);
            var claimed = new HashSet<RoPath>();
            var suffixes = new HashSet<string>(views.WeaponSuffixes.Values.Where(s => !string.IsNullOrEmpty(s)).Select(Canonical), StringComparer.Ordinal);
            var shared = new RoSharedSlots();

            foreach (int id in views.WeaponConstants.Values.Distinct().OrderBy(i => i))
            {
                string suffix = views.WeaponSuffixes.TryGetValue(id, out var s) && !string.IsNullOrEmpty(s) ? Canonical(s) : null;
                string baseSuffix = views.WeaponBaseTypes.TryGetValue(id, out int baseId) && baseId != id
                                    && views.WeaponSuffixes.TryGetValue(baseId, out var b) && !string.IsNullOrEmpty(b) ? Canonical(b) : null;
                if (suffix == null && baseSuffix == null)
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, $"WeaponNameTable[{id}]", $"weapon view id {id} has no name");
                    continue;
                }
                var slots = Slots(weaponFiles, own, suffix, baseSuffix, id, claimed, shared);
                if (slots.pairs.Count == 0)
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, $"Weapon_IDs[{id}] ({suffix ?? baseSuffix})", $"weapon view id {id} has no file for any class");
                    continue;
                }
                var values = new List<RoCatalogValue> { RoCatalogValue.Of("viewId", id), RoCatalogValue.Of("name", suffix ?? baseSuffix) };
                values.AddRange(slots.values);
                plan.Add(Category, RoCatalogKeys.Weapon(id), slots.pairs, values);
            }

            // Item-id files (<body>_<sex>_<digits>) that no view id claims (4.3.6).
            var itemIds = new SortedSet<int>();
            foreach (var rest in weaponFiles.Rests)
                if (!suffixes.Contains(rest) && TryItemId(rest, out int item)) itemIds.Add(item);
            foreach (int item in itemIds)
            {
                string rest = "_" + item.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var slots = Slots(weaponFiles, own, rest, null, 0, claimed, shared);
                if (slots.pairs.Count == 0) continue;
                plan.Add(Category, RoCatalogKeys.WeaponItem(item), slots.pairs, new[] { RoCatalogValue.Of("itemId", item) });
            }

            weaponFiles.ReportUnclaimed(claimed, Category, plan, "no weapon view id or item id uses this file");
            shared.Report(Category, plan);
        }

        // The slots of one record: per class file group, the file of `suffix`, else of `baseSuffix` (a fallback for view id `viewId`), with its trail.
        static (List<RoCatalogPair> pairs, List<RoCatalogValue> values) Slots(RoClassFiles files, RoOwnTables own, string suffix, string baseSuffix, int viewId,
                                                                              HashSet<RoPath> claimed, RoSharedSlots shared)
        {
            var slots = new Dictionary<string, (RoClassBody body, (RoCatalogPair weapon, RoCatalogPair? trail, bool fallback) pair)>(StringComparer.Ordinal);
            foreach (var group in files.Groups)
            {
                bool fallback = false;
                string rest = suffix != null && group.Has(suffix) ? suffix : null;
                if (rest == null && baseSuffix != null && group.Has(baseSuffix))
                {
                    rest = baseSuffix;
                    fallback = true;
                }
                if (rest == null) continue;
                var race = group.Body.Doram ? SpriteRace.Doram : SpriteRace.Human;
                string role = RoCatalogRoles.Weapon(group.Body.Job, group.Sex, race);
                var weapon = group.Pair(rest, role, claimed);
                RoCatalogPair? trail = null;
                foreach (string t in TrailSuffixes)
                    if (group.Has(rest + t))
                    {
                        trail = group.Pair(rest + t, RoCatalogRoles.WeaponTrail(group.Body.Job, group.Sex, race), claimed);
                        break;
                    }
                shared.Offer(slots, role, group.Body, (weapon, trail, fallback));
            }
            var pairs = new List<RoCatalogPair>();
            var values = new List<RoCatalogValue>();
            foreach (var slot in slots.Values)
            {
                pairs.Add(slot.pair.weapon);
                if (slot.pair.trail.HasValue) pairs.Add(slot.pair.trail.Value);
                if (slot.pair.fallback) values.Add(RoCatalogValue.Of(RoCatalogRoles.Fallback(slot.pair.weapon.Role), viewId));
            }
            return (pairs, values);
        }

        static bool TryItemId(string rest, out int item)
        {
            item = 0;
            return rest.Length > 1 && rest[0] == '_' && rest.Skip(1).All(char.IsDigit)
                   && int.TryParse(rest.Substring(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out item);
        }

        static string Canonical(string s) => s.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Shields (SPEC-0008 FR-4, FR-10; PLAN-0008 4.3.7): one record per view id of <c>shield-names.csv</c>, plus one per
    /// item id with its own <c>&lt;id&gt;_방패</c> file.
    /// </summary>
    public sealed class RoShieldPlanner : IRoCatalogPlanner
    {
        public const string Folder = "방패";

        public const string ItemSuffix = "_방패";

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Shields;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var own = ctx.Tables.Own;
            if (own == null) return;
            var files = RoClassFiles.Collect(ctx, new[] { Folder }, null, own);
            var claimed = new HashSet<RoPath>();
            var shared = new RoSharedSlots();

            foreach (var shield in own.ShieldNames.OrderBy(s => s.Key))
            {
                var pairs = Slots(files, "_" + shield.Value.Trim().ToLowerInvariant(), claimed, shared);
                if (pairs.Count == 0)
                {
                    plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, $"{RoOwnTables.ShieldNamesFile}[{shield.Key}] ({shield.Value})",
                        $"shield view id {shield.Key} has no file for any class");
                    continue;
                }
                plan.Add(Category, RoCatalogKeys.Shield(shield.Key), pairs, new[] { RoCatalogValue.Of("viewId", shield.Key), RoCatalogValue.Of("name", shield.Value) });
            }

            // Item-id files (<body>_<sex>_<digits>_방패), 4.3.7.
            var items = new SortedSet<int>();
            foreach (string rest in files.Rests)
                if (rest.Length > ItemSuffix.Length + 1 && rest[0] == '_' && rest.EndsWith(ItemSuffix, StringComparison.Ordinal)
                    && int.TryParse(rest.Substring(1, rest.Length - 1 - ItemSuffix.Length), System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out int item))
                    items.Add(item);
            foreach (int item in items)
            {
                var pairs = Slots(files, "_" + item.ToString(System.Globalization.CultureInfo.InvariantCulture) + ItemSuffix, claimed, shared);
                if (pairs.Count > 0) plan.Add(Category, RoCatalogKeys.ShieldItem(item), pairs, new[] { RoCatalogValue.Of("itemId", item) });
            }

            files.ReportUnclaimed(claimed, Category, plan, "no shield name or item id uses this file");
            shared.Report(Category, plan);
        }

        static List<RoCatalogPair> Slots(RoClassFiles files, string rest, HashSet<RoPath> claimed, RoSharedSlots shared)
        {
            var slots = new Dictionary<string, (RoClassBody body, RoCatalogPair pair)>(StringComparer.Ordinal);
            foreach (var group in files.Groups)
            {
                if (!group.Has(rest)) continue;
                string role = RoCatalogRoles.Shield(group.Body.Job, group.Sex, group.Body.Doram ? SpriteRace.Doram : SpriteRace.Human);
                shared.Offer(slots, role, group.Body, group.Pair(rest, role, claimed));
            }
            return slots.Values.Select(s => s.pair).ToList();
        }
    }
}
