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
- OQ-1 (per-corner or per-cell height) is **decided in this plan** (§8, D1). OQ-2 (which types are walkable) is **answered by the owner** (after T0.2, 2026-11-18): the cell types and their meaning follow the Hercules map server (`map_gat2cell`, `src/map/map.cpp`) and use its names; the definition is kept in `docs/data/GatCellType.md` (§4.2, D2).
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

1. Ledger check (hash of the GAT, hash of the GND file, rules version): current → `Skipped`.
2. Read the GAT through GRF into `RoGat`; read the GND through `RoGndReader` only for its width, height and zoom. A cell grid that is not exactly `2 × GND width` by `2 × GND height` gives one `GatGndMismatch` issue and no output.
3. Convert every cell to the stored form (§4.3) with `RoMapSpace`, build the arrays in memory.
4. Upsert `Assets/Data/map/<name>.asset` in place (FR-6) and, if requested, the optional type image (FR-8). Record outputs and dependencies in the ledger (category `mapdata`): the source hash is the GAT file's, the dependency is the GND file's whole-file hash (D12).

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
    public uint[] Types;                      // raw 32-bit terrain type per cell, unchanged (the rules of §4.2 read it, D2)
}
public static class RoGatReader { public static bool TryRead(RoPath path, byte[] bytes, out RoGat gat, out string reason); }
```

- Cell index `row * Width + column`, `Width` from the header; heights follow the grid corner naming of PLAN-0004 §4.1 (`H00` low column and low row).
- The adapter never throws; a rejected read returns `false` with GRF's reason (`CorruptFile`).
- **Mapping check (T0.2, done 2026-11-18).** A scratch script read each reference map through GRF (`new Gat((MultiType)bytes)`) and compared every cell with the raw bytes (four `float` heights at offsets 0 to 12 and the `uint32` type at 16 of each 20-byte cell after the 14-byte header): prontera 122,304 cells, izlude 80,400, prt_fild08 160,000, zero differences. `Cells[i]` is file cell `i`, and GRF's indexer `gat[column, row]` is `Cells[row * Width + column]` on the two non-square maps. Which corner each height is on the ground is checked by T0.3.

  | `RoGat` field | GRF member | Raw file field |
  |---|---|---|
  | `VersionMajor`, `VersionMinor` | none: `GatHeader` exposes only `Width` and `Height`; the adapter reads bytes 4 and 5 itself, next to its `GRAT` magic check (D10) | bytes 4 and 5 after `GRAT` |
  | `Width`, `Height` | `Header.Width`, `Header.Height` (equal to `Gat.Width`, `Gat.Height`) | `int32` at 6, `int32` at 10 |
  | `Heights[4i]` to `[4i+3]` (`H00`, `H10`, `H01`, `H11`) | `Cells[i].H0`, `H1`, `H2`, `H3` | heights `[0]` to `[3]`: south-west, south-east, north-west, north-east |
  | `Types[i]` | `(uint)(int)Cells[i].Type` (`GatType` holds the raw `int32`; nothing is changed while `Gat.AutomaticallyFixNegativeGatTypes` is `false`, its default, which the adapter never sets) | `uint32` type |

  **Not used:** `Cell.Average`, `Cell[k]`, `Cell.CalcNormal`, `Gat[i]`, `Gat[column, row]` (indexers over the same data), `Gat.Image`, `LoadImage`, `IdentifyCells`, `SpecialCells` (preview and analysis helpers), `Gat.LoadedPath`, `GatPreviewImageMaker`.

  **Bit 31 and the old prefix.** GRF keeps bit 31 in `Type` (its `Weird0` to `Weird9` values); none of the reference maps uses it. No reference file has a prefix before `GRAT`: each is exactly `14 + cells × 20` bytes. GRF throws `IndexOutOfRangeException` on a truncated or empty array, so the adapter checks the magic before calling GRF, catches anything GRF throws, and then checks the length against GRF's header (`14 + Width × Height × 20`), with no byte parsing of its own (T2.3).

  **Reference maps** (sample values kept for the reader tests, `H` in Ragnarok units):

  | Map | Version | Cells | Raw types (count) | Heights | Samples |
  |---|---|---|---|---|---|
  | prontera | 1.2 | 312 × 392 | 0: 61,516; 1: 60,788 | -11 to 33.5 | cell 0 `(0, 0, 0, 0)` type 1; cell 311 (col 311, row 0) `(0, 0, 0, 0)` type 0; cell 120531 (col 99, row 386) `(15, 30, 7.5, 15)` type 1 |
  | izlude | 1.2 | 268 × 300 | 0: 14,529; 1: 65,871 | -9.83 to 85 | cell 0 `(59, 59, 59, 59.2625)` type 1; cell 401 (col 133, row 1) `(60.75, 60.75, 62.5, 62.5)` type 1; cell 80399 (last) `(59.32813, 0, 0, 0)` type 0 |
  | prt_fild08 | 1.2 | 400 × 400 | 0: 90,798; 1: 61,303; 5: 7,899 | -97 to 60 | cell 401 (col 1, row 1) `(0, 0, 0, -40)` type 1; cell 13707 (col 107, row 34) `(-1.5, -3, -1, -1.5)` type 5; cell 80200 (col 200, row 200) `(3, 4.5, 3, 4.5)` type 0 |

  Every grid is exactly twice its GND (156 × 196, 134 × 150, 200 × 200 cubes).

### 4.2 Cell types: `RoGatRules`

Satisfies FR-1 (and OQ-2).

```csharp
public enum MapCellType : byte { Walkable = 0, Wall = 1, Type2 = 2, Water = 3, Type4 = 4, Cliff = 5, Type6 = 6, Other = 255 }   // runtime, Uninarok.Map
[Flags] public enum MapCellFlags : byte { None = 0, Walkable = 1, Shootable = 2, Water = 4 }                                      // runtime, Uninarok.Map
public static class RoGatRules
{
    public const int RulesVersion = 1;
    public static MapCellType TypeOf(uint rawType);       // 0 to 6 as themselves; anything else (bit 31 set, -1, above 6) is Other
    public static MapCellFlags FlagsOf(MapCellType type); // the table below
}
```

- **The server decides (owner, 2026-11-18).** Movement is checked by the Hercules map server, so the client reads cells the way the server does: `map_gat2cell` (`src/map/map.cpp` of the Hercules tree in this repository) turns the raw type into three flags, `walkable`, `shootable` and `water`, and the client stores the same three flags with the same names. The type names are the server's where it has one (`cell_chk` and the comments of `map_gat2cell`: walkable ground, wall, walkable water, cliff); types 2, 4 and 6 have no name there ("???") and are `Type2`, `Type4`, `Type6`. **`docs/data/GatCellType.md` is the project's definition of the enum and the flags** and is the source the runtime enums, the rules table and the tests follow.
- **Table** (pinned by `RoGatRulesTests`, equal to `map_gat2cell`):

  | Raw | `MapCellType` | Walkable | Shootable | Water |
  |---:|---|:-:|:-:|:-:|
  | 0 | `Walkable` | yes | yes | no |
  | 1 | `Wall` | no | no | no |
  | 2 | `Type2` | yes | yes | no |
  | 3 | `Water` | yes | yes | yes |
  | 4 | `Type4` | yes | yes | no |
  | 5 | `Cliff` | no | yes | no |
  | 6 | `Type6` | yes | yes | no |
  | other | `Other` | no | no | no |

- **Bit 31 is not water here.** GRF names raw values with bit 31 set `Weird0` to `Weird9`, and the format document calls the bit a water flag of GAT 1.3, but the server does not mask it: such a value is "unrecognized" and the cell gets no flag. The client does the same: the cell is `Other`, blocked and not shootable, and the map reports `GatUnknownType` once with the values and a count. None of the reference maps uses it (T0.2).
- **Water from the RSW water level is not applied here.** The server also turns a type-0 cell whose first height is below the map's water level into type 3 (`map_readgat`). The water level is in the RSW, so this rule belongs to PLAN-0006, which owns the map settings of the same asset (§4.3).
- The **raw type is stored as well** (its low byte) next to the mapped type, so nothing is lost and a rules change needs no new reading of the file.
- The flags are stored per cell in the asset (§4.3) so gameplay never needs the table.

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
    [SerializeField] byte[] flags;                       // MapCellFlags: bit 0 walkable, bit 1 shootable, bit 2 water (docs/data/GatCellType.md)
    [SerializeField] byte[] rawTypes;                    // raw GAT type per cell (low byte; the value range seen is recorded by T0.2)
    // queries (no allocation, no exception)
    public int CellsX { get; } public int CellsZ { get; } public float CellSize { get; }
    public bool TryGetCell(Vector3 position, out int x, out int z);
    public bool InBounds(int x, int z);
    public bool IsWalkable(int x, int z);                // outside the map: false
    public bool IsWalkable(Vector3 position);            // outside the map: false
    public MapCellType TypeAt(int x, int z);             // outside: Other
    public MapCellFlags FlagsAt(int x, int z);           // outside: None
    public bool IsWalkableWater(int x, int z);           // walkable and water flag; outside: false
    public bool TryGetHeight(Vector3 position, out float y);   // interpolated inside the cell; false outside
    public Vector3 CellCenter(int x, int z);             // Unity position, y = height at the centre by the diagonal rule (T0.3)
    public Vector4 CornerHeights(int x, int z);          // H00, H10, H01, H11
}
```

- **Storage (FR-5, D14).** Per cell: 16 bytes of heights, 1 byte type, 1 byte flags, 1 byte raw type = **19 bytes of data**; flat arrays, no per-cell object, linear in the cell count. No compression and no size optimization: the asset is written as Unity serializes it. With the project's text serialization (`ForceText`) arrays are written as hex, so the file is about twice the data (T7.2: prt_fild08, 160,000 cells, 6.5 MB, about 41 bytes per cell). Heights stay `float` (the ground uses the same scale; half precision would break the FR-3 tolerance on tall maps). `flags` and `rawTypes` can be rebuilt from `types` for raw values 0 to 6; they are kept so gameplay never needs the table and nothing of the file is lost.
- **Conversion at import.** The converter fills the geometry fields from `RoMapSpace` so that **the one function of PLAN-0004 §4.2 is the single source of truth** and the runtime code has only arithmetic: a cell `(x, z)` has corner `(x, z)` at `origin + (x · cellSize, z · cellSize · rowSign)`. Heights are converted with `RoSpace` (Ragnarok Y points down; Unity up).
- **Queries.**
  - `TryGetCell`: `x = floor((p.x - origin.x) / cellSize)`, `z = floor((p.z - origin.y) · rowSign / cellSize)`; out of range gives `false`.
  - `TryGetHeight`: the position inside the cell is split by the cell's diagonal (`diagonalFlip`, same rule as the ground, PLAN-0004 §4.3) and the height is interpolated across the triangle, so it follows the ground surface, not a bilinear patch that the ground does not have.
  - Positions outside the map return `false`; there is no exception path. Queries do not allocate.
- **Settings for SPEC-0006.** The asset also has a `MapSettings settings` field and a `hasSettings` flag, empty after this plan; PLAN-0006 extends the converter to fill it from the RSW (water level, lighting, music name), so there is exactly one map data asset per map and one converter owning it (FR-6, SPEC-0006 FR-7).
- **Filling the asset.** `internal void Assign(name, cellsX, cellsZ, cellSize, origin, rowSign, diagonalFlip, heights, types, flags, rawTypes)` sets every field at once and throws on inconsistent lengths (importer code, never gameplay); `Uninarok.Map` grants its internals to `Uninarok.RoImport.Editor` and the two test assemblies (`InternalsVisibleTo`), which keeps the public API read-only and adds no reference from the runtime to the importer (T1.1).
- **`MapRoot`** is a small runtime component for scenes: a reference to the map's `MapData` and the map name. SPEC-0006 puts it on the map object; the gizmos of §4.6 attach to it. It holds no logic.
- The asset has no reference to importer code (FR-4, PLAN-0002 FR-14); a test checks the assembly references.

### 4.4 Cell geometry and the ground (FR-3): `RoMapSpace`, `RoGroundSampler`

Satisfies FR-3.

- Cell geometry is `RoMapSpace` of PLAN-0004 with the GND width, height and zoom; a GAT cell `(x, z)` belongs to the ground cube `(x / 2, z / 2)` (`CubeOfGatCell`). Nothing here has its own row direction or offset.
- `RoGroundSampler.HeightAt(RoGnd gnd, RoGndRules rules, Vector3 roPosition, out float h)` (pure, in Core) returns the height of the ground surface at a position by the same top-quad rules (corners, diagonal) as the mesh builder, `false` over a gap. It is used by the FR-3 test and by PLAN-0006's ground contact check.
- **FR-3 check (owner, 2026-11-18, after T0.3).** Over every walkable cell of each reference map (types with the walkable flag, cell over a ground top), the GAT height at the cell centre (`TryGetHeight`, diagonal rule) is compared with `RoGroundSampler` at the same point:
  1. **Never below the ground:** no centre is more than `GroundTolerance = 0.3` Unity units (3 Ragnarok units) below the ground. Centres above the ground by more than that are walkable model surfaces (bridges, decks); they are allowed and their count is reported.
  2. **On the ground:** on prontera and prt_fild08 at least `OnGroundShare = 98%` of walkable cells are within `OnGroundTolerance = 0.05` Unity units. A grid with mirrored rows gives 69.6% and 20.3%, so the check catches a wrong row direction or a swapped grid. izlude (a town on decks) is reported, not checked.
- **Measurements (T0.3, 2026-11-18).** A scratch script read each reference map's GAT through GRF and GND through `RoGndReader`, and computed the ground at each walkable cell centre with the 1.7 rules (corners `H00` to `H11` of cube `cell / 2`, diagonal `H10`–`H01`). Results, Ragnarok units unless stated:

  | Map | Walkable cells over ground | Corner `H00` equal to the ground (exact) | Within ±0.05 Unity (diagonal centre) | Rows mirrored | More than 1 RO above the ground | Below the ground: p99 / max |
  |---|---|---|---|---|---|---|
  | prontera | 60,813 | 60,176 (99.0%) | 99.73% | 69.64% | 162 (0.3%) | 0 / 0.25 |
  | izlude | 14,529 | 11,392 (78.4%) | 78.00% | 51.73% | 3,061 (21.1%) | 0.25 / 3.0 |
  | prt_fild08 | 90,001 | 81,460 (90.5%) | 98.89% | 20.30% | 148 (0.2%) | 0.5 / 2.06 |

  Findings: GAT heights have the same sign and scale as the ground (Ragnarok Y down; the opposite sign is far worse); cell `(x, z)` lies in cube `(x / 2, z / 2)` with the rows of `RoMapSpace.RowDirection` on the two non-square maps (no swap, no mirror); there is no systematic offset (the small differences are symmetric, up to about 1 RO, from cells whose corners do not follow the ground's triangles exactly). The centre height by the diagonal rule fits the ground better than the mean of the four corners (prt_fild08: 492 against 2,361 cells more than 0.5 RO below), so `CellCenter` uses it.

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
- **Layers**, each switchable in the "Ragnarok Map Data" overlay, values in `EditorPrefs`: cells coloured by type (one toggle per group: walkable (types 0, 2, 4, 6), water (3), wall (1), cliff (5), other); cell grid; corner heights (labels); legend. A hidden type is not drawn.
- **Bounded cost on large maps.** Drawing is limited to cells within `RoMapDataGizmoSettings.Radius` (default 38 cells, D13) of the scene camera's focus point, or to a rectangle the user picks by dragging in the scene view (a "Region" tool in the overlay); the number of drawn cells is capped (default 6000), cells are taken nearest first (rings around the focus), and a one-line notice appears when the cap cuts the view. Cells are drawn as batched quads with `Handles`/`Gizmos` calls in a single pass per type.
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
| `RoGatRulesTests` | 1 | the table of §4.2 (every raw type 0 to 7, the absent cell -1, bit 31 with each type and a high value), walkable, shootable and water per type, `GatUnknownType` once per map with a count; the enum values equal the table of `docs/data/GatCellType.md` |
| `RoGatBuilderTests` | 1, 3 | a synthetic 4 × 3 cube map (8 × 6 cells) builds the expected heights, types and flags per cell; the cell of a known corner lands at the `RoMapSpace` position; non-square maps are not swapped; a grid that is not `2 × GND` gives `GatGndMismatch` and no output |
| `MapDataQueryTests` (runtime) | 2 | `IsWalkable` and `TryGetHeight` for cell (0, 0), the last cell, a cell edge, a diagonal and a position outside (all four sides) give the right result and never throw; the interpolated height on the diagonal equals both triangles; no allocation (`GC.GetAllocatedBytesForCurrentThread` before and after 10,000 calls) |
| `RoGroundSamplerTests` | 3 | a flat, a ramp and a cliff synthetic ground give the expected heights; a gap returns false; the diagonal rule is the one of `RoGndRules` |
| `MapDataStorageTests` | 5 | the data is 19 bytes per cell (arrays' byte count over cell count, pins the layout of 4.3); growth is linear (two sizes) |
| `MapDataAssemblyTests` | 4 | `Uninarok.Map` references no editor assembly and no importer assembly; the asset class is in it; a built player has no importer type (checked once manually, T7.x) |
| `RoMapDataWriterTests` | 6 | a synthetic map converts to one asset at the path of §3 with the right fields; the second conversion is `Skipped`; a forced conversion leaves the asset byte-identical and its GUID unchanged; a scene object referencing it (`MapRoot`) keeps the reference; the optional image only exists when requested and equals the layout of the cells |
| `RoGatImageTests` | 8 | one pixel per cell, the colour of each type, row direction, deterministic |
| `RoMapDataGizmoSelectorTests` | 7 | region and radius selection, cap, per-type hiding |
| `RoGatReaderTests` (`NeedsData`) | 1 | the three reference maps read through GRF; `Width = 2 × GND width` and `Height = 2 × GND height`; sample cells equal the raw values; a truncated copy returns false with a reason and never throws |
| `RoMapDataReferenceTests` (`NeedsData`) | 1, 2, 3, 5 | sample queries on every reference map equal the source's cells; the two-part FR-3 check of §4.4; the size of the largest map's data per cell is within the target; a second batch skips everything |

**Manual checks** (recorded in the PR with screenshots):

- FR-7: select the map object of a reference map; walkable and blocked cells show different colours; a toggle hides a type; panning keeps the editor responsive on prt_fild08; deselecting costs nothing in the Profiler.
- FR-8: the image of a reference map shows the same layout as the gizmos.
- FR-4: a development build of an empty scene with `MapRoot` and the `MapData` queries a cell correctly with the source folder absent.

## 7. Migration and compatibility

- `Assets/Scripts/MapData.cs` (`TileType`, `SetMapData`) is deleted after its users are migrated (T6.1); the old `GatImporter.cs` (`Assets/Editor/GatImporter.cs`) is deleted by T6.2, and with it the debug texture it produced (SPEC-0005 §1: gameplay must not depend on a debug texture).
- Existing scenes or scripts that reference the old `MapData` get a recorded migration (list of users and what replaces each) in the PR of T6.1; nothing is migrated silently.
- Output assets in `Assets/Data/map/` are committed as ordinary assets, text-serialised (the project uses `ForceText`); T7.2 records their sizes. No `.gitattributes` LFS line is added in this spec (D14); size handling, if needed later, is in `BACKLOG.md`.
- PLAN-0006 uses `ctx.MapData(...)`, `MapRoot`, and the `settings` field; the contract between the plans: the path of §3, `MapData`'s public API, and one converter owner.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 (OQ-1) | Four corner heights per cell are stored (19 bytes of data per cell in total) | One height per cell (cannot follow slopes; cell centre queries would sink or float on ramps, and the FR-1 acceptance asks for the four corners) |
| D2 (OQ-2) | (owner, 2026-11-18, after T0.2) The cell types and flags follow the Hercules map server's `map_gat2cell` and use its names (`walkable`, `shootable`, `water`; wall, walkable water, cliff; `Type2`, `Type4`, `Type6` where the server has no name), defined for the project in `docs/data/GatCellType.md`; the flags are stored per cell; a raw value with bit 31 set is `Other`, as on the server | GRF's enum names read as rules (first draft: 2 and 4 blocked, 3 plain walkable, bit 31 as water), which disagrees with the server on types 2, 3 and 4 and on bit 31; hard-coding "0 is walkable" |
| D3 | The runtime asset and its queries are in a runtime assembly `Uninarok.Map` and carry the geometry (origin, cell size, row sign, diagonal) so no importer code is needed at runtime | Putting the asset in an editor assembly (not in builds, FR-4); a second copy of the map space rules at runtime |
| D4 | One `MapData` asset per map, one owning converter; SPEC-0006 extends the same converter for the map-wide settings | Two assets per map (a scene would need two references; FR-6 and SPEC-0006 FR-7 ask for one) |
| D5 | Height queries split the cell by the ground's diagonal | Bilinear interpolation (a surface the ground mesh does not have, a visible step between the data and the ground) |
| D6 | Gizmos attach to `MapRoot` through `DrawGizmo`, the data is the project's own asset | A gizmo component with importer code (not allowed in scenes, FR-14); a custom editor window only (not a gizmo, FR-7) |
| D7 | Cell grid must be exactly `2 × GND` or the map is rejected with `GatGndMismatch` | Silently resampling (hides a wrong pairing of files) |
| D8 | The type image is optional and separate (`WithImage`) | Always generating it (extra assets in every map) |
| D9 | (T0.1, 2026-11-18) Map sources are `<name>.gat` at the data root, as PLAN-0004 D11 found for `.gnd` (owner decision there); the batch enumerates the whole root by extension. The old `MapData` assets sit in `Assets/Data/Map/` (capital M, same folder as `Assets/Data/map/` on Windows): T6.1 deletes them and renames the folder to `map` before any real map is converted (T7.x), so the new asset never upserts into an object of the old class | A `map/` source folder (absent from the data); writing next to the old assets and replacing their script (the old fields would linger in the YAML) |
| D10 | (T0.2) The GAT version is read by the adapter from bytes 4 and 5, next to its magic and length checks, because GRF does not expose it | Dropping the version from `RoGat` (reports would not show it) |
| D11 | (owner, after T0.3) FR-3 is checked in two parts: never more than 0.3 Unity units below the ground, and at least 98% of walkable cells within 0.05 units on the maps without large model floors; `CellCenter` uses the diagonal rule | One tolerance for every walkable cell (fails on bridges and decks: 21% of izlude); the mean of four corners (worse fit) |
| D12 | (T4.2) The ledger records the GND as a dependency by its whole-file hash, because the ledger compares file hashes; a GND change converts the map data again (cheap: arrays only). The ledger rules number is `RulesVersion * 1000 + RoGatRules.RulesVersion`, so a change of the cell table converts again. An image requested for a current map converts it again; an image made earlier stays owned when a later run does not ask for it | A GND header hash (the ledger has no partial-file dependency); deleting an earlier image when not requested (surprising output loss) |
| D13 | (T5.1) Gizmo radius default 38 (77 × 77 = 5,929 cells, under the cap of 6,000) and cells taken in rings nearest the focus first, so the default view is never cut and a cut view keeps the nearest cells | Radius 48 (97 × 97 = 9,409 cells: the cap would always cut it and the notice would always show) |
| D14 | (owner, 2026-11-18, T7.2) No compression and no size optimization of the map data for now; FR-5 asks for flat, linear storage only. The text-serialised file is about twice the data (hex); it can be optimized later | Deflating the arrays into one byte array unpacked on load (measured: 0.5 to 4.3 bytes per cell on disk), deferred to the backlog; a 24-byte per-cell target on the file (the first draft) |

Items for the owner: approve D1 (per-corner heights), the new runtime assembly `Uninarok.Map`, the replacement of `MapData.cs`, and `docs/data/GatCellType.md` (new).

## 9. Risks

| Risk | Mitigation |
|---|---|
| The client reads a cell differently from the server | The table is the server's `map_gat2cell` (D2); one table, one pinning test, and `docs/data/GatCellType.md` changes with it |
| GAT and GND heights differ more than assumed | T0.3 measures it; the tolerance is a recorded number, any systematic offset is described and decided with the owner |
| GAT version 1.3 bit 31 or an old prefix is read wrongly by GRF | T0.2 compared raw bytes (exact); bit 31 is kept in the raw type and mapped to `Other`, as on the server |
| `MapData` for the largest map is large in version control | Accepted for now (D14): about 6.5 MB for prt_fild08 in text; compression or LFS later if it becomes a problem (`BACKLOG.md`) |
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
