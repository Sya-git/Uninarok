# PLAN-0007: SPR, ACT and PAL Sprite Import (core)

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0007 revision 3, approved 2026-11-19) |
| **Status** | Approved (by the owner, 2026-11-19) |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Format facts come from [act-reference.md](act-reference.md). Shared import rules (paths, source, writer, ledger, results, batch, menu, window) come from [PLAN-0002](../0002-ro-import-foundation/plan.md). This plan replaces the earlier draft (animator controllers and clips per file, withdrawn by spec §6).

## 1. Constraints and assumptions

- Import code lives in the editor assemblies of PLAN-0002: `Uninarok.RoImport.Core` (pure, GRF adapters) and `Uninarok.RoImport.Editor` (Unity assets). What a sprite needs **at run time** (the animation data type, the player, the shader) lives in a new runtime assembly `Uninarok.Sprites` (`Assets/Scripts/Sprites/`), which has no reference to the importer.
- **SPR, ACT and PAL are read only through GRF** (`Spr`, `Act`, `Pal`) by three adapters; no code of ours parses their bytes (PLAN-0002 §1, enforced by `RoArchitectureTests`).
- **Decided by the spec (OQ-1):** images as Unity sprite sheets (one texture per distinct SPR content, one sprite per frame), animations as one data asset per ACT, one runtime system for all sprites, no `AnimationClip` or `Animator`.
- Facts used (act-reference): action index = animation × 8 + direction (`UninarokDirection`); `SpriteType` 0 indexed / 1 true colour, `SpriteIndex` counted inside its type; −1 = empty layer; frame time = `AnimationSpeed` × 25 ms; layer `Width`/`Height` derived (not stored); an ACT names no SPR.
- Measured on the data set (scratch tool, 2026-11-19): the largest distinct SPR is 6.7 million pixels (`몬스터/c_amdarais.spr`, 167 frames, frames up to 300 × 500), so **every SPR fits one 4096 × 4096 texture**; 9 monster SPR exceed 2048 × 2048. All distinct SPR together are about **1.07 billion pixels** (monsters 548 M, bodies/heads/weapons 236 M, headgears 118 M, NPCs 90 M, effects 31 M, garments 25 M, the rest 17 M). Unity version 6000.7.0b7, URP 17.7, `com.unity.2d.sprite` 1.0.0 (already in the manifest).
- Which ACT goes with which SPR is the caller's choice (act-reference §6). This plan's own entry points pair by same folder and base name; a later spec passes other pairs through the same API.

## 2. Architecture

Satisfies FR-1 to FR-11.

```
  data/sprite/…/x.spr ──┐                     data/palette/…/y.pal
  data/sprite/…/x.act ──┤ IRoSource                     │
                        ▼                               ▼
     RoSprReader / RoActReader (GRF adapters)      RoPalReader            Core/Formats
                        │                               │
          RoSpr (frames, palette)   RoAct (actions, frames, layers, anchors, events)       Core/Sprites
                        │                    │
          RoSpriteSheetPacker (pure)    RoActCompiler (pure: dedupe actions, flatten, rules)
          RoSpriteSheetPixels (pure)          │
                        │                    │
     RoSpriteSheetWriter ── RoSpriteSheetIndex (content hash → sheet)                     Editor/Sprites
                        │                    │
                        └────► RoSpriteConverter ◄──── RoPaletteConverter
                                     │
                  sheet PNG (+ index PNG)  ·  SpriteAnimation asset  ·  palette PNG
                                     │
   runtime (Uninarok.Sprites): SpriteAnimation (data) · SpriteActor (one per sprite) · SpriteSystem (one loop)
                               SpritePalette · shader Uninarok/RoSprite
```

Data flow of `RoImport.Sprite("sprite/몬스터/poring.act")`:

1. Ledger check (`sprite` category): ACT hash, SPR hash (dependency), rules version → current = `Skipped`.
2. Read the ACT and the SPR through GRF. Failure = one issue, no output (FR-8).
3. Sheet: SPR content hash → `RoSpriteSheetIndex`. Known and present → reuse (FR-2). Otherwise pack, write the PNG(s), slice into sprites, record in the index.
4. Validate the ACT against the sheet (every index inside its type's list, FR-8).
5. Static ACT (1 action, 1 frame, FR-11) → the result is the frame's sprite; no animation asset.
6. Otherwise compile the ACT (dedupe actions, flatten) into a `SpriteAnimation` asset that references the sheet's sprites; write it in place; record the ledger entry.

## 3. Assemblies and file layout

Satisfies FR-1, FR-3, FR-9, FR-10, FR-11.

| Path | Contents | Assembly |
|---|---|---|
| `Assets/Scripts/Sprites/Uninarok.Sprites.asmdef` | runtime assembly, all platforms, references nothing of ours | `Uninarok.Sprites` |
| `Assets/Scripts/Sprites/SpriteAnimation.cs` | the data asset (§4.5) | `Uninarok.Sprites` |
| `Assets/Scripts/Sprites/SpriteActor.cs`, `SpriteSystem.cs`, `SpriteMissingAction.cs` | the player and its single update loop (§4.6) | `Uninarok.Sprites` |
| `Assets/Scripts/Sprites/SpritePalette.cs` | palette swap through a property block (§4.7) | `Uninarok.Sprites` |
| `Assets/Shaders/Sprites/RoSprite.shader` | `Uninarok/RoSprite`, URP sprite shader with palette lookup (§4.7) | runtime asset |
| `Assets/Editor/RoImport/Core/Formats/` | `RoSprReader`, `RoActReader`, `RoPalReader` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Sprites/` | `RoSpr`, `RoAct`, `RoPal`, `RoSpriteRules`, `RoSpriteSheetPacker`, `RoSpriteSheetPixels`, `RoActCompiler` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Editor/Sprites/` | `RoSpriteConverter`, `RoSpriteSheetWriter`, `RoSpriteSheetIndex`, `RoPaletteConverter`, `RoSpriteOutputPaths`, `SpriteAnimationEditor`, `RoSpritePreviewWindow` | `Uninarok.RoImport.Editor` (references `Uninarok.Sprites`) |
| `Assets/Tests/EditMode/RoImport/Sprites/` | import tests; synthetic `RoSpr`/`RoAct` built in code | `Uninarok.RoImport.Tests` |
| `Assets/Tests/EditMode/Sprites/` | runtime tests (player, system, palette) | new `Uninarok.Sprites.Tests` |

Outputs (roots of PLAN-0002 §4.5):

| Output | Path |
|---|---|
| Sheet (main texture, sliced) | `Art/Imported/sprite/_sheets/<h2>/<name>_<h8>.png` — `<h8>` = first 8 hex of the SPR's SHA-256, `<h2>` its first 2 (keeps folders small), `<name>` = base name of the SPR that first produced it |
| Index texture (only when requested, §4.4) | same folder, `<name>_<h8>_index.png` |
| Animation asset | `Art/Imported/sprite/<folders>/<name>.asset` — mirrors the ACT path |
| Palette texture | `Art/Imported/palette/<folders>/<name>.png` — mirrors the PAL path |
| Sheet index | `Assets/Editor/RoImport/Ledger/spritesheets.json` (next to the ledgers) |
| Shared materials | `Art/_materials/sprite_default.mat` (`Uninarok/RoSprite`) |

## 4. Component contracts

### 4.1 Adapters and data types

Satisfies FR-1, FR-3, FR-7, FR-8.

```csharp
public sealed class RoSpr
{
    public int VersionMajor, VersionMinor;
    public IReadOnlyList<RoSprFrame> Indexed;     // SpriteType 0, file order
    public IReadOnlyList<RoSprFrame> TrueColor;   // SpriteType 1, file order
    public RoPal Palette;                         // embedded, 256 entries
}
public sealed class RoSprFrame { public int Width, Height; public byte[] Indices; public byte[] Rgba; }   // top row first; exactly one of Indices/Rgba set
public sealed class RoPal { public Color32[] Colors; }                                                  // 256; index 0 = transparent by rule (§4.3)
public sealed class RoAct
{
    public int VersionMajor, VersionMinor;
    public IReadOnlyList<RoActAction> Actions;    // file order
    public IReadOnlyList<string> Events;          // event table: "atk", "x.wav", …
}
public sealed class RoActAction { public float AnimationSpeed; public IReadOnlyList<RoActFrame> Frames; }
public sealed class RoActFrame  { public IReadOnlyList<RoActLayer> Layers; public int EventId; public IReadOnlyList<Vector2Int> Anchors; }
public readonly struct RoActLayer
{
    public readonly int SpriteType, SpriteIndex;  // SpriteIndex −1 = empty
    public readonly int OffsetX, OffsetY;
    public readonly bool Mirror;
    public readonly float ScaleX, ScaleY;
    public readonly int Rotation;                 // degrees
    public readonly Color32 Tint;
}
public static class RoSprReader { public static bool TryRead(RoPath path, byte[] bytes, out RoSpr spr, out string reason); }
public static class RoActReader { public static bool TryRead(RoPath path, byte[] bytes, out RoAct act, out string reason); }
public static class RoPalReader { public static bool TryRead(RoPath path, byte[] bytes, out RoPal pal, out string reason); }
```

- GRF members used: `Spr.Images` split by `GrfImage` type, `Spr.Palette`; `Act.Actions`, `Action.AnimationSpeed`, `Action.Frames`, `Frame.Layers`, `Frame.SoundId`, `Frame.Anchors` (`OffsetX`, `OffsetY`), `Act.SoundFiles`; `Layer.SpriteType`, `SpriteIndex`, `OffsetX`, `OffsetY`, `Mirror`, `ScaleX`, `ScaleY`, `Rotation`, `Color`. **Not used:** `Action.Interval` (derived), `Layer.Width`/`Height` (derived), `Act.Sprite` (in-memory link), `Layer.Angle`/`RotationRadian` (other views of `Rotation`). The GRF mapping check (phase 0) confirms each pairing and the pixel row order.
**GRF mapping (TASKS T0.1, checked on the reference set, 2026-11-19).** GRF 1 build in `Assets/Editor/Library/GRF.dll`.

| Our field | GRF member | Notes |
|---|---|---|
| `RoSpr.VersionMajor/Minor` | `Spr.Header.MajorVersion` / `MinorVersion` (bytes) | every reference SPR is 2.1 |
| `RoSpr.Indexed` | `Spr.Images` where `GrfImageType == Indexed8`, in list order | `Spr.Images` holds all indexed frames first, then all true-colour frames (file order) |
| `RoSpr.TrueColor` | `Spr.Images` where `GrfImageType == Bgra32`, in list order | |
| `RoSprFrame.Width/Height` | `GrfImage.Width` / `Height` | |
| `RoSprFrame.Indices` | `GrfImage.Pixels` (Indexed8: 1 byte per pixel) | **top row first** (RLE already decoded) |
| `RoSprFrame.Rgba` | `GrfImage.Pixels` (Bgra32: 4 bytes **B, G, R, A** per pixel), reordered to R, G, B, A | **top row first** (GRF flips the file's bottom-up storage on load) |
| `RoSpr.Palette` | `Spr.Palette.BytePalette` (1,024 bytes, **R, G, B, A** per entry) | the SPR's stored alpha bytes are 0; GRF sets 255 on entries 1–255 and keeps entry 0 as stored; our rule (§4.3) makes index 0 transparent anyway. `GrfImage.Palette` of an indexed frame is the same table (not used) |
| `RoPal.Colors` | `new Pal(bytes, Pal.FormatMode.PreserveOriginal).BytePalette` (R, G, B, A) | PAL files store alpha 0 on every entry: the adapter sets alpha 255 on all entries (index 0 is transparent by rule) |
| `RoAct.VersionMajor/Minor` | `Act.Header.MajorVersion` / `MinorVersion` | reference set: 2.1, 2.3, 2.4, 2.5 |
| `RoAct.Actions` | `Act.Actions` (list order) | |
| `RoActAction.AnimationSpeed` | `Action.AnimationSpeed` (float) | |
| `RoActAction.Frames` | `Action.Frames` | |
| `RoActFrame.Layers` | `Frame.Layers` (list order) | |
| `RoActFrame.EventId` | `Frame.SoundId` | −1 = none, otherwise an index into `Act.SoundFiles` |
| `RoActFrame.Anchors` | `Frame.Anchors[i].OffsetX` / `OffsetY` | `Anchor.Other` and `Anchor.Unknown` not used |
| `RoAct.Events` | `Act.SoundFiles` | holds `atk` and sound names in one table |
| `RoActLayer.SpriteType` | `Layer.SpriteType` (`SpriteTypes.Indexed8` = 0, `Bgra32` = 1) | |
| `RoActLayer.SpriteIndex` | `Layer.SpriteIndex` | −1 = empty; an empty layer carries zero scale and zero tint (not used) |
| `RoActLayer.OffsetX/Y` | `Layer.OffsetX` / `OffsetY` | |
| `RoActLayer.Mirror` | `Layer.Mirror` | |
| `RoActLayer.ScaleX/Y` | `Layer.ScaleX` / `ScaleY` (float) | |
| `RoActLayer.Rotation` | `Layer.Rotation` (int degrees, 0–359 seen) | |
| `RoActLayer.Tint` | `Layer.Color` (`GrfColor`: `R`, `G`, `B`, `A`) | |

**Not used:** `Action.Interval` (= `AnimationSpeed × 25`), `Layer.Width`/`Height` (0 when the ACT is loaded without its SPR, otherwise the image size), `Layer.Angle`/`RotationRadian`, `Act.Sprite`, `Act.Version` (double; the header bytes are used instead), `Anchor.Other`/`Unknown`.

**Bad input as GRF reports it.** Junk bytes: `GrfException` ("Unrecognized file format") for SPR and ACT. Empty or very short input: `IndexOutOfRangeException`. A truncated ACT throws. **A truncated SPR does not throw:** GRF keeps the frames that fit and reads the palette from whatever the last 1,024 bytes are. The SPR adapter therefore reads the two frame counts the header declares (bytes 4–7, after the `SP` magic and version, the same kind of header check the RSW adapter does with its magic) and rejects the file when GRF loaded fewer frames (D11). A PAL shorter than 1,024 bytes does not throw either; the adapter requires exactly 1,024 bytes.

- Adapters never throw: a rejected read is `false` with GRF's message. The ACT adapter is given the ACT bytes only (`new Act(bytes)`); pairing with an SPR is done by the converter.
- Values are raw; no rule of §4.3 is applied here.

### 4.2 Sheet: `RoSpriteSheetPacker`, `RoSpriteSheetPixels`, `RoSpriteSheetWriter`, `RoSpriteSheetIndex`

Satisfies FR-1, FR-2, FR-7, FR-9.

```csharp
public static class RoSpriteSheetPacker
{
    public const int Padding = 2, MaxSize = 4096;
    public static bool TryPack(IReadOnlyList<Vector2Int> sizes, out RoSheetLayout layout);   // false only if > MaxSize (none in the data set)
}
public sealed class RoSheetLayout { public int Width, Height; public RectInt[] Rects; }     // Rects[i] = frame i; indexed frames first, then true colour
public static class RoSpriteSheetPixels
{
    public static byte[] Color(RoSpr spr, RoSheetLayout layout);   // RGBA32: indexed frames through the embedded palette, index 0 → alpha 0; true colour as stored
    public static byte[] Index(RoSpr spr, RoSheetLayout layout);   // R8: palette index of indexed pixels; 0 for transparent and true-colour pixels
}
```

- **Frame order and slots.** Slot `s` of a sheet = indexed frame `s` for `s < IndexedCount`, else true-colour frame `s − IndexedCount`. A layer's slot is `SpriteType == 0 ? SpriteIndex : IndexedCount + SpriteIndex`.
- **Packing.** Our own deterministic shelf packer (sort by height, then slot), power-of-two size, 2-pixel padding filled by edge bleed so point sampling never shows a neighbour. `Texture2D.PackTextures` is not used (layout not stable, FR-9).
- **Texture settings (FR-1).** Main texture: sprite mode Multiple, point filter, no mipmaps, clamp, sRGB, alpha is transparency, no compression (RGBA32) in this plan (see D6 and R1), max size 4096. Sprites: one per slot, named `f<slot>`, rect from the layout, **pivot at the frame centre**, `PixelsPerUnit` from §4.3, full-rect mesh. Sprite ids are derived from the name so references survive re-import (FR-9). Set through `ISpriteEditorDataProvider` (`com.unity.2d.sprite`); the index texture is attached as the secondary texture `_IndexTex` through `ISecondaryTextureDataProvider`.
- **Unity API (TASKS T0.3, spike on Unity 6000.7.0b7, 2026-11-19).** Calls, in order:
  1. Write the PNG(s) with `Texture2D.EncodeToPNG` (main: `RGBA32`; index: `TextureFormat.R8`, linear, `SetPixelData`, which encodes as an 8-bit grey PNG and re-imports as exact bytes), then `AssetDatabase.ImportAsset(path, ForceSynchronousImport)`.
  2. Index texture importer: `textureType = Default`, `sRGBTexture = false`, `alphaSource = None`, `mipmapEnabled = false`, `filterMode = Point`, `wrapMode = Clamp`, `npotScale = None`, default platform settings `format = R8`, `textureCompression = Uncompressed`; `SaveAndReimport()`.
  3. Main importer: `textureType = Sprite`, `spriteImportMode = Multiple`, `spritePixelsPerUnit`, point, clamp, no mipmaps, sRGB, `alphaIsTransparency`, `npotScale = None`, `maxTextureSize = 4096`; `TextureImporterSettings.spriteMeshType = FullRect`, `spriteGenerateFallbackPhysicsShape = false`; **default platform settings `format = RGBA32` set explicitly** (with only `Uncompressed`, an opaque sheet imports as `RGB24`); `SaveAndReimport()`.
  4. `new SpriteDataProviderFactories().Init()`, `GetSpriteEditorDataProviderFromObject(importer)`, `InitSpriteEditorDataProvider()`; `SetSpriteRects(SpriteRect[])` with `name`, `rect`, `alignment = Center` (or `Custom` with `pivot`), `spriteID`; `GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(…)` with the same ids; `GetDataProvider<ISecondaryTextureDataProvider>().textures = { new SecondarySpriteTexture { name = "_IndexTex", texture } }`; `Apply()`; `importer.SaveAndReimport()`.
  - **Sprite ids:** `spriteID = new GUID(<32 hex of MD5("uninarok-sprite:" + sprite name)>)`. After two forced re-imports the names, rects, pivots, PPU, sprite ids, sub-asset file ids and the secondary texture were unchanged.
  - **Render:** a `SpriteRenderer` binds the sprite's secondary texture to the material property of the same name (`_IndexTex`); a palette texture set through a `MaterialPropertyBlock` recoloured index > 0 pixels exactly (sRGB 256 × 1 palette, values read back unchanged), left true-colour pixels (index 0) unchanged, and created no texture. URP `Camera.Render()` into a `RenderTexture` works for tests.
  - No caveat beyond the RGB24 one was found.
- **Index texture (FR-7).** Written only when the caller asks for palette support (`RoSpriteOptions.Palettes`), because it adds 1 byte per pixel to every sheet that has it; R8, point, no sRGB, no compression, no mipmaps.
- **Content dedupe (FR-2).** `RoSpriteSheetIndex` maps an SPR content hash (SHA-256 of the file, as `IRoSource` gives it) to its sheet path and whether it has an index texture. Converting any SPR first looks up its hash; a present sheet is reused (`Skipped`), a sheet without an index texture gets one added when palettes are now requested. Sheets are owned by the index, not by a ledger entry, because one sheet serves many sources (the ledger allows one owner per output, PLAN-0002 §4.8). The index is JSON, sorted, rewritten only when it changes.
- Sheets that no animation references any more are not deleted by this plan (R5).

### 4.3 Rules: `RoSpriteRules`

Satisfies FR-1, FR-3, FR-5, FR-6.

One table of named constants, each verified by the rules check (phase 0) against the reference viewer and pinned by tests:

| Rule | Candidate | Verified by |
|---|---|---|
| Frame time | `AnimationSpeed × 25` ms | act-reference §4 (confirmed) |
| Directional | action count is a multiple of 8; the caller may override it per file (R7) | rules check (8-action effects) |
| Layer offset | pixel position of the frame's **centre** relative to the origin, X right, **Y down** | rules check |
| Origin | the unit's ground point (feet) | rules check |
| Pixels per unit | measured: a reference monster's height against GAT cells (`RoUnits.GatCell`) in the reference client | rules check |
| Mirror | horizontal flip about the layer's own centre | rules check |
| Rotation | degrees, sign verified; about the layer centre | rules check |
| Scale | multiplies the image size; applied before rotation | rules check |
| Tint | multiplies the frame colour, alpha included | rules check |
| Palette index 0 | transparent whatever its stored alpha | act-reference §5 |
| Draw order | layers in file order, later on top | rules check |
| Loop | every action loops; the caller decides to hold the last frame (death) | rules check |

A rules change touches this table and its pinning tests only, and raises `RoSpriteConverter.RulesVersion`.

### 4.4 Converter: `RoSpriteConverter`, `RoPaletteConverter`

Satisfies FR-2, FR-8, FR-9, FR-11 (uses SPEC-0002 FR-5, FR-6, FR-16).

```csharp
public sealed class RoSpriteOptions { public bool Palettes; }                 // write / attach the index texture
public sealed class RoSpriteConverter : IRoConverter                          // RoJobKind.Sprite; Handles(.act)
{
    public const string Category = "sprite";
    public const int RulesVersion = 1;
    public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath act);                     // pairs act with the same-name .spr
    public static RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath act, RoPath spr, RoSpriteOptions options);
}
public sealed class RoPaletteConverter : IRoConverter { /* RoJobKind.Palette; Handles(.pal) */ }
// RoImportContext gains: RoResult<UnityEngine.Object> Sprite(RoPath act, RoPath spr, RoSpriteOptions options = null);   memoised per run
// RoImport gains:        Sprite(string actPath, RoImportOptions o = null), Palette(string palPath, RoImportOptions o = null)
```

- **Result.** A `SpriteAnimation` asset, or for a static ACT (1 action with 1 frame with at most one non-empty layer) the frame's `Sprite` (FR-11). A static frame whose layer offset is not (0, 0) gets its own sprite `f<slot>@<x>_<y>` in the sheet with the pivot moved by the offset, so the sprite alone is placed correctly; the data check (phase 0) counts how many need it.
- **Ledger** (category `sprite`): source = the ACT; outputs = the animation asset (none for a static result); dependencies = the SPR (path and hash), so a changed SPR re-converts every ACT that uses it.
- **No partial output (FR-8).** Everything is built in memory, then written: sheet first (shared, so a later failure leaves only a valid reusable sheet), then the animation. Issues, each once, naming the file: `SourceMissing` (no SPR next to the ACT), `CorruptFile` (GRF rejected it), `UnsupportedSprVersion`, `UnsupportedActVersion`, `SpriteIndexOutOfRange` (a layer points outside its type's list), `SpriteSheetTooLarge` (over 4096, none in the data set). Unknown event names are kept and reported once as info (`a`, `atk.txt`).
- **Palettes.** A `.pal` becomes a 256 × 1 PNG (point, clamp, no mipmaps, no compression, sRGB, index 0 alpha 0), ledger category `palette`.
- **Menu and window.** `RoJobKind.Sprite` and `RoJobKind.Palette` join the enum, the registry, the window and the menu (`Tools/Ragnarok/Convert Sprite Folder…`, `Convert All Palettes`). Import by category is a later spec; this plan converts what it is given.

### 4.5 Animation data: `RoActCompiler` → `SpriteAnimation`

Satisfies FR-3, FR-4, FR-6.

```csharp
public sealed class SpriteAnimation : ScriptableObject          // Uninarok.Sprites
{
    public const float FrameUnitMs = 25f;
    public Sprite[] Sprites;           // by slot (§4.2); shared with every animation that uses the sheet
    public float PixelsPerUnit;        // from RoSpriteRules
    public int FileActionCount;        // as in the ACT
    public bool Directional;
    public short[] ActionMap;          // file action → unique action (FR-4)
    public ActionData[] Actions;       // unique actions
    public FrameData[] Frames;
    public LayerData[] Layers;
    public Vector2Int[] Anchors;       // pixels, Y down as in the file
    public string[] Events;            // event table
    public int MaxLayers;              // slots a player needs

    [Serializable] public struct ActionData { public int FirstFrame; public int FrameCount; public float FrameMs; }
    [Serializable] public struct FrameData  { public int FirstLayer; public short LayerCount; public short EventId; public int FirstAnchor; public short AnchorCount; }
    [Serializable] public struct LayerData  { public short Slot; public short OffsetX, OffsetY; public bool Mirror; public float ScaleX, ScaleY, Rotation; public Color32 Tint; }   // Slot −1 = empty

    public bool TryGetAction(int animation, int direction, SpriteMissingAction policy, out int uniqueAction);
}
```

- **Flat arrays**, no nested lists: cheap to serialize, cache-friendly for the update loop, no allocation at run time.
- **Dedupe (FR-4).** `RoActCompiler` compares actions by value (speed, every frame's layers, event, anchors) and keeps the first; `ActionMap` points every file action at its unique action. An all-empty action stays as data (FR-5).
- **Lookup and FR-5.** `TryGetAction` computes the file action (`Directional ? animation × 8 + direction : animation`). Inside the file's action count → that action, even if empty (draws nothing). Outside → `policy`: `DrawNothing` returns false; `Fallback` tries the same animation's first direction, then animation 0 in the requested direction, then action 0.
- Pixel values are stored as in the file; the player converts with `PixelsPerUnit` and the rules (Y down → Unity Y up).

### 4.6 Runtime player: `SpriteActor`, `SpriteSystem`

Satisfies FR-3, FR-5, FR-6 (display mode of spec §6).

```csharp
public enum SpriteMissingAction { DrawNothing, Fallback }
public sealed class SpriteActor : MonoBehaviour
{
    public SpriteAnimation Data;
    public SpriteMissingAction Missing = SpriteMissingAction.DrawNothing;
    public int Animation { get; }  public int Direction { get; }             // current
    public float Speed = 1f;                                                 // multiplies frame time (walk speed, attack speed)
    public bool Billboard = true;
    public void Play(int animation, int direction, bool restart = true);    // restart = false keeps the frame (direction change)
    public int CurrentFrame { get; }
    public bool TryGetAnchor(int index, out Vector3 localPosition);          // FR-6
    public event Action<SpriteActor, string> FrameEvent;                     // "atk" or a sound name, at the frame's start
}
public static class SpriteSystem { /* registry + one hidden driver; LateUpdate loop over all actors */ }
```

- The actor creates `MaxLayers` child `SpriteRenderer`s (`Layer_0…`), sorting order by slot, shared material `sprite_default.mat`, no shadows. Per frame change it sets each slot's sprite (or disables it for −1/unused), local position (offset / PPU, Y flipped, a small z step per slot), rotation, scale, `flipX` for mirror, and color for tint.
- **One loop (performance).** Actors register on enable; `SpriteSystem` advances time, computes the frame (`elapsed / (FrameMs × Speed)` modulo frame count), and touches renderers **only when the frame index changes**; it applies the billboard facing (rotation to the camera's yaw, cached camera, skipped when the camera did not move). No per-actor `Update`, no allocation per frame.
- **Shared timing.** Time and frame are a small state struct on the actor; a later composition spec can make several actors read one leader's state. This plan exposes the state but adds no composition.
- Events fire once per frame entry (FR-3); nothing is played (audio is a later spec).

### 4.7 Palette swap: `SpritePalette`, shader `Uninarok/RoSprite`

Satisfies FR-7, FR-1.

- `Uninarok/RoSprite`: URP 2D-compatible unlit sprite shader, transparent, cull off, per-renderer color. Samples `_MainTex`; when `_PaletteTex` is set and the sprite has `_IndexTex` (bound by `SpriteRenderer` from the sprite's secondary texture), a pixel whose index is greater than 0 takes its colour from the palette row (256 × 1, point) instead of `_MainTex`, keeping alpha and tint. True-colour pixels have index 0 and are unchanged. Hand-written and committed, not generated.
- `SpritePalette.Apply(SpriteActor actor, Texture2D palette)` / `Clear(actor)`: one cached `MaterialPropertyBlock` per actor, set on its layer renderers; no new texture, no new frames.

### 4.8 Preview: `SpriteAnimationEditor`, `RoSpritePreviewWindow`

Satisfies FR-10.

- The inspector of a `SpriteAnimation` shows counts (actions, unique actions, frames, layers) and an "Open preview" button. The window plays the asset in a preview scene (`PreviewRenderUtility`) with an animation number field, a direction picker (8 buttons laid out as compass), play/pause/step, speed, missing-action policy, a palette slot, and anchors drawn as markers. Editor only.

## 5. Threading and lifecycle

Satisfies FR-8, FR-9.

- Import: main thread, synchronous, inside the batch of PLAN-0002 §4.10 (`AssetDatabase` editing batched by `RoAssetWriter`). Pixel buffers of one SPR are released after its sheet is written; memory is bounded by the largest sheet (4096² × 5 bytes ≈ 84 MB). The 200-job checkpoint unloads unused assets.
- Run time: `SpriteSystem` runs on the main thread in `LateUpdate`; actors and data are read-only during the loop. Domain reload safe: the registry is rebuilt from enabled actors (`RuntimeInitializeOnLoadMethod` reset).

## 6. Test plan

Satisfies every FR. EditMode NUnit. Data-dependent tests are `[Category("NeedsData")]`; pure tests build `RoSpr`/`RoAct` in code.

| Test class | FR | Checks |
|---|---|---|
| `RoSprReaderTests`, `RoActReaderTests`, `RoPalReaderTests` (`NeedsData`) | 1, 3, 8 | reference files read with the counts recorded by the GRF mapping check; `SpriteType`/`SpriteIndex` per type; truncated and empty input return false, never throw |
| `RoSpriteSheetPackerTests` | 1, 9 | deterministic layout; no overlap; inside bounds; power of two; padding bled; 100 frames → one texture |
| `RoSpriteSheetPixelsTests` | 1, 7 | palette applied; index 0 transparent whatever its alpha; true colour keeps alpha; index texture holds indices only for indexed pixels |
| `RoSpriteRulesTests` | 3, 5, 6 | the §4.3 table pinned |
| `RoActCompilerTests` | 3, 4, 5 | flattening keeps every layer field; identical actions stored once and mapped; empty actions kept; frame ms = speed × 25; non-directional detection |
| `SpriteAnimationLookupTests` | 5 | inside count → action even if empty; outside → `DrawNothing` false, `Fallback` chain |
| `RoSpriteConverterTests` | 2, 8, 9, 11 | synthetic pair → sheet + asset at §3 paths; second identical SPR reuses the sheet; ACT against another file's sheet; index out of range, corrupt, missing SPR → one issue and no output; static ACT → sprite only; second run `Skipped`; forced run keeps GUIDs, sprite ids and bytes |
| `RoPaletteConverterTests` | 7 | 256 × 1, index 0 transparent |
| `SpriteActorTests` (Uninarok.Sprites.Tests) | 3, 5, 6 | frame advance with speed; renderers change only on frame change; empty frame hides slots; anchors; events once per frame; direction change keeps frame with `restart: false` |
| `SpriteSystemTests` | 3 | 1,000 actors: zero GC allocation per frame; disable unregisters |
| `SpritePaletteTests` | 7 | render of one sprite with two palettes differs per pixel, no new texture; true-colour pixels unchanged |
| `RoSpriteReferenceTests` (`NeedsData`) | 1–9, 11 | the reference set of spec §5 converts with no failure; sheet count = distinct SPR hashes; stored actions ≤ file actions |

**Manual checks** (PR evidence, Unity MCP or owner): reference sprites against the reference viewer (layout, timing, direction, mirror, rotation); sharp at 1× and 4×; a weapon hidden while walking; palette swap on a body; 300 actors on screen with the Profiler (update loop time, zero GC).

## 7. Migration and compatibility

- No importer for these formats exists today; nothing is deleted. The source folder stays hidden from Unity (`~`).
- `RoJobKind` gains `Sprite` and `Palette`; `RoIssueCode` gains `UnsupportedSprVersion`, `UnsupportedActVersion`, `SpriteIndexOutOfRange`, `SpriteSheetTooLarge`. `Uninarok.RoImport.Editor` gains a reference to `Uninarok.Sprites`; `RoArchitectureTests` gets the new asmdefs added to its checks.
- PNG sheets fall under the existing LFS rule for PNG; `.asset` animations under the `Art/Imported/**/*.asset` LFS rule.
- A later spec (grouping by game object, import by category, composition) calls `RoImportContext.Sprite(act, spr, options)` with its own pairs and reads the actors' shared timing state; nothing here needs to change for it.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Read through GRF only; rules tested on synthetic data | Own parser (PLAN-0002 §1) |
| D2 | One sheet per distinct SPR content, owned by a content index, shared by every ACT | One sheet per file (60,438 garment sheets instead of 294); ledger ownership (one owner per output) |
| D3 | ACT → one `SpriteAnimation` data asset with flat arrays and an action map | AnimationClips + Animator (spec OQ-1: millions of assets, no shared clock, per-instance cost) |
| D4 | One `SpriteSystem` loop, renderers touched only on frame change | `Update` per actor; Animator |
| D5 | Child `SpriteRenderer` per layer slot | One combined mesh per frame (faster draw, but loses per-layer tint and order, more code); kept as the fallback if the budget check misses the budget |
| D6 (owner, 2026-11-19) | Main texture RGBA32 uncompressed, index texture R8 only when palettes are requested; measured in the full-data run | Index-only sheets decoded by the shader (4× smaller, exact, but every view of a sheet needs the shader and the editor shows grey images); BC7 (4× smaller, not pixel-exact, FR-1) — both kept as options for R1 |
| D7 | Pivot at the frame centre; offsets applied by the player | Pivot per layer use (a sprite is shared by many layers and files) |
| D8 | The job unit is the ACT; the SPR is a dependency | SPR as the unit (an ACT without its own SPR could not be requested) |
| D9 | Static ACT → sprite only (FR-11), with an offset sprite only when the offset is not zero | An animation asset for one frame |
| D10 | Unused sheets are kept; cleanup is a later tool | Deleting by reference counting during import (slow, risky with partial runs) |
| D11 (T0.1) | The SPR adapter reads the header's two declared frame counts and rejects a file where GRF loaded fewer (GRF accepts a truncated SPR silently) | Trusting GRF (a cut file would convert with frames missing, FR-8); re-saving through GRF and comparing (slow, RLE output may differ) |

D6 approved by the owner (2026-11-19): RGBA32 now; the full-data run measures the real total and the owner decides on R1's options from that number. D3–D5 approved by the owner with the plan (2026-11-19).

## 9. Risks

| Risk | Mitigation |
|---|---|
| R1 Texture size: all distinct SPR ≈ 1.07 G pixels → about 4.3 GB as RGBA32 (+1 byte/px where palettes are on), before packing overhead | the full-data run measures the real total; options ready without changing assets' structure: BC7 per platform (not exact), index-only sheets (exact, shader everywhere), or converting only what the game uses (a later spec picks categories) |
| R2 A §4.3 rule differs from the candidate | rules check checks each against the reference viewer; each rule is one constant with a pinning test |
| R3 Sprite-editor data API or secondary textures behave differently in Unity 6000.7 beta | The Unity API spike (phase 0) tries it first (create, slice, attach `_IndexTex`, re-import, ids stable); isolated in `RoSpriteSheetWriter` |
| R4 Import time with ~108,000 ACT and ~17,000 distinct SPR | Content dedupe, ledger skip, batched asset editing; the full-data run records time; a full run is resumable |
| R5 Orphan sheets accumulate after source changes | Index lists users; a later cleanup tool; sheets are small next to the total |
| R6 Many layer renderers (monsters up to 10) cost draw calls | Same material and sheet per actor batch well; the budget check measures 300 actors; D5 fallback |
| R7 8-action files that are not directional (effects) | Rule in §4.3 with rules check check; the flag is per file and can be overridden by the caller |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.1, 4.2, 4.3, 4.7, 6 |
| FR-2 | 2, 4.2, 4.4, 6 |
| FR-3 | 4.1, 4.3, 4.5, 4.6, 6 |
| FR-4 | 4.5, 6 |
| FR-5 | 4.3, 4.5, 4.6, 6 |
| FR-6 | 4.3, 4.5, 4.6, 6 |
| FR-7 | 4.2, 4.4, 4.7, 6 |
| FR-8 | 4.1, 4.4, 5, 6 |
| FR-9 | 4.2, 4.4, 5, 6 |
| FR-10 | 4.8, 6 |
| FR-11 | 4.4, 6 |
