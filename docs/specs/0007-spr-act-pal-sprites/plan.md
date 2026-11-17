# PLAN-0007: SPR, ACT and PAL Sprites

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0007) |
| **Status** | Draft |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Shared rules (paths, units and axes, textures, writer, ledger, results, batch) come from [PLAN-0002](../0002-ro-import-foundation/plan.md). Animation as clips played by an animator follows the approach of [PLAN-0003](../0003-rsm-models/plan.md) §4.8.

## 1. Constraints and assumptions

- Code lives in the editor assemblies of PLAN-0002 under `Core/Sprites/`, `Core/Formats/` (adapters) and `Editor/Sprites/`; the few things a sprite needs **at runtime** (billboard behaviour, animation event receiver, palette helper, a shader) are runtime code in a new runtime assembly `Uninarok.Sprites` (`Assets/Scripts/Sprites/`), because they must exist in player builds and cannot sit in an editor-only assembly (PLAN-0002 §4.14).
- **SPR, ACT and PAL files are read by the GRF library** (`Spr`, `Act`, `Pal` classes of `GRF.dll`) through three adapters in `Core/Formats/` (`RoSprReader`, `RoActReader`, `RoPalReader`); no code of ours parses their bytes (PLAN-0002 §1). After the adapters everything works on our own types `RoSpr`, `RoAct` and `RoPal`. GRF also has an `Imf` class; IMF is **out of scope** (OQ-1 decided, §8 D8).
- Format facts used here, from the community documents: **SPR** starts with `SP` and a version, a count of indexed ("bitmap") frames and (from 2.0) a count of true-colour frames; indexed frames are a width, a height and one palette index per pixel, run-length coded from 2.1 (only runs of index 0); true-colour frames are four bytes per pixel in alpha-blue-green-red order; the 256-entry palette (four bytes each) is at the end of the file, and index 0 is the transparent background whatever its alpha; a size of −1 × −1 is an invalid blank frame; frame sizes differ. **ACT** starts with `AC` and a version (2.0 to 2.5); it has a list of animation clips (the "actions"), each a list of frames, each a list of layers (position, sprite index, a mirror flag, RGBA tint, scale as one number or two, rotation in degrees, image type, and in the last layout the image size), an event id per frame, from the anchor layouts on a list of anchors (a position each, used to attach head, weapon and headgear to the body), and per clip a frame time in ticks (×24 ms); an event table of names (`atk` and sound files ending in `.wav`). **PAL** is a 256-entry colour table; loading one replaces the sprite's embedded palette. The documents do **not** state how clip indices map to actions and directions, the origin of layer offsets, the units of offsets, which way a mirror flips, or the values of the image type. Those are the rules table of §4.3, verified by T0.3.
- Sprite animation is delivered as animation clips played by an `Animator` (spec §6, decided). Sprites are flat billboards facing the camera (spec §6, decided). All categories (characters, weapons, items, NPCs, monsters, other) go through the same converter; characters and weapons are individual body-part and weapon sprites (assembly into a character is a later spec).
- Texture memory targets are set by T0.1 once the file count and sizes are known (§4.4, §9).

## 2. Architecture

Satisfies FR-1 to FR-6, FR-10.

```
 sprite/x.spr ─┐
 sprite/x.act ─┼─(IRoSource)─> bytes ── RoSprReader / RoActReader (GRF adapters)      Core/Formats
 palette/*.pal ┘                                  │
                                                  ▼
                              RoSpr (frames, palette)      RoAct (clips, frames, layers, anchors, events)     Core/Sprites
                                       │                              │
                       RoSpriteAtlasPacker (pure)            RoSpriteClipPlan (pure: per layer slot curves)
                       RoSpriteRules (table)                          │
                                       └───────────────┬─────────────┘
                                                       ▼
                RoSpriteConverter (Editor/Sprites: atlas PNGs, sprite sheet, clips, controller, prefab, ledger)
                                                       │
            runtime (Uninarok.Sprites): RoSpriteBillboard, RoSpriteEvents, RoSpritePalette, shader Uninarok/RoSprite
```

Data flow for `RoImport.Sprite("sprite/몬스터/poring.spr")` (the ACT with the same base name is the pair):

1. Ledger check (hashes of the SPR and the ACT, rules version): current → `Skipped`.
2. Read both through GRF; a missing partner or a failed read is one issue and **no output** (FR-10).
3. Pack the frames into the atlases, build the sprite rects (§4.4).
4. Build the clips per action (§4.5), the controller and the prefab (§4.6).
5. Write the outputs in place, record outputs and dependencies in the ledger.

## 3. Assemblies and file layout

Satisfies FR-5, FR-6.

| Path | Contents | Assembly |
|---|---|---|
| `Assets/Scripts/Sprites/Uninarok.Sprites.asmdef` | runtime assembly, no editor and no importer references | `Uninarok.Sprites` (all platforms) |
| `Assets/Scripts/Sprites/RoSpriteBillboard.cs` | camera-facing behaviour and its static registry (§4.7) | `Uninarok.Sprites` |
| `Assets/Scripts/Sprites/RoSpriteEvents.cs` | receiver of animation events (`OnSpriteSound(string)`, `OnSpriteAttack()`) as a C# event | `Uninarok.Sprites` |
| `Assets/Scripts/Sprites/RoSpritePalette.cs` | applies a palette texture to a sprite's renderers without duplicating frames | `Uninarok.Sprites` |
| `Assets/Shaders/Sprites/RoSprite.shader` | shader `Uninarok/RoSprite` (URP, unlit, transparent) | runtime asset |
| `Assets/Editor/RoImport/Core/Formats/` | `RoSprReader`, `RoActReader`, `RoPalReader` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Sprites/` | `RoSpr`, `RoAct`, `RoPal`, `RoSpriteRules`, `RoSpriteAtlasPacker`, `RoSpriteClipPlan`, `RoSpriteNaming` | Core |
| `Assets/Editor/RoImport/Editor/Sprites/` | `RoSpriteConverter`, `RoPaletteConverter`, `RoSpriteAtlasWriter`, `RoSpriteClipBuilder`, `RoSpriteControllerBuilder`, `RoSpritePrefabBuilder`, `RoSpriteGizmos` | `Uninarok.RoImport.Editor` |
| `Assets/Tests/EditMode/RoImport/Sprites/` | tests; synthetic sprites built in code | `Uninarok.RoImport.Tests` |

Outputs (through `RoOutputPaths.Mirror`, PLAN-0002 §4.5), for a source `sprite/<folders>/<name>.spr` and its `.act`:

| Output | Path |
|---|---|
| Colour atlas (main texture) | `Art/Imported/sprite/<folders>/<name>/<name>.png` (one per file; further pages `<name>_p1.png` only if a file exceeds the page size, §4.4) |
| Index atlas (secondary texture, indexed frames only) | `Art/Imported/sprite/<folders>/<name>/<name>_index.png` |
| Animator controller with the clips as sub-assets named `a<n>` | `Art/Imported/sprite/<folders>/<name>/<name>.controller` |
| Prefab | `Art/Imported/sprite/<folders>/<name>/<name>.prefab` |
| Palette texture | `Art/Imported/palette/<folders>/<name>.png` (from `palette/.../<name>.pal`) |
| Sprite material (shared by every sprite) | `Art/_materials/sprite_default.mat` (and `sprite_palette.mat` for palette use) |

## 4. Component contracts

### 4.1 Reading: the adapters and the data types

Satisfies FR-1, FR-2, FR-3, FR-4, FR-7, FR-10.

```csharp
public sealed class RoSpr
{
    public int VersionMajor, VersionMinor;
    public IReadOnlyList<RoSprFrame> Indexed;       // palette-indexed frames, file order
    public IReadOnlyList<RoSprFrame> Color;         // true-colour frames, file order
    public RoPal Palette;                           // embedded palette, 256 entries, index 0 forced transparent
}
public sealed class RoSprFrame { public int Width, Height; public byte[] Indices; public byte[] Rgba; /* top row first; exactly one is set */ public bool Blank; }
public sealed class RoPal { public Color32[] Colors; /* 256 */ }
public sealed class RoAct
{
    public int VersionMajor, VersionMinor;
    public IReadOnlyList<RoActClip> Actions;        // the file's clips, in order
    public IReadOnlyList<string> Events;            // event table (sound names, "atk")
}
public sealed class RoActClip { public float FrameTicks; public IReadOnlyList<RoActFrame> Frames; }       // ticks per displayed frame; × 24 ms
public sealed class RoActFrame { public IReadOnlyList<RoActLayer> Layers; public int EventId; public IReadOnlyList<Vector2Int> Anchors; }
public readonly struct RoActLayer { public int X, Y; public int SpriteIndex; public bool Mirror; public Color32 Tint; public float ScaleX, ScaleY; public int Rotation; public int ImageType; }
public static class RoSprReader { public static bool TryRead(RoPath path, byte[] bytes, out RoSpr spr, out string reason); }
public static class RoActReader { public static bool TryRead(RoPath path, byte[] bytes, out RoAct act, out string reason); }
public static class RoPalReader { public static bool TryRead(RoPath path, byte[] bytes, out RoPal pal, out string reason); }
```

- Values are raw: no rule of §4.3 is applied in the adapters; pixel rows are top first (the orientation GRF returns is recorded by T0.2 and converted once, in the adapter).
- Adapters never throw; a rejected read returns `false` with GRF's reason (`CorruptFile`). A blank (−1 × −1) frame is kept as `Blank` so sprite indices stay aligned with the ACT.
- **Mapping check (T0.2).** The task prints, for reference sprites of each category (a monster, an NPC, a body, a headgear, a weapon, an item, an effect sprite), the version, the frame counts, sizes, the palette, and the ACT's actions, frames, layers, anchors and events through GRF; it compares them with the raw values of the same files, and writes into this section one row per field with the GRF member that supplies it and the members that are derived and must not be used.

### 4.2 Palettes: `RoPaletteConverter`

Satisfies FR-2.

- A `.pal` file converts to a 256 × 1 PNG, point filtering, clamp, no mipmaps, no compression, sRGB, index 0 forced transparent, at the path of §3, kind `RoJobKind.Palette`, with `RoImport.Palette(string)`; it is requested on demand when something needs a palette and by the menu `Tools/Ragnarok/Convert All Palettes` (`Enumerate("palette/")`).
- **Applying a palette (FR-2).** The sprite's own palette is already in the colour atlas (§4.4), so a sprite shows correctly with no setup. An alternative palette is applied at run time by `RoSpritePalette.Apply(renderers, paletteTexture)` (a `MaterialPropertyBlock` setting `_PaletteTex`, no new texture and no new frames) or by `RoSpritePalette.Clear`; the shader (§4.7) uses the palette only for pixels of **indexed** frames and leaves true-colour frames untouched. The same frames and one atlas serve any number of palettes. `RoSpritePalette` has no allocation after its first call per renderer set (the property block is cached).

### 4.3 Rules table: `RoSpriteRules`

Satisfies FR-3, FR-4.

The choices the files do not state, each one named constant in one table, **verified by T0.3** against the reference viewer on a monster, an NPC, a body, a headgear and an item, and pinned by tests:

1. **Action and direction of a clip.** Clip index `i` is action `i / 8` and direction `i % 8` for files whose clip count is a multiple of 8, and a single direction otherwise (candidate). The meaning of the eight directions (which way is south, and the order around) is recorded in the table and used by the clip names.
2. **Layer origin and offsets.** The sprite's origin is the unit's position on the ground (feet); a layer's offset is the position of the centre of its image relative to the origin in pixels, X to the right and Y down (candidate); the pivot of every Unity sprite is its centre.
3. **Pixel to world size.** One pixel is `PixelsPerUnit⁻¹` Unity units; the value is measured in T0.3 by comparing a reference monster's height with ground cells in the reference client (no candidate is assumed here).
4. **Mirror.** The layer's mirror flag flips the image horizontally about the layer's own centre (candidate; the document calls the field a vertical flip, which T0.3 settles).
5. **Rotation.** Degrees, with a sign verified in T0.3, about the layer centre.
6. **Scale.** `ScaleX`/`ScaleY` (or the single scale) multiply the image size.
7. **Image type.** Type 0 selects `Indexed[SpriteIndex]`, type 1 selects `Color[SpriteIndex - Indexed.Count]` or `Color[SpriteIndex]` (T0.3 decides which numbering the files use).
8. **Time.** One frame lasts `FrameTicks × 24 ms`; a clip loops except the actions the table lists as non-looping (die, candidate), which hold their last frame (T0.3 checks idle, walk, attack and die).
9. **Draw order.** Layers are drawn in file order, one sorting order per layer slot.

A rules change touches this table and the tests that pin it, never the builder's structure.

### 4.4 Atlas: `RoSpriteAtlasPacker`, `RoSpriteAtlasWriter`

Satisfies FR-1, FR-6, FR-8.

```csharp
public static class RoSpriteAtlasPacker
{
    public const int Padding = 2, MaxPage = 4096;
    public static RoAtlasLayout Pack(IReadOnlyList<(int w, int h)> sizes);    // rects, page sizes, deterministic
}
```

- **One texture per file (FR-6).** All frames, indexed and true-colour, are packed by our own deterministic shelf packer (sorted by height then index, power-of-two page size, padding of 2 pixels filled by edge bleed, so no fringe at sprite edges when filtered). Unity's `PackTextures` is not used (its layout is not stable). If the frames do not fit one `MaxPage` page the packer returns more pages, the converter reports one `SpriteAtlasMultiPage` info issue, and the file uses `_p1`, `_p2` textures; the count is recorded by T0.1 (the target is zero such files).
- **Colour atlas (main texture).** Every frame is drawn as final RGBA: indexed frames use the sprite's embedded palette with index 0 transparent; true-colour frames are used as stored (alpha kept). It is written as a PNG (PLAN-0002 §4.7) with the importer settings of FR-8: **point filtering, no mipmaps, no compression (RGBA32), clamp, sRGB on, alpha is transparency**. Frames are straight alpha, colours of fully transparent pixels bled from neighbours (the index-0 pixels never leave a fringe).
- **Index atlas (secondary texture).** Present only when the file has indexed frames. Same layout, one channel carrying the palette index (R = index, 0 for transparent and for true-colour pixels), no sRGB, point filtering, no compression, no mipmaps. It is assigned to the sprites as a *secondary texture* named `_IndexTex`, so a plain sprite shader ignores it and `Uninarok/RoSprite` can use it for palette swaps.
- **Sprites.** The colour atlas importer is set to *Multiple* sprite mode through the sprite-editor data API with one named sprite per frame (`f0`, `f1`, …; blank frames are skipped and the names keep the ACT's indices), pivot at the centre, `PixelsPerUnit` from §4.3 and the two secondary textures; the sprite ids are derived from the name so references from clips survive re-conversion.
- Texture memory: the total over the data set (colour and index atlases) is measured in T7.2 and compared with the target set by T0.1 (see §9).

### 4.5 Animation: `RoSpriteClipPlan`, `RoSpriteClipBuilder`

Satisfies FR-3, FR-4, FR-7.

- **Layer slots.** A sprite object has a child object `Layer_k` for each layer slot, the number of slots being the largest layer count of any frame of the file; each has a `SpriteRenderer` with its own sorting order. In a frame the layers fill slots in file order; the unused slots are inactive.
- **One clip per ACT clip (FR-3).** `a<i>` (named with action and direction from §4.3, for instance `a5_d3`) holds, for each slot and frame, in a step (constant) interpolation: the active flag (`GameObject.m_IsActive` curve), the sprite (object-reference curve on `SpriteRenderer.m_Sprite`), `localPosition` (offset in pixels converted to Unity units, Y up, a small z step per slot to keep the order stable), `localEulerAngles.z`, `localScale` (scale and a negative X for a mirror), and the renderer colour (tint). Keys are at `frame × FrameTicks × 0.024` seconds with the last frame holding for its duration; the loop flag follows the table (§4.3). Curves that never change in a clip are written as a single key. Clips are deterministic (the same input gives the same bytes).
- **Anchors (FR-4).** If the file has anchors, the prefab has children `Anchor_0 … Anchor_n−1` (n is the largest anchor count) animated by `localPosition` curves in every clip, so the position of a head or a weapon attach point is the position of that transform at any time; an anchor missing in a frame keeps its last position and is flagged inactive.
- **Events (FR-7).** A frame's event id becomes an `AnimationEvent` at that frame's time: a name ending in `.wav` calls `OnSpriteSound(string name)`, `atk` calls `OnSpriteAttack()` on the root object's `RoSpriteEvents`, so every event has a receiver and Unity logs nothing.
- **Controller (FR-5).** One `AnimatorController` (sub-assets: the clips, named `a<i>`): one layer; one state per clip named like it; the default state is the first clip of action 0, direction 0; two integer parameters, `Action` and `Direction`; transitions **from Any State** to each state, condition `Action == a && Direction == d`, no exit time, zero duration, no transition to self; for single-direction files only `Action` is used. Game code can also play a state by name hash; the plan does not require transitions at runtime (§9 risk R2).
- All of this is pure (`RoSpriteClipPlan` yields arrays of curves from `RoAct` and the layout) except for the Unity object creation in `RoSpriteClipBuilder`.

### 4.6 Converter and prefab: `RoSpriteConverter`, `RoSpritePrefabBuilder`

Satisfies FR-1, FR-4, FR-5, FR-10 (uses SPEC-0002 FR-5, FR-6, FR-10, FR-16).

```csharp
public sealed class RoSpriteConverter : IRoConverter       // registers RoJobKind.Sprite; source path is sprite/.../<name>.spr
{
    public const int RulesVersion = 1;
    public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);   // returns the prefab
}
```

- `RoJobKind.Sprite` and `RoJobKind.Palette` are added to the enum and registry, with `RoImport.Sprite(string)` and `RoImport.Palette(string)`; the menu gets `Tools/Ragnarok/Convert All Sprites` (`Enumerate("sprite/")`, extension `.spr`) and the window the two kinds.
- **The pair.** The ACT is the file with the same folder and base name and extension `.act`. A missing partner (SPR without ACT, ACT without SPR when asked for by its path), an unreadable file or an unsupported version gives one issue (`SpriteMissingAct`, `SpriteMissingSpr`, `CorruptFile`, `UnsupportedSprVersion`, `UnsupportedActVersion`) naming the file, and **no partial output** (FR-10): everything is built in memory first and written at the end.
- **Prefab (FR-5).** Root object `<name>` at the origin (the unit's feet; `Y` up), carrying `Animator` (the controller), `RoSpriteBillboard`, `RoSpriteEvents`; children `Layer_k` (slots), `Anchor_k` when present. No importer type is referenced. The prefab is saved at the path of §3; its default state plays on instantiation (FR-5); every renderer uses the shared sprite material `Art/_materials/sprite_default.mat` (`Uninarok/RoSprite`, transparent, no shadows, no light probes). The sprite objects need no lights, shadows or colliders.
- Materials come from one small factory, `RoSpriteMaterials` (the same in-place rules as `RoMaterialFactory`), because the shader differs from the world materials.
- The ledger entry (category `sprite`) lists as outputs the atlases, controller (with clips) and prefab; as dependencies the hashes of the SPR and ACT (and the shader's version constant). All outputs are written only after everything is built in memory; assets are upserted in place, so a scene or prefab that references the sprite prefab or a clip keeps its reference.

### 4.7 Runtime: `RoSpriteBillboard`, `RoSpriteEvents`, `RoSpritePalette`, shader `Uninarok/RoSprite`

Satisfies FR-5, FR-8, FR-2 (display mode of spec §6).

- **`RoSpriteBillboard`** rotates the sprite root so that it faces the camera (the root's forward to the camera's forward flattened to the camera's yaw, up stays up; a field selects "full facing" or "upright only"; the default is the one T0.3 matches). Instances register in a static list on enable and unregister on disable; **one** `LateUpdate` per frame in a hidden driver object loops over the list: no per-instance `Update`, no allocation. The camera is the main camera, cached, and its transform change is detected so unchanged frames cost one comparison.
- **`RoSpriteEvents`** exposes `OnSpriteSound(string)` and `OnSpriteAttack()` as C# events (`event Action<string> Sound; event Action Attack;`); it plays nothing itself (audio is a later spec).
- **Shader `Uninarok/RoSprite`** (URP, unlit, transparent, cull off, no depth write, `SpriteRenderer` compatible, per-renderer `_Color` tint): samples `_MainTex` (the colour atlas); when `_PaletteTex` is set it also samples `_IndexTex` (secondary texture) and, for pixels whose index is greater than 0, takes the colour from the palette row at that index (a 256 × 1 texture, point filtered) instead of `_MainTex`, keeping the sprite's own alpha and tint. True-colour pixels have index 0 and are untouched. Without a palette the shader is a plain tinted sprite shader. It is a hand-written, committed runtime asset under `Assets/Shaders/Sprites/`, not generated.
- **Display (FR-8).** Point filtering and no mipmaps come from the texture settings (§4.4); the project's sprite sorting is the transparency sort mode of the camera (the plan records the setting the project needs, T0.3 checks overlapping sprites against the reference viewer).

### 4.8 Gizmos: `RoSpriteGizmos`

Satisfies FR-9.

- `[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(RoSpriteBillboard))]` in the editor assembly draws, for a selected sprite: the origin (a cross at the feet), the anchors (spheres with the index, from the `Anchor_k` children), and the bounds of the current frame (the union of the active layers' renderer bounds), each switchable in the overlay "Ragnarok Sprite" (`EditorPrefs`). It returns immediately when nothing is selected, reads only the prefab in the scene and is not in builds (editor assembly).

## 5. Threading and lifecycle

Satisfies FR-6, FR-10.

- Main thread, synchronous, inside the batch of PLAN-0002 §4.10. Pixel buffers of a file are released after its atlases are written; the writer encodes PNGs per file, so memory is bounded by one sprite at a time; the 200-job checkpoint unloads unused assets.
- At runtime only the prefab, atlases, controller, clips, material, the three small components and the shader exist.

## 6. Test plan

Satisfies every FR of the spec. EditMode NUnit tests in `Uninarok.RoImport.Tests` (command in `CLAUDE.md` §2). Tests needing real data are marked `[Category("NeedsData")]`; pure tests build `RoSpr`/`RoAct` objects in code.

| Test class | FR | Checks |
|---|---|---|
| `RoSpriteAtlasTests` | 1, 6, 8 | packing is deterministic; no rects overlap and every rect is inside its page; padding is filled by edge bleed (no transparent-pixel colour fringe); a 100-frame synthetic file uses one texture; an oversized file reports `SpriteAtlasMultiPage`; blank frames keep their indices |
| `RoSpriteColorTests` | 1, 2 | an indexed frame is drawn with its palette, index 0 is fully transparent whatever its alpha; a true-colour frame keeps its alpha; the index atlas holds the index only for indexed pixels |
| `RoSpriteRulesTests` | 3, 4 | the table of §4.3 pinned: action/direction of clip indices (multiple of 8 and not), pixel size, mirror, rotation sign, image type numbering, time per frame, non-looping actions |
| `RoSpriteClipPlanTests` | 3, 4, 7 | per-slot curves for a synthetic two-action sprite equal the expected keys (positions in Unity units, tint, mirror as negative scale, rotation, active flag); durations are `ticks × 24 ms`; the last frame holds; anchors become position curves; events land at the right time with the right function name |
| `RoSpriteControllerTests` | 3, 5 | one state per clip, parameters `Action` and `Direction`, the Any State transitions select the right clip for each value pair, the default state is action 0 direction 0; a single-direction file uses `Action` only |
| `RoSpriteWriterTests` | 5, 6, 10 | a synthetic sprite converts to the atlases, controller with clips and prefab at the paths of §3; the prefab has `Layer_k` and `Anchor_k` children, an `Animator`, the billboard and events components, the shared material; a missing partner file, a corrupt file and an unsupported version give one issue each and **no** output |
| `RoSpriteIdempotencyTests` | (0002 FR-5, FR-16) | second conversion is `Skipped`; a forced second conversion leaves atlases, clips and controller byte-identical and every GUID unchanged; a scene referencing the prefab and a prefab referencing a clip keep their references |
| `RoPaletteTests` | 2 | a PAL converts to a 256 × 1 texture with index 0 transparent; `RoSpritePalette.Apply` on a sprite with two palettes changes the rendered colours and creates no texture and no frames (a render of the quad in a test camera compared per pixel); true-colour pixels are unchanged |
| `RoSpriteBillboardTests` (play mode or `UnityTest` in edit mode) | 5 | a billboard faces the camera after a `LateUpdate`; 1,000 registered billboards cost zero allocations per frame; disabling unregisters |
| `RoSpriteEventsTests` | 7 | the events of a clip reach the receiver at the right frame |
| `RoSprReaderTests` and `RoActReaderTests` (`NeedsData`) | 1, 3, 10 | reference sprites read through GRF with frame, action, layer and anchor counts equal to the recorded values (T0.2); a truncated copy and an empty array return false with a reason and never throw |
| `RoSpriteReferenceTests` (`NeedsData`) | 1 to 10 | the reference sprites (a monster, an NPC, a body, an item) convert with no failure; the frame count equals the SPR's non-blank frames; the clip count equals the ACT's; one colour atlas per file; a second batch skips everything; every `.spr` under `sprite/` is attempted and failures are listed by name |

**Manual checks** (recorded in the PR with screenshots and screen recordings):

- FR-1: a reference monster's frames show no coloured background or fringe on a bright and on a dark background.
- FR-2: the same sprite with its own palette and with two alternative palettes, from one set of frames.
- FR-3: walk, attack and idle of a monster, an NPC and a body against the reference viewer in speed, layout and loop.
- FR-4: a sprite on a map cell stands on it; a head anchor follows the head through an animation.
- FR-5: drop the prefab in a scene, see its default action, and switch `Action` and `Direction` in the animator.
- FR-7: an attack animation raises its sound event at the right frame.
- FR-8: sharp at 1× and 4×.
- FR-9: gizmos appear on selection only and cost nothing otherwise.
- Budget: with 300 animated sprites on screen, the animator update and the billboard loop in a development build (R2).

## 7. Migration and compatibility

- There is no importer for these formats in the project today (the old importers handle models, ground, walkability, maps and BMP only), so nothing is deleted by this plan; the source folder is hidden from Unity (PLAN-0002 §7) so the SPR, ACT and PAL files are not imported by Unity either.
- Atlas PNGs and palettes are covered by the PLAN-0002 LFS rule for generated PNGs; the controller (with clip sub-assets) is a binary-serialised asset covered by the `Art/Imported/**/*.asset` rule or, if serialized as text, by a size check in T7.2 (a `.gitattributes` line is added if it is large). Prefabs stay text.
- A later spec composes characters from body, head, headgear and weapon sprites using the `Anchor_k` transforms and may add IMF layer ordering; this plan leaves both open and breaks nothing for them.
- Contract with PLAN-0006: none now; a later spec places NPCs and monsters at map cells using the `MapData` of PLAN-0005 and the prefab of §3.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Read SPR, ACT and PAL through GRF; rules are tested on synthetic data | Parsing the formats ourselves (owner decision: read through GRF) |
| D2 | One colour atlas per file, drawn with the sprite's own palette, plus a one-channel index atlas as a secondary texture; a custom shader applies a palette texture on demand | A coloured atlas per palette (duplicates frames, FR-2 forbids); real-time palette lookup for every sprite (needs the shader even for the default look) |
| D3 | One child object per layer slot, animated by curves (sprite, position, rotation, scale, tint, active); anchors as animated child transforms | One quad per frame with a combined mesh (loses the layers' separate offsets, tints and order); baking frames into combined textures per frame (explodes memory) |
| D4 | One `AnimatorController` per sprite with a state per clip and Any State transitions on `Action` and `Direction`; clips are sub-assets of the controller | One controller per action; a custom runtime sampler (a runtime assembly and scripts for what the animator already does; spec §6 decided clips and animator) |
| D5 | A small runtime assembly `Uninarok.Sprites` with a billboard system (one loop for all instances), an event receiver and a palette helper | A script per sprite with its own `Update` (cost per instance); shader-only billboards (no control of the facing rule, harder to verify) |
| D6 | Our own deterministic atlas packer | `Texture2D.PackTextures` (layout not stable, breaks FR-16) |
| D7 | Rules the files leave open (clip→action/direction, origin, pixel scale, mirror, rotation sign, image type numbering, loop) are one table verified in T0.3 | Choosing from memory of other viewers without checking |
| D8 (OQ-1) | IMF files are out of scope; layer ordering for characters belongs to the later composition spec | Reading IMF now (not needed by a single sprite; no consumer yet) |
| D9 | A pair with a missing or bad partner produces nothing (one issue) | A half sprite (an atlas without animation or the reverse, FR-10 forbids partial output) |

Items for the owner: approve D2 and D5 (a hand-written shader and a small runtime assembly), D4 (Any State transitions; budget in §9), and D8 (IMF out of scope, answering SPEC-0007 OQ-1).

## 9. Risks

| Risk | Mitigation |
|---|---|
| R1 A rule of §4.3 differs from the candidates (directions, origin, mirror, rotation sign, image type numbering) | T0.3 verifies each on five sprites of different kinds against the reference viewer; each rule is one constant with a pinning test |
| R2 The animator with many states and Any State transitions is costly for hundreds of sprites on screen | The Profiler check (T8.3) with 300 sprites sets the budget; the fallback is to drop the transitions and let game code play states by name hash (no change to the assets, a flag in the controller builder) |
| R3 Texture memory is too high for the whole data set | T0.1 counts files and sizes and the owner sets the target; atlases are RGBA32 with an optional index atlas; the measured total is recorded by T7.2; if over target, options are compression of true-colour atlases and splitting rarely used sprites (a decision, not an assumption) |
| R4 Very large sprites (effects, big monsters) exceed one page | The packer reports `SpriteAtlasMultiPage`; T0.1 counts how many files this affects; a multi-page file keeps one material and one controller |
| R5 Sprite sorting between overlapping billboards differs from the original | T0.3 compares overlapping sprites; the plan records the transparency sort setting the project needs and the layer sorting orders |
| R6 The ACT layouts of the versions differ from the documents (scale, image size, anchors, events) | T0.2 compares raw values for each version found in the data; an unknown version is reported once |
| R7 Secondary textures on sprites need the sprite-editor data API and may change between Unity versions | Isolated in `RoSpriteAtlasWriter`; a test imports a synthetic atlas and reads back the secondary texture assignment; the Unity version is pinned by the project |
| R8 The number of sprite files makes the batch long | Per-file memory is bounded; the batch resumes through the ledger (PLAN-0002 §4.10); T7.2 records the time |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.1, 4.4, 6 |
| FR-2 | 4.2, 4.4, 4.7, 6 |
| FR-3 | 4.3, 4.5, 6 |
| FR-4 | 4.3, 4.5, 4.6, 6 |
| FR-5 | 4.5, 4.6, 4.7, 6 |
| FR-6 | 4.4, 6 |
| FR-7 | 4.5, 4.7, 6 |
| FR-8 | 4.4, 4.7, 6 |
| FR-9 | 4.8, 6 |
| FR-10 | 4.1, 4.6, 6 |
