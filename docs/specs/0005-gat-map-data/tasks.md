# TASKS-0005: GAT Map Data

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved |

**How to use this file**
- Start only when TASKS-0002 phases 0 to 6 and TASKS-0004 phases 0 to 3 are done (the foundation, `RoMapSpace`, the ground rules). Tasks here use those contracts as written in PLAN-0002 and PLAN-0004.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0005): T3.2 implement RoGatRules` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent.
- Paths are relative to `Uninarok-client/Assets/Editor/RoImport/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a spike (phase 0) shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Spike scripts are scratch code: delete them when the task is done. Production code never parses GAT bytes (PLAN-0002 §1).
- Steps that need the running editor (scratch scenes, screenshots, Profiler, a player build) are done with the Unity MCP when it is connected, otherwise by the owner.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Verify before building

- [x] **T0.1** Check the data and the old code: list `.gat` files of the three reference maps and count every `.gat` under `map/`; find every user of `Assets/Scripts/MapData.cs` (`TileType`, `SetMapData`) and of the debug texture of `GatImporter.cs` with a project-wide search, and list them with what each needs from the new asset. Record all in the PR. (FR-1, FR-4)
  *Done when:* every reference `.gat` exists or its absence is agreed with the owner, and the list of old users is in the PR.
  *Result (2026-11-18):* the `.gat` files are at the data root, not in `map/` (as PLAN-0004 D11; plan D9). The data set has 3 `.gat`: prontera 2,446,094 B (312 × 392 cells), izlude 1,608,014 B (268 × 300), prt_fild08 3,200,014 B (400 × 400); each size is `14 + cells × 20`, and each grid is 2 × its GND. Old users: `Assets/Scripts/MapData.cs` (`Uninarok.Data.MapData`, `TileType`, `SetMapData`) is used only by `Assets/Editor/GatImporter.cs` (compiled out by `RO_LEGACY_IMPORTERS`; it wanted tile data, size and the debug texture, all replaced by the new asset and the optional image) and by six empty assets `Assets/Data/Map/{1@4igd,izlude,prontera,prt_fild01,prt_fild02,prt_fild08}.asset` (size 0, no texture; nothing references them: no scene, prefab or script). No scene, prefab or runtime script uses the old class or the debug texture. T6.1 deletes the six assets with the class (plan D9).
- [x] **T0.2** Map GRF to the file fields and fix the type table (plan §4.1, §4.2). In a scratch editor script, load each reference `.gat` through the GRF `Gat` class and print the header and a handful of cells; compare with the raw bytes (include a water cell and a cliff cell). Record how GRF exposes the water flag of 1.3 and the old 1.2 prefix, the version of each reference map and the set of raw types actually present. Print the `GatType` enum of the shipped `GRF.dll` and compare it with `docs/data/GatCellType.md`; for each raw type that occurs on the reference maps, check its walkable and snipable reading against the reference viewer (a cell of each type on screen) and update `docs/data/GatCellType.md` and `plan.md` §4.2 together if one differs. Write the `RoGat` field-to-GRF-member table into §4.1. (FR-1)
  *Done when:* every `RoGat` field has a GRF member, `docs/data/GatCellType.md` and the type table of the plan agree with GRF's enum and with the reference viewer, and the observed raw types are listed.
  *Result (2026-11-18):* GRF and the raw bytes agree on every cell of the three maps (field table, samples and unused members in plan §4.1). All three maps are GAT 1.2, raw types 0, 1 and 5 only, no bit 31, no prefix; GRF does not expose the version (the adapter reads bytes 4 and 5, D10) and throws on a truncated array (the adapter checks the length first). The type table was compared with the Hercules map server instead of a viewer: `map_gat2cell` treats 2 and 4 as walkable, 3 as walkable water and bit 31 as unrecognized, unlike the first draft that read GRF's names. The owner chose the server's rules and names (D2): `docs/data/GatCellType.md` and plan §4.2 now hold that table (types `Walkable`, `Wall`, `Type2`, `Water`, `Type4`, `Cliff`, `Type6`, `Other`; flags `Walkable`, `Shootable`, `Water`). For the types on the reference maps (0, 1, 5) GRF's names, the draft and the server agree.
- [x] **T0.3** Measure GAT against the ground (plan §4.4). In a scratch editor script read each reference map's GAT and GND through GRF, take the four corners, the centre and 100 random cells (fixed seed), compute the ground height at each cell centre with the candidate rules of PLAN-0004 and compare with the GAT corner mean. Record the maximum and mean difference per map, any systematic offset, and the cell position check (a cell lies in the cube `cell / 2` on prontera and izlude, which are not square). Write `GroundTolerance` and the finding into `plan.md` §4.4. (FR-3)
  *Done when:* the numbers are in the plan, the tolerance is agreed with the owner, and the scratch script is deleted.
  *Result (2026-11-18):* numbers in plan §4.4. Same sign and scale, no offset, cell `(x, z)` in cube `(x / 2, z / 2)` on the non-square maps (99.7% of prontera's walkable cells on the ground, 69.6% with mirrored rows). Walkable model surfaces (bridges, decks) lie above the ground: 21% of izlude. The owner approved a two-part FR-3 check (spec FR-3 acceptance updated, plan D11): never more than `GroundTolerance = 0.3` Unity units below the ground, and at least 98% within 0.05 units on prontera and prt_fild08; `CellCenter` uses the diagonal rule. The scratch code ran through the Unity MCP and was never saved to the project.

## Phase 1: Runtime assembly and data asset

- [x] **T1.1** Create `Assets/Scripts/Map/Uninarok.Map.asmdef` (no editor references, no importer references), `MapCellType`, the serialized fields of `MapData` (plan §4.3) and `MapRoot`. Add `Assets/Tests/EditMode/Map/` with an assembly for runtime query tests that references `Uninarok.Map` only. (FR-4, FR-5)
  *Done when:* the project compiles, a test creates a `MapData` in memory with synthetic arrays, and `MapDataAssemblyTests` (written failing, ignored `pending T1.2`) are added.
- [x] **T1.2** Add `MapDataQueryTests` and `MapDataStorageTests` (failing, ignored): cell (0, 0), the last cell, a cell edge, both triangles of a cell, positions outside on all four sides; heights, flags and types by cell; storage bytes per cell at most 24 and linear growth; no allocation over 10,000 calls; non-square maps and both row signs. (FR-2, FR-5)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [x] **T1.3** Implement the query methods of `MapData` (`TryGetCell`, `InBounds`, `IsWalkable` ×2, `TypeAt`, `TryGetHeight` with the diagonal rule, `CellCenter`, `CornerHeights`). (FR-2, FR-5)
  *Done when:* `[Ignore]` removed and the tests of T1.2 and `MapDataAssemblyTests` pass.

## Phase 2: Data type and reader

- [x] **T2.1** Add `Core/Gat/RoGat` exactly as in plan §4.1 and the issue codes `GatGndMismatch`, `GatUnknownType`, `GatBadHeader`; add a test-only builder `GatTestMaps` in `Assets/Tests/EditMode/RoImport/Gat/` (a flat 4 × 4 cell map, a ramp, a cliff cell, a 8 × 6 and a 6 × 8 map with an asymmetric pattern, a water cell). (FR-1)
  *Done when:* the project compiles and a test builds each sample map and checks its counts.
- [x] **T2.2** Add `RoGatReaderTests` (`NeedsData`, failing, ignored): the three reference maps read through GRF with `Width = 2 × GND width` and `Height = 2 × GND height`; sample cells equal the raw values recorded by T0.2; a truncated copy and an empty array return false with a reason and never throw. (FR-1)
  *Done when:* the tests compile and are ignored with `pending T2.3`.
- [x] **T2.3** Implement `Core/Formats/RoGatReader` using the GRF members recorded by T0.2. No parsing code of our own. (FR-1)
  *Done when:* `[Ignore]` removed and `RoGatReaderTests` pass; a text search shows no `BinaryReader` or `BitConverter` use added.

## Phase 3: Rules and builder

- [ ] **T3.1** Add `RoGatRulesTests` and `RoGatBuilderTests` (failing, ignored): the type table of plan §4.2 and `docs/data/GatCellType.md` (raw 0 to 7, the absent cell -1, a high value, bit 31 with each type), walkable, shootable and water per type, `GatUnknownType` once per map with a count; a synthetic 4 × 3 cube map (8 × 6 cells) builds the expected heights, types and flags; a known corner lands at the `RoMapSpace` position; non-square maps are not swapped; a grid that is not `2 × GND` gives `GatGndMismatch` and no output; heights are converted to Unity up. (FR-1, FR-3)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [ ] **T3.2** Implement `Core/Gat/RoGatRules` (the table of plan §4.2) and `RoGatBuilder` (arrays for `MapData`, geometry fields from `RoMapSpace`, `diagonalFlip` from `RoGndRules`, `rowSign` from `RoMapSpace.RowDirection`). (FR-1, FR-3, FR-5)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.
- [ ] **T3.3** Add `RoGroundSamplerTests` (failing, ignored) and implement `Core/Gnd/Space/RoGroundSampler` (plan §4.4): flat, ramp and cliff synthetic grounds give the expected heights, a gap returns false, the diagonal is the one of `RoGndRules`. (FR-3)
  *Done when:* `[Ignore]` removed and the tests pass.

## Phase 4: Editor side

- [ ] **T4.1** Add `RoMapDataWriterTests` and `RoGatImageTests` (failing, ignored): a synthetic map converts to one asset at `Assets/Data/map/<name>.asset` with the right fields; the second conversion is `Skipped`; a forced conversion leaves the asset byte-identical and its GUID unchanged; a `MapRoot` in a scene keeps its reference after re-conversion; no partial output on failure; the image exists only when requested, has one pixel per cell, one colour per type, the right row direction, and is deterministic. (FR-6, FR-8; SPEC-0002 FR-5, FR-16)
  *Done when:* the tests compile and are ignored with `pending T4.2`.
- [ ] **T4.2** Implement `Editor/Gat/RoMapDataConverter`, `RoMapDataOutputPaths`, `Core/Gat/RoGatImage` and `Editor/Gat/RoGatImageWriter`; add `RoJobKind.MapData`, the `RoConverterRegistry` entry, `RoImport.MapData(string)` and `RoImportContext.MapData(RoPath)`; the ledger category `mapdata` lists the asset (and the image) as outputs and the GAT and GND header hashes as dependencies. (FR-1, FR-3, FR-6, FR-8)
  *Done when:* `[Ignore]` removed and the tests of T4.1 pass.
- [ ] **T4.3** Add the kind `MapData` and the `WithImage` toggle to the window and the menu `Tools/Ragnarok/Convert All Map Data` (`Enumerate("")`, extension `.gat`, plan D9). (FR-6, FR-8)
  *Done when:* a synthetic map converts from the window and from the menu with progress and cancel working, with and without the image.

## Phase 5: Gizmos

- [ ] **T5.1** Add `RoMapDataGizmoSelectorTests` (failing, ignored): region and radius selection, the cap, per-type hiding, the notice flag when the cap cuts. (FR-7)
  *Done when:* the tests compile and are ignored with `pending T5.2`.
- [ ] **T5.2** Implement `RoMapDataGizmoSelector` (pure, in Core) and `RoMapDataGizmoSettings` (radius, cap, layer and type flags in `EditorPrefs`). (FR-7)
  *Done when:* `[Ignore]` removed and the tests pass.
- [ ] **T5.3** Implement `RoMapDataGizmos` (`[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]`: coloured cells per type, grid, corner heights), `RoMapDataOverlay` (type toggles, layers, legend, region tool, cap notice) and the custom inspector of `MapData` with the legend and counts. (FR-7)
  *Done when:* on a converted reference map, selecting a `MapRoot` shows walkable and blocked cells in different colours, a toggle hides a type, the region tool limits drawing, prt_fild08 stays responsive, and with nothing selected the Profiler shows no gizmo time.

## Phase 6: Migration

- [ ] **T6.1** Migrate every user of the old `MapData` listed by T0.1 to the new asset (or record that it is replaced by SPEC-0006's map scene), then delete `Assets/Scripts/MapData.cs` and its `.meta`. (FR-4)
  *Done when:* the project compiles with no reference to the deleted class and the list of migrated users is in the PR.
- [ ] **T6.2** Delete `Assets/Editor/GatImporter.cs` (and its `.meta`) and any debug texture generation; update `plan.md` §7 if the list of remaining old importers changes. (FR-4)
  *Done when:* the project compiles, nothing in the project refers to a generated GAT debug texture, and `RoArchitectureTests` (TASKS-0002 T7.2) still pass.

## Phase 7: Whole data set

- [ ] **T7.1** Add `RoMapDataReferenceTests` (`NeedsData`): sample queries on every reference map equal the source cells; the two-part FR-3 check of plan §4.4 (none more than `GroundTolerance` below the ground, at least `OnGroundShare` within `OnGroundTolerance` on prontera and prt_fild08); every `.gat` of the data root reads, with failures listed by name; a second batch skips everything. (FR-1, FR-2, FR-3)
  *Done when:* the tests pass on the owner's data.
- [ ] **T7.2** Run **Convert All Map Data** on the full data set. Record the number converted, warnings by code (especially `GatUnknownType` values and `GatGndMismatch`), failures, total time, the size of the largest map's asset and its bytes per cell, and the serialization mode; add the `.gitattributes` LFS line for `Assets/Data/map/**/*.asset` if the size warrants it. (FR-5, FR-6)
  *Done when:* every map converts or its failure is one listed issue, a second run skips all, the largest asset is within 24 bytes per cell, and the numbers are in the PR.
- [ ] **T7.3** Build check: make a development player build of an empty scene containing a `MapRoot` for a reference map, with the source folder renamed away; query a walkable cell, a blocked cell and a position outside; search the build output for importer assembly names and for source files. (FR-4)
  *Done when:* the queries are correct in the build and the search finds nothing.

## Phase 8: Verify and close

- [ ] **T8.1** Visual check: on each reference map compare the gizmo colours with the reference viewer's walkable layout (or the server's cell map) for a cliff, a water area, a building and the map border; prontera and izlude are not rotated or mirrored; compare the optional image with the gizmos. (FR-3, FR-7, FR-8)
  *Done when:* screenshots are in the PR and each case is marked as matching.
- [ ] **T8.2** Close: verify each FR of the spec against the tests and manual evidence; set the status of `spec.md`, `plan.md` and `tasks.md` to `Implemented`; move leftovers to `docs/specs/BACKLOG.md`.
  *Done when:* the traceability table of the plan has a passing test or recorded evidence for each of FR-1 to FR-8 and the leftovers are in `BACKLOG.md`.

---

## Coverage check (FR → tasks)

| FR | Tasks |
|---|---|
| FR-1 | T0.1, T0.2, T2.1, T2.2, T2.3, T3.1, T3.2, T4.2, T7.1 |
| FR-2 | T1.2, T1.3, T7.1 |
| FR-3 | T0.3, T3.1, T3.2, T3.3, T4.2, T7.1, T8.1 |
| FR-4 | T0.1, T1.1, T6.1, T6.2, T7.3 |
| FR-5 | T1.1, T1.2, T1.3, T3.2, T7.2 |
| FR-6 | T4.1, T4.2, T4.3, T7.1, T7.2 |
| FR-7 | T5.1, T5.2, T5.3, T8.1 |
| FR-8 | T4.1, T4.2, T4.3, T8.1 |
