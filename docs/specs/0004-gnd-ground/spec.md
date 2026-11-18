# SPEC-0004: GND Ground

| | |
|---|---|
| **Status** | Implemented |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | plan.md _(written after approval)_ |
| **Tasks** | tasks.md _(written after plan approval)_ |

## 1. Why

Every map's terrain is stored as a grid of cubes with corner heights and texture tiles. The ground must be rebuilt as a clean mesh that matches the original exactly: no holes, no stray walls, no swapped rows and columns on non-square maps.

## 2. Goals

1. Convert a map's ground into a mesh that matches the original client.

## 3. Non-goals

- Water surfaces (future spec).
- Walkability data (SPEC-0005).
- Placing models (SPEC-0006).
- Editor gizmos: the walkability gizmos are SPEC-0005 FR-7 (FR-9 below was removed by the owner on 2026-11-18).

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
| FR-8 | S | _Not implemented (owner decision, OQ-2)._ The map's baked ground lighting (per-tile shadow brightness and color) is applied. | A map with painted shadows shows them as in the reference client. |
| FR-9 | M | _Removed by the owner on 2026-11-18: gizmos belong to the walkability data, SPEC-0005 FR-7._ Selecting the ground showed editor gizmos. | |

## 5. Success criteria

- Side-by-side comparison with the reference viewer shows no difference in heights, walls or textures on the reference maps.

## 6. Assumptions and constraints

- Shared rules (units and axes, textures, materials, idempotency, batches) are in SPEC-0002 and apply here.
- Reference maps are listed in `0003-rsm-models/reference-models.md`: prontera (156 x 196 cubes) and izlude (134 x 150) are non-square, prt_fild08 is 200 x 200.

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | None open. |  |
| OQ-2 | Closed by the owner: nothing is baked; Unity's real time lighting and shadows are used. FR-8 is not implemented. | FR-8 |
