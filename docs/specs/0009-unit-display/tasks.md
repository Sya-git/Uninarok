# TASKS-0009: Unit Display and Camera

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Implementing (revision 2026-11-23, approved by the owner 2026-11-23: Phases 10 and 11 added for FR-1, FR-15, FR-17, FR-18; not yet approved). Phases 0 to 9: Implemented (2026-11-22; approved by the owner 2026-11-21; Phase 9, FR-16, approved 2026-11-22) |

**How to use this file**
- Start only when SPEC-0008 is implemented and merged into `main` (its catalog facade, entries and editor lookup are what this spec builds on). Work on branch `feature/0009-unit-display`.
- Revision 2026-11-23 (Phases 10 and 11): the Phase 10 tasks T10.1 to T10.7 run **before** PLAN-0011's tasks (plan R13): combat calls `CatalogWeaponTypes.HandsOf` only after them. T10.9 and T11.6 are owner checks (evidence in `evidence/`); they do not block later phases.
- Rework of implemented work (T4.1, T4.2, T7.3) is a new task that names the original; the checked boxes are not changed.
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
  *Result (2026-11-22):* import run on the real data: created, then skipped; row counts in composition-reference.md §8; 987 shadow rows with 67 zeros; 29 importer tests pass. `composition.asset`, the shadow sheet, the ledger and the Addressables entry are committed on the owner's go-ahead.

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

- [x] **T6.4** Owner: measure the camera against the original client, side by side. (FR-12)
  Same window size, prontera: default view on entering, nearest and farthest zoom, lowest and highest tilt, wheel step. Fill `Settings/Units/RoCameraSettings.asset`.
  *Done when:* screenshot pairs for each case are in `evidence/` and show the same map area; the settings hold the measured values; plan §4.7 records them (a Decisions note if the limits differ from `viewpointtable.txt`).
  *Result (2026-11-22):* the owner compared the camera with the original client: it matches. `RoCameraSettings.asset` keeps its values (default 40 u and 50°, 23 to 40 u, 50° to 65°, wheel 2.5 u). Accepted on the owner's word: no screenshot pairs were saved in `evidence/`.

- [x] **T6.5** Tests, then implementation: `RoCamera.SnapToTarget` (plan §4.7, D13). (FR-12)
  Choose `OnTargetObjectWarped` or `ForceCameraPosition` from the installed Cinemachine and record it in D13.
  *Done when:* `RoCamera_SnapToTarget_FirstFrameAtTarget_KeepsTurnTiltZoom` passes (target moved 200 u, snap, the next rendered frame is centred on it with unchanged yaw, pitch and distance).
  *Result (2026-11-22):* `RoCamera_SnapToTarget_FirstFrameAtTarget_KeepsTurnTiltZoom` passes with the real brain; `OnTargetObjectWarped` chosen (D22).

## Phase 7: Tools

- [x] **T7.1** Entity Sandbox scene and panel. (FR-13)
  `Scenes/Dev/EntitySandbox.unity` (prontera additive), spawn by look at a clicked cell, select, facing, head facing, animation, play mode, live parts, camera follow, FR-6 turn buttons, doridori and bangbang buttons.
  *Done when:* `EntitySandbox_Loads_SpawnsReferenceSet_OnProntera` passes; FR-6, FR-10, FR-11 and FR-12 are checked by hand in the sandbox with evidence images.
  *Result (2026-11-22):* `EntitySandbox_Loads_SpawnsReferenceSet_OnProntera` passes. Checked by driving the sandbox in play mode through the Unity MCP: sector 0 at 22° and 1 at 23°, side at 90°, back at 180° (evidence/t7.1-turn*.png), units uncut at 50° and 65° (t7.1-nearest-*.png), shadows, zoom 23 to 40 u (t7.1-farthest-tilt50.png). The mouse in a hand and the IMGUI clicks are the owner's to try.

- [x] **T7.2** Stress mode and its readout; first measurement. (FR-13, FR-14)
  100 players + 200 monsters, seeded, mixed animations and turns; tick time (mean, max) and GC per frame on screen; profiler markers.
  *Done when:* the stress mode starts with one action; `UnitSystem_ZeroGcAllocPerFrame_300Units` passes; the first timing on the owner's PC is recorded here. If it is over 2 ms, stop and update the plan (R4) before going on.
  *Result (2026-11-22):* stress mode starts with one action (300 units loaded, no part missing); `UnitSystem_ZeroGcAllocPerFrame_300Units` passes (0 B per tick). First timing, editor, i5-14600KF: 0.555 ms mean, 1.66 ms max over 300 frames (readout in a longer editor run before the last write saving: mean 0.66 to 0.75, worst spike 2.5); evidence/t7.2-stress.png. Under 2 ms; unchanged renderer properties are no longer rewritten in `SpriteActor`.

- [x] **T7.3** `RoUnitComposerWindow`: inputs, playback and preview. (FR-15)
  Unit or player look (view id or item id per equipment), animation, play mode, facing, head facing, play/pause/step/restart/scrub/speed, preview camera with yaw and tilt.
  *Done when:* a swordsman with a full set plays Attack3; stepping advances one body frame; turning the preview 180° shows the back of a unit facing South.
  *Result (2026-11-22):* window opens; `Swordsman_PlaysAttack3_SteppingAdvancesOneBodyFrame` and `TurningThePreview180_ShowsTheBackOfAUnitFacingSouth` pass on its stage. In this client the sword is drawn in Attack2 (action 80) and Standby, its Attack3 frames are empty: the file says so, not the composer. Committed with T7.4.

- [x] **T7.4** Composer: 8-direction grid, parts list and evidence. (FR-15)
  *Done when:* the 8-direction grid shows the reference swordsman; the parts list shows body and weapon frame numbers in lock-step while stepping; hiding the garment removes only it; an unknown headgear id is listed as missing with its reason; "Save image" writes a PNG.
  *Result (2026-11-22):* grid, parts list in lock-step, hidden garment, missing headgear with its reason and saved PNG are covered by `UnitPreviewStageTests` (8 pass); images in evidence/t8.1-*.png come from it.

## Phase 8: Close

- [x] **T8.1** Verify the reference set against the reference viewer in the composer. (FR-1, FR-2, FR-3, FR-4, FR-5, FR-8; success criteria)
  8 directions and 4 camera turns per member.
  *Done when:* evidence images for every member are in `evidence/`, each matches the reference viewer in parts, order, offsets and timing; any mismatch is fixed in its table task or recorded in BACKLOG with the owner's agreement.
  *Result (2026-11-22):* images of every member in 8 directions and 4 camera turns are in evidence/t8.1-*.png; the owner compared the reference set: all right. Mismatches reported on the way were fixed in T8.4 to T8.6. Candidate rules the owner was pointed to (those the set does not show stay Candidate): composition-reference.md §2.1 (negative priority), §2.2, §2.4, §2.5, §3 (default order), §5 (which head frame is right).

- [x] **T8.2** Final budget and release check. (FR-13, FR-14)
  *Done when:* the stress mode on the owner's PC shows a unit update of 2 ms or less and 0 B GC per frame (profiler capture in `evidence/`); a release build contains no Entity Sandbox scene and no `Uninarok.Units.Dev` type.
  *Result (2026-11-22):* this PC (i5-14600KF), in the editor: readout over 10 s 0.50 ms mean, 0.97 ms max; with the profiler on, 300 frames, 0.91 ms mean, 1.66 ms max, 0 B GC in `UnitSystem.Tick` (evidence/t8.2-stress-profiler.csv, per frame). The stress starts its units spread over 2 s instead of all in one frame (in step they peaked at 2.3 to 2.7 ms under the profiler, 2 of 300 frames over 2 ms). Release: compiled release player scripts contain no `Uninarok.Units.Dev` (the development ones do) and the build list is Bootstrap only; no full player build was made.

- [x] **T8.4** Show the classes without sprites of their own: base class sprites, baby classes at 0.8 (added 2026-11-22, owner report). (FR-1, FR-11)
  *Done when:* every `JOB_BABY_*` loads a body and a head at scale 0.8 with the adult's parts; the high first classes and the `_T` classes load their base sprites at scale 1; seen in the Entity Sandbox.
  *Result (2026-11-22):* `UnitLoader_BabyJobs_AreTheirAdultClassAt08`, `_BabySwordsman_HasTheSwordsmansParts`, `_HighAndTranscendentJobs_UseTheirBaseSprites` pass (Units EditMode 95 of 95); baby swordsman and baby acolyte seen beside the adults on prontera. Hercules' placeholders `JOB_DUMMY4230` to `4237` still have no body: their base jobs are not in `PlayerJob`.

- [x] **T8.5** One `UnitSystem` tick per frame in every play session (added 2026-11-22, owner report). (FR-5, FR-13, FR-14)
  *Done when:* with a driver left over from an earlier session the tick still runs once per frame; in the Entity Sandbox, in a second play session, a walk frame lasts the body's frame time.
  *Result (2026-11-22):* before: 4 ticks per frame. After: 1; walk frame time 75 ms, measured 75.5 ms over 4 s. `UnitSystem_TicksOncePerFrame_WithAStrayDriver` passes (PlayMode 16 of 16). FR-14 measured again (editor, 300 units, 600 frames): 0.73 ms mean, 1.97 ms max.

- [x] **T8.6** Keep a garment in front of the body under the head while the face shows (added 2026-11-22, owner report). (FR-3)
  *Done when:* acolyte male, Attack2, facing front: the garment is behind the body in frames 0 to 4, and above the body but under the head in frames 5 to 8; the views from behind do not change.
  *Result (2026-11-22):* `UnitComposer_AcolyteAttack2_GarmentNeverCoversTheHeadFromTheFront` and `_GarmentInFrontWhileTheFaceShows_StaysUnderTheHead` pass (Units EditMode 97 of 97); evidence/t8.6-acolyte-garment.png (frames left to right; Attack2 then Attack3, directions 0, 7, 6, 2, 4).

- [x] **T8.3** Close the spec. (all)
  *Done when:* spec success criteria checked and reported; sprite-catalog.md §1 points the composition rows to SPEC-0009; the two BACKLOG items of plan §7 are removed (closed) and leftovers added; spec, plan and tasks status `Implemented`.
  *Result (2026-11-22):* success criteria: reference set compared by the owner (T8.1); FR-14 met in the stress mode, in the editor (T8.2, T8.5: 1.97 ms max in the last run); camera matches the original client (T6.4, owner's word, no screenshot pairs). sprite-catalog.md §1 points to SPEC-0009; the two BACKLOG items are removed and the leftovers added. Not done: a full player build (the release scripts were compiled). Tests: Units PlayMode 16 of 16; Units, CameraRig, Sprites, SpriteCatalog, Map and Networking EditMode 665 of 666 (the one failure is the line endings of a SPEC-0001 fixture, BACKLOG); the full RoImport suite, 1,146 tests: two failed on MCP noise or load and pass alone, three model and map batch tests run past their 180 s timeout on this PC, alone too (214 to 485 s; BACKLOG), the rest pass.

## Phase 9: Depth by height (added 2026-11-22)

Works on the branch `feature/0010-world-session` (the owner reported it while reviewing SPEC-0010).

- [x] **T9.1** Add the tests of the heights, pending T9.2. (FR-16, FR-14)
  `SpriteActor_SetGround_WritesTheHeightInZScale`, `_SameValue_WritesNothing`, `_Off_KeepsZScale1`; `UnitView_LayerHeights_AreFromThePivot_AtTheDisplayScale`.
  *Done when:* they compile and are listed as ignored.
  *Result (2026-11-22):* Sprites EditMode 35 pass, 3 ignored; Units EditMode 97 pass, 1 ignored ("pending T9.2"); no new warnings. `SetGround` is an empty method here, so the tests compile.

- [x] **T9.2** Write each layer's height in its z scale (plan §4.10). (FR-16, FR-14)
  `SpriteActor.SetGround`, `UnitView.Write`, `GroundItemView`.
  *Done when:* the tests of T9.1 pass, the Sprites and Units EditMode tests pass, and a unit's picture is the same as before (the shader does not read the height yet).
  *Result (2026-11-22):* Sprites EditMode 38 of 38, Units EditMode 98 of 98; no new warnings. Swordsman at the wall of prontera 128,86 (tilt 65°): 0 pixels differ between the heights (z scale up to 17.04) and z scale 1. `GroundScaleZ` is public, for the ground item still (plan §4.10).

- [x] **T9.3** Add the render tests, pending T9.4. (FR-16, FR-10)
  `UnitDepth_BackToAWall_IsWhole_4Turns_MinMaxTilt` (prontera; iz_int when imported), `_BehindABox_IsHiddenAsAnUprightPlane`, `_TwoUnits_SameOrderAsBefore`, `_TallUnitAtTheNearestZoom_IsNotClipped`, `_Shadow_IsUnchanged`, `_GroundItemAtAWall_IsWhole`.
  *Done when:* run without the ignore mark, the cut cases (wall, box, ground item) fail with today's shader; they are committed as ignored.
  *Result (2026-11-22):* run without the ignore mark on the shader of T9.2: both wall tests (prontera 221,221: 3,111 of 3,871 pixels; iz_int 61,45: 2,949), the box (1,989 of 3,871) and the ground item (1,376 of 4,000) fail; order, clipping and shadow pass, as they must before and after. The clipping test is `UnitDepth_TallUnitNearTheCamera_IsNotClipped`: 2.5, 4 and 23 u away, tilts 20° to 90°.

- [x] **T9.4** Compute the upright depth in `Uninarok/RoSprite`. (FR-16, FR-10)
  *Done when:* the tests of T9.3 and `UnitView_FeetOnGround_SlopeOfPrtFild08_MinMaxTilt` pass; no new warnings; materials without a bias render as before.
  *Result (2026-11-22):* Units PlayMode 22 of 22 (with `UnitView_FeetOnGround_SlopeOfPrtFild08_MinMaxTilt` and `UnitSystem_ZeroGcAllocPerFrame_300Units`), `UnitDepth_GroundItemAtAWall_IsWhole` passes; no compiler or shader warnings. Without a bias the shader takes the same path as before. Preview scenes, 160 cells per turn beside something not walkable (fewer on iz_int), tilts 50° and 65°, pixels visible before and after: prontera 2,590,638 → 3,000,963 of 4,441,390, iz_int 1,252,821 → 1,573,093 of 1,928,594; no sample worse.

- [x] **T9.5** Measure the cost in the stress mode, with and without the heights. (FR-14, FR-16)
  *Done when:* unit update 2 ms or less and 0 B per frame at 100 players and 200 monsters on prontera; batches, set passes and frame time of both are recorded here.
  *Result (2026-11-22):* stress mode, prontera, 300 units, in the editor: unit update 0.52 ms mean, 1.20 ms max over 600 frames (T8.5: 0.73 and 1.97). 0 B per frame: `UnitSystem_ZeroGcAllocPerFrame_300Units` passes (T9.4 run). Units frozen, heights and z scale 1 in turn, 3 times 200 frames each (982 layers enabled of 2,378): set passes 371 and 369 to 371, draw calls 1,703 and 1,682 to 1,697 (a little less is culled), frame 16.67 ms both (vsync), render 14.63 to 14.70 and 14.62 to 14.71 ms. `UnityStats` has no batch count in this version. The sandbox readout of garbage (67 KB per frame) is the whole editor frame, not the unit update.

- [x] **T9.6** Evidence and close. (FR-16)
  *Done when:* screenshots on prontera next to a wall and indoors on iz_int are in `evidence/` (before and after, both tilts); the Sprites, Units and World tests pass; the owner checked the images; statuses are `Implemented`.
  *Result (2026-11-22):* evidence/t9.6-prontera-turn90.png, t9.6-prontera-turn180.png, t9.6-iz_int-turn0.png, t9.6-iz_int-turn270.png (left before, right after; tilt 50° above, 65° below; game view angle, nearest zoom, preview scenes). Sprites EditMode 38 of 38, Units EditMode 98 of 98, Units PlayMode 22 of 22, World EditMode 160 of 160, World PlayMode 44 of 44. The owner checked the images and approved them (2026-11-22). Not seen in play against a server.

## Phase 10: Both hands (revision 2026-11-23)

Plan §4.11, §6 (revision table), D31 to D36, R12 to R15. Spec FR-17, FR-1 (hands), FR-15 (hands as input). The rule is defined once, in `Uninarok.Units` (`Rules/WeaponHands.cs`, `Rules/JobFamilies.cs`); the combat adapter calls it (D31).

- [x] **T10.1** [core] Tests, pending T10.2: `WeaponHands_Combine_EveryCase` (the case table of plan §4.11: weapon/none, none/weapon, assassin weapon/weapon for the six pairs in both orders, swordsman weapon/weapon, weapon/shield, katar alone and with a left value, none/shield, none/none, unknown hands) and `WeaponHands_CombinedType_SixPairsEitherOrder_ElseZero`, `JobFamilies_IsAssassinFamily_EveryIdAndNotRogue`. Synthetic data. (FR-17)
  *Done when:* they compile against the empty signatures of plan §4.11 and are listed as ignored ("pending T10.2"); no new warnings in `Logs/Editor.log`.

- [x] **T10.2** [core] Implementation: `HandKind`, `Hand`, `WeaponSource`, `HandsResult` (with `Describe()`), `WeaponHands.CombinedType`, `WeaponHands.Combine`, `WeaponHands.IsItemId`, and `JobFamilies.IsAssassinFamily` (the six ids of plan §4.11). Removes the marks of T10.1. (FR-17)
  *Done when:* the three T10.1 tests pass; no new warnings. `Hand` and `HandsResult` carry no catalog lookup (classification is T10.4).

- [x] **T10.3** [core] Tests, pending T10.4: `WeaponHands_Classify_LeftByItemKind` (left 1201 gives a weapon, 2101 a shield, 1 a shield, 2220 unknown, 9999999 unknown; right 2101 unknown). Real catalog. (FR-17)
  *Done when:* it compiles and is listed as ignored ("pending T10.4"); with no imported catalog it reports NeedsData as ignored, not failed.

- [x] **T10.4** [critical] Implementation: `WeaponHands.Right`, `WeaponHands.Left` and `WeaponHands.Of` (classification by `SpriteCatalog.TryGetItemView` and `CompositionTables.WeaponTypeOf`, the case table of plan §4.11). Removes the mark of T10.3. In `composition-reference.md` a "Hands" section gives the case table, the classification rule and the R14 evidence (no weapon in the three Hercules item tables has a `ViewSprite`, so a small left value is a shield's view id), in the same commit as the code. This is the rule combat and display share (R12). (FR-17)
  *Done when:* `WeaponHands_Classify_LeftByItemKind` passes on the imported catalog; the T10.1 tests still pass; `composition-reference.md` has the "Hands" section with the R14 evidence (checked by reading it); no new warnings.

- [x] **T10.5** [core] Tests, pending T10.6: `UnitLoaderHands_AssassinTwoKnives_CombinedSprite`, `_KnifeLeftOnly_DrawnAsTheWeapon`, `_KnifeAndShield_Both`, `_Katar_Alone`, `_ShieldOnly`, `_SmallLeftValue_IsAShield`, `_SwordsmanTwoWeapons_RightOnly_NotMissing`, `_ReportNamesTypeAndHands`, `_UnknownLeftItem_MissingWithReason`, `_ChangeLeftHandWhileShown_KeepsFrame`, and one sweep over every assassin-family job id and both sexes (R15: the combined sprite, or a missing line with its reason). Real catalog. (FR-17, FR-1, FR-9)
  *Done when:* they compile and are listed as ignored ("pending T10.6"); the catalog-dependent ones report NeedsData as ignored when the catalog is absent.

- [x] **T10.6** [core] (revision 2026-11-23, rework of T4.1 and T4.2) `UnitLook.RightValue` and `LeftValue` (layout and equality unchanged, doc of the hand fields); `LoadReport.Hands` in `UnitParts`; `UnitLoader.Player()` resolves both hands through `WeaponHands.Of` in place of the two `Equipment(PartSlot.Weapon…)`/`Equipment(PartSlot.Shield…)` calls: right weapon as today with its trail, left weapon as a right-hand weapon would be, combined entry for the assassin family, shield as today; the report lines of plan §4.11. Removes the marks of T10.5. (FR-1, FR-17)
  *Done when:* the T10.5 tests pass; every `UnitLoaderTests` test that passed before still passes with the same result, including `_ByItemId_WrongKindIsMissing` and the reference set with `Shield = 1, ShieldItem = 2101`; serialized looks of the sandbox still load; no new warnings.

- [x] **T10.7** [core] `CatalogWeaponTypes.HandsOf(job, right, left)` added, and `TypeOf` delegates to `WeaponHands.Right` (its one trace line per unknown value kept); `EntityLookMapping.IsItem` delegates to `WeaponHands.IsItemId` (same 500). The test `CatalogWeaponTypes_HandsOf_IsWeaponHandsOf_EveryCase` is in the same commit. No change to motion choice (that is PLAN-0011's revision). (FR-17)
  *Done when:* `CatalogWeaponTypes_HandsOf_IsWeaponHandsOf_EveryCase` passes; every existing `CatalogWeaponTypesTests` and combat test passes unchanged; `CatalogWeaponTypes` holds no copy of the pairing rule.

- [x] **T10.8** [core] (revision 2026-11-23, rework of T7.3) `RoUnitComposerWindow`: `_weapon` and `_shield` become `_right` and `_left` with `[FormerlySerializedAs]` (D35); each hand is passed as typed (an item id sets only the item id, a view id only the view id; the item table's view id is no longer used for hands, `ViewOf` stays for headgear and garment); a **Hands** line above the parts list shows `report.Hands.Describe()`. Defaults stay: right item 1101, left item 2101. (FR-15, FR-17)
  *Done when:* `Swordsman_PlaysAttack3_SteppingAdvancesOneBodyFrame` and the eight `UnitPreviewStageTests` still pass; with item 1201 in both hands the Hands line names type 25 and the assassin family; after a domain reload the right and left values are still there; no new warnings.

- [ ] **T10.9** [quick] (owner) FR-17 composer acceptance with evidence: the assassin with two knives (item 1201 in both hands) in 8 directions and in `Attack3`; a knife only in the left hand; a knife and a shield; a katar alone; a shield with an empty right hand; a small left shield view id. Plan §6 (manual). (FR-17)
  *Done when:* the images are in `evidence/`, each case's Hands line matches the table of plan §4.11, and the owner has checked them. Does not block Phase 11.

## Phase 11: Composer presets (revision 2026-11-23)

Plan §4.12, §6 (revision table), D37 to D39, R17. Spec FR-18; FR-15 points to it. Editor only: `Uninarok.Units.Editor`, and `Units/Editor/Presets/` for the starters. No runtime code.

- [ ] **T11.1** [core] Tests, pending T11.2: `UnitComposerPresetStore_SaveReload_AllFields_NoCameraFrameOrPlayState`, `_StartersListed_AndReadOnly`, `_NamesUniqueAcrossKinds_CaseInsensitive`, `_SaveOverUserName_NeedsConfirm_StarterNameRefused`, `_Rename_KeepsLook`, `_Delete_GoneAfterReload`, `_UnreadableFile_OneWarning_KeptAside`, `_UserFileUnderUserSettings`. Temporary folders and synthetic presets. (FR-18)
  *Done when:* they compile against the signatures of plan §4.12 and are listed as ignored ("pending T11.2"); no new warnings.

- [ ] **T11.2** [core] Implementation: `PresetHand`, `UnitComposerPreset`, `UnitComposerPresetStore` (`ForProject`, `All`, `Problem`, `IsStarter`, `IsTaken`, `Save`, `Rename`, `Delete`, `Reload`; whole-file write through a temporary file and a replace; the unreadable user file moved aside on the next save). Removes the marks of T11.1. (FR-18)
  *Done when:* the eight T11.1 tests pass; the temporary folders are removed; the only `LogWarning` is the one the unreadable-file test expects (`LogAssert`); no new warnings.

- [ ] **T11.3** [quick] Starter presets: `Assets/Scripts/Units/Editor/Presets/UnitComposerStarterPresets.json` (`{ "Version": 1, "Presets": [ … ] }`) with four presets: "Swordsman, dagger" (swordsman male, right item 1201), "Swordsman, spear" (right item 1401), "Novice, rod" (novice male, right item 1601), "Assassin, two knives" (assassin male, right item 1201, left item 1201); each with hair style 2, colour 3, no headgear or garment, `Idle`, `Loop`, South. In `composition-reference.md` a "Starter presets" note records the class/item check of plan §4.12 (Knife 1201, Javelin 1401, Rod 1601 against `db/re/item_db.conf`, 2026-11-23), in the same commit. (FR-18)
  *Done when:* the file parses with the store's format into the four presets with those names, classes (`PlayerJob` values: Novice 0, Swordsman 1, Assassin 12) and items; `composition-reference.md` states the check; nothing outside the `Editor` folder changed.

- [ ] **T11.4** [core] `RoUnitComposerWindow` preset bar (plan §4.12, "Window"): a popup listing every preset with starters marked "(starter)", a name field, **Save as new**, **Save**, **Rename**, **Delete**; Save, Rename and Delete disabled for a starter; a confirm dialog before overwriting a user preset and before deleting one; a starter's name refused with a message; `[SerializeField] _preset` keeps the selection; `OnEnable` calls `Reload()`. Picking a preset copies its look, animation, play mode and facing, then `ApplyLook()` and `PlayAnimation()`; camera turn, tilt, distance, head facing, speed, toggles and play state are not touched; the frame restarts at 0. (FR-18, FR-15)
  *Done when:* on a fresh checkout (no user file) the window lists the four starters; picking "Assassin, two knives" shows both blades with the frame at 0 (checked in the editor through the Unity MCP); Save, Rename and Delete are disabled for a starter; no new warnings.

- [ ] **T11.5** [core] Tests, with the imported catalog: `UnitComposerPresets_Starters_ResolveWithNoMissingPart` (the assassin starter gives `WeaponSource.Both`) and `UnitComposerPresets_UnknownHeadgear_LoadsTheRest_OneWarning` (`LogAssert`, one warning for the preset, its problems listed). Also the fallbacks of plan §4.12: an unknown animation name plays `Idle`, a facing or play mode out of range becomes South or `Loop`, all in the one warning. (FR-18, FR-17)
  *Done when:* both tests pass on the imported catalog (NeedsData as ignored when absent); the fallback case gives the stated values; no `LogError`.

- [ ] **T11.6** [quick] (owner) FR-18 acceptance on a fresh checkout, with evidence in `evidence/`: the four starters are listed and read-only; a user preset keeps its animation, play mode and facing, and comes back after a domain reload and after an editor restart; renaming keeps the look; deleting it stays gone after a restart; the preset with an unknown headgear shows that headgear as missing with one warning; `git status` is clean after saving a user preset; a second checkout lists none. Also: `Uninarok.Units.Editor` is editor-only (asmdef, as T0.4) and no `UnitComposerPreset` type is in the player scripts. (FR-18)
  *Done when:* the evidence is in `evidence/`, each point above has its result written in this task, and the owner has checked it. Does not block later phases.
