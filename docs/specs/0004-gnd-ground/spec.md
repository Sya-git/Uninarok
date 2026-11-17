# SPEC-0004: GND Ground

| | |
|---|---|
| **Status** | Approved |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | plan.md _(written after approval)_ |
| **Tasks** | tasks.md _(written after plan approval)_ |

> This file defines **what** and **why**. It contains no class names, file paths or library choices. Those live in `plan.md`. Shared rules are in SPEC-0002.

## 1. Why

Every map's terrain is stored as a grid of cubes with corner heights and texture tiles. The ground must be rebuilt as a clean mesh that matches the original exactly: no holes, no stray walls, no swapped rows and columns on non-square maps. Terrain errors are hard to spot by eye on a large map, so the importer must also give a way to check the result visually.

## 2. Goals

1. Convert a map's ground into a mesh that matches the original client.
2. Make the generated ground inspectable and verifiable in the editor.

## 3. Non-goals

- Water surfaces (future spec).
- Walkability data (SPEC-0005).
- Placing models (SPEC-0006).

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Each cube produces its top surface from its four corner heights, with the tile's texture and coordinates. A cube without a top tile draws no top. | A reference map's top surfaces match the reference viewer. A deliberately empty cube is a visible gap. |
| FR-2 | M | Vertical walls appear between neighbouring cubes only where their heights differ and a wall tile exists, using the tile of the cube that owns the wall. Cubes on the map edge never read outside the map. | A cliff has a wall with the correct texture. Flat areas have no wall faces. No error on edge cubes. |
| FR-3 | M | The ground has no cracks between cubes and no overlapping duplicate faces. | Top-down and side views of a reference map show no light leaks. The face count equals the expected top plus wall count. |
| FR-4 | M | Rows and columns are correct on maps whose width differs from height. | On a non-square reference map the ground matches the reference viewer, not a rotated or mirrored copy. |
| FR-5 | M | Cube size and height scale follow SPEC-0002 FR-7 (one cube is one unit wide) and use the map's own values, not fixed constants. | A map with a non-default cube proportion renders at the right size. |
| FR-6 | M | Maps of any size in the game are supported (no limit that breaks large maps), the ground is split so parts out of view can be skipped, and one material is used per texture. | The largest map in the data imports and renders. Moving the camera away from a region stops drawing it. |
| FR-7 | M | Lit shading is smooth across cubes: no visible grid pattern on flat or sloped ground. | A flat reference area shows even lighting. |
| FR-8 | S | The map's baked ground lighting (per-tile shadow brightness and color) is applied. | A map with painted shadows shows them as in the reference client. |
| FR-9 | M | Selecting the ground (or its map object) shows editor gizmos: the cube grid, cube coordinates, corner heights, and for each face whether it is a top, front wall or side wall plus its texture. Each layer can be switched on and off. Gizmos are limited to the selected area when the map is large. | Selecting a reference ground shows the grid. Hovering or selecting a cube shows its data. Switching a layer off hides it. No gizmo cost in builds or when not selected. |

## 5. Success criteria

- Side-by-side comparison with the reference viewer shows no difference in heights, walls or textures on the reference maps.
- Gizmos let a person confirm a suspect cube without opening the source file.

## 6. Assumptions and constraints

- Reference maps are listed in `0003-rsm-models/reference-models.md`: prontera (156 x 196 cubes) and izlude (134 x 150) are non-square, prt_fild08 is 200 x 200.
- Gizmos are editor-only (SPEC-0002 FR-14).

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | None open. |  |
| OQ-2 | Are lightmaps wanted in the first delivery or with Unity's baking only? | FR-8 |
