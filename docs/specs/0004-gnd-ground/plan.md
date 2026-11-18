# PLAN-0004: GND Ground

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0004) |
| **Status** | Approved |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Shared rules (paths, units and axes, textures, materials, ledger, results, batch) come from [PLAN-0002](../0002-ro-import-foundation/plan.md) and are used here, not restated. The reference maps are in [`../0003-rsm-models/reference-models.md`](../0003-rsm-models/reference-models.md) §4.

## 1. Constraints and assumptions

- Code lives in the editor assemblies of PLAN-0002 under `Core/Gnd/`, `Core/Formats/` (adapter) and `Editor/Gnd/`; main thread only.
- **GND files are read by the GRF library** (the `Gnd` class of `GRF.dll`) through one adapter, `RoGndReader`, in `Core/Formats/`. No code of ours parses GND bytes (PLAN-0002 §1). After the adapter everything works on our own data type `RoGnd` (§4.1), so every rule is testable without the library and without data files.
- The GRF members the old importer used are `Header.Width`, `Header.Height`, `Header.ProportionRatio`, `Textures`, `Tiles` (`TextureIndex`, `TexCoords[0..3]`) and `Cubes` (`BottomLeft`, `BottomRight`, `TopLeft`, `TopRight`, `TileUp`, `TileFront`, `TileSide`); GRF also has lightmap and tile colour members, which are not read (nothing is baked, see below). The mapping of each to a file field is verified by task T0.2 before a rule depends on it.
- The format facts used here come from the community GND document (`rdw-archive/RagnarokFileFormats`, `GND.MD`, which the owner named as the reference for every GND doubt) and the old importer: cubes are stored bottom (south) row first and left (west) to right, like pixels of a bitmap, so the index is `row * width + column`; each cube has four corner heights (bottom-left, bottom-right, top-left, top-right) and three surface ids, **top**, **north wall** and **east wall**; a cube's south, west and bottom sides come from its neighbours' surfaces; a surface is a tile with four texture coordinate pairs; a surface id of `-1` means none; each surface is two GAT cells wide. The points that document leaves open (§4.3, §4.2) are verified by T0.3.
- The shared map-space rules (cube to world, centre offset, row direction) are defined once here, in `RoMapSpace` (§4.2), and are used by PLAN-0005 (GAT) and PLAN-0006 (RSW).
- Outputs are written through `RoAssetWriter`, in place, owned by ledger category `ground` (PLAN-0002 §4.7, §4.8). Materials come from `RoMaterialFactory` (PLAN-0002 §4.6).
- OQ-2 of the spec is **answered by the owner: nothing is baked, Unity's real-time lighting and shadows are used**. FR-8 (the file's baked ground lighting) is therefore not implemented; the lightmap members of the file are ignored.

## 2. Architecture

Satisfies FR-1, FR-2, FR-3, FR-6.

```
   x.gnd ──(IRoSource)──> bytes
                             │
                   RoGndReader (GRF adapter)                       Core/Formats
                             │
                             ▼
                   RoGnd  (our data: header, tiles, cubes)         Core/Gnd
                             │
      ┌──────────────────────┼───────────────────────────────┐
      ▼                      ▼                               ▼
  RoMapSpace            RoGndSurfaces                   RoGndChunker
  (cube ↔ world)        (tops, walls: quads)            (16 x 16 cubes per chunk)
      └──────────────────────┴──────────────┬────────────────┘
                                            ▼
                         RoGndMeshBuilder  (vertices, uv, normals, submeshes)
                                            │  plain arrays
                                            ▼
                    RoGndConverter  (Editor/Gnd: ctx, materials, meshes, prefab, ledger)
                    meshes ── ground prefab
```

Data flow for one request `RoImport.Ground("prontera")` (the ground of the map named `prontera`, source `prontera.gnd`; the map files sit directly in the data root, as in the original client, confirmed by T0.1, see D11):

1. Ledger check (source hash, rules version, texture dependencies): current → `Skipped`.
2. Read through GRF into `RoGnd`; failure is one `CorruptFile` issue and no output.
3. Build the list of top and wall quads from the cube grid (§4.3), compute welded normals over the whole map, cut the map into chunks, and build one mesh per chunk (§4.4).
4. Resolve each texture through `ctx.Texture` and its material through `ctx.Material` (shared per texture, FR-6).
5. Write chunk meshes, build and save the `Ground` prefab, record outputs and dependencies in the ledger.

Because normals and corner positions are computed on the whole map and only then cut into chunks, a chunk border can never open a crack or a lighting seam (FR-3, FR-7).

## 3. Assemblies and file layout

Satisfies FR-6.

| Path (under `Assets/Editor/RoImport/`) | Contents | Assembly |
|---|---|---|
| `Core/Formats/RoGndReader.cs` | adapter: GRF `Gnd` → `RoGnd` (the only GND code that touches GRF types) | `Uninarok.RoImport.Core` |
| `Core/Gnd/Data/` | `RoGnd`, `RoGndTile`, `RoGndCube` | Core |
| `Core/Gnd/Space/` | `RoMapSpace` (shared with PLAN-0005 and PLAN-0006) | Core |
| `Core/Gnd/Surfaces/` | `RoGndRules`, `RoGndSurfaces`, `RoGndQuad` | Core |
| `Core/Gnd/Mesh/` | `RoGndChunker`, `RoGndMeshBuilder`, `RoGndMeshData` | Core |
| `Editor/Gnd/` | `RoGndConverter` (`IRoConverter`), `RoGndPrefabBuilder`, `RoGndOutputPaths` | `Uninarok.RoImport.Editor` |
| `Assets/Tests/EditMode/RoImport/Gnd/` | tests; synthetic maps built in code | `Uninarok.RoImport.Tests` |

Output paths (through `RoOutputPaths.Mirror`, PLAN-0002 §4.5), for a source `<name>.gnd` at the data root (the output keeps the `map/<name>/` folder, built from the map name, D11):

| Output | Path |
|---|---|
| Ground prefab | `Art/Imported/map/<name>/<name>_ground.prefab` (root named after the file, `<name>_ground`, D14; one child `Chunk_<cx>_<cz>` per chunk) |
| Chunk mesh | `Art/Imported/map/<name>/ground/chunk_<cx>_<cz>.asset` |

## 4. Component contracts

### 4.1 Reading: `RoGndReader` and the data type `RoGnd`

Satisfies FR-4, FR-5.

```csharp
public sealed class RoGnd
{
    public int VersionMajor, VersionMinor;
    public int Width, Height;                      // cubes along x and along rows; from the header, never assumed equal
    public float Zoom;                             // cube edge in Ragnarok units (ProportionRatio; 10 for the reference maps)
    public IReadOnlyList<string> Textures;         // as stored, relative to texture/
    public IReadOnlyList<RoGndTile> Tiles;
    public IReadOnlyList<RoGndCube> Cubes;         // Width * Height entries, index = row * Width + column
}
public readonly struct RoGndTile { public Vector2 Uv00, Uv10, Uv01, Uv11; public int Texture; }
public readonly struct RoGndCube { public float H00, H10, H01, H11; public int TileUp, TileFront, TileSide; }   // -1 = none
public static class RoGndReader { public static bool TryRead(RoPath path, byte[] bytes, RoReport report, out RoGnd gnd, out string reason); }   // report may be null (D13)
```

- Names follow the grid: `H00` is the corner at the low column and low row, `H10` the next column, `H01` the next row, `H11` both. The format document names these corners bottom-left (south-west), bottom-right, top-left and top-right (north-east), so GRF's `BottomLeft`, `BottomRight`, `TopLeft`, `TopRight` map to `H00`, `H10`, `H01`, `H11`; the tile's coordinates are stored in the same order (all U values, then all V values: bottom-left, bottom-right, top-left, top-right) and go to `Uv00`, `Uv10`, `Uv01`, `Uv11`. T0.2 confirms both against raw header and cube values. Heights are in Ragnarok units, **unconverted** (Y points down, so a higher ground has a lower number).
- `Width` and `Height` come from the header, and every index is `row * Width + column`. The old importer used `rightCubeIndex + z * height` for the neighbour, which is correct only on square maps; that expression does not exist in the new code, and the row/column count is covered by tests on non-square maps (FR-4).
- Tile id `-1` means none; `0` is a valid tile. Every comparison is `>= 0`.
- A tile index or texture index outside its table is reported as `GndBadIndex` once per map (with a count) into the `report` passed to `TryRead`, and treated as none: the cube's tile id becomes `-1`, the tile's texture index becomes `-1` (D13).
- The adapter never throws; a rejected read returns `false` with GRF's reason.
- **Versions.** The format has unversioned, 1.5, 1.7, 1.8 and 1.9 files; they differ in the lightmap and water parts, which this plan does not use, so the geometry rules are the same for all of them. A version GRF cannot read is reported once as `UnsupportedGndVersion` (§4.6). The cube and tile fields of the versions the data uses are checked in T0.1/T0.2. The texture-coordinate V axis of the file points down; the conversion to Unity's texture space is the one of PLAN-0002 §4.4 and is checked by T0.3.
- **Mapping check (T0.2, done 2026-11-18).** A scratch script read each reference map through GRF (`new Gnd((MultiType)bytes)`) and, separately, decoded the raw bytes of the same file following `GND.MD`. Every header value, texture name, tile (`TextureIndex`, the four coordinate pairs) and cube (four heights, three tile ids) is equal in both: prontera 30,576 cubes and 28,511 tiles, izlude 20,100 and 20,358, prt_fild08 40,000 and 39,596, zero differences. Cubes are stored in file order, so `Cubes[i]` is file cube `i`, and GRF's own indexer `gnd[column, row]` is `Cubes[row * Width + column]` on the two non-square maps. Which grid corner and which direction each slot is on the ground is a geometry question, checked by T0.3.

  | `RoGnd` field | GRF member | Raw file field (`GND.MD`) |
  |---|---|---|
  | `VersionMajor`, `VersionMinor` | `Header.MajorVersion`, `Header.MinorVersion` | bytes 4 and 5 after `GRGN` |
  | `Width`, `Height` | `Header.Width`, `Header.Height` | `int32` width, `int32` height |
  | `Zoom` | `Header.ProportionRatio` | `float` zoom |
  | `Textures[i]` | `Textures[i]` (strings; read with `EncodingService.DisplayEncoding = Korean`, as `RoRsmReader` does, PLAN-0003 D9) | texture names, `TexturePathSize` (80) bytes each, CP949 |
  | `Tiles[i].Texture` | `Tiles[i].TextureIndex` (`short`) | `int16` texture id |
  | `Tiles[i].Uv00`, `Uv10`, `Uv01`, `Uv11` | `Tiles[i].TexCoords[0]`, `[1]`, `[2]`, `[3]` (`X` = u, `Y` = v) | `u[0..3]` then `v[0..3]`: bottom-left, bottom-right, top-left, top-right |
  | `Cubes[i].H00`, `H10`, `H01`, `H11` | `Cubes[i].BottomLeft`, `BottomRight`, `TopLeft`, `TopRight` | heights `[0]`, `[1]`, `[2]`, `[3]` |
  | `Cubes[i].TileUp`, `TileFront`, `TileSide` | `Cubes[i].TileUp`, `TileFront`, `TileSide` | `int32` top, north, east surface ids (version 1.6 and later; `int16` before) |

  **Not used** (derived, layout-only or lighting): `Header.Version`, `Header.Magic`, `Header.BuildNumber`, `Header.UnknownData`, `Header.HexVersionFormat`, `Header.TextureCount`, `Header.TexturePathSize`; `Gnd.TexturesPath` (the same list as `Textures`), `Gnd[column, row]` and `Cube[k]` (indexers over the same data), `Cube.Average` (computed); `Gnd.LightmapWidth`, `LightmapHeight`, `LightmapSizeCell`, `PerCell`, `Lightmaps`, `Tile.LightmapIndex`, `Tile.TileColor` (baked lighting, FR-8 not implemented); `Gnd.Water` (water is a non-goal); `Gnd.LoadedPath`; the `UnsafeGnd`, `UCube` and `UTile` types.

  **Reference maps:**

  | Map | Version | Width × Height | Zoom | Textures | Tiles | Tile ids `-1` / `0` / out of range |
  |---|---|---|---|---|---|---|
  | prontera | 1.7 | 156 × 196 | 10 | 12 | 28,511 | 63,217 / 1 / 0 |
  | izlude | 1.7 | 134 × 150 | 10 | 21 | 20,358 | 39,942 / 1 / 0 |
  | prt_fild08 | 1.7 | 200 × 200 | 10 | 21 | 39,596 | 80,404 / 1 / 0 |

  All three are version 1.7, so the first `RoGndRules` row is 1.7. Tile `0` is used by real cubes (for example prt_fild08 cube 0 has top 0, north 1, east 2), which confirms that `0` is a valid tile. Every tile's texture index is inside the texture table. Examples kept for the reader tests: izlude cube 0 is `H = (59, 59, 59, 60.05)`, top 0, and cube 132 (column 132, row 0) is `H = (59, 59, 61.1, 60.05)`, top 132, north 133; prt_fild08 tile 20100 has `u = (1, 0, 1, 0)`, `v = (1, 1, 0, 0)`, texture 0.

### 4.2 Map space: `RoMapSpace` (shared contract)

Satisfies FR-4, FR-5. Also used by PLAN-0005 and PLAN-0006.

```csharp
public readonly struct RoMapSpace
{
    public RoMapSpace(int width, int height, float zoom);
    public int Width { get; } public int Height { get; } public float Zoom { get; }          // cubes, cubes, RO units
    public Vector3 CornerRo(int column, int row, float roHeight);                            // RO-space position of the grid corner (column, row)
    public Vector3 CornerUnity(int column, int row, float roHeight);                         // RoSpace.Point(CornerRo(...))
    public Vector3 CentreOffsetRo { get; }                                                   // (Width * Zoom / 2, 0, Height * Zoom / 2)
    public Vector3 FromRswRo(Vector3 rswPosition);                                           // RSW position (relative to the centre) → RO map space
    public bool TryCubeAtUnity(Vector3 unityPosition, out int column, out int row);
    public static (int cubeColumn, int cubeRow) CubeOfGatCell(int gatColumn, int gatRow);   // GAT is 2x resolution: cell / 2
}
```

- `CornerRo(column, row, h) = (column · Zoom, h, row · Zoom · RowSign)` where `RowSign` is `+1` or `-1` and is one constant, `RoMapSpace.RowDirection`. The format document says only that row 0 is the south edge and rows grow to the north; which Ragnarok axis that is, it does not say, so the constant is **verified by T0.3** against the reference viewer and against the placement of RSW objects in PLAN-0006 (the old importer assumed a plain row index; the data decides). Everything else (GAT cells, RSW positions, gizmos, picking) derives from this one function.
- The cube edge is `Zoom` taken from the header, not a constant (FR-5): a synthetic map with `Zoom = 5` yields cubes of 0.5 Unity units; heights keep their own scale (`RoSpace.Point`), so a ground cube of the default zoom is exactly 1 unit wide, as SPEC-0002 FR-7 requires.
- `CentreOffsetRo` is the translation RSW positions are relative to; PLAN-0006 uses `FromRswRo` and verifies it on the ground-contact sample.
- `RoMapSpace` is pure and takes only numbers, so PLAN-0005 and PLAN-0006 test against it directly.

### 4.3 Surfaces: `RoGndRules`, `RoGndSurfaces`

Satisfies FR-1, FR-2, FR-3.

```csharp
public enum RoGndFace { Top, FrontWall, SideWall }
public readonly struct RoGndQuad { public RoGndFace Face; public int Column, Row; public int Tile; public Vector3 P00, P10, P01, P11; /* RO space */ }
public static class RoGndSurfaces { public static IEnumerable<RoGndQuad> Enumerate(RoGnd gnd, RoGndRules rules, RoReport report); }
```

- **Top (FR-1).** A cube with `TileUp >= 0` produces one quad whose corners are the cube's four heights at the four grid corners; the tile's four texture coordinates go to the matching corners (`Uv00` to the `H00` corner, and so on). A cube with `TileUp < 0` produces nothing: a visible gap (FR-1).
- **Front wall (the file's north wall).** A cube with `TileFront >= 0` and a cube in the next row produces a wall at the shared row edge, from this cube's `H01`/`H11` (its edge at the next row) to the next-row cube's `H00`/`H10` (its edge at this row). The wall belongs to the cube that carries `TileFront` and uses that tile (FR-2).
- **Side wall (the file's east wall).** A cube with `TileSide >= 0` and a cube in the next column produces a wall at the shared column edge, from this cube's `H10`/`H11` to the next-column cube's `H00`/`H01`, with the tile `TileSide` of this cube.
- **Only where heights differ (FR-2).** The format document says the client draws a wall only when the neighbour is higher or lower and the surface is textured; so a wall is emitted only if the heights differ at the shared edge by more than `Epsilon` (0.0001 Ragnarok units) at one end or both. When they are equal at one end the quad degenerates to a triangle: the zero-area triangle is not emitted. No wall is emitted when its tile is `-1`.
- **Edges (FR-2).** The last row has no front neighbour and the last column no side neighbour; those walls are skipped without reading outside the grid and without a warning (an expected condition).
- **Rules table `RoGndRules`** holds the choices that the data must confirm (T0.3), each as one named constant:
  1. the diagonal that splits a top quad into two triangles (the format document says the original client connects the bottom-right corner to the top-left corner, and that the other diagonal gives slope glitches: so the candidate is `H10`–`H01`, **not** the `H00`–`H11` the old importer used; T0.3 confirms it on a sloped area),
  2. which way a wall faces (candidate: toward the side whose ground is lower, so that the outer face of a cliff is visible and the back is culled),
  3. the UV corner order of the tile for walls (candidate: as for tops),
  4. `RowDirection` (§4.2).
  A rules change touches this one place and the tests that pin it; it never touches the builder's structure.
- **Verified rules (T0.3, 2026-11-18).** A scratch script built throwaway meshes of the three reference maps from the GRF values of §4.1 with the candidate rules; the owner compared them with the reference viewer (whole maps top-down, the izlude cliff at cube 99,102 from all four sides, the izlude slope at cube 23,76, the prontera gaps at cube 45,89, and the prt_fild08 west and south borders) and **all match**. Final values, row `1.7` of `RoGndRules` (the only version in the data):

  | Rule | Value |
  |---|---|
  | Top diagonal | `H10`–`H01`: triangles `(00, 10, 01)` and `(10, 11, 01)` |
  | Wall facing | toward the side whose edge is lower, comparing the sum of the two end heights of this cube's edge with the neighbour's (Ragnarok Y points down, so the larger sum is lower); the back is culled |
  | Wall UV order | as for tops, `Uv00` and `Uv10` on this cube's edge (low column or row first), `Uv01` and `Uv11` on the neighbour's edge: front wall `Uv00`→ this `H01`, `Uv10`→ this `H11`, `Uv01`→ next `H00`, `Uv11`→ next `H10`; side wall `Uv00`→ this `H10`, `Uv10`→ this `H11`, `Uv01`→ next `H00`, `Uv11`→ next `H01` |
  | `RowDirection` | `+1`: `CornerRo(column, row, h) = (column · Zoom, h, row · Zoom)` |
  | V axis | Unity UV is `(u, 1 − v)`, as for RSM (PLAN-0003 §4.6), with textures stored bottom row first by Unity |

  **Row direction against RSW objects.** Every model of each RSW was placed on the ground with each of the four sign choices for x and z, and the ground height under it compared with its own height. Only `column = (x + Width · Zoom / 2) / Zoom`, `row = (z + Height · Zoom / 2) / Zoom` (no sign change, so `FromRswRo` adds `CentreOffsetRo`) puts the models on the ground: prontera 87.6 % of 1,303 models within 1 unit (best other choice 84.7 %, the city is almost flat), prt_fild08 76.8 % of 570 (best other 45.1 %), izlude 40.9 % of 552 (best other 25.5 %; bridges, the airship and the arena are not on the ground). PLAN-0006 still verifies `FromRswRo` on its own samples.

  **Result by map:** prontera 28,498 tops, 0 walls (the ground is flat; its walls are models), 2 enclosed gaps visible as holes; izlude 20,100 tops, 244 walls; prt_fild08 39,206 tops, 355 walls, including the dark `BACKSIDE` skirt along the south and west borders. All match the reference viewer.
- **No overlaps and no cracks (FR-3).** Every quad takes its corner positions from one function (`RoMapSpace.CornerUnity` with the height of the cube that owns the corner), so the same corner always yields the same bit pattern: two quads that share a corner height share the exact vertex position. Walls take their top edge from the owning cube and their bottom edge from the neighbour, so they meet both tops exactly. Only top and wall quads exist; no other face is created, so no face is duplicated. The expected triangle count is therefore `2 · (cubes with TileUp ≥ 0) + 2 · (walls that pass the height test)` minus the degenerate triangles, and the tests check it against an oracle written separately from the builder.

### 4.4 Meshes: `RoGndChunker`, `RoGndMeshBuilder`

Satisfies FR-3, FR-6, FR-7.

```csharp
public sealed class RoGndMeshData
{
    public int ChunkColumn, ChunkRow;
    public Vector3[] Positions; public Vector3[] Normals; public Vector2[] Uvs;   // Unity space, chunk-local origin (no second UV set, D6)
    public IReadOnlyList<RoGndSubmesh> Submeshes;                       // one per texture used in the chunk, ascending texture index
    public Vector3 Origin;                                              // chunk object position, Unity space
}
public sealed class RoGndSubmesh { public int Texture; public int[] Triangles; }
public static class RoGndMeshBuilder
{
    public const int ChunkSize = 16;                                    // cubes per chunk edge
    public static IReadOnlyList<RoGndMeshData> Build(RoGnd gnd, RoGndRules rules, RoReport report);
}
```

- **Chunks (FR-6).** The map is cut into blocks of `ChunkSize` × `ChunkSize` cubes; a quad belongs to the chunk of its cube (a wall belongs to the chunk of its owning cube). A 200 × 200 map has at most 169 chunks. Each chunk is one mesh, one renderer and one object, so Unity's frustum culling skips chunks out of view (FR-6), and the vertex count per chunk (at most 256 tops and 512 walls, four vertices each) stays below 65,536, so indices are 16-bit. The size is a constant; a map of any size just has more chunks (no map-size limit).
- **One material per texture (FR-6).** A chunk's submeshes are one per distinct texture in ascending order; the materials are the shared ones of PLAN-0002 (`RoMaterialKey(texture, surface, false)`), so every chunk of every map that uses a texture uses one material.
- **Vertex de-duplication (same rules as PLAN-0003 §4.6, owner decision D12).**
  1. Positions are welded by value: the converted `(x, y, z)` as exact float bit patterns, with `-0` normalized to `0`. Because every corner comes from `RoMapSpace.CornerUnity` (§4.3), this is the same as welding by `(grid corner, height bits)`.
  2. A **vertex key** is `(welded position, u, v bit patterns, normal group key)`. Each distinct key in a chunk becomes one vertex. The vertex count of a chunk therefore equals the number of distinct (position, texture coordinate) pairs, plus extra vertices only where the shading rule below needs a different normal at the same position and coordinate.
  3. Degenerate (zero-area) triangles are not emitted (§4.3) and contribute nothing to normals.
- **Normals (FR-7), computed on the whole map before cutting.** The normal group key of a corner depends on the face kind, as the shade types of PLAN-0003 §4.6 do:

  | Face | Normal group key | Result |
  |---|---|---|
  | Top | `(welded position, 0)`: one smoothing group for the whole ground | the tops that meet at a position share one normal, **across texture seams too**, because the position is welded by value and not by (position, uv); flat ground gives exactly `up` everywhere (no grid pattern) and a slope a continuous gradient; at a cliff the corner heights differ, so the corners are different positions and stay separate |
  | Front or side wall | the face's normal, rounded to 1e-4 (PLAN-0003 D11) | each wall is flat shaded; coplanar walls (a straight cliff over several cubes) share their corners, and the edge between a wall and a top, or between walls of different normals, stays hard |

  - A shared normal is the sum of the face normals at that position and group, **weighted by the corner angle** of each triangle at that position, then normalized; angle weighting makes the result independent of the diagonal.
  - Face normals come from the converted triangle after the winding correction, so they point outward by construction.
  - Chunk borders use the same normals, because they are computed before the cut: a vertex on a chunk border has the same normal in both chunks.
  - `Mesh.RecalculateNormals` is never used (it splits at every UV seam, which is the lighting bug of the old importers).
- **Winding.** The quads are built in Ragnarok space and converted with `RoSpace.Point`; winding is reversed once with `RoSpace.ReverseWinding` (PLAN-0002 §4.3) and the top faces are checked by test to face up and each wall to face its lower side.
- **No second UV set (D6).** The ground has no UV set of our own for lightmaps: lighting is Unity's real time lighting (OQ-2), and if a person later chooses to bake the ground or static models, Unity's built-in baking system is used (owner decision, 2026-11-18). A second UV set made per quad would also break the vertex merging above, since a merged corner can only have one value per UV set.
- **Chunk origin.** Each chunk object is positioned at the Unity position of its first grid corner at height 0 (`CornerUnity(cx · ChunkSize, cz · ChunkSize, 0)`) and its vertices are relative to it, so meshes keep small coordinates and a chunk can be moved or batched.
- All of this is pure: arrays in, arrays out.

### 4.5 Materials and textures

Satisfies FR-6.

- Tile `Texture` indices refer to `RoGnd.Textures`; each is parsed as `RoPath.Parse("texture").Combine(name)` and requested with `ctx.GroundTexture` then `ctx.Material(new RoMaterialKey(texture, RoMaterialFactory.SurfaceFor(analysis), false, ground: true))` (D17) (PLAN-0002 §4.4, §4.6). Ground faces are one-sided; a missing texture gives the placeholder and one warning naming the texture and the map.
- Lit materials carry no baked ground shadow: lighting and shadows are Unity's real time ones (owner decision on OQ-2).
- **Ground textures and materials of their own, with Clamp wrap (D17).** About two thirds of a map's top tiles end exactly on a texture edge (izlude: 13,398 of 20,100 reach v = 0 or 1). With Repeat wrap, bilinear filtering there blends in the opposite edge of the texture, which draws a thin light line along tile rows; Clamp removes it. Wrap mode belongs to the texture asset, and a model can use a ground texture (`model/프론테라/시계탑벽.rsm` uses `프론테라/pron-ctwr4.bmp`), so the ground does not share the model's texture or material:
  - `ctx.GroundTexture(path, referrer)` converts the source like `ctx.Texture` (same decoding, keying and import settings, PLAN-0002 §4.4) but writes `RoOutputPaths.GroundTexture` = `Art/<texture path without extension>__ground.png` with wrap Clamp, recorded in its own ledger category `groundtexture`. The source stays the ground's dependency.
  - `RoMaterialKey` gains `Ground` (default false; suffix `<mode>_ground`, e.g. `Art/_materials/texture/a__opaque_ground.mat`). The factory builds a ground key's material on the ground texture, and refuses a ground key whose referrer is a model (`.rsm`, `.rsm2`): a model never gets a ground material.
  - The ground converter asks only for ground keys; its rules version becomes 2.

### 4.6 Converter and prefab: `RoGndConverter`, `RoGndPrefabBuilder`

Satisfies FR-1, FR-6 (uses SPEC-0002 FR-5, FR-6, FR-10, FR-16).

```csharp
public sealed class RoGndConverter : IRoConverter        // registers RoJobKind.Ground
{
    public const int RulesVersion = 1;
    public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);   // returns the ground prefab
    internal static RoResult<GameObject> Write(RoImportContext ctx, RoPath path, RoGnd gnd, string sourceHash);   // D14
}
public sealed class RoGndOutputPaths { public string Prefab, Folder; public string Chunk(int cx, int cz); public static RoGndOutputPaths For(RoOutputRoots roots, RoPath source); }
```

- `RoJobKind.Ground` is added to the enum and `RoConverterRegistry`, with the wrapper `RoImport.Ground(string mapName)` and `RoImportContext.Ground(RoPath)` so the map converter of PLAN-0006 requests the ground on demand (SPEC-0002 FR-6).
- The prefab has a root (named after its file, D14) and one `Chunk_<cx>_<cz>` child per chunk, each with `MeshFilter`, `MeshRenderer` (materials in submesh order, shadow receiving on, light probes off), and static editor flags for lightmap contribution and batching (SPEC-0006 FR-6); no occlusion flag (D15). The root has no transform of its own: chunks are positioned in map space, so the map scene (PLAN-0006) places the prefab at the origin.
- The ledger entry (category `ground`) lists as outputs the prefab and every chunk mesh; as dependencies the source hash of every texture the ground uses. All outputs are written only after the whole ground is built in memory (no partial output on failure); meshes are upserted in place, so references from a committed map scene survive a re-conversion.
- Expected conditions are warnings (PLAN-0002 §4.9), with new `RoIssueCode` values `GndBadIndex` and `UnsupportedGndVersion` (a version GRF reads but this plan has no rules row for is reported once and gives no output). No version is guessed; the three reference maps fix the first row of the table.
- The menu and window gain the kind `Ground` and `Tools/Ragnarok/Convert All Grounds` (`Enumerate("")`, the whole data root, extension `.gnd`; D11).

### 4.7 (removed)

The gizmos and picking of the first draft (FR-9) were removed by the owner on 2026-11-18: gizmos belong to the walkability data, SPEC-0005 FR-7 (D16).

## 5. Threading and lifecycle

Satisfies FR-6.

- Main thread, synchronous, inside the batch of PLAN-0002 §4.10. `Core/Gnd` is arrays and structs; `RoGnd` holds the cube table as arrays (a 200 × 200 map is 40,000 cubes, a few MB) and is released after each map.
- The ground mesh data for a whole map is built in memory first (so normals are map-wide), then written chunk by chunk and released.
- At runtime nothing of this plan runs: the player has the prefab, meshes and materials only.

## 6. Test plan

Satisfies every FR of the spec. EditMode NUnit tests in `Uninarok.RoImport.Tests` (command in `CLAUDE.md` §2). Tests needing the real data set are marked `[Category("NeedsData")]` and are ignored with a message when the source folder is absent; pure tests build `RoGnd` objects in code.

| Test class | FR | Checks |
|---|---|---|
| `RoMapSpaceTests` | 4, 5 | corner positions; `Zoom = 5` gives 0.5-unit cubes and `Zoom = 10` exactly 1 unit; the centre offset; cube lookup round trips; GAT cell to cube is `/ 2`; `RowDirection` pinned |
| `RoGndSurfacesTests` | 1, 2 | a flat map has tops only and no walls; a step has exactly one wall per differing edge, with the owning cube's tile; a wall with `-1` produces nothing; equal heights at one end produce a triangle, not a quad; a cube with `TileUp = -1` is a gap; tile id `0` is a valid tile; edge cubes read no neighbour outside the grid and give no warning; `H00..H11` land on the right grid corners |
| `RoGndNonSquareTests` | 4 | a 5 × 3 map and a 3 × 5 map: the cube at column 4 row 2 (and its neighbours) comes out at the expected position and uses the expected tile; the old `column + row * height` index would fail (regression guard) |
| `RoGndCrackTests` | 3 | for every pair of neighbouring cubes whose shared-corner heights are equal the converted positions are bit-identical; walls meet the top edges exactly; the triangle count equals the independent oracle `2 · tops + 2 · walls − degenerate`; no two triangles have identical vertex sets |
| `RoGndNormalsTests` | 3, 7 | flat ground gives `up` at every vertex, including across chunk borders; a ramp gives a continuous normal field with no jump at cube borders; two tops with different textures share the normal at their common corner; a cliff top corner is not smoothed into the lower ground; wall normals are flat; two coplanar walls side by side share their common corners; the vertex count equals the distinct (position, uv) pairs plus the hard-edge vertices (D12) |
| `RoGndWindingTests` | 1, 2, 3 | every top triangle faces up; every wall faces its lower side; mesh has no inside-out faces |
| `RoGndChunkTests` | 6 | a 40 × 33 map gives 3 × 3 chunks with the right cube ranges; a quad belongs to the chunk of its cube; a wall at a chunk border is in its owner's chunk and meets the neighbour's top exactly; indices are 16-bit; submeshes are one per texture in ascending order; the two chunks that use one texture reference the same material (through the context) |
| `RoGndWriterTests` | 6 | a synthetic map converts to the prefab and chunk meshes at the paths of §3; the prefab has one chunk per chunk with static flags; materials are the shared ones; a missing texture gives the placeholder and one warning |
| `RoGndIdempotencyTests` | (0002 FR-5, FR-16) | second conversion is `Skipped`; a forced second conversion leaves every mesh byte-identical and every GUID unchanged; the prefab and a scene that references a chunk mesh keep their references |
| `RoGndReaderTests` (`NeedsData`) | 4, 5 | the three reference maps read through GRF with widths and heights 156 × 196, 134 × 150, 200 × 200 and the expected zoom; the cube table length equals `Width · Height`; a truncated copy returns false with a reason and never throws |
| `RoGndReferenceTests` (`NeedsData`) | 1–6 | each reference map converts with no failure; the chunk and triangle counts equal the oracle computed from the raw table; the largest map converts within the batch; a second batch skips everything |

**Manual checks** (recorded in the PR with screenshots):

- FR-1, FR-2, FR-4: each reference map, top-down and from the side, against the reference viewer, including a cliff, a gap and sloped ground; the non-square maps (prontera, izlude) are not rotated or mirrored.
- FR-3: top-down and side views show no light leaks along cube and chunk borders.
- FR-6: with the scene camera moved away from a region, the stats window shows its chunks no longer drawn.
- FR-7: a flat area shows even lighting, a slope a smooth gradient.
- Lighting: the ground reacts to a real time light and casts and receives real time shadows (no baked data involved).

## 7. Migration and compatibility

- `GNDImporter.cs` (a `ScriptedImporter` on `.gnd`) is deleted by the last task of this plan. Once the source folder is hidden from Unity (PLAN-0002 §7) it no longer fires and is inert. Its logic is not carried over: the old index, tile-id, wall and shader bugs are replaced by §4.1 to §4.5.
- The shared helpers of `UtilExtensions.cs` that only the GND importer used are deleted with it; the file itself is deleted when no importer uses it (PLAN-0003 §7 lists the order). T8.4 found no user at all (the GND, GAT and RSW importers never called it; `RsmImpoter.cs` only in commented-out lines), so the whole file was deleted with `GNDImporter.cs`. The old importers left, all compiled out: `GatImporter.cs` (PLAN-0005), `RswImporter.cs` (PLAN-0006), `RsmImpoter.cs` and `RsmImpoter.bkp.cs` (BACKLOG).
- `Assets/Scripts/MapData.cs` is not used here; PLAN-0005 decides its replacement.
- PLAN-0006 requests the ground through `ctx.Ground(...)`, instantiates the prefab at the map origin and uses `RoMapSpace`. The contract between the plans: the prefab path of §3, a root with no transform of its own, and the `RoMapSpace` of §4.2.
- Chunk meshes are covered by the LFS rule `Art/Imported/**/*.asset` of PLAN-0002 §7.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | GRF reads the GND; our own `RoGnd` is the input of every rule; rules are tested on synthetic maps | Parsing GND ourselves (owner decision: read through GRF); building meshes directly from GRF types (untestable without data) |
| D2 | One `RoMapSpace` for cube, world and centre rules, shared by GND, GAT and RSW; row direction is one verified constant | Each importer carrying its own convention (the cause of mismatches between ground, walkability and models) |
| D3 | Corner positions from one function and one height per owning cube, so shared corners are bit-identical; walls take both edges from the owners | Computing wall corners from neighbours' averages or re-deriving positions per face (hairline cracks) |
| D4 | Normals welded by (grid corner, height) over the whole map, angle-weighted, walls flat (made precise by D12) | `Mesh.RecalculateNormals` per chunk (splits at every vertex that is not shared, which is the old grid pattern and seams at chunk borders) |
| D5 | 16 × 16 cube chunks, one mesh and renderer per chunk, one material per texture | One mesh for the whole map (no culling, 32-bit indices); one mesh per cube (draw-call count) |
| D6 | (owner, 2026-11-18) No second UV set in the ground: lighting is real time, and any baking a person chooses later uses Unity's built-in system. Vertices merge on (position, uv, normal group) as D12 says, which smooth lighting needs | A second UV set from a regular cell grid per quad, computed by us (§4.4 first draft): a merged corner cannot sit in two quads' cells, so it would have forced one vertex per quad corner, duplicated vertices and lit seams; `Unwrapping.GenerateSecondaryUVSet` at conversion (not stable across editor versions, breaks byte-identical output) |
| D7 | _Withdrawn with FR-9 (D16)._ | |
| D8 | _Withdrawn with FR-9 (D16)._ | |
| D9 | The rules table of §4.3 (diagonal, wall facing, UV corner order, row direction) is verified by T0.3 against the reference viewer before any builder code | Choosing from memory of other loaders without checking |
| D10 | FR-8 is not implemented: owner answered OQ-2 with real time lighting only | A baked-shadow shader and lightmap atlas |
| D11 | Map sources are `<name>.gnd` at the data root (T0.1 found no `map/` folder; the original client stores maps there too). Batch enumeration covers the whole root and filters by extension; output paths keep `Art/Imported/map/<name>/` (owner decision, 2026-11-18) | Moving the source files into a `map/` folder (differs from the client layout) |
| D12 | (owner, 2026-11-18) Vertex de-duplication and smoothing follow PLAN-0003 §4.6 (D3, D11): vertex key `(welded position, uv, normal group)`; tops are one smoothing group across texture seams; walls are flat with the face normal (rounded to 1e-4) as their group, so coplanar walls share corners; angle-weighted sums; never `RecalculateNormals` | A ground-only key `(position, uv, normal)` and a per-face normal for every wall (§4.4 first draft): coplanar walls along one cliff kept duplicate corners, and the two importers would weld and shade by different rules |
| D13 | (T1.2, 2026-11-18) `RoGndReader.TryRead` takes an optional `RoReport`, into which it reports `GndBadIndex` once per map with a count; the bad id is replaced by `-1` in `RoGnd`, so no later stage sees it | Keeping the §4.1 first-draft signature without a report (the rule "reported once" had nowhere to go); leaving bad ids in `RoGnd` for the surface builder to report (every stage after the reader would have to check them again) |
| D14 | (T5.1, 2026-11-18) The ground prefab's root is named after its file (`<name>_ground`), because Unity names a prefab asset's root after the file; the map scene (PLAN-0006) may name its instance `Ground`. `RoGndConverter.Write(ctx, path, gnd, sourceHash)` converts a ground already read, as `RoRsmConverter.Write` does, so writer tests use maps built in code | A root named `Ground` in a file named `<name>_ground.prefab` (Unity renames it on save); writer tests only on real data (they would need the data folder and a GND writer of our own) |
| D15 | (T5.1, 2026-11-18) Chunks get the static flags `ContributeGI` and `BatchingStatic` only. Unity 6.7 marks `StaticEditorFlags.OccludeeStatic` obsolete (CS0618, "migrate to GPU Occlusion Culling"), which needs no per-object flag | Setting the occludee flag as §4.6 first said: a new compiler warning in the converter, for a feature Unity is removing |
| D16 | (owner, 2026-11-18) No gizmos or picking for the ground: FR-9 is removed. The gizmos wanted are for walkable versus non-walkable cells, which SPEC-0005 FR-7 already covers on the map data | Ground gizmos reading the source (§4.7 first draft) or the chunk meshes (TASKS first draft): neither was wanted |
| D17 | (owner, T8.2, 2026-11-18) The ground uses its own Clamp copy of each texture (`__ground.png`) and its own materials (`Ground` in `RoMaterialKey`); the material factory never gives a ground material to a model. Tested on izlude: Repeat shows a light line at tile rows whatever the compression, mipmaps or light; Clamp has none | Clamping the shared texture (the clock tower model uses a ground texture and would lose its repeat); separate materials over one shared texture (wrap is a texture setting, so the model would still be clamped); insetting the UVs by half a texel (changes the mesh and still samples the neighbouring texel at lower mipmaps); keeping the lines (GRF Editor shows them too, but fainter) |

Items for the owner: none open.

## 9. Risks

| Risk | Mitigation |
|---|---|
| The row direction, diagonal, wall facing or UV order differ from the candidates | T0.3 compares every rule with the reference viewer on the three reference maps before implementation; each rule is one constant with a pinning test |
| GRF's `BottomLeft`/`TopLeft` naming is not the grid order assumed | T0.2 checks the mapping against raw values of real cubes |
| A map larger than 200 × 200 appears in the full data | Chunking has no size limit; the batch converts every map and reports each failure once |
| Some maps use non-default zoom | `Zoom` comes from the header; synthetic test with `Zoom = 5`; T0.2 lists the zoom of every reference map |
| A wall facing rule is wrong for walls viewed from the high side in the original client | T0.3 checks cliffs from both sides; fallback is two-sided wall submeshes, decided by the owner |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.1, 4.3, 6 |
| FR-2 | 4.3, 6 |
| FR-3 | 4.3, 4.4, 6 |
| FR-4 | 4.1, 4.2, 6 |
| FR-5 | 4.1, 4.2, 6 |
| FR-6 | 4.4, 4.5, 4.6, 6 |
| FR-7 | 4.4, 6 |
| FR-8 | Not implemented (owner decision, §1) |
| FR-9 | Removed (D16): gizmos are SPEC-0005 FR-7 |
