using NUnit.Framework;
using Uninarok.Units;
using Uninarok.Units.Editor;
using Uninarok.World.View;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-4, FR-14 (PLAN-0011 4.7, D18): the server's weapon values to base types, on the imported catalog.
    [Category("NeedsData")]
    public class CatalogWeaponTypesTests
    {
        [Test]
        public void CatalogWeaponTypes_ViewIdItemIdUnknown()
        {
            var source = AssetDatabaseUnitAssetSource.Create();
            if (source == null || source.Tables == null) Assert.Ignore("the catalog or the composition tables are not imported");
            var types = new CatalogWeaponTypes(source);
            Assert.AreEqual(1, types.TypeOf(1), "view id 1: short sword");
            Assert.AreEqual(11, types.TypeOf(11), "view id 11: bow");
            Assert.AreEqual(1, types.TypeOf(1201), "item 1201, Knife: a short sword");
            Assert.AreEqual(1, types.TypeOf(1207), "item 1207, Main Gauche: a short sword");
            Assert.AreEqual(2, types.TypeOf(1101), "item 1101, Sword");
            Assert.AreEqual(11, types.TypeOf(1701), "item 1701, Bow");
            Assert.AreEqual(0, types.TypeOf(2101), "item 2101, Guard: not a weapon");
            Assert.AreEqual(0, types.TypeOf(9_999_999), "unknown: bare hand");
            Assert.AreEqual(0, types.TypeOf(0));
        }

        const int Swordsman = 1, Assassin = 12, AssassinCross = 4013;
        const int Knife = 1201, Sword = 1101, Axe = 1301, Guard = 2101, Katar = 1250, Bow = 1701, Unknown = 9_999_999;

        static CatalogWeaponTypes TypesOrIgnore(out IUnitAssetSource source)
        {
            source = AssetDatabaseUnitAssetSource.Create();
            if (source == null || source.Tables == null) Assert.Ignore("the catalog or the composition tables are not imported");
            return new CatalogWeaponTypes(source);
        }

        // SPEC-0011 FR-15 (PLAN-0011 4.7 hands table, D21): the type for the motion is the hands rule's, for every case.
        [Test]
        public void CatalogWeaponTypes_TypeOf_BothHands_IsHandsOf()
        {
            var types = TypesOrIgnore(out _);
            (int job, int right, int left, string what)[] cases =
            {
                (Assassin, Knife, 0, "weapon/none"), (Assassin, 0, Knife, "none/weapon"),
                (AssassinCross, Knife, Knife, "assassin family, two knives"), (Swordsman, Knife, Knife, "other job, two weapons"),
                (Assassin, Sword, Axe, "assassin, sword and axe"), (Assassin, Axe, Sword, "assassin, axe and sword"),
                (Swordsman, Sword, Guard, "weapon/shield item"), (Swordsman, Bow, 0, "two-handed weapon"),
                (Assassin, Katar, Knife, "katar/anything"), (Swordsman, 0, Guard, "none/shield"), (Swordsman, 0, 0, "none/none"),
                (Assassin, Unknown, Unknown, "unknown hands"), (Swordsman, Sword, Unknown, "weapon/unknown"),
            };
            foreach (var (job, right, left, what) in cases)
                Assert.AreEqual(types.HandsOf(job, right, left).WeaponType, types.TypeOf(job, right, left), what);
            Assert.AreEqual(1, types.TypeOf(Assassin, 0, Knife), "a left-only knife is the left type (1), not bare hand");
            Assert.AreEqual(25, types.TypeOf(AssassinCross, Knife, Knife), "assassin cross, two knives: 25");
            Assert.AreEqual(1, types.TypeOf(Swordsman, Knife, Knife), "swordsman, two knives: the right type");
            Assert.AreEqual(0, types.TypeOf(Swordsman, 0, Guard), "a shield alone is bare hand");
        }

        // FR-15 (4.7: a left value below 500 is a shield view id): view id 1 would read as a short sword, but leaves the right type.
        [Test]
        public void CatalogWeaponTypes_LeftShieldViewId_IsShield()
        {
            var types = TypesOrIgnore(out _);
            Assert.AreEqual(1, types.TypeOf(Assassin, Knife, 1), "knife + shield view id 1: the knife, not two knives (25)");
            Assert.AreEqual(2, types.TypeOf(Swordsman, Sword, 1), "sword + shield view id 1: the sword");
            Assert.AreEqual(0, types.TypeOf(Assassin, 0, 1), "shield view id alone: bare hand");
        }

        // FR-15 (4.7, D20, D24): the rows are JobFamilies', nothing added.
        [Test]
        public void CatalogWeaponTypes_RowJobOf_IsJobFamilies()
        {
            var types = TypesOrIgnore(out _);
            int[] jobs = { 0, 1, 7, 12, 4001, 4008, 4013, 4022, 4046, 4050, 4060, 4240, 4242, 22, 9_999 };
            foreach (int job in jobs)
            {
                Assert.AreEqual(JobFamilies.RowJobOf(job), types.RowJobOf(job), "row job of " + job);
                for (byte sex = 0; sex <= 1; sex++)
                    Assert.AreEqual(JobFamilies.RowSexOf(job, sex), types.RowSexOf(job, sex), "row sex of " + job + ", " + sex);
            }
            Assert.AreEqual(7, types.RowJobOf(4060), "rune knight: the knight row");
            Assert.AreEqual(1, types.RowSexOf(4240, 0), "soul reaper: the male row");
        }

        // SPEC-0009 FR-17 (PLAN-0009 4.11): the adapter adds nothing to WeaponHands.Of.
        [Test]
        public void CatalogWeaponTypes_HandsOf_IsWeaponHandsOf_EveryCase()
        {
            var source = AssetDatabaseUnitAssetSource.Create();
            if (source == null || source.Tables == null) Assert.Ignore("the catalog or the composition tables are not imported");
            var types = new CatalogWeaponTypes(source);

            void Same(int job, int right, int left, string what)
            {
                var expected = WeaponHands.Of(job, right, left, source.Catalog, source.Tables);
                var actual = types.HandsOf(job, right, left);
                Assert.AreEqual(expected.WeaponType, actual.WeaponType, what + " type");
                Assert.AreEqual(expected.Weapon, actual.Weapon, what + " source");
                Assert.AreEqual(expected.DrawShield, actual.DrawShield, what + " shield");
                Assert.AreEqual(expected.LeftNotDrawn, actual.LeftNotDrawn, what + " leftNotDrawn");
            }

            Same(Swordsman, Sword, 0, "weapon/none");
            Same(Swordsman, 0, Sword, "none/weapon");
            Same(Assassin, Knife, Knife, "assassin knife+knife");
            Assert.AreEqual(25, types.HandsOf(Assassin, Knife, Knife).WeaponType, "two knives: type 25");
            Same(Swordsman, Knife, Knife, "swordsman two weapons");
            Assert.AreEqual(1, types.HandsOf(Swordsman, Knife, Knife).WeaponType, "swordsman: the right type");
            // The other pairs, in either order: short sword 25 / 28 / 29, sword 26 / 30, axe 27.
            (int a, int b, int type)[] pairs =
            {
                (Knife, Knife, 25), (Sword, Sword, 26), (Axe, Axe, 27), (Knife, Sword, 28), (Knife, Axe, 29), (Sword, Axe, 30),
            };
            foreach (var (a, b, type) in pairs)
            {
                Same(Assassin, a, b, "pair " + a + "/" + b);
                Same(Assassin, b, a, "pair " + b + "/" + a);
                if (source.Catalog != null && source.Catalog.TryGetItemView(a, out _, out _) && source.Catalog.TryGetItemView(b, out _, out _))
                {
                    Assert.AreEqual(type, types.HandsOf(Assassin, a, b).WeaponType, "pair " + a + "/" + b + " type");
                    Assert.AreEqual(type, types.HandsOf(Assassin, b, a).WeaponType, "pair " + b + "/" + a + " type");
                }
            }
            Same(Swordsman, Sword, Guard, "weapon/shield");
            Same(Assassin, Katar, 0, "katar alone");
            Same(Assassin, Katar, Knife, "katar with a left value");
            Same(Swordsman, 0, Guard, "none/shield");
            Same(Swordsman, 0, 0, "none/none");
            Same(Assassin, Unknown, Unknown, "unknown hands");
            Same(Swordsman, Sword, Unknown, "weapon/unknown");
            Same(Swordsman, Sword, 1, "weapon/small left value (shield view id)");
        }
    }
}
