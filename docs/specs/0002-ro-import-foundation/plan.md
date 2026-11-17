# PLAN-0002: Ragnarok Import Foundation

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0002) |
| **Status** | Approved |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. The per-format plans (PLAN-0003 to PLAN-0007) build on the contracts here. Where this plan fixes a rule for all converters (units, paths, materials, ledger), the format plans reference it and do not restate it.

## 1. Constraints and assumptions

- Unity 6 (6000.x) with URP 17.7 (from `Packages/manifest.json`), C# as supported by the editor, .NET Standard 2.1 profile.
- All importer code is **editor-only** (FR-14). Unity APIs run on the main thread only (CLAUDE.md, spec §6).
- Output roots (spec §6 leaves the folders to this plan): `Assets/Art/Imported/` for visual assets, `Assets/Data/` for data assets (the existing `Assets/Data/Map/` is kept for map data). The art root is assumed to sit under `Assets/`; the owner wrote it as `Art/Imported/`.
- Source root: the folder `Assets/Editor/GRF Resources~/data` (§4.2, decision D2). Ragnarok paths are relative to `data`.
- The unit of conversion is one **source file** (a Ragnarok path). One source file produces one or more outputs; the ledger (§4.8) records which.
- Dependencies: the Unity Test Framework (`com.unity.test-framework`, first-party, added to `Packages/manifest.json`; PLAN-0001 adds the same package). No new third-party library is added. **The existing GRF library** (GRFEditor's `GRF.dll`, with `Encryption.dll`, `ErrorManager.dll`, `Utilities.dll`, in `Assets/Editor/Library/`) **reads every Ragnarok file format** (images now; RSM, RSW, GND, GAT, SPR, ACT and PAL in the format plans). Importer code never parses file bytes itself (no `BinaryReader`, no hand-written decoders): it hands the bytes to GRF through a thin adapter in `Core/Formats/` (§4.4) and works with the result. The foundation reads files from a folder on disk, not from GRF archives (spec §3); locating a file and reading its bytes is plain file access, interpreting them is GRF's job.
- Generated assets are committed (spec §6). Large binary outputs (meshes, textures) are covered by Git LFS path rules (§7).
- The reference data is the folder `Assets/Editor/GRF Resources~/data` with the maps and models listed in `0003-rsm-models/reference-models.md`.

## 2. Architecture

Satisfies FR-2, FR-5, FR-6, FR-13, FR-14.

```
                    Menu / window (§4.11)        Code: RoImport.* (§4.10)
                                   \              /
                                    v            v
                         RoImportContext  (source, writer, ledger, report, roots)
                                          |
        +---------------------------------+--------------------------------+
        v                                 v                                v
  Format converters                 Shared services                    RoAssetWriter (§4.7)
  (PLAN-0003..0007)                 RoSpace (§4.3), imaging (§4.4),    only code that calls
  pure data -> Unity data           materials (§4.6), paths (§4.5)     AssetDatabase
        ^                                 ^                                |
        |                                 |                                v
  IRoSource (§4.2): folder on disk    RoLedger (§4.8)               Assets/Art/Imported, Assets/Data
```

Layers, one-way dependencies (a lower layer never refers to a higher one):

1. **Source**: `IRoSource` resolves Ragnarok paths to files on disk.
2. **Core rules**: paths, units and axes, image decoding and color keying, material keys, output paths. No `AssetDatabase`. These are the rules FR-13 requires to be testable.
3. **Writers**: `RoAssetWriter`, `RoMaterialFactory`, `RoLedger`. The only layer that touches `AssetDatabase`.
4. **Pipeline**: context, results, report, batch, converter registry.
5. **Entry points**: static API, menu, window.

Data flow for one request (a map asking for a texture):

1. The converter calls `ctx.Texture(path)`.
2. The ledger says the entry is current (source hash and rules version unchanged, outputs exist) → `Skipped`, return the existing asset.
3. Otherwise read bytes from the source, decode, key, bleed, encode PNG, write through the writer, record in the ledger → `Created` or `Updated`.
4. A missing or undecodable source yields one report issue and the placeholder (§4.12); the caller continues.

## 3. Assemblies and file layout

Satisfies FR-13, FR-14, FR-3.

Root: `Uninarok-client/Assets/Editor/RoImport/`. The folder sits under `Editor/`, so Unity already treats it as editor-only; the asmdefs state it explicitly.

| Path | Contents | Assembly |
|---|---|---|
| `Core/Paths/` | `RoPath`, `RoPathException` | `Uninarok.RoImport.Core` (`includePlatforms: ["Editor"]`) |
| `Core/Source/` | `IRoSource`, `RoSourceFile`, `RoFolderSource`, `RoHashCache` | Core |
| `Core/Space/` | `RoUnits`, `RoSpace` | Core |
| `Core/Formats/` | `RoImageReader` (adapter over the GRF image classes; the only code in the foundation that touches GRF types; the format plans add their adapters here) | Core |
| `Core/Imaging/` | `RoImage`, `RoColorKey`, `RoTextureAnalysis`, `RoTextureUsage` | Core |
| `Core/Materials/` | `RoSurface`, `RoMaterialKey` | Core |
| `Core/Output/` | `RoOutputRoots`, `RoOutputPaths` | Core |
| `Core/Results/` | `RoStatus`, `RoResult<T>`, `RoIssueCode`, `RoIssue`, `RoReport` | Core |
| `Editor/Writing/` | `RoAssetWriter`, `RoPngCodec` | `Uninarok.RoImport.Editor` (`includePlatforms: ["Editor"]`; references Core) |
| `Editor/Materials/` | `RoMaterialFactory` | Editor |
| `Editor/Ledger/` | `RoLedger`, `RoLedgerEntry`, `Ledger/<category>.json` (committed data) | Editor |
| `Editor/Pipeline/` | `RoImportContext`, `RoImport`, `RoJob`, `RoJobKind`, `IRoConverter`, `RoConverterRegistry`, `RoBatch`, `RoPlaceholders`, `RoTextureConverter` | Editor |
| `Editor/Ui/` | `RoImportMenu`, `RoImportWindow`, `RoSourceLocation` | Editor |
| `Uninarok-client/Assets/Tests/EditMode/RoImport/` | tests, `Fixtures~/` (a folder Unity ignores) | `Uninarok.RoImport.Tests` (Editor platform; references Core, Editor, test framework) |

- `Uninarok.RoImport.Core` references `UnityEngine` for math (`Vector3`, `Matrix4x4`) but never `UnityEditor`. A test (§6) checks the compiled assembly's references so the rule cannot rot.
- `Core` and `Editor` reference the format library through `overrideReferences: true` and `precompiledReferences: ["GRF.dll", "Encryption.dll", "ErrorManager.dll", "Utilities.dll"]` (the DLLs are editor-only, so both assemblies stay editor-only). Only types under `Core/Formats/` may use GRF types; the rest of the code sees our own types (`RoImage`, and the data types of the format plans).
- Namespaces follow folders: `Uninarok.RoImport.Core.*`, `Uninarok.RoImport.Editor.*`.
- No `AssetPostprocessor`, `ScriptedImporter` or `[InitializeOnLoad]` conversion hook exists in either assembly (FR-3, tested).
- `Core` and `Editor` declare `InternalsVisibleTo("Uninarok.RoImport.Tests")`.

## 4. Component contracts

### 4.1 Ragnarok paths: `RoPath`

Satisfies FR-1.

```csharp
public readonly struct RoPath : IEquatable<RoPath>, IComparable<RoPath>
{
    public string Canonical { get; }          // "texture/유저인터페이스/a.bmp"
    public string Extension { get; }          // ".bmp" (lowercase, with dot) or ""
    public string FileNameWithoutExtension { get; }
    public RoPath Folder { get; }
    public static RoPath Parse(string raw);                       // throws RoPathException
    public static bool TryParse(string raw, out RoPath path);
    public RoPath WithExtension(string extension);
    public RoPath Combine(string relative);                       // relative to this path used as a folder
}
```

Rules, in this order:

1. Replace `\` with `/`; collapse repeated `/`; trim leading and trailing `/` and whitespace.
2. Normalize to Unicode form C (so a Korean name stored decomposed on macOS equals the composed one).
3. Lowercase with the invariant culture. Korean syllables are unaffected.
4. Strip one leading `data/` segment, so `data\texture\a.bmp`, `DATA/Texture/a.bmp` and `texture/a.bmp` are one path.
5. Reject (throw `RoPathException`) an empty path, a `..` or `.` segment, a drive letter, or a control character.

Equality and hashing use `Canonical` with ordinal comparison. Every other component receives a `RoPath`, never a raw string.

### 4.2 Source: `IRoSource`, `RoFolderSource`, `RoSourceLocation`

Satisfies FR-1, FR-15, FR-3.

```csharp
public interface IRoSource
{
    bool TryGetFile(RoPath path, out RoSourceFile file);
    IEnumerable<RoPath> Enumerate(string folderPrefix);           // canonical prefix, e.g. "model/"
}
public readonly struct RoSourceFile
{
    public RoPath Path { get; }
    public string DiskPath { get; }
    public long Length { get; }
    public byte[] ReadAllBytes();
    public string ContentHash();                                  // SHA-256 hex, via RoHashCache
}
public sealed class RoFolderSource : IRoSource
{
    public RoFolderSource(string dataRootOnDisk, RoReport report = null);
    public void Refresh();                                        // rebuilds the index
}
```

- On first use `RoFolderSource` enumerates the data root once and builds a dictionary from the canonical path to the real disk path. One enumeration serves all lookups in the session; `Refresh` rebuilds it. This is what makes the lookup independent of letter case, slash direction and Korean names (FR-1), because the lookup key is the canonical form of both sides.
- Two disk files with the same canonical path (names that differ only by case) raise one `SourceDuplicate` issue; the first in ordinal disk-path order wins, so the choice is deterministic.
- `RoHashCache` keeps `(disk path, length, last-write ticks) → SHA-256` in `Library/RoImport/hashcache.json` (local, not committed) so a full batch does not rehash gigabytes of unchanged files.
- `RoSourceLocation.Resolve()` (editor layer) returns the data root: the `EditorPrefs` key `Uninarok.RoImport.SourceRoot` if set, otherwise the project-relative default `Assets/Editor/GRF Resources~/data`. The window shows the active path and has a Browse button that sets the preference.
- **The default folder name ends with `~`.** Unity does not import, index or build folders whose name ends with `~`. Copying or updating the full data set there creates no `.meta`, no import and nothing in the build (FR-15). See D2 for the alternatives rejected.
- Nothing in the source layer writes to the source folder (spec §6).

### 4.3 Units and axes: `RoUnits`, `RoSpace`

Satisfies FR-7.

```csharp
public static class RoUnits
{
    public const float CubeRoUnits = 10f;     // one GND cube edge in Ragnarok units
    public const float Scale = 0.1f;          // Ragnarok unit -> Unity unit (1 cube = 1 unit)
    public const float GatCell = 0.5f;        // one GAT cell edge in Unity units (half a cube)
}

public static class RoSpace
{
    public static readonly Matrix4x4 Mirror;                      // diag(1, -1, 1, 1)
    public static Vector3 Point(Vector3 ro);                      // Mirror * p * Scale
    public static Vector3 Direction(Vector3 ro);                  // Mirror * v (normals, axes; no scale)
    public static Matrix4x4 Matrix(Matrix4x4 roLocal);            // Mirror * M * Mirror, translation scaled
    public static Quaternion Rotation(Matrix4x4 roRotation);      // from the converted matrix
    public static Vector3 Scale(Vector3 roScale);                 // unchanged by the conjugation
    public static void ReverseWinding(int[] triangleIndices);     // swaps the 2nd and 3rd index of each triple
}
```

Rules, and the reason for each:

- Ragnarok's Y axis points down; Unity's points up. The change of basis is the reflection `F = diag(1, -1, 1)`.
- Positions and directions are transformed as vectors (`F·p`, scaled by `Scale` for positions only).
- **Rotations and local matrices are transformed as full matrices by conjugation, `F·M·F`.** A conjugated rotation stays a proper rotation (determinant +1), so no Euler angle is negated and no axis is swapped by hand. This replaces the sign tweaks of the old importers, whose per-case corrections are the likely source of the wrong placements listed in `reference-models.md`.
- Scale components keep their sign (a mirrored model keeps its mirroring); winding is **not** corrected here for scale.
- A reflection reverses triangle orientation, so mesh builders call `ReverseWinding` once per mesh, and only there. No other code reverses winding.
- Only `RoSpace` and `RoUnits` contain the numbers `0.1`, `0.5` and the `-1` of the Y flip. A format converter that needs a different rule (for example RSM node offsets) states it in its own plan and builds it from these functions.
- `RoSpace` is pure (no `AssetDatabase`, no scene), so it is tested in EditMode without fixtures.

### 4.4 Textures: imaging, color key, usage

Satisfies FR-8.

```csharp
public sealed class RoImage { public int Width, Height; public byte[] Rgba32; /* top row first */ public bool SourceHadAlpha; }

public static class RoImageReader                                 // Core/Formats/: adapter over GRF
{
    public static bool IsSupportedExtension(string extension);    // ".bmp", ".png", ".tga"
    public static bool TryRead(RoPath path, byte[] bytes, IRoPngCodec png,
                               out RoImage image, out string reason);
}
public interface IRoPngCodec { bool TryDecode(byte[] png, out RoImage image); byte[] Encode(RoImage image); }   // Unity ImageConversion: encodes every output; decodes PNG only if GRF cannot (§4.4)

public enum RoTextureAlpha { None, Keyed, Partial }
public static class RoColorKey
{
    public const byte KeyR = 255, KeyG = 0, KeyB = 255, Tolerance = 10;   // carried over from the current importer
    public static RoTextureAlpha Apply(RoImage image);            // keys or classifies; see below
    public static void BleedTransparentColor(RoImage image);      // fills RGB of alpha-0 pixels from neighbors
}
public enum RoTextureUsage { World, Ui }
public static class RoTextureUsageRules { public static RoTextureUsage For(RoPath path); }
```

- **Reading uses GRF, not our own decoders.** `RoImageReader` calls the GRF image classes (`GrfImage` and its BMP and TGA support) to decode the bytes, then converts the result to our `RoImage` (RGBA, top row first, `SourceHadAlpha` set from the file's pixel format). Paletted BMPs keep their palette index information only as far as GRF exposes it; the key color is applied on RGBA (below). The extension picks the format; a read that GRF rejects (bad header, truncated data) is a `CorruptFile` issue with GRF's reason, never an exception to the caller.
- **PNG:** read with GRF if its image classes decode PNG in Unity's runtime; otherwise through `IRoPngCodec` (Unity's `ImageConversion`, which is Unity's own decoder, not code of ours). Which of the two applies is recorded in this section by task T0.4 (§7). **Encoding** the output PNG always uses `ImageConversion` (`RoPngCodec`, editor layer).
- The exact GRF members used (loading from bytes, width and height, pixel format, conversion to RGBA) are written into this section by T0.4 from the shipped `GRF.dll`; until then only the adapter's contract above is fixed.
- An extension outside the three gives one `UnsupportedTextureFormat` issue naming the file and its referrer, and the placeholder (§4.12) is returned (FR-8, FR-10).
- **Color key.** For an image with no real alpha (`SourceHadAlpha == false`: BMP, 24-bit TGA, PNG without alpha), any pixel within `Tolerance` of the key color per channel gets alpha 0. The result is `Keyed` if any pixel was keyed, otherwise `None`. An image with real alpha is not keyed; it is `Partial` if it has alpha values between 1 and 254, else `None`.
- **Bleed.** After keying, the RGB of every alpha-0 pixel is replaced by the average of its nearest non-transparent neighbors (repeated passes until filled, capped at the image size). Without it, bilinear filtering and mip chains blend the key color into the edge, which is the pink fringe FR-8 forbids.
- **Usage by path, not by extension** (FR-8): `texture/유저인터페이스/**` is `Ui`; everything else under `texture/` is `World`. Sprite frame atlases are produced by PLAN-0007 and are not source textures.
- The output is always a PNG (D7). Import settings by usage (applied by the writer, §4.7):

| Setting | World | Ui |
|---|---|---|
| Texture type | Default | Sprite (single) |
| sRGB | on | on |
| Alpha is transparency | on | on |
| Mipmaps | on, preserve coverage when `Keyed` | off |
| Wrap | Repeat | Clamp |
| Filter | Bilinear | Point |
| Compression | normal quality | none |
| Read/write | off | off |

### 4.5 Output paths: `RoOutputRoots`, `RoOutputPaths`

Satisfies FR-4, FR-16.

```csharp
public sealed class RoOutputRoots
{
    public string Art  = "Assets/Art/Imported";
    public string Data = "Assets/Data";
    public string Scenes = "Assets/Scenes/Maps";          // generated map scenes and the hand-made additive scenes (§4.13)
}
public static class RoOutputPaths
{
    public static string Mirror(RoOutputRoots r, RoPath source, string newExtension);        // Art/<canonical folder>/<name><ext>
    public static string Texture(RoOutputRoots r, RoPath source);                            // Art/texture/a/b.png
    public static string Material(RoOutputRoots r, RoMaterialKey key);                      // Art/_materials/<texture path>__<mode>.mat
    public static string Placeholder(RoOutputRoots r, string name);                          // Art/_placeholders/<name>
    public static string Data(RoOutputRoots r, string kind, string name, string extension);  // Data/<kind>/<name>.asset
    public static string Scene(RoOutputRoots r, string mapName);                              // Scenes/<name>.unity (generated scene, ledger-owned)
    public static string AdditiveScene(RoOutputRoots r, string mapName);                      // Scenes/<name>.additive.unity (hand-made, never written)
}
```

- Outputs mirror the canonical (lowercase, forward slash) Ragnarok path, so two files with the same name in different Ragnarok folders never collide (FR-4), and the path is the same on every machine and file system.
- The converter for each format chooses its own extension and kind (`Mirror(r, path, ".prefab")` and so on); the foundation owns the root, the case rule and the collision rule below.
- **Collision rule:** two different source files must not map to the same output path (for example `a.bmp` and `a.tga`). The ledger (§4.8) owns each output by source path; a second owner gives one `OutputCollision` issue and the second file is skipped.
- **GUIDs are assigned by Unity** when it first imports a new asset (the writer then calls `AssetDatabase.Refresh`/`ImportAsset`); the tool never writes a `.meta` itself. An output the tool owns is always **updated in place** (§4.7), never deleted and recreated, so its GUID and every reference to it stay valid. Consequence for teams: committed outputs are pulled from version control, not regenerated from scratch on another machine, because that would assign new GUIDs (D4, §9).
- Hand-made assets are never placed under the two output roots, except through §4.13.

### 4.6 Materials: `RoSurface`, `RoMaterialKey`, `RoMaterialFactory`

Satisfies FR-9.

```csharp
public enum RoSurface { Opaque, Cutout, Blend }
public readonly struct RoMaterialKey : IEquatable<RoMaterialKey>
{
    public RoMaterialKey(RoPath texture, RoSurface surface, bool twoSided);
    public RoPath Texture { get; } public RoSurface Surface { get; } public bool TwoSided { get; }
    public string Suffix { get; }                                 // "opaque", "cutout", "blend", + "_2s"
}
public static class RoMaterialFactory
{
    public static RoResult<Material> GetOrCreate(RoImportContext ctx, RoMaterialKey key);
    public static RoSurface SurfaceFor(RoTextureAlpha alpha);     // None -> Opaque, Keyed -> Cutout, Partial -> Blend
}
```

- The key is `(texture, surface, twoSided)`, so 1000 models that use one texture reach one material asset (FR-9). The path is `Art/_materials/<texture canonical path without extension>__<suffix>.mat`.
- The surface is **derived from the texture's analysis** (`SurfaceFor`), never from the model. The model contributes only the `twoSided` flag from its faces. `Blend` exists for textures with real partial alpha (TGA or PNG); the spec's list of surface modes names opaque, cutout and two-sided, and `Blend` is the third surface that URP Lit needs for such textures (D11, flagged for approval).
- Shader: `Universal Render Pipeline/Lit`, found once through `Shader.Find`; if it is missing the factory returns a `Failed` result with a `ShaderMissing` issue.
- Property setup is explicit, in one method, so it does not depend on the material inspector. For all materials: `_BaseMap` = the texture, `_BaseColor` = white, `_Metallic` = 0, `_Smoothness` = 0, keywords `_SPECULARHIGHLIGHTS_OFF` and `_ENVIRONMENTREFLECTIONS_OFF` on. Then:

| Mode | `_Surface` | `_AlphaClip` | `_Cutoff` | Blend | `_ZWrite` | Queue | Keywords |
|---|---|---|---|---|---|---|---|
| Opaque | 0 | 0 | n/a | One/Zero | 1 | 2000 | none |
| Cutout | 0 | 1 | 0.5 | One/Zero | 1 | 2450 | `_ALPHATEST_ON` |
| Blend | 1 | 0 | n/a | SrcAlpha/OneMinusSrcAlpha | 0 | 3000 | `_SURFACE_TYPE_TRANSPARENT` |

  Two-sided sets `_Cull` = 0 (URP Lit flips the back-face normal for lighting when culling is off), otherwise `_Cull` = 2.
- A key whose texture is missing is built with the placeholder texture (§4.12) and the issue is reported once.
- Every material is recorded in the ledger under the category `material`: the entry's source is the texture's Ragnarok path plus the key suffix, its hash is the texture's source hash, and its output is the `.mat` path. Several models and maps may ask for one material without owning it (added while writing PLAN-0003).
- Materials are written through `RoAssetWriter.UpsertObject`, so an existing material keeps its GUID and references. A user's manual edit of a generated material is overwritten when its inputs change (spec §6: only FR-12 layers survive).

### 4.7 Writing assets: `RoAssetWriter`

Satisfies FR-4, FR-5, FR-16.

```csharp
public sealed class RoAssetWriter
{
    public RoStatus WriteBytes(string projectPath, byte[] bytes, Action<AssetImporter> configure = null);   // PNG
    public RoStatus UpsertObject<T>(string projectPath, T fresh) where T : UnityEngine.Object;              // mesh, material, SO
    public bool Exists(string projectPath);
    public T Load<T>(string projectPath) where T : UnityEngine.Object;
    public void BeginBatch(); public void EndBatch();             // StartAssetEditing / StopAssetEditing, re-entrant
}
```

- Only this class and `RoAssetWriter`'s helpers call `AssetDatabase` for writes.
- `WriteBytes`: if the file exists with identical bytes nothing is written (`Skipped`). Otherwise the folder is created, the bytes are written with `File.WriteAllBytes`, and `AssetDatabase.ImportAsset` runs. The `configure` callback sets the importer (texture settings of §4.4) and is applied only when the importer's current settings differ, so a repeat run triggers no reimport.
- `UpsertObject`: a new asset is created with `AssetDatabase.CreateAsset` and Unity assigns its GUID. For an existing asset the writer loads the existing object and copies the fresh content into it with `EditorUtility.CopySerialized`, then marks it dirty; it never deletes and re-creates, so GUIDs and every reference to it survive (FR-5). `fresh` is destroyed after the copy.
- All writes go through the writer so that a batch brackets them with `StartAssetEditing`/`StopAssetEditing` (in `try/finally`), then `AssetDatabase.SaveAssets` per chunk (§4.10).
- Serialization of generated objects is the project's setting (text). Meshes serialize their data in a stable order, so a rerun produces byte-identical files; this is asserted by the determinism test (§6).
- The writer refuses a path outside the two output roots (and the additive area of §4.13) with an exception: a bug, not an expected condition.

### 4.8 Ledger and skipping: `RoLedger`

Satisfies FR-5, FR-16, FR-6.

```csharp
public sealed class RoLedgerEntry
{
    public string Source;            // canonical source path
    public string SourceHash;        // SHA-256 hex of the source file
    public int Rules;                // converter rules version
    public string[] Outputs;         // project paths owned by this source, sorted ordinally
    public RoLedgerDependency[] Dependencies;   // (source path, hash) pairs, sorted
}
public sealed class RoLedger
{
    public static RoLedger Load(string category);
    public bool IsCurrent(RoPath source, string hash, int rules, IRoSource files, RoAssetWriter writer);
    public RoLedgerEntry Find(RoPath source);
    public void Record(RoLedgerEntry entry);                      // detects output collisions
    public string OwnerOf(string outputPath);
    public void Save();
}
```

- One JSON file per category (`texture`, `model`, `map`, `sprite`) at `Assets/Editor/RoImport/Ledger/<category>.json`, committed. Entries sorted by source path, keys written in a fixed order, `\n` newlines, no timestamps, so the file is deterministic and diffs show only real change. `JsonUtility` serializes a sorted list (it does not support dictionaries).
- `IsCurrent` is true only if the source hash matches, the converter's rules version matches, **all outputs still exist**, and every dependency's current hash equals the recorded one (a map is stale when a model it uses changed). Otherwise the item is converted. This gives the "skipped" report of FR-5 and means an unchanged full batch writes nothing.
- A converter's rules version is an integer constant in that converter. Changing a rule bumps it and forces regeneration (FR-16: a change appears in version control only when an input or a rule changed). An editor upgrade that changes encoders is handled the same way: bump the version.
- The ledger also bounds what the tool may delete: **only paths listed in a ledger entry are ever removed or overwritten**; any other asset in the output roots is left alone.
- Saving happens at the end of each chunk (§4.10), so an aborted batch resumes where it stopped.

### 4.9 Results, issues and logging: `RoResult<T>`, `RoReport`, `RoIssue`

Satisfies FR-2, FR-10, FR-11.

```csharp
public enum RoStatus { Created, Updated, Skipped, Failed }
public readonly struct RoResult<T> { public RoStatus Status; public T Value; public RoIssue Failure; }

public enum RoIssueCode { SourceMissing, TextureMissing, UnsupportedTextureFormat, CorruptFile,
                          OutputCollision, SourceDuplicate, ShaderMissing, DependencyCycle, Unexpected }
public sealed class RoIssue { public RoIssueCode Code; public string Item; public string Referrer; public string Message; }

public sealed class RoReport
{
    public void Warn(RoIssueCode code, string item, string referrer, string message);   // de-duplicated by (code, item)
    public void Fail(RoPath item, RoIssue issue);
    public int Created, Updated, Skipped, Failed;
    public IReadOnlyList<RoIssue> Issues { get; }
    public string Summarize();                                    // counts, then each failed item once with its reason
}
```

- **Expected conditions are warnings, not errors** (FR-11): a missing source file, a missing texture, an unsupported format, a corrupt file, a collision. `Warn` writes one `Debug.LogWarning` per distinct `(code, item)` in the whole run and lists the item once in the final summary (FR-10). The warning names the item and, where there is one, the referrer (the model or map that asked for it).
- `Debug.LogError` is used only for `Unexpected`: an exception that escaped a converter (a bug). `RoBatch` catches it per item, records a `Failed` result with the exception message, logs it once as an error with the stack, and continues. A clean batch therefore produces no error log entries.
- Converters never throw for expected conditions; they return `Failed` or a placeholder-backed `Created`.

### 4.10 Context, entry points and batch: `RoImportContext`, `RoImport`, `RoBatch`

Satisfies FR-2, FR-3, FR-5, FR-6, FR-10.

```csharp
public sealed class RoImportContext
{
    public IRoSource Source; public RoAssetWriter Writer; public RoReport Report;
    public RoOutputRoots Roots; public IRoPngCodec Png; public bool Force;
    public RoLedger Ledger(string category);
    public RoResult<Texture2D> Texture(RoPath path, string referrer = null);          // on demand (FR-6)
    public RoResult<Material> Material(RoMaterialKey key, string referrer = null);
}

public static class RoImport
{
    public static RoResult<Texture2D> Texture(string roPath);                          // FR-2: by path, no UI
    public static RoResult<Material> Material(RoPath texture, RoSurface surface, bool twoSided);
    public static RoReport Run(IEnumerable<RoJob> jobs, RoImportOptions options = null);
    // Model(...), Map(...), Sprite(...) are added by PLAN-0003..0007 as one-line wrappers over Run.
}

public readonly struct RoJob { public RoJobKind Kind; public RoPath Path; }
public enum RoJobKind { Texture /*, Model, Ground, MapData, Map, Sprite: added by later plans */ }
public interface IRoConverter { RoJobKind Kind { get; } RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path); }
public static class RoConverterRegistry { public static IRoConverter For(RoJobKind kind); }   // one hand-written array
public sealed class RoImportOptions { public bool Force; public bool ShowProgress = true; public RoOutputRoots Roots; }
```

- Every conversion starts from a Ragnarok path string and returns a result; nothing needs a window or a selection (FR-2). Tests and scripts call `RoImport.*` directly.
- **Dependencies on demand (FR-6).** A converter asks `ctx.Texture(...)`/`ctx.Material(...)` (and later `ctx.Model(...)` from the map plan) for what it needs. Each call checks the ledger, converts if missing or stale, and records itself as a dependency of the caller. A per-run memo makes repeat requests free; an in-progress set breaks a dependency cycle with a `DependencyCycle` issue (cycles do not occur in these formats; the guard prevents an endless loop).
- **No implicit conversion (FR-3).** The importer has no `AssetPostprocessor`, no `ScriptedImporter` and no `InitializeOnLoad` converter. Source files are not assets (§4.2), so copying them triggers nothing. The old importers (`BmpImporter`, a postprocessor on every `.bmp`, and the others) are compiled out by T0.2 and deleted later (§7, D15).
- **Batch.** `RoBatch.Run`:
  1. Sorts jobs by `(kind, path)` for a deterministic order.
  2. Locks assembly reload (`EditorApplication.LockReloadAssemblies`) and brackets the work with `AssetDatabase.StartAssetEditing`, in `try/finally`.
  3. Shows a cancelable progress bar (`EditorUtility.DisplayCancelableProgressBar`) with item name and counts.
  4. Every 200 jobs (constant) ends the editing block, calls `SaveAssets`, saves the ledgers, calls `EditorUtility.UnloadUnusedAssetsImmediate` and restarts the block, which bounds memory on a whole-game run.
  5. Catches an exception per job (§4.9) and continues.
  6. Ends with the summary (one `Debug.Log`) listing failures once.
- Cancel stops after the current job and still saves the ledgers; running the same batch again resumes because finished items are `Skipped`.

### 4.11 Editor entry points: `RoImportMenu`, `RoImportWindow`

Satisfies FR-2.

- Source files are not Unity assets (§4.2), so the Project window selection cannot name them. The menu therefore opens a window:
  - `Tools/Ragnarok/Import Window` opens `RoImportWindow`.
  - `Tools/Ragnarok/Convert All Textures` runs a batch over `Enumerate("texture/")`.
- `RoImportWindow` shows the source root (with Browse), a kind popup (one entry per registered `RoJobKind`), a text area with one Ragnarok path or folder prefix per line (a folder prefix ends with `/` and expands through `Enumerate`), a **Scan** button that lists what the lines expand to, a **Force** toggle, and **Convert**. The result summary is shown in the window and logged once.
- The window and the menu call only `RoImport.Run`, so UI and code paths cannot differ.

### 4.12 Placeholders: `RoPlaceholders`

Satisfies FR-8, FR-10.

- `RoPlaceholders.MissingTexture(ctx)` returns a generated 16 x 16 magenta and black checker PNG at `Art/_placeholders/missing_texture.png`, created once through the normal writer (deterministic bytes), with its own material key. It is created on the first need, not at install.
- A missing source texture, an unsupported format and a corrupt texture all resolve to it, so the model or map that needed the texture still converts and shows an obvious placeholder (FR-10), with one warning naming the texture and its referrer.
- The placeholder is never listed as a dependency, so replacing a missing file later and re-running a batch is enough: the dependent item is stale because its recorded dependency list contains the missing path with the empty hash, which no longer matches.

### 4.13 Additive layer rule

Satisfies FR-12.

- The foundation guarantees two things; the format plans use them:
  1. Converters write, overwrite or delete **only** paths owned by a ledger entry (§4.8), never other files, even under the output roots.
  2. The scenes folder `Assets/Scenes/Maps/` (the third root, `RoOutputRoots.Scenes`) holds two kinds of file side by side, and the importer treats them differently: the **generated** map scene `<map>.unity` is owned by a ledger entry and is rewritten by conversion (owner decision on SPEC-0006 OQ-2); the **additive** scene `<map>.additive.unity` is hand-made and is never created, written, renamed or deleted by the importer, even though it sits in the same folder. The importer never writes any other file in this folder (rule 1). PLAN-0006 loads the two together.
- Re-converting a map rewrites the generated scene and map data only. The additive scene is untouched, so its custom objects remain. The reference check is by name and path of the custom object, so a regenerated generated scene must not rename or move the objects the additive scene refers to; PLAN-0006 defines which references are stable.

### 4.14 Editor-only and build exclusion

Satisfies FR-14, FR-15, FR-3.

- Both importer assemblies and the test assembly have `includePlatforms: ["Editor"]`. They are not compiled into players. The generated assets the player needs (map data, meshes, textures) are ordinary assets referenced by scenes; no importer code is referenced from them.
- The source folder is `~`-suffixed (§4.2) so Unity ignores it entirely.
- The ledger JSON lives under `Assets/Editor/`, so it is not packed into builds.
- A development build is made once as a manual check (T-list in `tasks.md`) and its output searched for the importer assembly names and for any source file.

## 5. Threading and lifecycle

Satisfies FR-5, FR-10.

- Everything runs on the main thread, synchronously, inside one editor call. No `Task`, no thread pool. Pure `Core` code (keying, bleeding, path and space rules; GRF adapters) allocates managed arrays only and could be parallelized later without changing a contract (D12).
- Domain reload: `LockReloadAssemblies` is held for the duration of a batch. If the editor is closed or crashes mid-batch the ledger holds every finished chunk; the next run skips them.
- `RoFolderSource` builds its index lazily on first use and is owned by the context of one run; the `RoHashCache` file is loaded and saved around each run.
- Memory: decoded images (`byte[]`) are released after each texture; the chunked `UnloadUnusedAssetsImmediate` of §4.10 frees Unity objects. A 4096 x 4096 RGBA source needs about 64 MB transiently; Ragnarok textures are far smaller.

## 6. Test plan

Satisfies FR-13 and every acceptance row of spec §4. Tests are EditMode NUnit tests in `Uninarok.RoImport.Tests`, run in batch mode with the command in `CLAUDE.md` §2 (`-runTests -testPlatform EditMode`). Fixtures are tiny files created in code or placed in `Fixtures~/` (ignored by Unity). Tests that write assets use a throwaway output root `Assets/__RoImportTests/` created in `SetUp` and deleted in `TearDown`.

| Test class | Covers | Checks |
|---|---|---|
| `RoPathTests` | FR-1 | `data\texture\a.bmp`, `DATA/Texture/a.bmp` and `texture/a.bmp` are equal; Korean folder name composed and decomposed are equal; rejects `..`, drive letters, empty |
| `RoFolderSourceTests` | FR-1, FR-15 | a temp folder with mixed-case and Korean-named files: lookups by either form return the same disk file; duplicate-by-case gives one `SourceDuplicate` issue; the default source root name ends with `~` and `AssetDatabase.AssetPathToGUID` of a file inside it is empty |
| `RoSpaceTests` | FR-7 | `Matrix` of a rotation has determinant +1 and round-trips; `Point` of a 10-unit RO cube edge is 1 unit; `Direction` is not scaled; `ReverseWinding` swaps each triple once; a known RO point and rotation map to the expected Unity values (table in the test) |
| `RoImageReaderTests`, `RoPngCodecTests` | FR-8 | the fixture BMP (8-bit paletted and 24-bit), TGA (32-bit with alpha) and PNG decode through GRF (or through `IRoPngCodec` for PNG if T0.4 says so) to the expected pixels at known coordinates; the corrupt fixture returns false with a reason and never throws; an encode then decode round trip of a small image is lossless |
| `RoColorKeyTests` | FR-8 | key-color and near-key pixels (within tolerance) get alpha 0; others do not; real-alpha images are not keyed; after bleed no alpha-0 pixel's RGB is within tolerance of the key; bilinear sample at a keyed edge of a green-on-magenta image has no magenta hue |
| `RoTextureConverterTests` | FR-8, FR-10 | a BMP, a PNG and a TGA each convert to a PNG with the right import settings per usage; `.dds` gives one `UnsupportedTextureFormat` warning and returns the placeholder; a missing texture gives one warning naming referrer and file |
| `RoMaterialTests` | FR-9 | 1000 requests with one key give one material asset; keys differing in surface or two-sidedness give different materials; property table of §4.6 per mode (`_Cull`, `_AlphaClip`, keywords, queue) |
| `RoIdempotencyTests` | FR-5, FR-6 | converting a fixture texture twice: second result is `Skipped`, asset set, GUIDs and references unchanged; deleting an output and rerunning recreates it with the same content (the GUID is new, assigned by Unity); requesting a material in an empty output root converts the texture first |
| `RoDeterminismTests` | FR-16, FR-4 | convert, snapshot the SHA-256 of every file under the output root except `.meta`, delete the root, convert again, snapshot again: identical; a second run in place also leaves every `.meta` byte-identical; two sources with the same file name in different Ragnarok folders give two outputs and no collision |
| `RoLedgerTests` | FR-5, FR-12 | ledger file is sorted and byte-identical after a reload and save; a changed source hash or rules version makes the entry stale; the converter never deletes or rewrites a file that is not owned by a ledger entry (a hand-made asset in the root survives; the additive area is untouched) |
| `RoBatchTests` | FR-10, FR-11 | a batch with a corrupt file and a missing file finishes the rest, lists each failure once, and produces no `LogError` (`LogAssert.NoUnexpectedReceived`); cancel saves the ledger and a rerun resumes |
| `RoArchitectureTests` | FR-3, FR-13, FR-14 | no type in the importer assemblies derives from `AssetPostprocessor`, `ScriptedImporter` or has `InitializeOnLoad`; the `Core` assembly's referenced assemblies do not include `UnityEditor.*`; the asmdefs of the importer and tests have `includePlatforms == ["Editor"]`; no C# file of the importer assemblies outside `Core/Formats/` uses `BinaryReader`, `BitConverter`, `MemoryStream`-based parsing or references a GRF type (text search over the source files), so no file format is parsed outside GRF and its adapters |

**Manual checks** (recorded in the task's evidence, with screenshots where visual):

- FR-7 and FR-8 visual: a reference model and map side by side with the reference viewer, a leaf texture at close and far range with no pink edge.
- FR-15: copy the full data set into the source folder with the editor open: no import progress, no new `.meta`, and a development build contains none of the files.
- FR-14: a development build contains no `Uninarok.RoImport.*` assembly.
- Success criterion: a whole-game texture batch, then a second batch that reports everything `Skipped` and changes no file in `git status`.

## 7. Migration and compatibility

- **Hide the source folder.** With Unity closed, rename `Assets/Editor/GRF Resources` to `Assets/Editor/GRF Resources~` and delete `GRF Resources.meta` (and the `.meta` files inside it, if any remain). Unity then ignores the folder (FR-15). Reopen the editor; the folder disappears from the Project window, which is expected.
- **Old data location.** `Assets/Res/` (the earlier copy of the data used by the old importers) has already been removed by the owner; nothing is left to decide.
- **Old importers.** T0.2 compiles out every file of the old pipeline in `Assets/Editor/` (`BmpImporter.cs`, `AssetPostProcessor.cs`, `RsmImpoter.cs`, `RsmImpoter.bkp.cs`, `RswImporter.cs`, `GNDImporter.cs`, `GatImporter.cs`, `UtilExtensions.cs`) by wrapping each in `#if RO_LEGACY_IMPORTERS`. The define is not set in any build target, so none of them runs on any file, wherever it sits in `Assets/` (FR-3), and the code stays readable for the format plans that replace it (D15). `BmpImporter.cs` and `AssetPostProcessor.cs` are deleted by T8.1, because they conflict with FR-3 and FR-8. The others are deleted by the task of the plan that replaces each (0003, 0004, 0005, 0006), not here. Setting the define temporarily brings the old pipeline back for comparison.
- **Git LFS.** `.gitattributes` already tracks `*.png`, `*.spr`, `*.act`, `*.rsw`, `*.gat`, `*.grf`, `*.jpg` and audio. A task adds `Uninarok-client/Assets/Art/Imported/**/*.asset filter=lfs diff=lfs merge=lfs -text` for generated meshes. Materials and the ledger JSON stay as text for readable diffs. Whether the source data set itself is committed is outside this plan; the `~` folder works either way.
- **GRF verification.** T0.4 proves the GRF image classes load the fixture images inside the Unity editor and records the members used (§4.4).
- **Test framework.** `com.unity.test-framework` is added to `Packages/manifest.json` (shared with PLAN-0001).
- **Map data class.** The existing runtime `Assets/Scripts/MapData.cs` stays as it is; PLAN-0005 decides how it becomes the map data asset.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Two editor assemblies (`Core` for rules, `Editor` for writers and UI) plus a test assembly; layering checked by a test | One assembly (the testable rules would mix with `AssetDatabase` code); `Core` for all platforms (it would ship in builds, violating FR-14) |
| D2 | Source folder `Assets/Editor/GRF Resources~/data`, hidden from Unity by the `~` suffix | Keep it visible and try to stop import (Unity imports every `.bmp`/`.png`/`.tga` and creates `.meta` files; a postprocessor cannot cancel it); a folder outside the Unity project (breaks the owner's decision to keep it in `Editor/GRF Resources`) |
| D3 | Explicit static API and context; no `ScriptedImporter` or `AssetPostprocessor` | `ScriptedImporter` per extension (outputs are read-only sub-assets, conversion is implicit: the problems that started this work) |
| D4 | GUIDs are assigned by Unity at first import; outputs are updated in place, and committed outputs are pulled, not regenerated elsewhere | Choosing GUIDs ourselves by writing `.meta` files (owner decision: let Unity create them and refresh; more fragile across Unity versions) |
| D5 | Committed JSON ledger per category plus a local, uncommitted hash cache | Ledger in `AssetImporter.userData` (meshes and materials have no importer); only file timestamps (not stable across git checkouts); rehashing the data set on every run |
| D6 | Coordinate change by conjugating full matrices with `diag(1,-1,1)`; winding reversed once | Per-axis Euler sign tweaks (the likely cause of the wrong rotations in the reference models); mirroring through negative scale (breaks lighting and physics) |
| D7 | All textures output as PNG (color key and bleed baked in). **Files are read through the GRF library** (image classes for BMP and TGA, and for PNG if it works in Unity; otherwise Unity's `ImageConversion`); output PNG is encoded by Unity | Hand-written BMP/TGA decoders (owner decision: use the GRF library to read the data; duplicated work and a rare-variant risk); keep the original BMP/TGA as assets (no real alpha, magenta fringe at mip levels); `UnityWebRequestTexture` (no BMP or TGA support) |
| D8 | Texture usage (`World`/`Ui`) decided by the source path | A usage argument per call (one file could be converted twice with different settings and collide) |
| D9 | Output paths are the canonical lowercase Ragnarok path | Keep original letter case (two machines or file systems disagree; Ragnarok paths are case-insensitive) |
| D10 | Material properties set explicitly per mode in one method | `BaseShaderGUI` helper methods (internal API, moves between URP versions) |
| D11 | A third surface `Blend` for textures with real partial alpha, derived from the texture | Treat all alpha as cutout (loses smooth edges); decide per model (the same texture would need different materials per model) |
| D12 | Single-threaded, synchronous batch | Thread pool for decoding (complexity before a measured need; contracts allow it later) |
| D13 | Window and menu open a path list, not "convert the selected assets" | Project-window selection (source files are not Unity assets, so there is nothing to select) |
| D14 | Ledger-owned outputs are the only files the tool overwrites or deletes | Clean the whole output folder before a run (destroys additive and hand-made assets) |
| D15 | Old importers are compiled out at once with `#if RO_LEGACY_IMPORTERS` (owner decision, 2026-11-17) and deleted later by the plan that replaces each; the source folder is still hidden with `~` (D2) | Rely on the `~` folder alone (the old importers would still fire on any source file placed elsewhere in `Assets/`); comment the code out (a large, unreadable diff); delete everything now (the format plans read the old code as reference); keep the folder visible and only disable the old importers (Unity would still import every image and create a `.meta` per file, violating FR-15) |

Items marked for the owner's approval: D2 (the folder rename), the art output root `Assets/Art/Imported`, and D11 (`Blend`). D4 was decided by the owner.

## 9. Risks

| Risk | Mitigation |
|---|---|
| Regenerating committed outputs from scratch on another machine assigns new GUIDs and breaks references in committed scenes and prefabs | Outputs are updated in place only; committed outputs are pulled from version control; a rules change is regenerated once, by one person, in place and committed |
| `EncodeToPNG` output can differ between editor versions, which would break byte-identical reruns | Determinism tests run on the project's editor version; the rules version of the texture converter is bumped on an editor upgrade (§4.8) |
| Korean folder names in Unity asset paths, git and Windows long paths | The tests use a Korean-named fixture; the first batch is run on the reference data; paths over the Windows limit are reported as a `CorruptFile`-class warning, not a crash |
| A whole-game batch is slow single-threaded and hashing gigabytes is slow once | Hash cache after the first run; chunked batches with resume; parallel pure decoding is available later (D12) |
| Memory growth over a long batch | Chunked editing blocks with unload (§4.10); measured in the whole-game criterion run |
| URP property names change in a later URP version | One method sets them (§4.6); a test compares the table per mode, so an upgrade fails loudly |
| The old importers keep importing or confusing the project | Compiled out by T0.2 (§7, D15); `Assets/Res` is already removed |
| The owner relies on the visible `GRF Resources` folder in the Project window | Window shows the source path and offers Browse; the folder is one rename to undo, and the setting can point anywhere |
| GRF image classes behave differently in Unity's Mono runtime than in GRFEditor (`Utilities.dll` references `System.Drawing`), or do not decode PNG or a TGA variant | T0.4 loads every fixture through GRF in the editor before any adapter is written; PNG falls back to Unity's decoder; a variant GRF cannot read is listed once by the batch and reported to the GRF library's maintainer or worked around in the adapter, never by a private decoder |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.1, 4.2, 6 |
| FR-2 | 2, 4.9, 4.10, 4.11 |
| FR-3 | 3, 4.2, 4.10, 4.14, 6 |
| FR-4 | 4.5, 4.7, 6 |
| FR-5 | 2, 4.7, 4.8, 4.10, 6 |
| FR-6 | 4.8, 4.10, 6 |
| FR-7 | 4.3, 6 |
| FR-8 | 4.4, 4.7, 4.12, 6 |
| FR-9 | 4.6, 6 |
| FR-10 | 4.9, 4.10, 4.12, 6 |
| FR-11 | 4.9, 6 |
| FR-12 | 4.8, 4.13, 6 |
| FR-13 | 2, 3, 6 |
| FR-14 | 3, 4.14, 6 |
| FR-15 | 4.2, 4.14, 7 |
| FR-16 | 4.5, 4.7, 4.8, 6 |
