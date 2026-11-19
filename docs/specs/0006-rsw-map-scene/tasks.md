# TASKS-0006: RSW Map Scene

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved |

**How to use this file**
- Start only when TASKS-0002 phases 0 to 6, TASKS-0003 phases 0 to 6, TASKS-0004 phases 0 to 5 and TASKS-0005 phases 0 to 4 are done (foundation, model prefabs, ground and `RoMapSpace`, map data and `MapRoot`). Tasks here use those contracts as written in their plans.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0006): T3.2 implement RoRswPlacement` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent.
- Paths are relative to `Uninarok-client/Assets/Editor/RoImport/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a spike (phase 0) shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Spike scripts are scratch code: delete them when the task is done. Production code never parses RSW bytes (PLAN-0002 §1).
- Steps that need the running editor (scratch scenes, screenshots, baking, Profiler) are done with the Unity MCP when it is connected, otherwise by the owner.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Verify before building

- [x] **T0.1** Check the data and the old code: list the three reference maps (`map/<name>.rsw`, `.gnd`, `.gat`) and count every `.rsw` under `map/`; list scenes and prefabs in the project that were made by `RswImporter.cs` and what refers to them. Record the counts and the list in the PR. (FR-1, FR-12)
  *Done when:* every reference file exists or its absence is agreed with the owner, and the list of old scenes and users is in the PR.
  *Result (2026-11-18):* the map files are at the data root `Assets/Editor/GRF Resources~/data/` (the default of `RoSourceLocation`, no preference set), not in `map/` (as PLAN-0004 D11 and PLAN-0005 D9); the converter enumerates the data root for `.rsw`, not `map/`. All nine reference files present: prontera `.rsw` 417,262 B, `.gnd` 3,334,150 B, `.gat` 2,446,094 B; izlude 228,430 / 2,327,838 / 1,608,014 B; prt_fild08 219,402 / 5,272,734 / 3,200,014 B. The data set has 3 `.rsw` in total. Old code: `Assets/Editor/RswImporter.cs` (compiled out by `RO_LEGACY_IMPORTERS`) has no users; no scene, prefab or asset in `Assets/` was made by it or refers to it (the only scene is `Assets/Scenes/Bootstrap.unity`, no `.rsw` outside the hidden data folder); `UtilExtensions.cs` was already deleted by TASKS-0004 T8.4, so T8.5 deletes only `RswImporter.cs` and its `.meta`.
- [x] **T0.2** Map GRF to the file fields (plan §4.1). In a scratch editor script load each reference `.rsw` through the GRF `Rsw` class and print the version, the file references, the water, the lighting values and the first and last object of each type, taking GRF's values as they are (no comparison with the file bytes, plan D11). Check the counts against `reference-models.md` §4. Write into `plan.md` §4.1 a table: one row per `RoRsw` field with the GRF member that supplies it, and one note for every derived member that must not be used. Record the versions of the three maps and where the water plane is read from. (FR-1, FR-7)
  *Done when:* every `RoRsw` field has a GRF member (or is declared absent for the version), the versions are recorded, and the table is in `plan.md`.
  *Result (2026-11-18):* field table, members not to use and observed counts in plan §4.1 (scratch code run in the editor through the Unity MCP, no file kept). All three maps are RSW 2.1, water read from the RSW. Findings for later tasks: `NodeName` is non-empty on every instance (T0.3 decides), `BlockType` is uninitialized memory on 2.1 so `Collision` is not kept (plan §4.3 updated), `AnimationSpeed` is 1 or 0.01 (a multiplier, not a percentage; T0.3 confirms on the clock towers), and 395 instances have mirrored scale (the data does have them, unlike the models of `reference-models.md` §5). GRF's totals differ from `reference-models.md` §4 (prt_fild08 570/23 vs 616/26, prontera 1303/62 vs 1367/67, izlude 552/90 vs 579/93) while every notable content matches; which numbers T1.2 pins is for the owner.
- [x] **T0.3** Verify the rules table (plan §4.3, §4.4). In a scratch editor script place the models of prt_fild08 using the GRF values of T0.2 and the candidate rules (centre offset from `RoMapSpace`, position scale, rotation order and signs, scale Y sign), on the throwaway ground of TASKS-0004, and compare with the reference viewer from top, front and oblique. Do the four models earlier reported as misplaced first, then the control group of `reference-models.md`, then prontera's two clock towers and izlude. For any model that differs change the rule (not the model) and repeat; check the earlier models after every change. Also record: non-empty `NodeName` values (count and whether the node is the main node), animation types and speeds seen, collision values seen, a check of the ambient and sun colours against the reference viewer, and the sun direction against the shadow of a tall model. Record the final rules and "result by model" in `plan.md` §4.3 and §4.4, and decide with the owner on `NodeName`. (FR-3, FR-8)
  *Done when:* all four reported models, the control group and both clock towers match in the three views, the rules table and lighting rule are in `plan.md`, the owner has seen the comparison images, and the scratch script is deleted.
  *Result (2026-11-18):* the candidate rules hold except the scale Y sign, which is +1 (plan §4.3, D13). The owner compared shots of prt_fild08 (whole map; mushrooms, vine, bushes, palms), izlude (whole map; the mirrored axe and hammer on their bench) and prontera (whole map, both clock towers, lighting) with the reference viewer: all match. Lighting rule in plan §4.4. `NodeName` is ignored (owner), the warning only for a child node. Animation types and speeds recorded in §4.3; the RSW `AnimSpeed` (0.01 on the clock towers) is applied as `Animator.speed` (owner, plan D14). Spike code ran as editor snippets in unsaved scenes; nothing kept on disk.
- [x] **T0.4** Record the ground-contact samples (plan §4.3). With the owner, pick at least 20 models across the three reference maps (street props, buildings, fountains and bridges, trees on slopes, the clock towers), measure for each in the reference viewer the vertical distance between the model's lowest point and the ground under it, and write `Assets/Tests/EditMode/RoImport/Rsw/ground-contact.json` (map, instance index, model file, expected distance, tolerance). (FR-4)
  *Done when:* the file has at least 20 samples with distances the owner agrees with, and it is committed.
  *Result (2026-11-18):* 29 samples (9 prt_fild08, 10 prontera, 10 izlude): street props, buildings, the fountain, both clock towers, the iz_bridge01 bridge, the airship, bushes and vines on slopes. Distances measured in a scratch scene with the T0.3 rules (lowest vertex to the ground under it, Unity units), tolerance 0.05; the owner checked them against the reference viewer and agreed.

## Phase 1: Data and reader

- [x] **T1.1** Add `Core/Rsw/` types exactly as in plan §4.1 (`RoRsw`, `RoRswWater`, `RoRswLight`, `RoRswModel`, `RoRswPointLight`, `RoRswSound`, `RoRswEffect`) and a test-only builder `RswTestMaps` in `Assets/Tests/EditMode/RoImport/Rsw/` (a map with one model at the centre, one at each corner, rotated and mirrored models, a light, a sound, an effect, a missing-model reference, a non-square map). Add the issue codes `UnsupportedRswVersion`, `RswFileMismatch`, `RswNodeNameIgnored`, `RswUnknownAnimType`, `ModelMissing` to `RoIssueCode`. (FR-1, FR-3)
  *Done when:* the project compiles and a test builds each sample map and checks its counts.
- [x] **T1.2** Add `RoRswReaderTests` (`NeedsData`, failing, ignored): the three reference maps read through GRF with the object counts of `reference-models.md` §4; the versions are as recorded in T0.2; a truncated copy and an empty array return false with a reason and never throw. (FR-1)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [x] **T1.3** Implement `Core/Formats/RoRswReader` using the GRF members recorded by T0.2. No parsing code of our own. (FR-1)
  *Done when:* `[Ignore]` removed and `RoRswReaderTests` pass; a text search shows no `BinaryReader` or `BitConverter` use added.

## Phase 2: Map-wide values and settings

- [x] **T2.1** Add `RoMapSettingsTests` (failing, ignored): settings equal the RSW (water level converted to Unity units, lighting values, size); an absent RSW converts without settings. (FR-7)
  *Done when:* the tests compile and are ignored with `pending T2.2`.
- [x] **T2.2** Extend `RoMapDataConverter` (PLAN-0005) to read the RSW, fill `MapData.settings`, set `hasSettings` and record the RSW hash as a dependency. (FR-7)
  *Done when:* `[Ignore]` removed, the tests of T2.1 pass, and the PLAN-0005 tests still pass.

## Phase 3: Placement and lighting

- [x] **T3.1** Add `RoRswPlacementTests` and `RoRswLightingTests` (failing, ignored): a model at the map centre, at a corner and at a non-square map's corner; a rotation about each axis (order and signs pinned); mirrored scale on one and three axes (the instance is not inside-out); positions relative to the centre equal `RoMapSpace.FromRswRo`; ambient and sun colours, shadow strength and sun rotation for a table of longitude and latitude values. (FR-3, FR-8)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [x] **T3.2** Implement `Core/Rsw/RoRswRules` (values of T0.3), `RoRswPlacement` (one matrix chain in Ragnarok space, one conversion) and `RoRswLighting` (plan §4.3, §4.4). (FR-3, FR-8)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.

## Phase 4: Runtime markers

- [x] **T4.1** Add the marker components to `Uninarok.Map` (one file each, plan D15): `MapLightMarker`, `MapSoundMarker`, `MapEffectMarker`, `MapMissingModelMarker` (data only, public fields, no logic, no editor or importer references) and extend `MapDataAssemblyTests` to cover them. (FR-9, FR-10)
  *Done when:* the project compiles, the assembly test still passes, and a test creates each marker and checks its fields.

## Phase 5: The scene

- [x] **T5.1** Add `RoMapSceneTests`, `RoMapMissingModelTests`, `RoMapIdempotencyTests` and `RoMapAdditiveTests` (failing, ignored): a synthetic map converts to a scene with `Map_<name>` (with `MapRoot` and the `MapData` reference), `Ground`, the four groups with their counts, and `Sun`; each model object is a prefab instance of the model prefab (changing the prefab's material changes every instance); names and order are the file order; lights, sounds and effects are markers with their parameters; static flags are set on ground chunks and instances and not on animated instances, markers and groups; a missing model gives one warning naming model and map and a placeholder with the instance's name and transform; the second conversion is `Skipped`; a forced second conversion gives an identical scene by structure with unchanged GUIDs for the scene, the map data, the ground and the models; the build settings entry survives; an additive scene with a custom object is byte-identical after re-conversion and its object still resolves under the stable names. (FR-1, FR-2, FR-5, FR-6, FR-9, FR-10, FR-11)
  *Done when:* the tests compile and are ignored with `pending T5.3`.
- [x] **T5.2** Add `RoMapNames` (`Group(kind)`, `FindGroup(root, kind)`, the root and child names) and `RoMapOutputPaths` (plan §3). (FR-5, FR-11)
  *Done when:* a test pins the names and the lookups by prefix.
- [x] **T5.3** Implement `Editor/Map/RoMapConverter` and `RoMapSceneBuilder` (plan §4.6): dependencies on demand (`ctx.Ground`, `ctx.MapData`, `ctx.Model` once per distinct file), prefab instances with the placement of T3.2, markers, `Sun` and ambient settings, static flags, saving with `SaveScene` to the same path, closing the temporary scene in `finally`, ledger entries and dependencies. Add `RoJobKind.Map`, the `RoConverterRegistry` entry, `RoImport.Map(string)` and `RoImportContext.Model(RoPath)` if not present. (FR-1, FR-2, FR-5, FR-6, FR-9, FR-10, FR-11)
  *Done when:* `[Ignore]` removed and the tests of T5.1 pass.
- [x] **T5.4** Add the kind `Map` to the window and the menu `Tools/Ragnarok/Convert All Maps` (every `.rsw` at the data root, plan D16), and implement `RoMapOpener` (`Tools/Ragnarok/Open Map`: generated scene, then the additive scene on top when present). Add `RoMapBatchTests`: three synthetic maps with a shared model convert the model once, end with a complete result list, a failing map does not stop the others, a second batch changes nothing. (FR-11, FR-12)
  *Done when:* the batch tests pass and a synthetic map converts and opens from the window and the menu with progress and cancel working.
  *Result (2026-11-18):* `RoMapBatchTests` pass (4/4: menu jobs, batch with a shared model and a corrupt map, second batch all skipped, Open Map with the additive scene). In the editor (Unity MCP) a synthetic map gave the job `Map demo.rsw` from the window's `/` line and from the menu's list (the window lists the kind `Map` from the registry); the run with the progress bar created it, the rerun skipped it, `Open Map` opened it. Cancel is `RoBatch`'s, covered by TASKS-0002's batch tests; the menu's confirmation dialogs were not clicked through (modal). A run reports a missing model once, naming the first map that uses it (SPEC-0002 FR-11).

## Phase 6: Gizmos

- [x] **T6.1** Implement `RoMapGizmos` (`[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]`: map rectangle from the settings, origin, labelled axes) and the label of groups if the hierarchy needs more than the name. (FR-13)
  *Done when:* on a converted reference map selecting the map object shows the rectangle, the origin and the axes, and with nothing selected the Profiler shows no gizmo time.
  *Result (2026-11-18):* prt_fild08 converted in the editor (Unity MCP); with `Map_prt_fild08` selected the Scene view shows the yellow map rectangle labelled `prt_fild08 200 x 200 cubes`, the origin with the X (columns), up and Z (rows, north) axes, and the centre `centre (RSW 0, 0)`. The draw carries the profiler marker `RoMapGizmos.Draw`: over 5 Scene view repaints it took 0.0 µs with nothing selected and about 1.2 ms with the map selected. The group names already show the counts, so no group label gizmo was needed.

## Phase 7: Whole data set

- [x] **T7.1** Add `RoMapGroundContactTests` and `RoMapReferenceTests` (`NeedsData`): the samples of `ground-contact.json` sit at the recorded distance within the recorded tolerance; each reference map converts with no failure; the instance count per group equals the file's counts; every distinct model converted; a second batch skips everything. (FR-1 to FR-12)
  *Done when:* the tests pass on the owner's data.
  *Result (2026-11-18):* on the owner's data `RoMapGroundContactTests` 4/4 (the 29 samples within 0.05 of the recorded distance) and `RoMapReferenceTests` 1/1 in 1 min 43 s: the batch of izlude, prontera and prt_fild08 into a throwaway root gave `Created 3, Updated 0, Skipped 0, Failed 0, Warnings 0`, every group count equals the file's, no placeholder, and the second batch `Skipped 3` with nothing else. The ground-contact test places the instances with `RoRswPlacement` (as the scene builder) on the project's converted prefabs.
- [x] **T7.2** Run **Convert All Maps** on the full data set. Record the number of maps, the models and ground chunks converted on demand, warnings by code (especially `ModelMissing`, `RswNodeNameIgnored`, `RswUnknownAnimType`), failures, total time, the time and scene size of the largest map and its instance count, and whether scenes need a `.gitattributes` rule. (FR-12)
  *Done when:* every map converts or its failure is one listed issue, a second run skips all, and the numbers are in the PR.
  *Result (2026-11-18):* the data set has 3 maps (`izlude`, `prontera`, `prt_fild08`); all converted, 0 failures, 0 warnings (no `ModelMissing`, `RswNodeNameIgnored` or `RswUnknownAnimType`). On demand: 0 models and 0 grounds (all already current from SPEC-0003/0004), 3 map data assets updated with their settings (converter rules 2). Times in the editor: izlude 2.1 s, prontera 2.3 s, prt_fild08 1.8 s; a second run of all three: 0.2 s, `Skipped 3`. Largest map: prontera, 1303 instances (plus 25 sounds, 152 effects), scene 4.4 MB of text YAML (izlude 2.0 MB, prt_fild08 1.8 MB); the size is the per-instance prefab overrides (name, position, rotation, static flags). `.gitattributes`: no rule now, the scenes stay text (diffable, 8.2 MB in all); revisit when the data set grows (BACKLOG).

## Phase 8: Verify and close

- [ ] **T8.1** Placement check: fixed-camera screenshots (top, front and one oblique) of prt_fild08, prontera and izlude against the reference viewer, including the four models earlier reported as misplaced, the two clock towers and the control group; no offset, tilt or mirroring. (FR-3)
  *Done when:* screenshots are in the PR and each case is marked as matching.
- [x] **T8.2** Ground contact: report the numbers of the 20 or more samples of T0.4 with the tolerance, and mark each as matching the reference viewer. (FR-4)
  *Done when:* the table is in the PR.
  *Result (2026-11-18):* distances (Unity units, lowest vertex to the ground under it, tolerance 0.05) measured with `RoRswPlacement` on the converted prefabs; every sample differs by 0.000 from the distance the owner checked against the reference viewer at T0.4.

  | Map | # | Model | Expected | Measured | Matches the reference viewer |
  |---|---|---|---|---|---|
  | prt_fild08 | 420 | `나무잡초꽃\버섯01` | 0.112 | 0.112 | yes |
  | prt_fild08 | 419 | `나무잡초꽃\버섯02` | -0.023 | -0.023 | yes |
  | prt_fild08 | 430 | `나무잡초꽃\넝쿨01` | -0.527 | -0.527 | yes |
  | prt_fild08 | 239 | `나무잡초꽃\덤불02` | 0.535 | 0.535 | yes |
  | prt_fild08 | 244 | `나무잡초꽃\덤불02` | -0.723 | -0.723 | yes |
  | prt_fild08 | 44 | `나무잡초꽃\야자수01` | -0.383 | -0.383 | yes |
  | prt_fild08 | 0 | `프론테라\성_성벽` | 0.000 | 0.000 | yes |
  | prt_fild08 | 513 | `외부소품\이정표01` | -0.014 | -0.014 | yes |
  | prt_fild08 | 238 | `나무잡초꽃\덤불01` | 0.364 | 0.364 | yes |
  | prontera | 147 | `프론테라\시계탑` | 0.100 | 0.100 | yes |
  | prontera | 218 | `프론테라\시계탑` | 0.100 | 0.100 | yes |
  | prontera | 912 | `프론테라\분수대` | 0.100 | 0.100 | yes |
  | prontera | 1288 | `외부소품\게시판` | 0.000 | 0.000 | yes |
  | prontera | 162 | `프론테라\교역소` | 0.030 | 0.030 | yes |
  | prontera | 341 | `프론테라\도구점` | 0.030 | 0.030 | yes |
  | prontera | 0 | `프론테라\성_기둥` | 0.000 | 0.000 | yes |
  | prontera | 1046 | `프론테라\프론테라성입구` | 0.000 | 0.000 | yes |
  | prontera | 433 | `프론테라\휘장가로등` | 0.000 | 0.000 | yes |
  | prontera | 175 | `나무잡초꽃\나무01` | 0.000 | 0.000 | yes |
  | izlude | 340 | `izlude\iz_bridge01` | -1.649 | -1.649 | yes |
  | izlude | 345 | `izlude\iz_bridge01` | -1.261 | -1.261 | yes |
  | izlude | 404 | `izlude\iz_smelter` | -0.200 | -0.200 | yes |
  | izlude | 398 | `izlude\iz_enchant` | 0.000 | 0.000 | yes |
  | izlude | 278 | `아인브로크\비공정` | 2.200 | 2.200 | yes |
  | izlude | 469 | `아인브로크\벤치` | 0.000 | 0.000 | yes |
  | izlude | 3 | `나무잡초꽃\덤불02` | -0.202 | -0.202 | yes |
  | izlude | 290 | `나무잡초꽃\넝쿨01` | -0.378 | -0.378 | yes |
  | izlude | 1 | `외부소품\프론아레나` | -0.100 | -0.100 | yes |
  | izlude | 0 | `프론테라\검사길드` | 0.020 | 0.020 | yes |
- [ ] **T8.3** Baking and batching: bake lightmaps and use static batching on prt_fild08 with no change by hand; check that animated instances still animate and that the real-time lighting and shadows of the scene work; check the lighting of each reference map beside the reference client. (FR-6, FR-8)
  *Done when:* screenshots and the batching statistics are in the PR.
- [ ] **T8.4** Additive layer and gizmos by hand: add a custom object in an additive scene, re-convert, open with `Tools/Ragnarok/Open Map`, and see the object still in place; select the map object and see the gizmos. (FR-11, FR-13)
  *Done when:* screenshots are in the PR.
- [x] **T8.5** Delete `RswImporter.cs` (and its `.meta`), the old scenes and prefabs listed by T0.1 that the new scenes replace, and the parts of `UtilExtensions.cs` that only it used (and the whole file if nothing else uses it; a search result is pasted). Update `plan.md` §7 if the list of remaining old importers changes. (FR-1)
  *Done when:* the project compiles with no reference to the deleted classes and `RoArchitectureTests` (TASKS-0002 T7.2) still pass.
  *Result (2026-11-18, done before T8.1 to T8.4, which need the owner):* `RswImporter.cs` and its `.meta` deleted. A search of `Assets` (`*.cs`, `*.asmdef`, `*.unity`, `*.prefab`) for `RswImporter` finds nothing else; no old scene or prefab existed (T0.1); `UtilExtensions.cs` was already deleted (TASKS-0004 T8.4). The project compiles and `RoArchitectureTests` pass (11/11). Plan §7 records the old importers left.
- [ ] **T8.6** Close: verify each FR of the spec against the tests and manual evidence; also close FR-3 of SPEC-0003 with the ground-contact result and set TASKS-0003/PLAN-0003/SPEC-0003 to `Implemented` if everything else there is done; set the status of this spec, plan and tasks to `Implemented`; move leftovers to `docs/specs/BACKLOG.md`.
  *Done when:* the traceability table of the plan has a passing test or recorded evidence for each of FR-1 to FR-13 and the leftovers are in `BACKLOG.md`.

---

## Coverage check (FR → tasks)

| FR | Tasks |
|---|---|
| FR-1 | T0.1, T0.2, T1.1, T1.2, T1.3, T5.1, T5.3, T7.1, T8.5 |
| FR-2 | T5.1, T5.3, T7.1 |
| FR-3 | T0.3, T1.1, T3.1, T3.2, T8.1 |
| FR-4 | T0.4, T7.1, T8.2 |
| FR-5 | T5.1, T5.2, T5.3, T7.1 |
| FR-6 | T5.1, T5.3, T8.3 |
| FR-7 | T0.1, T0.2, T2.1, T2.2 |
| FR-8 | T0.3, T3.1, T3.2, T8.3 |
| FR-9 | T4.1, T5.1, T5.3 |
| FR-10 | T4.1, T5.1, T5.3 |
| FR-11 | T5.1, T5.2, T5.3, T5.4, T8.4 |
| FR-12 | T0.1, T5.4, T7.1, T7.2 |
| FR-13 | T6.1, T8.4 |
