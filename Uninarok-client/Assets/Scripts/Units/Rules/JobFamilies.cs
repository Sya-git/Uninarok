using System;

namespace Uninarok.Units
{
    /// <summary>Job families the display and attack rules depend on (PLAN-0009 4.11, PLAN-0011 4.7, FR-15).</summary>
    public static class JobFamilies
    {
        /// <summary>The row job of the Taekwon and Star Gladiator family: <c>Attack2</c> for every weapon (D22).</summary>
        public const int Taekwon = 4046;

        const int Assassin = 12;   // JOB_ASSASSIN
        const int Stride = 5;
        const byte MaleRow = 1;    // flag bit 0: the row is the male one for either sex (D24)

        // Family table (PLAN-0011 4.7, D20). One entry of 5 bytes per job id that does not stand for itself, sorted by job id:
        //   job (ushort, little endian), row job (ushort, little endian), flag (bit 0: MaleRow).
        // An id that is not listed is its own row job (first classes, Taekwon 4046, Soul Linker 4049, Summoner 4218, Druid 4351;
        // and the jobs with no row, which play Attack1: Gangsi, Death Knight, Dark Collector, Wedding, Xmas, Summer, the outfit
        // jobs, WEREWOLF 4356 and WERERAPTOR 4357 (proposal, to confirm), D26).
        // Ids up to 4242: Hercules src/common/class.h and class_hidden.h (PlayerJob names). Ids above 4242: roBrowserLegacy
        // DB/Jobs/JobConst.js names and the WeaponAction.js copies, accepted untested (D27, "not run"). See combat-reference.md.
        // A property returning a constant array compiles to a span over static data: no allocation, no static field (UAL0010).
        static ReadOnlySpan<byte> Table => new byte[]
        {
            0xA1, 0x0F,  0x00, 0x00,  0,  // 4001 JOB_NOVICE_HIGH -> 0 JOB_NOVICE
            0xA2, 0x0F,  0x01, 0x00,  0,  // 4002 JOB_SWORDMAN_HIGH -> 1 JOB_SWORDMAN
            0xA3, 0x0F,  0x02, 0x00,  0,  // 4003 JOB_MAGE_HIGH -> 2 JOB_MAGE
            0xA4, 0x0F,  0x03, 0x00,  0,  // 4004 JOB_ARCHER_HIGH -> 3 JOB_ARCHER
            0xA5, 0x0F,  0x04, 0x00,  0,  // 4005 JOB_ACOLYTE_HIGH -> 4 JOB_ACOLYTE
            0xA6, 0x0F,  0x05, 0x00,  0,  // 4006 JOB_MERCHANT_HIGH -> 5 JOB_MERCHANT
            0xA7, 0x0F,  0x06, 0x00,  0,  // 4007 JOB_THIEF_HIGH -> 6 JOB_THIEF
            0xA8, 0x0F,  0x07, 0x00,  0,  // 4008 JOB_LORD_KNIGHT -> 7 JOB_KNIGHT
            0xA9, 0x0F,  0x08, 0x00,  0,  // 4009 JOB_HIGH_PRIEST -> 8 JOB_PRIEST
            0xAA, 0x0F,  0x09, 0x00,  0,  // 4010 JOB_HIGH_WIZARD -> 9 JOB_WIZARD
            0xAB, 0x0F,  0x0A, 0x00,  0,  // 4011 JOB_WHITESMITH -> 10 JOB_BLACKSMITH
            0xAC, 0x0F,  0x0B, 0x00,  0,  // 4012 JOB_SNIPER -> 11 JOB_HUNTER
            0xAD, 0x0F,  0x0C, 0x00,  0,  // 4013 JOB_ASSASSIN_CROSS -> 12 JOB_ASSASSIN
            0xAE, 0x0F,  0x0D, 0x00,  0,  // 4014 JOB_LORD_KNIGHT2 -> 13 JOB_KNIGHT2
            0xAF, 0x0F,  0x0E, 0x00,  0,  // 4015 JOB_PALADIN -> 14 JOB_CRUSADER
            0xB0, 0x0F,  0x0F, 0x00,  0,  // 4016 JOB_CHAMPION -> 15 JOB_MONK
            0xB1, 0x0F,  0x10, 0x00,  0,  // 4017 JOB_PROFESSOR -> 16 JOB_SAGE
            0xB2, 0x0F,  0x11, 0x00,  0,  // 4018 JOB_STALKER -> 17 JOB_ROGUE
            0xB3, 0x0F,  0x12, 0x00,  0,  // 4019 JOB_CREATOR -> 18 JOB_ALCHEMIST
            0xB4, 0x0F,  0x13, 0x00,  0,  // 4020 JOB_CLOWN -> 19 JOB_BARD
            0xB5, 0x0F,  0x14, 0x00,  0,  // 4021 JOB_GYPSY -> 20 JOB_DANCER
            0xB6, 0x0F,  0x15, 0x00,  0,  // 4022 JOB_PALADIN2 -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2
            0xB7, 0x0F,  0x00, 0x00,  0,  // 4023 JOB_BABY -> 0 JOB_NOVICE
            0xB8, 0x0F,  0x01, 0x00,  0,  // 4024 JOB_BABY_SWORDMAN -> 1 JOB_SWORDMAN
            0xB9, 0x0F,  0x02, 0x00,  0,  // 4025 JOB_BABY_MAGE -> 2 JOB_MAGE
            0xBA, 0x0F,  0x03, 0x00,  0,  // 4026 JOB_BABY_ARCHER -> 3 JOB_ARCHER
            0xBB, 0x0F,  0x04, 0x00,  0,  // 4027 JOB_BABY_ACOLYTE -> 4 JOB_ACOLYTE
            0xBC, 0x0F,  0x05, 0x00,  0,  // 4028 JOB_BABY_MERCHANT -> 5 JOB_MERCHANT
            0xBD, 0x0F,  0x06, 0x00,  0,  // 4029 JOB_BABY_THIEF -> 6 JOB_THIEF
            0xBE, 0x0F,  0x07, 0x00,  0,  // 4030 JOB_BABY_KNIGHT -> 7 JOB_KNIGHT
            0xBF, 0x0F,  0x08, 0x00,  0,  // 4031 JOB_BABY_PRIEST -> 8 JOB_PRIEST
            0xC0, 0x0F,  0x09, 0x00,  0,  // 4032 JOB_BABY_WIZARD -> 9 JOB_WIZARD
            0xC1, 0x0F,  0x0A, 0x00,  0,  // 4033 JOB_BABY_BLACKSMITH -> 10 JOB_BLACKSMITH
            0xC2, 0x0F,  0x0B, 0x00,  0,  // 4034 JOB_BABY_HUNTER -> 11 JOB_HUNTER
            0xC3, 0x0F,  0x0C, 0x00,  0,  // 4035 JOB_BABY_ASSASSIN -> 12 JOB_ASSASSIN
            0xC4, 0x0F,  0x0D, 0x00,  0,  // 4036 JOB_BABY_KNIGHT2 -> 13 JOB_KNIGHT2
            0xC5, 0x0F,  0x0E, 0x00,  0,  // 4037 JOB_BABY_CRUSADER -> 14 JOB_CRUSADER
            0xC6, 0x0F,  0x0F, 0x00,  0,  // 4038 JOB_BABY_MONK -> 15 JOB_MONK
            0xC7, 0x0F,  0x10, 0x00,  0,  // 4039 JOB_BABY_SAGE -> 16 JOB_SAGE
            0xC8, 0x0F,  0x11, 0x00,  0,  // 4040 JOB_BABY_ROGUE -> 17 JOB_ROGUE
            0xC9, 0x0F,  0x12, 0x00,  0,  // 4041 JOB_BABY_ALCHEMIST -> 18 JOB_ALCHEMIST
            0xCA, 0x0F,  0x13, 0x00,  0,  // 4042 JOB_BABY_BARD -> 19 JOB_BARD
            0xCB, 0x0F,  0x14, 0x00,  0,  // 4043 JOB_BABY_DANCER -> 20 JOB_DANCER
            0xCC, 0x0F,  0x15, 0x00,  0,  // 4044 JOB_BABY_CRUSADER2 -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2
            0xCD, 0x0F,  0x17, 0x00,  0,  // 4045 JOB_SUPER_BABY -> 23 JOB_SUPER_NOVICE
            0xCF, 0x0F,  0xCE, 0x0F,  0,  // 4047 JOB_STAR_GLADIATOR -> 4046 JOB_TAEKWON
            0xD0, 0x0F,  0xCE, 0x0F,  0,  // 4048 JOB_STAR_GLADIATOR2 -> 4046 JOB_TAEKWON
            0xD6, 0x0F,  0x07, 0x00,  0,  // 4054 JOB_RUNE_KNIGHT -> 7 JOB_KNIGHT
            0xD7, 0x0F,  0x09, 0x00,  0,  // 4055 JOB_WARLOCK -> 9 JOB_WIZARD
            0xD8, 0x0F,  0x0B, 0x00,  0,  // 4056 JOB_RANGER -> 11 JOB_HUNTER
            0xD9, 0x0F,  0x08, 0x00,  0,  // 4057 JOB_ARCH_BISHOP -> 8 JOB_PRIEST
            0xDA, 0x0F,  0x0A, 0x00,  0,  // 4058 JOB_MECHANIC -> 10 JOB_BLACKSMITH
            0xDB, 0x0F,  0x0C, 0x00,  0,  // 4059 JOB_GUILLOTINE_CROSS -> 12 JOB_ASSASSIN
            0xDC, 0x0F,  0x07, 0x00,  0,  // 4060 JOB_RUNE_KNIGHT_T -> 7 JOB_KNIGHT
            0xDD, 0x0F,  0x09, 0x00,  0,  // 4061 JOB_WARLOCK_T -> 9 JOB_WIZARD
            0xDE, 0x0F,  0x0B, 0x00,  0,  // 4062 JOB_RANGER_T -> 11 JOB_HUNTER
            0xDF, 0x0F,  0x08, 0x00,  0,  // 4063 JOB_ARCH_BISHOP_T -> 8 JOB_PRIEST
            0xE0, 0x0F,  0x0A, 0x00,  0,  // 4064 JOB_MECHANIC_T -> 10 JOB_BLACKSMITH
            0xE1, 0x0F,  0x0C, 0x00,  0,  // 4065 JOB_GUILLOTINE_CROSS_T -> 12 JOB_ASSASSIN
            0xE2, 0x0F,  0x0E, 0x00,  0,  // 4066 JOB_ROYAL_GUARD -> 14 JOB_CRUSADER
            0xE3, 0x0F,  0x10, 0x00,  0,  // 4067 JOB_SORCERER -> 16 JOB_SAGE
            0xE4, 0x0F,  0x13, 0x00,  0,  // 4068 JOB_MINSTREL -> 19 JOB_BARD
            0xE5, 0x0F,  0x14, 0x00,  0,  // 4069 JOB_WANDERER -> 20 JOB_DANCER
            0xE6, 0x0F,  0x0F, 0x00,  0,  // 4070 JOB_SURA -> 15 JOB_MONK
            0xE7, 0x0F,  0x12, 0x00,  0,  // 4071 JOB_GENETIC -> 18 JOB_ALCHEMIST
            0xE8, 0x0F,  0x11, 0x00,  0,  // 4072 JOB_SHADOW_CHASER -> 17 JOB_ROGUE
            0xE9, 0x0F,  0x0E, 0x00,  0,  // 4073 JOB_ROYAL_GUARD_T -> 14 JOB_CRUSADER
            0xEA, 0x0F,  0x10, 0x00,  0,  // 4074 JOB_SORCERER_T -> 16 JOB_SAGE
            0xEB, 0x0F,  0x13, 0x00,  0,  // 4075 JOB_MINSTREL_T -> 19 JOB_BARD
            0xEC, 0x0F,  0x14, 0x00,  0,  // 4076 JOB_WANDERER_T -> 20 JOB_DANCER
            0xED, 0x0F,  0x0F, 0x00,  0,  // 4077 JOB_SURA_T -> 15 JOB_MONK
            0xEE, 0x0F,  0x12, 0x00,  0,  // 4078 JOB_GENETIC_T -> 18 JOB_ALCHEMIST
            0xEF, 0x0F,  0x11, 0x00,  0,  // 4079 JOB_SHADOW_CHASER_T -> 17 JOB_ROGUE
            0xF0, 0x0F,  0x0D, 0x00,  0,  // 4080 JOB_RUNE_KNIGHT2 -> 13 JOB_KNIGHT2
            0xF1, 0x0F,  0x0D, 0x00,  0,  // 4081 JOB_RUNE_KNIGHT_T2 -> 13 JOB_KNIGHT2
            0xF2, 0x0F,  0x15, 0x00,  0,  // 4082 JOB_ROYAL_GUARD2 -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2
            0xF3, 0x0F,  0x15, 0x00,  0,  // 4083 JOB_ROYAL_GUARD_T2 -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2
            0xF4, 0x0F,  0x0B, 0x00,  0,  // 4084 JOB_RANGER2 -> 11 JOB_HUNTER
            0xF5, 0x0F,  0x0B, 0x00,  0,  // 4085 JOB_RANGER_T2 -> 11 JOB_HUNTER
            0xF6, 0x0F,  0x0A, 0x00,  0,  // 4086 JOB_MECHANIC2 -> 10 JOB_BLACKSMITH
            0xF7, 0x0F,  0x0A, 0x00,  0,  // 4087 JOB_MECHANIC_T2 -> 10 JOB_BLACKSMITH
            0xF8, 0x0F,  0x0D, 0x00,  0,  // 4088 JOB_RUNE_KNIGHT3 -> 13 JOB_KNIGHT2
            0xF9, 0x0F,  0x0D, 0x00,  0,  // 4089 JOB_RUNE_KNIGHT4 -> 13 JOB_KNIGHT2
            0xFA, 0x0F,  0x0D, 0x00,  0,  // 4090 JOB_RUNE_KNIGHT5 -> 13 JOB_KNIGHT2
            0xFB, 0x0F,  0x0D, 0x00,  0,  // 4091 JOB_RUNE_KNIGHT6 -> 13 JOB_KNIGHT2
            0xFC, 0x0F,  0x0D, 0x00,  0,  // 4092 JOB_RUNE_KNIGHT7 -> 13 JOB_KNIGHT2
            0xFD, 0x0F,  0x0D, 0x00,  0,  // 4093 JOB_RUNE_KNIGHT8 -> 13 JOB_KNIGHT2
            0xFE, 0x0F,  0x0D, 0x00,  0,  // 4094 JOB_RUNE_KNIGHT9 -> 13 JOB_KNIGHT2
            0xFF, 0x0F,  0x0D, 0x00,  0,  // 4095 JOB_RUNE_KNIGHT10 -> 13 JOB_KNIGHT2
            0x00, 0x10,  0x07, 0x00,  0,  // 4096 JOB_BABY_RUNE -> 7 JOB_KNIGHT
            0x01, 0x10,  0x09, 0x00,  0,  // 4097 JOB_BABY_WARLOCK -> 9 JOB_WIZARD
            0x02, 0x10,  0x0B, 0x00,  0,  // 4098 JOB_BABY_RANGER -> 11 JOB_HUNTER
            0x03, 0x10,  0x08, 0x00,  0,  // 4099 JOB_BABY_BISHOP -> 8 JOB_PRIEST
            0x04, 0x10,  0x0A, 0x00,  0,  // 4100 JOB_BABY_MECHANIC -> 10 JOB_BLACKSMITH
            0x05, 0x10,  0x0C, 0x00,  0,  // 4101 JOB_BABY_CROSS -> 12 JOB_ASSASSIN
            0x06, 0x10,  0x0E, 0x00,  0,  // 4102 JOB_BABY_GUARD -> 14 JOB_CRUSADER
            0x07, 0x10,  0x10, 0x00,  0,  // 4103 JOB_BABY_SORCERER -> 16 JOB_SAGE
            0x08, 0x10,  0x13, 0x00,  0,  // 4104 JOB_BABY_MINSTREL -> 19 JOB_BARD
            0x09, 0x10,  0x14, 0x00,  0,  // 4105 JOB_BABY_WANDERER -> 20 JOB_DANCER
            0x0A, 0x10,  0x0F, 0x00,  0,  // 4106 JOB_BABY_SURA -> 15 JOB_MONK
            0x0B, 0x10,  0x12, 0x00,  0,  // 4107 JOB_BABY_GENETIC -> 18 JOB_ALCHEMIST
            0x0C, 0x10,  0x11, 0x00,  0,  // 4108 JOB_BABY_CHASER -> 17 JOB_ROGUE
            0x0D, 0x10,  0x0D, 0x00,  0,  // 4109 JOB_BABY_RUNE2 -> 13 JOB_KNIGHT2
            0x0E, 0x10,  0x15, 0x00,  0,  // 4110 JOB_BABY_GUARD2 -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2
            0x0F, 0x10,  0x0B, 0x00,  0,  // 4111 JOB_BABY_RANGER2 -> 11 JOB_HUNTER
            0x10, 0x10,  0x0A, 0x00,  0,  // 4112 JOB_BABY_MECHANIC2 -> 10 JOB_BLACKSMITH
            0x5E, 0x10,  0x17, 0x00,  0,  // 4190 JOB_SUPER_NOVICE_E -> 23 JOB_SUPER_NOVICE
            0x5F, 0x10,  0x17, 0x00,  0,  // 4191 JOB_SUPER_BABY_E -> 23 JOB_SUPER_NOVICE
            0x73, 0x10,  0x19, 0x00,  0,  // 4211 JOB_KAGEROU -> 25 JOB_NINJA
            0x74, 0x10,  0x19, 0x00,  0,  // 4212 JOB_OBORO -> 25 JOB_NINJA
            0x77, 0x10,  0x18, 0x00,  0,  // 4215 JOB_REBELLION -> 24 JOB_GUNSLINGER
            0x7B, 0x10,  0x7A, 0x10,  0,  // 4219 JOB_DUMMY4219 -> 4218 JOB_SUMMONER; Hercules JOB_DUMMY4219; roBrowserLegacy DO_CART_SUMMONER1 copies DO_SUMMONER; proposal, to confirm
            0x7C, 0x10,  0x7A, 0x10,  0,  // 4220 JOB_BABY_SUMMONER -> 4218 JOB_SUMMONER
            0x7E, 0x10,  0x19, 0x00,  0,  // 4222 JOB_BABY_NINJA -> 25 JOB_NINJA
            0x7F, 0x10,  0x19, 0x00,  0,  // 4223 JOB_BABY_KAGEROU -> 25 JOB_NINJA
            0x80, 0x10,  0x19, 0x00,  0,  // 4224 JOB_BABY_OBORO -> 25 JOB_NINJA
            0x81, 0x10,  0xCE, 0x0F,  0,  // 4225 JOB_BABY_TAEKWON -> 4046 JOB_TAEKWON
            0x82, 0x10,  0xCE, 0x0F,  0,  // 4226 JOB_BABY_STAR_GLADIATOR -> 4046 JOB_TAEKWON
            0x83, 0x10,  0xD1, 0x0F,  0,  // 4227 JOB_BABY_SOUL_LINKER -> 4049 JOB_SOUL_LINKER
            0x84, 0x10,  0x18, 0x00,  0,  // 4228 JOB_BABY_GUNSLINGER -> 24 JOB_GUNSLINGER
            0x85, 0x10,  0x18, 0x00,  0,  // 4229 JOB_BABY_REBELLION -> 24 JOB_GUNSLINGER
            0x8E, 0x10,  0xCE, 0x0F,  0,  // 4238 JOB_BABY_STAR_GLADIATOR2 -> 4046 JOB_TAEKWON
            0x8F, 0x10,  0xCE, 0x0F,  0,  // 4239 JOB_STAR_EMPEROR -> 4046 JOB_TAEKWON
            0x90, 0x10,  0x09, 0x00,  1,  // 4240 JOB_SOUL_REAPER -> 9 JOB_WIZARD, male row
            0x91, 0x10,  0xCE, 0x0F,  0,  // 4241 JOB_BABY_STAR_EMPEROR -> 4046 JOB_TAEKWON
            0x92, 0x10,  0x09, 0x00,  1,  // 4242 JOB_BABY_SOUL_REAPER -> 9 JOB_WIZARD, male row
            0x93, 0x10,  0xCE, 0x0F,  0,  // 4243 STAR_EMPEROR2 -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0x94, 0x10,  0xCE, 0x0F,  0,  // 4244 STAR_EMPEROR2_B -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0x95, 0x10,  0xCE, 0x0F,  0,  // 4245 HAETAE_STAR_EMPEROR -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0x96, 0x10,  0x09, 0x00,  1,  // 4246 HAETAE_SOUL_REAPER -> 9 JOB_WIZARD, male row (roBrowserLegacy, not run); no WeaponAction entry; Soul Reaper family by its name; proposal, to confirm
            0x97, 0x10,  0xCE, 0x0F,  0,  // 4247 HAETAE_STAR_EMPEROR_B -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0x98, 0x10,  0x09, 0x00,  1,  // 4248 HAETAE_SOUL_REAPER_B -> 9 JOB_WIZARD, male row (roBrowserLegacy, not run); no WeaponAction entry; Soul Reaper family by its name; proposal, to confirm
            0x9C, 0x10,  0x07, 0x00,  0,  // 4252 DRAGON_KNIGHT -> 7 JOB_KNIGHT (roBrowserLegacy, not run)
            0x9D, 0x10,  0x0A, 0x00,  0,  // 4253 MEISTER -> 10 JOB_BLACKSMITH (roBrowserLegacy, not run)
            0x9E, 0x10,  0x0C, 0x00,  0,  // 4254 SHADOW_CROSS -> 12 JOB_ASSASSIN (roBrowserLegacy, not run)
            0x9F, 0x10,  0x09, 0x00,  0,  // 4255 ARCH_MAGE -> 9 JOB_WIZARD (roBrowserLegacy, not run)
            0xA0, 0x10,  0x08, 0x00,  0,  // 4256 CARDINAL -> 8 JOB_PRIEST (roBrowserLegacy, not run)
            0xA1, 0x10,  0x0B, 0x00,  0,  // 4257 WINDHAWK -> 11 JOB_HUNTER (roBrowserLegacy, not run)
            0xA2, 0x10,  0x0E, 0x00,  0,  // 4258 IMPERIAL_GUARD -> 14 JOB_CRUSADER (roBrowserLegacy, not run)
            0xA3, 0x10,  0x12, 0x00,  0,  // 4259 BIOLO -> 18 JOB_ALCHEMIST (roBrowserLegacy, not run)
            0xA4, 0x10,  0x11, 0x00,  0,  // 4260 ABYSS_CHASER -> 17 JOB_ROGUE (roBrowserLegacy, not run)
            0xA5, 0x10,  0x10, 0x00,  0,  // 4261 ELEMENTAL_MASTER -> 16 JOB_SAGE (roBrowserLegacy, not run)
            0xA6, 0x10,  0x0F, 0x00,  0,  // 4262 INQUISITOR -> 15 JOB_MONK (roBrowserLegacy, not run)
            0xA7, 0x10,  0x13, 0x00,  0,  // 4263 TROUBADOUR -> 19 JOB_BARD (roBrowserLegacy, not run)
            0xA8, 0x10,  0x14, 0x00,  0,  // 4264 TROUVERE -> 20 JOB_DANCER (roBrowserLegacy, not run)
            0xA9, 0x10,  0x07, 0x00,  0,  // 4265 DRAGON_KNIGHT_RIDING -> 7 JOB_KNIGHT (roBrowserLegacy, not run)
            0xAA, 0x10,  0x0A, 0x00,  0,  // 4266 MEISTER_RIDING -> 10 JOB_BLACKSMITH (roBrowserLegacy, not run)
            0xAB, 0x10,  0x0C, 0x00,  0,  // 4267 SHADOW_CROSS_RIDING -> 12 JOB_ASSASSIN (roBrowserLegacy, not run)
            0xAC, 0x10,  0x09, 0x00,  0,  // 4268 ARCH_MAGE_RIDING -> 9 JOB_WIZARD (roBrowserLegacy, not run)
            0xAD, 0x10,  0x08, 0x00,  0,  // 4269 CARDINAL_RIDING -> 8 JOB_PRIEST (roBrowserLegacy, not run)
            0xAE, 0x10,  0x0B, 0x00,  0,  // 4270 WINDHAWK_RIDING -> 11 JOB_HUNTER (roBrowserLegacy, not run)
            0xAF, 0x10,  0x0E, 0x00,  0,  // 4271 IMPERIAL_GUARD_RIDING -> 14 JOB_CRUSADER (roBrowserLegacy, not run)
            0xB0, 0x10,  0x12, 0x00,  0,  // 4272 BIOLO_RIDING -> 18 JOB_ALCHEMIST (roBrowserLegacy, not run)
            0xB1, 0x10,  0x11, 0x00,  0,  // 4273 ABYSS_CHASER_RIDING -> 17 JOB_ROGUE (roBrowserLegacy, not run)
            0xB2, 0x10,  0x10, 0x00,  0,  // 4274 ELEMENTAL_MASTER_RIDING -> 16 JOB_SAGE (roBrowserLegacy, not run)
            0xB3, 0x10,  0x0F, 0x00,  0,  // 4275 INQUISITOR_RIDING -> 15 JOB_MONK (roBrowserLegacy, not run)
            0xB4, 0x10,  0x13, 0x00,  0,  // 4276 TROUBADOUR_RIDING -> 19 JOB_BARD (roBrowserLegacy, not run)
            0xB5, 0x10,  0x14, 0x00,  0,  // 4277 TROUVERE_RIDING -> 20 JOB_DANCER (roBrowserLegacy, not run)
            0xB6, 0x10,  0x0B, 0x00,  0,  // 4278 WINDHAWK2 -> 11 JOB_HUNTER (roBrowserLegacy, not run)
            0xB7, 0x10,  0x0A, 0x00,  0,  // 4279 MEISTER2 -> 10 JOB_BLACKSMITH (roBrowserLegacy, not run)
            0xB8, 0x10,  0x0D, 0x00,  0,  // 4280 DRAGON_KNIGHT2 -> 13 JOB_KNIGHT2 (roBrowserLegacy, not run)
            0xB9, 0x10,  0x15, 0x00,  0,  // 4281 IMPERIAL_GUARD2 -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2 (roBrowserLegacy, not run)
            0xCE, 0x10,  0xCE, 0x0F,  0,  // 4302 SKY_EMPEROR -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0xCF, 0x10,  0x09, 0x00,  1,  // 4303 SOUL_ASCETIC -> 9 JOB_WIZARD, male row (roBrowserLegacy, not run)
            0xD0, 0x10,  0x19, 0x00,  0,  // 4304 SHINKIRO -> 25 JOB_NINJA (roBrowserLegacy, not run)
            0xD1, 0x10,  0x19, 0x00,  0,  // 4305 SHIRANUI -> 25 JOB_NINJA (roBrowserLegacy, not run)
            0xD2, 0x10,  0x18, 0x00,  0,  // 4306 NIGHT_WATCH -> 24 JOB_GUNSLINGER (roBrowserLegacy, not run)
            0xD3, 0x10,  0x17, 0x00,  0,  // 4307 HYPER_NOVICE -> 23 JOB_SUPER_NOVICE (roBrowserLegacy, not run)
            0xD4, 0x10,  0x7A, 0x10,  0,  // 4308 SPIRIT_HANDLER -> 4218 JOB_SUMMONER (roBrowserLegacy, not run)
            0xD5, 0x10,  0xCE, 0x0F,  0,  // 4309 SKY_EMPEROR_RIDING -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0xD6, 0x10,  0x09, 0x00,  1,  // 4310 SOUL_ASCETIC_RIDING -> 9 JOB_WIZARD, male row (roBrowserLegacy, not run)
            0xD7, 0x10,  0x19, 0x00,  0,  // 4311 SHINKIRO_RIDING -> 25 JOB_NINJA (roBrowserLegacy, not run)
            0xD8, 0x10,  0x19, 0x00,  0,  // 4312 SHIRANUI_RIDING -> 25 JOB_NINJA (roBrowserLegacy, not run)
            0xD9, 0x10,  0x18, 0x00,  0,  // 4313 NIGHT_WATCH_RIDING -> 24 JOB_GUNSLINGER (roBrowserLegacy, not run)
            0xDA, 0x10,  0x17, 0x00,  0,  // 4314 HYPER_NOVICE_RIDING -> 23 JOB_SUPER_NOVICE (roBrowserLegacy, not run)
            0xDB, 0x10,  0x7A, 0x10,  0,  // 4315 SPIRIT_HANDLER_RIDING1 -> 4218 JOB_SUMMONER (roBrowserLegacy, not run)
            0xDC, 0x10,  0xCE, 0x0F,  0,  // 4316 SKY_EMPEROR2 -> 4046 JOB_TAEKWON (roBrowserLegacy, not run); no WeaponAction entry; Taekwon family by D22; proposal, to confirm
            0xEC, 0x10,  0x07, 0x00,  0,  // 4332 RUNE_KNIGHT_2ND -> 7 JOB_KNIGHT (roBrowserLegacy, not run)
            0xED, 0x10,  0x0A, 0x00,  0,  // 4333 MECHANIC_2ND -> 10 JOB_BLACKSMITH (roBrowserLegacy, not run)
            0xEE, 0x10,  0x0C, 0x00,  0,  // 4334 GUILLOTINE_CROSS_2ND -> 12 JOB_ASSASSIN (roBrowserLegacy, not run)
            0xEF, 0x10,  0x09, 0x00,  0,  // 4335 WARLOCK_2ND -> 9 JOB_WIZARD (roBrowserLegacy, not run)
            0xF0, 0x10,  0x08, 0x00,  0,  // 4336 ARCH_BISHOP_2ND -> 8 JOB_PRIEST (roBrowserLegacy, not run); WeaponAction.js names ARCHBISHOP_2ND, an undefined constant (source bug): Priest row as intended; proposal, to confirm
            0xF1, 0x10,  0x0B, 0x00,  0,  // 4337 RANGER_2ND -> 11 JOB_HUNTER (roBrowserLegacy, not run)
            0xF2, 0x10,  0x0E, 0x00,  0,  // 4338 ROYAL_GUARD_2ND -> 14 JOB_CRUSADER (roBrowserLegacy, not run)
            0xF3, 0x10,  0x12, 0x00,  0,  // 4339 GENETIC_2ND -> 18 JOB_ALCHEMIST (roBrowserLegacy, not run)
            0xF4, 0x10,  0x11, 0x00,  0,  // 4340 SHADOW_CHASER_2ND -> 17 JOB_ROGUE (roBrowserLegacy, not run)
            0xF5, 0x10,  0x10, 0x00,  0,  // 4341 SORCERER_2ND -> 16 JOB_SAGE (roBrowserLegacy, not run)
            0xF6, 0x10,  0x0F, 0x00,  0,  // 4342 SURA_2ND -> 15 JOB_MONK (roBrowserLegacy, not run)
            0xF7, 0x10,  0x13, 0x00,  0,  // 4343 MINSTREL_2ND -> 19 JOB_BARD (roBrowserLegacy, not run)
            0xF8, 0x10,  0x14, 0x00,  0,  // 4344 WANDERER_2ND -> 20 JOB_DANCER (roBrowserLegacy, not run)
            0xF9, 0x10,  0x0D, 0x00,  0,  // 4345 RUNE_KNIGHT2_2ND -> 13 JOB_KNIGHT2 (roBrowserLegacy, not run)
            0xFA, 0x10,  0x0B, 0x00,  0,  // 4346 RANGER2_2ND -> 11 JOB_HUNTER (roBrowserLegacy, not run)
            0xFB, 0x10,  0x0A, 0x00,  0,  // 4347 MECHANIC2_2ND -> 10 JOB_BLACKSMITH (roBrowserLegacy, not run)
            0xFC, 0x10,  0x15, 0x00,  0,  // 4348 ROYAL_GUARD2_2ND -> 21 JOB_CRUSADER2, D25: WeaponAction.js copies CRUSADER2 (roBrowserLegacy, not run)
            0x00, 0x11,  0xFF, 0x10,  0,  // 4352 DRUID_B -> 4351 DRUID (roBrowserLegacy, not run)
            0x01, 0x11,  0xFF, 0x10,  0,  // 4353 KARNOS -> 4351 DRUID (roBrowserLegacy, not run)
            0x02, 0x11,  0xFF, 0x10,  0,  // 4354 KARNOS_B -> 4351 DRUID (roBrowserLegacy, not run)
            0x03, 0x11,  0xFF, 0x10,  0,  // 4355 ALITEA -> 4351 DRUID (roBrowserLegacy, not run)
            0x06, 0x11,  0xFF, 0x10,  0,  // 4358 DRUID_RIDING -> 4351 DRUID (roBrowserLegacy, not run)
            0x07, 0x11,  0xFF, 0x10,  0,  // 4359 KARNOS_RIDING -> 4351 DRUID (roBrowserLegacy, not run)
            0x08, 0x11,  0xFF, 0x10,  0,  // 4360 ALITEA_RIDING -> 4351 DRUID (roBrowserLegacy, not run)
        };

        /// <summary>The job id whose attack row <paramref name="job"/> uses (PLAN-0011 4.7); <paramref name="job"/> itself when none.</summary>
        public static int RowJobOf(int job)
        {
            int at = Find(job);
            if (at < 0) return job;
            var t = Table;
            return t[at + 2] | (t[at + 3] << 8);
        }

        /// <summary>Male (1) for the Soul Reaper family, <paramref name="sex"/> otherwise (PLAN-0011 4.7, D24).</summary>
        public static byte RowSexOf(int job, byte sex)
        {
            int at = Find(job);
            return at >= 0 && (Table[at + 4] & MaleRow) != 0 ? (byte)1 : sex;
        }

        /// <summary>Every job whose row job is the Assassin's (12): Assassin, Assassin Cross, the babies, Guillotine Cross and
        /// Shadow Cross with their variants (PLAN-0011 4.7, PLAN-0009 R16).</summary>
        public static bool IsAssassinFamily(int job) => RowJobOf(job) == Assassin;

        // Byte offset of job's entry, or -1. Binary search; ids outside 0..65535 (a hostile or unknown value) are never listed.
        static int Find(int job)
        {
            if ((uint)job > ushort.MaxValue) return -1;
            var t = Table;
            int lo = 0, hi = t.Length / Stride - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int at = mid * Stride;
                int id = t[at] | (t[at + 1] << 8);
                if (id == job) return at;
                if (id < job) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }
    }
}
