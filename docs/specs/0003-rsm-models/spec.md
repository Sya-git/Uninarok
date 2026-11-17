# SPEC-0003: RSM Models

| | |
|---|---|
| **Status** | Approved |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | plan.md _(written after approval)_ |
| **Tasks** | tasks.md _(written after plan approval)_ |

> This file defines **what** and **why**. It contains no class names, file paths or library choices. Those live in `plan.md`. Shared rules (units, textures, materials, idempotency, batches) are in SPEC-0002.

## 1. Why

RSM models are the props and buildings of every map. Today most import fine, but some come out with wrong rotation or offset, mesh data is bloated by one vertex per face corner, and lighting shows hard facets on curved surfaces. Models must look and sit exactly as in the original client, and be efficient enough to place by the thousand.

## 2. Goals

1. Each RSM becomes a reusable model with its node structure intact.
2. Every node ends up at the position, rotation and scale the original client shows, for all node arrangements found in the data.
3. Meshes are compact and light correctly.
4. Animated models animate as in the original.

## 3. Non-goals

- Placing models in maps (SPEC-0006).
- Merging nodes into one mesh (nodes stay separate, decided).
- Shadows and lightmap baking settings.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | A model has one object per RSM node, with the same names and parent/child structure as the file, regardless of the order nodes appear in the file. Each node has its own mesh. | A model whose child node appears before its parent still has the correct hierarchy. Node names match the source. |
| FR-2 | M | Every node's position, rotation and scale match the original client for all arrangements in the data: nodes with or without a parent, with or without rotation, with an offset or global transform, mirrored (negative) scale, and animated nodes (shown in their rest pose). | A reference set of models covering each arrangement matches the reference viewer in screenshots from front, side and top. Zero models in the reference set are offset, rotated or mirrored. |
| FR-3 | M | The model's origin is the point the original client uses when placing it on a map, so a model placed at its map coordinates stands where the original puts it. | A reference building placed at its map coordinates touches the ground as in the reference client (checked in SPEC-0006). |
| FR-4 | M | Vertices that share position and texture coordinate are stored once. A vertex is duplicated only where position or texture coordinate differs. | For a box-like and a curved reference model the vertex count equals the number of distinct (position, texture coordinate) pairs, and the model looks identical to the non-merged version. |
| FR-5 | M | Lighting follows the shading information in the file: where it defines smoothing groups or a shading type (none, flat, smooth), those are used; where it defines none, shading is smooth as in the original client. Normals are shared between faces that meet at a position within a smoothing group, including across texture seams. | A lit curved model (for example a barrel or tree trunk) shows no facets or lighting seams along texture borders. A model whose file marks flat shading, or whose faces are in different smoothing groups, keeps crisp edges. A model from a version without shading data matches the reference client. |
| FR-6 | M | Faces are grouped by texture. Faces flagged two-sided are visible from both sides; other faces from the front only. Triangle facing is correct (no inside-out surfaces). | A two-sided leaf plane is visible from both sides. A closed building shows no see-through walls from outside. |
| FR-7 | M | Textures and materials come from the shared pipeline of SPEC-0002 (lit, cutout where the texture uses the transparency color). | Models sharing a texture share a material. |
| FR-8 | M | Every RSM version present in the data imports. A version that cannot be read gives one warning naming the file, and no partial output. | A batch over the full model folder converts all files or lists each unsupported one once. |
| FR-9 | M | Animated nodes (position, rotation, scale over time, whichever the version defines) animate as in the original: same motion, speed, direction and loop. Many animated instances in one map stay cheap. | The clock tower reference model (see `reference-models.md`) moves at the original speed and direction. A map with 200 animated instances adds no per-frame memory garbage and keeps its frame rate within the agreed budget. |
| FR-10 | M | Meshes and materials of a model are separate editable assets (SPEC-0002 FR-4). | A mesh can be assigned to a collider and a material can be changed without touching the source. |

## 5. Success criteria

- The reference model set matches the reference viewer with no wrong offset, rotation or mirroring.
- Vertex counts drop compared with the current importer on the same models, with identical appearance.
- The full model folder converts in one batch.

## 6. Assumptions and constraints

- A reference viewer (the original client or a trusted viewer) is available for comparison.
- Other RSM versions and mirrored (negative) scale models are assumed to exist in the full game data, although the reference data has none (decided).
- Transform meaning can differ between RSM versions. The set of rules per version is established from the reference implementation and recorded in the plan.
- Animation is delivered as animation clips (decided). How the clips are played, and how many animated instances stay cheap, is decided in the plan against FR-9.
- The reference models and maps are listed in `reference-models.md` next to this spec. It is normative for the acceptance checks of FR-2, FR-3, FR-5 and FR-9.
- RSM files can carry shading data: per-face smoothing groups and, in later versions, a per-model shading type (none, flat, smooth). Which versions carry which is recorded in the plan from the format reference ([RagnarokFileFormats RSM](https://github.com/rdw-archive/RagnarokFileFormats/blob/master/RSM.MD)) and the data set. Older versions have no such data and are shaded smooth by the original client.

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | None open. Other RSM versions and mirrored-scale models are assumed to exist in the full data (decided); the plan must handle them and may search the full data. |  |
