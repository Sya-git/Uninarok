# PLAN-0005: GAT Map Data

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0005) |
| **Status** | Approved |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Shared rules (paths, units and axes, results, writer, ledger, batch) come from [PLAN-0002](../0002-ro-import-foundation/plan.md); the map space (`RoMapSpace`), the ground rules and the pure ground height sampler come from [PLAN-0004](../0004-gnd-ground/plan.md). Reference maps are in [`../0003-rsm-models/reference-models.md`](../0003-rsm-models/reference-models.md) §4.

## 1. Constraints and assumptions

- Code lives in the editor assemblies of PLAN-0002 under `Core/Gat/`, `Core/Formats/` (adapter) and `Editor/Gat/`; the **data asset and its query API are runtime code** in a new runtime assembly `Uninarok.Map` (`Assets/Scripts/Map/`), because the asset must exist in player builds without the source file (FR-4). The runtime assembly references nothing from the importer; the importer references it.
- **GAT files are read by the GRF library** (the `Gat` class of `GRF.dll`) through one adapter, `RoGatReader`, in `Core/Formats/` (PLAN-0002 §1). After the adapter, everything works on our own type `RoGat`.
- Format facts used here, from the community GAT document: a 14-byte header (`GRAT`, version, width and height in cells), then `Width × Height` cells of 20 bytes: four corner altitudes (south-west, south-east, north-west, north-east) and a 32-bit terrain type; version 1.2 knows types 0 (walkable), 1 (obstructed) and 5 (obstructed but snipable); version 1.3 embeds a water flag in the type field; other types exist and their client meaning is unclear. The document does not state the cell order, the relation to ground cubes, the row direction or the height sign; those come from PLAN-0004 (cubes are stored south row first, `row * width + column`, a cube is 2 × 2 cells) and are verified by T0.2/T0.3 of this plan.
- OQ-1 (per-corner or per-cell height) is **decided in this plan** (§8, D1). OQ-2 (which types are walkable) is **answered by the owner**: the types are the ones GRF.dll defines (`Walkable`, `Walkable2`, `Walkable3`, water by flag), and the definition is kept in `docs/data/GatCellType.md` (§4.2, D2).
- The old runtime `Assets/Scripts/MapData.cs` (a ScriptableObject with `TileType` and `SetMapData`) is replaced; its users are found and migrated by T0.1 and T6.1.

## 2. Architecture

Satisfies FR-1 to FR-6.

```
 x.gat ──(IRoSource)──> bytes                x.gnd (header only: width, height, zoom)
                  │                                   │
        RoGatReader (GRF adapter)               RoGndReader (PLAN-0004)
                  │                                   │
                  ▼                                   ▼
        RoGat (cells, raw types)             RoMapSpace(width/…, zoom)            Core/Gat, Core/Gnd
                  └──────────────┬─────────────────────┘
                                 ▼
                    RoGatRules (GRF GatType → flags; docs/data/GatCellType.md)      RoGatBuilder (pure: RoMapDataImage)
                                 │ plain arrays
                                 ▼
            RoMapDataConverter (Editor/Gat: ctx, MapData asset, ledger, optional image)
                                 │
              MapData (ScriptableObject, runtime, Uninarok.Map)  ── queries ── gameplay
                                 │
              MapRoot (runtime component, links the scene to the data) ── gizmos (editor)
```

Data flow for `RoImport.MapData("prontera")` (source `prontera.gat` at the data root, D9):

1. Ledger check (hashes of the GAT and of the GND header, rules version): current → `Skipped`.
2. Read the GAT through GRF into `RoGat`; read the GND through `RoGndReader` only for its width, height and zoom. A cell grid that is not exactly `2 × GND width` by `2 × GND height` gives one `GatGndMismatch` issue and no output.
3. Convert every cell to the stored form (§4.3) with `RoMapSpace`, build the arrays in memory.
4. Upsert `Assets/Data/map/<name>.asset` in place (FR-6) and, if requested, the optional type image (FR-8). Record outputs and dependencies in the ledger (category `mapdata`).

## 3. Assemblies and file layout

Satisfies FR-4, FR-6.

| Path | Contents | Assembly |
|---|---|---|
| `Assets/Scripts/Map/Uninarok.Map.asmdef` | runtime assembly, no editor references, no importer references | `Uninarok.Map` (all platforms) |
| `Assets/Scripts/Map/MapData.cs` | `MapData` (ScriptableObject), `MapCellType`, `MapCell` (value returned by queries) | `Uninarok.Map` |
| `Assets/Scripts/Map/MapRoot.cs` | `MapRoot` (MonoBehaviour: map name and a `MapData` reference; used by SPEC-0006 and by the gizmos) | `Uninarok.Map` |
| `Assets/Editor/RoImport/Core/Formats/RoGatReader.cs` | adapter GRF `Gat` → `RoGat` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Gat/` | `RoGat`, `RoGatRules`, `RoGatBuilder`, `RoGatImage` | Core |
| `Assets/Editor/RoImport/Core/Gnd/Space/RoGroundSampler.cs` | pure ground height at a point, from `RoGnd` (used by the FR-3 test and by PLAN-0006) | Core |
| `Assets/Editor/RoImport/Editor/Gat/` | `RoMapDataConverter`, `RoMapDataOutputPaths`, `RoGatImageWriter` | `Uninarok.RoImport.Editor` |
| `Assets/Editor/RoImport/Editor/Gat/Gizmos/` | `RoMapDataGizmos`, `RoMapDataOverlay`, `RoMapDataGizmoSettings` | Editor |
| `Assets/Tests/EditMode/RoImport/Gat/` | importer tests; `Assets/Tests/EditMode/Map/` runtime query tests | `Uninarok.RoImport.Tests`, `Uninarok.Map.Tests` |

Importer and runtime tests may share one assembly if the owner prefers; the runtime tests must not reference the importer.

Outputs:

| Output | Path |
|---|---|
| Map data | `Assets/Data/map/<name>.asset` (`RoOutputPaths.Data(roots, "map", name, ".asset")`) |
| Type image (optional, FR-8) | `Art/Imported/map/<name>/<name>_cells.png` |

## 4. Component contracts

### 4.1 Reading: `RoGatReader`, `RoGat`

Satisfies FR-1, FR-3.

```csharp
public sealed class RoGat
{
    public int VersionMajor, VersionMinor;
    public int Width, Height;                 // cells, from the header
    public float[] Heights;                   // 4 per cell: H00, H10, H01, H11 (south-west, south-east, north-west, north-east), Ragnarok units, unconverted
    public uint[] Types;                      // raw 32-bit terrain type per cell, version flags removed (see below)
    public bool[] WaterFlag;                  // water flag of version 1.3 and later, false before
}
public static class RoGatReader { public static bool TryRead(RoPath path, byte[] bytes, out RoGat gat, out string reason); }
```

- Cell index `row * Width + column`, `Width` from the header; heights follow the grid corner naming of PLAN-0004 §4.1 (`H00` low column and low row).
- The adapter never throws; a rejected read returns `false` with GRF's reason (`CorruptFile`).
- **Mapping check (T0.2).** The task prints the header and a handful of cells through GRF, compares them with the raw bytes, and writes into this section one row per `RoGat` field with the GRF member that supplies it, including how GRF exposes the water flag of 1.3 and how it handles the zero-byte prefix of old 1.2 files.

### 4.2 Cell types: `RoGatRules`

Satisfies FR-1 (and OQ-2).

```csharp
public enum MapCellType : byte { Walkable = 0, NoWalkable = 1, NoWalkableNoSnipable = 2, Walkable2 = 3, Unknown = 4, NoWalkableSnipable = 5, Walkable3 = 6, Other = 255 }   // runtime enum, Uninarok.Map
public static class RoGatRules
{
    public const int RulesVersion = 1;
    public static MapCellType TypeOf(int rawType);        // the low bits of the raw type; anything else is Other
    public static bool IsWalkable(MapCellType type);      // Walkable, Walkable2, Walkable3
    public static bool IsSnipable(MapCellType type);      // NoWalkableSnipable
    public static bool HasWaterFlag(int rawType);         // bit 31
}
```

- **The set of types is GRF's own.** The owner pointed out that GRF.dll already defines the types, and that the definition belongs in `docs/data`. `GRF.FileFormats.GatFormat.GatType` has `NoGat` (-1), `Walkable` (0), `NoWalkable` (1), `NoWalkableNoSnipable` (2), `Walkable2` (3), `Unknown` (4), `NoWalkableSnipable` (5), `Walkable3` (6), and ten values `Weird0` to `Weird9` that are `0x80000000` plus 0 to 9 (the water flag of GAT 1.3 and later); a separate `SpecialCellTypes` has `Water`, `InnerGutterLine` and `OutterGutterLine`. `MapCellType` keeps GRF's names and values for 0 to 6 and adds `Other` (255). **`docs/data/GatCellType.md` is the project's definition of the enum** (it did not exist; it is created with this plan, in the style of the other files of `docs/data`) and is the source the runtime enum, the rules table and the tests follow.
- **Water is a flag, not a type.** Bit 31 of the raw value is stored as the water flag; a walkable cell with the flag is walkable water (`MapData.IsWalkableWater`). The type of such a cell is the low-bit type.
- **Walkable table** (pinned by `RoGatRulesTests`): walkable = types 0, 3 and 6; snipable = type 5; blocked = 1, 2, 4, 5; `Other` (absent cell `-1`, values above 6) is blocked and reported once per map as `GatUnknownType` with the value and a count. T0.2 checks, on the reference maps, which types actually occur and that the walkable and snipable reading of each matches what the reference viewer shows; a difference changes `docs/data/GatCellType.md` and the table together.
- The **raw type is stored as well** (its low byte) next to the mapped type, so nothing is lost and a rules change needs no new reading of the file.
- The walkable, snipable and water bits are stored per cell in the asset (§4.3) so gameplay never needs the table.

### 4.3 The data asset: `MapData`, `MapCell`, `MapRoot` (runtime)

Satisfies FR-1, FR-2, FR-4, FR-5, FR-6.

```csharp
public sealed class MapData : ScriptableObject
{
    // identity and geometry
    [SerializeField] string mapName;
    [SerializeField] int cellsX, cellsZ;                 // GAT width and height in cells
    [SerializeField] float cellSize;                     // Unity units per cell (0.5 at the default zoom)
    [SerializeField] Vector2 origin;                     // Unity x and z of the corner of cell (0, 0)
    [SerializeField] float rowSign;                      // +1 or -1: Unity z direction of increasing rows (from RoMapSpace.RowDirection)
    [SerializeField] bool diagonalFlip;                  // which diagonal splits a cell for height queries (from RoGndRules)
    // cell data, flat arrays
    [SerializeField] float[] heights;                    // 4 per cell, Unity units (up is +y): H00, H10, H01, H11
    [SerializeField] byte[] types;                       // MapCellType per cell
    [SerializeField] byte[] flags;                       // bit 0 walkable, bit 1 snipable, bit 2 water (docs/data/GatCellType.md)
    [SerializeField] byte[] rawTypes;                    // raw GAT type per cell (low byte; the value range seen is recorded by T0.2)
    // queries (no allocation, no exception)
    public int CellsX { get; } public int CellsZ { get; } public float CellSize { get; }
    public bool TryGetCell(Vector3 position, out int x, out int z);
    public bool InBounds(int x, int z);
    public bool IsWalkable(int x, int z);                // outside the map: false
    public bool IsWalkable(Vector3 position);            // outside the map: false
    public MapCellType TypeAt(int x, int z);             // outside: Other
    public bool IsWalkableWater(int x, int z);           // walkable and water flag; outside: false
    public bool TryGetHeight(Vector3 position, out float y);   // interpolated inside the cell; false outside
    public Vector3 CellCenter(int x, int z);             // Unity position, y = mean of the four corners
    public Vector4 CornerHeights(int x, int z);          // H00, H10, H01, H11
}
```

- **Storage (FR-5).** Per cell: 16 bytes of heights, 1 byte type, 1 byte flags, 1 byte raw type = **19 bytes**, under the target of 24; flat arrays, no per-cell object, linear in the cell count. The largest map (200 × 200 cubes = 400 × 400 cells = 160,000 cells) is about 3 MB; the tasks measure it. Heights stay `float` (the ground uses the same scale; half precision would break the FR-3 tolerance on tall maps).
- **Conversion at import.** The converter fills the geometry fields from `RoMapSpace` so that **the one function of PLAN-0004 §4.2 is the single source of truth** and the runtime code has only arithmetic: a cell `(x, z)` has corner `(x, z)` at `origin + (x · cellSize, z · cellSize · rowSign)`. Heights are converted with `RoSpace` (Ragnarok Y points down; Unity up).
- **Queries.**
  - `TryGetCell`: `x = floor((p.x - origin.x) / cellSize)`, `z = floor((p.z - origin.y) · rowSign / cellSize)`; out of range gives `false`.
  - `TryGetHeight`: the position inside the cell is split by the cell's diagonal (`diagonalFlip`, same rule as the ground, PLAN-0004 §4.3) and the height is interpolated across the triangle, so it follows the ground surface, not a bilinear patch that the ground does not have.
  - Positions outside the map return `false`; there is no exception path. Queries do not allocate.
- **Settings for SPEC-0006.** The asset also has a `MapSettings settings` field and a `hasSettings` flag, empty after this plan; PLAN-0006 extends the converter to fill it from the RSW (water level, lighting, music name), so there is exactly one map data asset per map and one converter owning it (FR-6, SPEC-0006 FR-7).
- **`MapRoot`** is a small runtime component for scenes: a reference to the map's `MapData` and the map name. SPEC-0006 puts it on the map object; the gizmos of §4.6 attach to it. It holds no logic.
- The asset has no reference to importer code (FR-4, PLAN-0002 FR-14); a test checks the assembly references.

### 4.4 Cell geometry and the ground (FR-3): `RoMapSpace`, `RoGroundSampler`

Satisfies FR-3.

- Cell geometry is `RoMapSpace` of PLAN-0004 with the GND width, height and zoom; a GAT cell `(x, z)` belongs to the ground cube `(x / 2, z / 2)` (`CubeOfGatCell`). Nothing here has its own row direction or offset.
- `RoGroundSampler.HeightAt(RoGnd gnd, RoGndRules rules, Vector3 roPosition, out float h)` (pure, in Core) returns the height of the ground surface at a position by the same top-quad rules (corners, diagonal) as the mesh builder, `false` over a gap. It is used by the FR-3 test and by PLAN-0006's ground contact check.
- **FR-3 check.** For a sample of walkable cells on each reference map (fixed seed: the four corners of the map, the centre, and 100 random cells), the height of the cell centre equals `RoGroundSampler` at the same point within `GroundTolerance`. GAT and GND heights are separate fields in the files and may differ slightly; T0.3 measures the real difference on the three reference maps and writes the tolerance (candidate: 0.05 Unity units) and any systematic difference into this section.

### 4.5 Converter and optional image: `RoMapDataConverter`, `RoGatImageWriter`

Satisfies FR-6, FR-8 (uses SPEC-0002 FR-5, FR-6, FR-10, FR-16).

```csharp
public sealed class RoMapDataConverter : IRoConverter      // registers RoJobKind.MapData; source path is <name>.gat at the data root (D9)
{
    public const int RulesVersion = 1;
    public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);   // returns the MapData asset
}
```

- `RoJobKind.MapData` is added to the enum and `RoConverterRegistry`, with `RoImport.MapData(string mapName)` and `RoImportContext.MapData(RoPath)` so the map converter of PLAN-0006 can ask for it on demand.
- New `RoIssueCode` values: `GatGndMismatch`, `GatUnknownType`, `GatBadHeader`.
- The asset is written with `RoAssetWriter.UpsertObject` (PLAN-0002 §4.7): the first conversion creates it and Unity assigns the GUID; every later one copies the new content into the existing object, so a scene reference (`MapRoot.mapData`) survives (FR-6). One asset per map, at the predictable path of §3, owned by ledger category `mapdata`.
- Outputs are written only after the whole arrays are built (no partial output on failure). Content is deterministic (arrays in cell order, no timestamps), so a second forced conversion is byte-identical (FR-16).
- **Optional type image (FR-8, Should).** `RoGatImage` (pure) makes an RGBA image, one pixel per cell, one colour per `MapCellType` (a fixed palette in the code, listed in the legend of §4.6), row 0 at the bottom as in the map. It is written as a PNG with point filtering and no compression only when the request has `WithImage` set (a window toggle and the batch option `GatImage`), and it is a separate ledger output of the same entry.
- The menu and window gain the kind `MapData` and `Tools/Ragnarok/Convert All Map Data` (`Enumerate("")`, the whole data root, extension `.gat`; D9).

### 4.6 Gizmos: `RoMapDataGizmos`, `RoMapDataOverlay`

Satisfies FR-7.

- `MapData` is an asset, not a scene object, so the gizmos attach to its scene handle: `[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]` on a static method in the editor assembly. It draws only when the `MapRoot` or one of its ancestors or children is selected, and returns immediately otherwise. A map data asset selected in the Project window also shows its overlay information (legend and counts) through a custom inspector.
- **Layers**, each switchable in the "Ragnarok Map Data" overlay, values in `EditorPrefs`: cells coloured by type (one toggle per group: walkable (types 0, 3, 6), blocked (1, 2, 4), snipable (5), other, and water); cell grid; corner heights (labels); legend. A hidden type is not drawn.
- **Bounded cost on large maps.** Drawing is limited to cells within `RoMapDataGizmoSettings.Radius` (default 48 cells) of the scene camera's focus point, or to a rectangle the user picks by dragging in the scene view (a "Region" tool in the overlay); the number of drawn cells is capped (default 6000) and a one-line notice appears when the cap cuts the view. Cells are drawn as batched quads with `Handles`/`Gizmos` calls in a single pass per type.
- The gizmos read the arrays of the `MapData` that already exists in the project; they never read the source.
- Pure helper `RoMapDataGizmoSelector` (which cells are in the region, capped, by type) is tested; the drawing is checked manually.

## 5. Threading and lifecycle

Satisfies FR-4, FR-5.

- Main thread, synchronous, inside the batch of PLAN-0002 §4.10. `RoGat` arrays of one map (160,000 cells at most on the reference maps) are released after the asset is written.
- At runtime nothing of the importer runs: the player has the `MapData` asset and the `Uninarok.Map` assembly only. Queries use no allocation and no Unity API besides `Mathf`, so they can be called from jobs later if arrays are copied out (not part of this plan).

## 6. Test plan

Satisfies every FR of the spec. EditMode NUnit tests (command in `CLAUDE.md` §2). Tests needing the real data are marked `[Category("NeedsData")]`; pure tests build `RoGat` and `MapData` in code.

| Test class | FR | Checks |
|---|---|---|
| `RoGatRulesTests` | 1 | the table of §4.2 (every raw type 0 to 7, the absent cell -1 and a high value), the water flag (bit 31, with each of the types), walkable and snipable per type, `GatUnknownType` once per map with a count; the enum values equal the table of `docs/data/GatCellType.md` |
| `RoGatBuilderTests` | 1, 3 | a synthetic 4 × 3 cube map (8 × 6 cells) builds the expected heights, types and flags per cell; the cell of a known corner lands at the `RoMapSpace` position; non-square maps are not swapped; a grid that is not `2 × GND` gives `GatGndMismatch` and no output |
| `MapDataQueryTests` (runtime) | 2 | `IsWalkable` and `TryGetHeight` for cell (0, 0), the last cell, a cell edge, a diagonal and a position outside (all four sides) give the right result and never throw; the interpolated height on the diagonal equals both triangles; no allocation (`GC.GetAllocatedBytesForCurrentThread` before and after 10,000 calls) |
| `RoGroundSamplerTests` | 3 | a flat, a ramp and a cliff synthetic ground give the expected heights; a gap returns false; the diagonal rule is the one of `RoGndRules` |
| `MapDataStorageTests` | 5 | per-cell storage is at most 24 bytes (arrays' byte count over cell count); growth is linear (two sizes) |
| `MapDataAssemblyTests` | 4 | `Uninarok.Map` references no editor assembly and no importer assembly; the asset class is in it; a built player has no importer type (checked once manually, T7.x) |
| `RoMapDataWriterTests` | 6 | a synthetic map converts to one asset at the path of §3 with the right fields; the second conversion is `Skipped`; a forced conversion leaves the asset byte-identical and its GUID unchanged; a scene object referencing it (`MapRoot`) keeps the reference; the optional image only exists when requested and equals the layout of the cells |
| `RoGatImageTests` | 8 | one pixel per cell, the colour of each type, row direction, deterministic |
| `RoMapDataGizmoSelectorTests` | 7 | region and radius selection, cap, per-type hiding |
| `RoGatReaderTests` (`NeedsData`) | 1 | the three reference maps read through GRF; `Width = 2 × GND width` and `Height = 2 × GND height`; sample cells equal the raw values; a truncated copy returns false with a reason and never throws |
| `RoMapDataReferenceTests` (`NeedsData`) | 1, 2, 3, 5 | sample queries on every reference map equal the source's cells; the centre of every walkable sample cell lies on the ground within `GroundTolerance`; the size of the largest map's data per cell is within the target; a second batch skips everything |

**Manual checks** (recorded in the PR with screenshots):

- FR-7: select the map object of a reference map; walkable and blocked cells show different colours; a toggle hides a type; panning keeps the editor responsive on prt_fild08; deselecting costs nothing in the Profiler.
- FR-8: the image of a reference map shows the same layout as the gizmos.
- FR-4: a development build of an empty scene with `MapRoot` and the `MapData` queries a cell correctly with the source folder absent.

## 7. Migration and compatibility

- `Assets/Scripts/MapData.cs` (`TileType`, `SetMapData`) is deleted after its users are migrated (T6.1); the old `GatImporter.cs` (`Assets/Editor/GatImporter.cs`) is deleted by T6.2, and with it the debug texture it produced (SPEC-0005 §1: gameplay must not depend on a debug texture).
- Existing scenes or scripts that reference the old `MapData` get a recorded migration (list of users and what replaces each) in the PR of T6.1; nothing is migrated silently.
- Output assets in `Assets/Data/map/` are committed as ordinary assets; the asset is a binary-serialised ScriptableObject if the project's asset serialization is binary, otherwise text-serialised with large float arrays; T7.2 records which and the `.gitattributes` line (LFS for `Assets/Data/map/**/*.asset`) is added if the size warrants it.
- PLAN-0006 uses `ctx.MapData(...)`, `MapRoot`, and the `settings` field; the contract between the plans: the path of §3, `MapData`'s public API, and one converter owner.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 (OQ-1) | Four corner heights per cell are stored (19 bytes per cell in total, under the 24 target) | One height per cell (cannot follow slopes; cell centre queries would sink or float on ramps, and the FR-1 acceptance asks for the four corners) |
| D2 (OQ-2) | The cell types are GRF's own `GatType` (owner's answer), defined for the project in `docs/data/GatCellType.md`; water is a flag; walkability, snipability and water are stored per cell as flags | Hard-coding "0 is walkable" (the client has more types); a project-specific set unrelated to GRF's (two definitions to keep in step); reading the server's source (not needed, GRF defines the types) |
| D3 | The runtime asset and its queries are in a runtime assembly `Uninarok.Map` and carry the geometry (origin, cell size, row sign, diagonal) so no importer code is needed at runtime | Putting the asset in an editor assembly (not in builds, FR-4); a second copy of the map space rules at runtime |
| D4 | One `MapData` asset per map, one owning converter; SPEC-0006 extends the same converter for the map-wide settings | Two assets per map (a scene would need two references; FR-6 and SPEC-0006 FR-7 ask for one) |
| D5 | Height queries split the cell by the ground's diagonal | Bilinear interpolation (a surface the ground mesh does not have, a visible step between the data and the ground) |
| D6 | Gizmos attach to `MapRoot` through `DrawGizmo`, the data is the project's own asset | A gizmo component with importer code (not allowed in scenes, FR-14); a custom editor window only (not a gizmo, FR-7) |
| D7 | Cell grid must be exactly `2 × GND` or the map is rejected with `GatGndMismatch` | Silently resampling (hides a wrong pairing of files) |
| D8 | The type image is optional and separate (`WithImage`) | Always generating it (extra assets in every map) |
| D9 | (T0.1, 2026-11-18) Map sources are `<name>.gat` at the data root, as PLAN-0004 D11 found for `.gnd` (owner decision there); the batch enumerates the whole root by extension. The old `MapData` assets sit in `Assets/Data/Map/` (capital M, same folder as `Assets/Data/map/` on Windows): T6.1 deletes them and renames the folder to `map` before any real map is converted (T7.x), so the new asset never upserts into an object of the old class | A `map/` source folder (absent from the data); writing next to the old assets and replacing their script (the old fields would linger in the YAML) |

Items for the owner: approve D1 (per-corner heights), the new runtime assembly `Uninarok.Map`, the replacement of `MapData.cs`, and `docs/data/GatCellType.md` (new).

## 9. Risks

| Risk | Mitigation |
|---|---|
| A type behaves differently from the reading of its GRF name (`Unknown`, `Walkable2`, `Walkable3`) | T0.2 checks the types that occur on the reference maps against the reference viewer; one table, one pinning test, and `docs/data/GatCellType.md` changes with it |
| GAT and GND heights differ more than assumed | T0.3 measures it; the tolerance is a recorded number, any systematic offset is described and decided with the owner |
| GAT version 1.3 water flag or old 1.2 prefix is read wrongly by GRF | T0.2 compares raw bytes; the adapter strips flags and records the version |
| `MapData` for the largest map is large in version control | 19 bytes per cell measured in T7.2; LFS line if needed |
| Gizmos are slow on large maps | Radius, region tool and the cap; manual Profiler check |
| Old scripts depend on the old `MapData` | T0.1 lists the users before anything is deleted |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.1, 4.2, 4.3, 6 |
| FR-2 | 4.3, 6 |
| FR-3 | 4.3, 4.4, 6 |
| FR-4 | 3, 4.3, 5, 6 |
| FR-5 | 4.3, 6 |
| FR-6 | 4.3, 4.5, 6 |
| FR-7 | 4.6, 6 |
| FR-8 | 4.5, 6 |
