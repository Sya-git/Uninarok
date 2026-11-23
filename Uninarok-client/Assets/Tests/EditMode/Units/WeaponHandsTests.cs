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
        [Test]
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
        const string Pending = "pending T10.2";

        // Expected row job per id (PLAN-0011 4.7 family table, FR-15). Ids up to 4242: Hercules class.h and class_hidden.h.
        // Ids above 4242: roBrowserLegacy JobConst.js / WeaponAction.js, copied untested (D27). Ids absent here map to themselves.
        static readonly (int Row, int[] Ids)[] Families =
        {
            (0, new[] { 4001, 4023 }),
            (1, new[] { 4002, 4024 }), (2, new[] { 4003, 4025 }), (3, new[] { 4004, 4026 }),
            (4, new[] { 4005, 4027 }), (5, new[] { 4006, 4028 }), (6, new[] { 4007, 4029 }),
            // Knight: Lord Knight, baby, Rune Knight (+2nd outfit), Dragon Knight (+riding)
            (7, new[] { 4008, 4030, 4054, 4060, 4096, 4252, 4265, 4332 }),
            // Knight on a peco: RUNE_KNIGHT2_2ND 4345, DRAGON_KNIGHT2 4280
            (13, new[] { 4014, 4036, 4080, 4081, 4088, 4089, 4090, 4091, 4092, 4093, 4094, 4095, 4109, 4280, 4345 }),
            // Crusader: Paladin, baby, Royal Guard (+2nd), Imperial Guard (+riding)
            (14, new[] { 4015, 4037, 4066, 4073, 4102, 4258, 4271, 4338 }),
            // Crusader on a peco: roBrowserLegacy copies CRUSADER2 to all of these (owner D25); Imperial Guard 2 (4281), Royal Guard 2 2nd (4348)
            (21, new[] { 4022, 4044, 4082, 4083, 4110, 4281, 4348 }),
            (8, new[] { 4009, 4031, 4057, 4063, 4099, 4256, 4269, 4336 }),   // 4336: source bug in WeaponAction.js, proposal
            (9, new[] { 4010, 4032, 4055, 4061, 4097, 4255, 4268, 4335 }),
            (16, new[] { 4017, 4039, 4067, 4074, 4103, 4261, 4274, 4341 }),
            (10, new[] { 4011, 4033, 4058, 4064, 4086, 4087, 4100, 4112, 4253, 4266, 4279, 4333, 4347 }),
            (18, new[] { 4019, 4041, 4071, 4078, 4107, 4259, 4272, 4339 }),
            (11, new[] { 4012, 4034, 4056, 4062, 4084, 4085, 4098, 4111, 4257, 4270, 4278, 4337, 4346 }),
            (17, new[] { 4018, 4040, 4072, 4079, 4108, 4260, 4273, 4340 }),
            (12, new[] { 4013, 4035, 4059, 4065, 4101, 4254, 4267, 4334 }),
            (15, new[] { 4016, 4038, 4070, 4077, 4106, 4262, 4275, 4342 }),
            (19, new[] { 4020, 4042, 4068, 4075, 4104, 4263, 4276, 4343 }),
            (20, new[] { 4021, 4043, 4069, 4076, 4105, 4264, 4277, 4344 }),
            (23, new[] { 4045, 4190, 4191, 4307, 4314 }),                    // Hyper Novice
            (24, new[] { 4215, 4228, 4229, 4306, 4313 }),                    // Night Watch
            (25, new[] { 4211, 4212, 4222, 4223, 4224, 4304, 4305, 4311, 4312 }),
            (4049, new[] { 4227 }),
            (4218, new[] { 4219, 4220, 4308, 4315 }),                        // Doram, Spirit Handler (4219 copied by roBrowserLegacy)
            // Taekwon family: Attack2 for every weapon. 4243 to 4245, 4247, 4302, 4309, 4316: roBrowserLegacy has no entry; proposal (D22)
            (4046, new[] { 4047, 4048, 4225, 4226, 4238, 4239, 4241, 4243, 4244, 4245, 4247, 4302, 4309, 4316 }),
            // Soul Reaper family: the Wizard row. 4246, 4248 (HAETAE_SOUL_REAPER*): proposal
            (9, new[] { 4240, 4242, 4246, 4248, 4303, 4310 }),
            // Druid: own row 4351
            (4351, new[] { 4352, 4353, 4354, 4355, 4358, 4359, 4360 }),
        };

        // Soul Reaper, Soul Reaper baby, HAETAE_SOUL_REAPER, HAETAE_SOUL_REAPER_B, Soul Ascetic and its riding variant.
        static readonly int[] SoulReaperFamily = { 4240, 4242, 4246, 4248, 4303, 4310 };

        // Themselves: no row (Attack1). Gangsi, Death Knight, Dark Collector, outfit jobs, WEREWOLF/WERERAPTOR (proposal),
        // range markers and an unlisted id; also the first classes and rows that stand for themselves.
        static readonly int[] OwnRow = { 0, 1, 7, 12, 17, 22, 26, 27, 4046, 4049, 4050, 4051, 4052, 4218, 4249, 4331, 4350, 4351, 4356, 4357, 4361, 9999 };

        [Test, Ignore(Pending)]
        public void JobFamilies_RowJobOf_Table()
        {
            foreach (var (row, ids) in Families)
                foreach (int id in ids)
                    Assert.That(JobFamilies.RowJobOf(id), Is.EqualTo(row), $"job {id}");
            foreach (int id in OwnRow)
                Assert.That(JobFamilies.RowJobOf(id), Is.EqualTo(id), $"job {id} stands for itself");
        }

        [Test, Ignore(Pending)]
        public void JobFamilies_RowSexOf_SoulReaperMale()
        {
            foreach (int id in SoulReaperFamily)
                foreach (byte sex in new byte[] { 0, 1 })
                    Assert.That(JobFamilies.RowSexOf(id, sex), Is.EqualTo((byte)1), $"job {id} sex {sex}");

            // Every other id keeps its sex.
            foreach (var (_, ids) in Families)
                foreach (int id in ids)
                {
                    if (System.Array.IndexOf(SoulReaperFamily, id) >= 0) continue;
                    foreach (byte sex in new byte[] { 0, 1 })
                        Assert.That(JobFamilies.RowSexOf(id, sex), Is.EqualTo(sex), $"job {id} sex {sex}");
                }
            foreach (int id in OwnRow)
                foreach (byte sex in new byte[] { 0, 1 })
                    Assert.That(JobFamilies.RowSexOf(id, sex), Is.EqualTo(sex), $"job {id} sex {sex}");
        }

        [Test, Ignore(Pending)]
        public void JobFamilies_IsAssassinFamily_EveryIdAndNotRogue()
        {
            // JOB_ASSASSIN, JOB_ASSASSIN_CROSS, JOB_BABY_ASSASSIN, JOB_GUILLOTINE_CROSS, JOB_GUILLOTINE_CROSS_T, JOB_BABY_CROSS,
            // then the ids above 4242: SHADOW_CROSS, SHADOW_CROSS_RIDING (roBrowserLegacy) and GUILLOTINE_CROSS_2ND
            foreach (int id in new[] { 12, 4013, 4035, 4059, 4065, 4101, 4254, 4267, 4334 })
                Assert.That(JobFamilies.IsAssassinFamily(id), Is.True, $"job {id}");

            // Rogue lines and others are not the assassin family (ABYSS_CHASER 4260, 4273; SHADOW_CHASER_2ND 4340).
            foreach (int id in new[] { 0, 1, 11, 17, 4015, 4036, 4060, 4066, 4102, 4012, 4014, 4260, 4273, 4340, 4018, 4108 })
                Assert.That(JobFamilies.IsAssassinFamily(id), Is.False, $"job {id}");
        }
    }
}
