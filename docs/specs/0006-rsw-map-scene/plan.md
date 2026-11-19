# PLAN-0006: RSW Map Scene

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0006) |
| **Status** | Approved |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Shared rules (paths, units and axes, textures, materials, writer, ledger, results, batch, the additive layer) come from [PLAN-0002](../0002-ro-import-foundation/plan.md). The model prefabs come from [PLAN-0003](../0003-rsm-models/plan.md), the ground and `RoMapSpace` from [PLAN-0004](../0004-gnd-ground/plan.md), the map data asset, `MapRoot` and the runtime assembly `Uninarok.Map` from [PLAN-0005](../0005-gat-map-data/plan.md). Reference maps and models: [`../0003-rsm-models/reference-models.md`](../0003-rsm-models/reference-models.md).

## 1. Constraints and assumptions

- Code lives in the editor assemblies of PLAN-0002 under `Core/Rsw/`, `Core/Formats/` (adapter) and `Editor/Map/`; the small data components that the scene holds (markers for lights, sounds and effects) are **runtime code** in `Uninarok.Map` (PLAN-0005 §3), because a component of an editor-only assembly cannot be saved in a scene that ships (PLAN-0002 §4.14).
- **RSW files are read by the GRF library** (the `Rsw` class of `GRF.dll`) through one adapter, `RoRswReader` (PLAN-0002 §1). After the adapter everything works on our own type `RoRsw`.
- Format facts used here, from the community RSW document: the file starts with `GRSW` and a version (1.9 to 2.6, with a build number from 2.2); it holds a water plane (moved to the GND file from 2.6), global lighting (directional light by longitude and latitude in degrees, diffuse and ambient colours, shadow opacity), a ground bounding box that appears unused, a list of scene objects, and a quad tree (absent in 1.9) used by the client for culling. A scene object is one of four types: **model** (name, animation type and speed, collision flags, model file, node name, position, rotation, scale), **light** (name, position, colour, range), **sound** (name, file, position, volume, width, height, range, cycle interval) and **effect** (name, position, preset id, emission delay, four launch parameters). The document gives **no** unit, axis or rotation convention (no map-centre offset, no Y sign, no rotation order or units). Those are the rules table of §4.3, verified by T0.3. The quad tree is not read.
- The map-wide values of FR-7 (size, water level, lighting values) go into the `settings` field of the `MapData` asset of PLAN-0005, which this plan fills (§4.5); there is one converter owner for that asset.
- OQ-1 (per-model shadow flags) and OQ-2 (scene location) of the spec are **decided in this plan** (§8, D4 and D5) and listed for the owner at approval.
- Re-conversion never touches the additive scene (PLAN-0002 §4.13).

## 2. Architecture

Satisfies FR-1, FR-2, FR-12.

```
 map/x.rsw ──(IRoSource)──> bytes
                  │
        RoRswReader (GRF adapter)                                     Core/Formats
                  │
                  ▼
        RoRsw (settings, models, lights, sounds, effects)             Core/Rsw
                  │
     ┌────────────┼──────────────────────────────┐
     ▼            ▼                              ▼
 RoRswRules   RoRswPlacement                RoRswLighting
 (table)      (RSW object → Unity TRS)      (ambient, sun direction)           pure arrays and structs
     └────────────┴──────────────┬───────────────┘
                                 ▼
        RoMapConverter (Editor/Map: ctx, scene, groups, instances, markers, lights, static flags, ledger)
                 │            │              │
          ctx.Ground      ctx.MapData      ctx.Model (per distinct model file)
                 ▼            ▼              ▼
      <name>.unity: Map_<name> (MapRoot) ─ Ground ─ Models ─ Lights ─ Sounds ─ Effects
```

Data flow for `RoImport.Map("prontera")` (source `map/prontera.rsw`):

1. Ledger check (hash of the RSW, rules version, and the recorded dependencies: ground, map data, every model, the map-name table): current → `Skipped`.
2. Read the RSW through GRF into `RoRsw`; failure is one `CorruptFile` issue and no output.
3. Ask for the dependencies on demand (PLAN-0002 FR-6): `ctx.Ground`, `ctx.MapData` (which also fills the map settings, §4.5), and `ctx.Model` for every distinct model file; a missing or failed model gives one warning naming the model and map and that object becomes a placeholder (FR-10).
4. Compute every instance's transform (§4.3) and the lighting values (§4.4) in memory.
5. Build the scene in memory, write it to its path (§4.6), record outputs and dependencies.

## 3. Assemblies and file layout

Satisfies FR-1, FR-5, FR-11.

| Path | Contents | Assembly |
|---|---|---|
| `Assets/Scripts/Map/MapLightMarker.cs`, `MapSoundMarker.cs`, `MapEffectMarker.cs`, `MapMissingModelMarker.cs`, `MapModelAnimation.cs` (D17) | one marker per file (Unity resolves a component saved in a scene by its file name, D15); data only: parameters, no logic; ranges in Unity units | `Uninarok.Map` |
| `Assets/Scripts/Map/MapRoot.cs` | (PLAN-0005) gains `MapSettings` access through its `MapData` | `Uninarok.Map` |
| `Assets/Editor/RoImport/Core/Formats/RoRswReader.cs` | adapter GRF `Rsw` → `RoRsw` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Rsw/` | `RoRsw`, `RoRswObject` types, `RoRswRules`, `RoRswPlacement`, `RoRswLighting` | Core |
| `Assets/Editor/RoImport/Editor/Map/` | `RoMapConverter`, `RoMapSceneBuilder`, `RoMapOutputPaths`, `RoMapOpener`, `RoMapGizmos` | `Uninarok.RoImport.Editor` |
| `Assets/Tests/EditMode/RoImport/Rsw/` | tests; synthetic maps built in code | `Uninarok.RoImport.Tests` |

Outputs (through `RoOutputPaths.Scene`, PLAN-0002 §4.5), for a source `<name>.rsw` at the data root (T0.1, D16):

| Output | Path |
|---|---|
| Generated scene (ledger-owned, rewritten by conversion) | `Assets/Scenes/Maps/<name>.unity` (`RoOutputPaths.Scene`; **decision D5**, owner's answer to OQ-2) |
| Additive scene (hand-made, **never** created or written by the importer, same folder) | `Assets/Scenes/Maps/<name>.additive.unity` (`RoOutputPaths.AdditiveScene`) |
| Map data | `Assets/Data/map/<name>.asset` (PLAN-0005) |

## 4. Component contracts

### 4.1 Reading: `RoRswReader` and the data type `RoRsw`

Satisfies FR-1, FR-7, FR-9.

```csharp
public sealed class RoRsw
{
    public int VersionMajor, VersionMinor, Build;
    public string GndFile, GatFile;                          // as stored; checked against the map name (below)
    public RoRswWater Water;                                 // level, type, wave height/speed/pitch; from the RSW (<= 2.5) or the GND (2.6)
    public RoRswLight Light;                                 // longitude, latitude (degrees), diffuse RGB, ambient RGB, shadow opacity
    public IReadOnlyList<RoRswModel> Models;                 // file order
    public IReadOnlyList<RoRswPointLight> Lights;
    public IReadOnlyList<RoRswSound> Sounds;
    public IReadOnlyList<RoRswEffect> Effects;
}
public readonly struct RoRswModel { public string Name; public int AnimType; public float AnimSpeed; public int Collision; public string File, Node; public Vector3 Position, Rotation, Scale; }
public readonly struct RoRswPointLight { public string Name; public Vector3 Position; public Color Color; public float Range; }
public readonly struct RoRswSound { public string Name, File; public Vector3 Position; public float Volume, Range, Interval; public int Width, Height; }
public readonly struct RoRswEffect { public string Name; public Vector3 Position; public int Preset; public float Delay; public float A, B, C, D; }
public static class RoRswReader { public static bool TryRead(RoPath path, byte[] bytes, out RoRsw rsw, out string reason); }
```

- Values are **raw**, unconverted; no rule of §4.3 is applied here. Order is the file order of each list (it makes the scene deterministic).
- The adapter never throws; a rejected read returns `false` with GRF's reason.
- A version GRF reads but this plan has no rules row for gives one `UnsupportedRswVersion` issue and no output; the three reference maps fix the first row of the table.
- The ground and walkability files named in the RSW are checked against the map name: if they differ from `<name>.gnd` and `<name>.gat` at the data root the converter uses the names from the RSW if the files exist, otherwise reports `RswFileMismatch` once.
- **Mapping check (T0.2).** The task prints the header and the first and last objects of each type through GRF, then writes into this section one row per `RoRsw` field with the GRF member that supplies it, noting any member GRF derives and that must not be used. GRF's values are taken as they are; they are not checked against the file bytes (D11).

**Field map (T0.2, 2026-11-18).** GRF `GRF.FileFormats.RswFormat.Rsw`, built with `new Rsw((MultiType)bytes)` while `EncodingService.DisplayEncoding` is `Korean` (names are CP949, as for GND and RSM), then restored.

| `RoRsw` field | GRF member | Note |
|---|---|---|
| `VersionMajor`, `VersionMinor` | `Header.MajorVersion`, `Header.MinorVersion` | all three reference maps are **2.1** |
| `Build` | `Header.BuildNumber` | 0 below 2.2 (absent in the file) |
| `GndFile`, `GatFile` | `Header.GroundFile`, `Header.AltitudeFile` | `<name>.gnd`, `<name>.gat` on all three maps |
| `Water` (level, type, wave height, speed, pitch) | `Water.Level`, `.Type`, `.WaveHeight`, `.WaveSpeed`, `.WavePitch` | read from the RSW for 2.1; `.TextureCycling` is the animation period (kept for a later spec, not used) |
| `Light` longitude, latitude | `Light.Longitude`, `Light.Latitude` | int degrees (45/45, 45/45, 70/20) |
| `Light` diffuse, ambient | `Light.DiffuseRed/Green/Blue`, `Light.AmbientRed/Green/Blue` | floats 0 to 1 |
| `Light` shadow opacity | `Light.Intensity` | GRF names it `Intensity` (0.5, 0.5, 0.6) |
| object lists | `Objects` (`List<RswObject>`), split by `RswObject.Type` (`Model`=1, `Light`=2, `Sound`=3, `Effect`=4), file order kept | |
| `RoRswModel.Name`, `File`, `Node` | `Model.Name`, `Model.ModelName`, `Model.NodeName` | `ModelName` is the path under `model/` with `\` separators |
| `RoRswModel.AnimType`, `AnimSpeed` | `Model.AnimationType`, `Model.AnimationSpeed` | seen: type 0 (prt_fild08, all) and 2 (prontera, izlude, all); speed 1, and 0.01 on prontera's two clock towers |
| `RoRswModel.Collision` | `Model.BlockType` | read but **not used**: on 2.1 the values are uninitialized memory (hundreds of distinct large and negative numbers) |
| `Position`, `Rotation`, `Scale` (all types) | `RswObject.Position`, `Model.Rotation`, `Model.Scale` (`TkVector3` `.X .Y .Z`) | rotation in degrees (0 to 360) |
| `RoRswPointLight.Name`, `Color`, `Range` | `Light.Name`, `Light.ColorVector`, `Light.Range` | `ColorVector` holds the file's floats (0.2, 0.05, 0) |
| `RoRswSound.*` | `Sound.Name`, `.WaveName`, `.Volume`, `.Width`, `.Height`, `.Range`, `.Cycle` | |
| `RoRswEffect.*` | `Effect.Name`, `.EffectNumber` (preset), `.EmitSpeed` (delay), `.Param[0..3]` (A to D) | |

Members that must not be used: `Light.Color` (bytes derived from `ColorVector`), `Light.RealRange()`, `Light.Custom_Range`, `Custom_Intensity`, `Custom_CutOff`, `Gives_Shadow` (editor extensions, not file fields of 2.1), `Rsw.WaterLevel(byte[])`, `Rsw.Ground` (the unused bounding box, garbage on izlude), `QuadTree`, `ModelResources`, `LubEffects`, `WindEffects`, `StrEffects`, `EvilsPawEffects` (empty on all three), `Effect.LubEffectAttached`, `Header.IniFile`, `SourceFile`, `UnknownData`.

Observed counts (GRF):

| Map | Version | Models (distinct files) | Lights | Sounds | Effects | `NodeName` non-empty | Mirrored instances (axes) |
|---|---|---|---|---|---|---|---|
| prt_fild08 | 2.1 | 570 (23) | 0 | 51 | 0 | 570 | 9 (X 1, Z 2, XZ 6) |
| prontera | 2.1 | 1303 (62) | 0 | 25 | 152 | 1303 | 289 (X 120, Z 90, XZ 79) |
| izlude | 2.1 | 552 (90) | 5 | 110 | 12 | 552 | 97 (X 26, Z 45, XZ 21, Y 3, XY 2) |

The notable contents of `reference-models.md` §4 match exactly (prt_fild08: 덤불02 32, 버섯01 5, 버섯02 3, 넝쿨01 1; prontera: 시계탑 2, 교역소 1, 도구점 1, 분수대 1, 게시판 3; izlude: 덤불02 13, 넝쿨01 2, iz_bridge01 6, iz_smelter, iz_enchant, 비공정, 퀴즈경기장, 프론아레나 1 each). The totals of §4 were higher than GRF's and were corrected to GRF's counts by the owner's decision (2026-11-18). Every model file referenced exists under `model/`.

### 4.2 Dependencies: ground, map data and models on demand

Satisfies FR-1, FR-2, FR-10 (uses SPEC-0002 FR-6).

- `RoJobKind.Map` is added to the enum and registry, with `RoImport.Map(string mapName)`; `RoImportContext` gains `Model(RoPath)` (PLAN-0003 adds the converter, this plan the context method if not present yet), `Ground(RoPath)` (PLAN-0004) and `MapData(RoPath)` (PLAN-0005).
- Models are requested **once per distinct file** (a map has hundreds of instances of a few dozen models: prontera has 1303 references to 62 distinct models), in file order, so the work and the log are small and deterministic.
- A model that is missing or fails to convert gives one `ModelMissing` warning naming the model and the map; every object that uses it becomes an empty named object with a `MapMissingModelMarker` (data in §4.6), so counts and positions are preserved (FR-10).
- Textures and materials are the models' own business (PLAN-0003); this plan never asks for them.

### 4.3 Placement: `RoRswRules`, `RoRswPlacement`

Satisfies FR-2, FR-3, FR-4.

```csharp
public readonly struct RoRswRules                       // the choices the data must confirm (T0.3), each a named constant
{
    public float PositionScale;                         // 1 (T0.3; PLAN-0004 T0.3 verified the offset)
    public RoAxisOrder RotationOrder;                   // Z·X·Y as a matrix product (Y applied first), T0.3
    public float RotationSignX, RotationSignY, RotationSignZ;   // +1, +1, +1 (T0.3)
    public float ScaleYSign;                            // +1 (T0.3: -1 floats izlude's mirrored tools above their bench)
    public bool NodeNameSelectsRoot;                    // false (T0.3: the node is always the root, or a node that does not exist)
    public static readonly RoRswRules V1;
}
public static class RoRswPlacement
{
    public static Matrix4x4 ModelMatrixRo(in RoRswModel m, RoMapSpace space, in RoRswRules rules);   // Ragnarok space, column vectors
    public static void ToUnity(Matrix4x4 ro, out Vector3 position, out Quaternion rotation, out Vector3 scale);   // F·M·F through RoSpace
}
```

- **One matrix chain in Ragnarok space, like PLAN-0003.** The placement of a model is `M = T(centreOffset + position·PositionScale) · R(rotation) · S(scale)` with `centreOffset = RoMapSpace.CentreOffsetRo` (RSW positions are relative to the map centre; the offset is defined once, in PLAN-0004 §4.2, so ground, walkability and objects can never disagree), `R` built with the order and signs of the rules table and `S` with the Y sign. The matrix is converted to Unity with the one conjugation of PLAN-0002 §4.3 (`F·M·F`, scale 0.1) and decomposed once; nothing in this plan converts a component on its own.
- **Mirrored (negative) scale.** A negative scale on one or three axes flips winding. The decomposition keeps the negative sign on the scale component and the model's materials are one-sided, so a mirrored instance is inside-out unless the renderer flips winding: Unity does this itself for a negative `lossyScale` determinant (the renderer reverses the culling), so no extra work is needed; a test confirms it on a synthetic mirrored model. The data has 395 mirrored instances (T0.2), on X and Z and, in izlude, on Y; the owner compared them (T0.3).
- **The instance transform is the placement alone.** The model prefab's own transforms already contain the recentering of PLAN-0003 (the prefab root has none of its own and its origin is the bottom centre of the bounds), so the instance gets exactly `M` and nothing is added.
- **`NodeName`.** Ignored (the whole model is drawn), owner's decision (T0.3). It is non-empty on every instance: on 2,416 of 2,425 it names the model's root node, on the other 9 a node that does not exist, never a child node. `RswNodeNameIgnored` is reported once per map, with a count, only for instances whose `NodeName` names a **child** node (none in the reference data).
- **Animation.** `AnimType` 0 means a static prop: the instance's `Animator` (present only on animated model prefabs) is disabled; `AnimType` 2 keeps it enabled; any other value is reported once as `RswUnknownAnimType` and treated as 2. Seen (T0.3): type 0 on all of prt_fild08, type 2 on all of prontera and izlude. The model's own timing (1000 ticks = 1 s, PLAN-0003 D4) is the animation's speed. `AnimSpeed` is 1 everywhere except 0.01 on prontera's two clock towers; it multiplies the model's speed: `Animator.speed = AnimSpeed`, kept by a `MapModelAnimation` component (runtime, `Uninarok.Map`) that applies it when enabled, because Unity does not save `Animator.speed` in a scene (D17) (owner's decision, 2026-11-18; the clock towers' 768 s animation then runs 100 times slower). `Collision` is not used in this delivery (no colliders are generated) and is not kept: on 2.1 it holds uninitialized memory (T0.2).
- **Ground contact (FR-4).** `RoGroundSampler` (PLAN-0005 §4.4) gives the ground height under an instance's origin. The test compares, for at least 20 models of the reference maps, the vertical distance between the model's lowest point and the ground with the expected distance recorded in `Assets/Tests/EditMode/RoImport/Rsw/ground-contact.json`. That file is made by T0.4 from the reference viewer: for each sample model it records the distance the original shows (zero for a barrel on the street, the sunk depth of a fountain base, and so on), so models that float or sink in the original are checked against what the original shows, not against an assumption.
- All of this is pure: structs in, structs out; it takes `RoMapSpace` as a parameter.

### 4.4 Lighting: `RoRswLighting`

Satisfies FR-8.

```csharp
public static class RoRswLighting
{
    public static Color Ambient(in RoRswLight l);                       // colour for RenderSettings
    public static Color Sun(in RoRswLight l);                           // colour of the directional light
    public static Quaternion SunRotation(in RoRswLight l);              // from longitude and latitude
    public static float ShadowStrength(in RoRswLight l);                // from the shadow opacity
}
```

- The scene gets ambient lighting in flat colour mode (`RenderSettings.ambientMode = Flat`, `ambientLight`) and **one** real-time directional light named `Sun` with soft shadows, because lighting is real time (owner decision on SPEC-0004 OQ-2). The two colours are the RSW's, taken as stored (0 to 1 floats, no factor): `ambientLight = (AmbientR, G, B)`, `Sun.color = (DiffuseR, G, B)`, `Sun.intensity = 1`, `Sun.shadowStrength = 1 - opacity` (opacity is GRF's `Light.Intensity`). The owner compared prontera with this rule (T0.3).
- The direction of the sun is built from longitude and latitude (degrees) in Ragnarok space and converted with `RoSpace`; latitude is measured from the zenith. The vector toward the light in Ragnarok space is `(-cos lon · sin lat, -cos lat, sin lon · sin lat)`; `Sun.forward` is minus its `RoSpace.Direction`. On prontera (45, 45) the sun comes from the north-west, 45° up. Verified with the owner (T0.3).
- FR-8 is a Should; if T0.3 cannot establish the rule, the lighting step is left out and the scene gets Unity's default lighting with a recorded note, without blocking the other requirements.

### 4.5 Map-wide values: filling `MapData.settings`

Satisfies FR-7.

- PLAN-0005's `MapData` already has the field `settings` (`MapSettings`: width and height in cubes, zoom, water level in Unity units, water type and wave values, ambient colour, sun colour, longitude, latitude, shadow opacity, source versions of the GND, GAT and RSW). This plan extends `RoMapDataConverter` so that, when the RSW exists, it also reads it (through `RoRswReader`) and fills `settings`, sets `hasSettings`, and records the RSW hash among its dependencies. If the RSW is absent the asset is converted without settings (SPEC-0005 stands alone).
- **Music name.** Not read and not stored: background music is out of scope (spec §6, D12). The RSW does not hold it, and the client's music table is not used.
- Water: the plane level and the wave values are stored in Unity units, **no water is rendered** (non-goal); the water level is also stored so a later spec can draw it. The source (RSW up to 2.5, GND from 2.6) is chosen by the reader.

### 4.6 The scene: `RoMapConverter`, `RoMapSceneBuilder`

Satisfies FR-1, FR-2, FR-5, FR-6, FR-9, FR-10, FR-11, FR-12.

```csharp
public sealed class RoMapConverter : IRoConverter       // registers RoJobKind.Map; source path <name>.rsw at the data root (D16)
{
    public const int RulesVersion = 1;
    public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);   // returns the SceneAsset
}
```

- **Hierarchy (FR-1, FR-5).** The scene has one root object `Map_<name>` carrying `MapRoot` (map name and the `MapData` reference, FR-1), with these children in this order, whose names are a **stable contract** with the additive scene:
  - `Ground`: an instance of the ground prefab (PLAN-0004 §4.6) at the origin;
  - `Models`, `Lights`, `Sounds`, `Effects`: groups. The hierarchy must show the count of each group (FR-5), so the object name is `Models (1303)`; the base name is the stable part and the count follows after a space and an opening bracket, so a lookup by prefix (`Models`) always works. `RoMapNames.Group(kind)` and `RoMapNames.FindGroup(root, kind)` are the only code that builds or looks up these names, and the additive scene's lookups are documented to use them.
  - `Sun` (the directional light) directly under the map root.
- **Instances (FR-2).** Each model object is `PrefabUtility.InstantiatePrefab(modelPrefab)` (a prefab instance, not a copy, so editing the model or its material changes every instance); its name is the object's name in the RSW (names need not be unique; the order is the file order, which is the sibling order), its transform is the placement of §4.3, and it is parented under `Models`. Instances are created without per-instance overrides other than name, transform, static flags and, for animated models, the `Animator` speed or enabled state.
- **Placeholders (FR-9).** Lights, sounds and effects become empty objects with the object's name and position under their group, each with its data marker: `MapLightMarker` (colour, range), `MapSoundMarker` (file, volume, range, interval, width, height), `MapEffectMarker` (preset id, delay, four parameters). They hold parameters and do nothing at runtime (non-goal). A model that could not be converted becomes an empty object with the instance's name and transform and a `MapMissingModelMarker` (model file) so the map keeps its structure (FR-10).
- **Static setup (FR-6).** Ground chunks and all model instances get the static editor flags for lightmap contribution, batching and reflection probes, so baking and static batching work with no user work (no occlusion flags, D18). **Exceptions:** instances of animated models (animation type not 0 and the prefab has an `Animator`) get no batching flag, because a moving object cannot be statically batched. Markers and groups are not static. This is a flag on each scene object, not on the prefab.
- **Per-model shadow flags (OQ-1, D4).** No per-model overrides in the first delivery: every renderer casts and receives shadows as its prefab says (PLAN-0003: both on). A person who wants an override makes it in the additive scene (§4.7) or on the model prefab.
- **Saving (FR-11).** The converter creates a new scene in memory (`EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)`), builds the hierarchy, applies the lighting settings (§4.4), and saves it with `EditorSceneManager.SaveScene(scene, path)` to the **same path**, which replaces the file's content and keeps the `.meta` and the GUID, so the build settings entry and any link to the scene survive. The temporary scene is closed in a `finally`. The ledger entry (category `map`) lists as outputs the scene (and, through its own entries, the ground, map data and models) and as dependencies the hash of the RSW, the GND and GAT (through the ground and map data), and every model source. All outputs are written after the scene is built in memory (no partial output on failure).
- **Determinism (PLAN-0002 FR-16).** The scene's instance and group order is the file order and no timestamps are written; the scene file's internal object ids are Unity's and may differ between runs, so the idempotency test compares the scene **by structure** (hierarchy, names, transforms, references, components, flags) and not by bytes; an unchanged map is skipped by the ledger, which is what keeps version control quiet (risk R3).

### 4.7 The additive layer: `RoMapOpener`

Satisfies FR-11 (uses PLAN-0002 §4.13).

- The generated scene and the additive scene sit side by side in `Assets/Scenes/Maps/` (PLAN-0002 §4.13). The importer never creates, writes, renames or deletes `Assets/Scenes/Maps/<name>.additive.unity`, and writes no file in that folder other than the ledger-owned `<name>.unity`; a person makes the additive scene with **File > New Scene** and saves it at that path. The importer guarantees the names of §4.6 (`Map_<name>`, `Ground`, the group base names `Models`, `Lights`, `Sounds`, `Effects` (the count after them can change), `Sun`) and the instance order do not change between conversions, so a custom object added under, or referring to, those names keeps working.
- `Tools/Ragnarok/Open Map` opens a map in the editor: the generated scene as the single scene, then the additive scene on top if it exists. At runtime loading both is the job of the game's scene loading (out of scope here); the importer documents the two paths.
- **Survival check (FR-11).** The test creates a throwaway additive scene with a custom object, re-converts the map, and checks the additive scene is byte-identical and its custom object still resolves under the same names.

### 4.8 Batch and menu

Satisfies FR-12.

- The menu and window gain the kind `Map` and `Tools/Ragnarok/Convert All Maps` (every `.rsw` at the data root, as the map data menu of PLAN-0005; D16). One `RoBatch` run over every `.rsw` converts the maps in sorted order; each map is one job (PLAN-0002 §4.10), shared dependencies are converted once by the memo, and the batch ends with the list of results (FR-12). Because a map pulls in its models, the batch of maps is also a batch of models; its time is dominated by the first maps.
- Chunks of 200 jobs are measured in maps and in nested dependencies; the batch saves ledgers every 200 jobs of any kind (PLAN-0002 §4.10).

### 4.9 Gizmos: `RoMapGizmos`

Satisfies FR-13.

- `[DrawGizmo(GizmoType.Selected | GizmoType.InSelectionHierarchy, typeof(MapRoot))]` draws, for a selected map object, the map rectangle (from `MapData.settings`), the origin and the axes (X, up and the row direction, labelled), and the centre. It returns immediately when nothing from the map is selected; it reads only the `MapData` already in the project. Editor-only (FR-14 of SPEC-0002).

## 5. Threading and lifecycle

Satisfies FR-12.

- Main thread, synchronous, inside the batch of PLAN-0002 §4.10. The scene is built in memory per job and closed at the end of the job (`finally`); `EditorUtility.UnloadUnusedAssetsImmediate` runs every 200 jobs. A 1400-instance map is a few MB of scene objects; no state survives a job.
- At runtime nothing of this plan runs: the player has the scene, the prefabs, the ground, the map data and the marker components only.

## 6. Test plan

Satisfies every FR of the spec. EditMode NUnit tests in `Uninarok.RoImport.Tests` (command in `CLAUDE.md` §2). Tests needing real data are marked `[Category("NeedsData")]`; pure tests build `RoRsw` objects in code and use a throwaway output root.

| Test class | FR | Checks |
|---|---|---|
| `RoRswPlacementTests` | 3 | a model at the map centre, at a corner and at a non-square map's corner; a rotation about each axis (order and signs of the rules table pinned); mirrored scale on one and three axes (determinant sign, instance is not inside-out); positions relative to the centre equal `RoMapSpace.FromRswRo`; non-square maps are not swapped |
| `RoRswLightingTests` | 8 | ambient and sun colours, shadow strength, sun rotation for a table of longitude and latitude values |
| `RoMapSceneTests` | 1, 2, 5, 6, 9 | a synthetic map converts to a scene with `Map_<name>`, `Ground`, the four groups with counts and `Sun`; each model object is a prefab instance of the model prefab (changing the prefab's material changes it for every instance); instance names and order are the file order; lights, sounds and effects are markers with their parameters; static flags are set on ground and instances and not on animated instances, markers and groups |
| `RoMapMissingModelTests` | 10 | a missing model gives one warning naming model and map, the object is a placeholder with the instance's name and transform, the map converts and its other instances are unaffected |
| `RoMapSettingsTests` | 7 | settings equal the RSW (water level, lighting values, size); absent RSW converts without settings |
| `RoMapIdempotencyTests` | 11, (0002 FR-5) | second conversion is `Skipped`; a forced second conversion yields an identical scene by structure and unchanged GUIDs for scene, map data, ground and models; the build settings entry survives |
| `RoMapAdditiveTests` | 11 | an additive scene with a custom object is byte-identical after re-conversion and its object still resolves under the stable names |
| `RoMapBatchTests` | 12 | a batch of three synthetic maps with a shared model converts the model once, ends with a complete result list, one failing map does not stop the others, a second batch changes nothing |
| `RoRswReaderTests` (`NeedsData`) | 1, 3 | the three reference maps read through GRF with the counts of `reference-models.md` §4 (1303, 552 and 570 model references, 62, 90 and 23 distinct models); a truncated copy returns false with a reason and never throws |
| `RoMapGroundContactTests` (`NeedsData`) | 4 | the 20 or more sample models of `ground-contact.json` sit at the recorded vertical distance from the ground within the recorded tolerance |
| `RoMapReferenceTests` (`NeedsData`) | 1 to 12 | each reference map converts with no failure; the number of instances per group equals the file's counts; every distinct model converted; a second batch skips everything |

**Manual checks** (recorded in the PR with screenshots):

- FR-3: fixed-camera screenshots (top, front and one oblique) of prt_fild08, prontera and izlude against the reference viewer, including the four models earlier reported as wrongly placed, the two animated clock towers and the control group of `reference-models.md`.
- FR-4: the 20 or more ground-contact samples, with the numbers.
- FR-6: bake lightmaps and static batching on prt_fild08 without changing anything by hand.
- FR-8: lighting of each reference map beside the reference client's.
- FR-11: add a custom object in the additive scene, re-convert, open with `Tools/Ragnarok/Open Map`.
- FR-13: select the map object and see the rectangle, origin and axes.

## 7. Migration and compatibility

- `RswImporter.cs` (a `ScriptedImporter` on `.rsw` that looks up model prefabs) is deleted by the last task of this plan; once the source folder is hidden from Unity (PLAN-0002 §7) it is inert. Its logic is not carried over: the root-only global transform, the use of GRF's derived positions and the fixed rotation order are replaced by §4.3.
- Scenes made by the old importer (if committed) are replaced by the regenerated scenes; the list of such scenes and their users is recorded by T0.1.
- T8.5 (2026-11-18): `RswImporter.cs` is deleted; it had no users and made no committed scene (T0.1), and `UtilExtensions.cs` was already gone (TASKS-0004 T8.4). The only old importers left, compiled out, are `RsmImpoter.cs` and `RsmImpoter.bkp.cs` (listed in BACKLOG).
- PLAN-0003 FR-3 (model recentering / ground contact) is closed by the ground-contact task here.
- The `.gitattributes` rules of PLAN-0002 §7 do not cover `*.unity` generated scenes under `Assets/Scenes/Maps/`; scenes stay text and are not put under LFS unless T7.2 shows a size problem (a scene with 1400 prefab instances is a few hundred KB).
- Contract with PLAN-0007: none (sprites are not map objects here). A later spec places NPCs and monsters at map cells using `MapData`.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | One matrix chain in Ragnarok space, offset from `RoMapSpace`, converted once; the rules table (position scale, rotation order and signs, scale Y sign) is verified by T0.3 against the reference viewer before any builder code | Per-component conversion (the old importer's root-only transform and derived positions, the cause of misplaced models); choosing rotation conventions from memory |
| D2 | Placeholders are data-only runtime markers in `Uninarok.Map` | Editor-only components (cannot ship in a scene); real Unity `Light` and `AudioSource` objects for every torch and sound (hundreds of lights, behaviour is out of scope) |
| D3 | Instances are prefab instances of the model prefab, with no per-instance overrides except name, transform, flags and animator speed | Copies of the model (FR-2 forbids) |
| D4 (OQ-1) | No per-model shadow overrides in this delivery; defaults come from the model prefab; custom overrides belong in the additive scene | Reading and applying per-model flags (the RSW has none in the versions seen; a future spec can add them) |
| D5 (OQ-2) | Owner's answer: the generated scene is stored in `Assets/Scenes/Maps/<name>.unity`, next to the hand-made `<name>.additive.unity`; the importer only ever writes the ledger-owned `<name>.unity` (PLAN-0002 §4.13 is updated to say so, and gets a `Scenes` output root) | Putting the generated scene under `Art/Imported` (the earlier proposal, rejected by the owner) |
| D6 | One converter owns `MapData`, extended here to fill `settings` | A second asset or a second owner (two writers on one asset) |
| D7 | The scene is saved in place with `SaveScene` to the same path; the scene is compared by structure, not bytes | Deleting and recreating the scene (new GUID, broken links) |
| D8 | Lighting is real time: ambient colour plus one real-time directional light | Baked lightmaps from the file (owner decision on SPEC-0004 OQ-2) |
| D9 | Ground-contact check compares with distances recorded from the reference viewer, not with an assumption of "touching" | Asserting every model touches the ground (many props float or sink in the original) |
| D10 | Static flags on scene instances, not on prefabs; animated instances are not batched | Making prefabs static (affects every use of the model, including the animated ones) |
| D11 | Owner's decision (2026-11-18): the RSW values are taken from the GRF library as it supplies them; T0.2 only maps fields to GRF members | Comparing GRF's values with a hand reading of the file bytes |
| D12 | Owner's decision (2026-11-18): no music name; the music table is not read and `MusicNameMissing` does not exist | Reading `data/mp3nametable.txt` (no music is available yet and music is not part of the spec) |
| D13 | T0.3 (2026-11-18): rotation `Rz·Rx·Ry`, all signs +1, scale used as stored (Y sign +1) in Ragnarok space. Checked by the tilt of the 36 tilted bushes of prt_fild08 against the ground normal (mean 20.9°, against 35.6° unrotated and ≥ 21.1° for all 47 other order and sign choices), the diagonal clock-tower walls of prontera (Y sign), izlude's mirrored axe and hammer on their bench (Y scale sign; diagonal on the bench, as in the reference viewer) and the owner's comparison of prt_fild08, izlude and prontera with the reference viewer | Scale Y sign -1 (BrowEdit3's, which belongs to its own Y flip; it floats the tools above the bench); the other orders (they stand the tools up) |
| D14 | Owner's decision (2026-11-18): the RSW `AnimSpeed` is applied as `Animator.speed` | Ignoring it (every instance at the model's own speed) |
| D15 | T4.1 (2026-11-18): one file per marker component instead of one `MapMarkers.cs` | One file for the four markers (Unity cannot resolve the script of a component saved in a scene unless the file is named after the class) |
| D16 | T0.1 (2026-11-18): the map source is `<name>.rsw` at the data root, like the GND and GAT (PLAN-0004 D11, PLAN-0005 D9); the menu takes every `.rsw` there | `map/<name>.rsw` (the data set has no `map/` folder) |
| D17 | T5.3 (2026-11-18): the animation speed of an instance is a field of a small runtime component, `MapModelAnimation`, applied in `OnEnable` | Setting `Animator.speed` on the instance (not serialized: lost when the scene is saved); a per-speed animator controller (one per speed value, more assets) |
| D18 | T5.3 (2026-11-18): no occluder or occludee static flags | Setting them (deprecated in Unity 6.7, which warns at compile time and points to GPU occlusion culling, which needs no per-object flag) |

Items for the owner: approve D4 (no per-model shadow flags) and D8, and the new runtime marker components.

## 9. Risks

| Risk | Mitigation |
|---|---|
| The rules table (position scale, rotation order and signs, scale sign) differs from the candidates (resolved by T0.3: the scale Y sign differed) | T0.3 compares every rule with the reference viewer on the three maps and the four reported models; each rule is one constant with a pinning test |
| The ground-contact tolerance is wrong or the recorded distances are wrong | T0.4 records them from the reference viewer with the owner; the file is committed test data |
| Scene files are not byte-identical on a forced re-save | The ledger skips unchanged maps; the determinism test compares by structure; recorded as a known limit of FR-16 for scenes |
| A map with 1400 instances makes `InstantiatePrefab` and save slow | Measured in T7.2; instances are created in file order in one pass; chunking by 200 jobs bounds memory; if too slow the fallback is creating the scene through `PrefabUtility.LoadPrefabContents` batching (decision recorded then) |
| `SaveScene` to an existing path while that scene is open in the editor | The converter closes or reloads an open copy first (and says so once); batch runs lock the editor (PLAN-0002 §4.10) |
| The RSW version of a map has a layout GRF reads wrongly | Wrong values show up as misplaced models in T0.3 and the reference tests; the version is checked in the rules table and an unknown one is reported once |
| Many models are missing or fail | One warning per model, placeholders keep the structure, the batch lists them once |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 2, 3, 4.1, 4.2, 4.6, 6 |
| FR-2 | 4.2, 4.6, 6 |
| FR-3 | 4.3, 6 |
| FR-4 | 4.3, 6 |
| FR-5 | 4.6, 6 |
| FR-6 | 4.6, 6 |
| FR-7 | 4.5, 6 |
| FR-8 | 4.4, 6 |
| FR-9 | 4.6, 6 |
| FR-10 | 4.2, 4.6, 6 |
| FR-11 | 4.6, 4.7, 6 |
| FR-12 | 4.8, 6 |
| FR-13 | 4.9, 6 |
