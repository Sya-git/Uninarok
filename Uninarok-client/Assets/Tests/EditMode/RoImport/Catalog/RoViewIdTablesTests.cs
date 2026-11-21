using NUnit.Framework;
using Uninarok.RoImport.Core.Catalog.Tables;

namespace Uninarok.RoImport.Tests.Catalog
{
    // TASKS-0008 T2.5 (FR-4; PLAN-0008 4.2): headgear, garment and weapon view ids.
    public class RoViewIdTablesTests
    {
        const string AccIds = "ACCESSORY_IDs = { ACCESSORY_GOGGLES = 1, ACCESSORY_HAT = 16 }";
        const string AccNames = "AccNameTable = { [ACCESSORY_IDs.ACCESSORY_GOGGLES] = \"_고글\", [ACCESSORY_IDs.ACCESSORY_HAT] = \"_햇\" }";
        const string RobeIds = "SPRITE_ROBE_IDs = { ROBE_WINGS = 1 }";
        const string RobeNames = "RobeNameTable = { [SPRITE_ROBE_IDs.ROBE_WINGS] = \"천사날개\" }\nRobeNameTable_Eng = { [SPRITE_ROBE_IDs.ROBE_WINGS] = \"Angel Wings\" }";
        const string Weapons =
            "Weapon_IDs = { WEAPONTYPE_NONE = 0, WEAPONTYPE_SHORTSWORD = 1, WEAPONTYPE_Main_Gauche = 51 }\n" +
            "WeaponNameTable = { [Weapon_IDs.WEAPONTYPE_SHORTSWORD] = \"_단검\", [Weapon_IDs.WEAPONTYPE_Main_Gauche] = \"_1207\" }\n" +
            "Expansion_Weapon_IDs = { [Weapon_IDs.WEAPONTYPE_Main_Gauche] = Weapon_IDs.WEAPONTYPE_SHORTSWORD }";

        [Test]
        public void ReadsEachNameTableWithItsConstants()
        {
            Assert.That(RoViewIdTables.TryRead(AccIds, AccNames, RobeIds, RobeNames, Weapons, out var t, out string reason), Is.True, reason);
            Assert.That(t.HeadgearStems[16], Is.EqualTo("_햇"));
            Assert.That(t.GarmentFolders[1], Is.EqualTo("천사날개"), "RobeNameTable, not _Eng");
            Assert.That(t.WeaponSuffixes[51], Is.EqualTo("_1207"));
            Assert.That(t.WeaponBaseTypes[51], Is.EqualTo(1));
            Assert.That(t.WeaponConstants["WEAPONTYPE_NONE"], Is.EqualTo(0));
        }

        [Test]
        public void AMissingTableIsNamed()
        {
            Assert.That(RoViewIdTables.TryRead(AccIds, AccNames, RobeIds, RobeNames, "Weapon_IDs = {}", out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain(RoViewIdTables.WeaponTableFile).And.Contain("WeaponNameTable"));
        }

        [Test, Category(CatalogTestFiles.Category)]
        public void ReadsTheClientTables()
        {
            bool ok = RoViewIdTables.TryRead(
                CatalogTestFiles.TableLua("datainfo/accessoryid.lub"), CatalogTestFiles.TableLua("datainfo/accname.lub"),
                CatalogTestFiles.TableLua("datainfo/spriterobeid.lub"), CatalogTestFiles.TableLua("datainfo/spriterobename.lub"),
                CatalogTestFiles.TableLua("datainfo/weapontable.lub"), out var t, out string reason);
            Assert.That(ok, Is.True, reason);

            Assert.That(t.HeadgearConstants.Count, Is.EqualTo(2859));
            Assert.That(t.HeadgearStems[1], Is.EqualTo("_고글"));
            Assert.That(t.HeadgearStems[16], Is.EqualTo("_햇"), "item 2220 is headgear view id 16");
            Assert.That(t.GarmentConstants.Count, Is.EqualTo(330), "ids 1 to 331, 168 unused (T2.5)");
            Assert.That(t.GarmentFolders[1], Is.EqualTo("천사날개"));
            Assert.That(t.WeaponConstants.Count, Is.EqualTo(103));
            Assert.That(t.WeaponSuffixes[1], Is.EqualTo("_단검"));
            Assert.That(t.WeaponBaseTypes.Count, Is.EqualTo(72));
            int mainGauche = t.WeaponConstants["WEAPONTYPE_Main_Gauche"];
            Assert.That(t.WeaponBaseTypes[mainGauche], Is.EqualTo(t.WeaponConstants["WEAPONTYPE_SHORTSWORD"]));
            TestContext.WriteLine($"headgear ids {t.HeadgearConstants.Count} (names {t.HeadgearStems.Count}), garment ids {t.GarmentConstants.Count} (names {t.GarmentFolders.Count}), " +
                                  $"weapon ids {t.WeaponConstants.Count} (suffixes {t.WeaponSuffixes.Count}, base types {t.WeaponBaseTypes.Count})");
        }
    }
}
