# T0.3 part C: dual dagger, katar, double attack (Ragnarok Online CLASSIC footage)

Videos (portrait phone recordings of RO Classic, 1080 x 1920; frame = `round(t * fps)`):
- **dual**: `Basic Dual Dagger Assassin`, 60 fps, 89 s. Status window in frame (key frame `C_status-aspd172_dual_t5.jpg`): Aspd **172**, Critical 1, Agi 99+26, two daggers (+5 Damascus and another +5 dagger), Thief Clothes.
- **a188**: `188aspd permanent crit ... assassin cross`, 29.97 fps, 17.6 s (katar, permanent critical, 188 ASPD; the status window is not on screen).
- **crit**: `Crit SinX v Tao Gunka`, 60 fps, 67 s (katar, criticals; ASPD not on screen).

All frame numbers below are frames of that video; "+n" is n frames after the stated frame (60 fps: 1 frame = 16.7 ms; a188: 33.4 ms). Frame counts were read by eye from 1 to 3-frame zoom sheets: error about +-1 frame (a188 +-1 frame = +-33 ms).

## Timeline (plan §4.6)

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| `MultiHitMs` (gap between the parts of a double attack) | 200 ms | **100 ms (6 frames)** between part 1 and part 2 | dual: parts 549 / 549 at f162 and f168; 429 / 429 at f4719 and f4725 (78.65 s, 78.75 s) | high (2 samples, both 6 frames) | **contradicted**: 100 ms |
| multi-hit total, when it appears | with the last part | a yellow total appears **with the first part** (the running sum so far) and is **replaced by a new running sum with each following number of the same attack**, including the left-hand number (see "How our timing differs") | dual: yellow 549 at f162 (with part 1), 1098 at f168, 1261 at f184; second sample yellow 429 (f4719), 858 (f4725), 1009 (f4740) | high for the pattern, medium for the rule behind it | **new value** |
| multi-hit total, over a player | none | not visible (all samples are on monsters) | - | - | not visible |
| `LeftHandMs` | 350 ms | **left-hand number 20 to 21 frames after the first part / right hand: 333 to 350 ms** (about 14 frames = 233 ms after the second part of a double attack) | dual: f162 -> f182 (20), f4719 -> f4740 (21); non-double attacks: right 532 at f227 -> left 168 at about f247 (20) | high | **confirmed** (333 to 350 ms; keep 350 or use 340) |
| `StandbyMs` / standby pose | -1 | not measured: the character is hitting all the time; after an attack the sprite shows a ready pose (arms in front, f174-f180 and f190-f195, dagger up) that it holds until the next swing | dual z6 sheet | low | not visible (no idle period) |
| hit moment (`attackMT`) | At + attackMT | the **monster's hit flash starts the same frame the first number appears**: f162 for the first blow, f183 for the left hand; the number and the flash are in sync, the weapon trail starts BEFORE them (f4708 trail starts, f4719 number: 11 frames) | dual sheets z2, z6 | medium: the swing start of each hand is hard to tell apart | consistent with "number at the hit moment", not enough to read blow start vs hit |
| hurt motion | `attackedMT` | the monster plays a flinch (squashed, dark face) for about 12 frames (200 ms): f162 to f173 for the first blow | dual z6 | medium | **new value**: about 200 ms per blow, restarted by the next blow |

## Attack animation

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| one attack cycle (right number to the next right number) at ASPD 172 | amotion = 2000 - 10 x 172 = **280 ms** (Hercules, below 190: `amotion = 2000 - aspd x 10`) | **about 533 ms (31 to 32 frames)**: right-hand numbers at f196, f227, f259, f291 (31, 32, 32 frames) and 4 more at 30 to 39 frames. That is **about 1.9 x amotion** | dual sheets z5, z6 | high for the 533 ms; the reason (dual wield, or this client) is not known | **new value**; flag for the plan: see "Rules and surprises" 1 |
| one attack cycle, katar | ASPD 188 -> amotion 120 ms | **about 230 to 250 ms: 7 frames at 29.97 fps (a188) and 15 frames at 60 fps = 250 ms (crit f1226 -> f1241)**, again about 2 x amotion (a188 ASPD 188 -> 240 ms; the ASPD of `crit` is not on screen) | a188 sheet z7: new big numbers about every 7 frames; crit z8 | medium | **new value**, same factor 2 |
| which of Attack1/2/3 (dual dagger) | Attack3 | **one motion contains two swings, one with each dagger**: right dagger slash trail to the left (f183 to f190), then the left arm extended to the right with a stab (f164 to f166, f197 to f201), then back to the ready pose; the motion is not a single stab. That matches the two-weapon Attack3 of the assassin; the sprites were not compared with an Attack2 reference | dual z6 | medium (no Attack2 reference clip) | **consistent** with Attack3 |
| which motion (katar) | Attack3 | not checked at sprite level (the character is too small and hidden by the starburst) | - | - | not visible |
| blows restart or overlap | - | blows do **not** overlap: the sprite finishes the swing, the ready pose shows between (f174 to f180, f190 to f195), then the next motion starts; the cycle is the 33 frames of "pose A to pose A" (f165 -> f198) | dual z6 | medium | **new value** |
| motion length vs cycle | `FitMs` fits the motion to `attackMT` | the visible motion takes the whole 533 ms cycle: pose A f165, ready f174 to f180, swing f183 to f190, ready f190 to f195, pose A f197; i.e. the sprite is stretched over the cycle, not played at 280 ms and then idle | dual z6 | medium | consistent with fitting to a cycle of about 530 ms |

Derived `attackMT` the server would send: formula says 280 ms (ASPD 172), the footage says the sprite and numbers repeat every 533 ms. Either the original sends `attackMT` = 2 x amotion for a dual wielder, or this Classic server does. The data of part C cannot say which; the single-hand (non-dual) cadence must come from the part A/B footage (whip, bare hand) to compare.

## Numbers (plan §4.8)

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| number on a monster | white | **white** with a black outline, tall digits | dual all, crit f1212 | high | confirmed |
| number picture, start and motion | scale 4 -> 0, up and +x | a part appears **very large (about 4 x), low, and shrinks while it rises and fades**; the left-hand and right-hand numbers sit left of and right of each other; no sideways drift larger than a few px was visible | dual z3, z9 | medium | consistent |
| `NumberMs` life | 1,500 ms | **at least 1.2 s up to about 1.5 s**: 519 (f196) is still readable at f276 (4.6 s... see next), 163 (f219) is small at f243 (4.05), so shorter than 1.5 s for the small ones; 549 (f162) is visible small at f216 (3.60, 0.9 s) | dual z9 | low (the numbers shrink to tiny; fade not resolved) | not contradicted |
| critical (katar) | (0.9, 0.9, 0.15) over msg 3 | **yellow digits** (bright yellow with a black outline) over an **orange-red 8-pointed starburst** (msg 3), with white-yellow streak lines around the hit; each crit has its own starburst; the starburst is **about as wide as the number (about 1.3 x the 3-digit width)** and behind it | a188 z7, crit z8 | high | confirmed picture; colour of the digit looks (1, 1, 0) fully saturated yellow rather than 0.9,0.9,0.15 (JPEG 4:2:0; low confidence) |
| katar second number | - | a katar attack gives **two criticals**: a big one (about 730 to 740 in `crit`, about 1160 in a188) and a **smaller one about 20 % of the first** (149 / 150 in `crit`; about 237 in a188 against 117x?): both have the starburst; the small one appears **5 frames (83 ms) after** the big one (f1241 big, f1246 small in `crit`); in a188 the two (117x and 23x) are about 3 frames (100 ms) apart | crit z8, a188 z7 | medium | **new value**: katar = two numbers per attack, about 85 to 100 ms apart, both critical when the attack is a critical |
| multi-hit total | (0.9, 0.9, 0.15), yellow-green | **bright yellow, plain (no starburst), no black-box: same font as a critical**, smaller than the parts when it first appears (scale grows from tiny to about 1.5 x of a part) and then **stays almost still for about 2.5 s** | dual z5: 1261 from f184 (3.07 s) to about f336 (5.6 s) | high | consistent with TotalMs 3,000 ms (2.5 s seen) and `TotalHeight`: at about 1.5 digit heights above the target's head |
| total height / scale (`TotalHeight`, `TotalScale`) | 7 cells, 2 | the total sits **about 80 px (about one character height) above the head**, in the same place for every update; its first frame is small (about 0.4 x of its final size) and it grows over about 6 frames | dual z3 f168 -> f172 | medium | **new value** (growth from small, not constant 2) |
| number density | newer over older | **numbers overlap, not stack**: they fly at the same place and cover each other, newer drawn over older (the white 163 over the yellow 1261, etc.); no repositioning | dual z5, a188 z7: up to 8 digit groups at once | high | confirmed `order: newer over older` |
| miss over a player | red | **red "Miss"** appears over the **own** character (small, left) in `crit` f1214 to f1224; white "Miss" over the monster | crit z8, dual z6 ("Miss" white over the poring) | high | confirmed |
| damage over a player | red | red digits (`92`, red 9 7 5 in a188 at 7.5 s) | crit 54 s, a188 7.5 s | medium (frames not zoomed) | confirmed colour |

## How our timing differs

Original (dual dagger, ASPD 172) in numbers; f = frame at 60 fps; f0 = the frame the first number of an attack appears.

1. **Double attack parts.** Original: part 1 at f0 (big white number, the monster flinches the same frame), **part 2 at f0 + 6 (100 ms)**, each a separate white number. Ours: 0 and 200 ms. Change `MultiHitMs` to 100.
2. **Total.** Original: a yellow total appears **at f0 together with part 1** and shows the running sum (549); at f0 + 6 it is **replaced** by 1098; the left hand's number at f0 + 20 makes it **1261** (= 549 + 549 + 163) at f0 + 22. Every update is a new small number that grows; the older one is gone at the update (1098 visible about 220 ms, 1261 about 2.5 s). Ours: one total with the last part, over a monster only. So: the original shows a running total, first number at part 1, and it **includes the left hand** (a second sample: 429, 858, 1009 = 858 + 151). A total is shown only for an attack with a double attack; plain right plus left (532 + 168) showed none.
3. **Left hand.** Original: **20 to 21 frames (333 to 350 ms) after part 1**, i.e. 14 to 15 frames (233 to 250 ms) after part 2. Ours: 350 ms after the right hand's number (part 1 or the last part?): equal when it is counted from part 1 (confirmed), 100 ms late if counted from the last part.
4. **Cycle.** Original: the whole sequence (right, optional double part, left) repeats every **about 533 ms** at ASPD 172. A server amotion of 280 ms would give a cycle of 280 ms; the footage shows 1.9 x that. If our `UnitClock` plays the motion at `attackMT` = 280 ms and repeats, we would show about twice the original's attack rate, with the left-hand number (+350 ms) landing after the next right-hand blow's number (+280 ms): the order would be R L R instead of R, L, R with the left hand before the next right (original: R at 0, L at +350, next R at +533).
5. **Katar.** Original: two criticals per attack, small one 83 to 100 ms after the big one, the cycle 230 to 250 ms. Ours has no katar rule (hit stream is the server's).

## Rules and surprises

1. The original's visible cycle (533 ms at ASPD 172; 230 to 250 ms at ASPD 188 and in `crit`) is about **2 x (2000 - 10 x ASPD)**. We do not know whether the server sends the doubled amotion in `attackMT`/`ZC_NOTIFY_ACT` `sDelay` (Hercules dual wield: the right and the left amotion are added, then x 7/10, so for two daggers the amotion is about 1.4 x one dagger's) or whether this Classic server differs. It must be checked in T9 against the live Hercules: use the server's own `amotion`, never a formula.
2. The yellow total includes the left-hand damage and is updated as numbers arrive; it is not "yellow-green" in this client: it is the same yellow as the critical digits, without the starburst.
3. Number drawing order follows the arrival; digits overlap (see density).
4. Parts of a double attack are **white** (monster target), like single hits: confirmed (`2 9`, `4 2 9` white big).
5. This is Classic: the font is wider-spaced than the plan's `DigitSpacingPx` 2 (not measured); the lingering small yellow totals are drawn smaller than the parts.
6. Not answerable from part C: the number's exact lifetime and fade, the idle pose after a fight, Attack2 vs Attack3 at sprite level (needs a reference clip of an Attack2 jobs), the single-hand cadence at a known ASPD, a double attack over a player.

## Key frames (in this folder)

- `C_doubleattack-parts-L_dual_t2.6-3.6.jpg`: f156 to f216 every 2 frames, double attack 549 + 549 (f162, f168), yellow 549 -> 1098 -> 1261, left hand 163 at f182.
- `C_player-poses_dual_t2.6-3.4.jpg`: every frame f156 to f203, close crop of the character: poses, the monster's flinch.
- `C_doubleattack-429_dual_t78.4-78.8.jpg`: every frame f4704 to f4727, second double attack, 429 at f4719 and f4725, tiny yellow 429 / 858.
- `C_RL-cadence_dual_t3.6-6.0.jpg`: right and left numbers alternating (163 / 532, 168 / 531, 167 / 533, 159 / 495, 201 / 508), 3 frames apart.
- `C_number-lifetime_dual_t3.6-4.6.jpg`: wide crop, how numbers rise, shrink, linger; the yellow 1261.
- `C_status-aspd172_dual_t5.jpg`: status window, Aspd 172, equipment.
- `C_katar-crit_188aspd_t1.5-2.5.jpg`: katar criticals every frame at 29.97 fps (starburst, yellow digits).
- `C_katar-crit-pairs_critTao_t20.2-20.8.jpg`: katar criticals every frame at 60 fps: 149 f1214, 728 f1226, 730 f1241, 149 f1246; red "Miss" over the own character.
