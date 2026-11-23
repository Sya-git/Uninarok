using NUnit.Framework;

namespace Uninarok.World.Tests
{
    // SPEC-0011 FR-4 (PLAN-0011 4.7, D7): the attack animation rows of combat-reference.md.
    public class AttackMotionsTests
    {
        const byte Female = 0, Male = 1;

        [Test]
        public void AttackMotions_Rows()
        {
            var rows = new (int job, byte sex, int weapon, bool second, int expected, string what)[]
            {
                (0, Female, 1, false, 3, "novice, female, short sword"),
                (0, Male, 1, false, 2, "novice, male, short sword"),
                (0, Male, 0, false, 1, "bare hand"),
                (1, Female, 4, false, 3, "swordman, spear"),
                (1, Male, 2, false, 2, "swordman, sword"),
                (3, Female, 11, false, 2, "archer, bow"),
                (11, Male, 11, false, 3, "hunter, bow"),
                (9, Female, 10, false, 3, "wizard, female, rod"),
                (9, Male, 10, false, 2, "wizard, male, rod"),
                (12, Male, 16, false, 3, "assassin, katar"),
                (12, Male, 1, false, 2, "assassin, one short sword"),
                (12, Male, 1, true, 3, "assassin, two short swords"),
                (7, Male, 1, true, 2, "a knight has no row for two weapons"),
                (20, Female, 14, false, 2, "dancer, whip"),
                (20, Female, 1, false, 1, "dancer, short sword"),
                (24, Male, 0, false, 2, "gunslinger, bare hand"),
                (24, Male, 18, false, 3, "gunslinger, rifle"),
                (4049, Male, 10, false, 2, "soul linker, male, rod"),
                (4218, Female, 23, false, 2, "summoner, two-handed rod"),
                (21, Male, 9, false, 1, "crusader on a peco has no two-handed mace row"),
                (1002, Male, 0, false, 1, "not a listed job"),
                (0, 2, 1, false, 1, "a sex other than 0 or 1"),
            };
            foreach (var r in rows) Assert.AreEqual(r.expected, AttackMotions.NumberOf(r.job, r.sex, r.weapon, r.second), r.what);
            Assert.AreEqual(6f, AttackMotions.HitFrameOf(11, Male, 11));
        }
    }
}
