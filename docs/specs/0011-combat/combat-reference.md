# Combat reference: display and rule values

| | |
|---|---|
| **Belongs to** | [plan.md](plan.md) §4.5 to §4.11 (SPEC-0011) |
| **Normative for** | every value of `Assets/Settings/World/CombatSettings.asset` and the code tables of plan §4.7 |
| **Updated** | in the same commit as `CombatSettings` or the tables it describes |

Each row is one setting or one table row. **Source** is where the first value comes from:
- a *lead* from roBrowserLegacy, as the plan cites it (read 2026-11-22);
- a Hercules fact (H-n of plan §1);
- a choice of the plan.

**Measured** is filled in by T0.3 from the owner's footage of the original client: the value, the shot of plan §6.3 and the frame numbers, with the frames in `evidence/`. In T9.3, a row that stays at its first value says "as lead", with the shot that confirms it.

## Own combat (plan §4.5)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `ApproachRepeatMs` | 200 ms | plan choice: at most five walk requests a second | not displayed; checked live in T9.1 |
| `AttackSilenceMs` | 1,500 ms | plan choice, from H1 and H6: the server drops an attack silently | live, T9.1 |
| `AttackSilenceMotions`, `AttackSilencePadMs` | 2 × the last attack motion + 500 ms | plan choice: a slow attacker is not asked twice per blow | live, T9.1 |
| `AttackRetries` | 3 | plan choice | live, T9.1 |
| `PickupRetryMs` | 400 ms | plan choice: one step of lag | live, T9.1 |
| `TargetMarkSingleMs` | 0 ms after the single blow's hit moment (`blow.At + AttackMs`); while approaching or in a continuous attack the mark stays | footage, `evidence/T0.3/README.md`, 3 samples | shot 17: the mark went 0.04 s and 0.12 s before the number and about 0.4 s after it; first value 0, to be refined with more single attacks |
| `TargetMark` (the picture) | `sprite/cursors` frame 6 (14 x 10 px, white faceted diamond pointing down, dark outline), imported as the sprite `f6` of `Assets/Art/Imported/sprite/_sheets/e7/cursors_e7ef9996.png`; in `cursors.act` it is action 3 (21 frames of 100 ms, the original's own target-mark animation, tint alpha 245); frames 16 and 24 of the same file are the same diamond in other shading (action 9) and an oval | extracted client data (`GRF Resources~/data/sprite/cursors.spr`, `cursors.act`), found by comparing the footage's `mark_sprite_26x18_x8.png` with every frame of cursors, effects and UI sprites | OpenCV template match of the frame-6 sprite on footage frame 17.63 s (`22-32-58.mp4`): score 0.975 at 22 x 15 |
| `TargetMarkSizePx` | 22 x 15 screen px (the 14 x 10 sprite shown about 1.5 times larger, scaled by the owner's client; the plan's "about 22 x 15") | footage, `evidence/T0.3/README.md`; best match of the scale test above (22 x 17, 21 x 15, 22 x 15 all above 0.973) | shot 17 |
| `TargetMarkHeight` | 66 screen px from the top of the standing sprite to the mark's centre | footage, `evidence/T0.3/README.md` (poring top 580, mark centre 514) | shot 17 |
| `TargetMarkBobPixels`, `TargetMarkBobMs`, `TargetMarkBobFrames` | 15 px at the lowest, 2,100 ms, frames 0,1,3,6,9,10,10,10,9,8,7,6,5,4,3,2,1,0,0,0,0 (sprite px below the top, 100 ms each, high, low, high) | `cursors.act` action 3 (offset y -80 to -70 and back, 21 frames of 100 ms); 15 = 10 sprite px x 1.5 (the size ratio 22 x 15 / 14 x 10); frame table, not a sine (D30) | footage `22-32-58`: mark y 514 (4.6 to 5.0 s), 527 to 529 (5.45 to 5.86 s), 515 (6.66 s): about 15 px, period about 2.1 s |

## Timeline (plan §4.6)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| hit moment | `At + attackMT`, for every attacker | FR-4, D3 (not the server's cap of H8) | shot 16 |
| hurt motion | `attackedMT` of the blow | FR-4 | shots 1, 4 |
| `StandbyMs` | −1 (until the player does something else) | lead, plan §4.6 and D11 | shots 1, 8 |
| `MultiHitMs` | 200 ms between the parts | lead, plan §4.6 (roBrowserLegacy) | shot 5 |
| multi-hit total | with the last part, as `Total`, only when the target is not a player; none over a player | owner, 2026-11-23 (D28); the rest lead, plan §4.6 | shot 5 (where and how) |
| `LeftHandMs` | 350 ms | lead, plan §4.6 (roBrowserLegacy) | shot 6 |
| `HurtBeforeDeathMs` | 250 ms (monsters) | H9: the death notice comes 250 ms after the damage | shot 8 |
| `PlayerHurtBeforeDeathMs` | 0 ms | H9: a player's notice comes at once | shot 9 |

## Projectiles (plan §4.7)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `FlightMs` | 192 ms | lead: `ArrowFlight.js` | shot 11 |
| `FlightFullCells` | 8 cells (shorter flights take `FlightMs × cells / 8`, at least 1 ms) | lead: `ArrowFlight.js` | shot 11 (2, 5, 9 cells) |
| launch (player) | `At + hitFrame × 24 × attackMT / 432`; the number at launch + flight | lead: `ArrowFlight.js` | shot 11 |
| launch (monster) | at the hit moment; the number does not wait | lead: `ArrowFlight.js` | shot 12 |
| `ProjectileHeight` | 1 cell | **no lead**: a first value only | shots 11, 12 |
| `ProjectilePool` | 32 | plan choice | not displayed |
| attack motion rows | the table below (`AttackMotions`) | lead: `src/DB/Jobs/WeaponAction.js` | shots 1, 13 |
| hit frame | 6 for every row | lead, plan §4.7; `WeaponAction.js` has no frames | shots 1, 11, 13 |
| projectile by attacker rows | the tables below (`ProjectileRules`) | lead: `src/DB/Monsters/AttackEffectTable.js`, `Engine/MapEngine/Entity.js` | shots 11 to 13 |

## Attack animation (plan §4.7, `AttackMotions`)

Lead: roBrowserLegacy `src/DB/Jobs/WeaponAction.js` (read 2026-11-23). Its job constants are given as Hercules job ids here. **1, 2, 3** stand for `Attack1`, `Attack2`, `Attack3` (the file's 0, 1, 2).

Rules:
- A job, a weapon type or a sex (other than 0 female or 1 male) that is not listed gives 1.
- Units that are not players always play `Attack`.
- The timeline passes the server's job and both hand values (`Look.Weapon`, `Look.Shield`): `IJobRows` (`CatalogWeaponTypes`, from `JobFamilies`) gives the row job and row sex of "Job families" below, and `IWeaponTypes.TypeOf(job, right, left)` (`CatalogWeaponTypes.HandsOf(job, right, left).WeaponType`) the one type of both hands. The type for each case is the hands table of plan §4.7; the rule itself is SPEC-0009 `composition-reference.md` "Hands".
- `AttackMotions.NumberOf(rowJob, sex, weaponType)` takes the row job and row sex of "Job families" below and the type of both hands (`WeaponHands.Combine`); the two-weapon rows 25 to 30 are matched by that combined type (assassin family only), there is no second-hand flag (D21).
- Taekwon (4046) has one row for every weapon type → 2: owner-observed in the original client, 2026-11-23 (D22); roBrowserLegacy has no row. Its family (Star Gladiator, Star and Sky Emperor) uses it.
- No row, so 1 for every weapon: Gangsi 4050, Death Knight 4051 (spec: deliberately), Dark Collector 4052, Wedding 22, Xmas 26, Summer 27 and the other outfit jobs (owner, 2026-11-23, D26).
- Druid (4351) rows: roBrowserLegacy's `DRUID` entry (0 → 0, 1 → 1, 6, 7, 8, 9 → 2 in its numbering; its comment: daggers in ATTACK2, axes and clubs in ATTACK3), copied from roBrowserLegacy, not run (D27).

Weapon types (`Weapon_IDs`):

| # | Type | # | Type | # | Type |
|---|---|---|---|---|---|
| 0 | bare hand | 9 | two-handed mace | 18 | rifle |
| 1 | short sword | 10 | rod | 19 | gatling gun |
| 2 | sword | 11 | bow | 20 | shotgun |
| 3 | two-handed sword | 12 | knuckle | 21 | grenade launcher |
| 4 | spear | 13 | instrument | 22 | shuriken |
| 5 | two-handed spear | 14 | whip | 23 | two-handed rod |
| 6 | axe | 15 | book | 25 to 30 | two weapons |
| 7 | two-handed axe | 16 | katar | | |
| 8 | mace | 17 | handgun | | |

| Job (id) | Sex | Rows (weapon type → animation) |
|---|---|---|
| Novice (0) | female | 0→1; 10, 23, 2, 3, 6, 7, 8, 9 → 2; 1→3 |
| Novice (0) | male | 0→1; 1→2; 10, 23, 2, 3, 6, 7, 8, 9 → 3 |
| Swordman (1) | either | 0→1; 1, 2, 3, 6, 7, 8, 9 → 2; 4, 5 → 3 |
| Magician (2) | either | 0→1; 10, 23 → 2; 1→3 |
| Archer (3) | either | 0→1; 11→2; 1→3 |
| Acolyte (4) | either | 0→1; 10, 23, 8, 9 → 2 |
| Merchant (5) | either | 0→1; 8, 9, 6, 7, 2, 3 → 2; 1→3 |
| Thief (6) | either | 0→1; 2, 3, 1 → 2; 11→3 |
| Knight (7), on a peco (13), Crusader (14) | either | 0→1; 1, 2, 3, 6, 7, 8, 9 → 2; 4, 5 → 3 |
| Priest (8) | either | 0→1; 10, 23, 8, 9 → 2; 15→3 |
| Wizard (9) | female | 0→1; 1→2; 10, 23 → 3 |
| Wizard (9) | male | 0→1; 10, 23 → 2; 1→3 |
| Blacksmith (10) | either | 0→1; 1→2; 2, 3, 6, 7, 8, 9 → 3 |
| Hunter (11) | either | 0→1; 1→2; 11→3 |
| Assassin (12) | either | 0→1; 6, 2, 1 → 2; 25 to 30, 16 → 3 |
| Monk (15) | either | 0→1; 10, 23, 8, 9 → 2; 12→3 |
| Sage (16) | either | 0→1; 1→2; 10, 23, 15 → 3 |
| Rogue (17) | either | 0→1; 2, 3, 1 → 2; 11→3 |
| Alchemist (18) | either | 0→1; 1→2; 3, 2, 6, 7, 8, 9 → 3 |
| Bard (19) | either | 0→1; 1, 13 → 2; 11→3 |
| Dancer (20) | either | 0, 1 → 1; 14→2; 11→3 |
| Crusader on a peco (21) | either | 0→1; 1, 2, 3, 6, 7, 8 → 2; 4, 5 → 3 (no row for 9) |
| Super Novice (23) | female | 0→1; 10, 23, 6, 7, 8, 9, 2 → 2; 1→3 |
| Super Novice (23) | male | 0→1; 1→2; 10, 23, 6, 7, 8, 9, 2 → 3 |
| Gunslinger (24) | either | 0, 17, 20 → 2; 19, 18, 21 → 3 |
| Ninja (25) | either | 0→1; 1→2; 22→3 |
| Soul Linker (4049) | female | 0→1; 1→2; 10, 23 → 3 |
| Soul Linker (4049) | male | 0→1; 10, 23 → 2; 1→3 |
| Summoner (4218) | either | 0→1; 10, 23 → 2 |
| Taekwon (4046) | either | every type → 2 |
| Druid (4351) | either | 0→1; 1→2; 6, 7, 8, 9 → 3 |

Two more lead facts, not used here:
- roBrowserLegacy also shortens the attack of monks with knuckles or bare hands (5 frames from frame 0), and of summoners with bare hands (4 frames).
- `UnitClock.FitMs` (plan §4.9) fits every attack to `attackMT`, so this is a T0.3 question.

### Job families (plan §4.7, `JobFamilies`)

`JobFamilies.RowJobOf(job)` gives the job whose rows above a job uses; `RowSexOf(job, sex)` gives the sex of the row. The table is in code (`Scripts/Units/Rules/JobFamilies.cs`, sorted entries of job, row job and a flag) and equals this list. An id that is not listed is its own row job. `IsAssassinFamily(job)` is `RowJobOf(job) == 12`.

Sources: ids up to 4242 are Hercules's (`src/common/class.h`, `class_hidden.h`). Ids above 4242 are not in Hercules 2026.09: their names are roBrowserLegacy's `DB/Jobs/JobConst.js` and their rows its `DB/Jobs/WeaponAction.js` copies (`WeaponAction[JobId.X] = WeaponAction[JobId.Y]`, read 2026-11-23). They are **copied from roBrowserLegacy, not run** (owner, 2026-11-23, D27). A row marked **proposal, to confirm** is a choice made where roBrowserLegacy has no copy or a broken one; the owner has not confirmed it yet.

| Row job | Job ids mapped to it (Hercules) | Ids above 4242 (copied from roBrowserLegacy, not run) |
|---|---|---|
| 0 Novice | 4001, 4023 | |
| 1 Swordman | 4002, 4024 | |
| 2 Mage | 4003, 4025 | |
| 3 Archer | 4004, 4026 | |
| 4 Acolyte | 4005, 4027 | |
| 5 Merchant | 4006, 4028 | |
| 6 Thief | 4007, 4029 | |
| 7 Knight | 4008, 4030, 4054, 4060, 4096 | 4252 DRAGON_KNIGHT, 4265 DRAGON_KNIGHT_RIDING, 4332 RUNE_KNIGHT_2ND |
| 13 Knight on a peco | 4014, 4036, 4080, 4081, 4088 to 4095, 4109 | 4280 DRAGON_KNIGHT2, 4345 RUNE_KNIGHT2_2ND |
| 14 Crusader | 4015, 4037, 4066, 4073, 4102 | 4258 IMPERIAL_GUARD, 4271 IMPERIAL_GUARD_RIDING, 4338 ROYAL_GUARD_2ND |
| 21 Crusader on a peco | 4022, 4044, 4082, 4083, 4110 (D25: each as `WeaponAction.js` copies it; it copies `CRUSADER2` to every one, so all take 21: CRUSADER2_H 4022, CRUSADER2_B 4044, ROYAL_GUARD2 4082, ROYAL_GUARD2_H 4083, ROYAL_GUARD2_B 4110) | 4281 IMPERIAL_GUARD2, 4348 ROYAL_GUARD2_2ND (also copies of `CRUSADER2`) |
| 8 Priest | 4009, 4031, 4057, 4063, 4099 | 4256 CARDINAL, 4269 CARDINAL_RIDING; 4336 ARCH_BISHOP_2ND: **proposal, to confirm** (`WeaponAction.js` copies to `JobId.ARCHBISHOP_2ND`, a constant `JobConst.js` does not define, so the source gives 4336 no row; the evident intent is the Priest row) |
| 9 Wizard | 4010, 4032, 4055, 4061, 4097 | 4255 ARCH_MAGE, 4268 ARCH_MAGE_RIDING, 4335 WARLOCK_2ND |
| 9 Wizard, **male** row for either sex (D24) | 4240 Soul Reaper, 4242 Baby Soul Reaper | 4303 SOUL_ASCETIC, 4310 SOUL_ASCETIC_RIDING; 4246 HAETAE_SOUL_REAPER, 4248 HAETAE_SOUL_REAPER_B: **proposal, to confirm** (no `WeaponAction.js` copy; Soul Reaper family by their name) |
| 16 Sage | 4017, 4039, 4067, 4074, 4103 | 4261 ELEMENTAL_MASTER, 4274 ELEMENTAL_MASTER_RIDING, 4341 SORCERER_2ND |
| 10 Blacksmith | 4011, 4033, 4058, 4064, 4086, 4087, 4100, 4112 | 4253 MEISTER, 4266 MEISTER_RIDING, 4279 MEISTER2, 4333 MECHANIC_2ND, 4347 MECHANIC2_2ND |
| 18 Alchemist | 4019, 4041, 4071, 4078, 4107 | 4259 BIOLO, 4272 BIOLO_RIDING, 4339 GENETIC_2ND |
| 11 Hunter | 4012, 4034, 4056, 4062, 4084, 4085, 4098, 4111 | 4257 WINDHAWK, 4270 WINDHAWK_RIDING, 4278 WINDHAWK2, 4337 RANGER_2ND, 4346 RANGER2_2ND |
| 17 Rogue | 4018, 4040, 4072, 4079, 4108 | 4260 ABYSS_CHASER, 4273 ABYSS_CHASER_RIDING, 4340 SHADOW_CHASER_2ND |
| 12 Assassin | 4013, 4035, 4059, 4065, 4101 | 4254 SHADOW_CROSS, 4267 SHADOW_CROSS_RIDING, 4334 GUILLOTINE_CROSS_2ND |
| 15 Monk | 4016, 4038, 4070, 4077, 4106 | 4262 INQUISITOR, 4275 INQUISITOR_RIDING, 4342 SURA_2ND |
| 19 Bard | 4020, 4042, 4068, 4075, 4104 | 4263 TROUBADOUR, 4276 TROUBADOUR_RIDING, 4343 MINSTREL_2ND |
| 20 Dancer | 4021, 4043, 4069, 4076, 4105 | 4264 TROUVERE, 4277 TROUVERE_RIDING, 4344 WANDERER_2ND |
| 23 Super Novice | 4045, 4190, 4191 | 4307 HYPER_NOVICE, 4314 HYPER_NOVICE_RIDING |
| 24 Gunslinger | 4215, 4228, 4229 | 4306 NIGHT_WATCH, 4313 NIGHT_WATCH_RIDING |
| 25 Ninja | 4211, 4212, 4222, 4223, 4224 | 4304 SHINKIRO, 4305 SHIRANUI, 4311 SHINKIRO_RIDING, 4312 SHIRANUI_RIDING |
| 4049 Soul Linker | 4227 | |
| 4218 Summoner | 4220; 4219 (Hercules `JOB_DUMMY4219`, roBrowserLegacy DO_CART_SUMMONER1, a copy of DO_SUMMONER): **proposal, to confirm** | 4308 SPIRIT_HANDLER, 4315 SPIRIT_HANDLER_RIDING1 |
| 4046 Taekwon: `Attack2` for every weapon (owner-observed, 2026-11-23, D22; roBrowserLegacy has no row for any of them) | 4047, 4048, 4225, 4226, 4238, 4239, 4241 | 4243 STAR_EMPEROR2, 4244 STAR_EMPEROR2_B, 4245 HAETAE_STAR_EMPEROR, 4247 HAETAE_STAR_EMPEROR_B, 4302 SKY_EMPEROR, 4309 SKY_EMPEROR_RIDING, 4316 SKY_EMPEROR2: **proposal, to confirm** (no `WeaponAction.js` entry; Star and Sky Emperor play `Attack2`, D22) |
| 4351 Druid (own rows, T10.3) | | 4352 DRUID_B, 4353 KARNOS, 4354 KARNOS_B, 4355 ALITEA, 4358 DRUID_RIDING, 4359 KARNOS_RIDING, 4360 ALITEA_RIDING |
| themselves, no row: `Attack1` | 4050 Gangsi, 4051 Death Knight (spec: deliberately); 4052 Dark Collector, 22 Wedding, 26 Xmas, 27 Summer and the other outfit jobs (owner, 2026-11-23, D26); any id not listed | 4356 WEREWOLF, 4357 WERERAPTOR: **proposal, to confirm** (no `WeaponAction.js` entry); markers 4331, 4350, 4361 |

## Projectiles by attacker (plan §4.7, `ProjectileRules`)

Lead: roBrowserLegacy, read 2026-11-23:
- `src/DB/Monsters/AttackEffectTable.js`: its job ids;
- `Engine/MapEngine/Entity.js` `onEntityAction`;
- `src/Renderer/Effects/ArrowFlight.js`: flight `8 × 24 ms × min(1, cells × 5 / 40)`, the same as the settings above.

**Players.** An arrow for the bow type (11). Crossbow, arbalest, kakkung, hunter bow and bow of Rudra all have the bow as their base type.
- Guns, shuriken, whips and instruments: none.
- roBrowserLegacy also varies the hit frame in a few special cases (5.85, 5.75, 3.0) that it does not name. Here it is 6 for all, a T0.3 question.

**Monsters and mercenaries with an arrow.** One row for each job id in the table (44 ids):

| Monsters | Job ids |
|---|---|
| archer skeleton | 1016, 1420, 3034, 3445, 3763 |
| orc archer | 1189, 1473, 1982, 3130, 3900 |
| goblin archer | 1258, 1577 |
| raydric archer | 1276, 1453, 2469, 3144, 20368, 20578, 21010 |
| kobold archer | 1282, 1455, 2287, 2784 |
| archer guardian | 1285 |
| gargoyle | 1253, 1597, 2826, 2827, 3197, 3435, 10070, 20369, 21028, 22256 |
| archer mercenaries | 6017 to 6026 |

**Monsters with a projectile of their own** (`Other`). They are launched and timed here, and drawn by SPEC-0013. 29 ids:

| Monsters | Job ids |
|---|---|
| parasite | 1500, 1555, 2720, 21005 |
| stone shooter | 1495, 20274, 20999 |
| wootan shooter | 1498, 20275, 20279 |
| live peach tree | 1410, 1550, 2775 |
| evil cloud hermit | 1412, 1531, 2935, 21023 |
| ragged zombie | 1865, 20371, 21574 |
| poton canon | 1664, 1683, 1724, 1665, 1666, 1667 |
| lady tanee | 1688, 3288, 20238 |

**Not projectiles.** The same file lists effects that appear at the target: mandragora, hydra, odium, drosera, mavka and entweihen. They are `None` here, for SPEC-0013.

Not in the lead: the projectile's height, and whether it follows a moving target. It takes the shown positions until its launch and flies straight after it (T0.3 shots 11, 12).

## Numbers (plan §4.8)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `NumberMs` | 1,500 ms (normal, critical) | lead: `Damage.js` | shots 1, 2 |
| `NumberScaleStart`, `NumberScaleEnd` | 4 → 0 | lead: `Damage.js` | shot 1 |
| `NumberDriftX`, `NumberDriftY` | 4 cells to +x, 4 to −y over the life | lead: `Damage.js` | shot 1 |
| `NumberHeightBase`, `NumberHeightAmplitude` | `2 + 5 · sin(−π/2 + π(0.5 + 1.5 t))` cells | lead: `Damage.js` | shot 1 |
| alpha | `1 − t` | lead: `Damage.js` | shot 1 |
| `MessageMs` | 800 ms (miss, lucky) | lead: `Damage.js` | shot 3 |
| `MessageScale` | 0.5 | lead: `Damage.js` | shot 3 |
| `MessageHeightStart`, `MessageHeightEnd` | 3.5 → 7 cells | lead: `Damage.js` | shot 3 |
| `TotalMs` | 3,000 ms, above the target | lead: `Damage.js` | shot 5 |
| `TotalHeight`, `TotalScale` | 7 cells, 2 | **no lead**: a first value only | shot 5 |
| miss and lucky pictures | `msg` 0 (miss), 4 and 5 (lucky); the miss tinted like a number: white over a monster, red over a player | lead, plan §4.8; the tint from the owner's footage notes (2026-11-22) | shot 3 |
| critical background | `msg` 3 behind the digits, at half the number's scale | lead, plan §4.8 | shot 2 |
| `DigitSpacingPx` | 2 px | lead: `Damage.js` | shot 1 |
| `NumberOnMonster` | white | lead: `Damage.js` | shot 1 |
| `NumberOnPlayer` | red | lead: `Damage.js` | shot 4 |
| `NumberCritical` | (0.9, 0.9, 0.15), over the `msg` critical background | lead: `Damage.js` | shot 2 |
| `NumberTotal` | (0.9, 0.9, 0.15): the multi-hit total, yellow-green; the parts keep `NumberOnMonster` / `NumberOnPlayer` | owner, 2026-11-23 (D28) | shot 5 |
| number size | sprite pixels / pixels per unit × scale, in the world (zooms with it) | lead: `Damage.js` | shots 1, 10 (world or screen size) |
| order | after the units, no depth test, newer over older | lead, plan §4.8 | shot 7 |
| `NumberCapacity`, `MaxDigits` | 512 numbers, 10 digits | plan choice (FR-12) | not displayed |

## Bars (plan §4.8)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `HpBarPx` | 60 × 5 screen pixels | lead: `EntityLife.js` | shot 10 |
| `HpSpBarPx` | 60 × 9 screen pixels (own HP and SP) | lead: `EntityLife.js` | shot 10 |
| `BarBelowFeet` | 0.5 cells | lead: `EntityLife.js` | shot 10 |
| `BarLowShare` | 25 % | lead: `EntityLife.js` | shot 10 (low HP) |
| `BarFrame` | `#10189c` | lead: `EntityLife.js` | shot 10 |
| `BarEmpty` | `#424242` | lead: `EntityLife.js` | shot 10 |
| `OwnHp`, `OwnHpLow` | `#10ef21`, `#ff0000` | lead: `EntityLife.js` | shots 4, 10 |
| `OwnSp` | `#1863de` | lead: `EntityLife.js` | shot 10 |
| `MonsterHp`, `MonsterHpLow` | `#ff00e7`, `#ffff00` | lead: `EntityLife.js` | shot 10 |
| `BarCapacity` | 512 | plan choice (FR-12) | not displayed |

## Input and restart (plan §4.10, §4.11)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `HoldAttackMs` | 300 ms | plan choice | shot 14 |
| `PickMinPx` | 24 screen pixels | plan choice | shot 14 |
| `RestartTimeoutMs` | 3,000 ms | plan choice | not displayed |
| restart prompt labels and order | "Return to Save Point", then "Cancel" (provisional) | T0.4 (owner); until then a placeholder of T7.5 | T0.4 |
| restart prompt window | 200 px wide, plain USS (`RestartPrompt.uss`), centred, no images (D8) | provisional (T7.5) | T0.4 |

## Owner's notes on the footage (2026-11-22)

The owner recorded five videos of the original client: `Ragnarok 2026-11-22 22-*.mp4`. They are not yet measured. T0.3 maps them onto the shots of plan §6.3 and fills the **Measured** columns. What the owner noted:

1. **Porings and a pupa.**
   - Own hits and one hit received; a poring's miss; one own miss on the pupa.
   - In a few cancelled attacks the poring still took the damage while the character walked away or stood.
   - After a kill the character went back to the combat idle pose; probably an item was picked up.
   - A monster's miss is shown over the monster in white; the own miss is shown over the own character in red.
2. **A lunatic.** It hits and misses often, the character does not answer, and the lunatic kills it.
3. **Bow against a roween** (odalisque class), at several distances.
4. **Whip against a roween and anopheles.** Some criticals, faster attacks.
5. **Medusa.** Many misses, a few skills, one attack at the end.

The stats window with ASPD is visible in every video.
