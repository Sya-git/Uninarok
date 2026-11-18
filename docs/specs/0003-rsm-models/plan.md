# PLAN-0003: RSM Models

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0003) |
| **Status** | Approved |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Shared rules (paths, units and axes, textures, materials, ledger, results, batch) come from [PLAN-0002](../0002-ro-import-foundation/plan.md) and are used here, not restated. The reference models are in [`reference-models.md`](reference-models.md), which is normative for the checks in §6.

## 1. Constraints and assumptions

- Everything runs in the editor assemblies of PLAN-0002 (`Uninarok.RoImport.Core`, `Uninarok.RoImport.Editor`), main thread only. New code lives under `Core/Rsm/`, `Core/Formats/` (adapter) and `Editor/Rsm/`.
- **RSM files are read by the GRF library** (the `Rsm` class of `GRF.dll`, including its handling of the format versions and of `.rsm2`), through one adapter, `RoRsmReader`, in `Core/Formats/`. No code of ours parses RSM bytes (PLAN-0002 §1). Everything after the adapter works on our own data type, `RoRsm` (§4.1), which makes the rules below testable without the library and without data files.
- GRF's `Rsm` and `Mesh` types expose both members that map one-to-one to fields of the file and members GRF derives for its own viewer (the names `LocalPosition`, `GlobalPosition`, `GlobalRotationAxis`, `GlobalRotationAngle`, `GlobalScale`, `TransformationMatrix` appear in the old importer). **The adapter uses only members that correspond to file fields, and the mapping is verified by task T0.2 before any rule depends on it** (§4.1). GRF's derived values are used only to cross-check our own results.
- The format's version differences are not fully documented (the format reference says the 1.x details conflict and are unverified). Rules that differ by version are isolated in one table (§4.4) that is filled and verified from the reference viewer and the data; the reference data has only version 1.4 (`reference-models.md` §5).
- Meshes, prefabs, animation clips and animator controllers are written through `RoAssetWriter` (PLAN-0002 §4.7), in place, owned by ledger category `model`. Materials come from `RoMaterialFactory` (PLAN-0002 §4.6) and belong to the `material` category.
- The reference viewer for visual comparison is the original client or a trusted viewer (spec §6); GRFEditor's own model viewer is acceptable.
- Animation is delivered as animation clips played by an `Animator` (spec §6, decided; D5).

## 2. Architecture

Satisfies FR-1, FR-2, FR-9.

```
 model/…/x.rsm ──(IRoSource, PLAN-0002)──> bytes
                                             │
                                  RoRsmReader (GRF adapter)           Core/Formats
                                             │
                                             ▼
                                    RoRsm  (our data)                 Core/Rsm
                                             │
        ┌────────────────┬───────────────────┼───────────────────┬──────────────────┐
        ▼                ▼                   ▼                   ▼                  ▼
  RsmHierarchy     RsmPose / RsmRules   RsmMeshBuilder     RsmShading        RsmAnimationData
  (parents first)  (rest pose, recenter) (dedup, submeshes)  (normals)        (keys → seconds)
        └────────────────┴───────────────────┴───────────────────┴──────────────────┘
                                             │  plain arrays, matrices, no UnityEngine objects
                                             ▼
                        RoRsmConverter  (Editor/Rsm: orchestrates, uses ctx)
             meshes ─ materials(ctx) ─ prefab ─ animation clip ─ controller ─ ledger
```

Data flow for one request `RoImport.Model("model/나무잡초꽃/버섯02.rsm")`:

1. Ledger check (source hash, rules version, dependency textures): current → `Skipped`.
2. Read through GRF into `RoRsm`; a read failure is one `CorruptFile` (or `UnsupportedRsmVersion`) issue and no output (FR-8).
3. Build the node tree, compute rest-pose transforms and the model recentering in Ragnarok space, build each node's mesh data, convert to Unity space with `RoSpace` (PLAN-0002 §4.3).
4. Resolve each texture through `ctx.Texture`, then the material through `ctx.Material` (the surface mode comes from the texture analysis, the two-sided flag from the faces).
5. Write meshes, build and save the prefab, then the clip and controller if the model is animated; record outputs and dependencies in the ledger.

The core of the design is that **every placement rule is a pure function in Ragnarok space** (§4.4), converted to Unity space in one last step by `RoSpace`. That is what the old importer did not do: it applied a global transform only to the root node and corrected axes per case in Unity space, which is where the wrong rotations and offsets of the models in `reference-models.md` §1 come from.

## 3. Assemblies and file layout

Satisfies FR-1, FR-10.

| Path (under `Assets/Editor/RoImport/`) | Contents | Assembly |
|---|---|---|
| `Core/Formats/RoRsmReader.cs` | adapter: GRF `Rsm` → `RoRsm` (the only RSM code that touches GRF types) | `Uninarok.RoImport.Core` |
| `Core/Rsm/Data/` | `RoRsm`, `RoRsmNode`, `RoRsmFace`, `RoRsmTexCoord`, `RoRsmPosKey`, `RoRsmRotKey`, `RoRsmScaleKey`, `RsmShadeType` | Core |
| `Core/Rsm/Tree/` | `RsmHierarchy`, `RsmTree` | Core |
| `Core/Rsm/Pose/` | `RsmRules`, `RsmPose`, `RsmBounds` | Core |
| `Core/Rsm/Mesh/` | `RsmMeshBuilder`, `RsmMeshData`, `RsmSubmesh`, `RsmShading` | Core |
| `Core/Rsm/Anim/` | `RsmAnimationData`, `RsmCurve` types, `RsmTiming` | Core |
| `Editor/Rsm/` | `RoRsmConverter` (`IRoConverter`), `RsmPrefabBuilder`, `RsmClipBuilder`, `RsmControllerBuilder`, `RsmOutputPaths` | `Uninarok.RoImport.Editor` |
| `Assets/Tests/EditMode/RoImport/Rsm/` | tests, `Fixtures~/` (hand-made `RoRsm` data builders; no binary fixtures) | `Uninarok.RoImport.Tests` |

Output paths (through `RoOutputPaths.Mirror`, PLAN-0002 §4.5). For a source `model/a/b.rsm`:

| Output | Path | Notes |
|---|---|---|
| Prefab | `Art/Imported/model/a/b.prefab` | root object named `b`, one child object per node |
| Mesh per node | `Art/Imported/model/a/b/<node>.asset` | `<node>` = node name made file-safe; duplicate names get `_2`, `_3` in file order |
| Animation clip | `Art/Imported/model/a/b/b.anim` | only for animated models |
| Animator controller | `Art/Imported/model/a/b/b.controller` | only for animated models |

Node object names inside the prefab keep the original node names; only the mesh file names are made file-safe. Two nodes with the same name keep distinct object names (`name`, `name_2`) so animation paths stay unique.

## 4. Component contracts

### 4.1 Reading: `RoRsmReader` and the data type `RoRsm`

Satisfies FR-8, FR-2.

```csharp
public sealed class RoRsm
{
    public int VersionMajor, VersionMinor;
    public RsmShadeType ShadeType;                 // None, Flat, Smooth; Smooth when the file has none
    public bool HasShadeType;                      // false for versions without it
    public int AnimationLength;                    // in the file's keyframe units
    public IReadOnlyList<string> Textures;         // as stored, relative to texture/
    public IReadOnlyList<RoRsmNode> Nodes;         // file order
}
public sealed class RoRsmNode
{
    public string Name, ParentName;                // ParentName null or "" for a root
    public int[] TextureSlots;                     // node-local slot -> index in RoRsm.Textures
    public float[] OffsetMatrix;                   // 3x3, row-major, as in the file
    public Vector3 Offset;                         // the file's per-node offset vector
    public Vector3 Position;
    public float RotationAngle; public Vector3 RotationAxis;
    public Vector3 Scale;
    public Vector3[] Vertices;
    public RoRsmTexCoord[] TexCoords;              // u, v (and color where the version has it)
    public RoRsmFace[] Faces;
    public RoRsmPosKey[] PosKeys; public RoRsmRotKey[] RotKeys; public RoRsmScaleKey[] ScaleKeys;
}
public struct RoRsmFace { public int V0, V1, V2, T0, T1, T2, TextureSlot; public bool TwoSided; public int SmoothGroup; }
public static class RoRsmReader { public static bool TryRead(RoPath path, byte[] bytes, out RoRsm rsm, out string reason); }
```

- `RoRsm` holds the file's values as GRF reads them, in Ragnarok space, unconverted: matrices and vectors as in the file, angles in radians, keyframe times in the file's units. No derived or recentered values.
- **GRF member mapping (T0.2).** The task prints, for each reference model of `reference-models.md`, every `Rsm` and `Mesh` member that could correspond to a file field (the old importer used `Name`, `ParentName`, `LocalPosition`, `GlobalPosition`, `GlobalRotationAxis`, `GlobalRotationAngle`, `GlobalScale`, `TransformationMatrix`, `Vertices`, `TextureVertices`, `Faces`, `TextureIndexes`, `Textures`, and GRF also has `ShadeType`, `AnimationLength`, `PosKeyFrames`, `RotationKeyFrames`, `ScaleKeyFrames`, `SmoothGroup`, `Rsm2` and `Rsm2Converter`), and compares them with the raw values already recorded for the four reported models (single node, rotation and position non-zero). The result, one row per `RoRsm` field with the GRF member that supplies it, is written into this section. Fields GRF does not supply (for example a version without shade type) are left at their defaults with `HasShadeType = false`.
- **T0.2 result (2026-11-17).** Checked against a raw read of the RSM 1.4 layout for `나무잡초꽃/버섯02`, `나무잡초꽃/넝쿨01` (single node, rotation and position non-zero), `dewata/다약화로01` (parent and child) and `프론테라/시계탑` (5 nodes, animated): every value below equals the raw value bit for bit. Despite their names, GRF's `Local*`/`Global*` node members are the plain file fields, not derived values.

  | `RoRsm` field | GRF member | Notes |
  |---|---|---|
  | `VersionMajor`, `VersionMinor` | `Rsm.Header.MajorVersion`, `.MinorVersion` | `Rsm.Version` (double) is the same, not used |
  | `ShadeType`, `HasShadeType` | `Rsm.ShadeType` (int: 0 none, 1 flat, 2 smooth) | every 1.x file in the data has it; `HasShadeType` comes from the `RsmRules` row |
  | `AnimationLength` | `Rsm.AnimationLength` | file units (`시계탑`: 768000) |
  | `Textures` | `Rsm.Textures` | relative to `texture/`, `\` separators; **needs the Korean encoding** (below) |
  | `Nodes` | `Rsm.Meshes` | file order |
  | `Name`, `ParentName` | `Mesh.Name`, `Mesh.ParentName` | `""` for a root |
  | `TextureSlots` | `Mesh.TextureIndexes` | node slot → index into `Rsm.Textures` |
  | `OffsetMatrix` | `Mesh.TransformationMatrix` (`TkMatrix3`, `[row, col]`) | row-major as in the file (raw `1,0,0, 0,0,1, 0,-1,0` = rows printed by GRF) |
  | `Offset` | `Mesh.LocalPosition` | the file's offset vector (first 3 floats after the matrix); non-zero on 17 nodes of the data |
  | `Position` | `Mesh.GlobalPosition` | the file's position (next 3 floats) |
  | `RotationAngle`, `RotationAxis` | `Mesh.GlobalRotationAngle`, `Mesh.GlobalRotationAxis` | radians; axis `(0,0,0)` with angle 0 means no rotation |
  | `Scale` | `Mesh.GlobalScale` | |
  | `Vertices` | `Mesh.Vertices` (`TkVector3`) | |
  | `TexCoords` | `Mesh.TextureVertices` (`U`, `V`, `C` color) | |
  | `Faces[].V0..V2`, `T0..T2` | `Face.VertexIds`, `Face.TextureVertexIds` | `ushort[3]` |
  | `Faces[].TextureSlot` | `Face.TextureId` | node-local slot; no out-of-range slot in the data |
  | `Faces[].TwoSided` | `Face.TwoSide != 0` | |
  | `Faces[].SmoothGroup` | `Face.SmoothGroup[0]` | GRF stores `int[3]`; in all 21,184 faces of the data the three are equal, values 0 to 7 (an integer group id, not a mask) |
  | `PosKeys` | `Mesh.PosKeyFrames` (`Frame`, `Position`) | per node from 1.5; none in the 1.4 data (1.4's global position keys are empty in every file) |
  | `RotKeys` | `Mesh.RotationKeyFrames` (`Frame`, `Quaternion` X, Y, Z, W) | 44 nodes in the data have them |
  | `ScaleKeys` | `Mesh.ScaleKeyFrames` (`Frame`, `Scale`) | 2.x only; empty for 1.x |

  **Not used** (derived by GRF or for features out of scope): `Mesh.Parent`, `Mesh.Children` (we resolve parents by name, §4.2), `Mesh.Matrix1`, `Mesh.Matrix2`, `Mesh.InvertTransformationMatrix`, `Mesh.BoundingBox`, `Mesh.CalculateBoundingBox()`, `Rsm.Box`, `Rsm.MainMesh`, `Rsm.MainMeshNames`, `Mesh.Textures` (empty for 1.x), `Mesh.TextureKeyFrameGroup` (texture animation, not in this spec), `Rsm.VolumeBoxes`, `Rsm.Alpha`, `Rsm.FrameRatePerSecond` (0 for 1.x; a 2.x timing input to be checked when 2.x data is available), `Rsm.ScaleKeyFrames`, `Rsm.Flatten()`, `Rsm.Downgrade()`, `Rsm2Converter`.

  **Text encoding.** GRF decodes strings with `Utilities.Services.EncodingService.DisplayEncoding`, which is UTF-8 in the editor; Korean texture names (CP949 in the file) come out as replacement characters, which cannot be undone. The adapter sets `DisplayEncoding = EncodingService.Korean` for the read and restores the previous value in a `finally` (main thread only, §5); with it all texture paths of the 140 models resolve. Node names in the data are ASCII.

  **Versions and `.rsm2`.** The working data set has 140 `.rsm` files, all 1.4; all load through `new Rsm((GRF.MultiType)bytes)` with no failure. The full game data has 1.5, 2.2 and 2.3 (`reference-models.md` §6) but none of them is in the working set, so whether `.rsm2` loads through GRF in the editor is **not verified**; until a file is available, those versions get no `RsmRules` row and are reported `UnsupportedRsmVersion` (§4.1, §4.4).
- Versions: the adapter records the version and passes through whatever GRF returns for it, including `.rsm2` through GRF's own `Rsm2` handling if it loads in the editor. A file GRF cannot read gives `false` with GRF's reason and the converter reports one `CorruptFile` issue; a version GRF reads but our rules table (§4.4) has no row for gives one `UnsupportedRsmVersion` issue and no output (FR-8). No version is guessed.
- The adapter never throws to its caller.

### 4.2 Hierarchy: `RsmHierarchy`

Satisfies FR-1.

```csharp
public sealed class RsmTree
{
    public IReadOnlyList<int> Order;               // node indices, every parent before its children
    public IReadOnlyList<int> Roots;
    public int[] Parent;                           // -1 for roots
    public IReadOnlyList<int>[] Children;
    public string[] UniqueNames;                   // original names, made unique (name, name_2, …)
}
public static class RsmHierarchy { public static bool TryBuild(RoRsm rsm, out RsmTree tree, out RoIssue issue); }
```

- Parents are resolved by name, independent of file order (FR-1): the order is a topological sort (stable: among nodes ready at the same time, file order).
- A parent name that matches no node makes that node a root and raises one `RsmParentMissing` warning. A cycle makes the build fail with one `RsmCycle` issue and no output. A name collision keeps the first node as the parent target and renames later ones with `_2`, `_3`, with one `RsmDuplicateName` warning.
- RSM version 1.x has one root; versions that allow several roots are handled the same way (each root is recentered, §4.4).

### 4.3 Texture slots and materials

Satisfies FR-6, FR-7.

- A node's `TextureSlots[face.TextureSlot]` gives an index into `RoRsm.Textures`; the string is parsed as `RoPath.Parse("texture").Combine(name)` (separators and case follow PLAN-0002 §4.1). A bad index is an `RsmBadIndex` issue for that face (the face is dropped, the others kept, one warning per node).
- For every distinct `(texture, twoSided)` used by a node, `ctx.Material(new RoMaterialKey(texture, surface, twoSided))` returns the shared material; `surface` comes from the texture's analysis (`RoMaterialFactory.SurfaceFor`, PLAN-0002 §4.6), so two models using one texture and mode share one material (FR-7). A missing texture gives the placeholder material and one warning naming the texture and the model.
- The shade type does not affect materials; it affects normals only (§4.6).

### 4.4 Placement: `RsmRules`, `RsmPose`, `RsmBounds`

Satisfies FR-2, FR-3. All of this section works in Ragnarok space with column vectors (`p' = M·p`); the conversion to Unity is §4.5.

**Rule set R1** (the rule implemented by the common RSM viewers, to be verified per version by T0.3; D1):

1. **Rest pose of a node.** `restPosition` = the node's first position key if it has position keys, otherwise `Position`. `restRotation` = the node's first rotation key (a quaternion) if it has rotation keys, otherwise the rotation about `RotationAxis` by `RotationAngle` (the file's axis/angle). `restScale` = the first scale key if the version has scale keys and the node has them, otherwise `Scale`.
2. **Local matrix.** `Local = T(restPosition) · R(restRotation) · S(restScale)`.
3. **Model-space matrix.** `Model(n) = Model(parent(n)) · Local(n)` (parents first, §4.2); for a root, `Model = Local`.
4. **Vertex matrix.** Each node's vertices are first taken through the node's own offset: `A = [OffsetMatrix | Offset]` (the 3x3 offset matrix with the offset vector as translation, `v' = OffsetMatrix·v + Offset`). `A` applies to that node's vertices only and is **not** inherited by children. A vertex in model space is `Model(n) · A(n) · v`.
5. **Recentering (FR-3).** From the rest-pose model-space vertices of **all** nodes, take the axis-aligned bounds. With Ragnarok's Y axis pointing down, the model's origin is the bottom center: `Recenter = T(-center.x, -max.y, -center.z)`. It is applied to every root: `Local(root) := Recenter · Local(root)`, which for a root with position keys means adding the same translation to every position key.
6. The recentering uses the rest pose and ignores animation after frame 0, as the viewers do.

- **Offset matrix convention (T0.3).** The file's nine floats are three groups of three, and **each group is a column** of `OffsetMatrix` (equivalently, the file matrix multiplies row vectors: `v' = v · M_file`). Both reference viewers read it this way (roBrowser `mat3.toMat4`, BrowEdit3 `offset[i][j]` in column-major `glm`). Reading the groups as rows changes the result of 139 of the 140 models of the data, so `RoRsm.OffsetMatrix` keeps the file order and `RsmPose` builds the matrix as `A[r, c] = OffsetMatrix[3c + r]`; a test pins this on `나무잡초꽃/버섯02` (`1,0,0, 0,0,1, 0,-1,0`).
- **R1 result (T0.3, 2026-11-17).** R1 was checked in three ways.
  1. **Against the reference implementations.** The 1.x code of roBrowser (`src/Loaders/Model.js`) and BrowEdit3 (`components/Rsm.cpp`, `RsmRenderer.cpp`) was re-implemented in a scratch script and run on all 140 models of the data. After each implementation's own recentering, every vertex of R1 equals roBrowser within 1e-5 and BrowEdit3 within 1.4e-4 Ragnarok units (float rounding). The viewers differ in details that cancel out: roBrowser skips the offset vector of a single-node model, BrowEdit3 ignores the root's position and recenters twice; any constant translation of the whole model disappears in the bottom-center recentering, so all three give the same model.
  2. **Visually.** The four reported models, the control group and one model per arrangement of `reference-models.md` §3 (18 models) were built with R1 in a scratch scene and captured from front, side, back and top (`Uninarok-client/Screenshots/0003-T0.3/`). Every model stands upright with its bottom center at the origin; closed buildings show their outside; no model is offset or rotated.
  3. **Handedness.** BrowEdit3 places models with a flip of both Y and Z (a rotation), which treats the file space as right-handed; the single Y flip of `RoSpace` (PLAN-0002 D6) is the one reflection from that space to Unity's left-handed space, so models are not mirrored. One observation is left for the reference viewer: the two-sided sign planes of `외부소품/이정표01` read correctly from their back face (the texture is mirrored on the side their winding faces), which is how the plane was authored: the owner compared all 18 captures with the reference viewer and they match (2026-11-17).

  | Model | R1 result |
  |---|---|
  | §1 버섯02, 버섯01, 넝쿨01, 덤불02 | match the viewer formulas; upright, bottom center at origin |
  | §2 덤불01, 야자수01, 성_기둥, 휘장, 이정표01 | match; 이정표01 sign reading side to confirm in the viewer |
  | §3 나무01, 나무기둥01, 벤치01, 입구초소, iz_bridge01 (9 nodes), 시계탑 (5 nodes, rotation keys), iz_enchant, 이즈루드초소, 동굴입구 | match |
  | all 140 models | match roBrowser and BrowEdit3 numerically |

- **Shade types (T0.3, plan §4.6 check).** roBrowser: none = one constant normal `(0, -1, 0)` in file space, which is up after the flip (the plan's rule); flat = face normal; smooth = face normals summed per (smoothing group, file vertex index), unweighted. BrowEdit3: none = no normal lighting at all (constant light factor), flat and smooth as roBrowser. The plan's smooth rule differs only where the spec asks for it: it welds by position value (FR-5, no seams where two file vertices share a position) and weights by corner angle (D3). `iz_enchant` and `이즈루드초소` (none), `넝쿨01`, `동굴입구`, `iz_bridge01` (flat) and the smooth models were rendered with these rules in the captures above.
- **Versions found (T0.3).** The working data has only 1.4 (140 files) and no mirrored scale or mirrored offset matrix (no negative determinant). The full game data has 1.5, 2.2 and 2.3 (`reference-models.md` §6). BrowEdit3 shows that 2.x uses a different rule (no position, rotation or scale fields; the offset vector is an absolute position; children use the inverse of the parent's offset matrix; no recentering); it is recorded here as a lead and gets no `RsmRules` row until 2.x data is available. 1.5 differs from 1.4 only by per-node position keys (read by GRF), so its row equals 1.4's with `hasPosKeys`; it is kept out of the table until a 1.5 file can be checked against a viewer.

  | Version | restFromFirstKey | hasScaleKeys | recenter | offsetVectorInVertexMatrix | Status |
  |---|---|---|---|---|---|
  | 1.4 | yes | no | bottom center of rest-pose bounds | yes | verified (T0.3) |
  | other | — | — | — | — | `UnsupportedRsmVersion` |
- The rules live in `RsmRules` as a small table keyed by `(major, minor)` of `{ restFromFirstKey, hasScaleKeys, recenter, offsetVectorInVertexMatrix }` with a row for 1.4 from the reference data and a conservative default row for other 1.x versions that has to be confirmed by T0.3 when such a file is found; for a version with no row and no safe default the converter reports `UnsupportedRsmVersion` (§4.1). Nothing else in the code branches on the version.
- **Why this addresses the reference failures.** The four models in `reference-models.md` §1 are single-node models with a non-zero rotation and position, so the whole result depends on how the rotation, the position, the offset and the recentering combine. R1 does it with one matrix chain in one space, and computes the bounds from our own transformed vertices rather than from values GRF derives for its viewer. The control group of §2 of the reference file (same arrangement, not reported wrong) is part of the same tests so that a rule that fixes the four and breaks the five fails.
- `RsmPose` exposes `ComputeRest(RoRsm, RsmTree, RsmRules) → RsmRestPose` (per-node `Local` and `Model` matrices, `Recenter`) and `RsmBounds.OfRestPose(...)`. Both are pure and take `Matrix4x4` and `Vector3`.
- **Verification (T0.3).** For each reference model R1 is run in a throwaway comparison and its result is compared with the reference viewer from front, side and top (spec FR-2). Any model where R1 differs is recorded with the difference, and the rule (not the model) is corrected in `RsmRules`; the comparison table is stored in this section (as "R1 result by model") when T0.3 is done. If the data forces a different rule for some version the table gets another row; the code does not change shape.

### 4.5 Converting to Unity and building the node objects

Satisfies FR-2, FR-6.

- A node object carries `Local(n)` converted with `RoSpace`: `localPosition = RoSpace.Point(translationOf(Local))`, `localRotation = RoSpace.Rotation(rotationOf(Local))`, `localScale = RoSpace.Scale(scaleOf(Local))`. Because the Y flip is a conjugation, the translation, rotation and scale decompose the same way before and after (PLAN-0002 §4.3), so the matrix chain of §4.4 and the transform hierarchy of the prefab agree. A negative scale component (mirrored model) stays a negative `localScale`; Unity flips the culling for negative determinants, so mirrored nodes render correctly (FR-2).
- A node's mesh vertices are `RoSpace.Point(A·v)` (the node's own offset baked in, §4.4 step 4). If the determinant of `OffsetMatrix` is negative the triangle winding is reversed once more, so that the final facing is outward (FR-6).
- Triangle winding: a mesh builder calls `RoSpace.ReverseWinding` once, for the conversion, plus the extra reversal above when the offset matrix mirrors. No other place reverses winding (PLAN-0002 §4.3).
- Texture coordinates: `uv = (u, 1 - v)`, as in the old importer, applied once in the mesh builder (§4.6).

### 4.6 Meshes: `RsmMeshBuilder` and `RsmShading`

Satisfies FR-4, FR-5, FR-6.

```csharp
public sealed class RsmMeshData
{
    public Vector3[] Positions; public Vector3[] Normals; public Vector2[] Uvs;     // Unity space, node-local
    public IReadOnlyList<RsmSubmesh> Submeshes;                                      // sorted by (texture index, twoSided)
}
public sealed class RsmSubmesh { public int TextureIndex; public bool TwoSided; public int[] Triangles; }
public static class RsmMeshBuilder
{
    public static RsmMeshData Build(RoRsmNode node, RsmShadeType shade, bool hasShadeType, in Matrix4x4 vertexMatrix,
                                    bool reverseExtraWinding, RoReport report);
}
```

**Vertex de-duplication (FR-4).**

1. Positions are welded by value: the converted `(x, y, z)` as exact float bit patterns, with `-0` normalized to `0`. Two vertex indices of the file with equal coordinates are one position.
2. A **vertex key** is `(welded position, u, v bit patterns, normal group key)`. Each distinct key becomes one vertex; every face corner looks its key up. The vertex count therefore equals the number of distinct (position, texture coordinate) pairs, plus extra vertices only where the shading rule below demands different normals at the same position and coordinate.
3. Faces keep their order within a submesh. Duplicate and degenerate faces in the file are kept as they are (the format reference notes duplicates exist; the original client draws them), but a zero-area face contributes nothing to normals.
4. Indices are 16-bit unless a node has more than 65,535 vertices, then 32-bit.

**Normals (FR-5): `RsmShading`.** The normal group key of a face corner depends on the shade type:

| Shade type | Normal group key | Result |
|---|---|---|
| Smooth, or the version has no shade type | `(welded position, face.SmoothGroup)` | faces that meet at a position and are in the same smoothing group share one normal, **across texture seams too** because the position is welded by value, not by (position, uv); faces in different groups keep a hard edge; a version without smoothing groups has every face in group 0, which is smooth everywhere as in the original client |
| Flat | face index | every face keeps its own normal (crisp facets) |
| None | a constant | all vertices get one fixed normal (up, in Unity space), which gives the uniform look of an unshaded model; verified on the `iz_enchant` and `이즈루드초소` references in T0.3 |

- A shared normal is the sum of the face normals at that position and group, **weighted by the corner angle** of each face at that position and then normalized. Angle weighting makes the result independent of how a surface is triangulated.
- Face normals come from the converted triangle after the winding correction, so they point outward by construction.
- Smoothing-group equality uses the integer `SmoothGroup` that GRF reports; if T0.2 shows the field is a bit mask in some version, the equality becomes "masks intersect" (a one-line change in `RsmShading.SameGroup`, covered by a test).
- **Spec note (D3).** FR-4's acceptance counts distinct (position, UV) pairs; a hard edge (flat shading, or different smoothing groups) where two faces share both position and UV needs two vertices with different normals, so the count there is higher by exactly those vertices. The tests assert the exact equality for smooth models and "distinct (position, uv, normal group)" for the others; the spec wording is raised with the owner (§8 D3).

**Submeshes (FR-6).** Faces are grouped by `(texture index, twoSided)` into submeshes, in ascending order, so a node has at most one submesh per texture per sidedness. A two-sided face goes to a two-sided submesh and the material key carries `twoSided = true` (§4.3); one-sided faces cull their back. Two-sided faces are **not** duplicated in the index buffer.

### 4.7 Prefab: `RsmPrefabBuilder`

Satisfies FR-1, FR-7, FR-10.

- An in-memory `GameObject` root named after the model; one child per node, created in `RsmTree.Order`, parented by the tree, named `UniqueNames`, with local transforms from §4.5. Nodes with at least one face get a `MeshFilter` (the node's mesh asset) and a `MeshRenderer` (materials in submesh order, shadow casting and receiving left at Unity defaults, light probes off, per SPEC-0003 non-goals).
- The prefab is saved with `PrefabUtility.SaveAsPrefabAsset` at the mirrored path, then the in-memory object is destroyed. Saving to an existing path replaces its content and keeps its GUID; mesh and material references are to asset objects (meshes upserted first through `RoAssetWriter.UpsertObject`, in place, PLAN-0002 §4.7), so references to the model from maps (PLAN-0006) survive re-conversion (SPEC-0002 FR-5).
- Meshes and materials are separate assets of the model (FR-10): a mesh can be assigned to a collider, a material can be changed without touching the source.
- A model with no animation has no `Animator` and no scripts; a model with animation also gets the `Animator` of §4.8.

### 4.8 Animation: `RsmAnimationData`, `RsmClipBuilder`, `RsmControllerBuilder`

Satisfies FR-9.

```csharp
public sealed class RsmAnimationData
{
    public float LengthSeconds;                    // from AnimationLength via RsmTiming
    public IReadOnlyList<RsmNodeCurves> Nodes;     // only nodes with keyframes
}
public sealed class RsmNodeCurves { public int Node; public RsmKey<Vector3>[] Position; public RsmKey<Quaternion>[] Rotation; public RsmKey<Vector3>[] Scale; }
public readonly struct RsmKey<T> { public float Time; public T Value; }
public static class RsmTiming { public const float SecondsPerUnit = 0.001f; }                  // candidate, §below
```

- **Keys are converted once, in Core.** Position keys are `RoSpace.Point`; for a root they include the `Recenter` translation (§4.4 step 5). Rotation keys (quaternions) become matrices, go through `RoSpace.Rotation` and back to quaternions (this is the conjugation by the Y flip; for a quaternion `(x, y, z, w)` it gives `(-x, y, -z, w)`, which the tests assert). Scale keys are unchanged. A negative `w` is flipped to the nearest sign to the previous key so interpolation takes the short way.
- **Timing (D4).** `RsmTiming` converts the file's keyframe units to seconds with one constant. The working assumption is that they are milliseconds of the original client's animation clock; T0.4 checks it on the clock tower (`reference-models.md` §3), against the reference viewer's speed and direction, and the constant (and any per-version difference, as a column of the `RsmRules` table) is corrected there. The clip length is `AnimationLength` converted the same way.
- **Rotation interpolation.** The original interpolates rotation keys spherically; an `AnimationClip` quaternion curve is interpolated per component and normalized. To keep the motion equal (FR-9), `RsmClipBuilder` inserts extra keys, computed with an exact slerp, wherever two consecutive rotation keys differ by more than 10° (constant), so the per-component interpolation never strays visibly. Position and scale curves are linear with linear tangents (no smoothing). Keys are written in time order; the result is deterministic.
- **Clip.** One `AnimationClip` per animated model, with curves for `localPosition`, `localRotation` and `localScale` on the path of each animated node (path = names from the root, `UniqueNames`). Loop time is on, with the first key at time 0 as the rest pose of §4.4 and the clip length as above. Nodes without keys keep their static transform.
- **Playback.** The prefab gets one `Animator` whose controller is `b.controller` (`UnityEditor.Animations.AnimatorController`): one layer, one looping state `Play` with the clip, default state, no transitions, no parameters. The animator uses `cullingMode = CullUpdateTransforms` (no transform updates while off-screen) and no root motion. Nothing here needs a runtime script, so the importer assembly stays editor-only and the prefab references only Unity types.
- **Budget (FR-9, D5).** The "agreed budget" of the spec is proposed here and is part of the plan's approval: a map with 200 instances of an animated model produces **no managed allocation per frame** in steady state (Unity Profiler `GC.Alloc` is zero for the animator update), and costs at most **0.5 ms** of main-thread animation update per frame in a standalone development build on the owner's development machine, with off-screen instances costing close to nothing (culling). Measured by T9.x with 200 instances of the clock tower; if the budget is missed the fallback is in D5.

### 4.9 Converter: `RoRsmConverter`

Satisfies FR-8, FR-10 (and uses SPEC-0002 FR-5, FR-6, FR-10, FR-16).

```csharp
public sealed class RoRsmConverter : IRoConverter       // registers RoJobKind.Model
{
    public const int RulesVersion = 1;
    public RoResult<UnityEngine.Object> Convert(RoImportContext ctx, RoPath path);    // returns the prefab
}
```

- `RoJobKind.Model` is added to the enum and `RoConverterRegistry` of PLAN-0002 §4.10 with the one-line wrapper `RoImport.Model(string roPath)`.
- Steps as in §2. The ledger entry (category `model`) lists as outputs the prefab, every mesh, and the clip and controller if present; as dependencies the source hash of every texture the model uses (the surface mode, and so the material, depends on the texture's content). A change in a texture's content makes the model stale; a changed rules version makes every model stale.
- One failing model never stops a batch: `RoReport.Fail` with the reason; no partial output is left (outputs are written only after the whole model is built in memory; a failure while writing removes the ledger entry and leaves the previous outputs untouched).
- Expected conditions (missing texture, unsupported version, bad index, duplicate name) are warnings (PLAN-0002 §4.9) and new `RoIssueCode` values: `UnsupportedRsmVersion`, `RsmParentMissing`, `RsmCycle`, `RsmDuplicateName`, `RsmBadIndex`.
- Menu and window (PLAN-0002 §4.11) gain the kind `Model` and the entry `Tools/Ragnarok/Convert All Models` (`Enumerate("model/")`, extensions `.rsm` and `.rsm2`).

## 5. Threading and lifecycle

Satisfies FR-9, FR-10.

- Main thread only, synchronous, inside the batch of PLAN-0002 §4.10. The pure `Core/Rsm` code is allocation-friendly arrays and structs and has no Unity object dependencies except math types.
- The in-memory prefab object, the temporary meshes and the controller are created and destroyed inside one `Convert` call; none survives it. Large models (the largest, `라헬/분수`, has 23 nodes and 1078 keyframes) are built node by node and released before the next one.
- At runtime nothing of this plan runs. The player only sees prefabs, meshes, materials, clips and controllers.

## 6. Test plan

Satisfies every FR of the spec. EditMode NUnit tests in `Uninarok.RoImport.Tests` (command in `CLAUDE.md` §2). Tests that need the real data set are marked `[Category("NeedsData")]` and are ignored with a message when the source folder is absent; every pure test builds its `RoRsm` in code.

| Test class | FR | Checks |
|---|---|---|
| `RsmHierarchyTests` | 1 | child listed before parent gives the correct tree; names kept; duplicate names get `_2` and one warning; missing parent makes a root with one warning; a cycle fails with one issue; order is stable |
| `RsmPoseTests` | 2, 3 | hand-computed cases: identity node; rotation only; position only; position + rotation (the class of the reference failures); offset matrix applied to the node's vertices and **not** to its child; parent rotation moves the child; first-key rest pose beats the static fields; mirrored scale; `Recenter` puts the bounds' bottom center at the origin and shifts position keys; the same model with the old importer's formula fails the test (guard against reintroducing it) |
| `RsmSpaceConversionTests` | 2, 6 | a node's Unity `localPosition/Rotation/Scale` composed with its mesh vertices equals `RoSpace.Matrix(Model·A)` applied to the Ragnarok vertex, for random nodes; a unit cube has positive signed volume (outward facing) before and after a mirrored scale and a mirrored offset matrix |
| `RsmMeshBuilderTests` | 4 | a cube with per-face UVs → 24 vertices; a cube with seamless UV → 8; a strip approximating a cylinder → vertices equal the number of distinct (position, uv) pairs (smooth); duplicate file vertices with equal values merge; a model with duplicate faces keeps both |
| `RsmShadingTests` | 5 | smooth: a cylinder with a UV seam has equal normals on both sides of the seam; angle weighting is independent of triangulation of a flat quad; flat: each face keeps its normal and the vertex count rises exactly by the duplicated corners; two smoothing groups keep a hard edge; none: constant normal; no shade type in the version: smooth |
| `RsmSubmeshTests` | 6, 7 | faces group by (texture, twoSided) in ascending order; two-sided faces are not duplicated; the material key of a two-sided group has `twoSided = true`; a missing texture slot index drops that face with one warning |
| `RsmAnimationTests` | 9 | keys convert: position through `RoSpace.Point` (plus recenter for roots), rotation `(x,y,z,w) → (-x,y,-z,w)`, scale unchanged; `RsmTiming` maps units to seconds; slerp subdivision keeps the sampled rotation within 0.5° of the exact slerp for random key pairs; sign continuity |
| `RsmWriterTests` | 1, 10 | converting a small hand-made model to the throwaway output root yields the prefab, meshes and materials as separate assets with the paths of §3; assigning a mesh to a `MeshCollider` and editing a material do not touch the source; hierarchy of the prefab equals the tree |
| `RsmIdempotencyTests` | (0002 FR-5, FR-16) | second conversion is `Skipped`; a forced second conversion leaves every mesh, material, clip and controller byte-identical and every GUID unchanged; deleting only the prefab and rerunning restores it with unchanged meshes |
| `RsmReferenceTests` (`NeedsData`) | 2, 4, 5, 8 | each model of `reference-models.md` converts with no failures; for the four reported-wrong models and the control group, the root's rest transform and bounds match the table recorded by T0.3; vertex counts are lower than the old importer's (3 per face) with equal triangle count; a batch over the full `model/` folder converts every file or lists each failure once; any non-1.4 file found is converted or reported once |

**Manual checks** (recorded in the PR with screenshots, referenced from `reference-models.md`):

- FR-2: every model in the reference tables, front, side and top, against the reference viewer, side by side (`reference-models.md` §1 first).
- FR-5: a curved model (`야자수01` trunk, a barrel) has no facets or seams at texture borders; `넝쿨01`, `동굴입구` and `iz_bridge01` (flat) keep crisp edges; `iz_enchant` and `이즈루드초소` (none) match the reference.
- FR-9: the clock tower hands move at the original speed and direction; the 200-instance budget measurement of §4.8 with the Profiler.
- FR-3 is closed together with SPEC-0006's ground-contact check on prontera, izlude and prt_fild08.

## 7. Migration and compatibility

- `RsmImpoter.cs` (a `ScriptedImporter` on `.rsm`), `RsmImpoter.bkp.cs` and the helpers of `UtilExtensions.cs` that only it uses are deleted by the last task of this plan's implementation. `UtilExtensions.cs` is deleted only when no remaining file uses it; the remaining users (the GND, GAT and RSW importers) are removed by PLAN-0004 to PLAN-0006, and the last of those deletes it.
- Models imported by the old importer are sub-assets of the `.rsm` source files, which are hidden from Unity (PLAN-0002 §7); nothing of them is referenced by committed scenes except `RswImporter`'s prefab lookup, which PLAN-0006 replaces. No data migration is needed.
- PLAN-0006 (map scene) places the prefabs of this plan. The contract between them is exactly: the prefab at the mirrored path (§3), whose root has no transform of its own and whose origin is the bottom center of the rest-pose bounds (§4.4).
- Output assets are covered by the LFS path rule of PLAN-0002 §7 (`*.asset` under `Art/Imported/`). Prefabs, clips and controllers stay text.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | One rule set R1 in Ragnarok space, built from our own bounds and one matrix chain; version differences in one small table; a throwaway reference comparison (T0.3) verifies it before implementation | Reusing GRF's derived `Global*`/`Local*` values (they serve GRF's viewer, their meaning is undocumented, and the old importer's use of them is the likely source of the wrong placements); per-model corrections (does not scale to the full data set) |
| D2 | The GRF library reads RSM; the adapter produces our own `RoRsm`; rules are tested on hand-built `RoRsm` objects | Parsing RSM bytes ourselves (owner decision: read through GRF); applying rules directly on GRF's types (untestable without data, couples every rule to the library) |
| D3 | Vertex key is (welded position, uv, normal group); smooth shading welds across texture seams by position value; counts equal distinct (position, uv) pairs for smooth models and rise only where hard edges need separate normals | De-duplicating by (position index, uv index) (does not merge equal values, misses seams); averaging normals across all faces regardless of groups (rounds off crisp edges); recalculating normals with Unity's `RecalculateNormals` (splits at every uv seam, which is the lighting bug of the old importer). **Spec wording of FR-4's acceptance is to be aligned with this (owner)** |
| D4 | Animation timing in one constant plus per-version column, verified on the clock tower | Hard-coding per model; trusting a number from a format note without the reference |
| D5 | Clip + `Animator` with a one-state controller per model, no runtime script; culling mode `CullUpdateTransforms`; budget of §4.8 | A custom runtime component sampling keyframes (needs a runtime assembly and breaks the editor-only importer); a legacy `Animation` component (deprecated path, less culling control); baking animation into vertex shaders (rigid node animation does not need it; large extra system). **If the budget is missed:** an instance-level `Animator` disable when off-screen, then a runtime sampler in a separate runtime assembly decided in a new plan |
| D6 | One mesh per node, nodes stay separate (spec non-goal), with the node's own offset baked into its vertices | Merging static nodes (non-goal); baking the whole chain into vertices (breaks animation) |
| D7 | Two-sided faces go to separate submeshes with a two-sided material | Duplicating the triangles with reversed winding (doubles index data, breaks smooth normals); making whole models two-sided (see-through walls and z-fighting) |
| D8 | Mesh and animation assets are separate files per node and per model | Sub-assets of the prefab (read-only in the editor, spec FR-10 forbids) |

| D9 | (T0.2, T0.3, 2026-11-17) The offset matrix's file groups are columns; the reader sets GRF's display encoding to Korean for the read; the rules table has a verified row for 1.4 only, and 1.5 and 2.x stay `UnsupportedRsmVersion` until a file of that version is checked | Reading the matrix as rows (rotates 139 of 140 models wrongly); post-correcting garbled texture names (lossy); a guessed "conservative default row" for other 1.x versions (§4.4 first draft: nothing in the working data can verify it) |

Items for the owner's approval: D3's spec wording, and the animation budget of §4.8 (D5).

## 9. Risks

| Risk | Mitigation |
|---|---|
| GRF's member semantics differ from the file fields in ways the names do not reveal | T0.2 maps and checks every member against raw values of the reported models before any rule depends on it; derived members are never used |
| R1 is the commonly implemented rule but not documented authoritatively; some version or arrangement differs | T0.3 compares every reference model with the reference viewer before implementation; the rules table isolates version differences; the control group guards against fixing one case and breaking another |
| The data has only version 1.4 and no mirrored scale (`reference-models.md` §5) | Unsupported versions warn once and produce no output; synthetic tests cover mirrored scale and the offset-matrix mirror; a scan of the full extracted data for other versions is part of T0.3 when it is available |
| Smoothing groups may be a bit mask in some versions | One-line `SameGroup` switch, covered by tests (§4.6) |
| Keyframe unit and loop behavior are assumptions | T0.4 verifies on the clock tower and the stress model `라헬/분수`; one constant to change |
| 200 animated instances exceed the budget | D5 fallback chain; measurement is a task, not a hope |
| Prefab serialization is not byte-identical on a forced re-save (object IDs inside the prefab) | The determinism test compares meshes, materials, clips and controllers byte-for-byte and the prefab by structure; if the prefab differs textually the plan records it and SPEC-0002 FR-16 is read as applying to the data files only (owner) |
| A very large model exceeds 16-bit indices | 32-bit indices are chosen per node (§4.6) |
| Korean names in mesh file names | Node names are made file-safe (invalid characters replaced, Korean kept); tests use a Korean-named node |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 3, 4.2, 4.7, 6 |
| FR-2 | 2, 4.1, 4.4, 4.5, 6 |
| FR-3 | 4.4, 6 (closed with PLAN-0006) |
| FR-4 | 4.6, 6 |
| FR-5 | 4.6, 6 |
| FR-6 | 4.3, 4.5, 4.6, 6 |
| FR-7 | 4.3, 4.7, 6 |
| FR-8 | 4.1, 4.9, 6 |
| FR-9 | 4.8, 5, 6 |
| FR-10 | 3, 4.7, 4.9, 6 |
