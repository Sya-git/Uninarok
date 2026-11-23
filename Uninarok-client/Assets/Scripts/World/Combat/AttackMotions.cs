using System;

namespace Uninarok.World
{
    /// <summary>
    /// Which of the three attack animations a player plays, and its hit frame (SPEC-0011 FR-4; PLAN-0011 4.7, D7). Rows in code,
    /// listed in <c>docs/specs/0011-combat/combat-reference.md</c>. Lead: roBrowserLegacy <c>src/DB/Jobs/WeaponAction.js</c>
    /// (read 2026-11-23), its job constants as Hercules job ids. Pure.
    /// </summary>
    public static class AttackMotions
    {
        /// <summary>The hit frame of nearly every attack, in frames of 24 ms of a 432 ms motion (lead, plan 4.7).</summary>
        public const float DefaultHitFrame = 6f;

        const byte Any = 2;

        // Weapon types (Weapon_IDs).
        const byte None = 0, ShortSword = 1, Sword = 2, TwoHandSword = 3, Spear = 4, TwoHandSpear = 5, Axe = 6, TwoHandAxe = 7, Mace = 8,
                   TwoHandMace = 9, Rod = 10, Bow = 11, Knuckle = 12, Instrument = 13, Whip = 14, Book = 15, Katar = 16, Handgun = 17,
                   Rifle = 18, Gatling = 19, Shotgun = 20, Grenade = 21, Shuriken = 22, TwoHandRod = 23, ShortSwordShortSword = 25,
                   SwordSword = 26, AxeAxe = 27, ShortSwordSword = 28, ShortSwordAxe = 29, SwordAxe = 30;

        // Rows of 5 bytes: job (little endian), sex (0 female, 1 male, 2 either), weapon type, animation (1 to 3).
        static ReadOnlySpan<byte> Rows => new byte[]
        {
            // Novice 0
            0, 0, 0, None, 1,  0, 0, 0, Rod, 2,  0, 0, 0, TwoHandRod, 2,  0, 0, 0, Sword, 2,  0, 0, 0, TwoHandSword, 2,  0, 0, 0, Axe, 2,
            0, 0, 0, TwoHandAxe, 2,  0, 0, 0, Mace, 2,  0, 0, 0, TwoHandMace, 2,  0, 0, 0, ShortSword, 3,
            0, 0, 1, None, 1,  0, 0, 1, ShortSword, 2,  0, 0, 1, Rod, 3,  0, 0, 1, TwoHandRod, 3,  0, 0, 1, Sword, 3,  0, 0, 1, TwoHandSword, 3,
            0, 0, 1, Axe, 3,  0, 0, 1, TwoHandAxe, 3,  0, 0, 1, Mace, 3,  0, 0, 1, TwoHandMace, 3,
            // Swordman 1
            1, 0, Any, None, 1,  1, 0, Any, ShortSword, 2,  1, 0, Any, Sword, 2,  1, 0, Any, TwoHandSword, 2,  1, 0, Any, Axe, 2,
            1, 0, Any, TwoHandAxe, 2,  1, 0, Any, Mace, 2,  1, 0, Any, TwoHandMace, 2,  1, 0, Any, Spear, 3,  1, 0, Any, TwoHandSpear, 3,
            // Magician 2
            2, 0, Any, None, 1,  2, 0, Any, Rod, 2,  2, 0, Any, TwoHandRod, 2,  2, 0, Any, ShortSword, 3,
            // Archer 3
            3, 0, Any, None, 1,  3, 0, Any, Bow, 2,  3, 0, Any, ShortSword, 3,
            // Acolyte 4
            4, 0, Any, None, 1,  4, 0, Any, Rod, 2,  4, 0, Any, TwoHandRod, 2,  4, 0, Any, Mace, 2,  4, 0, Any, TwoHandMace, 2,
            // Merchant 5
            5, 0, Any, None, 1,  5, 0, Any, Mace, 2,  5, 0, Any, TwoHandMace, 2,  5, 0, Any, Axe, 2,  5, 0, Any, TwoHandAxe, 2,
            5, 0, Any, Sword, 2,  5, 0, Any, TwoHandSword, 2,  5, 0, Any, ShortSword, 3,
            // Thief 6
            6, 0, Any, None, 1,  6, 0, Any, Sword, 2,  6, 0, Any, TwoHandSword, 2,  6, 0, Any, ShortSword, 2,  6, 0, Any, Bow, 3,
            // Knight 7, Knight on a peco 13, Crusader 14
            7, 0, Any, None, 1,  7, 0, Any, ShortSword, 2,  7, 0, Any, Sword, 2,  7, 0, Any, TwoHandSword, 2,  7, 0, Any, TwoHandMace, 2,
            7, 0, Any, Axe, 2,  7, 0, Any, TwoHandAxe, 2,  7, 0, Any, Mace, 2,  7, 0, Any, Spear, 3,  7, 0, Any, TwoHandSpear, 3,
            13, 0, Any, None, 1,  13, 0, Any, ShortSword, 2,  13, 0, Any, Sword, 2,  13, 0, Any, TwoHandSword, 2,  13, 0, Any, Axe, 2,
            13, 0, Any, TwoHandAxe, 2,  13, 0, Any, Mace, 2,  13, 0, Any, TwoHandMace, 2,  13, 0, Any, Spear, 3,  13, 0, Any, TwoHandSpear, 3,
            14, 0, Any, None, 1,  14, 0, Any, ShortSword, 2,  14, 0, Any, Sword, 2,  14, 0, Any, TwoHandSword, 2,  14, 0, Any, Axe, 2,
            14, 0, Any, TwoHandAxe, 2,  14, 0, Any, Mace, 2,  14, 0, Any, TwoHandMace, 2,  14, 0, Any, Spear, 3,  14, 0, Any, TwoHandSpear, 3,
            // Priest 8
            8, 0, Any, None, 1,  8, 0, Any, Rod, 2,  8, 0, Any, TwoHandRod, 2,  8, 0, Any, Mace, 2,  8, 0, Any, TwoHandMace, 2,  8, 0, Any, Book, 3,
            // Wizard 9
            9, 0, 0, None, 1,  9, 0, 0, ShortSword, 2,  9, 0, 0, Rod, 3,  9, 0, 0, TwoHandRod, 3,
            9, 0, 1, None, 1,  9, 0, 1, Rod, 2,  9, 0, 1, TwoHandRod, 2,  9, 0, 1, ShortSword, 3,
            // Blacksmith 10
            10, 0, Any, None, 1,  10, 0, Any, ShortSword, 2,  10, 0, Any, Sword, 3,  10, 0, Any, TwoHandSword, 3,  10, 0, Any, Axe, 3,
            10, 0, Any, TwoHandAxe, 3,  10, 0, Any, Mace, 3,  10, 0, Any, TwoHandMace, 3,
            // Hunter 11
            11, 0, Any, None, 1,  11, 0, Any, ShortSword, 2,  11, 0, Any, Bow, 3,
            // Assassin 12
            12, 0, Any, None, 1,  12, 0, Any, Axe, 2,  12, 0, Any, Sword, 2,  12, 0, Any, ShortSword, 2,  12, 0, Any, ShortSwordShortSword, 3,
            12, 0, Any, SwordSword, 3,  12, 0, Any, AxeAxe, 3,  12, 0, Any, ShortSwordSword, 3,  12, 0, Any, ShortSwordAxe, 3,
            12, 0, Any, SwordAxe, 3,  12, 0, Any, Katar, 3,
            // Monk 15
            15, 0, Any, None, 1,  15, 0, Any, Rod, 2,  15, 0, Any, TwoHandRod, 2,  15, 0, Any, Mace, 2,  15, 0, Any, TwoHandMace, 2,
            15, 0, Any, Knuckle, 3,
            // Sage 16
            16, 0, Any, None, 1,  16, 0, Any, ShortSword, 2,  16, 0, Any, Rod, 3,  16, 0, Any, TwoHandRod, 3,  16, 0, Any, Book, 3,
            // Rogue 17
            17, 0, Any, None, 1,  17, 0, Any, Sword, 2,  17, 0, Any, TwoHandSword, 2,  17, 0, Any, ShortSword, 2,  17, 0, Any, Bow, 3,
            // Alchemist 18
            18, 0, Any, None, 1,  18, 0, Any, ShortSword, 2,  18, 0, Any, TwoHandSword, 3,  18, 0, Any, Sword, 3,  18, 0, Any, Axe, 3,
            18, 0, Any, TwoHandAxe, 3,  18, 0, Any, Mace, 3,  18, 0, Any, TwoHandMace, 3,
            // Bard 19
            19, 0, Any, None, 1,  19, 0, Any, ShortSword, 2,  19, 0, Any, Instrument, 2,  19, 0, Any, Bow, 3,
            // Dancer 20
            20, 0, Any, None, 1,  20, 0, Any, ShortSword, 1,  20, 0, Any, Whip, 2,  20, 0, Any, Bow, 3,
            // Crusader on a peco 21 (no two-handed mace row)
            21, 0, Any, None, 1,  21, 0, Any, ShortSword, 2,  21, 0, Any, Sword, 2,  21, 0, Any, TwoHandSword, 2,  21, 0, Any, Axe, 2,
            21, 0, Any, TwoHandAxe, 2,  21, 0, Any, Mace, 2,  21, 0, Any, Spear, 3,  21, 0, Any, TwoHandSpear, 3,
            // Super Novice 23
            23, 0, 0, None, 1,  23, 0, 0, Rod, 2,  23, 0, 0, TwoHandRod, 2,  23, 0, 0, Axe, 2,  23, 0, 0, TwoHandAxe, 2,  23, 0, 0, Mace, 2,
            23, 0, 0, TwoHandMace, 2,  23, 0, 0, Sword, 2,  23, 0, 0, ShortSword, 3,
            23, 0, 1, None, 1,  23, 0, 1, ShortSword, 2,  23, 0, 1, Rod, 3,  23, 0, 1, TwoHandRod, 3,  23, 0, 1, Axe, 3,  23, 0, 1, TwoHandAxe, 3,
            23, 0, 1, Mace, 3,  23, 0, 1, TwoHandMace, 3,  23, 0, 1, Sword, 3,
            // Gunslinger 24
            24, 0, Any, None, 2,  24, 0, Any, Handgun, 2,  24, 0, Any, Shotgun, 2,  24, 0, Any, Gatling, 3,  24, 0, Any, Rifle, 3,
            24, 0, Any, Grenade, 3,
            // Ninja 25
            25, 0, Any, None, 1,  25, 0, Any, ShortSword, 2,  25, 0, Any, Shuriken, 3,
            // Soul Linker 4049
            0xD1, 0x0F, 0, None, 1,  0xD1, 0x0F, 0, ShortSword, 2,  0xD1, 0x0F, 0, Rod, 3,  0xD1, 0x0F, 0, TwoHandRod, 3,
            0xD1, 0x0F, 1, None, 1,  0xD1, 0x0F, 1, Rod, 2,  0xD1, 0x0F, 1, TwoHandRod, 2,  0xD1, 0x0F, 1, ShortSword, 3,
            // Summoner 4218
            0x7A, 0x10, Any, None, 1,  0x7A, 0x10, Any, Rod, 2,  0x7A, 0x10, Any, TwoHandRod, 2,
        };

        /// <summary>
        /// 1 to 3: <c>Attack1</c> to <c>Attack3</c>. <paramref name="job"/> is the job whose sprites the player uses (the base job of
        /// baby, high and transcendent jobs). A sex other than 0 or 1, or a job or weapon not listed: 1. With a second hand the row
        /// of the two weapons is used (short sword and short sword, sword and sword, axe and axe) when there is one.
        /// </summary>
        public static int NumberOf(int job, byte sex, int weaponType, bool secondHand)
        {
            if (sex > 1) return 1;
            if (secondHand)
            {
                int pair = weaponType == ShortSword ? ShortSwordShortSword : weaponType == Sword ? SwordSword : weaponType == Axe ? AxeAxe : -1;
                if (pair >= 0 && TryFind(job, sex, pair, out int n)) return n;
            }
            return TryFind(job, sex, weaponType, out int number) ? number : 1;
        }

        /// <summary>The attack motion's hit frame: 6 for every row so far (lead; T0.3 shots 1, 11, 13).</summary>
        public static float HitFrameOf(int job, byte sex, int weaponType) => DefaultHitFrame;

        static bool TryFind(int job, byte sex, int weaponType, out int number)
        {
            var rows = Rows;
            for (int i = 0; i + 4 < rows.Length; i += 5)
            {
                if ((rows[i] | (rows[i + 1] << 8)) != job || rows[i + 3] != weaponType) continue;
                if (rows[i + 2] != Any && rows[i + 2] != sex) continue;
                number = rows[i + 4];
                return true;
            }
            number = 1;
            return false;
        }
    }
}
