using System;
using Uninarok.RoImport.Core.Paths;

namespace Uninarok.RoImport.Core.Catalog.Planning
{
    /// <summary>
    /// The file-name builders of the planners (PLAN-0008 4.3), so no planner concatenates paths by hand. Folders are
    /// relative to <c>sprite/</c> or <c>palette/</c>; the results are canonical <see cref="RoPath"/>s.
    /// </summary>
    public static class RoFileNames
    {
        public const string SpriteRoot = "sprite/";
        public const string PaletteRoot = "palette/";
        public const string ActExtension = ".act";
        public const string SprExtension = ".spr";
        public const string PalExtension = ".pal";

        /// <summary><c>sprite/&lt;folder&gt;/&lt;stem&gt;.spr</c>.</summary>
        public static RoPath Spr(string folder, string stem) => RoPath.Parse(SpriteRoot + Join(folder, stem) + SprExtension);

        /// <summary><c>sprite/&lt;folder&gt;/&lt;stem&gt;.act</c>.</summary>
        public static RoPath Act(string folder, string stem) => RoPath.Parse(SpriteRoot + Join(folder, stem) + ActExtension);

        /// <summary><c>palette/&lt;folder&gt;/&lt;stem&gt;.pal</c>.</summary>
        public static RoPath Pal(string folder, string stem) => RoPath.Parse(PaletteRoot + Join(folder, stem) + PalExtension);

        /// <summary>The ACT and SPR of one stem in one folder (the SPEC-0007 default pairing).</summary>
        public static RoCatalogPair Pair(string folder, string stem, string role) => new RoCatalogPair(Act(folder, stem), Spr(folder, stem), role);

        /// <summary>An SPR only: a static sprite.</summary>
        public static RoCatalogPair Still(string folder, string stem, string role) => new RoCatalogPair(default, Spr(folder, stem), role);

        /// <summary>The canonical prefix of a sprite folder (<c>sprite/몬스터/</c>).</summary>
        public static string SpriteFolder(string folder) => Prefix(SpriteRoot + (folder ?? string.Empty));

        /// <summary>The canonical prefix of a palette folder (<c>palette/몸/</c>).</summary>
        public static string PaletteFolder(string folder) => Prefix(PaletteRoot + (folder ?? string.Empty));

        /// <summary>A folder prefix in canonical form, ending with <c>/</c> ("" stays "").</summary>
        public static string Prefix(string folderPrefix)
        {
            if (string.IsNullOrWhiteSpace(folderPrefix)) return string.Empty;
            string trimmed = folderPrefix.Trim().TrimEnd('/', '\\');
            return trimmed.Length == 0 ? string.Empty : RoPath.Parse(trimmed).Canonical + "/";
        }

        /// <summary>The path relative to <paramref name="prefix"/> without extension (<c>sprite/npc/a/b.spr</c>, <c>sprite/npc/</c> → <c>a/b</c>).</summary>
        public static string StemUnder(RoPath path, string prefix)
        {
            string c = path.Canonical;
            if (!c.StartsWith(prefix, StringComparison.Ordinal)) throw new ArgumentException($"{c} is not under {prefix}", nameof(path));
            string rest = c.Substring(prefix.Length);
            string ext = path.Extension;
            return ext.Length == 0 ? rest : rest.Substring(0, rest.Length - ext.Length);
        }

        static string Join(string folder, string stem)
        {
            if (string.IsNullOrEmpty(stem)) throw new ArgumentException("a file name needs a stem", nameof(stem));
            folder = (folder ?? string.Empty).Trim('/', '\\');
            return folder.Length == 0 ? stem : folder + "/" + stem;
        }
    }
}
