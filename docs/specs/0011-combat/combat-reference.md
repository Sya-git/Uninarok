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

## Timeline (plan §4.6)

| Setting | Value | Source | Measured (shot, frames) |
|---|---|---|---|
| hit moment | `At + attackMT`, for every attacker | FR-4, D3 (not the server's cap of H8) | shot 16 |
| hurt motion | `attackedMT` of the blow | FR-4 | shots 1, 4 |
| `StandbyMs` | −1 (until the player does something else) | lead, plan §4.6 and D11 | shots 1, 8 |
| `MultiHitMs` | 200 ms between the parts | lead, plan §4.6 (roBrowserLegacy) | shot 5 |
| multi-hit total | with the last part, as `Total` | lead, plan §4.6 (roBrowserLegacy) | shot 5 (where and how) |
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
| projectile by attacker rows | T4.8 | lead: spec §6 | shots 11 to 13 |

## Attack animation (plan §4.7, `AttackMotions`)

Lead: roBrowserLegacy `src/DB/Jobs/WeaponAction.js` (read 2026-11-23). Its job constants are given as Hercules job ids here. **1, 2, 3** stand for `Attack1`, `Attack2`, `Attack3` (the file's 0, 1, 2).

Rules:
- A job, a weapon type or a sex (other than 0 female or 1 male) that is not listed gives 1.
- Units that are not players always play `Attack`.
- The job is the one whose sprites are used. Baby, high and transcendent jobs, and the third jobs that roBrowserLegacy copies from a base job, are looked up by that base job: the caller maps them, T5.4.
- Not listed: Druid and its jobs, whose ids are not known here.
- With two weapons, the two-weapon row is used: assassin only.

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

Two more lead facts, not used here:
- roBrowserLegacy also shortens the attack of monks with knuckles or bare hands (5 frames from frame 0), and of summoners with bare hands (4 frames).
- `UnitClock.FitMs` (plan §4.9) fits every attack to `attackMT`, so this is a T0.3 question.

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
| `DigitSpacingPx` | 2 px | lead: `Damage.js` | shot 1 |
| `NumberOnMonster` | white | lead: `Damage.js` | shot 1 |
| `NumberOnPlayer` | red | lead: `Damage.js` | shot 4 |
| `NumberCritical` | (0.9, 0.9, 0.15), over the `msg` critical background | lead: `Damage.js` | shot 2 |
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
| restart prompt labels and order | T0.4 | owner | T0.4 |

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
