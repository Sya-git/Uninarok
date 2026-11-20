using System;

namespace Uninarok.Catalog
{
    /// <summary>
    /// The fixed Addressables keys of the assets nothing else references: the category indices, the item table and the class
    /// table (PLAN-0008 4.5). The importer and the run time share them.
    /// </summary>
    public static class SpriteCatalogKeys
    {
        public const string Root = "Assets/Art/Imported/catalog/";

        /// <summary><c>Assets/Art/Imported/catalog/&lt;category&gt;/_index.asset</c>; <see cref="SpriteCatalogCategory.Palettes"/> has no index.</summary>
        public static string Index(SpriteCatalogCategory category) => throw new NotImplementedException("pending T3.4");

        /// <summary>The folder name of a category (<c>ground-items</c>).</summary>
        public static string Folder(SpriteCatalogCategory category) => throw new NotImplementedException("pending T3.4");

        public static string Items() => throw new NotImplementedException("pending T3.4");

        public static string Classes() => throw new NotImplementedException("pending T3.4");
    }
}
