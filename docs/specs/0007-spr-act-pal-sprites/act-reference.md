# SPR, ACT and PAL facts (as read through GRF)

| | |
|---|---|
| **Belongs to** | [SPEC-0007](spec.md) (reference material, normative for its topic) |
| **Status** | Draft (survey of 2026-11-19) |
| **Data set** | `Uninarok-client/Assets/Editor/GRF Resources~/data/sprite`, `data/palette` and `data/wav` (LATAM client) |
| **Method** | SPR/ACT read with GRF `Spr`/`Act` in a scratch tool over every category (up to 1,500 files each); MD5 for duplicate files. Nothing parses bytes by hand. |

Status of each claim: **Confirmed** (checked in the data), **Candidate** (consistent with the data, not yet checked in the reference viewer).

## 1. Actions — Confirmed

- An ACT is a list of actions. For directional sprites, **action index = animation × 8 + direction**; index 8 is animation 1 (walk) in direction 0.
- The direction is `UninarokDirection` ([UninarokPosition](../../data/UninarokPosition.md)): the sprite and the network use the same values (owner decision, 2026-11-19).
- The meaning of the animation number depends on the sprite family: players 13 animations, monsters 5 or more, NPCs usually 1. See [SpriteAction](../../data/SpriteAction.md).
- **Not every file has every animation.** Action counts found: 104 (players), 40, 48, 56, 64, 72 (monsters, homunculi), 8 (NPCs), 1 (ground items), and odd counts (10, 41, 47, 98, …). A count that is not a multiple of 8 means a non-directional sprite.

## 2. Duplicate actions — Confirmed

- Inside one file, about **half the actions are distinct**: monsters 48 %, NPCs 46 %, bodies 56 %, heads 56 %, garments 56 %, weapons 17 %, shields 15 %.
- Typical patterns: monsters repeat direction pairs (0 = 1, 2 = 3, 4 = 5, 6 = 7); player bodies have 8 distinct directions for idle, walk and sit and pairs for the rest; some animations (head "freeze 2") use one image for all 8.
- Mirrored layers draw one side from the other (the mirror flag is used in almost every file).

## 3. Frames and layers — Confirmed

- A frame has **zero or more layers**. Multi-layer frames: 90 % of monsters (up to 10 layers), 23 % of NPCs, 58 % of effects; player parts have one (heads two, see below).
- Each layer: sprite index, image type (indexed or true colour), offset X/Y in pixels, mirror, scale X/Y, rotation in degrees, RGBA tint. Monsters use rotation (74 %), scale (83 %) and tint (86 %); weapons and shields set scale and tint on every file.
- **Layer fields read through GRF** (public fields of `Layer`): `SpriteType` (0 = `Indexed8`, a palette frame; 1 = `Bgra32`, a true-colour frame), `SpriteIndex`, `OffsetX`, `OffsetY`, `Mirror`, `ScaleX`, `ScaleY`, `Rotation` (degrees), `Color` (RGBA tint), `Width`, `Height`. Frame: `Layers`, `SoundId`, `Anchors` (`OffsetX`, `OffsetY`). Action: `Frames`, `AnimationSpeed`.
- **`SpriteIndex` counts inside its own type:** type 0 indexes the SPR's indexed frames, type 1 its true-colour frames, each from 0. Checked on 8,800 files: no index outside its type's list.
- **`Width` and `Height` are derived:** in every layer checked (3.5 million) they equal the size of the image the layer points to. They are not stored.
- **Palette use by type** (layers that draw something, sample of up to 1,500 files per folder):

| Folder | Files indexed only | Files true-colour only | Files with both | Indexed layers | True-colour layers |
|---|---:|---:|---:|---:|---:|
| Monsters | 1,119 | 23 | 351 | 1,739,151 | 394,603 |
| NPCs | 1,222 | 18 | 42 | 279,756 | 25,913 |
| Effects | 151 | 63 | 10 | 23,201 | 4,203 |
| Headgears | 1,418 | 82 | 0 | 661,031 | 59,545 |
| Bodies, heads, weapons | 1,474 | 23 | 0 | 217,298 | 2,200 |
| Ground items | 1,500 | 0 | 0 | 1,513 | 0 |
| Shields | 1,319 | 0 | 0 | 63,408 | 0 |

  Every indexed frame uses the palette embedded at the end of its SPR; an external PAL file (bodies, heads) replaces that palette.
- **Sprite index −1 = empty layer** (draws nothing). Every head (its first layer), weapon and shield file uses it (frames where the item is hidden).
- **Empty actions are data, not gaps.** Weapons and shields always have all 104 actions; where the item is hidden every frame is empty. Share of fully empty actions per animation (sample of 164–400 files):

| Animation | 0 idle | 1 walk | 2 sit | 3 pick | 4 standby | 5 atk1 | 6 hurt | 7 frz1 | 8 dead | 9 frz2 | 10 atk2 | 11 atk3 | 12 cast |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Weapons (swordman, knight) | 100 % | 100 % | 99–100 % | 99–100 % | 12 % | 100 % | 100 % | 100 % | 100 % | 100 % | 18–19 % | 80–81 % | 100 % |
| Shields | 100 % | 100 % | 100 % | 100 % | 0 % | 98 % | 100 % | 100 % | 100 % | 100 % | 100 % | 100 % | 100 % |
| Headgears | 2–10 % in every animation |||||||||||||
| Garments, monsters | none |||||||||||||

- Some actions are only **partly** empty (the item disappears for some frames of an attack): 268–428 such actions per job folder of weapons, 754 in 400 headgears, 468 in 400 monsters.
- **Anchors:** player parts carry one anchor per frame (body: where the head attaches; head and equipment: their own attach point). Monsters 5 %, NPCs 2 %.
- **Events:** a frame may name an event from the file's event list: `atk` (hit moment) or a `.wav` sound. Example: `poring.act` has `poring_move.wav` on walk, `poring_attack.wav` on attack, `poring_damage.wav` on hurt, `poring_die.wav` on die.
- **True-colour (BGRA) frames:** 26 % of monsters, 6 % of NPCs, 7 % of headgears, 33 % of effects; none in bodies, heads, garments, shields.
- **Versions:** 2.1, 2.3, 2.4, 2.5 (2.5 is 78–100 % of every category).

## 4. Timing — Confirmed

- GRF exposes `Action.AnimationSpeed` and `Action.Interval`; in every action **`Interval` (ms) = `AnimationSpeed` × 25** (4 → 100 ms, 2.5 → 62 ms, 1 → 25 ms). One value; `Interval` is derived.
- `AnimationSpeed` is a **frame-delay factor**: a larger value is a **slower** animation (4 = 100 ms per frame).
- Common values: 4 (all categories), 3, 2, 2.5, 1, 8.
- Scaling the walk animation by the unit's move speed belongs to the movement/display spec.

## 5. SPR and palettes — Confirmed

- An SPR holds indexed frames (one 256-colour palette for the file) and, in some files, true-colour frames. Index 0 is the transparent background.
- **Identical SPR files are common** across folders: garments 60,438 SPR → 294 distinct, shields 1,320 → 29, headgears 4,812 → 2,830, monsters 1,493 → 1,322.
- An ACT may have **no SPR of its own** and use another file's sheet (20,901 garment ACTs use their folder's root sheet). The ACT file does not name its SPR (§6): the pairing is decided by whoever asks for the conversion.
- A PAL file is a 256-colour table that replaces the SPR's palette; index 0 stays transparent. Palettes exist for bodies (cloth colour) and heads (hair colour).

## 6. ACT ↔ SPR link — Confirmed

- An ACT file holds **no reference to an SPR** (no file name, no hash). GRF's `Act.Sprite` is an in-memory link set by the caller (`new Act(actData, sprData)` or `SetSprite`); loading only the ACT (`new Act(actData)`) gives an empty `Spr` with 0 images.
- The client pairs files by name (same folder and base name), with category rules for shared sheets (garments, doram).
- A few ACTs contain stray `.spr` text in unused bytes (for example `아이템/book_of_sorcery.act`, `몬스터/ep19_hallucigenia.act`): leftovers of the authoring tool, not read by GRF and not a reference.

## 7. Sound events → `data/wav` — Confirmed

- Event names ending in `.wav` are file names relative to `data/wav/` (no sub-folder in any event name).
- Checked on 25,422 ACTs (all categories except garments): 1,849 distinct event names, **1,845 found** in `data/wav`; missing: `spore_move.wav`; non-sound names besides `atk`: `a`, `atk.txt` (typos, ignored).
- `data/wav` holds 2,990 WAV files (plus sub-folders `effect/…` for skills, not used by ACT events).
