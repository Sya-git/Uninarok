# SPEC-0005: GAT Map Data

| | |
|---|---|
| **Status** | Approved |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | plan.md _(written after approval)_ |
| **Tasks** | tasks.md _(written after plan approval)_ |

## 1. Why

Movement, pathing and click-to-walk need to know which cells of a map are walkable and how high they are. That information is in the GAT file. Gameplay must use one map data asset created at import time, never parse the source file or depend on a debug texture.

## 2. Goals

1. Each map has one data asset holding its cell grid.
2. Gameplay can ask whether a cell is walkable and how high a point is.
3. The data can be checked visually in the editor.

## 3. Non-goals

- Pathfinding algorithms and movement rules.
- Server-side validation.
- Map-level settings such as lighting and music (SPEC-0006).

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | The map data asset holds for every cell: its type (walkable, blocked, snipable and so on, mapped to the project's own set of values) and the heights of its four corners. | For sample cells of a reference map the stored type and heights equal the source's. |
| FR-2 | M | Walkability and height can be queried by cell and by world position. Positions outside the map are non-walkable and never throw. | Querying cell (0,0), the last cell and one outside gives correct results. |
| FR-3 | M | Cell positions are consistent with the ground of SPEC-0004: a cell lies where the original client shows it, including on non-square maps. | The centre of every walkable sample cell lies on the ground surface within a small tolerance. |
| FR-4 | M | The asset is available in player builds and does not require the source file. | A build without the source folder queries a cell correctly. |
| FR-5 | M | Storage is compact: flat data, no per-cell objects, growing linearly with cell count. | The asset for the largest map stays within an agreed size per cell (target 24 bytes). |
| FR-6 | M | There is exactly one map data asset per map, created or updated at a predictable location. Re-conversion updates it in place without changing references to it (SPEC-0002 FR-5). | Converting twice leaves one asset with the same identity. |
| FR-7 | M | Selecting the map data asset's map shows gizmos: cells coloured by type, corner heights, grid, and a legend. Types and layers can be toggled, and drawing is limited to a region around the selection or a chosen area on large maps. | Walkable and blocked cells show different colours. A toggle hides a type. A large map stays responsive. |
| FR-8 | S | A flat image of the cell types can be generated for quick inspection, as a separate optional asset. | The image of a reference map shows the same layout as the gizmos. |

## 5. Success criteria

- Sample queries match the source on every reference map.
- Gameplay code that needs walkability uses only the map data asset.

## 6. Assumptions and constraints

- Shared rules (units and axes, textures, materials, idempotency, batches) are in SPEC-0002 and apply here.
- Cells are finer than ground cubes (2 by 2 cells per cube), as in the format.
- The set of cell types and their meaning is confirmed from the format reference during planning.

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | Does gameplay need per-corner heights, or one height per cell? (Storage size.) | FR-1, FR-5 |
| OQ-2 | Which cell types does the server treat as walkable for this client? | FR-1 |
