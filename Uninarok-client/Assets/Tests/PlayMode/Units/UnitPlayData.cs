using System;
using System.Collections;
using NUnit.Framework;
using Uninarok.Catalog;
using Uninarok.Networking.Data;

namespace Uninarok.Units.PlayTests
{
    /// <summary>The reference set (composition-reference.md 7) and the Addressables source of the play-mode tests.</summary>
    static class UnitPlayData
    {
        public static UnitLook Novice() => new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_NOVICE, Sex = AccountSex.SEX_MALE, HairStyle = 1 };

        public static UnitLook Swordsman() => new UnitLook
        {
            Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_SWORDMAN, Sex = AccountSex.SEX_MALE, HairStyle = 2, HairColour = 3,
            HeadTop = 16, HeadBottom = 8, Garment = 1, Weapon = 2, WeaponItem = 1101, Shield = 1, ShieldItem = 2101,
        };

        public static UnitLook[] ReferenceLooks() => new[]
        {
            Novice(),
            Swordsman(),
            new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_KNIGHT2, Sex = AccountSex.SEX_FEMALE, HairStyle = 3 },
            new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_SUMMONER, Sex = AccountSex.SEX_MALE, HairStyle = 1 },
            UnitLook.ForJob(1002),
            UnitLook.ForJob(1063),
            UnitLook.ForJob(117),
        };

        public static IEnumerator LoadSource(Action<AddressablesUnitAssetSource> got)
        {
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadMainAssetAtPath(SpriteCatalogKeys.Index(SpriteCatalogCategory.Bodies)) == null)
                Assert.Ignore("the catalog is not imported yet (Tools/Ragnarok/Import Catalog…)");
#endif
            AddressablesUnitAssetSource source = null;
            string error = null;
            bool done = false;
            AddressablesUnitAssetSource.LoadAsync((s, e) =>
            {
                source = s;
                error = e;
                done = true;
            });
            for (int i = 0; i < 1800 && !done; i++) yield return null;
            Assert.That(source, Is.Not.Null, error ?? "the catalog did not load in time");
            got(source);
        }
    }
}
