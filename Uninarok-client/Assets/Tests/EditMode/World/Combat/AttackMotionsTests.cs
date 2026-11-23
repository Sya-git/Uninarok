using NUnit.Framework;
using Uninarok.Units;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-4, FR-15 (PLAN-0011 4.7, D7, D20 to D27): the attack animation rows of combat-reference.md, asked by row job
    // (JobFamilies) and by the type of both hands (WeaponHands.Combine, synthetic hands, no catalog).
    public class AttackMotionsTests
    {
        const byte Female = 0, Male = 1, Either = 2;
        const int MaxType = 30;

        // Expected rows: a copy of combat-reference.md "Attack animation" (row job, sex, weapon types -> number).
        // A type not listed gives 1. Taekwon 4046 is the one row for every weapon (D22): its own test.
        static (int Job, byte Sex, int[] Types, int Number)[] Expected => new (int, byte, int[], int)[]
        {
            (0, Female, new[] { 10, 23, 2, 3, 6, 7, 8, 9 }, 2), (0, Female, new[] { 1 }, 3),
            (0, Male, new[] { 1 }, 2), (0, Male, new[] { 10, 23, 2, 3, 6, 7, 8, 9 }, 3),
            (1, Either, new[] { 1, 2, 3, 6, 7, 8, 9 }, 2), (1, Either, new[] { 4, 5 }, 3),
            (2, Either, new[] { 10, 23 }, 2), (2, Either, new[] { 1 }, 3),
            (3, Either, new[] { 11 }, 2), (3, Either, new[] { 1 }, 3),
            (4, Either, new[] { 10, 23, 8, 9 }, 2),
            (5, Either, new[] { 8, 9, 6, 7, 2, 3 }, 2), (5, Either, new[] { 1 }, 3),
            (6, Either, new[] { 2, 3, 1 }, 2), (6, Either, new[] { 11 }, 3),
            (7, Either, new[] { 1, 2, 3, 6, 7, 8, 9 }, 2), (7, Either, new[] { 4, 5 }, 3),
            (13, Either, new[] { 1, 2, 3, 6, 7, 8, 9 }, 2), (13, Either, new[] { 4, 5 }, 3),
            (14, Either, new[] { 1, 2, 3, 6, 7, 8, 9 }, 2), (14, Either, new[] { 4, 5 }, 3),
            (8, Either, new[] { 10, 23, 8, 9 }, 2), (8, Either, new[] { 15 }, 3),
            (9, Female, new[] { 1 }, 2), (9, Female, new[] { 10, 23 }, 3),
            (9, Male, new[] { 10, 23 }, 2), (9, Male, new[] { 1 }, 3),
            (10, Either, new[] { 1 }, 2), (10, Either, new[] { 2, 3, 6, 7, 8, 9 }, 3),
            (11, Either, new[] { 1 }, 2), (11, Either, new[] { 11 }, 3),
            (12, Either, new[] { 6, 2, 1 }, 2), (12, Either, new[] { 25, 26, 27, 28, 29, 30, 16 }, 3),
            (15, Either, new[] { 10, 23, 8, 9 }, 2), (15, Either, new[] { 12 }, 3),
            (16, Either, new[] { 1 }, 2), (16, Either, new[] { 10, 23, 15 }, 3),
            (17, Either, new[] { 2, 3, 1 }, 2), (17, Either, new[] { 11 }, 3),
            (18, Either, new[] { 1 }, 2), (18, Either, new[] { 3, 2, 6, 7, 8, 9 }, 3),
            (19, Either, new[] { 1, 13 }, 2), (19, Either, new[] { 11 }, 3),
            (20, Either, new[] { 14 }, 2), (20, Either, new[] { 11 }, 3),
            (21, Either, new[] { 1, 2, 3, 6, 7, 8 }, 2), (21, Either, new[] { 4, 5 }, 3),
            (23, Female, new[] { 10, 23, 6, 7, 8, 9, 2 }, 2), (23, Female, new[] { 1 }, 3),
            (23, Male, new[] { 1 }, 2), (23, Male, new[] { 10, 23, 6, 7, 8, 9, 2 }, 3),
            (24, Either, new[] { 0, 17, 20 }, 2), (24, Either, new[] { 19, 18, 21 }, 3),
            (25, Either, new[] { 1 }, 2), (25, Either, new[] { 22 }, 3),
            (4049, Female, new[] { 1 }, 2), (4049, Female, new[] { 10, 23 }, 3),
            (4049, Male, new[] { 10, 23 }, 2), (4049, Male, new[] { 1 }, 3),
            (4218, Either, new[] { 10, 23 }, 2),
            (4351, Either, new[] { 1 }, 2), (4351, Either, new[] { 6, 7, 8, 9 }, 3),
        };

        static int[] RowJobs => new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 4049, 4218, 4351 };

        // Derived ids and their expected row job (plan 4.7 family table; Taekwon and Soul Reaper families have their own tests).
        static (int Row, int[] Ids)[] Families => new (int, int[])[]
        {
            (0, new[] { 4001, 4023 }), (1, new[] { 4002, 4024 }), (2, new[] { 4003, 4025 }), (3, new[] { 4004, 4026 }),
            (4, new[] { 4005, 4027 }), (5, new[] { 4006, 4028 }), (6, new[] { 4007, 4029 }),
            (7, new[] { 4008, 4030, 4054, 4060, 4096, 4252, 4265, 4332 }),
            (13, new[] { 4014, 4036, 4080, 4081, 4088, 4089, 4090, 4091, 4092, 4093, 4094, 4095, 4109, 4280, 4345 }),
            (14, new[] { 4015, 4037, 4066, 4073, 4102, 4258, 4271, 4338 }),
            (21, new[] { 4022, 4044, 4082, 4083, 4110, 4281, 4348 }),
            (8, new[] { 4009, 4031, 4057, 4063, 4099, 4256, 4269, 4336 }),
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
            (23, new[] { 4045, 4190, 4191, 4307, 4314 }),     // Hyper Novice as Super Novice
            (24, new[] { 4215, 4228, 4229, 4306, 4313 }),
            (25, new[] { 4211, 4212, 4222, 4223, 4224, 4304, 4305, 4311, 4312 }),
            (4049, new[] { 4227 }),
            (4218, new[] { 4219, 4220, 4308, 4315 }),         // Doram: own row
            (4351, new[] { 4352, 4353, 4354, 4355, 4358, 4359, 4360 }),   // Druid: own row
        };

        static int[] TaekwonFamily => new[] { 4046, 4047, 4048, 4225, 4226, 4238, 4239, 4241, 4243, 4244, 4245, 4247, 4302, 4309, 4316 };
        static int[] SoulReaperFamily => new[] { 4240, 4242, 4246, 4248, 4303, 4310 };
        static int[] NoRowJobs => new[] { 4050, 4051, 4052, 22, 26, 27, 4356, 4357 };

        static int ExpectedOf(int job, byte sex, int type)
        {
            foreach (var (j, s, types, n) in Expected)
                if (j == job && (s == Either || s == sex) && System.Array.IndexOf(types, type) >= 0) return n;
            return 1;
        }

        // The number the timeline asks for (plan 4.7): row job and row sex of the server's job, the type of both hands.
        static int NumberOfJob(int job, byte sex, int type) =>
            AttackMotions.NumberOf(JobFamilies.RowJobOf(job), JobFamilies.RowSexOf(job, sex), type);

        static Hand W(int type) => type == 0 ? None() : new Hand(1200 + type, HandKind.Weapon, type, type);
        static Hand Shield() => new Hand(2101, HandKind.Shield, 1, 0);
        static Hand ShieldViewId() => new Hand(1, HandKind.Shield, 1, 0);   // a small left value: a view id, counts as a shield
        static Hand None() => new Hand(0, HandKind.None, 0, 0);

        [Test]
        public void AttackMotions_Rows()
        {
            foreach (int job in RowJobs)
                foreach (byte sex in new[] { Female, Male })
                    for (int type = 0; type <= MaxType; type++)
                        Assert.AreEqual(ExpectedOf(job, sex, type), AttackMotions.NumberOf(job, sex, type), $"job {job} sex {sex} type {type}");

            Assert.AreEqual(3, AttackMotions.NumberOf(12, Male, 25), "assassin, two short swords: the combined type");
            Assert.AreEqual(2, AttackMotions.NumberOf(7, Male, 1), "knight, short sword");
            Assert.AreEqual(1, AttackMotions.NumberOf(7, Male, 25), "a knight has no two-weapon row");
            Assert.AreEqual(1, AttackMotions.NumberOf(21, Male, 9), "crusader on a peco has no two-handed mace row");
            Assert.AreEqual(1, AttackMotions.NumberOf(1002, Male, 0), "not a listed job");
            Assert.AreEqual(1, AttackMotions.NumberOf(0, Either, 1), "a sex other than 0 or 1");
            Assert.AreEqual(1, AttackMotions.NumberOf(0, 200, 1), "a sex other than 0 or 1");
            foreach (int type in new[] { -1, 31, 255, 256, 1000, int.MaxValue, int.MinValue })
                Assert.AreEqual(1, AttackMotions.NumberOf(1, Male, type), $"weapon type {type} out of range");
            Assert.AreEqual(1, AttackMotions.NumberOf(-1, Male, 1), "negative job");
            Assert.AreEqual(1, AttackMotions.NumberOf(4046 + 65536, Male, 1), "a job that matches a row only in its low 16 bits");

            foreach (int job in RowJobs)
                Assert.AreEqual(6f, AttackMotions.HitFrameOf(job, Male, 1), $"hit frame, job {job}");
        }

        [Test]
        public void AttackMotions_JobFamilies_EveryWeaponType()
        {
            foreach (var (row, ids) in Families)
                foreach (int id in ids)
                    foreach (byte sex in new[] { Female, Male })
                        for (int type = 0; type <= MaxType; type++)
                            Assert.AreEqual(ExpectedOf(row, sex, type), NumberOfJob(id, sex, type), $"job {id} (row {row}) sex {sex} type {type}");
        }

        [Test]
        public void AttackMotions_TaekwonFamily_Attack2EveryWeapon()
        {
            foreach (int id in TaekwonFamily)
                foreach (byte sex in new[] { Female, Male })
                {
                    for (int type = 0; type <= MaxType; type++)
                        Assert.AreEqual(2, NumberOfJob(id, sex, type), $"job {id} sex {sex} type {type}");
                    Assert.AreEqual(2, NumberOfJob(id, sex, 1000), $"job {id} sex {sex} unknown type");
                }
        }

        [Test]
        public void AttackMotions_SoulReaper_MaleWizardRowEitherSex()
        {
            foreach (int id in SoulReaperFamily)
                foreach (byte sex in new[] { Female, Male })
                    for (int type = 0; type <= MaxType; type++)
                        Assert.AreEqual(ExpectedOf(9, Male, type), NumberOfJob(id, sex, type), $"job {id} sex {sex} type {type}");

            // The male row differs from the female one: a female soul reaper with a rod plays 2, a female wizard 3.
            Assert.AreEqual(2, NumberOfJob(4240, Female, 10));
            Assert.AreEqual(3, NumberOfJob(9, Female, 10));
        }

        [Test]
        public void AttackMotions_NoRowJobs_Attack1()
        {
            foreach (int id in NoRowJobs)
                foreach (byte sex in new[] { Female, Male })
                    for (int type = 0; type <= MaxType; type++)
                        Assert.AreEqual(1, NumberOfJob(id, sex, type), $"job {id} sex {sex} type {type}");
        }

        static void Case(int job, Hand right, Hand left, int number, WeaponSource source, string what)
        {
            var hands = WeaponHands.Combine(job, right, left);
            Assert.AreEqual(source, hands.Weapon, what + " source");
            Assert.AreEqual(number, NumberOfJob(job, Male, hands.WeaponType), what + " number");
        }

        [Test]
        public void AttackMotions_Hands_EachCase()
        {
            const int Swordman = 1, Knight = 7, Assassin = 12, AssassinCross = 4013, GuillotineCross = 4059, Gunslinger = 24, Rebellion = 4215;
            const int Knife = 1, TwoHandSword = 3, Katar = 16;

            Case(Assassin, W(Knife), None(), 2, WeaponSource.Right, "weapon / none");
            Case(Assassin, None(), W(Knife), 2, WeaponSource.Left, "knife in the left hand only");
            foreach (int job in new[] { Assassin, AssassinCross, GuillotineCross })
                Case(job, W(Knife), W(Knife), 3, WeaponSource.Both, $"two knives, job {job}");
            Case(AssassinCross, W(2), W(6), 3, WeaponSource.Both, "sword and axe, assassin cross");
            Case(Swordman, W(Knife), W(Knife), 2, WeaponSource.Right, "two knives, swordman");
            Case(Assassin, W(Knife), Shield(), 2, WeaponSource.Right, "weapon / shield");
            Case(Assassin, W(Knife), ShieldViewId(), 2, WeaponSource.Right, "weapon / shield view id");
            Case(Knight, W(TwoHandSword), None(), 2, WeaponSource.Right, "two-handed weapon / none");
            foreach (int job in new[] { Assassin, GuillotineCross })
            {
                Case(job, W(Katar), None(), 3, WeaponSource.Right, $"katar, job {job}");
                Case(job, W(Katar), W(Knife), 3, WeaponSource.Right, $"katar / knife, job {job}");
                Case(job, W(Katar), Shield(), 3, WeaponSource.Right, $"katar / shield, job {job}");
            }
            Case(Swordman, None(), Shield(), 1, WeaponSource.None, "none / shield");
            Case(Swordman, None(), ShieldViewId(), 1, WeaponSource.None, "none / shield view id");
            Case(Gunslinger, None(), Shield(), 2, WeaponSource.None, "none / shield, gunslinger");
            Case(Rebellion, None(), Shield(), 2, WeaponSource.None, "none / shield, rebellion");
            Case(Swordman, None(), None(), 1, WeaponSource.None, "none / none");
        }

        [Test]
        public void AttackMotions_ShieldNeverChangesMotion()
        {
            var jobs = new System.Collections.Generic.List<int>(RowJobs) { JobFamilies.Taekwon };
            foreach (int job in jobs)
                foreach (byte sex in new[] { Female, Male })
                    for (int type = 0; type <= MaxType; type++)
                    {
                        int alone = AttackMotions.NumberOf(job, sex, WeaponHands.Combine(job, W(type), None()).WeaponType);
                        int withShield = AttackMotions.NumberOf(job, sex, WeaponHands.Combine(job, W(type), Shield()).WeaponType);
                        int withViewId = AttackMotions.NumberOf(job, sex, WeaponHands.Combine(job, W(type), ShieldViewId()).WeaponType);
                        Assert.AreEqual(alone, withShield, $"job {job} sex {sex} type {type}: shield");
                        Assert.AreEqual(alone, withViewId, $"job {job} sex {sex} type {type}: shield view id");
                    }
        }
    }
}
