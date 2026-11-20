using System.Collections.Generic;

namespace Uninarok.RoImport.Core.Catalog.Tables
{
    /// <summary>
    /// The view-id tables of equipment (PLAN-0008 4.2; sprite-catalog.md §1, FR-4): headgears (<c>ACCESSORY_IDs</c>,
    /// <c>AccNameTable</c>), garments (<c>SPRITE_ROBE_IDs</c>, <c>RobeNameTable</c>; not <c>_Eng</c>) and weapons
    /// (<c>Weapon_IDs</c>, <c>WeaponNameTable</c>, <c>Expansion_Weapon_IDs</c>). Each name table is read with the
    /// constants of its id file as imports.
    /// </summary>
    public sealed class RoViewIdTables
    {
        public const string AccessoryIdFile = "accessoryid.lub";
        public const string AccNameFile = "accname.lub";
        public const string RobeIdFile = "spriterobeid.lub";
        public const string RobeNameFile = "spriterobename.lub";
        public const string WeaponTableFile = "weapontable.lub";

        RoViewIdTables() { }

        /// <summary><c>ACCESSORY_IDs</c>: <c>ACCESSORY_GOGGLES</c> → 1.</summary>
        public IReadOnlyDictionary<string, int> HeadgearConstants { get; private set; }
        /// <summary><c>AccNameTable</c>: headgear view id → file stem with its leading underscore (1 → <c>_고글</c>).</summary>
        public IReadOnlyDictionary<int, string> HeadgearStems { get; private set; }
        /// <summary><c>SPRITE_ROBE_IDs</c>: <c>ROBE_WINGS</c> → 1.</summary>
        public IReadOnlyDictionary<string, int> GarmentConstants { get; private set; }
        /// <summary><c>RobeNameTable</c>: garment view id → folder name (1 → <c>천사날개</c>).</summary>
        public IReadOnlyDictionary<int, string> GarmentFolders { get; private set; }
        /// <summary><c>Weapon_IDs</c>: <c>WEAPONTYPE_SHORTSWORD</c> → 1.</summary>
        public IReadOnlyDictionary<string, int> WeaponConstants { get; private set; }
        /// <summary><c>WeaponNameTable</c>: weapon view id → file suffix (1 → <c>_단검</c>, or an item id such as <c>_1207</c>).</summary>
        public IReadOnlyDictionary<int, string> WeaponSuffixes { get; private set; }
        /// <summary><c>Expansion_Weapon_IDs</c>: specific weapon view id → base type view id (Main Gauche → short sword).</summary>
        public IReadOnlyDictionary<int, int> WeaponBaseTypes { get; private set; }

        public static bool TryRead(string accessoryIds, string accNames, string robeIds, string robeNames, string weaponTable,
                                   out RoViewIdTables tables, out string reason)
        {
            tables = null;
            if (!RoLuaTables.TryRead(accessoryIds, AccessoryIdFile, null, null, out var accIds, out reason)) return false;
            if (!RoLuaTables.TryTable(accIds, "ACCESSORY_IDs", AccessoryIdFile, out var accIdTable, out reason)) return false;
            if (!RoLuaTables.TryRead(accNames, AccNameFile, null, RoLuaTables.Imports(accIds), out var accNameChunk, out reason)) return false;
            if (!RoLuaTables.TryTable(accNameChunk, "AccNameTable", AccNameFile, out var accNameTable, out reason)) return false;

            if (!RoLuaTables.TryRead(robeIds, RobeIdFile, null, null, out var robeIdChunk, out reason)) return false;
            if (!RoLuaTables.TryTable(robeIdChunk, "SPRITE_ROBE_IDs", RobeIdFile, out var robeIdTable, out reason)) return false;
            if (!RoLuaTables.TryRead(robeNames, RobeNameFile, null, RoLuaTables.Imports(robeIdChunk), out var robeNameChunk, out reason)) return false;
            if (!RoLuaTables.TryTable(robeNameChunk, "RobeNameTable", RobeNameFile, out var robeNameTable, out reason)) return false;

            if (!RoLuaTables.TryRead(weaponTable, WeaponTableFile, null, null, out var weapons, out reason)) return false;
            if (!RoLuaTables.TryTable(weapons, "Weapon_IDs", WeaponTableFile, out var weaponIds, out reason)) return false;
            if (!RoLuaTables.TryTable(weapons, "WeaponNameTable", WeaponTableFile, out var weaponNames, out reason)) return false;
            if (!RoLuaTables.TryTable(weapons, "Expansion_Weapon_IDs", WeaponTableFile, out var expansion, out reason)) return false;

            tables = new RoViewIdTables
            {
                HeadgearConstants = RoLuaTables.Constants(accIdTable),
                HeadgearStems = RoLuaTables.Names(accNameTable),
                GarmentConstants = RoLuaTables.Constants(robeIdTable),
                GarmentFolders = RoLuaTables.Names(robeNameTable),
                WeaponConstants = RoLuaTables.Constants(weaponIds),
                WeaponSuffixes = RoLuaTables.Names(weaponNames),
                WeaponBaseTypes = RoLuaTables.IntegerMap(expansion),
            };
            return true;
        }
    }
}
