# T0.3 evidence: the target mark (FR-16, shot 17)

Measured on `Ragnarok 2026-11-22 22-32-58.mp4` (acolyte, bare hands, poring), `22-39-21.mp4` and `22-41-02.mp4` (dancer, Roween), 1920 x 1080, about 59.5 fps (frame = `round(t * fps)`). Tool: OpenCV template match on the mark (a venv, not kept); `mark_track_22-32-58.json` holds `(frame, time, score, x, y)` for every second frame (score above 0.75 = mark seen).

## What the mark is
A small **white faceted diamond pointing down** (a cut gem), about 22 x 15 px at 1080p, with a dark outline: `mark_sprite_26x18_x8.png` (the 26 x 18 cut, 8 times larger). It is a flat picture, always upright, not turned with the camera, over the attacked monster. It is not one of the green corner brackets (that is the ground-cell cursor).

## When it shows and goes (22-32-58)
| Run (s) | What happens | Reading |
|---|---|---|
| 17.55 to 18.24 | The pointer turns into the attack cursor over the poring at 17.55; the mark is there at 17.63 and stays through the blow whose number `26` shows at 17.85; gone at 18.24 | single attack: shown from the click, goes after the blow |
| 20.29 to 21.13 | Click at 20.2 on a poring 4 cells away; the mark appears at 20.29 and **moves with the monster while the acolyte walks to it**; the number `26` shows at 21.25; the mark is already gone at 21.25 | shown during the approach too; gone at about the hit moment (0.1 s before) |
| 35.40 to 35.91 | Click on a standing poring at 35.3; the number `26` shows at 35.95; the mark goes at 35.91 | gone at about the hit moment (0.04 s before) |
| 3.90 to 7.00 | Several blows and misses with the mark staying through them (the owner held the attack) | continuous attack: stays until the attack stops |
| 10.47 to 11.95, 37.05 to 39.5, 40.55 to 43.2, 44.0 to 46.4 | The mark follows monsters that walk | follows the monster |

Reading for a **single** attack: the mark goes at the hit moment of its own blow (the moment its number shows), between 0.12 s before and 0.4 s after (three samples: -0.04 s, -0.12 s, and about +0.4 s). First value `TargetMarkSingleMs` = 0 after the hit moment; to be refined with more single attacks.

## Where it floats
- It follows the monster's position on screen frame by frame, and does not keep its own float: over a standing poring (4.7 to 5.0 s) its y is constant (514 px), and the small changes elsewhere follow the monster's own movement. **No independent bobbing was seen** (the picture is still).
- Height: **about 66 px above the top of the standing sprite** at the default view: poring (top 580, mark centre 514) and Roween (about 65 to 70). The mark is not tied to the *current* frame's top: over a Roween that rears up the mark stays at the standing height (`mark_roween_22-39-21_and_22-41-02.jpg`, left picture). The sprites are small, so this reads as "a fixed height above the feet, depending on the monster": about 93 px above the poring's feet and about 125 px above Roween's.
- Size does not change with the monster.

## Not settled
- Whether the height depends on the monster's own sprite height or on a data field of the monster; two monsters only.
- Whether the picture and its size change with camera zoom (the footage is at one zoom).
- The single-attack time needs more samples; the video has three.
