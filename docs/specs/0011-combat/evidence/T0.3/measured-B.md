# T0.3 part B: bow (shot 11) and whip (shot 13)

Videos (1920 x 1080, about 59.5 fps, frame = `round(t * fps)`, one frame = 16.8 ms): `Ragnarok 2026-11-22 22-39-21.mp4` (dancer, bow, Roween) and `22-41-02.mp4` (dancer, whip). Read by zoom sheets one frame apart (frame numbers `f...` below are the video's). Frame sheets are in this folder as `B_*.jpg`.

Scale: the green ground-cell cursor brackets measure **52 px wide x 50 px tall** at the footage's zoom (`B_*` frame 618, brackets at abs 1333..1385 x 465..515). Cell distances below use that (dx/52, dy/50, distance = Euclid) between the shooter's and the target's HP bars (both bars hang a fixed offset below the feet). The scale and the bar offset are estimates: distances are +-0.5 cell.

Players' stats window: video 1 ASPD **161** (attack delay 2 x (200 - 161) x 10 = 780 ms); video 2 ASPD **181** (380 ms). Measured arrow-to-arrow interval in video 1: 47 / 46 frames = 783 ms, so `attackMT` here is about 780 ms.

## Arrow events measured (video 1)

| # | Shooter-target distance | Attack start (first changed pose) | Arrow first / last seen | Number + hurt + white hit arc first seen | Sheet |
|---|---|---|---|---|---|
| 1 | about 7.1 cells (dx 356 px, dy 92 px, monster nearly still) | f621 (10.45 s) | f636 (10.70) / f645 (10.85) = 10 frames | f647 (10.89 s) | `B_arrow7cells_22-39-21_10.40s.jpg`, `B_attackstart_...` |
| 2 | about 5.3 cells (dx 270, dy 60); the monster WALKS toward the archer | f668 inferred (47 frames after #1) | f683 (11.49) / f689 (11.59) = 7 frames | f694 (11.67 s) | `B_arrow5cells_movingtarget_22-39-21_11.25s.jpg` |
| 3 | about 1.2 cells (adjacent, dx 43, dy 39) | f714 (12.02 s) | f729 (12.27) / f731 (12.30) = 3 frames (partly behind the sprites) | f740 (12.46 s) | `B_arrow1cell_22-39-21_12.17s.jpg` |
| 4 | far, about 11 cells across, 7 cells up (about 13 Euclid; beyond the 8-cell cap) | not framed (about f1917) | f1932 (32.50) / f1942 (32.67) = 11 frames | f1943 (32.68 s) | `B_arrowfar_launch_...32.30s.jpg`, `B_arrowfar_hit_...32.60s.jpg` |
| 5 | about 9.5 cells (monster walks away to the right) | not framed | f2025 (34.07) / f2032 (34.19) = 8 frames (tip ends 110 px short of the monster's launch-time spot) | f2035 (34.24 s) | `B_arrow9cells_22-39-21_34.02s.jpg` |

Derived: attack start to arrow first seen = **15 frames (252 ms)** in #1 and #3 (and 15 in #2 by inference); arrow first seen to number = **11 frames (185 ms)** in #1, #2, #3, #4, and 10 frames (168 ms) in #5; attack start to number = 26 frames (437 ms) in #1 and #3. Arrow speed on screen: 34 to 44 px/frame (about 2,000 to 2,600 px/s), constant along the flight.

## Projectiles (plan 4.7)

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| `FlightMs` | 192 ms | the arrow of the far shot (#4) is visible 11 frames = 185 ms (f1932 to f1942), the tip ending on the monster's body; the number the next frame | video 1, f1932..f1943 | 1 clean far sample, 1 more (#5) at 8 frames (tip stopped short, see surprises) | confirmed (within one frame) |
| `FlightFullCells` (flight scales with cells up to 8) | `192 x cells / 8` | arrow visible 10 frames at 7.1 cells (lead 10.0 frames), 7 frames at 5.3 cells (lead 7.6), 3 frames at 1.2 cells (lead 1.7; first frames hidden behind the sprites), 11 frames at over 8 cells (cap, lead 11.4) | events #1 to #4 | 4 samples, spread about 1 frame | confirmed |
| launch (player) | `At + hitFrame x 24 x attackMT / 432`, number at launch + flight | launch = attack start + 15 frames (252 ms). With `attackMT` = 780 ms and hit frame 6 the lead gives 260 ms: match. Hit frame that gives 252 ms is **5.8** (lead says roBrowserLegacy has a 5.85 special case: probably this one) | #1, #3 (#2 inferred) | 2 measured, equal | confirmed (hit frame 5.8 to 6) |
| the number's wait | number at launch + flight (so shorter for a near target) | **number at launch + about 185 ms (11 frames) for 1.2, 5.3, 7.1 and over 8 cells alike**, so it does NOT shorten with distance; the arrow vanishes 8 frames (#3), 5 frames (#2), 1 frame (#1, #4) before the number. Total attack start to number = 26 frames (437 ms) in both framed events | #1, #2, #3, #4 (#5: 10 frames) | 5 samples, 10 to 11 frames | **contradicted if the plan means "launch + flight"; confirmed if it means the flat 192 ms of plan 4.7 text** (the reference row says "launch + flight"; fix the row) |
| hurt and white hit arc | with the number | the monster's hurt pose, a white curved slash arc plus sparkles on the target, and the number all start on the same frame (#1 f647, #3 f740, #4 f1943, #5 f2035) | all | 4 samples | new value (the white arc is a hit effect; SPEC-0013 or an overlay detail) |
| launch (monster) | at the hit moment | not visible (no shooting monster in the footage) | | | not visible |
| `ProjectileHeight` | 1 cell (first value) | about 35 to 40 px above the shooter's feet at launch (arrow tail near the hip); at the target the tip is at the body's middle, about 30 to 40 px above the monster's feet; on screen the arrow keeps the same slope as the ground line between feet (-0.27 measured, -0.26 for the ground), so the height is constant along the flight. 40 px at 50 px/cell vertical = 0.8 cell on the screen, which is about 1 cell in world height | #1, #4 | 2 | new value: about 0.8 to 1 cell, keep 1 |
| arrow path | straight line | straight, no arc, no fall; constant speed | #1, #4, #5 | many frames | confirmed |
| follows a moving target? | no (flies to the position at launch) | **No.** In #2 the monster walks toward the archer; the arrow flies to where the monster stood at launch, passes the monster's body at f687 to f689 (monster now left of the arrow tip), then vanishes at that spot at f690; the number and hurt then show on the monster's current place 5 frames later. In #5 the monster walks away and the arrow stops 110 px short of the monster's launch spot at f2032 | #2, #5 | 2 | confirmed |
| arrow picture | not in lead | thin flat line: dark maroon shaft about 2 px thick, a white-grey arrowhead at the front and a white-grey fletching at the back, length about 85 px at the default zoom (about 1.6 cells), pointing along the flight line | `B_arrowpicture_22-39-21_10.74s.jpg` | 1 close-up, same look in all events | new value |
| arrow rotation | not in lead | one fixed angle for the whole flight, equal to the screen direction shooter to target (15 degrees above horizontal in #1; about 25 degrees in #4); the head always forward; no spin, no tumble | #1, #4 | 2 | new value |
| arrow drawing order | | drawn over the monster's sprite while crossing it (#2 f687 to f689), over the archer's sprite at launch | #2, #1 | 2 | new value |
| arrow at launch position | | first visible with its tail already at the archer's body (hip height), head about 70 to 100 px ahead of the body | #1 f636, #2 f683 | 2 | new value |
| hit frame (bow) | 6 | about 5.8 (see launch) | | | confirmed |
| hit frame (whip) | 6 | about **4** (see whip) | video 2 | 2 samples | contradicted |
| attack motion rows | Dancer: 14 -> 2, 11 -> 3 | the bow shows a draw-and-release pose (one motion; I cannot tell Attack3 from Attack2 without the act file); the whip shows one motion with the whip unfurling | | | not decidable from the pictures |
| `ProjectilePool` | 32 | not displayed. Overlap: shots are 783 ms apart and an arrow lives at most 190 ms, so 1 arrow is on screen at a time | | | not visible |
| miss with a bow | | **not in the footage** (all bow shots in video 1 hit; the number shows each time) | | | not visible |
| camera angles | | the camera does not rotate and the zoom does not change in either video | | | not visible |

## Whip (shot 13, video 2)

| Row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| projectile for the whip | none | none: no flying object. The red whip is part of the dancer's sprite motion: coiled at the hand at rest (f1132 to f1139), unfurled to a long line pointing up-right (f1140 to f1146; about 130 px long, going past the adjacent monster, so it does not aim at the target), then coiled again (f1152) | `B_whip_22-41-02_19.01s.jpg` | 3 attacks seen alike | confirmed |
| attack start to number | hit frame 6 (about 7.6 frames at attackMT 380) | whip unfurls at f1140 (19.09 s); the number, the target's hurt and the white arc show at f1145 (19.18 s): **5 frames = 84 ms**. The previous attack: white arc at f1125, 20 frames before f1145. 84 ms = 4 frames of 24 ms x (380 / 432), so the hit frame is about 4 | f1139 to f1146; earlier hit f1125 (same pose rule) | 2 samples | **contradicted** (not 6; 4 or 3.6 to 4.5) |
| motion length | fitted to `attackMT` | the unfurl-to-recoil motion lasts f1140 to f1152 = 12 frames (210 ms); the interval between attacks is 20 frames (336 ms) at ASPD 181 (amotion 380 ms by the formula; the displayed ASPD may include a buff change) | | 2 samples | new value |
| the number | | shown at the target when the hit starts, with the white arc; no wait | f1145 | 2 | new value |
| effect | | white curved slash arc plus a sparkle at the target on the hit frame, the same effect as for the bow hit | | | new value |

## Other rows these videos answer

| Section / row | Lead | Measured | Evidence | Confidence | Verdict |
|---|---|---|---|---|---|
| Timeline: hit moment | `At + attackMT` | for the bow: At + 252 ms (arrow launch) + 185 ms = At + 437 ms with `attackMT` about 780; so the number is at `At + 0.56 x attackMT`, not `At + attackMT` | #1, #3 | 2 samples | **contradicted** for ranged attacks (the plan's own 4.7 formula, launch + 192, gives 260 + 192 = 452 ms, which matches) |
| Timeline: hit moment (whip) | `At + attackMT` | At + 84 ms with `attackMT` about 380 | video 2 | 2 | contradicted |
| `NumberMs` (1,500 ms), alpha, scale | `Damage.js` | the number of #4 (first at f1943 = 32.68 s) is still faintly visible at 33.45 s (0.8 s later) and gone by 34 s: plausible with 1.5 s fade, not measured exactly | video 1 | 1 | not measured |
| number size | world size | the digits scale from large to small while rising, as in the lead (see `B_arrow*` sheets, the "3 7 3" series) | video 1 | qualitative | confirmed qualitatively |

## Rules and surprises (the plan does not know)

1. **The number does not wait for the arrow, it waits a fixed 11 frames (about 185 ms, equal to the 8-cell flight) after launch, whatever the distance.** A near target sees the arrow vanish up to 8 frames before the number. Make `Times` return `arrives = launch + 192 ms` for the number when the attacker is a player (the plan's "number at launch + 192 ms" text); the reference row "launch + flight" is wrong for the number (right for the arrow's disappearance).
2. The arrow flies to the target's position at launch and vanishes there even if the target has moved (#2, #5). The number and hurt are on the target's current place.
3. Attack start to arrow launch is 15 frames at attackMT 780 (hit frame 5.8): the lead's 6 is right for the bow (within 1 frame). For the whip it is about 4. Hit frame needs a per-weapon value (bow 5.8 to 6, whip 4).
4. The white curved slash arc plus sparkles on the target on every hit (bow and whip, normal hits) is a hit effect not in the plan.
5. Arrow length about 85 px (about 1.6 cells), thin; the arrow's tail is already at the archer's body on its first frame, so the draw can start at the shooter's position.
6. In #5 the arrow ends 110 px (about 2 cells) short of the monster's launch-time position at f2032, after only 8 frames; the flight there was not full length. Possible causes: the shown positions at launch differ from what I read, or the arrow's flight uses the target cell the server knew. One sample: do not rely on it.
7. Cell scale: 52 x 50 px per cell at the default zoom (the ground cursor), pitch nearly top-down.
8. The monster's hurt pose, number and arc start on the same frame; for a bow the monster is not hurt when the arrow passes it (#2).

## Not answered by this footage

- Miss with a bow or whip; own miss pictures; camera angle variants; monster archers (shot 12); guns, shuriken, instruments.
- The precise attack-start frame of events #2, #4, #5 (inferred or not framed).
- Whether attack motion number is 2 or 3 for the bow (needs `act` comparison).
- Whip hit frame needs more samples at another ASPD.

## Key frames saved (this folder)

- `B_attackstart_22-39-21_10.40s.jpg`: the dancer f618 to f641, attack start f621, arrow at f636.
- `B_arrow7cells_22-39-21_10.40s.jpg`: event 1, f618 to f653 (arrow, number and hit arc at f647).
- `B_arrow5cells_movingtarget_22-39-21_11.25s.jpg`: event 2, the arrow passing a walking monster.
- `B_arrow1cell_22-39-21_12.17s.jpg`: event 3, adjacent target, arrow gone 8 frames before the number.
- `B_arrowfar_launch_22-39-21_32.30s.jpg`, `B_arrowfar_hit_22-39-21_32.60s.jpg`: event 4, the far shot.
- `B_arrow9cells_22-39-21_34.02s.jpg`: event 5.
- `B_arrowpicture_22-39-21_10.74s.jpg`: the arrow close-up, 8 times.
- `B_whip_22-41-02_19.01s.jpg`, `B_whip_wide_22-41-02_18.85s.jpg`: the whip attack f1125 to f1157.
- `B_aspd_bow_22-39-21_11.00s.jpg`, `B_aspd_whip_22-41-02_19.00s.jpg`: the stats windows (ASPD 161 and 181).
