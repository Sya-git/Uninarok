using NUnit.Framework;

namespace Uninarok.Units.Tests
{
    // TASKS-0009 T10.1 (FR-17; PLAN-0009 4.11). Synthetic hands.
    public class WeaponHandsTests
    {
        const int Swordsman = 1, Assassin = 12, Rogue = 17;
        const int ShortSword = 1, Sword = 2, Axe = 6, Katar = 16, Bow = 11;

        static Hand W(int type) => new Hand(1200 + type, HandKind.Weapon, 0, type);
        static Hand Shield() => new Hand(2101, HandKind.Shield, 1, 0);
        static Hand None() => new Hand(0, HandKind.None, 0, 0);
        static Hand Unknown() => new Hand(9999, HandKind.Unknown, 0, 0);

        static void Check(int job, Hand right, Hand left, int type, WeaponSource source, bool shield, bool leftNotDrawn, string what)
        {
            var r = WeaponHands.Combine(job, right, left);
            Assert.That(r.WeaponType, Is.EqualTo(type), what + " type");
            Assert.That(r.Weapon, Is.EqualTo(source), what + " source");
            Assert.That(r.DrawShield, Is.EqualTo(shield), what + " shield");
            Assert.That(r.LeftNotDrawn, Is.EqualTo(leftNotDrawn), what + " leftNotDrawn");
        }

        [Test]
        public void WeaponHands_Combine_EveryCase()
        {
            Check(Swordsman, W(Sword), None(), Sword, WeaponSource.Right, false, false, "weapon/none");
            Check(Swordsman, None(), W(Axe), Axe, WeaponSource.Left, false, false, "none/weapon");
            Check(Assassin, None(), W(Sword), Sword, WeaponSource.Left, false, false, "none/weapon assassin");

            // Assassin family, the six pairs in both orders.
            (int a, int b, int combined)[] pairs =
            {
                (ShortSword, ShortSword, 25), (Sword, Sword, 26), (Axe, Axe, 27),
                (ShortSword, Sword, 28), (ShortSword, Axe, 29), (Sword, Axe, 30),
            };
            foreach (var (a, b, c) in pairs)
            {
                Check(Assassin, W(a), W(b), c, WeaponSource.Both, false, false, $"assassin {a}/{b}");
                Check(Assassin, W(b), W(a), c, WeaponSource.Both, false, false, $"assassin {b}/{a}");
                // Another job keeps the right weapon and does not draw the left one.
                Check(Swordsman, W(a), W(b), a, WeaponSource.Right, false, true, $"swordsman {a}/{b}");
                Check(Swordsman, W(b), W(a), b, WeaponSource.Right, false, true, $"swordsman {b}/{a}");
            }
            // An assassin with a pair that has no combined sprite keeps the right weapon.
            Check(Assassin, W(Sword), W(Bow), Sword, WeaponSource.Right, false, true, "assassin no combined pair");

            Check(Swordsman, W(Sword), Shield(), Sword, WeaponSource.Right, true, false, "weapon/shield");
            Check(Assassin, W(Sword), Shield(), Sword, WeaponSource.Right, true, false, "assassin weapon/shield");

            Check(Assassin, W(Katar), None(), Katar, WeaponSource.Right, false, false, "katar alone");
            Check(Assassin, W(Katar), W(Katar), Katar, WeaponSource.Right, false, false, "katar/katar");
            Check(Assassin, W(Katar), W(Sword), Katar, WeaponSource.Right, false, false, "katar/weapon");
            Check(Assassin, W(Katar), Shield(), Katar, WeaponSource.Right, false, false, "katar/shield");

            Check(Swordsman, None(), Shield(), 0, WeaponSource.None, true, false, "none/shield");
            Check(Swordsman, None(), None(), 0, WeaponSource.None, false, false, "none/none");
            Check(Swordsman, Unknown(), Unknown(), 0, WeaponSource.None, false, false, "unknown/unknown");
            Check(Swordsman, W(Sword), Unknown(), Sword, WeaponSource.Right, false, false, "weapon/unknown");
            Check(Swordsman, Unknown(), W(Axe), Axe, WeaponSource.Left, false, false, "unknown/weapon");
            Check(Swordsman, Unknown(), Shield(), 0, WeaponSource.None, true, false, "unknown/shield");
        }

        [Test]
        public void WeaponHands_CombinedType_SixPairsEitherOrder_ElseZero()
        {
            Assert.That(WeaponHands.CombinedType(ShortSword, ShortSword), Is.EqualTo(25));
            Assert.That(WeaponHands.CombinedType(Sword, Sword), Is.EqualTo(26));
            Assert.That(WeaponHands.CombinedType(Axe, Axe), Is.EqualTo(27));
            Assert.That(WeaponHands.CombinedType(ShortSword, Sword), Is.EqualTo(28));
            Assert.That(WeaponHands.CombinedType(Sword, ShortSword), Is.EqualTo(28));
            Assert.That(WeaponHands.CombinedType(ShortSword, Axe), Is.EqualTo(29));
            Assert.That(WeaponHands.CombinedType(Axe, ShortSword), Is.EqualTo(29));
            Assert.That(WeaponHands.CombinedType(Sword, Axe), Is.EqualTo(30));
            Assert.That(WeaponHands.CombinedType(Axe, Sword), Is.EqualTo(30));

            Assert.That(WeaponHands.CombinedType(0, Sword), Is.EqualTo(0));
            Assert.That(WeaponHands.CombinedType(Sword, 0), Is.EqualTo(0));
            Assert.That(WeaponHands.CombinedType(Katar, Katar), Is.EqualTo(0));
            Assert.That(WeaponHands.CombinedType(Bow, Sword), Is.EqualTo(0));
            Assert.That(WeaponHands.CombinedType(Axe, Katar), Is.EqualTo(0));
        }
    }

    // TASKS-0009 T10.3 (FR-17; PLAN-0009 4.11). Real catalog.
    [Category("NeedsData")]
    public class WeaponHandsClassifyTests
    {
        [Test, Ignore("pending T10.4")]
        public void WeaponHands_Classify_LeftByItemKind()
        {
            var source = UnitTestData.CatalogOrIgnore();
            var catalog = source.Catalog;
            var tables = source.Tables;

            var knife = WeaponHands.Left(1201, catalog, tables);
            Assert.That(knife.Kind, Is.EqualTo(HandKind.Weapon), "left 1201");
            Assert.That(knife.WeaponType, Is.EqualTo(1), "left 1201 type");

            Assert.That(WeaponHands.Left(2101, catalog, tables).Kind, Is.EqualTo(HandKind.Shield), "left 2101");
            Assert.That(WeaponHands.Left(1, catalog, tables).Kind, Is.EqualTo(HandKind.Shield), "left 1 (view id)");
            Assert.That(WeaponHands.Left(2220, catalog, tables).Kind, Is.EqualTo(HandKind.Unknown), "left 2220 (headgear)");
            Assert.That(WeaponHands.Left(9999999, catalog, tables).Kind, Is.EqualTo(HandKind.Unknown), "left 9999999");
            Assert.That(WeaponHands.Right(2101, catalog, tables).Kind, Is.EqualTo(HandKind.Unknown), "right 2101");
        }
    }

    public class JobFamiliesTests
    {
        [Test]
        public void JobFamilies_IsAssassinFamily_EveryIdAndNotRogue()
        {
            // JOB_ASSASSIN, JOB_ASSASSIN_CROSS, JOB_BABY_ASSASSIN, JOB_GUILLOTINE_CROSS, JOB_GUILLOTINE_CROSS_T, JOB_BABY_CROSS
            foreach (int id in new[] { 12, 4013, 4035, 4059, 4065, 4101 })
                Assert.That(JobFamilies.IsAssassinFamily(id), Is.True, $"job {id}");

            // Rogue lines and others are not the assassin family.
            foreach (int id in new[] { 0, 1, 11, 17, 4015, 4036, 4060, 4066, 4102, 4012, 4014 })
                Assert.That(JobFamilies.IsAssassinFamily(id), Is.False, $"job {id}");
        }
    }
}
