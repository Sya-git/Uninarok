# SPEC-0006: RSW Map Scene

| | |
|---|---|
| **Status** | Approved |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | plan.md _(written after approval)_ |
| **Tasks** | tasks.md _(written after plan approval)_ |

## 1. Why

A map is the ground (GND), walkability (GAT) and a list of placed objects (RSW). The client needs a playable map in the Unity scene format, so it gets lightmapping, batching and culling, with map-wide data held separately. Placement mistakes (wrong offset, wrong rotation, models floating or sunk) are the most visible import defects.

## 2. Goals

1. One conversion produces a complete map: scene, ground, placed models and map data.
2. Every placed model sits where it sits in the original client.
3. The result supports lightmaps, batching and culling, and re-conversion.

## 3. Non-goals

- Server-side map logic (warps, spawns, NPCs).
- Water, effects and sound playback (placeholders only).
- Runtime streaming of maps.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Converting a map by name yields a scene containing the ground (SPEC-0004), all placed models, and a link to the map data asset (SPEC-0005). | Opening the generated scene shows a complete map, and the map data asset is reachable from it. |
| FR-2 | M | Each placed model is an instance of the converted model (SPEC-0003), not a copy, so editing the model updates every instance. Instances keep the name given in the map file. | Changing a model's material changes all its instances. |
| FR-3 | M | Position, rotation and scale of every instance match the original client, including positions relative to the map centre, any rotation, and mirrored (negative) scale. | On the reference map, screenshots from fixed cameras match the reference viewer. Models of the reference set show no offset, tilt or mirroring. |
| FR-4 | M | Instances stand on the ground as in the original: a sample of at least 20 models on the reference map touches the ground within a small tolerance, and none is buried or floating beyond what the original shows. | The check passes on the sample. |
| FR-5 | M | Instances are organised under category groups (models, lights, sounds, effects) so the scene is easy to read. | The hierarchy shows groups with counts. |
| FR-6 | M | Instances and ground are set up for baking and batching (static). | Baking lightmaps and static batching work on the map with no change by the user. |
| FR-7 | M | Map-wide values (size, water level, lighting values, music name) are stored in the map data asset. | The values for a reference map equal the source's. |
| FR-8 | S | Map lighting values create matching scene lights (ambient and main directional). | A reference map's lighting resembles the reference client. |
| FR-9 | S | Lights, sound sources and effect emitters from the map become named placeholder objects holding their parameters. | A map with torches lists a light object per torch with its colour and range. |
| FR-10 | M | A missing model gives one warning naming the model and map, and the map still converts. | A map referencing a missing model converts and the warning appears once. |
| FR-11 | M | Re-converting a map regenerates the generated scene without breaking links to it, and custom changes kept in a separate additive scene survive (decided). | A custom object in an additive scene remains after re-conversion. |
| FR-12 | M | Every map in the source folder converts in a batch (SPEC-0002 FR-10). | The batch ends with a complete list of results. |
| FR-13 | S | Selecting the map object shows its bounds and the map axes as gizmos. | Gizmos show the map rectangle and the origin. |

## 5. Success criteria

- Reference maps match the reference viewer in fixed-camera screenshots.
- All maps convert in one batch; a second batch changes nothing.

## 6. Assumptions and constraints

- Shared rules (units and axes, textures, materials, idempotency, batches) are in SPEC-0002 and apply here.
- Models are converted first or on demand (SPEC-0002 FR-6).
- Map objects other than models are placeholders in this spec; their behaviour is a later spec.

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | Are placed-object lighting overrides (per-model shadow flags) needed? | FR-6 |
| OQ-2 | Where is the scene stored relative to the existing project scenes (location convention)? | FR-1 |
