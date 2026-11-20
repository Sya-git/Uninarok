using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Uninarok.Catalog;
using Uninarok.Networking.Data;
using Uninarok.RoImport.Core.Catalog.Tables;
using Uninarok.RoImport.Core.Paths;
using Uninarok.RoImport.Core.Results;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// Bodies (SPEC-0008 FR-2, FR-10; PLAN-0008 4.3.2, 4.3.11): one record per base class (<see cref="RoCatalogKeys.Body"/>),
    /// one pair per (variant, sex) from the rows of <c>class-bodies.csv</c>, costumes from <c>costume_&lt;n&gt;/</c>, palettes by
    /// colour number per variant. Only the palettes of its own records (4.3.10).
    /// </summary>
    public sealed class RoBodyPlanner : IRoCatalogPlanner
    {
        public const int Costumes = 4;

        public SpriteCatalogCategory Category => SpriteCatalogCategory.Bodies;
        public RoCatalogNeeds Needs => RoCatalogNeeds.Own;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var own = ctx.Tables.Own;
            if (own == null) return;
            var claimed = new HashSet<RoPath>();
            var human = new Race(ctx, SpriteRace.Human, "인간족/몸통", "몸");
            var doram = new Race(ctx, SpriteRace.Doram, "도람족/몸통", "도람족/body");

            foreach (var group in own.Bodies.GroupBy(b => b.BaseJob).OrderBy(g => (int)g.Key))
            {
                var pairs = new List<RoCatalogPair>();
                var values = new List<RoCatalogValue> { RoCatalogValue.Of("baseJob", group.Key.ToString()) };
                foreach (var row in group)
                {
                    var race = row.Doram ? doram : human;
                    var kind = KindOf(row.Kind);
                    bool any = false;
                    foreach (var sex in RoPlayerFiles.Sexes)
                    {
                        string marker = RoCatalogRoles.SexMarker(sex);
                        string stem = row.BodyName + "_" + marker;
                        any |= Variant(race, marker + "/" + stem, string.Empty, row.Job, sex, kind, row.BodyName, stem, string.Empty, pairs, values, claimed, plan);
                        for (int n = 1; n <= Costumes; n++)
                        {
                            string costume = "costume_" + n.ToString(CultureInfo.InvariantCulture);
                            string suffix = "_" + n.ToString(CultureInfo.InvariantCulture);
                            any |= Variant(race, marker + "/" + costume + "/" + stem + suffix, costume, row.Job, sex,
                                (BodyVariantKind)((int)BodyVariantKind.Costume1 + n - 1), row.BodyName, stem, suffix, pairs, values, claimed, plan);
                        }
                    }
                    if (!any)
                        plan.Gap(RoIssueCode.CatalogNameWithoutFile, Category, race.Prefix + row.BodyName,
                            $"the class table row {row.BodyName} ({row.Job}, {(row.Doram ? "Doram" : "Human")}) has no body file");
                }
                if (pairs.Count > 0) plan.Add(Category, RoCatalogKeys.Body(group.Key), pairs, values);
            }

            foreach (var race in new[] { human, doram })
                ReportUnclaimed(race, claimed, plan);
        }

        bool Variant(Race race, string relative, string costumeFolder, PlayerJob job, AccountSex sex, BodyVariantKind kind, string bodyName,
                     string paletteStem, string paletteSuffix, List<RoCatalogPair> pairs, List<RoCatalogValue> values, HashSet<RoPath> claimed,
                     RoCatalogPlan.Builder plan)
        {
            if (!RoPath.TryParse(race.Prefix + relative + RoFileNames.SprExtension, out var spr)) return false;
            var act = spr.WithExtension(RoFileNames.ActExtension);
            bool hasAct = race.Files.Contains(act), hasSpr = race.Files.Contains(spr);
            if (!hasAct && !hasSpr) return false;
            claimed.Add(act);
            claimed.Add(spr);
            if (!hasAct || !hasSpr)
            {
                if (hasAct) plan.Gap(RoIssueCode.CatalogOrphanAct, Category, act.Canonical, $"{bodyName}: the ACT has no SPR");
                else plan.Gap(RoIssueCode.CatalogOrphanSpr, Category, spr.Canonical, $"{bodyName}: the SPR has no ACT");
                return true;
            }
            string role = RoCatalogRoles.Body(job, sex, race.Kind, kind);
            pairs.Add(new RoCatalogPair(act, spr, role));
            values.Add(RoCatalogValue.Of(RoCatalogRoles.Name(role), bodyName));
            foreach (var p in race.Palettes.Of(costumeFolder, paletteStem, paletteSuffix))
                values.Add(RoCatalogValue.Of(RoCatalogRoles.Palette(role, p.colour), p.path.Canonical));
            return true;
        }

        // Body files no row claimed: the usual pattern is a file without a name (bodies of classes not in the table), anything
        // else a stray (weapons in the body folder, the _여 folder, names with quotes; OQ-6).
        void ReportUnclaimed(Race race, HashSet<RoPath> claimed, RoCatalogPlan.Builder plan)
        {
            foreach (var path in RoPlayerFiles.Stems(race.Files, claimed))
            {
                string rel = RoFileNames.StemUnder(path, race.Prefix);
                var code = RoPlayerFiles.IsBodyPattern(rel) ? RoIssueCode.CatalogFileWithoutName : RoIssueCode.CatalogStrayFile;
                plan.Gap(code, Category, path.Canonical, code == RoIssueCode.CatalogFileWithoutName
                    ? "no class table row names this body" : "the file breaks the body naming pattern");
            }
        }

        static BodyVariantKind KindOf(string kind) =>
            Enum.TryParse(kind, out BodyVariantKind k) ? k : BodyVariantKind.Base;

        sealed class Race
        {
            public Race(RoCatalogContext ctx, SpriteRace kind, string spriteFolder, string paletteFolder)
            {
                Kind = kind;
                Prefix = RoFileNames.SpriteFolder(spriteFolder);
                Files = ctx.Files(Prefix);
                Palettes = new RoPaletteIndex(ctx, paletteFolder);
            }

            public SpriteRace Kind { get; }
            public string Prefix { get; }
            public RoCatalogContext.FileSet Files { get; }
            public RoPaletteIndex Palettes { get; }
        }
    }

    /// <summary>
    /// Heads (SPEC-0008 FR-3, FR-10; PLAN-0008 4.3.3, 4.3.11): one record per hair style number (<see cref="RoCatalogKeys.Head"/>),
    /// one pair per (sex, race), hair palettes by colour number.
    /// </summary>
    public sealed class RoHeadPlanner : IRoCatalogPlanner
    {
        public SpriteCatalogCategory Category => SpriteCatalogCategory.Heads;
        public RoCatalogNeeds Needs => RoCatalogNeeds.None;

        public void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan)
        {
            var styles = new SortedDictionary<int, (List<RoCatalogPair> pairs, List<RoCatalogValue> values)>();
            foreach (var (race, spriteFolder, paletteFolder) in new[] { (SpriteRace.Human, "인간족/머리통", "머리"), (SpriteRace.Doram, "도람족/머리통", "도람족/머리") })
            {
                string prefix = RoFileNames.SpriteFolder(spriteFolder);
                var files = ctx.Files(prefix);
                var palettes = new RoPaletteIndex(ctx, paletteFolder);
                var claimed = new HashSet<RoPath>();
                foreach (var sex in RoPlayerFiles.Sexes)
                {
                    string marker = RoCatalogRoles.SexMarker(sex);
                    string sexPrefix = prefix + marker + "/";
                    foreach (var spr in files.All.Where(p => p.Extension == RoFileNames.SprExtension && p.Canonical.StartsWith(sexPrefix, StringComparison.Ordinal)))
                    {
                        string stem = RoFileNames.StemUnder(spr, sexPrefix);
                        if (!stem.EndsWith("_" + marker, StringComparison.Ordinal)
                            || !int.TryParse(stem.Substring(0, stem.Length - marker.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int style)) continue;
                        var act = spr.WithExtension(RoFileNames.ActExtension);
                        claimed.Add(spr);
                        claimed.Add(act);
                        if (!files.Contains(act))
                        {
                            plan.Gap(RoIssueCode.CatalogOrphanSpr, Category, spr.Canonical, "the SPR has no ACT");
                            continue;
                        }
                        if (!styles.TryGetValue(style, out var record))
                        {
                            record = (new List<RoCatalogPair>(), new List<RoCatalogValue> { RoCatalogValue.Of("style", style) });
                            styles.Add(style, record);
                        }
                        string role = RoCatalogRoles.Head(sex, race);
                        record.pairs.Add(new RoCatalogPair(act, spr, role));
                        foreach (var p in palettes.Of(string.Empty, "머리" + style.ToString(CultureInfo.InvariantCulture) + "_" + marker, string.Empty))
                            record.values.Add(RoCatalogValue.Of(RoCatalogRoles.Palette(role, p.colour), p.path.Canonical));
                    }
                }
                foreach (var path in RoPlayerFiles.Stems(files, claimed))
                    plan.Gap(RoIssueCode.CatalogStrayFile, Category, path.Canonical, "not a <style>_<sex> head file");
            }
            foreach (var s in styles) plan.Add(Category, RoCatalogKeys.Head(s.Key), s.Value.pairs, s.Value.values);
        }
    }

    /// <summary>Helpers of the body and head planners.</summary>
    static class RoPlayerFiles
    {
        public static readonly AccountSex[] Sexes = { AccountSex.SEX_MALE, AccountSex.SEX_FEMALE };

        /// <summary>One path per unclaimed sprite stem: the SPR, or the ACT when the stem has no SPR.</summary>
        public static IEnumerable<RoPath> Stems(RoCatalogContext.FileSet files, HashSet<RoPath> claimed)
        {
            foreach (var path in files.All)
            {
                if (claimed.Contains(path)) continue;
                if (path.Extension == RoFileNames.SprExtension) yield return path;
                else if (path.Extension == RoFileNames.ActExtension && !files.Contains(path.WithExtension(RoFileNames.SprExtension))) yield return path;
            }
        }

        /// <summary><c>&lt;sex&gt;/&lt;name&gt;_&lt;sex&gt;</c> or <c>&lt;sex&gt;/costume_n/&lt;name&gt;_&lt;sex&gt;_n</c>, with no quote.</summary>
        public static bool IsBodyPattern(string relative)
        {
            if (relative.IndexOf('"') >= 0 || relative.IndexOf('\'') >= 0) return false;
            var parts = relative.Split('/');
            if (parts.Length < 2 || (parts[0] != "남" && parts[0] != "여")) return false;
            string marker = "_" + parts[0];
            if (parts.Length == 2) return parts[1].EndsWith(marker, StringComparison.Ordinal) && parts[1].Length > marker.Length;
            if (parts.Length == 3 && parts[1].StartsWith("costume_", StringComparison.Ordinal))
                return parts[2].EndsWith(marker + "_" + parts[1].Substring("costume_".Length), StringComparison.Ordinal);
            return false;
        }
    }

    /// <summary>
    /// The palettes of one palette folder, by the stem they belong to: <c>&lt;stem&gt;_&lt;colour&gt;[&lt;suffix&gt;].pal</c>
    /// (<c>기사_남_1.pal</c>, <c>costume_1/기사_남_0_1.pal</c>), in colour order (PLAN-0008 4.3.2, 4.3.3).
    /// </summary>
    sealed class RoPaletteIndex
    {
        readonly RoCatalogContext _ctx;
        readonly string _folder;
        readonly Dictionary<string, Dictionary<string, List<(int colour, RoPath path)>>> _bySubfolder =
            new Dictionary<string, Dictionary<string, List<(int, RoPath)>>>(StringComparer.Ordinal);

        public RoPaletteIndex(RoCatalogContext ctx, string folder)
        {
            _ctx = ctx;
            _folder = folder;
        }

        /// <summary>The palettes of <paramref name="stem"/> in <paramref name="subfolder"/> ("" for the folder itself), with the file suffix after the colour.</summary>
        public IReadOnlyList<(int colour, RoPath path)> Of(string subfolder, string stem, string suffix)
        {
            string key = subfolder ?? string.Empty;
            if (!_bySubfolder.TryGetValue(key, out var index))
            {
                index = Build(key);
                _bySubfolder.Add(key, index);
            }
            string lookup = RoPath.TryParse(stem + "_0" + suffix, out var p) ? p.Canonical : null;
            if (lookup == null) return Array.Empty<(int, RoPath)>();
            string canonicalStem = lookup.Substring(0, lookup.Length - 2 - suffix.Length) + "|" + suffix;
            return index.TryGetValue(canonicalStem, out var list) ? list : (IReadOnlyList<(int, RoPath)>)Array.Empty<(int, RoPath)>();
        }

        Dictionary<string, List<(int, RoPath)>> Build(string subfolder)
        {
            string prefix = RoFileNames.PaletteFolder(subfolder.Length == 0 ? _folder : _folder + "/" + subfolder);
            var index = new Dictionary<string, List<(int, RoPath)>>(StringComparer.Ordinal);
            string suffix = subfolder.StartsWith("costume_", StringComparison.Ordinal) ? "_" + subfolder.Substring("costume_".Length) : string.Empty;
            foreach (var path in _ctx.Files(prefix).All)
            {
                if (path.Extension != RoFileNames.PalExtension) continue;
                string rest = RoFileNames.StemUnder(path, prefix);
                if (rest.IndexOf('/') >= 0) continue;
                if (suffix.Length > 0)
                {
                    if (!rest.EndsWith(suffix, StringComparison.Ordinal)) continue;
                    rest = rest.Substring(0, rest.Length - suffix.Length);
                }
                int underscore = rest.LastIndexOf('_');
                if (underscore <= 0 || !int.TryParse(rest.Substring(underscore + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int colour)) continue;
                string key = rest.Substring(0, underscore) + "|" + suffix;
                if (!index.TryGetValue(key, out var list)) index.Add(key, list = new List<(int, RoPath)>());
                list.Add((colour, path));
            }
            foreach (var list in index.Values) list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            return index;
        }
    }
}
