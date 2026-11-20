# PlayerJob

- **Kind:** Shared enum
- **Underlying type:** `short`
- **Description:** Player class ids used by networking (`CharInfo.Class`) and by the sprite catalog (bodies, class tables, equipment slots). Monster, NPC, homunculus and mercenary ids are a different id space and are not members.
- **Source of the values:** Hercules `src/common/class.h` (`JOB_ENUM_VALUE(NAME, id, msg)`) and `src/common/class_hidden.h` (second forms of mounted classes). Member name = `JOB_` + the Hercules constant. Hand-written; checked against the client's `JTtbl` by `PlayerJobTests`.

| Name | Value | Description |
|---|---:|---|
| `JOB_NOVICE` | `0` | Name in messages: `JOB_NOVICE` (`class.h`). |
| `JOB_SWORDMAN` | `1` | Name in messages: `JOB_SWORDMAN` (`class.h`). |
| `JOB_MAGE` | `2` | Name in messages: `JOB_MAGE` (`class.h`). |
| `JOB_ARCHER` | `3` | Name in messages: `JOB_ARCHER` (`class.h`). |
| `JOB_ACOLYTE` | `4` | Name in messages: `JOB_ACOLYTE` (`class.h`). |
| `JOB_MERCHANT` | `5` | Name in messages: `JOB_MERCHANT` (`class.h`). |
| `JOB_THIEF` | `6` | Name in messages: `JOB_THIEF` (`class.h`). |
| `JOB_KNIGHT` | `7` | Name in messages: `JOB_KNIGHT` (`class.h`). |
| `JOB_PRIEST` | `8` | Name in messages: `JOB_PRIEST` (`class.h`). |
| `JOB_WIZARD` | `9` | Name in messages: `JOB_WIZARD` (`class.h`). |
| `JOB_BLACKSMITH` | `10` | Name in messages: `JOB_BLACKSMITH` (`class.h`). |
| `JOB_HUNTER` | `11` | Name in messages: `JOB_HUNTER` (`class.h`). |
| `JOB_ASSASSIN` | `12` | Name in messages: `JOB_ASSASSIN` (`class.h`). |
| `JOB_KNIGHT2` | `13` | Second (mounted) form; name in messages: `JOB_KNIGHT` (`class_hidden.h`). |
| `JOB_CRUSADER` | `14` | Name in messages: `JOB_CRUSADER` (`class.h`). |
| `JOB_MONK` | `15` | Name in messages: `JOB_MONK` (`class.h`). |
| `JOB_SAGE` | `16` | Name in messages: `JOB_SAGE` (`class.h`). |
| `JOB_ROGUE` | `17` | Name in messages: `JOB_ROGUE` (`class.h`). |
| `JOB_ALCHEMIST` | `18` | Name in messages: `JOB_ALCHEMIST` (`class.h`). |
| `JOB_CRUSADER2` | `21` | Second (mounted) form; name in messages: `JOB_CRUSADER` (`class_hidden.h`). |
| `JOB_WEDDING` | `22` | Name in messages: `JOB_WEDDING` (`class.h`). |
| `JOB_SUPER_NOVICE` | `23` | Name in messages: `JOB_SUPER_NOVICE` (`class.h`). |
| `JOB_GUNSLINGER` | `24` | Name in messages: `JOB_GUNSLINGER` (`class.h`). |
| `JOB_NINJA` | `25` | Name in messages: `JOB_NINJA` (`class.h`). |
| `JOB_XMAS` | `26` | Name in messages: `JOB_XMAS` (`class.h`). |
| `JOB_SUMMER` | `27` | Name in messages: `JOB_SUMMER` (`class.h`). |
| `JOB_NOVICE_HIGH` | `4001` | Name in messages: `JOB_NOVICE_HIGH` (`class.h`). |
| `JOB_SWORDMAN_HIGH` | `4002` | Name in messages: `JOB_SWORDMAN_HIGH` (`class.h`). |
| `JOB_MAGE_HIGH` | `4003` | Name in messages: `JOB_MAGE_HIGH` (`class.h`). |
| `JOB_ARCHER_HIGH` | `4004` | Name in messages: `JOB_ARCHER_HIGH` (`class.h`). |
| `JOB_ACOLYTE_HIGH` | `4005` | Name in messages: `JOB_ACOLYTE_HIGH` (`class.h`). |
| `JOB_MERCHANT_HIGH` | `4006` | Name in messages: `JOB_MERCHANT_HIGH` (`class.h`). |
| `JOB_THIEF_HIGH` | `4007` | Name in messages: `JOB_THIEF_HIGH` (`class.h`). |
| `JOB_LORD_KNIGHT` | `4008` | Name in messages: `JOB_LORD_KNIGHT` (`class.h`). |
| `JOB_HIGH_PRIEST` | `4009` | Name in messages: `JOB_HIGH_PRIEST` (`class.h`). |
| `JOB_HIGH_WIZARD` | `4010` | Name in messages: `JOB_HIGH_WIZARD` (`class.h`). |
| `JOB_WHITESMITH` | `4011` | Name in messages: `JOB_WHITESMITH` (`class.h`). |
| `JOB_SNIPER` | `4012` | Name in messages: `JOB_SNIPER` (`class.h`). |
| `JOB_ASSASSIN_CROSS` | `4013` | Name in messages: `JOB_ASSASSIN_CROSS` (`class.h`). |
| `JOB_LORD_KNIGHT2` | `4014` | Second (mounted) form; name in messages: `JOB_LORD_KNIGHT` (`class_hidden.h`). |
| `JOB_PALADIN` | `4015` | Name in messages: `JOB_PALADIN` (`class.h`). |
| `JOB_CHAMPION` | `4016` | Name in messages: `JOB_CHAMPION` (`class.h`). |
| `JOB_PROFESSOR` | `4017` | Name in messages: `JOB_PROFESSOR` (`class.h`). |
| `JOB_STALKER` | `4018` | Name in messages: `JOB_STALKER` (`class.h`). |
| `JOB_CREATOR` | `4019` | Name in messages: `JOB_CREATOR` (`class.h`). |
| `JOB_PALADIN2` | `4022` | Second (mounted) form; name in messages: `JOB_PALADIN` (`class_hidden.h`). |
| `JOB_BABY` | `4023` | Name in messages: `JOB_BABY` (`class.h`). |
| `JOB_BABY_SWORDMAN` | `4024` | Name in messages: `JOB_BABY_SWORDMAN` (`class.h`). |
| `JOB_BABY_MAGE` | `4025` | Name in messages: `JOB_BABY_MAGE` (`class.h`). |
| `JOB_BABY_ARCHER` | `4026` | Name in messages: `JOB_BABY_ARCHER` (`class.h`). |
| `JOB_BABY_ACOLYTE` | `4027` | Name in messages: `JOB_BABY_ACOLYTE` (`class.h`). |
| `JOB_BABY_MERCHANT` | `4028` | Name in messages: `JOB_BABY_MERCHANT` (`class.h`). |
| `JOB_BABY_THIEF` | `4029` | Name in messages: `JOB_BABY_THIEF` (`class.h`). |
| `JOB_BABY_KNIGHT` | `4030` | Name in messages: `JOB_BABY_KNIGHT` (`class.h`). |
| `JOB_BABY_PRIEST` | `4031` | Name in messages: `JOB_BABY_PRIEST` (`class.h`). |
| `JOB_BABY_WIZARD` | `4032` | Name in messages: `JOB_BABY_WIZARD` (`class.h`). |
| `JOB_BABY_BLACKSMITH` | `4033` | Name in messages: `JOB_BABY_BLACKSMITH` (`class.h`). |
| `JOB_BABY_HUNTER` | `4034` | Name in messages: `JOB_BABY_HUNTER` (`class.h`). |
| `JOB_BABY_ASSASSIN` | `4035` | Name in messages: `JOB_BABY_ASSASSIN` (`class.h`). |
| `JOB_BABY_KNIGHT2` | `4036` | Second (mounted) form; name in messages: `JOB_BABY_KNIGHT` (`class_hidden.h`). |
| `JOB_BABY_CRUSADER` | `4037` | Name in messages: `JOB_BABY_CRUSADER` (`class.h`). |
| `JOB_BABY_MONK` | `4038` | Name in messages: `JOB_BABY_MONK` (`class.h`). |
| `JOB_BABY_SAGE` | `4039` | Name in messages: `JOB_BABY_SAGE` (`class.h`). |
| `JOB_BABY_ROGUE` | `4040` | Name in messages: `JOB_BABY_ROGUE` (`class.h`). |
| `JOB_BABY_ALCHEMIST` | `4041` | Name in messages: `JOB_BABY_ALCHEMIST` (`class.h`). |
| `JOB_BABY_CRUSADER2` | `4044` | Second (mounted) form; name in messages: `JOB_BABY_CRUSADER` (`class_hidden.h`). |
| `JOB_SUPER_BABY` | `4045` | Name in messages: `JOB_SUPER_BABY` (`class.h`). |
| `JOB_TAEKWON` | `4046` | Name in messages: `JOB_TAEKWON` (`class.h`). |
| `JOB_STAR_GLADIATOR` | `4047` | Name in messages: `JOB_STAR_GLADIATOR` (`class.h`). |
| `JOB_STAR_GLADIATOR2` | `4048` | Second (mounted) form; name in messages: `JOB_STAR_GLADIATOR` (`class_hidden.h`). |
| `JOB_SOUL_LINKER` | `4049` | Name in messages: `JOB_SOUL_LINKER` (`class.h`). |
| `JOB_GANGSI` | `4050` | Name in messages: `JOB_GANGSI` (`class.h`). |
| `JOB_DEATH_KNIGHT` | `4051` | Name in messages: `JOB_DEATH_KNIGHT` (`class.h`). |
| `JOB_DARK_COLLECTOR` | `4052` | Name in messages: `JOB_DARK_COLLECTOR` (`class.h`). |
| `JOB_RUNE_KNIGHT` | `4054` | Name in messages: `JOB_RUNE_KNIGHT` (`class.h`). |
| `JOB_WARLOCK` | `4055` | Name in messages: `JOB_WARLOCK` (`class.h`). |
| `JOB_RANGER` | `4056` | Name in messages: `JOB_RANGER` (`class.h`). |
| `JOB_ARCH_BISHOP` | `4057` | Name in messages: `JOB_ARCH_BISHOP` (`class.h`). |
| `JOB_MECHANIC` | `4058` | Name in messages: `JOB_MECHANIC` (`class.h`). |
| `JOB_GUILLOTINE_CROSS` | `4059` | Name in messages: `JOB_GUILLOTINE_CROSS` (`class.h`). |
| `JOB_RUNE_KNIGHT_T` | `4060` | Name in messages: `JOB_RUNE_KNIGHT_T` (`class.h`). |
| `JOB_WARLOCK_T` | `4061` | Name in messages: `JOB_WARLOCK_T` (`class.h`). |
| `JOB_RANGER_T` | `4062` | Name in messages: `JOB_RANGER_T` (`class.h`). |
| `JOB_ARCH_BISHOP_T` | `4063` | Name in messages: `JOB_ARCH_BISHOP_T` (`class.h`). |
| `JOB_MECHANIC_T` | `4064` | Name in messages: `JOB_MECHANIC_T` (`class.h`). |
| `JOB_GUILLOTINE_CROSS_T` | `4065` | Name in messages: `JOB_GUILLOTINE_CROSS_T` (`class.h`). |
| `JOB_ROYAL_GUARD` | `4066` | Name in messages: `JOB_ROYAL_GUARD` (`class.h`). |
| `JOB_SORCERER` | `4067` | Name in messages: `JOB_SORCERER` (`class.h`). |
| `JOB_SURA` | `4070` | Name in messages: `JOB_SURA` (`class.h`). |
| `JOB_GENETIC` | `4071` | Name in messages: `JOB_GENETIC` (`class.h`). |
| `JOB_SHADOW_CHASER` | `4072` | Name in messages: `JOB_SHADOW_CHASER` (`class.h`). |
| `JOB_ROYAL_GUARD_T` | `4073` | Name in messages: `JOB_ROYAL_GUARD_T` (`class.h`). |
| `JOB_SORCERER_T` | `4074` | Name in messages: `JOB_SORCERER_T` (`class.h`). |
| `JOB_SURA_T` | `4077` | Name in messages: `JOB_SURA_T` (`class.h`). |
| `JOB_GENETIC_T` | `4078` | Name in messages: `JOB_GENETIC_T` (`class.h`). |
| `JOB_SHADOW_CHASER_T` | `4079` | Name in messages: `JOB_SHADOW_CHASER_T` (`class.h`). |
| `JOB_RUNE_KNIGHT2` | `4080` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT_T2` | `4081` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT_T` (`class_hidden.h`). |
| `JOB_ROYAL_GUARD2` | `4082` | Second (mounted) form; name in messages: `JOB_ROYAL_GUARD` (`class_hidden.h`). |
| `JOB_ROYAL_GUARD_T2` | `4083` | Second (mounted) form; name in messages: `JOB_ROYAL_GUARD_T` (`class_hidden.h`). |
| `JOB_RANGER2` | `4084` | Second (mounted) form; name in messages: `JOB_RANGER` (`class_hidden.h`). |
| `JOB_RANGER_T2` | `4085` | Second (mounted) form; name in messages: `JOB_RANGER_T` (`class_hidden.h`). |
| `JOB_MECHANIC2` | `4086` | Second (mounted) form; name in messages: `JOB_MECHANIC` (`class_hidden.h`). |
| `JOB_MECHANIC_T2` | `4087` | Second (mounted) form; name in messages: `JOB_MECHANIC_T` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT3` | `4088` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT4` | `4089` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT5` | `4090` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT6` | `4091` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT7` | `4092` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT8` | `4093` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT9` | `4094` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_RUNE_KNIGHT10` | `4095` | Second (mounted) form; name in messages: `JOB_RUNE_KNIGHT` (`class_hidden.h`). |
| `JOB_BABY_RUNE` | `4096` | Name in messages: `JOB_BABY_RUNE` (`class.h`). |
| `JOB_BABY_WARLOCK` | `4097` | Name in messages: `JOB_BABY_WARLOCK` (`class.h`). |
| `JOB_BABY_RANGER` | `4098` | Name in messages: `JOB_BABY_RANGER` (`class.h`). |
| `JOB_BABY_BISHOP` | `4099` | Name in messages: `JOB_BABY_BISHOP` (`class.h`). |
| `JOB_BABY_MECHANIC` | `4100` | Name in messages: `JOB_BABY_MECHANIC` (`class.h`). |
| `JOB_BABY_CROSS` | `4101` | Name in messages: `JOB_BABY_CROSS` (`class.h`). |
| `JOB_BABY_GUARD` | `4102` | Name in messages: `JOB_BABY_GUARD` (`class.h`). |
| `JOB_BABY_SORCERER` | `4103` | Name in messages: `JOB_BABY_SORCERER` (`class.h`). |
| `JOB_BABY_SURA` | `4106` | Name in messages: `JOB_BABY_SURA` (`class.h`). |
| `JOB_BABY_GENETIC` | `4107` | Name in messages: `JOB_BABY_GENETIC` (`class.h`). |
| `JOB_BABY_CHASER` | `4108` | Name in messages: `JOB_BABY_CHASER` (`class.h`). |
| `JOB_BABY_RUNE2` | `4109` | Second (mounted) form; name in messages: `JOB_BABY_RUNE` (`class_hidden.h`). |
| `JOB_BABY_GUARD2` | `4110` | Second (mounted) form; name in messages: `JOB_BABY_GUARD` (`class_hidden.h`). |
| `JOB_BABY_RANGER2` | `4111` | Second (mounted) form; name in messages: `JOB_BABY_RANGER` (`class_hidden.h`). |
| `JOB_BABY_MECHANIC2` | `4112` | Second (mounted) form; name in messages: `JOB_BABY_MECHANIC` (`class_hidden.h`). |
| `JOB_THIRD_LAST` | `4113` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_SUPER_NOVICE_E` | `4190` | Name in messages: `JOB_SUPER_NOVICE_E` (`class.h`). |
| `JOB_SUPER_BABY_E` | `4191` | Name in messages: `JOB_SUPER_BABY_E` (`class.h`). |
| `JOB_REBELLION` | `4215` | Name in messages: `JOB_REBELLION` (`class.h`). |
| `JOB_SUMMONER` | `4218` | Name in messages: `JOB_SUMMONER` (`class.h`). |
| `JOB_DUMMY4219` | `4219` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_BABY_SUMMONER` | `4220` | Name in messages: `JOB_BABY_SUMMONER` (`class.h`). |
| `JOB_DUMMY4221` | `4221` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_BABY_NINJA` | `4222` | Name in messages: `JOB_BABY_NINJA` (`class.h`). |
| `JOB_BABY_TAEKWON` | `4225` | Name in messages: `JOB_BABY_TAEKWON` (`class.h`). |
| `JOB_BABY_STAR_GLADIATOR` | `4226` | Name in messages: `JOB_BABY_STAR_GLADIATOR` (`class.h`). |
| `JOB_BABY_SOUL_LINKER` | `4227` | Name in messages: `JOB_BABY_SOUL_LINKER` (`class.h`). |
| `JOB_BABY_GUNSLINGER` | `4228` | Name in messages: `JOB_BABY_GUNSLINGER` (`class.h`). |
| `JOB_BABY_REBELLION` | `4229` | Name in messages: `JOB_BABY_REBELLION` (`class.h`). |
| `JOB_DUMMY4230` | `4230` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4231` | `4231` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4232` | `4232` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4233` | `4233` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4234` | `4234` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4235` | `4235` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4236` | `4236` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_DUMMY4237` | `4237` | Second (mounted) form; name in messages: `UNKNOWN_JOB` (`class_hidden.h`). |
| `JOB_BABY_STAR_GLADIATOR2` | `4238` | Second (mounted) form; name in messages: `JOB_BABY_STAR_GLADIATOR` (`class_hidden.h`). |
| `JOB_STAR_EMPEROR` | `4239` | Name in messages: `JOB_STAR_EMPEROR` (`class.h`). |
| `JOB_SOUL_REAPER` | `4240` | Name in messages: `JOB_SOUL_REAPER` (`class.h`). |
| `JOB_BABY_STAR_EMPEROR` | `4241` | Name in messages: `JOB_BABY_STAR_EMPEROR` (`class.h`). |
| `JOB_BABY_SOUL_REAPER` | `4242` | Name in messages: `JOB_BABY_SOUL_REAPER` (`class.h`). |
