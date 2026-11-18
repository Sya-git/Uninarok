# TASKS-0003: RSM Models

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved |

**How to use this file**
- Start only when TASKS-0002 phases 0 to 6 are done (the foundation: paths, `RoSpace`, writer, ledger, materials, context, batch, texture converter). Tasks here use those contracts as written in PLAN-0002.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0003): T2.2 implement RsmHierarchy` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent.
- Paths are relative to `Uninarok-client/Assets/Editor/RoImport/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a spike (phase 0) shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Spike scripts are scratch code: delete them when the task is done. They are the only code that may print or inspect raw values; production code never parses RSM bytes (PLAN-0002 §1).
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Verify before building

- [x] **T0.1** Check the data: list the reference models of `reference-models.md` §1 to §3 and the three reference maps in the source folder, count the `.rsm` and `.rsm2` files under `model/`, and record the counts and any missing reference model in the PR. (FR-8)
  *Done when:* every reference model path exists in the source folder or its absence is listed and agreed with the owner; the counts are in the PR.
- [x] **T0.2** Map GRF to the file fields (plan §4.1). In a scratch editor script, load each reference model of `reference-models.md` through the GRF `Rsm` class and print, per node, every member that could correspond to a file field (the members named in plan §4.1, plus offset, texture slots, shade type, animation length, keyframes, smoothing group, two-sided flag, version). Compare them with the raw values of the four reported models (single node, rotation angle and position non-zero) taken from the earlier scan of `reference-models.md`, and with a model with a parent and children. Write the result into `plan.md` §4.1 as a table: one row per `RoRsm` field with the GRF member that supplies it, and one note for every GRF member that is derived and must not be used. Record whether `.rsm2` files load through GRF in the editor. (FR-2, FR-8)
  *Done when:* every `RoRsm` field has a GRF member (or is declared absent for the version), the derived members are listed as unused, the `.rsm2` outcome is recorded, and the table is in `plan.md`.
- [x] **T0.3** Verify rule set R1 (plan §4.4). In a scratch editor script, apply R1 to the models of the reference tables using the GRF values mapped in T0.2 (rest pose from the first keyframe, local matrix, model matrix, node offset on its own vertices only, recentering by the bounds), build throwaway meshes in a scratch scene, and compare each with the reference viewer from front, side and top. Do the four reported models and the control group first, then one model of every arrangement of §3. For any model that differs, change the rule (not the model) and repeat; check that the control group still matches after every change. Also scan the full extracted `model/` folder for other versions and for mirrored scale if the folder is available. Record in `plan.md` §4.4: the final rule, the `RsmRules` table rows, "R1 result by model" (match or difference with the reason), and the versions and mirrored models found. (FR-2, FR-3, FR-5, FR-8)
  *Done when:* all four reported models and all control-group models match the reference viewer in the three views, the shade-type check of plan §4.6 (none, flat, smooth, with `iz_enchant`, `이즈루드초소`, `넝쿨01`) is recorded, the table is in `plan.md`, and the owner has seen the comparison images.
- [x] **T0.4** Verify animation timing (plan §4.8). In a scratch script read the keyframes of the clock tower (`프론테라/시계탑`) and the stress model `라헬/분수` through GRF, drive a throwaway object with them, and compare speed, direction and loop with the reference viewer. Determine the unit of the keyframe times, whether the last key wraps to the first, and whether the rest pose is the first key. Record the constant, any per-version difference and the loop behavior in `plan.md` §4.8. (FR-9)
  *Done when:* the clock tower matches the reference viewer in speed, direction and loop, the `RsmTiming` constant and loop rule are written into `plan.md`, and the scratch script is deleted.

## Phase 1: Data and reader

- [x] **T1.1** Add `Core/Rsm/Data/`: `RoRsm`, `RoRsmNode`, `RoRsmFace`, `RoRsmTexCoord`, `RoRsmPosKey`, `RoRsmRotKey`, `RoRsmScaleKey`, `RsmShadeType` exactly as in plan §4.1, plus a test-only builder `RsmTestModels` in `Assets/Tests/EditMode/RoImport/Rsm/` that creates small models in code (a cube, a cylinder strip, a single node with rotation and position, a parent with a child, a two-node animated model). Add the new issue codes `UnsupportedRsmVersion`, `RsmParentMissing`, `RsmCycle`, `RsmDuplicateName`, `RsmBadIndex` to `RoIssueCode`. (FR-1, FR-8)
  *Done when:* the project compiles and a test builds each sample model and checks its counts (vertices, faces, nodes, keys).
- [x] **T1.2** Add `RoRsmReaderTests` (`NeedsData`, failing, ignored): every reference model reads, its node names, parent names, vertex and face counts and keyframe counts equal the table of `reference-models.md`; the versions are as recorded in T0.3; a truncated copy of a model and an empty array return false with a reason and never throw. (FR-8)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [x] **T1.3** Implement `Core/Formats/RoRsmReader` using the GRF members recorded by T0.2 (fields GRF does not supply keep their defaults, `HasShadeType` is set from the version). No parsing code of our own. (FR-8, FR-2)
  *Done when:* `[Ignore]` removed and `RoRsmReaderTests` pass; a text search shows no `BinaryReader` or `BitConverter` use added.

## Phase 2: Hierarchy

- [x] **T2.1** Add `RsmHierarchyTests` (failing, ignored): a child listed before its parent gives the correct tree and `Order`; names are kept; duplicate names get `_2`, `_3` and one warning; a missing parent makes a root with one warning; a cycle fails with one issue; order is stable for nodes ready at the same time; a model with several roots lists them all. (FR-1)
  *Done when:* the tests compile and are ignored with `pending T2.2`.
- [x] **T2.2** Implement `Core/Rsm/Tree/RsmHierarchy` and `RsmTree` (plan §4.2). (FR-1)
  *Done when:* `[Ignore]` removed and `RsmHierarchyTests` pass.

## Phase 3: Placement

- [ ] **T3.1** Add `RsmPoseTests` and `RsmSpaceConversionTests` (failing, ignored), with the expected values worked out by hand in comments: identity node; rotation only; position only; position and rotation (the class of the reported models, with the numbers of one of them); offset matrix and offset vector applied to the node's own vertices and not to its child; a parent rotation moves the child; first-key rest pose beats the static fields; a mirrored scale and a mirrored offset matrix keep outward facing (positive signed volume of a unit cube); `Recenter` puts the bottom center of the bounds at the origin and adds the translation to position keys of a root; a test that applies the old importer's formula (root-only global transform, `GlobalPosition` subtraction) to the reported-model sample and fails, as a guard; for random nodes the Unity `localPosition`, `localRotation` and `localScale` composed with the converted vertices equal `RoSpace.Matrix(Model · A)` applied to the Ragnarok vertex. (FR-2, FR-3)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [ ] **T3.2** Implement `Core/Rsm/Pose/`: `RsmRules` (table from T0.3, one row per known version, none for unknown), `RsmPose.ComputeRest`, `RsmBounds`, and the conversion helpers of plan §4.5 (node transform to `localPosition`/`localRotation`/`localScale` through `RoSpace`, vertex conversion with the extra winding reversal when the offset matrix mirrors). (FR-2, FR-3)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.
- [ ] **T3.3** Add `RsmReferenceTests` part 1 (`NeedsData`): for the four reported models and the control group, the converted root rest transform and the bounds equal the values recorded by T0.3 within 1e-4. (FR-2)
  *Done when:* the tests pass on the reference data.

## Phase 4: Meshes

- [ ] **T4.1** Add `RsmMeshBuilderTests` (failing, ignored): a cube with per-face UVs gives 24 vertices; a cube with seamless UVs gives 8; a cylinder strip gives the number of distinct (position, uv) pairs; equal-valued vertices at different file indices merge; duplicate faces in the file are kept; 16-bit indices below 65,536 vertices and 32-bit above (a generated large node); the uv is `(u, 1 - v)`. (FR-4)
  *Done when:* the tests compile and are ignored with `pending T4.3`.
- [ ] **T4.2** Add `RsmShadingTests` and `RsmSubmeshTests` (failing, ignored): smooth shading gives equal normals on both sides of a UV seam at one position; angle weighting makes the normal independent of how a flat quad is triangulated; flat gives each face its own normal and raises the vertex count only by the duplicated corners; two smoothing groups keep a hard edge; none gives one constant normal; a version without shade type is smooth; faces group by (texture, twoSided) in ascending order; a two-sided face is not duplicated; the material key of a two-sided group has `twoSided = true`; a bad texture slot drops that face with one `RsmBadIndex` warning per node. (FR-5, FR-6, FR-7)
  *Done when:* the tests compile and are ignored with `pending T4.3`.
- [ ] **T4.3** Implement `Core/Rsm/Mesh/`: `RsmMeshBuilder`, `RsmMeshData`, `RsmSubmesh`, `RsmShading` per plan §4.6 (weld by value, vertex key of position, uv and normal group, angle-weighted normals, winding reversed once through `RoSpace.ReverseWinding`, submeshes). (FR-4, FR-5, FR-6)
  *Done when:* `[Ignore]` removed and the tests of T4.1 and T4.2 pass.
- [ ] **T4.4** Add `RsmReferenceTests` part 2 (`NeedsData`): for every reference model the vertex count is lower than three per face with an equal triangle count; the sum over nodes of distinct (position, uv) pairs equals the vertex count for the smooth models of the reference set; shade-type samples (none, flat, smooth) produce the normal rules of plan §4.6. (FR-4, FR-5)
  *Done when:* the tests pass on the reference data.

## Phase 5: Animation data

- [ ] **T5.1** Add `RsmAnimationTests` (failing, ignored): position keys convert through `RoSpace.Point` and roots include the recenter translation; rotation keys `(x, y, z, w)` become `(-x, y, -z, w)`; scale keys are unchanged; `RsmTiming` maps file units to seconds with the constant from T0.4 (milliseconds); nodes are grouped by their last key time into loop groups in ascending order, and each group's length equals that time converted; a node whose last key time is 0 holds its first key; quaternion keys keep their file sign; only nodes with keys get curves. (Changed by T0.4, plan D10: per-node loop, no slerp subdivision.) (FR-9)
  *Done when:* the tests compile and are ignored with `pending T5.2`.
- [ ] **T5.2** Implement `Core/Rsm/Anim/`: `RsmAnimationData`, `RsmNodeCurves`, `RsmKey<T>`, `RsmLoopGroup`, `RsmTiming`, and the key conversion and loop grouping of plan §4.8 (D10). (FR-9)
  *Done when:* `[Ignore]` removed and `RsmAnimationTests` pass.

## Phase 6: Editor side

- [ ] **T6.1** Add `RoJobKind.Model`, the `RoImport.Model(string)` wrapper, `RsmOutputPaths` (prefab, mesh, clip, controller paths of plan §3, file-safe node names with `_2` suffixes) and tests: paths mirror the Ragnarok path; two nodes with the same name give two mesh files; a node name with Korean characters and invalid file characters gives a valid path; two models with the same name in different folders do not collide. (FR-1, FR-10)
  *Done when:* the tests pass.
- [ ] **T6.2** Add `RsmWriterTests` (failing, ignored) using the throwaway output root: converting a hand-made model yields the prefab, one mesh asset per node with faces, and materials as separate assets at the paths of plan §3; the prefab hierarchy equals the tree and node object names are the unique names; a node without faces has no renderer; materials are the shared ones of PLAN-0002 (1000 nodes with one texture reference one material); assigning a mesh to a `MeshCollider` and editing a material do not touch the source file. (FR-1, FR-7, FR-10)
  *Done when:* the tests compile and are ignored with `pending T6.4`.
- [ ] **T6.3** Implement `Editor/Rsm/RsmPrefabBuilder` per plan §4.7: in-memory root and children in tree order, mesh upsert through `RoAssetWriter.UpsertObject`, materials from `ctx.Material` with the surface from the texture analysis, `PrefabUtility.SaveAsPrefabAsset` at the mirrored path, temporary objects destroyed. (FR-1, FR-7, FR-10)
  *Done when:* it compiles and the static (non-animated) cases of `RsmWriterTests` pass when enabled in T6.4.
- [ ] **T6.4** Implement `Editor/Rsm/RsmClipBuilder` and `RsmControllerBuilder` per plan §4.8 (one clip per loop group with linear curves on each animated node's path, loop on; one controller with one layer per loop group, each with one looping state `Play`, no transitions or parameters; the animator with `cullingMode = CullUpdateTransforms` and no root motion), and `RoRsmConverter` (`RulesVersion = 1`, ledger entry in category `model` with outputs and the texture source hashes as dependencies, all-or-nothing writing, the new issue codes), registered in `RoConverterRegistry`, with `Convert All Models` in the menu and the `Model` kind in the window. (FR-8, FR-9, FR-10)
  *Done when:* `[Ignore]` removed and `RsmWriterTests` pass; an animated hand-made model gets a clip and a controller and the prefab has an `Animator` referencing it; a failing model leaves no partial output and its previous outputs unchanged.
- [ ] **T6.5** Add `RsmIdempotencyTests`: the second conversion of a model is `Skipped`; a forced second conversion leaves every mesh, material, clip and controller byte-identical and every GUID unchanged; deleting only the prefab and rerunning restores it with unchanged meshes; a changed texture content (a fixture swapped between opaque and keyed) makes the model stale and updates its material mode. (0002 FR-5, FR-16)
  *Done when:* the tests pass; if the forced re-save of the prefab differs textually, record the difference in `plan.md` §9 and ask the owner before relaxing the check.

## Phase 7: Whole data set

- [ ] **T7.1** Add `RsmReferenceTests` part 3 (`NeedsData`): every model of the reference tables converts through `RoImport.Model` with no failure; the four reported models and the control group convert and match the recorded rest transforms; a batch over the whole `model/` folder converts every file or lists each failure exactly once, with no `LogError`; any file with a version other than 1.4 is converted or reported once. (FR-2, FR-8)
  *Done when:* the tests pass on the reference data, and the batch summary is pasted into the PR.
- [ ] **T7.2** Run the whole `model/` folder with the window, then run it again. (FR-8, 0002 FR-5, FR-16)
  *Done when:* the first run ends with a failure list that is empty or fully explained (pasted into the PR, unsupported versions named); the second run reports everything `Skipped` and `git status` shows no changed file.

## Phase 8: Verify and close

- [ ] **T8.1** Visual comparison: each model of the reference tables (the reported four first) imported and placed in a scratch scene, compared with the reference viewer from front, side and top; screenshots attached to the PR and referenced from `reference-models.md`. (FR-2)
  *Done when:* no model in the reference set is offset, rotated or mirrored differently from the reference viewer, or each difference is listed and the owner has decided.
- [ ] **T8.2** Lighting check: `야자수01` trunk and a barrel-like model show no facets or seams at texture borders; `넝쿨01`, `동굴입구` and `iz_bridge01` keep crisp edges; `iz_enchant` and `이즈루드초소` match the reference; a two-sided leaf plane is visible from both sides and `프론테라/입구초소` shows no see-through walls from outside. (FR-5, FR-6)
  *Done when:* screenshots are in the PR and each case is marked as matching.
- [ ] **T8.3** Animation budget: in a scratch scene instantiate 200 copies of the clock tower and, in a standalone development build on the owner's machine, record with the Profiler the managed allocation per frame and the animation update time, with all instances on screen and then all off screen. (FR-9)
  *Done when:* `GC.Alloc` for the animation update is zero in steady state and the update takes at most 0.5 ms (plan §4.8), or the owner has agreed another budget in writing, or the D5 fallback is opened as a new plan item. The clock tower's speed and direction are confirmed again in the build.
- [ ] **T8.4** Delete `RsmImpoter.cs` and `RsmImpoter.bkp.cs` (and their `.meta` files); delete the parts of `UtilExtensions.cs` that only the RSM importer used, and the whole file if nothing else uses it (a search result is pasted, the remaining users named for PLAN-0004 to PLAN-0006). Update `plan.md` §7 if the list of remaining old importers changes. (FR-10)
  *Done when:* the project compiles with no reference to the deleted classes and `RoArchitectureTests` (TASKS-0002 T7.2) still pass.
- [ ] **T8.5** Close: verify each FR of the spec against the tests and manual evidence; FR-3 is closed together with the ground-contact check of SPEC-0006; set the status of `spec.md`, `plan.md` and `tasks.md` to `Implemented` once FR-3 is confirmed; move leftovers to `docs/specs/BACKLOG.md`.
  *Done when:* the traceability table of the plan has a passing test or recorded evidence for each of FR-1 to FR-10 (FR-3 pending on SPEC-0006 is listed explicitly if not yet closed) and the leftovers are in `BACKLOG.md`.

---

## Coverage check (FR → tasks)

| FR | Tasks |
|---|---|
| FR-1 | T1.1, T2.1, T2.2, T6.1, T6.2, T6.3 |
| FR-2 | T0.2, T0.3, T1.1, T1.3, T3.1, T3.2, T3.3, T7.1, T8.1 |
| FR-3 | T0.3, T3.1, T3.2, T8.5 (closed with SPEC-0006) |
| FR-4 | T4.1, T4.3, T4.4 |
| FR-5 | T0.3, T4.2, T4.3, T4.4, T8.2 |
| FR-6 | T4.2, T4.3, T8.2 |
| FR-7 | T4.2, T6.2, T6.3 |
| FR-8 | T0.1, T0.2, T0.3, T1.1, T1.2, T1.3, T6.4, T7.1, T7.2 |
| FR-9 | T0.4, T5.1, T5.2, T6.4, T8.3 |
| FR-10 | T6.1, T6.2, T6.3, T6.4, T8.4 |
