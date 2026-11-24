# T11.2 evidence: the target mark in the overlay (FR-16, FR-12)

## The picture
Found in the extracted client data: `sprite/cursors` frame 6 (14 x 10 px), the diamond of `evidence/T0.3/mark_sprite_26x18_x8.png`. A template match of that frame, scaled to 22 x 15, on the original client's frame at 17.63 s of `Ragnarok 2026-11-22 22-32-58.mp4` scores 0.975. It is shown at 22 x 15 screen px (`TargetMarkSizePx`). See `combat-reference.md`.

## Live check (2026-11-23, local Hercules, prt_fild08, Play Mode from Bootstrap, game view 1920 x 1080)
A scratch script (not kept) spawned porings with `@monster 1002`, called `OwnCombat.Attack(id, continuous)` and took a `ScreenCapture` every 0.2 to 0.3 s, logging `OwnCombat.State` and `MarkedTarget` (`continuous_log.txt`, `single_log.txt`).

Continuous attack (`continuous_*.png`, `Attack(id, true)`): the mark is over the poring while it approaches (t 0 to 0.9 s) and while attacking (1.2 to 2.8 s), and is gone at the first frame after the kill (3.09 s, state Idle).
- `continuous_1_approach.png`, `continuous_2_attacking_mark.png`: mark present (in the second one over the poring beside the character, with a damage number).
- `continuous_3_after_kill_no_mark.png`: no mark.

Single attack (`single_*.png`, `Attack(id, false)`): the mark shows while approaching (0 to 0.7 s), stays after the blow was sent (state Idle at 0.87 s) until the blow's hit moment, and is gone from 1.48 s (`TargetMarkSingleMs` = 0).
- `single_1_approach.png`, `single_2_after_blow_mark.png`, `single_3_last_mark_frame.png`: mark present.
- `single_4_mark_gone.png`: mark gone.

Beside the original client's frames: `../T0.3/mark_single_22-32-58_t16.8-18.9.jpg` (single attack), `../T0.3/mark_poring_follow_22-32-58_t4.0-5.4.jpg` (continuous attack), `../T0.3/mark_sprite_26x18_x8.png`.

Not verified: the exact moment a single attack's mark goes against the original beyond what T0.3 measured; the live captures show it goes within about 0.6 s of the blow being sent, the hit moment being the rule in `OwnCombat`. The height over a unit that is not a poring was not compared.
