# Reference models and maps for SPEC-0003 / SPEC-0004 / SPEC-0006

| | |
|---|---|
| **Status** | Draft |
| **Created** | 2026-11-16 |
| **Used by** | SPEC-0003 FR-2, FR-3, FR-5, FR-9; SPEC-0004 FR-4, FR-6; SPEC-0006 FR-3, FR-4 |

Normative list of the models and maps that acceptance checks are run against. Update it in the same commit as any change to the checks.

**How this was built.** 148 `.rsm` files from the project's source data folder were scanned with a quick reader of the RSM 1.4 layout. It consumed every file to within the same 8 trailing bytes, so counts are reliable enough to choose samples, but the plan must re-verify them with the real parser before they are treated as facts. Map sizes and model references come from the GND and RSW files of the three reference maps.

**What the data contains.** Every one of the 148 models is RSM version 1.4. Every model carries a shading type (all three values occur: 0 none, 1 flat, 2 smooth, which is the most common) and per-face smoothing-group numbers. No model has mirrored (negative) scale.

## 1. Models reported wrong (must pass first)

All four are a single node with a non-zero rotation angle and a non-zero position. The wrong result is seen when the model is placed through its map.

| Model (path under `model/`) | What it is | Reported problem | Instances in reference maps |
|---|---|---|---|
| 나무잡초꽃/버섯02 | mushroom | position and rotation wrong when placed | prt_fild08 (3) |
| 나무잡초꽃/버섯01 | mushroom | same as 버섯02 | prt_fild08 (5) |
| 나무잡초꽃/넝쿨01 | vine | rotation may be wrong | prt_fild08 (1), izlude (2) |
| 나무잡초꽃/덤불02 | bush | position and rotation off | prt_fild08 (32), izlude (13) |

## 2. Control group (same arrangement, not reported wrong)

Same shape of data as section 1 (single node, rotation angle and position non-zero). Comparing these with section 1 isolates what differs.

나무잡초꽃/덤불01, 나무잡초꽃/야자수01, 프론테라/성_기둥, 프론테라/휘장, 외부소품/이정표01.

## 3. Coverage samples

| Arrangement | Samples |
|---|---|
| Single node, position set, no rotation | 나무잡초꽃/나무01, 나무잡초꽃/선인장01, 프론테라/성_다리 |
| Single node, no position, no rotation | 나무잡초꽃/나무기둥01, 프론테라/프론테라성기둥 |
| Node offset non-zero | 나무잡초꽃/나뭇잎01, 외부소품/벤치01, 외부소품/동굴입구, 프론테라/성_앞문, 프론테라/입구초소, 라헬/분수, 외부소품/게시판, 외부소품/프론아레나 |
| Parent and child nodes, no animation | izlude/iz_bridge01 (9 nodes), dewata/다약화로01 (2 nodes), izlude/iz_fruitshop (2 nodes) |
| Parent and child nodes, animated | 프론테라/시계탑 (5 nodes, 108 keyframes, very long animation length), izlude/iz_smelter (16 nodes, 246 keyframes), izlude/iz_enchant (4 nodes, 101 keyframes, shading none), 아인브로크/비공정 (7 nodes, 126 keyframes), 외부소품/퀴즈경기장 (6 nodes, 29 keyframes), 라헬/분수 (23 nodes, 1078 keyframes; not in a reference map, stress case) |
| Two nodes with only a few keyframes | 프론테라/교역소, 프론테라/도구점, 외부소품/게시판, 외부소품/프론아레나 (5 keyframes each) |
| Shading type none | 외부소품/이즈루드초소, izlude/iz_enchant |
| Shading type flat | 나무잡초꽃/넝쿨01, 외부소품/동굴입구, izlude/iz_bridge01 |
| Many two-sided faces | 프론테라/입구초소 (147), 나무잡초꽃/야자수01 (114), 나무잡초꽃/나무01 (48) |
| Largest geometry | 프론테라/프론테라성입구, 라헬/분수 |

## 4. Reference maps

| Map | Ground size (cubes) | Model references | Notable contents | Use |
|---|---|---|---|---|
| prontera | 156 x 196 (non-square) | 1367 (67 distinct models) | 시계탑 (2, animated), 교역소, 도구점, 분수대, 게시판 (3) | non-square ground, large city, animated hierarchy |
| izlude | 134 x 150 (non-square) | 579 (93 distinct models) | 덤불02 (13), 넝쿨01 (2), iz_smelter, iz_enchant, iz_bridge01 (6), 비공정, 퀴즈경기장, 프론아레나 | non-square ground, many hierarchies and animated models |
| prt_fild08 | 200 x 200 | 616 (26 distinct models) | 버섯01 (5), 버섯02 (3), 넝쿨01 (1), 덤불02 (32) | first map to check placement; contains every reported-wrong model |

prt_fild08 is the confirmed reference map (the earlier mention of prt_fild09 is superseded).

## 5. Gaps (not covered by the current data)

- No model with mirrored (negative) scale in the 148 scanned models (the full folder is scanned by TASKS-0003 T0.3).
- No RSM version other than 1.4 in the 148 scanned models. The full folder has other versions (§6).
- No very large map beyond 200 x 200 cubes.

## 6. Full data check (TASKS-0003 T0.1, 2026-11-17)

Source folder: `Uninarok-client/Assets/Editor/GRF Resources~/data/`.

- Every model of §1 to §3 exists under `model/` (34 of 34). The three reference maps exist with `.rsw`, `.gnd` and `.gat`.
- `model/` holds 7,044 `.rsm` and 1,893 `.rsm2` files (none outside `model/`). Versions from the file header (`GRSM` + major, minor):

| Extension | Version | Files |
|---|---|---|
| `.rsm` | 1.4 | 6,554 |
| `.rsm` | 1.5 | 490 |
| `.rsm2` | 2.2 | 258 |
| `.rsm2` | 2.3 | 1,635 |
