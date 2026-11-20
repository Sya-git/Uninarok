# TASKS-0007: SPR, ACT and PAL Sprite Import (core)

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved (by the owner, 2026-11-19) |

**How to use this file**
- Start only when TASKS-0002 phases 0 to 6 are done (paths, source, writer, ledger, context, batch, menu, window). This spec does not depend on 0003 to 0006.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0007): T4.2 implement RoActCompiler` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent.
- Paths are relative to `Uninarok-client/Assets/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a phase 0 check shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Phase 0 scripts are scratch code: delete them when the task is done. Production code never parses SPR, ACT or PAL bytes (PLAN-0002 §1).
- Steps that need the running editor (scratch scenes, screenshots, Profiler) are done with the Unity MCP when it is connected, otherwise by the owner. Comparisons with the reference viewer are done by the owner from images and recordings the task produces.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

Reference set (spec §5), used by every check below: a multi-layer monster `몬스터/poring` and a true-colour monster `몬스터/elemetal_master_s3` (chosen in T0.1: 113 true-colour frames, no indexed), an NPC `npc/4_f_agentkafra`, a male body `인간족/몸통/남/초보자_남`, a head `인간족/머리통/남/1_남`, a headgear `악세사리/남/남_고글`, a garment `로브/천사날개` (root sheet `로브/천사날개/천사날개.spr` + its root ACT + the class ACT `로브/천사날개/남/abyss_chaser_남.act`, which has no SPR of its own), a weapon with trail `인간족/검사/검사_남_검` (+ `_검광`), a shield `방패/검사/검사_남_2189_방패`, a ground item `아이템/사과`, a skill icon `아이템/sm_bash`, the emote sprite `이팩트/emotion`.

---

## Phase 0: Verify before building

- [x] **T0.1** GRF mapping check (plan §4.1). In a scratch editor script, load every file of the reference set through GRF (`new Act(bytes)`, `new Spr(bytes)`, `new Pal(bytes)`) and print, per file: versions, indexed and true-colour frame counts and sizes, the first rows of one frame (row order), the palette's index 0, each action's `AnimationSpeed` and frame count, and for a few frames every layer field, `SoundId` and anchors. Pick the true-colour monster for the reference set. Write into `plan.md` §4.1 one row per `RoSpr`/`RoAct`/`RoPal` field with the GRF member it comes from, the row order, and the members not used. (FR-1, FR-3, FR-7)
  *Done when:* every field has its GRF member in `plan.md`, the row order is recorded, and the scratch script is deleted.
- [x] **T0.2** Rules check (plan §4.3). In a scratch editor script, draw frames of the reference set as throwaway sprites under the candidate rules, and produce images and short recordings for the owner to compare with the reference viewer: the origin at the feet on a GAT cell, offsets (Y down), mirror axis, rotation sign, scale, tint, draw order, frame time, loop; measure pixels per unit from a monster's height against GAT cells; check whether the 8-action effect files are directional. For any rule that differs, change the rule (not the sprite) and repeat. Record each rule's final value in `plan.md` §4.3. (FR-1, FR-3, FR-5, FR-6)
  *Done when:* the owner has confirmed every rule from the images, the table in `plan.md` holds the final values, and the scratch script is deleted.
- [x] **T0.3** Unity API spike (plan §4.2, R3). In a scratch editor script: write a small RGBA32 PNG and an R8 PNG; slice the first into named sprites with `ISpriteEditorDataProvider` (pivot centre, chosen PPU, full-rect mesh); attach the second as secondary texture `_IndexTex`; re-import twice and confirm sprite ids, names, rects and the secondary texture are unchanged; render one sprite with a throwaway shader that reads `_IndexTex` and a palette texture through a `SpriteRenderer`. Record the API calls and any caveat of Unity 6000.7 in `plan.md` §4.2. (FR-1, FR-7, FR-9)
  *Done when:* the palette render works from one sheet, ids are stable across re-imports, the caveats are in `plan.md`, and the scratch script is deleted.
- [x] **T0.4** Data check (plan §4.4 static sprites). With a scratch script count, over all of `sprite/`: ACTs that are static (1 action, 1 frame, at most one non-empty layer) and how many of those have a non-zero layer offset; ACTs whose action count is not a multiple of 8. Record the counts in `plan.md` §4.4. (FR-11, FR-3)
  *Done when:* the counts are in `plan.md` and the scratch script is deleted.

## Phase 1: Data types and readers

- [x] **T1.1** Add `Editor/RoImport/Core/Sprites/RoSpr.cs`, `RoAct.cs`, `RoPal.cs` exactly as plan §4.1, the issue codes `UnsupportedSprVersion`, `UnsupportedActVersion`, `SpriteIndexOutOfRange`, `SpriteSheetTooLarge` in `RoIssueCode`, and a test-only builder `Tests/EditMode/RoImport/Sprites/SpriteTestData.cs` (an indexed sprite with two animations of 8 directions and repeated directions, a true-colour sprite, a mixed sprite, a weapon-like ACT with empty actions and partly empty frames, a static ACT, one with anchors, one with events, one with an out-of-range index, a 100-frame SPR). (FR-1, FR-3, FR-8)
  *Done when:* the project compiles with no new warning and a test builds each sample and checks its counts.
- [x] **T1.2** Add `RoSprReaderTests`, `RoActReaderTests`, `RoPalReaderTests` (`NeedsData`, ignored): the reference set reads with the values recorded by T0.1; `SpriteType`/`SpriteIndex` per type; truncated and empty input return false with a reason and never throw. (FR-1, FR-3, FR-8)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [x] **T1.3** Implement `Editor/RoImport/Core/Formats/RoSprReader.cs`, `RoActReader.cs`, `RoPalReader.cs` with the GRF members of T0.1. (FR-1, FR-3, FR-8)
  *Done when:* `[Ignore]` removed, the tests pass, and `RoArchitectureTests` still passes (no byte parsing added).

## Phase 2: Runtime assembly

- [x] **T2.1** Create `Scripts/Sprites/Uninarok.Sprites.asmdef`, `SpriteAnimation.cs` (plan §4.5, data and `TryGetAction`), `SpriteMissingAction.cs`, and the test assembly `Tests/EditMode/Sprites/Uninarok.Sprites.Tests.asmdef` with `SpriteAnimationLookupTests`: inside the file's count → that action even if empty; outside → `DrawNothing` false, `Fallback` chain; non-directional files ignore the direction. Add the new asmdefs to `RoArchitectureTests`. (FR-3, FR-5)
  *Done when:* the project compiles with no new warning and the lookup tests pass.
- [x] **T2.2** Add `SpriteActorTests` and `SpriteSystemTests` (ignored): frame advance with `Speed`; renderers touched only on frame change; empty frames and −1 layers hide their slots; unused slots inactive; layer transform, flip, tint from the data with plan §4.3 rules; anchors in local space; `FrameEvent` once per frame entry; `Play(…, restart: false)` keeps the frame; 1,000 actors cost zero GC allocation per frame; disabling unregisters. (FR-3, FR-5, FR-6)
  *Done when:* the tests compile and are ignored with `pending T2.3`.
- [x] **T2.3** Implement `SpriteActor.cs` and `SpriteSystem.cs` (plan §4.6), including the billboard facing in the same loop. (FR-3, FR-5, FR-6)
  *Done when:* `[Ignore]` removed and the tests of T2.2 pass.
- [x] **T2.4** Implement the shader `Shaders/Sprites/RoSprite.shader` and `SpritePalette.cs` (plan §4.7) with `SpritePaletteTests`: one sprite rendered with two palettes differs per pixel, creates no texture, and leaves true-colour pixels unchanged; without a palette it renders like the main texture. (FR-7, FR-1)
  *Done when:* the tests pass and the shader compiles for URP 17.7 with no warning.

## Phase 3: Sheets

- [x] **T3.1** Add `RoSpriteSheetPackerTests` and `RoSpriteSheetPixelsTests` (ignored): deterministic layout; no overlap; inside bounds; power-of-two size ≤ 4096; padding filled by edge bleed; 100 frames → one layout; slot order indexed then true colour; palette applied; index 0 transparent whatever its alpha; true colour keeps alpha; index texture holds indices only for indexed pixels. (FR-1, FR-7, FR-9)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [x] **T3.2** Implement `Editor/RoImport/Core/Sprites/RoSpriteSheetPacker.cs` and `RoSpriteSheetPixels.cs`. (FR-1, FR-7)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.
- [x] **T3.3** Implement `Editor/RoImport/Editor/Sprites/RoSpriteSheetWriter.cs` and `RoSpriteOutputPaths.cs` with the API of T0.3 (texture settings of plan §4.2, sprites `f<slot>`, pivot centre, PPU from `RoSpriteRules`, optional `_IndexTex`), and a test that writes a synthetic sheet and reads back settings, names, rects, pivots and the secondary texture. (FR-1, FR-7, FR-9)
  *Done when:* the test passes and a second write leaves the PNGs byte-identical and the sprite ids unchanged.
- [x] **T3.4** Implement `Editor/RoImport/Editor/Sprites/RoSpriteSheetIndex.cs` (content hash → sheet path and index flag, JSON next to the ledgers, sorted, written only when changed) with tests: lookup, add, upgrade to an index texture, deterministic file. (FR-2, FR-9)
  *Done when:* the tests pass.

## Phase 4: Rules and compiler

- [x] **T4.1** Add `RoSpriteRulesTests` and `RoActCompilerTests` (ignored): the plan §4.3 table pinned with T0.2's values; flattening keeps every layer field; slots computed from type and index; identical actions stored once and mapped; empty actions and partly empty frames kept; frame ms = speed × 25; directional flag; events and anchors kept. (FR-3, FR-4, FR-5, FR-6)
  *Done when:* the tests compile and are ignored with `pending T4.2`.
- [x] **T4.2** Implement `Editor/RoImport/Core/Sprites/RoSpriteRules.cs` and `RoActCompiler.cs` (produces the arrays of `SpriteAnimation`). (FR-3, FR-4, FR-5, FR-6)
  *Done when:* `[Ignore]` removed and the tests of T4.1 pass.

## Phase 5: Converters, menu and preview

- [x] **T5.1** Add `RoSpriteConverterTests` (ignored): a synthetic pair converts to a sheet and an animation at plan §3 paths; a second byte-identical SPR reuses the sheet; an ACT converts against another file's SPR; an out-of-range index, a corrupt file, a missing SPR and an unsupported version each give one issue and no output; a static ACT gives a sprite and no animation asset (an offset one gets its offset sprite); second run `Skipped`; forced run keeps GUIDs, sprite ids and bytes; a changed SPR re-converts the ACTs that use it. (FR-2, FR-8, FR-9, FR-11)
  *Done when:* the tests compile and are ignored with `pending T5.2`.
- [x] **T5.2** Implement `Editor/RoImport/Editor/Sprites/RoSpriteConverter.cs`, `RoSpriteOptions`, `RoImportContext.Sprite(act, spr, options)`, `RoImport.Sprite(string)`, `RoJobKind.Sprite` and the registry entry; the shared material `Art/_materials/sprite_default.mat`. (FR-2, FR-3, FR-8, FR-9, FR-11)
  *Done when:* `[Ignore]` removed and the tests of T5.1 pass.
- [x] **T5.3** Implement `RoPaletteConverter.cs`, `RoJobKind.Palette`, `RoImport.Palette(string)`, with `RoPaletteConverterTests` (256 × 1, index 0 transparent, settings); add both kinds to the window and the menu items `Tools/Ragnarok/Convert Sprite Folder…` and `Convert All Palettes`. (FR-7)
  *Done when:* the palette tests pass and a synthetic sprite and palette convert from the window and the menu with progress and cancel working.
- [x] **T5.4** Implement `SpriteAnimationEditor.cs` and `RoSpritePreviewWindow.cs` (plan §4.8). (FR-10)
  *Done when:* on a converted reference sprite the inspector shows its counts, the preview plays it, the direction picker, step, speed, missing-action policy and palette slot work, and anchors are drawn; screenshots in the PR.

## Phase 6: Real data

- [x] **T6.1** Add `RoSpriteReferenceTests` (`NeedsData`): the reference set converts with no failure; sheets = distinct SPR hashes; stored actions ≤ file actions; the weapon's walk actions are empty and its attack actions are not; the skill icon and ground item are sprites. (FR-1 to FR-9, FR-11)
  *Done when:* the tests pass on the owner's data.
- [x] **T6.2** Sample run (changed by the owner on 2026-11-19 from the full-data run: "all classes, poring, fabre, lunatic and some basic weapons like knife, main gauche, sword, club"): convert every body ACT under `sprite/인간족/몸통/` with palettes, the weapon ACTs of every class for dagger `_단검`, main gauche `_1207`, sword `_검` and club `_클럽` (with their trails `_검광`), and `몬스터/poring`, `fabre`, `lunatic`, into the project. Record counts, issues, time, sheets, pixels, memory, disk and a second run, and extrapolate to the whole set for R1. (FR-2, FR-8, FR-9)
  *Done when:* every file of the sample converts or its failure is one listed issue, the second run skips all, and the numbers are in `plan.md` §9.
- [x] **T6.3** Minimal data import (changed by the owner on 2026-11-19 from the full-data run: "I'll run full data later if needed or I'll import only specific assets"): the sample of T6.2 plus every head ACT under `sprite/인간족/머리통/` with palettes (85, 0 failures, 55 s), converted locally and not committed. The full run (`Tools/Ragnarok/Convert Sprite Folder…` on `sprite/`, `Convert All Palettes`) is left to the owner, on demand. (FR-2, FR-8, FR-9)
  *Done when:* the minimal set converts with no failure and the full run stays available from the menu.

## Phase 7: Verify and close

- [ ] **T7.1** Visual check against the reference viewer for the whole reference set: layout, timing, direction, mirror, rotation, no background or fringe on bright and dark ground at 1× and 4×, a sprite standing on a GAT cell, a body's head anchor following the body, a weapon hidden while walking and shown while attacking, a body with two cloth palettes from one sheet. (FR-1, FR-3, FR-5, FR-6, FR-7)
  *Done when:* recordings or screenshots are in the PR and the owner marks each case as matching.
- [ ] **T7.2** Budget: in a development build place 300 actors (a mix of the reference set) and record the `SpriteSystem` loop time, the render cost and managed allocation per frame, on screen and off screen. If over budget, open the D5 fallback as a plan item. (FR-3)
  *Done when:* the numbers are in the PR, steady-state `GC.Alloc` is zero, and the owner accepts the budget or the fallback is opened.
- [ ] **T7.3** Close: verify each FR against tests and evidence; set `spec.md`, `plan.md` and `tasks.md` to `Implemented`; move leftovers (unused-sheet cleanup, clip export for Timeline, texture compression if pending) to `docs/specs/BACKLOG.md`.
  *Done when:* the traceability table of the plan has a passing test or recorded evidence for each FR and the leftovers are in `BACKLOG.md`.

---

## Coverage check (FR → tasks)

| FR | Tasks |
|---|---|
| FR-1 | T0.1, T0.2, T0.3, T1.1, T1.2, T1.3, T2.4, T3.1, T3.2, T3.3, T6.1, T7.1 |
| FR-2 | T3.4, T5.1, T5.2, T6.1, T6.2, T6.3 |
| FR-3 | T0.1, T0.2, T0.4, T1.1–T1.3, T2.1–T2.3, T4.1, T4.2, T5.2, T6.1, T7.1, T7.2 |
| FR-4 | T4.1, T4.2, T6.1 |
| FR-5 | T0.2, T2.1–T2.3, T4.1, T4.2, T6.1, T7.1 |
| FR-6 | T0.2, T2.2, T2.3, T4.1, T4.2, T7.1 |
| FR-7 | T0.1, T0.3, T2.4, T3.1–T3.3, T5.3, T7.1 |
| FR-8 | T1.1–T1.3, T5.1, T5.2, T6.2, T6.3 |
| FR-9 | T0.3, T3.1, T3.3, T3.4, T5.1, T5.2, T6.2, T6.3 |
| FR-10 | T5.4 |
| FR-11 | T0.4, T5.1, T5.2, T6.1 |
