# TASKS-0004: GND Ground

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved |

**How to use this file**
- Start only when TASKS-0002 phases 0 to 6 are done (paths, `RoSpace`, writer, ledger, materials, context, batch, texture converter). Tasks here use those contracts as written in PLAN-0002.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0004): T2.1 implement RoMapSpace` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent.
- Paths are relative to `Uninarok-client/Assets/Editor/RoImport/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a spike (phase 0) shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Spike scripts are scratch code: delete them when the task is done. They are the only code that may print or inspect raw values; production code never parses GND bytes (PLAN-0002 §1).
- FR-8 (baked ground lighting) is **not implemented** (owner decision on OQ-2); there are no tasks for it.
- Steps that need the running editor (scratch scenes, screenshots, Profiler) are done with the Unity MCP when it is connected, otherwise by the owner.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Verify before building

- [x] **T0.1** Check the data: list the three reference maps of `0003-rsm-models/reference-models.md` §4 in the source folder (`<name>.gnd`, plus `.gat` and `.rsw` for later specs), count every `.gnd`, and record the counts and any missing file in the PR. Confirm which folder of the data root holds the map files (plan §2). (FR-6)
  *Done when:* every reference map file exists or its absence is listed and agreed with the owner; the counts and the folder are in the PR.
  *Result (2026-11-18):* the map files are at the data root, not in `map/` (owner agreed, plan D11). All nine files present: prontera `.gnd` 3,334,150 B, `.gat` 2,446,094 B, `.rsw` 417,262 B; izlude 2,327,838 / 1,608,014 / 228,430 B; prt_fild08 5,272,734 / 3,200,014 / 219,402 B. The data set has 3 `.gnd`, 3 `.gat`, 3 `.rsw` in total; the largest map is prt_fild08.
- [x] **T0.2** Map GRF to the file fields (plan §4.1). In a scratch editor script, load each reference map through the GRF `Gnd` class and print the header (version, width, height, zoom), the texture list and the first and last cubes. Compare them with the raw values of the same files (a handful of cubes and tiles, including one with tile id `-1` and one with tile id `0`). Confirm that `BottomLeft`, `BottomRight`, `TopLeft`, `TopRight` are the corners `H00`, `H10`, `H01`, `H11`, that the tile coordinates map to `Uv00`, `Uv10`, `Uv01`, `Uv11`, that the cube order is `row * Width + column` on a non-square map, and which GRF members are derived and must not be used. Write the result into `plan.md` §4.1 as a table: one row per `RoGnd` field with the GRF member that supplies it. Record the version and zoom of every reference map. (FR-4, FR-5)
  *Done when:* every `RoGnd` field has a GRF member, the derived members are listed as unused, the versions and zooms of the three maps are recorded, and the table is in `plan.md`.
  *Result (2026-11-18):* table in plan §4.1. GRF equals the raw bytes on every header field, texture, tile and cube of the three maps (zero differences); all are version 1.7, zoom 10. The scratch script ran as an editor snippet through the Unity MCP and left no file.
- [x] **T0.3** Verify the rules table of plan §4.3. In a scratch editor script build throwaway meshes of each reference map from the GRF values mapped in T0.2, using the candidate rules (diagonal `H10`–`H01`, wall facing toward the lower side, wall UV order as for tops, `RowDirection`, V axis flip), and compare them with the reference viewer top-down and from the side, on a cliff, a sloped area, a gap and the map border. For any rule that differs, change the rule (not the map) and repeat. Check that the row direction also matches the placement of at least two RSW objects on the ground (a model on a cliff edge and a model at the map corner). Record in `plan.md` §4.3: the final value of each rule, the `RoGndRules` table row for the versions found, and "result by map" (match, or the difference with its reason). (FR-1, FR-2, FR-4)
  *Done when:* all three reference maps match the reference viewer in both views for the final rules, the cliff has the same visible side from both directions as the reference viewer, the table is in `plan.md`, the owner has seen the comparison images, and the scratch script is deleted.
  *Result (2026-11-18):* the owner compared 13 shots (three whole maps top-down, the izlude cliff from four sides, a slope, gaps, two borders) with the reference viewer: all match with the candidate rules unchanged. Rules, RSW row-direction check and result by map are in plan §4.3. The spike ran as editor snippets in an unsaved scene; the scene and shots are deleted.

## Phase 1: Data and reader

- [x] **T1.1** Add `Core/Gnd/Data/`: `RoGnd`, `RoGndTile`, `RoGndCube` exactly as in plan §4.1, plus a test-only builder `GndTestMaps` in `Assets/Tests/EditMode/RoImport/Gnd/` that creates small maps in code (flat 4 × 4, a step, a ramp, a cliff with a gap, a 5 × 3 and a 3 × 5 map with an asymmetric height pattern, a map with `Zoom = 5`, a 40 × 33 map). Add the issue codes `GndBadIndex` and `UnsupportedGndVersion` to `RoIssueCode`. (FR-1, FR-4, FR-5)
  *Done when:* the project compiles and a test builds each sample map and checks its width, height, cube count and tile count.
- [x] **T1.2** Add `RoGndReaderTests` (`NeedsData`, failing, ignored): the three reference maps read through GRF with the widths and heights of `reference-models.md` §4 and the zoom recorded by T0.2; the cube table length equals `Width · Height`; a tile or texture index outside its table is reported once as `GndBadIndex` and treated as none; a truncated copy and an empty array return false with a reason and never throw. (FR-4, FR-5)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [x] **T1.3** Implement `Core/Formats/RoGndReader` using the GRF members recorded by T0.2. No parsing code of our own. Tile id `-1` is none, `0` is valid. (FR-4, FR-5)
  *Done when:* `[Ignore]` removed and `RoGndReaderTests` pass; a text search shows no `BinaryReader` or `BitConverter` use added.

## Phase 2: Map space

- [x] **T2.1** Add `RoMapSpaceTests` (failing, ignored): corner positions for known columns, rows and heights; `Zoom = 5` gives cubes of 0.5 Unity units and `Zoom = 10` exactly 1 unit; heights keep their own scale through `RoSpace.Point`; `CentreOffsetRo`; `TryCubeAtUnity` round trips every corner and centre of a non-square map and fails outside it; `CubeOfGatCell` is `/ 2`; `RowDirection` has the value recorded by T0.3. (FR-4, FR-5)
  *Done when:* the tests compile and are ignored with `pending T2.2`.
- [x] **T2.2** Implement `Core/Gnd/Space/RoMapSpace` (plan §4.2) with the `RowDirection` constant of T0.3. (FR-4, FR-5)
  *Done when:* `[Ignore]` removed and `RoMapSpaceTests` pass.

## Phase 3: Surfaces

- [x] **T3.1** Add `RoGndSurfacesTests` and `RoGndNonSquareTests` (failing, ignored): a flat map has tops only and no walls; a step has exactly one wall per differing edge, with the owning cube's tile; a wall whose tile is `-1` produces nothing; equal heights at one end give a triangle, not a quad; a cube with `TileUp = -1` is a gap; tile id `0` is a valid tile; edge cubes read no neighbour outside the grid and give no warning; `H00` to `H11` land on the right grid corners; on 5 × 3 and 3 × 5 maps the cube at column 4, row 2 and its neighbours come out at the expected position and tile, and the old `column + row * height` index would fail (regression guard). (FR-1, FR-2, FR-4)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [x] **T3.2** Implement `Core/Gnd/Surfaces/RoGndRules` (values of T0.3), `RoGndQuad` and `RoGndSurfaces.Enumerate` (plan §4.3). (FR-1, FR-2, FR-3)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.

## Phase 4: Meshes

- [x] **T4.1** Add `RoGndCrackTests` and `RoGndWindingTests` (failing, ignored): for every pair of neighbouring cubes whose shared-corner heights are equal the converted positions are bit-identical; walls meet the top edges exactly; the triangle count equals an oracle written separately from the builder (`2 · tops + 2 · walls − degenerate`); no two triangles have identical vertex sets; every top triangle faces up and every wall faces its lower side. (FR-1, FR-2, FR-3)
  *Done when:* the tests compile and are ignored with `pending T4.3`.
- [x] **T4.2** Add `RoGndNormalsTests` and `RoGndChunkTests` (failing, ignored; `RoGndUv2Tests` dropped with the second UV set, plan D6): flat ground gives `up` at every vertex, including across chunk borders; a ramp gives a continuous normal field with no jump at cube borders; tops with different textures share the normal at a common corner; a cliff top corner is not smoothed into the lower ground; wall normals are flat and coplanar walls share their common corners; the vertex count equals the distinct (position, uv) pairs plus the hard-edge vertices (plan D12); a 40 × 33 map gives 3 × 3 chunks with the right cube ranges, a quad belongs to the chunk of its cube, a wall at a chunk border is in its owner's chunk and meets the neighbour's top exactly, indices are 16-bit, submeshes are one per texture in ascending order; a second build gives the same arrays. (FR-3, FR-6, FR-7)
  *Done when:* the tests compile and are ignored with `pending T4.3`.
- [x] **T4.3** Implement `Core/Gnd/Mesh/RoGndChunker`, `RoGndMeshData` and `RoGndMeshBuilder` (plan §4.4): welding with the PLAN-0003 vertex key, map-wide angle-weighted normals, walls flat by face normal, reversed winding once, chunk origin. (FR-1, FR-2, FR-3, FR-6, FR-7)
  *Done when:* `[Ignore]` removed and the tests of T4.1 and T4.2 pass.

## Phase 5: Editor side

- [ ] **T5.1** Add `RoGndWriterTests` and `RoGndIdempotencyTests` (failing, ignored): a synthetic map converts to the prefab and chunk meshes at the paths of plan §3; the prefab has a root named after its file (plan D14) with no transform of its own and one `Chunk_<cx>_<cz>` per chunk, with static flags and shadow receiving on and casting on; materials are the shared ones of the context; a missing texture gives the placeholder and one warning naming the texture and the map; a second conversion is `Skipped`; a forced second conversion leaves every mesh byte-identical and every GUID unchanged; the prefab and a scene that references a chunk mesh keep their references; a failure leaves no partial output. (FR-1, FR-6; SPEC-0002 FR-5, FR-16)
  *Done when:* the tests compile and are ignored with `pending T5.2`.
- [ ] **T5.2** Implement `Editor/Gnd/RoGndConverter`, `RoGndPrefabBuilder` and `RoGndOutputPaths`; add `RoJobKind.Ground`, the `RoConverterRegistry` entry, `RoImport.Ground(string)` and `RoImportContext.Ground(RoPath)`; the ledger category `ground` lists the prefab and meshes as outputs and the texture hashes as dependencies. (FR-1, FR-6)
  *Done when:* `[Ignore]` removed and the tests of T5.1 pass.
- [ ] **T5.3** Add the kind `Ground` to the window and the menu `Tools/Ragnarok/Convert All Grounds` (`Enumerate("")`, extension `.gnd`, plan D11). (FR-6)
  *Done when:* a synthetic ground converts from the window and from the menu, with progress and cancel working, as in the foundation.

## Phase 6: Gizmos and picking

- [ ] **T6.1** Add `RoGndInspectorTests` and `RoGndPickerTests` (failing, ignored): converting a synthetic map and inspecting its chunk meshes gives back every cube's coordinates, face kind, four corner heights and texture; a gap is a cube without a top; front and side walls are told apart on a non-square map; `RoGndPicker` returns the right cube and face for rays from above and from the side on a step map, and nothing for a ray that misses or passes through a gap. (FR-9)
  *Done when:* the tests compile and are ignored with `pending T6.2`.
- [ ] **T6.2** Implement `Core/Gnd/Debug/RoGndMeshInspector` and `RoGndPicker` (plan §4.7). The gap layer needs the map size from the map data of SPEC-0005; until that exists the inspector takes the size as a parameter and the editor side leaves the gap layer off. (FR-9)
  *Done when:* `[Ignore]` removed and the tests of T6.1 pass.
- [ ] **T6.3** Implement `Editor/Gnd/Gizmos/RoGndGizmoSettings` (radius, layer flags in `EditorPrefs`) and the per-mesh cache of inspected chunks (dropped on deselect and domain reload). (FR-9)
  *Done when:* a scratch check shows a chunk is inspected once per selection and dropped on deselect, with nothing read from the source folder.
- [ ] **T6.4** Implement `RoGndGizmos` (`[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MeshFilter))]`, layers: grid, coordinates, corner heights, face kind with texture, gaps) and `RoGndSceneTools` (overlay "Ragnarok Ground" with the layer switches, hover data, click to pin). (FR-9)
  *Done when:* on a converted reference map, selecting `Ground`, a chunk or the map object draws only chunks within the radius, each layer switches off, hovering shows the cube data, and with nothing selected the Profiler shows no gizmo time.

## Phase 7: Whole data set

- [ ] **T7.1** Add `RoGndReferenceTests` (`NeedsData`): each reference map converts with no failure; the chunk and triangle counts equal an oracle computed from the table read through GRF; the largest map converts within the batch; a second batch skips everything; every `.gnd` in the source folder reads, with the failures listed by name. (FR-1 to FR-6)
  *Done when:* the tests pass on the owner's data.
- [ ] **T7.2** Run **Convert All Grounds** on the full data set. Record the number converted, warnings by code, failures, the total time and the largest map's chunk and vertex counts. (FR-6)
  *Done when:* every map converts or its failure is one listed issue, a second run skips all, and the numbers are in the PR.
- [ ] **T7.3** Add the LFS lines needed for the chunk meshes if PLAN-0002's `Art/Imported/**/*.asset` rule does not already match them (check with `git check-attr`). (FR-6)
  *Done when:* a converted chunk mesh is reported as LFS by `git check-attr`.

## Phase 8: Verify and close

- [ ] **T8.1** Visual check of the three reference maps against the reference viewer, top-down and from the side, including a cliff, a gap, sloped ground and the border; prontera and izlude are not rotated or mirrored. (FR-1, FR-2, FR-4)
  *Done when:* screenshots are in the PR and each case is marked as matching.
- [ ] **T8.2** Seam and lighting check: top-down and side views show no light leaks along cube and chunk borders; with a real time directional light and shadows a flat area is evenly lit, a slope shows a smooth gradient, and the ground casts and receives real time shadows. (FR-3, FR-7)
  *Done when:* screenshots are in the PR and each case is marked as matching.
- [ ] **T8.3** Culling check: with the scene camera moved away from a region of the largest map, the stats window shows its chunks no longer drawn, and the material count equals the distinct textures used. (FR-6)
  *Done when:* before and after screenshots with the numbers are in the PR.
- [ ] **T8.4** Delete `GNDImporter.cs` (and its `.meta`) and the parts of `UtilExtensions.cs` that only the GND importer used, and the whole file if nothing else uses it (a search result is pasted, the remaining users named for PLAN-0005 and PLAN-0006). Update `plan.md` §7 if the list of remaining old importers changes. (FR-4)
  *Done when:* the project compiles with no reference to the deleted classes and `RoArchitectureTests` (TASKS-0002 T7.2) still pass.
- [ ] **T8.5** Close: verify each FR of the spec against the tests and manual evidence; set the status of `spec.md`, `plan.md` and `tasks.md` to `Implemented`; move leftovers to `docs/specs/BACKLOG.md`.
  *Done when:* the traceability table of the plan has a passing test or recorded evidence for each of FR-1 to FR-7 and FR-9, FR-8 is listed as not implemented by owner decision, and the leftovers are in `BACKLOG.md`.

---

## Coverage check (FR → tasks)

| FR | Tasks |
|---|---|
| FR-1 | T0.3, T1.1, T3.1, T3.2, T4.1, T4.3, T5.1, T5.2, T7.1, T8.1 |
| FR-2 | T0.3, T3.1, T3.2, T4.1, T4.3, T7.1, T8.1 |
| FR-3 | T3.2, T4.1, T4.2, T4.3, T7.1, T8.2 |
| FR-4 | T0.2, T0.3, T1.1, T1.2, T1.3, T2.1, T2.2, T3.1, T7.1, T8.1, T8.4 |
| FR-5 | T0.2, T1.1, T1.2, T1.3, T2.1, T2.2, T7.1 |
| FR-6 | T0.1, T4.2, T4.3, T5.1, T5.2, T5.3, T7.1, T7.2, T7.3, T8.3 |
| FR-7 | T4.2, T4.3, T7.1, T8.2 |
| FR-8 | Not implemented (owner decision, OQ-2) |
| FR-9 | T6.1, T6.2, T6.3, T6.4 |
