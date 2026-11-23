# T0.3 part A: measured values (videos 22-32-58 and 22-47-07)

Videos 1920 x 1080. `22-32-58`: 59.43 fps, 3390 frames. `22-47-07`: 59.70 fps. Frame f = round(t * fps); times below are f / fps. All lengths are video pixels at the owner's default zoom.

**Cell size.** One ground cell is about **50 px** at this zoom, by two methods: the green ground-cursor bracket is 51 x 50 px; and while the acolyte walks, the ground scrolls at about 333 px/s (phase correlation, 22-32-58 f520 to f620: about (22, -25) px per 0.1 s), which is 1 cell per 150 ms at 50 px. The camera looks nearly straight down: the bracket is a square on screen, so ground x is screen x and ground y is screen y with no foreshortening. A "cell of height" (z) cannot be measured separately. The camera also drifts slowly (about 10 px in 0.5 s) while the acolyte moves, which adds about 10 px of error to the paths below.

**Acolyte ASPD** (stats window, `A_stats_aspd_22-32-58_t10.1.jpg`): Aspd 157, Agi 1. In Hercules `ADelay = (200 - 157) * 10 = 430 ms`, `amotion` about 215 ms. This is the attackMT of the own blows (derived, not read from a packet).

Verdicts: confirmed / contradicted / new value / not visible. Confidence: n = number of samples.

## Timeline (plan §4.6)

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| hit moment (own blow, adjacent poring) | `At + attackMT` | The attack cursor appears at 17.55 s (click). The number `26` and the white slash hit effect both appear on f1059 (17.82 s). 0.27 s from click to number, against `amotion` about 215 ms plus a short round trip. The number and the hit effect are the same frame. | 22-32-58 f1040 to f1066, `A_miss_red_and_blow...jpg` | n = 1 clean; the acolyte is seen from behind, so the start of the punch is not readable in the sprite | consistent with `At + attackMT`, not proven |
| hit moment, Medusa | `At + attackMT` (1320 ms) for every attacker | The Medusa's strike pose (tentacles up) runs f355 to f379 (5.95 to 6.35 s, about 0.4 s). The "LUCKY!!" dodge text over the Archbishop appears f361 (6.05 s), 0.10 s after the pose starts. No number shows 1.32 s after the pose starts. The only damage number in the clip comes at f416 (6.97 s), 1.0 s after the pose start, with a pink and white skill effect. It cannot be tied to this strike. | 22-47-07 f355 to f424, `A_medusa_attack...jpg` | n = 1 strike; the Medusa's pose and the Archbishop overlap, and which motion belongs to which result is not certain | NOT confirmed: the dodge text came 0.1 s after the strike started, not 1.32 s. The plan's rule for the Medusa needs a clean basic hit |
| hurt motion | `attackedMT` | Poring: hidden by the 4x number and the hit effect for f1059 to f1066; no visible change of the poring sprite afterwards. Archbishop: not distinguishable in the overlap. | | n/a | not visible |
| `StandbyMs` | -1 | After the last blow at about 21.3 s the acolyte stays in the same fist pose (arms folded in front) for at least 14 s of standing (f1307 to f2080, `pose` crops). The pose at 1.0 s and 3.0 s, before any blow, is identical, so the pose is also the plain idle of this sprite. | 22-32-58 | n = 1 stretch | consistent with -1, but the combat pose and the idle pose look the same for the bare-handed acolyte |
| cancelled attack | (rule unknown) | No clean cancelled-by-walk sample found. The number at 17.82 s is followed by the acolyte turning and walking from about 18.2 s (f1084 on), after the number. The mark README rows (20.29 s: click on a poring 4 cells away, number at 21.25 s on arrival) show a normal approach then blow. | | none | not answered |
| `HurtBeforeDeathMs`, death | 250 ms | Not measured in the time available. Pupa killed at about 55.5 s (`2 3`, then it sinks into a green puff at 56.0 s). Porings leave flat pink corpses at 15 s to 16 s. | | | not measured |

## Numbers (plan §4.8)

Reference sample: the first blow, 22-32-58, f1059 (17.82 s) to f1144 (19.25 s), poring right of the acolyte. `A_number_size`, `A_number_path`, `A_number_fall`.

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| `NumberMs` | 1,500 ms | Visible f1059 to f1142 (19.22 s) and gone on f1144: 1.40 s until the fade makes it invisible. About 1.5 s with the last alpha. | `A_number_fall` | n = 1 measured, other numbers (35.9 s, 21.2 s) look the same | confirmed (to about 0.1 s) |
| `NumberScaleStart`, `NumberScaleEnd` (4 -> 0) | 4 to 0 over the life | Digit height 78 px at birth (f1059), 75 (f1062), 62 (f1066), 47 (f1070), 40 (f1074), 28 (f1078), about 20 px from f1082 (0.39 s) **to the end**; it does not go to 0. So the scale goes **4 -> 1 in about 0.35 s** (slow, fast, slow; smooth-step like) and stays at 1. At 1 the digit is about 20 px tall and 18 px wide; digit advance 23 px at scale 1 and 93 px at scale 4. | `A_number_size`, `A_number_path` | n = 1 full curve; the small final size is the same for 6 numbers in `A_number_drift_births` | **contradicted** (lead 4 -> 0) |
| `DigitSpacingPx` | 2 px | Advance between digits is 23.3 px per unit of scale in video pixels. The sprite digit width is not known, so the 2 px gap cannot be checked. | | n = 1 | not decidable |
| `NumberDriftX` | 4 cells to +x | x of the number centre: 983 (age 0), 1053 (0.52 s), 1072 (0.66 s), 1110 (0.96 s), 1132 (1.16 s), 1136 (1.40 s). Total **+153 px, 3.1 cells**, decelerating (not linear). In 5 of 6 further monster numbers the drift is also to the right (`A_number_drift_births`). A red number on the player in 22-47-07 drifted **left** (see the player row), and red `1` on the player at 25.5 s sits to the left of the acolyte. | 22-32-58, 22-47-07 | n = 1 tracked, about 6 seen | **contradicted** in size (3.1, not 4) and the direction is not always +x |
| `NumberDriftY` and `NumberHeight...` | up and down arc, 4 cells in -y plus `2 + 5 sin` | Centre y: 511 (0), 457 at the peak (age 0.52 s, 35 % of life), 507 (0.96 s), 566 (1.16 s), 705 (1.40 s). The bottom edge of the digits: 550 at birth, 467 at the peak, so the rise is **83 px** (about 1.7 cells) and the fall from the peak to the end **about 250 px** (5 cells). Peak at one third of the life as the lead's sine says. The fall speeds up: 20 px per 4 frames at 1.1 s, 45 px per 4 frames at 1.3 s. | 22-32-58 f1059 to f1142 | n = 1 | peak time confirmed; the amplitude in px is smaller than the lead's 5 cells unless 1 height cell is about 17 px; shape (rise, then faster fall) confirmed |
| start position | over the target | Number centre (983, 511) at birth. The poring centre was about x 975 and its feet about y 515. The 4x digits cover the poring and reach about 35 px below its feet. | f1059 | n = 1 | new value |
| alpha | `1 - t` | Solid until about 0.7 s (f1100), about half at 1.0 s (f1116), about 0.3 at 1.2 s, invisible at 1.40 s. Looks linear from about 1/3 of the life, not from 0. | `A_number_fall` | by eye, n = 1 | roughly confirmed, not measured exactly |
| `NumberOnMonster` | white | White with dark outline and a light grey shading (`26`, `23`) | all | n many | confirmed |
| `NumberOnPlayer` | red | Red digits with dark outline on the Archbishop (`244`, 22-47-07 f416) and red `1` on the acolyte (25 s, 45.5 s of 22-32-58) | `A_medusa_numbers` | n = 3 | confirmed |
| heal | not in the plan | A **green** `244` (same size, same arc) appears at f492 (8.24 s) in 22-47-07, 1.27 s after the red `244` | `A_medusa_numbers` | n = 1 | **new value**: heal numbers are green |
| player number size and path | like monster | Red `244`: 70 px high at birth, still big at 7.1 s (0.15 s), small (about 20 px) by 7.5 s, then drifts **left** (centre x 940 -> 852 -> 830 -> 805 at 7.0, 7.64, 7.84, 8.04 s) and down (y 520 -> 485 peak at 7.64 s -> 565 at 8.04 s) | `A_medusa_numbers` | n = 1 | same arc as monsters, x mirrored |
| `NumberCritical`, critical background, `NumberTotal`, multi-hit | | no critical or multi-hit in these two videos | | | not visible |
| order | after units, no depth test | The 4x number covers the poring and the acolyte, and the digits stay over the white hit effect | f1059 | | confirmed (loosely) |

## Messages: miss and lucky (plan §4.8)

Samples: white Miss over a poring (22-32-58 f1388 to f1466), red Miss over the acolyte (f1005 to f1094), "Miss" repeated over the Medusa and "LUCKY!!" (22-47-07).

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| **anchor of Miss** | (not in the lead; assumed over the target) | The Miss is shown over the **attacker**, not the target. Over a poring that stands next to the acolyte while she does nothing (23.4 s) it is white; over the acolyte when her own blow missed (pupa, which never attacks, at 53.5 s and 54.0 s; also the poring at 16.9 s and 17.4 s) it is red. In 22-47-07 the many white Misses start at the Medusa. | `A_miss_white`, `A_miss_red_and_blow` | n about 6 | **new rule**: Miss comes from the missing unit; tint white for a monster, red for a player |
| `MessageMs` | 800 ms | White Miss: first on f1388 (23.355 s), invisible on f1466 (24.67 s): **1.31 s**. Red Miss: first f1005 to f1008 (16.95 s), faint at f1092 (18.37 s): **about 1.4 s**. The alpha is 1 at the start and decreases roughly linearly, so the real life is about 1.5 s | `A_miss_fade` | n = 2 | **contradicted** (about 1.4 to 1.5 s, not 0.8 s) |
| `MessageScale` | 0.5 | Constant size: width 45 px, height about 14 px, from first to last frame (no scale change) | | n = 2 | size 45 x 14 px new value (the picture size at scale 0.5) |
| `MessageHeightStart`, `End` (3.5 -> 7 cells) | rises 3.5 cells | Rises at a constant 100 to 110 px/s (1.7 to 1.9 px per frame): **about 130 px (2.6 cells at 50 px)** in 1.3 s. White Miss centre y 488 -> about 358. It starts about 20 px above the poring's feet (poring feet y about 515, Miss centre 495 at birth), so about 0.4 cell above the feet and ends about 3 cells above | `A_miss_white` | n = 2 | **contradicted** in size (2.6 cells rise, constant speed) |
| tint | white over monster, red over player | White (grey outline) over the poring and the Medusa; dark red with dark outline over the acolyte | | n many | confirmed |
| Miss x | | x does not drift (white Miss x 862 to 904 for 1.3 s) | | n = 1 | new value: no drift |
| lucky | `msg` 4, 5, rise like Miss | "LUCKY!!" (orange letters, red outline) appears **over the Archbishop's head** (the dodging unit), f361 (6.05 s), fully visible to f388 (6.50 s), gone f394 (6.60 s): **about 0.55 s**. It does **not rise**: y 440 on f361 and on f388. It appears 0.10 s after the Medusa's strike pose starts. | `A_medusa_attack` | n = 1 | **contradicted** (no rise, about 0.55 s) |
| Miss count | | Several Miss at once over the Medusa (up to 3 on screen), spawning at 0.2 to 0.7 s intervals | 22-47-07 f293 to f418 | | new: many Misses are alive at once, they stack at different heights |
| `TotalMs`, `TotalHeight` | | not seen | | | not visible |

## Bars (plan §4.8, shot 10)

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| `HpBarPx` (monster) | 60 x 5 | 60 px wide including the 1 px frame (fill 58 px), 5 rows high: frame row, 2 fill rows, 2 more rows (fill 56 px of 58 at 50 % HP in the pixel count) | 22-32-58 f1062 | n = 3 frames | confirmed |
| `HpSpBarPx` (own) | 60 x 9 | 60 wide, 9 rows: border, 3 HP rows, 1 divider, 3 SP rows, border (rows 554 to 562 on f1062) | | n = 3 | confirmed |
| `OwnHp`, `OwnSp`, frame | `#10ef21`, `#1863de`, `#10189c` | Pixels: HP fill (31, 255, 32) = `#1fff20`; SP fill (39, 99, 218) = `#2763da`; divider and frame (0, 21, 163) = `#0015a3`. Close to the lead, shifted by video compression | | n = 1 | confirmed within the video's colour error |
| `MonsterHp` | `#ff00e7` | magenta, dark-blue frame and dark-blue empty part visible | | n = 3 | confirmed |
| `BarBelowFeet` (0.5 cells) | 0.5 | Monster bar rows 534 to 538 against feet at about 520 to 530: the bar starts directly under the poring's feet (0 to 14 px). Own bar top is 2 to 6 px under the acolyte's lowest shoe row. About 0.2 cell, not 0.5 | f1062 | feet row is hard to read | probably contradicted (gap about 10 px, not 25); one sample |
| when the monster bar appears | | The bar shows on f1052 (17.70 s), 0.15 s after the click (17.55 s) and 0.12 s before the number. The own bar is present all the time in this video (it shows once she is in combat; absent before 17 s) | f1049 to f1052 | n = 1 | new value |
| own HP falling | | In 22-47-07 the green fill length is unchanged after the red `244` (f400 to f700) | `A_ownbar_22-47-07` | the Archbishop probably has a very large HP, or the number came with a heal | not visible |
| `BarLowShare` | 25 % | not seen (no low bar) | | | not visible |

## Input and restart (shot 14)

| Row | Value | Measured | Verdict |
|---|---|---|---|
| `HoldAttackMs`, `PickMinPx` | 300 ms, 24 px | The cursor changes to the attack cursor (sword) over the poring on f1040 (17.50 s) and the click lands about 0.05 s later; no hold or release sample found. A player standing in front of a monster and a ground click right after a blow were not isolated in the time available. | not answered |

## Rules and surprises

1. **Miss belongs to the attacker.** The number goes over the target, the Miss over whoever missed. A monster's miss floats up white from the monster; the acolyte's own miss floats up red from the acolyte. The plan says "over a player or a monster" without saying which of the two.
2. **Number scale is 4 -> 1, then fixed at 1** (done in about 0.35 s), not 4 -> 0. At scale 1 a digit is about 20 px high and 23 px apart in the video.
3. **Number lives 1.5 s, Miss lives about 1.3 to 1.5 s** (not 0.8 s), and the Miss rises at a constant speed with no x drift.
4. **Numbers drift mostly to +x but a number on the player went left** (22-47-07, and the red `1` at 25.5 s). The sign may depend on where the attacker stands (the player's attackers were on the left; for the poring the attacker was on the left and the number went right, so it is not simply "away from the attacker").
5. **Heal numbers are green** and use the same arc as damage (22-47-07 f492).
6. **"LUCKY!!" is a short, fixed message** (about 0.55 s, no rise) over the dodging unit, and it shows 0.1 s after the Medusa's strike pose starts. A result that appears 0.1 s after the strike starts does not fit "hit moment = At + 1320 ms" for the Medusa; a clean basic hit is needed to decide.
7. Many Miss messages are alive at once over one monster, spaced by their spawn time, with no cap seen (up to 3 on screen).
8. The hit effect (white slash blades) starts on the same frame as the number and lasts about 0.2 s (f1059 to f1066).
9. The own HP and SP bars are 9 px high, the monster bar 5 px; the monster bar appears about 0.15 s after the click.
10. The camera slowly follows the acolyte, so screen positions of floating numbers include about 10 px of scroll.

## Not answered (needs more footage or another tool)

- Hurt motion length of the poring and the Archbishop, and the pose after the blow (the poring sprite is hidden by the hit effect).
- A cancelled attack (walk after the click).
- Death timing and corpse time (porings leave flat pink corpses at 15 s to 16 s; the pupa dies at about 55.5 s).
- Shot 14 (hold, release, a player in front, a ground click right after a blow).
- A clean Medusa basic hit (all basic attacks here missed).

## Key frames saved (12 JPEG sheets in this folder)

| File | Shows |
|---|---|
| `A_number_size_22-32-58_t17.8.jpg` | number birth and shrink, f1058 to f1090, 20 px grid |
| `A_number_path_22-32-58_t17.9-18.6.jpg` | number path f1062 to f1106 |
| `A_number_fall_22-32-58_t18.5-19.3.jpg` | number falling, f1098 to f1146 |
| `A_number_drift_births_22-32-58.jpg` | 6 more numbers, drift to +x |
| `A_miss_red_and_blow_22-32-58_t17.4-18.4.jpg` | red Miss over the acolyte and the blow, f1034 to f1092 |
| `A_acolyte_blow_22-32-58_t17.6-18.5.jpg` | acolyte sprite f1044 to f1102 |
| `A_miss_white_22-32-58_t23.3-24.5.jpg` | white Miss over the poring, rise |
| `A_miss_fade_22-32-58_t23.3-24.9.jpg` | the Miss fading, last frames |
| `A_stats_aspd_22-32-58_t10.1.jpg` | stats window, Aspd 157 |
| `A_medusa_attack_22-47-07_t5.5-7.1.jpg` | Medusa strike pose, LUCKY, Miss, red 244 |
| `A_medusa_numbers_22-47-07_t6.9-8.4.jpg` | red 244 arc, green 244 heal |
| `A_ownbar_22-47-07_t6.7-11.7.jpg` | own HP and SP bar, unchanged |
