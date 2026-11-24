# T9.5 Cost of the combat update (FR-12, PLAN-0011 6.1)

Result: **GC: 0 B in every measured call over 600 frames (pass). CPU: the overlay exceeds the 0.5 ms guide in the Editor (mean 1.02 ms, p95 1.37 ms, max 4.07 ms); the task is therefore NOT ticked.** FR-12 itself treats the frame time as "measured and reported, not a pass/fail target"; the task's Done-when asks it to be recorded against the 0.5 ms guide, and it is above it.

Guide source: the 0.5 ms figure is the FR-12 text of `spec.md` ("guide: 0.5 ms on the owner's PC"); the same budget as SPEC-0003's FR-9 (`BACKLOG.md`, 0.5 ms animation update).

## Method

- Machine: the owner's PC (Windows 11), **Unity Editor in Play Mode, Mono (no IL2CPP), the test runner's PlayMode**. The editor is not a player: Mono and the editor's checks overstate CPU time, so the player would be faster; nothing here was measured in a build.
- Scratch code (kept as text in `measure-source.txt`, never committed to Assets) was added temporarily to the existing tests' classes and removed with `git checkout`: it reuses the setups of `CombatOverlay_ZeroAllocation_100Numbers300Units` (PlayMode, `WorldPresenterPlayTests`) and `Combat_ZeroAllocation_100Fighting` (EditMode, `CombatAllocationTests`).
- Per frame, each call is bracketed by `GC.GetAllocatedBytesForCurrentThread()` (exact bytes allocated by that call on the main thread) and `Stopwatch.GetTimestamp()`.
- Real frames: the PlayMode loop `yield return null` between measured iterations, 240 warm-up frames (pools, shaders, JIT) before the 600 measured ones; the clock advances 16 ms per frame. Project settings were not changed (nothing to restore).
- Load, verified from the objects each frame (columns `numbers`, `bars`, `arrows`, `marks`, `shown` in `combat-frames.csv`): 300 units shown (`WorldPresenter.ShownCount` 300 in all 600 frames), 100 distinct monsters hitting the own character in turn plus the own bow shots, a blow fed every frame (two every 10th), **numbers on screen 102 to 105 (mean 103.4)**, bars 201 in every frame, arrows 1 to 2, marks 1. So 300 units / 100 fighting / 100 numbers is reached.
- The overlay is measured as the real `CombatOverlay.LateUpdate` (a delegate to it, so `Draw(camera)` plus the four `Graphics.RenderMesh` calls); the test camera was tagged `MainCamera` so its `UnitSystemCamera()` finds it. (A first run of `Draw` alone gave 0.94 ms mean; the LateUpdate gives 1.02 ms.)
- The model side (packet delivery, `OwnCombat.Tick`, `CombatTimeline.Advance`, the DAMAGE packets) comes from the EditMode run: 1,200 warm-up frames then 600 measured, the 100 fighting monsters, 215 to 222 numbers alive in the timeline (more than 100 as the model test feeds a hit and a counter-hit every frame).

## Numbers (600 frames each; ms, mean / p95 / max; bytes min = max = 0 unless said)

PlayMode, 300 units, 100 fighting, about 103 numbers (`combat-frames.csv`):

| Part | GC bytes | mean | p95 | max |
|---|---|---|---|---|
| `CombatTimeline.Add` (the blows fed, 1 or 2 per frame) | 0 | 0.0056 | 0.0095 | 0.0367 |
| `WorldModel.Advance` | 0 | 0.0061 | 0.0121 | 0.0237 |
| `CombatTimeline.Advance` | 0 | 0.0052 | 0.0097 | 0.0204 |
| `WorldPresenter.Sync` (motion handling; SPEC-0009's unit update is not counted in the combat budget) | 0 | 0.0088 | 0.0139 | 0.0227 |
| `CombatOverlay.LateUpdate` (numbers, bars, marks, arrows meshes + RenderMesh) | 0 | **1.015** | **1.369** | 4.070 |
| Sum of the above (all five) | 0 | 1.041 | 1.409 | 4.120 |

EditMode, model only (`combat-model-frames.csv`), 100 fighting, 2 DAMAGE packets per frame:

| Part | GC bytes | mean | p95 | max |
|---|---|---|---|---|
| Two DAMAGE packets delivered (parse + model + timeline) | 0 | 0.0007 | 0.0015 | 0.0146 |
| `WorldModel.Advance` | 0 | 0.0001 | 0.0003 | 0.0064 |
| `OwnCombat.Tick` | 0 | 0.0001 | 0.0002 | 0.0146 |
| `CombatTimeline.Advance` | 0 | 0.0018 | 0.0029 | 0.0222 |

Combat update = Add + Advance + Timeline + Overlay (+ OwnCombat.Tick, 0.0001): about **1.03 ms mean, 1.39 ms p95, 4.1 ms max** in the Editor. Practically all of it (over 98 %) is the overlay's mesh writing; the model, the timeline, the own combat and the presenter's motion handling together cost under 0.03 ms.

## Verdict

- **Zero allocation (FR-12): met.** Every measured call allocated 0 B in all 600 frames, in both runs (also the packets). `GC.GetAllocatedBytesForCurrentThread` is exact per call.
- **Frame time: above the 0.5 ms guide in the Editor** (overlay mean 1.02 ms, p95 1.37 ms, a spike to 4.07 ms). Mono and the Editor overstate it by an unknown factor; a player build was not measured. Not ticked; recorded in `BACKLOG.md`.

## Caveats

- `frame_gc_bytes_profiler` (the profiler counter "GC Allocated In Frame", read after each frame) is about 24.9 KB per frame in the measured run and 24.2 KB in an idle run with the overlay disabled and nothing combat (`baseline-empty-frames.csv`): that is the Editor and the test runner, not the combat update; it cannot show the 0 B and was not used for it. The per-call counter is the evidence. A Profiler `.raw` capture of a player build would be the clean "0 B per frame" capture.
- Timings of sub-0.01 ms calls are near the `Stopwatch` resolution; the sum is what matters.
- The first row of each PlayMode run after a domain reload can show a spike (shader and pool warm-up beyond 240 frames); the max of 4.07 ms is such a frame.
- Suggestion (not done, out of this task): a permanent `ProfilerMarker` around `CombatOverlay.LateUpdate`, `CombatTimeline.Advance` and `OwnCombat.Tick` would let a player's Profiler capture show the parts without scratch code.
