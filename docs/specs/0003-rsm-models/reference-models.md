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
- After the owner trimmed the source folder (2026-11-17), `model/` holds **140 `.rsm` files, all version 1.4, and no `.rsm2`**. All 140 load through GRF and every texture they reference exists under `texture/`.
- Before the trim the folder held the full data: 7,044 `.rsm` (6,554 of 1.4, 490 of 1.5) and 1,893 `.rsm2` (258 of 2.2, 1,635 of 2.3). Versions 1.5, 2.2 and 2.3 therefore exist in the full game data but are not in the working data set; §5 still applies to it.

## 7. Expected values (TASKS-0003 T1.2)

Counted by a raw read of the files that does not use GRF (a scratch script of T1.2), so the reader tests check the GRF adapter against an independent source. Per node values (names, parents, vertices, texture coordinates, faces, keys) are in `Uninarok-client/Assets/Tests/EditMode/RoImport/Rsm/Fixtures~/reference-nodes.csv`; this table sums them per model. Shade type: 0 none, 1 flat, 2 smooth.

| Model | Version | Shade | Nodes | Vertices | Faces | Rotation keys |
|---|---|---|---|---|---|---|
| 나무잡초꽃/버섯02 | 1.4 | 2 | 1 | 21 | 26 | 0 |
| 나무잡초꽃/버섯01 | 1.4 | 2 | 1 | 21 | 26 | 0 |
| 나무잡초꽃/넝쿨01 | 1.4 | 1 | 1 | 4 | 2 | 0 |
| 나무잡초꽃/덤불02 | 1.4 | 2 | 1 | 72 | 64 | 0 |
| 나무잡초꽃/덤불01 | 1.4 | 2 | 1 | 54 | 48 | 0 |
| 나무잡초꽃/야자수01 | 1.4 | 2 | 1 | 144 | 114 | 0 |
| 프론테라/성_기둥 | 1.4 | 2 | 1 | 128 | 182 | 0 |
| 프론테라/휘장 | 1.4 | 2 | 1 | 4 | 2 | 0 |
| 외부소품/이정표01 | 1.4 | 2 | 1 | 20 | 16 | 0 |
| 나무잡초꽃/나무01 | 1.4 | 2 | 1 | 70 | 64 | 0 |
| 나무잡초꽃/선인장01 | 1.4 | 2 | 1 | 161 | 232 | 0 |
| 프론테라/성_다리 | 1.4 | 2 | 1 | 104 | 114 | 0 |
| 나무잡초꽃/나무기둥01 | 1.4 | 2 | 1 | 84 | 127 | 0 |
| 프론테라/프론테라성기둥 | 1.4 | 2 | 1 | 80 | 102 | 0 |
| 나무잡초꽃/나뭇잎01 | 1.4 | 2 | 1 | 9 | 10 | 0 |
| 외부소품/벤치01 | 1.4 | 2 | 1 | 20 | 14 | 0 |
| 외부소품/동굴입구 | 1.4 | 1 | 1 | 130 | 170 | 0 |
| 프론테라/성_앞문 | 1.4 | 2 | 1 | 45 | 64 | 0 |
| 프론테라/입구초소 | 1.4 | 2 | 1 | 130 | 147 | 0 |
| 라헬/분수 | 1.4 | 0 | 23 | 799 | 1071 | 1078 |
| 외부소품/게시판 | 1.4 | 2 | 2 | 53 | 80 | 5 |
| 외부소품/프론아레나 | 1.4 | 2 | 2 | 440 | 552 | 5 |
| izlude/iz_bridge01 | 1.4 | 1 | 9 | 158 | 144 | 0 |
| dewata/다약화로01 | 1.4 | 2 | 2 | 76 | 105 | 0 |
| izlude/iz_fruitshop | 1.4 | 2 | 2 | 108 | 158 | 0 |
| 프론테라/시계탑 | 1.4 | 2 | 5 | 131 | 142 | 108 |
| izlude/iz_smelter | 1.4 | 2 | 16 | 339 | 395 | 246 |
| izlude/iz_enchant | 1.4 | 0 | 4 | 810 | 1536 | 101 |
| 아인브로크/비공정 | 1.4 | 2 | 7 | 781 | 932 | 126 |
| 외부소품/퀴즈경기장 | 1.4 | 2 | 6 | 387 | 484 | 29 |
| 프론테라/교역소 | 1.4 | 2 | 2 | 201 | 219 | 5 |
| 프론테라/도구점 | 1.4 | 2 | 2 | 234 | 293 | 5 |
| 외부소품/이즈루드초소 | 1.4 | 0 | 1 | 120 | 114 | 0 |
| 프론테라/프론테라성입구 | 1.4 | 2 | 1 | 452 | 548 | 0 |

## 8. Batch results (TASKS-0003 T7.1)

`RsmReferenceConversionTests` converts every reference model through `RoImport.Model` and the whole `model/` folder in one batch (throwaway output root, temporary ledger), 2026-11-18:

```
[RoImport] Created 140, Updated 0, Skipped 0, Failed 0, Warnings 1
  OutputCollision texture/izlude/iz_rookie_06.tga (referenced by model/izlude/iz_enchant.rsm):
  'texture/izlude/iz_rookie_06.bmp' has the same output and is preferred; it is used instead
```

140 models, all version 1.4, no failure, no `LogError`. The one warning is a data note: `iz_enchant` names a `.tga` that has a `.bmp` of the same name beside it, and the `.bmp` is used (PLAN-0002 D19). The first batch also showed two material keys (`.tga` and `.bmp`) owning one material file; the converter now names the preferred `.bmp` in the material key (test `TextureNamedAsTgaWithABmpBeside_SharesTheBmpsMaterial_WithoutACollision`).

### Whole folder into the project (TASKS-0003 T7.2, 2026-11-18)

Same path as the window (`RoImportJobs.Expand("model/", Model)` → `RoImport.Run`), default roots and ledger:

```
run 1: [RoImport] Created 140, Updated 0, Skipped 0, Failed 0, Warnings 1   (the iz_rookie_06.tga note above)
run 2: [RoImport] Created 0, Updated 1, Skipped 139, Failed 0, Warnings 0   (iz_enchant stale: .tga dependency with empty hash -> D12)
run 3: [RoImport] Created 0, Updated 1, Skipped 139, Failed 0, Warnings 0   (after the D12 fix: iz_enchant rebuilt once with the .bmp dependency)
run 4: [RoImport] Created 0, Updated 0, Skipped 140, Failed 0, Warnings 0
```

Between run 3 and run 4 none of the 2,348 files under `Assets/Art` and `Assets/Editor/RoImport/Ledger` changed (SHA-256 of every file compared). The outputs are not committed; they are untracked like the folder itself (owner to decide).
