# SPEC-0007: SPR, ACT and PAL Sprites

| | |
|---|---|
| **Status** | Approved |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | plan.md _(written after approval)_ |
| **Tasks** | tasks.md _(written after plan approval)_ |

## 1. Why

Characters, monsters, NPCs, items and effects are 2D sprites in Ragnarok: SPR holds the frames, ACT describes how frames form actions, directions and animations, PAL holds colour palettes. Without them the world is empty. Sprites are the largest group of files and need to be efficient in memory and exact in layout, timing and anchors.

## 2. Goals

1. Each SPR/ACT pair becomes a ready-to-use animated sprite.
2. Palettes can change a sprite's colours without duplicating frames.
3. Placement (feet, head anchors) is correct.
4. All sprite categories are covered: characters, weapons, items on the ground, NPCs, monsters and other sprites.

## 3. Non-goals

- Composing a full character (body, head, headgear, weapon) from several sprites (future spec).
- Sprite effects and screen effects.
- Runtime loading of source files.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Each SPR converts to frames usable in Unity, for both indexed (palette) and full-colour frames, with the background transparent. | A reference monster's frames show no coloured background or fringe. |
| FR-2 | M | Palettes: an indexed sprite uses its own palette by default, and an alternative palette (for example hair or cloth colour) can be applied without duplicating frames. | The same sprite displays with two different palettes, using one set of frames. |
| FR-3 | M | Each ACT converts to animation clips, one per action and direction, played through an animator, with the original's per-frame durations, and layers (several sprite parts per frame) with their offset, scale, rotation, mirroring and tint. | A reference sprite's walk, attack and idle animations play at the original speed and layout, matching the reference viewer. |
| FR-4 | M | The sprite's origin is where the original client treats the unit's position (feet), and attach points from the ACT are available. | A sprite placed at a map cell stands on it. A head attach point is queryable. |
| FR-5 | M | Each SPR/ACT pair yields one ready-to-use item (one sprite object with an animator and its clips, that can be placed and played). | Dropping the item in a scene plays its default action, and switching action or direction through the animator works. |
| FR-6 | M | Frames of one file share a packed texture; no per-frame textures. | A file with 100 frames uses one texture. |
| FR-7 | S | Sound events in the ACT are available as animation clip events with the sound name. | A reference attack animation raises its sound event at the right frame. |
| FR-8 | S | Pixel-art friendly display settings (no blur) by default. | Sprites look sharp at 1x and 4x. |
| FR-9 | S | Selecting a sprite shows gizmos: origin, anchors, frame bounds. | Gizmos appear on selection only, editor-only. |
| FR-10 | M | A malformed or unsupported file gives one warning and no partial output. | A batch lists each bad file once. |

## 5. Success criteria

- Reference sprites (a monster, an NPC, a character body, an item) match the reference viewer in layout and timing.
- Palette swapping works without duplicating textures.

## 6. Assumptions and constraints

- Shared rules (units and axes, textures, materials, idempotency, batches) are in SPEC-0002 and apply here.
- Sprite animation is delivered as animation clips driven by an animator, like model animation in SPEC-0003 (decided).
- Sprites keep the original draw order and facing directions (eight directions).
- All sprite categories are in scope (decided). Characters and weapons are converted as individual body-part and weapon sprites; assembling them into a full character is a later spec.
- Character and weapon sprites are drawn as flat billboards facing the camera (decided, for now). The other categories are assumed to use the same display; say if any should differ. Billboards over the 3D world are the only display mode this spec covers; 3D quads are a later change.
- Texture memory targets are set in the plan once the file count is known.

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | Are IMF files (layer ordering for characters) in scope here? | FR-3 |
