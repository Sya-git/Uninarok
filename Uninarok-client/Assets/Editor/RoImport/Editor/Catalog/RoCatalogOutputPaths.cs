using System;
using System.Collections.Generic;
using System.Linq;
using Uninarok.Catalog;
using Uninarok.RoImport.Core.Output;

namespace Uninarok.RoImport.Editor.Catalog
{
    /// <summary>
    /// Where catalog outputs go (SPEC-0008 FR-8; PLAN-0008 §3, 4.6): <c>Art/catalog/&lt;category&gt;/&lt;key&gt;.asset</c> for
    /// entries, <c>…/_index.asset</c> for the index of each category, <c>catalog/items</c> and <c>catalog/classes</c> for the
    /// item and class tables. <c>Art/catalog/</c> is a reserved generated path (PLAN-0002 4.8): an asset found there that the
    /// ledger does not list is treated as generated. With the default roots the paths equal <see cref="SpriteCatalogKeys"/>.
    /// </summary>
    public static class RoCatalogOutputPaths
    {
        public const string CatalogFolder = "catalog";
        public const string ItemsFolder = "items";
        public const string ClassesFolder = "classes";
        public const string IndexName = "_index";

        /// <summary>The twelve category folders (every category but palettes, which stay in SPEC-0007's <c>palette/</c>).</summary>
        public static IReadOnlyList<SpriteCatalogCategory> IndexedCategories =>
            ((SpriteCatalogCategory[])Enum.GetValues(typeof(SpriteCatalogCategory))).Where(c => c != SpriteCatalogCategory.Palettes).ToArray();

        /// <summary>The categories whose records are entry assets (D5); the others are inline in their index.</summary>
        public static bool HasEntryAssets(SpriteCatalogCategory c) =>
            c == SpriteCatalogCategory.Bodies || c == SpriteCatalogCategory.Heads || c == SpriteCatalogCategory.Headgears
            || c == SpriteCatalogCategory.Garments || c == SpriteCatalogCategory.Weapons || c == SpriteCatalogCategory.Shields;

        /// <summary><c>Art/catalog</c>.</summary>
        public static string Root(RoOutputRoots roots) => Art(roots) + "/" + CatalogFolder;

        /// <summary><c>Art/catalog/&lt;category&gt;</c>; palettes have none.</summary>
        public static string Folder(RoOutputRoots roots, SpriteCatalogCategory category) => Root(roots) + "/" + SpriteCatalogKeys.Folder(category);

        /// <summary>The entry asset of one record. The key is the plan's ASCII key (§3); it is not a path and not <c>_index</c>.</summary>
        public static string Entry(RoOutputRoots roots, SpriteCatalogCategory category, string key)
        {
            if (!HasEntryAssets(category)) throw new ArgumentException($"{category} has no entry assets (D5)", nameof(category));
            if (string.IsNullOrEmpty(key) || key == IndexName || key.IndexOfAny(new[] { '/', '\\', '.', ':' }) >= 0 || key.Any(ch => ch > 0x7e || ch < 0x20))
                throw new ArgumentException($"'{key}' is not an entry key", nameof(key));
            return Folder(roots, category) + "/" + key + ".asset";
        }

        public static string Index(RoOutputRoots roots, SpriteCatalogCategory category) => Folder(roots, category) + "/" + IndexName + ".asset";

        public static string Items(RoOutputRoots roots) => Root(roots) + "/" + ItemsFolder + "/" + IndexName + ".asset";

        public static string Classes(RoOutputRoots roots) => Root(roots) + "/" + ClassesFolder + "/" + IndexName + ".asset";

        /// <summary>True for a project path under <c>Art/catalog/</c>: generated, never hand-made (PLAN-0002 4.8).</summary>
        public static bool IsReserved(RoOutputRoots roots, string projectPath) =>
            !string.IsNullOrEmpty(projectPath) && projectPath.Replace('\\', '/').StartsWith(Root(roots) + "/", StringComparison.Ordinal);

        static string Art(RoOutputRoots roots) => (roots ?? throw new ArgumentNullException(nameof(roots))).Art.TrimEnd('/');
    }
}
