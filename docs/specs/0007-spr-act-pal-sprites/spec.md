# SPEC-0007: SPR, ACT and PAL Sprite Import (core)

| | |
|---|---|
| **Status** | Implemented (2026-11-19; approved revision 3; FR-8 amended by the owner 2026-11-21, revision 4) |
| **Created** | 2026-11-16 |
| **Revised** | 2026-11-19: scope narrowed to converting one sprite (images, animations, palettes); finding sprites by game id and grouping them is a later spec. 2026-11-21 (owner, SPEC-0008 T7.4): an ACT layer that indexes an image its sheet does not have draws nothing instead of rejecting the file (FR-8). |
| **Owner** | _(fill in)_ |
| **Reference** | [act-reference.md](act-reference.md) (format facts read through GRF), [SpriteAction](../../data/SpriteAction.md), [UninarokPosition](../../data/UninarokPosition.md) (direction) |
| **Plan** | [plan.md](plan.md) (Implemented, 2026-11-19) |
| **Tasks** | [tasks.md](tasks.md) (Implemented, 2026-11-19) |

## 1. Why

Every unit, equipment piece, ground item and simple effect is an SPR (images) with an ACT (animations), sometimes recoloured by a PAL. Monsters, items and equipment all need the same conversion; only how the results are grouped and found differs. One shared, data-agnostic conversion keeps the rules (layers, timing, directions, transparency, deduplication) in one place, so grouping by game object can be a thin layer on top. The data is highly repetitive (half of all actions repeat inside their file; many SPR files are byte copies), so the conversion must share instead of copy.

## 2. Goals

1. Convert any SPR/ACT pair into images and animation data the game can play.
2. Never store the same images or the same action twice.
3. Recolour indexed sprites with a palette without new images.

## 3. Non-goals

- Knowing what a sprite is (monster, garment, …), looking it up by id, grouping, import menus by category: a later spec.
- Composing a character from several sprites; STR effects; sound playback; placing units on a map.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Images: the frames of an SPR (indexed and true-colour) become one Unity sprite sheet: one texture holding every frame, sliced into one sprite per frame, pixel-exact, transparent background (palette index 0), sharp at any zoom. | A multi-layer monster and a true-colour effect show no background or fringe at 1× and 4×; a 100-frame SPR is one texture with 100 sprites. A monster mixing indexed and true-colour frames shows both kinds from one sheet. |
| FR-2 | M | Shared images: an image set is identified by its content; converting a second sprite with the same images reuses the first result. An ACT can be converted against an image set from another file. | Converting two byte-identical SPR files yields one image set; a garment ACT without its own SPR converts against its folder's sheet. |
| FR-3 | M | Animations: each ACT becomes one data asset; every action keeps its frames, frame time in ms, every layer of every frame (which sprite of the sheet: image type indexed or true colour plus index, or empty −1; offset X/Y; mirror; scale X/Y; rotation; RGBA tint) in draw order, anchors and events (`atk`, sound names). Layer width and height are not kept: they always equal the sprite's size. Actions are addressed by animation and direction (action = animation × 8 + direction; files whose action count is not a multiple of 8 are non-directional). | A monster, a player body and a weapon play idle, walk and attack in all 8 directions with the reference viewer's layout and speed. |
| FR-4 | M | Shared actions: identical actions inside a file are stored once and referenced by every direction that uses them. | A monster whose directions repeat in pairs stores at most half its actions. |
| FR-5 | M | Empty is not missing. An action or frame whose layers are all empty (−1) is real data and draws **nothing**; it is never replaced. Equipment relies on this: weapons and shields have all 104 actions, but the ones where the item is hidden (idle, walk, sit, …) are empty, and some attack frames are empty. Only an action the file does **not have** (index beyond its action count) is missing; for a missing action the caller chooses "draw nothing" or "fall back" (same animation in the nearest stored direction, else idle, else the first action). Never an error. | A sword draws nothing while its owner walks and appears in the attack frames only; a one-animation NPC asked for "walk, direction 3" with fall back shows its idle, with draw nothing shows nothing. |
| FR-6 | M | Origin at the feet: the sprite's position is the ground point of the unit; anchors are available per frame. | A converted sprite placed on a map cell stands on it; a body's head anchor follows the body in every frame. |
| FR-7 | M | Palettes: an indexed sprite can be shown with an alternative palette without new images; its own palette is the default. | One body shows with two cloth palettes from one image set. |
| FR-8 | M | Bad input: a malformed file or an unsupported version gives one warning and no partial output. An ACT layer that indexes an image its sheet does not have **draws nothing** (as the client does): the file converts and gives one warning naming how many layers and the first one (revision 4). | Each bad file in a batch is listed once; the batch continues; an out-of-range layer is an empty layer in the output. |
| FR-9 | M | Re-import is stable: converting the same input again changes nothing and keeps every reference to the output valid. | A second conversion reports "skipped"; a scene using a converted sprite keeps working after a forced re-conversion. |
| FR-10 | S | Preview: any converted sprite can be played in the editor with an animation and direction picker and its anchors shown. | Selecting a converted sprite plays it; switching direction updates it. |
| FR-11 | M | Static sprites: an SPR/ACT whose ACT has one action of one frame (ground items, skill icons) becomes only its sprite (FR-1), with its offset; no animation asset and no player. | A ground item and a skill icon import as a sprite and show without any animation component. |

## 5. Success criteria

- Reference sprites (a multi-layer monster, an NPC, a body, a head, a headgear, a garment, a weapon with trail, a shield, a ground item, an emote) match the reference viewer in layout, timing and direction.
- Deduplication works at both levels: identical SPR content gives one image set, repeated actions are stored once.

## 6. Assumptions and constraints

- Shared import rules (paths, units, idempotency, ledger) come from SPEC-0002. Files are read through the GRF library only.
- Sprites are flat, camera-facing images over the 3D world (decided earlier, kept).
- The sprite direction **is** the network direction (`UninarokDirection`, owner decision 2026-11-19); camera rotation is applied by the display, not by this spec.
- Frame time = `AnimationSpeed` × 25 ms (act-reference §4); larger is slower.
- The meaning of each animation number (walk, attack, …) is data documentation ([SpriteAction](../../data/SpriteAction.md)); this spec only addresses animations by number.
- **Decided (OQ-1, owner, 2026-11-19): images as Unity sprite sheets, animations as data assets, played by one runtime system; no AnimationClips or Animators.** Reasons: an ACT as clips means millions of assets (about 108,000 ACT × up to 104 actions); a player's 9 parts (body, head, three headgears, garment, weapon, trail, shield) must stay on one shared frame clock; one loop for all units costs less than one Animator per part; RO rules (direction change keeping the frame, walk speed and attack speed scaling, FR-5) are simple in data and awkward in a state machine. Exporting a sprite to AnimationClip on demand (for Timeline) is a possible later tool, out of scope.
- An SPR stores each frame as its own bitmap (not as rectangles of one image); packing them into one texture is the conversion's job. Indexed frames keep their palette indices so FR-7 needs no second copy of the frames.

## 7. Open questions

None open. OQ-1 (how animations are stored) is decided in §6.
