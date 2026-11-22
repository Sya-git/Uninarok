# TASKS-0009: Unit Display and Camera

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Implementing (approved by the owner, 2026-11-21) |

**How to use this file**
- Start only when SPEC-0008 is implemented and merged into `main` (its catalog facade, entries and editor lookup are what this spec builds on). Work on branch `feature/0009-unit-display`.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0009): T3.2 add the body-driven unit clock` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence (command + result).
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Through the Unity MCP, run tests in groups (BACKLOG: one full run exceeds the tool timeout). Tests marked `NeedsData` need the source data folder or the imported catalog and are ignored with a message when it is absent.
- "No new warnings" is checked in `Logs/Editor.log` after a refresh, not in the console.
- Paths are relative to `Uninarok-client/Assets/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a phase 0 check shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Phase 0 scripts are scratch code: delete them when the task is done.
- Reference documents are updated in the same commit as the code they describe: [composition-reference.md](composition-reference.md) (created in T0.2), `docs/data/SpriteAction.md`, `docs/data/UninarokPosition.md`, sprite-catalog.md §1.
- Evidence images go to `docs/specs/0009-unit-display/evidence/` and are linked from the task's report.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

Reference set (spec §5, OQ-5), used by every check below: novice male; swordsman male with hair style and colour, top and low headgear (item 2220, view 16; item 2218 flu mask, view 8), garment `천사날개` (view id 1), a sword and a shield; knight on peco female; a doram (summoner male); Poring (1002); one 64-action monster (Lunatic, 1063); one town NPC of prontera (Kafra, 117). Ids in [composition-reference.md](composition-reference.md) §7. Also: warp NPC (45), a red plant, a fabre (shadow checks, FR-11).

---

## Phase 0: Prepare

- [x] **T0.1** Add `com.unity.cinemachine` (newest 3.x the registry offers for 6000.7.0b7) and check the plan's assumptions about it. (FR-12)
  Record in plan §1 the version; in §4.7 the `Damper.Damp` signature and constant read from the installed source (expected `remaining × (1 − e^(−4.605 × dt / dampTime))`); in §5 the `CinemachineBrain` execution order and update method.
  *Done when:* the package resolves and the project compiles with no new warnings (Editor.log); the three facts are written in the plan (with a Decisions note if any differs from the plan, and the damp times recomputed).
  *Result (2026-11-22):* 6.7.0 (Cinemachine 3 API, D14); `Damper.Damp` as assumed, damp times unchanged; brain order 100, `LateUpdate`; no `warning CS/UA` in Editor.log.

- [x] **T0.2** Decode the composition tables and write `composition-reference.md`. (FR-2, FR-3, FR-4, FR-5, FR-8, FR-11)
  For each table of plan §1 (`tb_layer_priority`, `spriterobename` `RobeTopLayer`, `*layerdir_*` old and new, `_2dlayer_headtobody`, `ridingspreditinfo` head and body calls, `offsetitempos` with Doram, `transparentitem`, `shadowtable`): file, shape, every column's meaning, counts, Confirmed/Candidate, as sprite-catalog.md does. Also settle, against the reference viewer on the reference set: the default part order per direction (weapon/shield front or back), the idle/sit 3-frame head rule (D9, R3), what a part with fewer frames than the body shows (R2), the job ids drawn as nothing, and pick the open members of the reference set.
  *Done when:* the document exists with every table and rule marked Confirmed or Candidate with the evidence used; the reference set is complete in this file's header; any plan rule found wrong is corrected in the plan with a Decisions note.
  *Result (2026-11-22):* document written; plan rules corrected in D12 and D15 to D20. The reference viewer was not available here: rules that need it are marked Candidate and go to the owner with T8.1.

- [x] **T0.3** Check the offset between network direction and the sprite's front. (FR-6)
  Compare a real capture's unit direction (idle/spawn packet) with what the original client shows at its default camera, for at least two directions.
  *Done when:* `docs/data/UninarokPosition.md` states the offset (0 or n) and the turning sign, with the evidence; plan §4.3 `UnitFacing.Shown` names the constant.
  *Result (2026-11-22):* offset 4, sign −1 (`FrontOffset`). Evidence is the server's `dirx`/`diry` and the client's own `OffsetItemPos.Direction`, not a capture beside the original client (not available here): owner to see it in T6.4/T8.1.

- [x] **T0.4** Create the assemblies and test assemblies, empty. (FR-13, FR-15)
  `Scripts/Units/Uninarok.Units.asmdef`, `Scripts/CameraRig/Uninarok.CameraRig.asmdef`, `Scripts/Units/Dev/Uninarok.Units.Dev.asmdef`, `Scripts/Units/Editor/Uninarok.Units.Editor.asmdef` (editor only), and test assemblies under `Tests/EditMode/Units`, `Tests/EditMode/CameraRig`, `Tests/PlayMode/Units`, with the references of plan §3.
  *Done when:* the project compiles with no new warnings and the test runner lists the (empty) assemblies.
  *Result (2026-11-22):* assemblies compile; no warnings in Editor.log for them. The Dev assembly is excluded from release builds by its define constraint (D21).

## Phase 1: Composition tables import

- [x] **T1.1** Tests, then implementation: `RoLuaReader` captures top-level calls. (FR-4)
  Calls `name(args)` and `a.b(args)` become a list of (name, literal arguments and references); nested table arguments (`{y=-15}`) are values. Skipping of function bodies and control blocks is unchanged.
  *Done when:* `RoLuaReader_Calls_CapturedWithArguments` and the existing reader tests pass; a fixture copied from `ridingspreditinfo.lub` and one from `offsetitempos.lub` (decompiled text) read with the expected call count.
  *Result (2026-11-22):* the 3 `RoLuaReader_Calls_*` tests and the 18 existing reader tests pass (21/21).

- [x] **T1.2** Tests, then implementation: `CompositionTables` asset type and its lookups. (FR-3, FR-4, FR-11)
  Flat sorted arrays as plan §4.1 and T0.2's columns; `ShadowScaleOf`, `PriorityOf`, `TryGetItemOffset` and one `TryGet` per table.
  *Done when:* `CompositionTables_ShadowScaleOf_Default1_Zero_Baby075` and the lookup tests on synthetic data pass; a lookup allocates 0 B (test with `GC.GetAllocatedBytesForCurrentThread`).
  *Result (2026-11-22):* 8 tests pass (`CompositionTablesTests`), lookups allocate 0 B. The layer swaps are keyed without the size (always big, D16) and hold a frame bit mask.

- [x] **T1.3** Tests, then implementation: `RoCompositionTablesBuilder`. (FR-3, FR-4, FR-11)
  Pure, from `RoCatalogTables` and the read chunks; a missing or unreadable table gives one gap and an empty table.
  *Done when:* `RoCompositionTablesBuilder_*` pass on small text fixtures of each table; a missing file gives exactly one warning.
  *Result (2026-11-22):* 7 `RoCompositionTablesBuilder_*` tests pass; a missing file gives one gap. `JOBID` comes from `skillinfoz/jobinheritlist.lub`, `pcJobTbl` from `admin/pcidentity.lub`.

- [x] **T1.4** `RoCompositionTablesConverter` and the **Composition** category in the catalog import menu. (FR-3, FR-4, FR-11)
  Runs also with Bodies or any equipment category (plan §4.1, D1); writes `Art/Imported/catalog/composition.asset`, addressable key `catalog/composition`, through the ledger.
  *Done when:* importing Composition on the real data writes the asset and reports per table the row count and warnings (recorded in composition-reference.md); a second run reports "skipped"; the shadow table has 987 rows with 67 zeros. The owner commits the generated asset.
  *Result (2026-11-22):* import run on the real data: created, then skipped; row counts in composition-reference.md §8; 987 shadow rows with 67 zeros; 29 importer tests pass. `composition.asset`, the shadow sheet, the ledger and the Addressables entry are left uncommitted for the owner.

## Phase 2: Sprite changes

- [x] **T2.1** Tests, then implementation: `SpriteActor` driven mode. (FR-5, FR-14)
  `Driven` and `ShowFrame(animation, direction, frame)`; `SpriteSystem.Tick` skips driven actors; renderers written only when (unique action, frame) changes; body events still fire.
  *Done when:* `SpriteActor_Driven_NotAdvancedBySystem_WritesOnlyOnChange` passes and every existing Sprites test still passes.
  *Result (2026-11-22):* 6 `SpriteActor_Driven_*` tests and every existing Sprites test pass (87/87 with the unit tests); 0 B per ShowFrame.

- [x] **T2.2** `_DepthBias` in `Uninarok/RoSprite` and the unit material. (FR-10)
  Clip-space depth-only bias toward the camera in world units (default 0); `Art/Materials/sprite_unit.mat` with 0.5.
  *Done when:* existing sprites render unchanged (sprite preview evidence image before/after identical); a test scene shows a sprite with its feet below a slope cut at bias 0 and whole at 0.5, at the same screen position and size (two evidence images).
  *Result (2026-11-22):* evidence/t2.2-default-before.png and -after.png are byte-identical; on a 20° slope at 50° tilt bias 0 cuts 11 px of the feet, 0.5 shows them, same x range and top (evidence/t2.2-slope-bias0.png, -bias0.5.png).

## Phase 3: Pure rules

- [x] **T3.1** Tests, then implementation: `UnitFacing`. (FR-6)
  *Done when:* `UnitFacing_CameraSector_Borders` and `UnitFacing_Shown_SouthFrontAtDefault_BackAt180_SideAt90` pass with T0.3's offset.
  *Result (2026-11-22):* `UnitFacingTests` pass (borders, South at 0/22/23/90/180, the client's names).

- [x] **T3.2** Tests, then implementation: `UnitClock` (body drives, play modes, idle/sit head frame). (FR-5, FR-7, FR-8)
  *Done when:* `UnitClock_BodyDrivesFrame_TrailOf150msFollows100msBody`, `_NoDriftAfter10MinutesSimulated`, `_OnceHoldStops`, `_OnceEndReportsOnce`, `_IdleAndSit_FrameIsHeadFacing`, `_HeadFacingIgnoredWhileWalking_ShowsAgainOnIdle` pass.
  *Result (2026-11-22):* the 8 `UnitClock_*` tests pass; the head-frame groups of D19 are covered.

- [x] **T3.3** `PcAnimation`, `MonsterAnimation` and the player-to-unit animation mapping. (FR-7)
  *Done when:* the enums match `docs/data/SpriteAction.md` (which now names the namespace `Uninarok.Units`); a mapping test covers Attack1/2/3 → Attack, Dead → Die, Hurt → Hurt, others → Idle.
  *Result (2026-11-22):* enums and mapping in `UnitAnimations.cs`; 11 `UnitAnimationsTests` pass; `SpriteAction.md` names the namespace.

- [x] **T3.4** Tests, then implementation: `UnitComposer` default order and attachment. (FR-3, FR-4)
  Default order per direction (T0.2), head on the body's anchor, headgears on the head's.
  *Done when:* `UnitComposer_DefaultOrder_PerDirection`, `_HeadAttachesToBodyAnchor`, `_EmptyTables_StillSane` pass; `Compose` allocates 0 B.
  *Result (2026-11-22):* default order, attachment and empty-table tests pass; `Compose` allocates 0 B. Committed together with T3.5 (one file).

- [x] **T3.5** Tests, then implementation: `UnitComposer` table rules. (FR-3, FR-4)
  Headgear priorities (per direction, riding), `RobeTopLayer`, frame layer swaps, head-to-body rows, item offsets (Doram), riding/body edits, transparency.
  *Done when:* `_RobeTopLayerAboveBody`, `_LayerSwapOnlyListedFrames`, `_HeadgearPriority_PerDirection_IgnoreRiding`, `_ItemOffset_DoramVariant` and the riding-edit test pass on synthetic tables.
  *Result (2026-11-22):* the 7 table-rule tests pass on synthetic tables (65/65 in `Uninarok.Units.Tests`).

## Phase 4: Loading

- [x] **T4.1** `UnitLook`, `IUnitAssetSource`, `AssetDatabaseUnitAssetSource` with reference counts. (FR-1, FR-2)
  *Done when:* a test acquires and releases the same reference from two users and the count returns to zero; `UnitLook.ForJob` gives Player for a player-class job id and Unit otherwise.
  *Result (2026-11-22):* `AssetSource_CountsTwoUsers_BackToZero` and `UnitLook_ForJob_PlayerClassOrUnit` pass. Committed together with T4.2.

- [x] **T4.2** Tests, then implementation: `UnitLoader` resolution and `LoadReport`. (FR-1, FR-2)
  *Done when:* `UnitLoader_ResolvesReferenceSet`, `_MissingHeadgear_ReportsReason`, `_PlayerJobOnNpc_ShowsAsPlayer`, `_HiddenJob_ShowsNothing` pass on the imported catalog (`NeedsData`).
  *Result (2026-11-22):* the 12 `UnitLoaderTests` pass on the imported catalog (reference set, missing headgear, player job on an NPC, hidden job, shadow scales, superseded loads).

- [x] **T4.3** `AddressablesUnitAssetSource`, ref-counted. (FR-14)
  *Done when:* a PlayMode test loads, changes and destroys looks 1,000 times and every handle count returns to zero (R7).
  *Result (2026-11-22):* `LoadChangeDestroy_1000Looks_EveryHandleCountReturnsToZero` passes in play mode (1.6 s).

## Phase 5: Unit in the scene

- [x] **T5.1** `UnitView`, `UnitSystem`, its driver; full-rotation billboard. (FR-5, FR-6, FR-7)
  *Done when:* a PlayMode test spawns the reference set and plays each animation of FR-7; the pivot rotation equals the camera rotation after a turn and a tilt and is not written when the camera did not move; `UnitView_HeadAndBodyTurnEveryFrame_Doridori` passes.
  *Result (2026-11-22):* play tests pass: every animation on the reference set, pivot rotation, doridori, parts in step (8/8 in `Uninarok.Units.PlayTests`). The shadow (T5.4) is in the same view code.

- [x] **T5.2** Live changes: `SetLook` diff. (FR-9)
  *Done when:* `UnitView_SetLook_KeepsFrameAndPosition` passes (weapon and hair colour swapped mid-walk keep the frame; a job change replaces the look in place); old assets are released.
  *Result (2026-11-22):* `UnitView_SetLook_KeepsFrameAndPosition` and `_ReleasesTheOldAssets` pass.

- [x] **T5.3** Ground placement: `PlaceOn` and the unit material's depth bias. (FR-10)
  *Done when:* `UnitView_FeetOnGround_SlopeOfPrtFild08_MinMaxTilt` passes; evidence images of a poring and a swordsman on a slope at 50° and 65° tilt.
  *Result (2026-11-22):* `UnitView_FeetOnGround_SlopeOfPrtFild08_MinMaxTilt` passes (no pixel cut at 50° and 65°); evidence/t5.3-*.png.

- [x] **T5.4** `UnitShadow`. (FR-11)
  *Done when:* poring and novice show a full shadow, fabre a half one, warp NPC and red plant none (PlayMode test on the scale; one evidence image).
  *Result (2026-11-22):* `UnitShadow_ScalesOfTheAcceptance` passes; evidence/t5.4-shadows.png (poring, novice, fabre, warp NPC, red plant). Alpha 0.45 is a Candidate: the static sprite carries no tint.

## Phase 6: Camera

- [x] **T6.1** Tests, then implementation: `RoCameraSettings`, `RoCamera` and the Cinemachine rig prefab. (FR-12)
  Perspective 15°, `OrbitalFollow` sphere, limits 23–40 u and 50°–65°, damping 0.384 s / 0.768 s (or T0.1's recomputed values).
  *Done when:* `RoCamera_Clamps_Eases_Resets`, `RoCamera_DampingMatchesRoBrowser_At30_60_144Fps`, `RoCamera_TiltOrbitsVertically_CameraHeightGrowsWithTilt_WithinLimits` pass.
  *Result (2026-11-22):* `RoCameraTests` pass (clamps, easing, resets, orbit height, free turn, follow without swinging, 0 B). Damping: D22, D23. The prefab comes with T6.2 (it holds the input component).

- [x] **T6.2** `RoCameraInput` and the `Camera` action map. (FR-12)
  Right-drag turn, shift tilt, wheel and ctrl zoom, double right-click resets with modifiers.
  *Done when:* `RoCamera_DoubleClickVariants` and an input test per control pass (Input System test fixture).
  *Result (2026-11-22):* one input test per control and `RoCamera_DoubleClickVariants` pass (7, Input System test fixture); `RoCameraRig.prefab` and `RoCameraSettings.asset` saved.

- [x] **T6.3** `UnitSystem` reads the sector from the brain's output camera. (FR-6)
  *Done when:* `UnitSystem_AllPartsSwitchSectorSameFrame` passes, and a test shows the sector read in a frame equals the one of the rendered camera that frame.
  *Result (2026-11-22):* `UnitSystem_AllPartsSwitchSectorSameFrame` (T5.1's file) and `UnitSystem_ReadsTheSectorOfTheRenderedCamera` pass over 240 frames of a turn.

- [ ] **T6.4** Owner: measure the camera against the original client, side by side. (FR-12)
  Same window size, prontera: default view on entering, nearest and farthest zoom, lowest and highest tilt, wheel step. Fill `Settings/Units/RoCameraSettings.asset`.
  *Done when:* screenshot pairs for each case are in `evidence/` and show the same map area; the settings hold the measured values; plan §4.7 records them (a Decisions note if the limits differ from `viewpointtable.txt`).
  *Open (2026-11-22):* owner task. Until then `RoCameraSettings.asset` holds the research values (default 40 u, 50°; wheel 2.5 u) and FR-12's side-by-side acceptance is not checked.

- [x] **T6.5** Tests, then implementation: `RoCamera.SnapToTarget` (plan §4.7, D13). (FR-12)
  Choose `OnTargetObjectWarped` or `ForceCameraPosition` from the installed Cinemachine and record it in D13.
  *Done when:* `RoCamera_SnapToTarget_FirstFrameAtTarget_KeepsTurnTiltZoom` passes (target moved 200 u, snap, the next rendered frame is centred on it with unchanged yaw, pitch and distance).
  *Result (2026-11-22):* `RoCamera_SnapToTarget_FirstFrameAtTarget_KeepsTurnTiltZoom` passes with the real brain; `OnTargetObjectWarped` chosen (D22).

## Phase 7: Tools

- [x] **T7.1** Entity Sandbox scene and panel. (FR-13)
  `Scenes/Dev/EntitySandbox.unity` (prontera additive), spawn by look at a clicked cell, select, facing, head facing, animation, play mode, live parts, camera follow, FR-6 turn buttons, doridori and bangbang buttons.
  *Done when:* `EntitySandbox_Loads_SpawnsReferenceSet_OnProntera` passes; FR-6, FR-10, FR-11 and FR-12 are checked by hand in the sandbox with evidence images.
  *Result (2026-11-22):* `EntitySandbox_Loads_SpawnsReferenceSet_OnProntera` passes. Checked by driving the sandbox in play mode through the Unity MCP: sector 0 at 22° and 1 at 23°, side at 90°, back at 180° (evidence/t7.1-turn*.png), units uncut at 50° and 65° (t7.1-nearest-*.png), shadows, zoom 23 to 40 u (t7.1-farthest-tilt50.png). The mouse in a hand and the IMGUI clicks are the owner's to try.

- [ ] **T7.2** Stress mode and its readout; first measurement. (FR-13, FR-14)
  100 players + 200 monsters, seeded, mixed animations and turns; tick time (mean, max) and GC per frame on screen; profiler markers.
  *Done when:* the stress mode starts with one action; `UnitSystem_ZeroGcAllocPerFrame_300Units` passes; the first timing on the owner's PC is recorded here. If it is over 2 ms, stop and update the plan (R4) before going on.

- [ ] **T7.3** `RoUnitComposerWindow`: inputs, playback and preview. (FR-15)
  Unit or player look (view id or item id per equipment), animation, play mode, facing, head facing, play/pause/step/restart/scrub/speed, preview camera with yaw and tilt.
  *Done when:* a swordsman with a full set plays Attack3; stepping advances one body frame; turning the preview 180° shows the back of a unit facing South.

- [ ] **T7.4** Composer: 8-direction grid, parts list and evidence. (FR-15)
  *Done when:* the 8-direction grid shows the reference swordsman; the parts list shows body and weapon frame numbers in lock-step while stepping; hiding the garment removes only it; an unknown headgear id is listed as missing with its reason; "Save image" writes a PNG.

## Phase 8: Close

- [ ] **T8.1** Verify the reference set against the reference viewer in the composer. (FR-1, FR-2, FR-3, FR-4, FR-5, FR-8; success criteria)
  8 directions and 4 camera turns per member.
  *Done when:* evidence images for every member are in `evidence/`, each matches the reference viewer in parts, order, offsets and timing; any mismatch is fixed in its table task or recorded in BACKLOG with the owner's agreement.

- [ ] **T8.2** Final budget and release check. (FR-13, FR-14)
  *Done when:* the stress mode on the owner's PC shows a unit update of 2 ms or less and 0 B GC per frame (profiler capture in `evidence/`); a release build contains no Entity Sandbox scene and no `Uninarok.Units.Dev` type.

- [ ] **T8.3** Close the spec. (all)
  *Done when:* spec success criteria checked and reported; sprite-catalog.md §1 points the composition rows to SPEC-0009; the two BACKLOG items of plan §7 are removed (closed) and leftovers added; spec, plan and tasks status `Implemented`.
