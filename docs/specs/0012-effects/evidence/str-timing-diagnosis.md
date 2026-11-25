# STR timing diagnosis: hasty_fire_in_the_hole_7x7.str (owner bug, T8 preview)

Owner report: (A) our playback is almost 2x faster than GRF Editor, (B) we show 69 frames where GRF shows 70.

## Measurements

Source: the owner's recording (10.118 s, 1904x928), played in the built-in browser; GRF Editor's "Str preview" pane, looping. Brightness of the preview pane sampled per video frame to find the cycle starts; the "Frame: n / N" counter read at two moments.

| Quantity | GRF Editor (video) | Ours (imported EffectAsset, plan 4.2) |
|---|---|---|
| Header fps / frame count (asset) | n/a | 60 / 70 (17 layers) |
| Counter text | "Frame: n / 70", 0-based, last shown 69 (read at +1.50 s of the cycle: 69) | preview label "frame n/69" (denominator = last index of a loop, EffectPreviewMath.LastFrame) |
| Cycle starts in video | 5.184 s, 6.684 s, 8.185 s | n/a |
| One cycle | 1.500 s (two consecutive cycles identical to 1 ms) | 70 / 60 = 1.1667 s (Length = EffectSampler.Length) |
| Rate | 70 frames / 1.5 s = 46.7 frames per second | 60 frames per second (frame = Age x fps, StrPart.Tick) |
| Counter at +0.75 s into a cycle | 36 | 45 |
| First visible peak (brightness) | +0.12 s | +0.09 s (same keyframe, 5.6 frames at 60 fps) |

Ratio GRF cycle / ours = 1.29, not 2.

## Findings

- (A) The "fps / 2" hypothesis is rejected: it would give 70 / 30 = 2.33 s per cycle; the video shows 1.5 s. GRF's rate (46.7 per second, about 21.4 ms per frame) is not a simple function of the header fps 60 (no factor of 2 or 1). It looks like GRF Editor's own preview timer (a nominal 20 ms tick plus overhead), but the video cannot prove that; one file only was recorded. Our 1.167 s is exactly the plan rule (FrameCount / Fps, D29) and we are 1.29x faster than GRF, which may feel like "almost 2x".
- (B) The frame index range is the same: both show 0..69 in a loop. GRF prints the denominator as the header frame count (70), we print the last index (69, loop) as the denominator. It is a label difference; no frame is dropped (our asset has FrameCount 70, Length 70/60 s).
- The real client's rate is unknown here (no client evidence read); GRF Editor's timer is not evidence of the client's rate.

## Decision needed (no code changed)

Changing the playback rate would change plan 4.2 / D29 (length = FrameCount / Fps) and the meaning of "frame rate" in FR-3 / FR-4, so it is left to the owner. Options:

1. Keep header fps (current). Matches the file's own data; 1.29x faster than GRF Editor's preview.
2. Add an EffectSettings STR speed factor (for example 46.7 / 60 = 0.78) to match GRF Editor's look. A constant taken from a preview timer, not from the file; not recommended unless the client rate is shown to match.
3. Label only: show "n / FrameCount" like GRF (a loop's denominator 70 instead of 69), no timing change.
