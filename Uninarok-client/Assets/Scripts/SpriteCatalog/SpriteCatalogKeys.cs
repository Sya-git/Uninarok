using System;

namespace Uninarok.Catalog
{
    /// <summary>
    /// The fixed Addressables keys of the assets nothing else references: the category indices, the item table and the class
    /// table (PLAN-0008 3, 4.5). The importer and the run time share them.
    /// </summary>
    public static class SpriteCatalogKeys
    {
        public const string Root = "Assets/Art/Imported/catalog/";
        public const string IndexFile = "_index.asset";

        /// <summary><c>Assets/Art/Imported/catalog/&lt;category&gt;/_index.asset</c>; <see cref="SpriteCatalogCategory.Palettes"/> has no index.</summary>
        public static string Index(SpriteCatalogCategory category) => Root + Folder(category) + "/" + IndexFile;

        /// <summary>The folder name of a category under <see cref="Root"/> (<c>ground-items</c>); palettes have none (they stay in SPEC-0007's <c>palette/</c>).</summary>
        public static string Folder(SpriteCatalogCategory category)
        {
            switch (category)
            {
                case SpriteCatalogCategory.Monsters: return "monsters";
                case SpriteCatalogCategory.Npcs: return "npcs";
                case SpriteCatalogCategory.Homunculi: return "homunculi";
                case SpriteCatalogCategory.Bodies: return "bodies";
                case SpriteCatalogCategory.Heads: return "heads";
                case SpriteCatalogCategory.Headgears: return "headgears";
                case SpriteCatalogCategory.Garments: return "garments";
                case SpriteCatalogCategory.Weapons: return "weapons";
                case SpriteCatalogCategory.Shields: return "shields";
                case SpriteCatalogCategory.GroundItems: return "ground-items";
                case SpriteCatalogCategory.SkillIcons: return "skill-icons";
                case SpriteCatalogCategory.Effects: return "effects";
                default: throw new ArgumentException($"{category} has no catalog folder", nameof(category));
            }
        }

        /// <summary>The <see cref="ItemSpriteTable"/>.</summary>
        public static string Items() => Root + "items/" + IndexFile;

        /// <summary>The <see cref="ClassTable"/>.</summary>
        public static string Classes() => Root + "classes/" + IndexFile;
    }
}
