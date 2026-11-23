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

Footage (T0.3, read in T9.3 on 2026-11-23): **A** = `evidence/T0.3/measured-A.md` (`22-32-58` acolyte, `22-47-07` Medusa), **B** = `measured-B.md` (`22-39-21` bow, `22-41-02` whip), **C** = `measured-C.md` (three YouTube Shorts of **Ragnarok Online CLASSIC**, another client version: its colours, sprites and zoom are taken with care), **M** = `evidence/T0.3/README.md` (the mark). `fN` is a video frame; lengths are video pixels at the default zoom, about 50 px per ground cell (A). A height cell cannot be measured apart from a ground cell (A), so screen rises are converted at 50 px per cell and noted.

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
| `TargetMarkSizePx` | 22 x 15 screen px (the 14 x 10 sprite shown about 1.5 times larger, scaled by the owner's client; the plan's "about 22 x 15") | footage, `evidence/T0.3/README.md`; best match of the scale test above (22 x 17, 21 x 15, 22 x 15 all above 0.973) | measured: shot 17 (M, `22-32-58` 17.63 s) |
| `TargetMarkHeight` | 66 screen px from the top of the standing sprite to the mark's centre | footage, `evidence/T0.3/README.md` (poring top 580, mark centre 514) | measured: shot 17 (M, `22-32-58` 4.6 to 5.0 s) |
| `TargetMarkBobPixels`, `TargetMarkBobMs`, `TargetMarkBobFrames` | 15 px at the lowest, 2,100 ms, frames 0,1,3,6,9,10,10,10,9,8,7,6,5,4,3,2,1,0,0,0,0 (sprite px below the top, 100 ms each, high, low, high) | `cursors.act` action 3 (offset y -80 to -70 and back, 21 frames of 100 ms); 15 = 10 sprite px x 1.5 (the size ratio 22 x 15 / 14 x 10); frame table, not a sine (D30) | footage `22-32-58`: mark y 514 (4.6 to 5.0 s), 527 to 529 (5.45 to 5.86 s), 515 (6.66 s): about 15 px, period about 2.1 s |

## Timeline (plan §4.6)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| hit moment | `At + attackMT`, for every attacker | FR-4, D3 (not the server's cap of H8) | as lead (owner, 2026-11-23: the rule from the server's packets). Shot 1 (A, `22-32-58` f1059): the number and the hit effect on the same frame, 0.27 s after the click, consistent with `At + attackMT` (about 215 ms) plus a round trip, n = 1. Shot 16 (A, `22-47-07` f355 to f424): not visible, the Medusa's basic attacks all missed. Ranged and whip blows differ: see "Rules and surprises" 9 |
| hurt motion | `attackedMT` of the blow | FR-4 | as lead: not visible in the owner's footage (A shot 1 f1059 to f1066: the poring is hidden by the 4x number and the hit effect; shot 4: the Archbishop overlaps the Medusa). C (Classic, `dual` f162 to f173) shows a flinch of about 200 ms, `attackedMT` unknown there |
| `StandbyMs` | −1 (until the player does something else) | lead, plan §4.6 and D11 | as lead, consistent: A shot 1, `22-32-58` f1307 to f2080: the acolyte keeps the same pose for 14 s after the last blow (for the bare-handed acolyte this pose is also her idle). Shot 8: not visible |
| `MultiHitMs` | **100 ms** between the parts of a blow **with** a second hand (`LeftDamage` > 0) (was 200, lead) | footage + roBrowserLegacy, D32, D36 | C shot 5: `dual` 549 / 549 at f162 and f168, 429 / 429 at f4719 and f4725: 6 frames at 60 fps in both |
| `MultiHitNoLeftMs` | **200 ms** between the parts of a blow **without** a second hand | roBrowserLegacy Entity.js (no footage sample), D36 | none: every footage sample of a double attack is dual-wield |
| multi-hit total | a **running total** from the first part: with each part, and with the second hand's number, a new `Total` replaces the one shown over that unit, its value the sum so far (parts and second hand); only when the target is not a player; a plain blow with a second hand has none (was: one total with the last part) | footage C, owner 2026-11-23 (D32); not over a player: owner (D28) | C shot 5: `dual` yellow 549 at f162 (with part 1), 1098 at f168, 1261 (= 549 + 549 + 163) at f184; second sample 429 (f4719), 858 (f4725), 1009 (f4740); plain 532 + 168 showed no total. Over a player: not visible (all samples on monsters), as D28 |
| `LeftHandMs` | 350 ms after the first part (the hit moment) | lead, plan §4.6 (roBrowserLegacy) | as lead, confirmed from part 1 (the timeline counts from the hit moment, which is part 1): C shot 6, `dual` f162 to f182 (20 frames, 333 ms), f4719 to f4740 (21, 350 ms), plain blows 20 frames |
| `HurtBeforeDeathMs` | 250 ms (monsters) | H9: the death notice comes 250 ms after the damage | as lead (H9): not measured, A `22-32-58` shot 8 (pupa killed about 55.5 s, sinks at 56.0 s) not framed |
| `PlayerHurtBeforeDeathMs` | 0 ms | H9: a player's notice comes at once | as lead (H9): shot 9 (own death) not in the footage |
| cancelled attack | (no rule of its own: a ground click stops the attack, FR-2) | FR-2 | as lead: not visible in the footage (A shot 14: no clean cancel by walking) |

## Projectiles (plan §4.7)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `FlightMs` | 192 ms | lead: `ArrowFlight.js` | as lead, confirmed: B shot 11, `22-39-21` event 4 (about 13 cells): arrow seen f1932 to f1942 (11 frames, 185 ms), number f1943 |
| `FlightFullCells` | 8 cells (shorter flights take `FlightMs × cells / 8`, at least 1 ms): how long the **arrow** flies | lead: `ArrowFlight.js` | as lead, confirmed: B shot 11, events 1 to 4: arrow seen 10 frames at 7.1 cells (lead 10.0), 7 at 5.3 (7.6), 3 at 1.2 (1.7, partly hidden), 11 beyond 8 (11.4) |
| launch (player) | `At + hitFrame × 24 × attackMT / 432` | lead: `ArrowFlight.js` | as lead, confirmed: B events 1 and 3, attack start f621 / f714 to arrow f636 / f729 = 15 frames (252 ms) at `attackMT` about 780 ms; hit frame 6 gives 260 ms (5.8 measured) |
| the number of a player's arrow | **launch + `FlightMs` (192 ms), whatever the distance** (was: launch + the flight); the arrow itself is gone at launch + flight, up to 8 frames earlier for a near target | footage B, D33 (plan §4.7 text already said launch + 192 ms) | B events 1 to 4: arrow first seen to number 11 frames (185 ms) at 1.2, 5.3, 7.1 and about 13 cells (f636 to f647, f683 to f694, f729 to f740, f1932 to f1943); event 5: 10 frames |
| launch (monster) | at the hit moment; the number does not wait | lead: `ArrowFlight.js` | as lead: not visible, no shooting monster in the footage (shot 12 not recorded) |
| arrow path | straight from the attacker's to the target's shown position at the launch; does not follow a moving target; constant speed; one fixed angle along the screen line | lead (straight) and plan §4.7 | confirmed: B events 2 (target walking towards the archer, f683 to f690: the arrow ends at the launch-time spot, the number on the target's current place) and 5; angle fixed (events 1, 4) |
| `ProjectileHeight` | 1 cell | **no lead**: a first value only | as first value, consistent: B events 1 and 4: about 35 to 40 px above the feet at both ends (0.8 cell on screen, about 1 cell of height); shot 12 not recorded |
| `ProjectilePool` | 32 | plan choice | not displayed (B: one arrow on screen at a time at 783 ms per shot) |
| attack motion rows | the table below (`AttackMotions`) | lead: `src/DB/Jobs/WeaponAction.js` | as lead: not decidable from the pictures (B shot 13: the bow and whip each show one motion; Attack2 or Attack3 needs the act file). C (Classic, `dual`): two swings per motion, consistent with the assassin's two-weapon `Attack3` |
| hit frame | **6 for every row, 4 for the whip** (type 14) (was 6 for all); only a projectile's launch uses it | lead, plan §4.7 (bow); footage B (whip) | bow confirmed: B events 1, 3 (5.8). Whip: B shot 13, `22-41-02`: whip unfurls f1140, number f1145 = 84 ms at `attackMT` about 380 ms = 4 frames of 24 × 380 / 432 ms; earlier attack f1125; 2 samples |
| hit effect | a white curved slash arc with sparkles on the target at the hit moment (bow, whip, bare hand) | footage A, B | A f1059 to f1066 (about 0.2 s); B f647, f740, f1943, f2035, f1145. **Not drawn here: hit effects are SPEC-0013's** (spec non-goal) |
| projectile by attacker rows | the tables below (`ProjectileRules`) | lead: `src/DB/Monsters/AttackEffectTable.js`, `Engine/MapEngine/Entity.js` | as lead; bow arrow confirmed (B shot 11), whip none confirmed (B shot 13, f1132 to f1152: the whip is part of the sprite); monsters, guns, throwing weapons, instruments: not visible (shots 12, 13 not recorded for them) |

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
- `UnitClock.FitMs` (plan §4.9) fits every attack to `attackMT`, so this is a T0.3 question. T9.3: as lead; C (Classic, `dual`) shows the motion stretched over the whole cycle, consistent with fitting; the monk and summoner cases were not recorded.

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
- roBrowserLegacy also varies the hit frame in a few special cases (5.85, 5.75, 3.0) that it does not name. Here it is 6, and 4 for the whip (T9.3, footage B: the bow measured 5.8, probably roBrowserLegacy's 5.85).

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

Not in the lead: the projectile's height, and whether it follows a moving target. It takes the shown positions until its launch and flies straight after it: confirmed for a player's arrow (B shot 11, events 2 and 5); a monster's arrow (shot 12) was not recorded.

Arrow picture (B, `B_arrowpicture_22-39-21_10.74s.jpg`, new, nothing to change): a thin maroon shaft with a white-grey head and fletching, about 85 px long at the default zoom, drawn over the sprites it crosses, its tail at the archer's body on the first frame.

## Numbers (plan §4.8)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `NumberMs` | 1,500 ms (normal, critical) | lead: `Damage.js` | as lead, confirmed: A shot 1, `22-32-58` f1059 to f1142 visible (1.40 s), gone f1144; the numbers at 21.2 s and 35.9 s look the same. Criticals (shot 2): C only, not timed |
| `NumberScaleStart`, `NumberScaleEnd`, `NumberShrinkMs` | **4 → 1, smooth-step over 400 ms, then 1 to the end** (was 4 → 0 over the life) | footage A, D34 | A shot 1, f1059 to f1082: digit height 78, 75, 62, 47, 40, 28, 20 px at 0, 0.05, 0.12, 0.18, 0.25, 0.32, 0.39 s (scale 4, 3.85, 3.18, 2.41, 2.05, 1.44, 1.03), then about 20 px to the end; the same final size on 6 more numbers (`A_number_drift_births`). Smooth-step over 400 ms fits within 0.32 of scale (350 ms leaves 0.6 at 0.25 s) |
| `NumberDriftX` | **3.1 cells to +x, decelerating: `3.1 · sin(π t / 2)`** (was 4, linear) | footage A, D34; the sign as lead | A shot 1, f1059 to f1142: x 983, 1053, 1072, 1110, 1132, 1136 at 0, 0.52, 0.66, 0.96, 1.16, 1.40 s = +153 px (3.1 cells), decelerating; a red 244 on the player (A `22-47-07` f416 on) drifted 135 px by 1.07 s, also decelerating. **Sign: as lead, mixed samples**: 5 of 6 monster numbers to +x, the player's numbers to −x (A, rule 4) |
| `NumberDriftY`, `NumberHeightBase`, `NumberHeightAmplitude` | 4 cells to −y over the life; `2 + 5 · sin(−π/2 + π(0.5 + 1.5 t))` cells | lead: `Damage.js` | as lead: the peak at one third of the life and the faster fall confirmed (A shot 1: centre y 511, 457 at 0.52 s, 507, 566, 705 at 1.40 s; the digits' bottom 83 px up then about 250 px down); the amplitude cannot be converted, a height cell is not measurable apart from a ground cell, and the one player sample rose less (35 px). The number's centre at birth was at the poring's feet (n = 1, not applied) |
| alpha | `1 − t` | lead: `Damage.js` | as lead, roughly: A shot 1, by eye: solid to about 0.7 s, half at 1.0 s, about 0.3 at 1.2 s, invisible at 1.40 s; not measured |
| `MessageMs` (miss) | **1,500 ms** (was 800, miss and lucky) | footage A, D34 | A shot 3: white Miss f1388 to f1466 (1.31 s visible), red Miss f1005 to f1092 (about 1.4 s), both fading linearly from alpha 1, so about 1.5 s in all |
| `MessageScale` | 0.5, constant | lead: `Damage.js` | as lead, confirmed constant: A shot 3, 45 x 14 px from the first to the last frame (2 samples) |
| `MessageHeightStart`, `MessageHeightEnd` (miss) | **0.4 → 3.5 cells, a constant rise, no x drift** (was 3.5 → 7) | footage A, D34 | A shot 3: the white Miss starts about 20 px above the poring's feet (centre 495, feet 515: 0.4 cell) and rises 100 to 110 px/s at a constant speed (488 to about 358 in 1.3 s, 2.6 cells), x 862 to 904 without drift; 2 samples. 105 px/s over 1.5 s = 3.1 cells, at 50 px per cell |
| miss: over whom | **over the unit that missed (the attacker)**, tinted as a number over it: white over a monster, red over a player (was: over the target) | footage A, owner 2026-11-23 (D31) | A shot 3: white Miss over a poring that missed the idle acolyte (`22-32-58` 23.4 s), red Miss over the acolyte when her blow missed the pupa (53.5 s, 54.0 s) and a poring (16.9 s, 17.4 s); white Misses from the Medusa (`22-47-07`); C `crit` f1214 to f1224: red Miss over the own character. Many Misses are alive at once (up to 3, A) |
| `LuckyMs`, `LuckyHeightStart`, `LuckyHeightEnd` | 800 ms, 3.5 → 7 cells, over the dodging unit (the target) | lead: `Damage.js` (the miss's old values, now settings of their own) | as lead, n = 1: A shot 3, `22-47-07` f361 to f394: "LUCKY!!" over the Archbishop's head, about 0.55 s, no rise. One sample: not applied |
| `TotalMs` | **2,500 ms**, above the target (was 3,000) | footage C, D32 | C shot 5, `dual`: 1261 from f184 to about f336 (2.53 s), almost still; n = 1, Classic |
| `TotalHeight`, `TotalScale`, `TotalScaleStart`, `TotalGrowMs` | 7 cells; **1.5, growing from 0.6 over 100 ms at each update** (was 2, constant) | footage C, D32 (`TotalHeight`: first value) | C shot 5, `dual` f168 to f172: the first frame of each total about 0.4 of its final size, grown in about 6 frames, final about 1.5 times a part; height: about 80 px above the monster's head in a phone recording at another zoom, not converted: `TotalHeight` stays the first value |
| miss and lucky pictures | `msg` 0 (miss), 4 and 5 (lucky); the miss tinted like a number over the unit that missed | lead, plan §4.8; tint and place: footage A (D31) | confirmed: A shot 3, white with a grey outline over a monster, dark red over the acolyte |
| critical background | `msg` 3 behind the digits, at half the number's scale | lead, plan §4.8 | as lead: C shot 2 (Classic, `crit` f1226, a188): an orange-red starburst behind yellow digits, about 1.3 times the number's width; not in the owner's footage |
| `DigitSpacingPx` | 2 px | lead: `Damage.js` | as lead: not decidable, A: the advance is 23.3 px per unit of scale but the digit sprite's width on screen is unknown |
| `NumberOnMonster` | white | lead: `Damage.js` | as lead, confirmed: A shot 1, white with a dark outline (`26`, `23`, many); C white too |
| `NumberOnPlayer` | red | lead: `Damage.js` | as lead, confirmed: A shot 4, red `244` (`22-47-07` f416), red `1` on the acolyte (25 s, 45.5 s) |
| `NumberCritical` | (0.9, 0.9, 0.15), over the `msg` critical background | lead: `Damage.js` | as lead: C shot 2 (Classic) looks fully saturated yellow, low confidence (phone video, JPEG); not in the owner's footage |
| `NumberTotal` | (0.9, 0.9, 0.15): the multi-hit total, yellow-green; the parts keep `NumberOnMonster` / `NumberOnPlayer` | owner, 2026-11-23 (D28) | as D28: C shot 5, bright yellow, the critical's colour without its background; parts white (`dual` f162, f168) |
| number size | sprite pixels / pixels per unit × scale, in the world (zooms with it) | lead: `Damage.js` | as lead: world or screen size not decidable, the zoom does not change in any video (B) |
| order | after the units, no depth test, newer over older | lead, plan §4.8 | as lead, confirmed: A shot 1 f1059 (the 4x number over the poring, the acolyte and the hit effect); C (`dual`, a188): numbers overlap, newer over older, no stacking |
| heal numbers | not drawn (damage < 0 shows nothing) | H11; heals are SPEC-0012's | A `22-47-07` f492: a **green** `244`, same size and arc: for SPEC-0012 (BACKLOG) |
| `NumberCapacity`, `MaxDigits` | 512 numbers, 10 digits | plan choice (FR-12) | not displayed |

## Bars (plan §4.8)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `HpBarPx` | 60 × 5 screen pixels | lead: `EntityLife.js` | as lead, confirmed: A shot 10, `22-32-58` f1062: 60 wide with the 1 px frame, 5 rows; 3 frames |
| `HpSpBarPx` | 60 × 9 screen pixels (own HP and SP) | lead: `EntityLife.js` | as lead, confirmed: A shot 10, f1062 rows 554 to 562: border, 3 HP, divider, 3 SP, border; 3 frames |
| `BarBelowFeet` | 0.5 cells | lead: `EntityLife.js` | as lead, one sample against it: A f1062, the gap looks about 0.2 cell (0 to 14 px under the poring's feet, 2 to 6 px under the acolyte's shoes), but the feet row is hard to read |
| `BarLowShare` | 25 % | lead: `EntityLife.js` | as lead: not visible, no low bar in the footage |
| `BarFrame` | `#10189c` | lead: `EntityLife.js` | as lead, within the video's colour error: A, `#0015a3` |
| `BarEmpty` | `#424242` | lead: `EntityLife.js` | as lead: dark empty part seen (A shot 10), not measured |
| `OwnHp`, `OwnHpLow` | `#10ef21`, `#ff0000` | lead: `EntityLife.js` | `OwnHp` as lead, within the video's colour error (A f1062: `#1fff20`); `OwnHpLow` as lead, not visible |
| `OwnSp` | `#1863de` | lead: `EntityLife.js` | as lead, within the video's colour error (A f1062: `#2763da`) |
| `MonsterHp`, `MonsterHpLow` | `#ff00e7`, `#ffff00` | lead: `EntityLife.js` | `MonsterHp` as lead, confirmed magenta (A, 3 frames); `MonsterHpLow` as lead, not visible |
| when a monster's bar appears | with the first HP report of the server (FR-7) | FR-7, H10 | consistent: A `22-32-58` f1052, 0.15 s after the click and before the number: nothing to change |
| `BarCapacity` | 512 | plan choice (FR-12) | not displayed |

## Input and restart (plan §4.10, §4.11)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| `HoldAttackMs` | 300 ms | plan choice | as plan choice: shot 14 (click, ctrl + click, hold, `/noctrl`) confirmed by the owner playing the game on 2026-11-23 (spec revision of FR-16); not in the footage (A: no hold or release sample) |
| `PickMinPx` | 24 screen pixels | plan choice | as plan choice: shot 14 confirmed by the owner playing the game on 2026-11-23; not in the footage |
| `RestartTimeoutMs` | 3,000 ms | plan choice | not displayed |
| restart prompt labels and order | "Return to Save Point", then "Cancel" (provisional) | T0.4 (owner); until then a placeholder of T7.5 | waiting for T0.4 (owner) |
| restart prompt window | 200 px wide, plain USS (`RestartPrompt.uss`), centred, no images (D8) | provisional (T7.5) | waiting for T0.4 (owner) |

## Rules and surprises (T9.3, from the footage)

1. **A miss belongs to the attacker** (D31): the number goes over the target, the Miss over whoever missed; "LUCKY!!" over the unit that dodged.
2. **A number's scale is 4 → 1, then fixed at 1** (D34), not 4 → 0.
3. **A Miss lives about 1.5 s and rises at a constant speed** with no x drift (D34).
4. **The multi-hit total is a running sum** from the first part, replaced at each part and at the second hand's number (D32); the parts are 100 ms apart with a second hand, 200 ms without (D36).
5. **A player's arrow number is at launch + 192 ms whatever the distance** (D33); the arrow vanishes earlier for a near target.
6. **The attack cycle is the server's.** In the dual-dagger Short (C, ASPD 172) the cycle is about 533 ms against an `amotion` of 280 ms. That is no surprise and not a doubled `attackMT`: the observed cycle is the server's `ADelay`, which is 2 × `amotion` (Hercules sends `amotion` as `attackMT`); at 188 ASPD (katar) the same. This client uses the packet's value, never a formula. No code.
7. **Green numbers are heals** (A `22-47-07` f492): SPEC-0012's (BACKLOG); nothing is drawn for a negative damage here.
8. **The white slash arc and sparkles at the hit** (A, B) are a hit effect: SPEC-0013's (plan §4.8), not drawn here.
9. **Hit moment of ranged and whip blows.** The bow's number comes at `At + 0.56 × attackMT` (launch + 192, which this client does, D33); the whip's about 84 ms after the whip unfurls at `attackMT` 380 (B). The melee hit moment stays `At + attackMT` (FR-4, owner 2026-11-23); the whip's early number is a question for the spec (BACKLOG).
10. **Katar (C, Classic only):** two criticals per attack, the small one 83 to 100 ms after the big one; here the second number is `LeftDamage` at `LeftHandMs` 350. Not in the owner's footage (BACKLOG).

## roBrowserLegacy timing constants (D36)

Source: roBrowserLegacy master, `src/Engine/MapEngine/Entity.js` `onEntityAction` (read 2026-11-23, quoted by a reader agent).

| roBrowserLegacy | Value | Ours |
|---|---|---|
| `C_MULTIHIT_DELAY` (`PLUSATTACKED_MOTIONTIME`) | 200 ms | `MultiHitNoLeftMs` 200 (the gap without a left hand); half of it, 100, is `MultiHitMs` (with a left hand) |
| single hit, number | at `attackMT` | hit moment `At + attackMT` (FR-4) |
| single hit, left hand | `attackMT + 200 × 1.75` = 350 after the hit | `LeftHandMs` 350 |
| multi-hit on a monster, part numbers | `attackMT` and `attackMT + C/2` (+100) with a left hand, `attackMT + C` (+200) without | `i × MultiHitMs` with a left hand, `i × MultiHitNoLeftMs` without |
| multi-hit total | `attackMT + 200`, or `attackMT + 350` with a left hand | a running total at each part and at the second hand (D32); not the same rule, footage C decided it |
| hurt of a multi-hit | triggered per hit at `attackMT + C × i` | each part starts its own hurt at its number's time |
| death of a multi-hit | the last hit + 200 ms | `HurtBeforeDeathMs` 250 after the last hit moment (H9) |
| `AVG_ATTACK_SPEED` | 432 ms (the default `attackMT`) | none: the packet's value is always used |
| `MAX_ATTACKMT` | 864 ms (`attackMT` is clamped to it) | **none: this client does not clamp the hit moment** (plan D3, uncapped). A difference, no code |

Attack speed enters only through `attackMT` (the packet's value, which sets the hit moment and the animation speed here); every gap above is a constant, not scaled by it.

## Owner's notes on the footage (2026-11-22)

The owner recorded five videos of the original client: `Ragnarok 2026-11-22 22-*.mp4`. T0.3 mapped them onto the shots of plan §6.3 (`evidence/T0.3/`) and T9.3 filled the **Measured** columns from them. What the owner noted:

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
