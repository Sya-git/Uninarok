using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Uninarok.Networking.Data;
using Uninarok.Units.Editor;

namespace Uninarok.Units.Tests
{
    /// <summary>Synthetic data and the reference set (composition-reference.md 7) shared by the unit tests (PLAN-0009 6).</summary>
    static class UnitTestData
    {
        public static CompositionTables Tables() => ScriptableObject.CreateInstance<CompositionTables>();

        /// <summary>The imported catalog and tables, or the test is ignored.</summary>
        public static AssetDatabaseUnitAssetSource CatalogOrIgnore()
        {
            var source = AssetDatabaseUnitAssetSource.Create();
            if (source == null) Assert.Ignore("the catalog is not imported (Tools/Ragnarok/Import Catalog…)");
            if (source.Tables == null) Assert.Ignore("the composition tables are not imported (Tools/Ragnarok/Catalog/Composition)");
            return source;
        }

        public static UnitLook Novice() => new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_NOVICE, Sex = AccountSex.SEX_MALE, HairStyle = 1 };

        public static UnitLook Swordsman() => new UnitLook
        {
            Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_SWORDMAN, Sex = AccountSex.SEX_MALE, HairStyle = 2, HairColour = 3,
            HeadTop = 16, HeadBottom = 8, Garment = 1, Weapon = 2, WeaponItem = 1101, Shield = 1, ShieldItem = 2101,
        };

        public static UnitLook KnightOnPeco() => new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_KNIGHT2, Sex = AccountSex.SEX_FEMALE, HairStyle = 3 };

        public static UnitLook Summoner() => new UnitLook { Kind = UnitKind.Player, JobId = (int)PlayerJob.JOB_SUMMONER, Sex = AccountSex.SEX_MALE, HairStyle = 1 };

        public static IEnumerable<(string name, UnitLook look, PartSlot[] slots)> ReferenceSet()
        {
            yield return ("novice", Novice(), new[] { PartSlot.Body, PartSlot.Head });
            yield return ("swordsman", Swordsman(), new[]
            {
                PartSlot.Body, PartSlot.Head, PartSlot.HeadTop, PartSlot.HeadBottom, PartSlot.Garment, PartSlot.Weapon, PartSlot.WeaponTrail, PartSlot.Shield,
            });
            yield return ("knight on peco", KnightOnPeco(), new[] { PartSlot.Body, PartSlot.Head });
            yield return ("summoner", Summoner(), new[] { PartSlot.Body, PartSlot.Head });
            yield return ("poring", UnitLook.ForJob(1002), new[] { PartSlot.Body });
            yield return ("lunatic", UnitLook.ForJob(1063), new[] { PartSlot.Body });
            yield return ("kafra", UnitLook.ForJob(117), new[] { PartSlot.Body });
        }
    }
}
