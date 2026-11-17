# TASKS-0007: SPR, ACT and PAL Sprites

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Draft |

**How to use this file**
- Start only when TASKS-0002 phases 0 to 6 are done (foundation: paths, writer, ledger, materials, context, batch, texture settings). This spec is independent of 0003 to 0006 and may be done before or after them.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0007): T4.2 implement RoSpriteClipPlan` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent.
- Paths are relative to `Uninarok-client/Assets/Editor/RoImport/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a spike (phase 0) shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Spike scripts are scratch code: delete them when the task is done. Production code never parses SPR, ACT or PAL bytes (PLAN-0002 §1).
- Steps that need the running editor (scratch scenes, screenshots, Profiler, builds) are done with the Unity MCP when it is connected, otherwise by the owner.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Verify before building

- [ ] **T0.1** Check the data: count `.spr`, `.act` and `.pal` files under `sprite/` and `palette/` by category folder (characters, monsters, NPCs, items, other); list SPR files without an ACT and the reverse; read sizes and frame counts through GRF in a scratch script for all of them and record the total frame count, the total pixel area, the number of files whose frames would not fit one 4096 page, the number of files with true-colour frames, with anchors and with events, and the versions found. Propose texture memory targets from the totals and agree them with the owner. (FR-6, FR-10)
  *Done when:* the counts and the agreed targets are in the PR and written into `plan.md` §9.
- [ ] **T0.2** Map GRF to the file fields (plan §4.1). In a scratch editor script load one reference sprite of each category (a monster, an NPC, a body, a headgear, a weapon, an item, an effect sprite) and a PAL through the GRF `Spr`, `Act` and `Pal` classes; print versions, counts, sizes, palettes, actions, frames, layers, anchors and events; compare with the raw values of the same files (include a sprite with true-colour frames, one with anchors and one with events). Record the pixel row orientation GRF returns, the units of the frame time, the layout per ACT version found, and which GRF members are derived and must not be used. Write into `plan.md` §4.1 a table: one row per `RoSpr`/`RoAct`/`RoPal` field with the GRF member that supplies it. (FR-1, FR-3, FR-7)
  *Done when:* every field has a GRF member (or is declared absent for the version), the derived members are listed as unused, and the table is in `plan.md`.
- [ ] **T0.3** Verify the rules table (plan §4.3). In a scratch editor script draw the frames of a monster, an NPC, a body, a headgear and an item as throwaway sprites using the candidate rules and the GRF values of T0.2, and compare with the reference viewer: action and direction of each clip (idle, walk, attack, damage, die in all eight directions), the origin at the feet, layer offsets, pixel size against ground cells, mirror axis, rotation sign, scale, image-type numbering, frame time, loop and non-loop actions, overlapping sprites and their order. For any rule that differs change the rule (not the sprite) and repeat. Record in `plan.md` §4.3 the final value of each rule, the `RoSpriteRules` table, the facing rule that matches the reference (`RoSpriteBillboard` mode) and "result by sprite". (FR-3, FR-4, FR-5)
  *Done when:* all five sprites match the reference viewer in layout, timing and facing, the table is in `plan.md`, the owner has seen the comparison images, and the scratch script is deleted.
- [ ] **T0.4** Verify the Unity side (plan §4.4, §4.7, R7). In a scratch editor script create a small atlas PNG with a secondary texture through the sprite-editor data API, read the assignment back, and render a quad with a throwaway shader that samples the secondary texture and a palette texture; confirm that sprite ids stay stable across a re-import and that animation clips with sprite, active-flag and transform curves play from an `Animator` with Any State transitions. Record the Unity version and any API caveat in `plan.md` §4.4. (FR-2, FR-3, FR-5)
  *Done when:* the palette swap renders correctly from one atlas, the sprite ids are stable, the caveats are in `plan.md`, and the scratch script is deleted.

## Phase 1: Data types and readers

- [ ] **T1.1** Add `Core/Sprites/` types exactly as in plan §4.1 (`RoSpr`, `RoSprFrame`, `RoPal`, `RoAct`, `RoActClip`, `RoActFrame`, `RoActLayer`) and a test-only builder `SpriteTestData` in `Assets/Tests/EditMode/RoImport/Sprites/` (an indexed sprite with two actions of eight directions, a true-colour sprite, a mixed sprite, a sprite with a blank frame, one with anchors, one with events, a 100-frame file). Add the issue codes `SpriteMissingAct`, `SpriteMissingSpr`, `UnsupportedSprVersion`, `UnsupportedActVersion`, `SpriteAtlasMultiPage` to `RoIssueCode`. (FR-1, FR-3, FR-10)
  *Done when:* the project compiles and a test builds each sample and checks its counts.
- [ ] **T1.2** Add `RoSprReaderTests` and `RoActReaderTests` (`NeedsData`, failing, ignored): the reference sprites read through GRF with the frame, action, layer and anchor counts recorded by T0.2; a PAL reads 256 entries; a truncated copy and an empty array return false with a reason and never throw. (FR-1, FR-3, FR-10)
  *Done when:* the tests compile and are ignored with `pending T1.3`.
- [ ] **T1.3** Implement `Core/Formats/RoSprReader`, `RoActReader` and `RoPalReader` using the GRF members recorded by T0.2 (pixel rows converted to top first here, blank frames kept as `Blank`). No parsing code of our own. (FR-1, FR-3, FR-10)
  *Done when:* `[Ignore]` removed and the tests pass; a text search shows no `BinaryReader` or `BitConverter` use added.

## Phase 2: Runtime assembly and shader

- [ ] **T2.1** Create `Assets/Scripts/Sprites/Uninarok.Sprites.asmdef` (no editor and no importer references), `RoSpriteEvents` (events `Sound` and `Attack`; methods `OnSpriteSound(string)` and `OnSpriteAttack()`), and add tests in a runtime test assembly: the events reach subscribers. (FR-7)
  *Done when:* the project compiles and the event tests pass.
- [ ] **T2.2** Add `RoSpriteBillboardTests` (failing, ignored): a billboard faces the camera after a `LateUpdate`, in both facing modes; 1,000 registered billboards cost zero allocations per frame; disabling unregisters; an unchanged camera costs one comparison. Implement `RoSpriteBillboard` with its static registry and single driver loop (plan §4.7). (FR-5)
  *Done when:* `[Ignore]` removed and the tests pass.
- [ ] **T2.3** Implement the shader `Assets/Shaders/Sprites/RoSprite.shader` (plan §4.7) and `RoSpritePalette` (apply and clear through a cached `MaterialPropertyBlock`). Add `RoPaletteTests` render test: one sprite with two palettes changes the rendered colours per pixel, creates no texture and no frames, and leaves true-colour pixels unchanged. (FR-2, FR-8)
  *Done when:* the render tests pass and the shader compiles for the project's URP version with no warning.

## Phase 3: Atlas

- [ ] **T3.1** Add `RoSpriteAtlasTests` and `RoSpriteColorTests` (failing, ignored): packing is deterministic; no rects overlap and every rect lies inside its page; padding is filled by edge bleed; a 100-frame file uses one texture; an oversized file reports `SpriteAtlasMultiPage`; blank frames keep their indices; an indexed frame is drawn with its palette and index 0 is fully transparent whatever its alpha; a true-colour frame keeps its alpha; the index atlas holds the index only for indexed pixels. (FR-1, FR-6, FR-8)
  *Done when:* the tests compile and are ignored with `pending T3.2`.
- [ ] **T3.2** Implement `Core/Sprites/RoSpriteAtlasPacker` and the pure frame compositing (colour atlas and index atlas pixel buffers). (FR-1, FR-6)
  *Done when:* `[Ignore]` removed and the tests of T3.1 pass.
- [ ] **T3.3** Implement `Editor/Sprites/RoSpriteAtlasWriter`: write the PNGs, apply the importer settings of plan §4.4 (point filter, no mipmaps, no compression, clamp, secondary texture `_IndexTex`), create the named sprites through the sprite-editor data API with stable ids. Add a test that converts a synthetic atlas and reads back the settings, the sprite names and rects and the secondary texture. (FR-6, FR-8)
  *Done when:* the test passes and a second write leaves the PNGs byte-identical and the sprite ids unchanged.

## Phase 4: Rules and clips

- [ ] **T4.1** Add `RoSpriteRulesTests` and `RoSpriteClipPlanTests` (failing, ignored): the table of plan §4.3 pinned (action and direction of clip indices for multiples of 8 and for other counts, pixel size, mirror, rotation sign, image type numbering, time per frame, non-looping actions); per-slot curves for a synthetic two-action sprite equal the expected keys (positions in Unity units with Y up, tint, mirror as a negative X scale, rotation, active flag); durations are ticks × 24 ms; the last frame holds; anchors become position curves; events land at the right time with the right function name. (FR-3, FR-4, FR-7)
  *Done when:* the tests compile and are ignored with `pending T4.2`.
- [ ] **T4.2** Implement `Core/Sprites/RoSpriteRules` (values of T0.3), `RoSpriteNaming` and `RoSpriteClipPlan` (plan §4.3, §4.5). (FR-3, FR-4, FR-7)
  *Done when:* `[Ignore]` removed and the tests of T4.1 pass.
- [ ] **T4.3** Add `RoSpriteControllerTests` (failing, ignored): one state per clip, parameters `Action` and `Direction`, the Any State transitions select the right clip for each value pair, the default state is action 0 direction 0, a single-direction file uses `Action` only. Implement `Editor/Sprites/RoSpriteClipBuilder` and `RoSpriteControllerBuilder` (clips as sub-assets named `a<i>`, updated in place). (FR-3, FR-5)
  *Done when:* `[Ignore]` removed and the tests pass.

## Phase 5: Converter and prefab

- [ ] **T5.1** Add `RoSpriteWriterTests` and `RoSpriteIdempotencyTests` (failing, ignored): a synthetic sprite converts to the atlases, controller with clips and prefab at the paths of plan §3; the prefab has `Layer_k` and `Anchor_k` children, an `Animator`, the billboard and events components and the shared material; a missing partner, a corrupt file and an unsupported version give one issue each and no output; the second conversion is `Skipped`; a forced second conversion leaves atlases, clips and controller byte-identical and every GUID unchanged; a scene referencing the prefab and a prefab referencing a clip keep their references; the prefab and its clips contain no importer type. (FR-4, FR-5, FR-6, FR-10; SPEC-0002 FR-5, FR-16)
  *Done when:* the tests compile and are ignored with `pending T5.2`.
- [ ] **T5.2** Implement `Editor/Sprites/RoSpriteConverter`, `RoSpritePrefabBuilder` and `RoSpriteMaterials` (`sprite_default.mat`, `sprite_palette.mat`); add `RoJobKind.Sprite`, the `RoConverterRegistry` entry, `RoImport.Sprite(string)`; the ledger category `sprite` lists atlases, controller and prefab as outputs and the SPR and ACT hashes as dependencies. (FR-1 to FR-6, FR-10)
  *Done when:* `[Ignore]` removed and the tests of T5.1 pass.
- [ ] **T5.3** Implement `Editor/Sprites/RoPaletteConverter` (256 × 1 texture, point, clamp, no mipmaps, index 0 transparent), `RoJobKind.Palette` and `RoImport.Palette(string)`; add the kinds `Sprite` and `Palette` to the window and the menu items `Tools/Ragnarok/Convert All Sprites` and `Convert All Palettes`. Add a test that a PAL converts to the expected texture. (FR-2)
  *Done when:* a synthetic sprite and a synthetic palette convert from the window and from the menu with progress and cancel working, and the palette test passes.

## Phase 6: Gizmos

- [ ] **T6.1** Implement `RoSpriteGizmos` (`[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(RoSpriteBillboard))]`: origin, anchors with index, current frame bounds) and the overlay "Ragnarok Sprite" with a switch per layer (`EditorPrefs`). (FR-9)
  *Done when:* on a converted reference sprite the gizmos appear on selection only, each layer switches off, and with nothing selected the Profiler shows no gizmo time.

## Phase 7: Whole data set

- [ ] **T7.1** Add `RoSpriteReferenceTests` (`NeedsData`): the reference sprites (a monster, an NPC, a body, an item) convert with no failure; the frame count equals the SPR's non-blank frames; the clip count equals the ACT's; one colour atlas per file; a second batch skips everything; every `.spr` under `sprite/` is attempted and failures are listed by name. (FR-1 to FR-10)
  *Done when:* the tests pass on the owner's data.
- [ ] **T7.2** Run **Convert All Sprites** and **Convert All Palettes** on the full data set. Record the numbers converted, warnings by code (`SpriteMissingAct`, `SpriteMissingSpr`, `SpriteAtlasMultiPage` and the unsupported versions), failures, total time, the total texture memory of the colour and index atlases against the target of T0.1, the size of the controllers and whether `.gitattributes` needs a line for them. (FR-6, FR-10)
  *Done when:* every file converts or its failure is one listed issue, a second run skips all, the memory is within the target (or the owner has agreed a change), and the numbers are in the PR.

## Phase 8: Verify and close

- [ ] **T8.1** Visual check against the reference viewer for a monster, an NPC, a body, a headgear, an item and an effect sprite: no coloured background or fringe on a bright and a dark background, sharp at 1× and 4×; walk, attack and idle at the original speed and layout; a sprite on a map cell stands on it; a head anchor follows the head. (FR-1, FR-3, FR-4, FR-8)
  *Done when:* screenshots or recordings are in the PR and each case is marked as matching.
- [ ] **T8.2** Palette and animator by hand: the same sprite with its own palette and with two alternative palettes from one set of frames (one atlas in the Project window); drop the prefab in a scene, see the default action, switch `Action` and `Direction` in the animator; an attack raises its sound event at the right frame. (FR-2, FR-5, FR-7)
  *Done when:* screenshots or recordings are in the PR.
- [ ] **T8.3** Budget: in a development build place 300 animated sprites on screen (a mix of the reference sprites) and record the animator update time, the billboard loop time and the managed allocation per frame, with all instances on screen and then all off screen. If over budget, switch the controller builder to no-transition mode (plan R2) and measure again. (FR-5)
  *Done when:* the numbers are in the PR, steady-state `GC.Alloc` is zero, and the budget is accepted by the owner or the fallback is opened as a plan item.
- [ ] **T8.4** Close: verify each FR of the spec against the tests and manual evidence; set the status of `spec.md`, `plan.md` and `tasks.md` to `Implemented`; move leftovers (IMF ordering, character composition, 3D quads) to `docs/specs/BACKLOG.md`.
  *Done when:* the traceability table of the plan has a passing test or recorded evidence for each of FR-1 to FR-10 and the leftovers are in `BACKLOG.md`.

---

## Coverage check (FR → tasks)

| FR | Tasks |
|---|---|
| FR-1 | T0.2, T1.1, T1.2, T1.3, T3.1, T3.2, T5.2, T7.1, T8.1 |
| FR-2 | T0.4, T2.3, T5.3, T8.2 |
| FR-3 | T0.2, T0.3, T0.4, T1.1, T4.1, T4.2, T4.3, T8.1 |
| FR-4 | T0.3, T4.1, T4.2, T5.1, T5.2, T8.1 |
| FR-5 | T0.3, T0.4, T2.2, T4.3, T5.1, T5.2, T8.2, T8.3 |
| FR-6 | T0.1, T3.1, T3.2, T3.3, T5.1, T5.2, T7.2 |
| FR-7 | T0.2, T2.1, T4.1, T4.2, T8.2 |
| FR-8 | T2.3, T3.1, T3.3, T8.1 |
| FR-9 | T6.1 |
| FR-10 | T0.1, T1.1, T1.2, T1.3, T5.1, T5.2, T7.1, T7.2 |
