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
- [ ] **T0.2** Map GRF to the file fields (plan §4.1). In a scratch editor script load each reference `.rsw` through the GRF `Rsw` class and print the version, the file references, the water, the lighting values and the first and last object of each type, taking GRF's values as they are (no comparison with the file bytes, plan D11). Check the counts against `reference-models.md` §4. Write into `plan.md` §4.1 a table: one row per `RoRsw` field with the GRF member that supplies it, and one note for every derived member that must not be used. Record the versions of the three maps and where the water plane is read from. (FR-1, FR-7)
  *Done when:* every `RoRsw` field has a GRF member (or is declared absent for the version), the versions are recorded, and the table is in `plan.md`.
- [ ] **T0.3** Verify the rules table (plan §4.3, §4.4). In a scratch editor script place the models of prt_fild08 using the GRF values of T0.2 and the candidate rules (centre offset from `RoMapSpace`, position scale, rotation order and signs, scale Y sign), on the throwaway ground of TASKS-0004, and compare with the reference viewer from top, front and oblique. Do the four models earlier reported as misplaced first, then the control group of `reference-models.md`, then prontera's two clock towers and izlude. For any model that differs change the rule (not the model) and repeat; check the earlier models after every change. Also record: non-empty `NodeName` values (count and whether the node is the main node), animation types and speeds seen, collision values seen, a check of the ambient and sun colours against the reference viewer, and the sun direction against the shadow of a tall model. Record the final rules and "result by model" in `plan.md` §4.3 and §4.4, and decide with the owner on `NodeName`. (FR-3, FR-8)
  *Done when:* all four reported models, the control group and both clock towers match in the three views, the rules table and lighting rule are in `plan.md`, the owner has seen the comparison images, and the scratch script is deleted.
- [ ] **T0.4** Record the ground-contact samples (plan §4.3). With the owner, pick at least 20 models across the three reference maps (street props, buildings, fountains and bridges, trees on slopes, the clock towers), measure for each in the reference viewer the vertical distance between the model's lowest point and the ground under it, and write `Assets/Tests/EditMode/RoImport/Rsw/ground-contact.json` (map, instance index, model file, expected distance, tolerance). (FR-4)
  *Done when:* the file has at least 20 samples with distances the owner agrees with, and it is committed.

## Phase 1: Data and reader

- [ ] **T1.1** Add `Core/Rsw/` types exactly as in plan §4.1 (`RoRsw`, `RoRswWater`, `RoRswLight`, `RoRswModel`, `RoRswPointLight`, `RoRswSound`, `RoRswEffect`) and a test-only builder `RswTestMaps` in `Assets/Tests/EditMode/RoImport/Rsw/` (a map with one model at the centre, one at each corner, rotated and mirrored models, a light, a sound, an effect, a missing-model reference, a non-square map). Add the issue codes `UnsupportedRswVersion`, `RswFileMismatch`, `RswNodeNameIgnored`, `RswUnknownAnimType`, `ModelMissing` to `RoIssueCode`. (FR-1, FR-3)
  *Done when:* the project compiles and a test builds each sample map and checks its counts.
- [ ] **T1.2** Add `RoRswReaderTests` (`NeedsData`, failing, ignored): the three reference maps read through GRF with the object counts of `reference-models.md` §4; the versions are as recorded in T0.2; a truncated copy and an empty array return false with a reason and never throw. (FR-1)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [ ] **T1.3** Implement `Core/Formats/RoRswReader` using the GRF members recorded by T0.2. No parsing code of our own. (FR-1)
  *Done when:* `[Ignore]` removed and `RoRswReaderTests` pass; a text search shows no `BinaryReader` or `BitConverter` use added.

## Phase 2: Map-wide values and settings

- [ ] **T2.1** Add `RoMapSettingsTests` (failing, ignored): settings equal the RSW (water level converted to Unity units, lighting values, size); an absent RSW converts without settings. (FR-7)
  *Done when:* the tests compile and are ignored with `pending T2.2`.
- [ ] **T2.2** Extend `RoMapDataConverter` (PLAN-0005) to read the RSW, fill `MapData.settings`, set `hasSettings` and record the RSW hash as a dependency. (FR-7)
  *Done when:* `[Ignore]` removed, the tests of T2.1 pass, and the PLAN-0005 tests still pass.

## Phase 3: Placement and lighting

- [ ] **T3.1** Add `RoRswPlacementTests` and `RoRswLightingTests` (failing, ignored): a model at the map centre, at a corner and at a non-square map's corner; a rotation about each axis (order and signs pinned); mirrored scale on one and three axes (the instance is not inside-out); positions relative to the centre equal `RoMapSpace.FromRswRo`; ambient and sun colours, shadow strength and sun rotation for a table of longitude and latitude values. (FR-3, FR-8)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [ ] **T3.2** Implement `Core/Rsw/RoRswRules` (values of T0.3), `RoRswPlacement` (one matrix chain in Ragnarok space, one conversion) and `RoRswLighting` (plan §4.3, §4.4). (FR-3, FR-8)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.

## Phase 4: Runtime markers

- [ ] **T4.1** Add `Assets/Scripts/Map/MapMarkers.cs` to `Uninarok.Map`: `MapLightMarker`, `MapSoundMarker`, `MapEffectMarker`, `MapMissingModelMarker` (data only, public fields, no logic, no editor or importer references) and extend `MapDataAssemblyTests` to cover them. (FR-9, FR-10)
  *Done when:* the project compiles, the assembly test still passes, and a test creates each marker and checks its fields.

## Phase 5: The scene

- [ ] **T5.1** Add `RoMapSceneTests`, `RoMapMissingModelTests`, `RoMapIdempotencyTests` and `RoMapAdditiveTests` (failing, ignored): a synthetic map converts to a scene with `Map_<name>` (with `MapRoot` and the `MapData` reference), `Ground`, the four groups with their counts, and `Sun`; each model object is a prefab instance of the model prefab (changing the prefab's material changes every instance); names and order are the file order; lights, sounds and effects are markers with their parameters; static flags are set on ground chunks and instances and not on animated instances, markers and groups; a missing model gives one warning naming model and map and a placeholder with the instance's name and transform; the second conversion is `Skipped`; a forced second conversion gives an identical scene by structure with unchanged GUIDs for the scene, the map data, the ground and the models; the build settings entry survives; an additive scene with a custom object is byte-identical after re-conversion and its object still resolves under the stable names. (FR-1, FR-2, FR-5, FR-6, FR-9, FR-10, FR-11)
  *Done when:* the tests compile and are ignored with `pending T5.3`.
- [ ] **T5.2** Add `RoMapNames` (`Group(kind)`, `FindGroup(root, kind)`, the root and child names) and `RoMapOutputPaths` (plan §3). (FR-5, FR-11)
  *Done when:* a test pins the names and the lookups by prefix.
- [ ] **T5.3** Implement `Editor/Map/RoMapConverter` and `RoMapSceneBuilder` (plan §4.6): dependencies on demand (`ctx.Ground`, `ctx.MapData`, `ctx.Model` once per distinct file), prefab instances with the placement of T3.2, markers, `Sun` and ambient settings, static flags, saving with `SaveScene` to the same path, closing the temporary scene in `finally`, ledger entries and dependencies. Add `RoJobKind.Map`, the `RoConverterRegistry` entry, `RoImport.Map(string)` and `RoImportContext.Model(RoPath)` if not present. (FR-1, FR-2, FR-5, FR-6, FR-9, FR-10, FR-11)
  *Done when:* `[Ignore]` removed and the tests of T5.1 pass.
- [ ] **T5.4** Add the kind `Map` to the window and the menu `Tools/Ragnarok/Convert All Maps` (`Enumerate("map/")`, extension `.rsw`), and implement `RoMapOpener` (`Tools/Ragnarok/Open Map`: generated scene, then the additive scene on top when present). Add `RoMapBatchTests`: three synthetic maps with a shared model convert the model once, end with a complete result list, a failing map does not stop the others, a second batch changes nothing. (FR-11, FR-12)
  *Done when:* the batch tests pass and a synthetic map converts and opens from the window and the menu with progress and cancel working.

## Phase 6: Gizmos

- [ ] **T6.1** Implement `RoMapGizmos` (`[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]`: map rectangle from the settings, origin, labelled axes) and the label of groups if the hierarchy needs more than the name. (FR-13)
  *Done when:* on a converted reference map selecting the map object shows the rectangle, the origin and the axes, and with nothing selected the Profiler shows no gizmo time.

## Phase 7: Whole data set

- [ ] **T7.1** Add `RoMapGroundContactTests` and `RoMapReferenceTests` (`NeedsData`): the samples of `ground-contact.json` sit at the recorded distance within the recorded tolerance; each reference map converts with no failure; the instance count per group equals the file's counts; every distinct model converted; a second batch skips everything. (FR-1 to FR-12)
  *Done when:* the tests pass on the owner's data.
- [ ] **T7.2** Run **Convert All Maps** on the full data set. Record the number of maps, the models and ground chunks converted on demand, warnings by code (especially `ModelMissing`, `RswNodeNameIgnored`, `RswUnknownAnimType`), failures, total time, the time and scene size of the largest map and its instance count, and whether scenes need a `.gitattributes` rule. (FR-12)
  *Done when:* every map converts or its failure is one listed issue, a second run skips all, and the numbers are in the PR.

## Phase 8: Verify and close

- [ ] **T8.1** Placement check: fixed-camera screenshots (top, front and one oblique) of prt_fild08, prontera and izlude against the reference viewer, including the four models earlier reported as misplaced, the two clock towers and the control group; no offset, tilt or mirroring. (FR-3)
  *Done when:* screenshots are in the PR and each case is marked as matching.
- [ ] **T8.2** Ground contact: report the numbers of the 20 or more samples of T0.4 with the tolerance, and mark each as matching the reference viewer. (FR-4)
  *Done when:* the table is in the PR.
- [ ] **T8.3** Baking and batching: bake lightmaps and use static batching on prt_fild08 with no change by hand; check that animated instances still animate and that the real-time lighting and shadows of the scene work; check the lighting of each reference map beside the reference client. (FR-6, FR-8)
  *Done when:* screenshots and the batching statistics are in the PR.
- [ ] **T8.4** Additive layer and gizmos by hand: add a custom object in an additive scene, re-convert, open with `Tools/Ragnarok/Open Map`, and see the object still in place; select the map object and see the gizmos. (FR-11, FR-13)
  *Done when:* screenshots are in the PR.
- [ ] **T8.5** Delete `RswImporter.cs` (and its `.meta`), the old scenes and prefabs listed by T0.1 that the new scenes replace, and the parts of `UtilExtensions.cs` that only it used (and the whole file if nothing else uses it; a search result is pasted). Update `plan.md` §7 if the list of remaining old importers changes. (FR-1)
  *Done when:* the project compiles with no reference to the deleted classes and `RoArchitectureTests` (TASKS-0002 T7.2) still pass.
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
