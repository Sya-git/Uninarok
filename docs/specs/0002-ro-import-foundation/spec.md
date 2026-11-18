# SPEC-0002: Ragnarok Import Foundation

| | |
|---|---|
| **Status** | Implemented |
| **Created** | 2026-11-16 |
| **Owner** | _(fill in)_ |
| **Plan** | [plan.md](plan.md) |
| **Tasks** | [tasks.md](tasks.md) |

Part of the importer family: 0002 foundation (this), 0003 models (RSM), 0004 ground (GND), 0005 map data (GAT), 0006 map scene (RSW), 0007 sprites (SPR/ACT/PAL). Requirements shared by all of them live here.

## 1. Why

The game's content (models, maps, textures, sprites) exists only in Ragnarok formats. The client needs it as normal Unity assets. The first attempt converts files inside per-extension importers: results are stored inside the source file's asset (so materials are read-only), logic is duplicated between importers, placement and rotation are wrong for some models, and map data cannot be created reliably. The importer must cover the whole game, so it needs one dependable foundation: shared conversion rules, predictable outputs, repeatable runs.

## 2. Goals

1. Any supported Ragnarok file can be converted on request, from code or from the editor, by its Ragnarok path.
2. Outputs are ordinary, editable project assets in predictable places.
3. Re-running a conversion never duplicates or breaks anything that references the outputs.
4. One shared definition of units, axes, textures and materials is used by every converter.
5. The whole game can be converted in one batch.

## 3. Non-goals

- Importing at runtime or in player builds (editor only).
- Reading the GRF archive itself (files come from a folder on disk).
- Converting audio, effects (EFST/STR) or UI assets (future specs).
- Individual format rules (see 0003 to 0007).

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Source files are read from a configured folder on disk. A file is found by its Ragnarok path regardless of letter case, slash direction or Korean folder names. | Requesting `data\texture\...` and `DATA/Texture/...` for the same file returns the same file. A texture in a Korean-named folder is found. |
| FR-2 | M | Every conversion can be started from code by Ragnarok path without any UI, and returns the resulting asset(s) or a clear failure. The editor offers the same conversions from a menu, for one item and for a batch. | A test script converts a model and a map by path. The menu converts a selected list. |
| FR-3 | M | Converting a file never happens implicitly (copying a file or opening the project triggers nothing). | Placing a source file in the project creates no generated asset. |
| FR-4 | M | Outputs (meshes, materials, textures, prefabs, scenes, data) are standalone project assets, never read-only parts of another asset, stored in three areas (data assets in the project's data area, all visual assets in its imported-art area, and map scenes in the project's map scenes folder, where hand-made additive scenes also live) in a layout that mirrors the Ragnarok path. | A material of an imported model can be edited and saved. Two files with the same name in different Ragnarok folders do not collide. |
| FR-5 | M | Converting the same input twice is idempotent: no duplicates, and objects that reference an output (scenes, prefabs, data) keep their references. Unchanged inputs are skipped. | Converting a map twice leaves the same asset set, identities and references unchanged. The second run reports "skipped". |
| FR-6 | M | Dependencies are converted on demand. Asking for a map converts its models, textures and materials that are missing. | Converting a map in an empty output folder yields a complete, working map. |
| FR-7 | M | One shared coordinate and unit convention is used everywhere: Ragnarok content appears with correct handedness (not mirrored, not inside-out), correct up direction, and at a fixed scale where one ground cube is exactly one Unity unit wide. | A reference model and a reference map match the reference viewer side by side, including text on signs reading correctly. A ground cube measures 1 unit. |
| FR-8 | M | Textures: the Ragnarok transparency color becomes real transparency, with no colored fringe at edges when filtered or at distance. Texture settings depend on usage (model, ground, sprite/UI), not on file extension. Supported source formats are BMP, PNG, JPG and TGA; a texture in any other format gives one warning naming the file and its referrer, and the conversion continues with a visible placeholder. | A leaf texture shows no pink edge at close and far range. A sprite texture is not treated as a model texture and vice versa. A BMP, a PNG, a JPG and a TGA texture each convert. A texture with an unsupported extension produces one warning and a placeholder. |
| FR-9 | M | Materials are lit, shared and de-duplicated: the same texture and the same surface mode (opaque, cutout, two-sided) always resolve to one material. | 1000 models using one texture reference one material. A two-sided face renders from both sides. |
| FR-10 | M | A batch continues past failures, shows progress, and ends with a list of failed items and reasons. A missing source file or texture produces one warning naming the item and its referrer, and the conversion continues with a visible placeholder. | A batch with one corrupt file finishes the rest and lists the corrupt one once. A missing texture shows a placeholder, not a crash. |
| FR-11 | M | Informational and expected conditions are not logged as errors. | A clean batch produces no error log entries. |
| FR-12 | S | Hand-made changes placed in a separate layer (additional scene or variant) survive re-conversion of the generated content. | Re-converting a map keeps an additive scene with custom objects unchanged. |
| FR-13 | M | The conversion rules that do not need the editor (coordinate conversion, text and path rules, color keying, de-duplication) are testable by automated tests. | The tests run in batch mode. |
| FR-14 | M | Importer code and its data are editor-only and absent from player builds. | A build contains no importer code. |
| FR-15 | M | Keeping the source files inside the project does not make Unity import them on its own: copying or updating the full data set creates no Unity-generated assets, causes no long import, and adds nothing to builds. | Copying the full data set into the source folder starts no import of those files. A build contains no source file. |
| FR-16 | M | Generated files are deterministic: converting the same input with the same rules twice produces identical files, so version control shows a change only when an input or a rule changed. | Converting a map twice and diffing the two outputs shows no differences. |

## 5. Success criteria

- The whole game's models, textures and maps convert in one batch with a failure list that is empty or fully explained.
- A second full batch changes nothing.
- Every acceptance item in section 4 is covered by an automated test, except visual comparisons (manual, recorded with screenshots).

## 6. Assumptions and constraints

- Target pipeline is URP; materials use lit shading (decided).
- The source folder lives inside the Unity project, in its editor-only area (decided; the exact location is set in the plan). It holds the game's files already extracted (for example `model`, `texture`, `luafiles514` folders), with Korean folder names stored as normal Unicode names. Being inside the project means Unity sees these files, which FR-15 addresses.
- Source files are never modified.
- The source folder's `data` subfolder is the root of Ragnarok paths, as in the GRF editor (decided).
- Version control can hold the committed generated binary files; large-file storage is being enabled by the project owner (how it is configured is a plan matter).
- Generated content is committed to version control (decided). Data assets (such as map data) go to the project's data area, and imported art (models, meshes, materials, textures) to the imported-art area; the exact folders are set in the plan.
- Generated content is regenerated freely. People edit it only through FR-12.
- Unity APIs are used on the main thread only.
- Following CLAUDE.md, new third-party libraries need a plan entry. A format-reading library is already in the project and its use is decided in the plan.

## 7. Open questions

| # | Question | Blocks |
|---|---|---|
| OQ-1 | None open. Texture formats are BMP, PNG, JPG and TGA (decided; JPG added by the owner on 2026-11-18, after the game data turned out to contain JPG textures); others warn. |  |
