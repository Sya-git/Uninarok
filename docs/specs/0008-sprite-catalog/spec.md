# SPEC-0008: Sprite Catalog (units, equipment, items, effects)

| | |
|---|---|
| **Status** | Approved |
| **Created** | 2026-11-19 |
| **Owner** | Jhonnatan |
| **Depends on** | [SPEC-0007](../0007-spr-act-pal-sprites/spec.md) (converting one sprite) |
| **Reference** | [sprite-catalog.md](sprite-catalog.md) (which table maps what, where every category lives), [SpriteAction](../../data/SpriteAction.md) |
| **Plan** | [plan.md](plan.md) |
| **Tasks** | _(after approval)_ |

## 1. Why

The server talks in ids: a monster's job id, a player's class and sex, an equipment's item id or view id. The client's files are named in Korean, spread over folders by category, and linked to those ids through lub tables and the client's item table. Without a catalog the game cannot show what the server sends. The data is also very repetitive (60,438 garment SPR are 294 distinct sheets; one sheet serves every class while the animation differs per class), so the catalog must group by game object, not by file. The import must be runnable per category because a full run covers about 95,000 sprite files.

## 2. Goals

1. Find every sprite the game shows from what the server sends, with the client's fallbacks.
2. One catalog entry per game object (a monster, a garment, a hair style), not per file.
3. Import one category, several, or everything, with a clear report.

## 3. Non-goals

- Converting SPR/ACT/PAL (SPEC-0007).
- Composing a character (layer order, offsets, riding corrections), STR and hat effects, pet accessories, 3D (Granny) monsters, item names and descriptions in the UI, shadows: the tables are documented in the reference for later specs.

## 4. Requirements

Priority: **M** = must, **S** = should.

| ID | Pri | Requirement | Acceptance (observable) |
|---|---|---|---|
| FR-1 | M | Monsters, NPCs, homunculi and mercenaries: one entry per job id with its sprite and the display scale from the client's monster table. The folder follows the client's id ranges, not the presence of a file. | Poring (1002) returns its sprite at scale 1; an id whose name exists in both the NPC and monster folders returns the one its id range selects. |
| FR-2 | M | Player bodies: one entry per class, with per sex, race (human, doram) and variant (mount, outfit, costume style) sprites and the class's cloth palettes. | Novice male, knight on peco female and summoner male return their bodies; a cloth palette applies. |
| FR-3 | M | Heads: one entry per hair style, per sex and race, with its hair palettes. | Style 1 male returns its head; a hair palette applies. |
| FR-4 | M | Equipment: one entry per visible equipment (headgear, garment, weapon with its trail, shield), found by view id and by item id. An entry holds the image sheet for common classes, the sheet for doram, optional per-class sheets, and the animations per class and sex. Asking for a class and sex returns that class's sheet if any, else the doram or common sheet, together with that class's animation; a missing class animation falls back as the client does (weapon: specific type → base type). Equipment never falls back to another action: an empty or missing action draws nothing (a weapon is hidden while walking). Units (monsters, NPCs) fall back to idle. | A garment used by 178 class files stores its common and doram sheets once and returns the right animation for a knight and for a summoner; item 1201 (Faca) returns the short-sword weapon for a swordman. |
| FR-5 | M | Ground items: found by item id (identified and unidentified) and by resource name; items sharing a resource share one entry. | Item 501 shows the red potion on the ground. |
| FR-6 | M | Simple effects: emotes by emote id; other effect sprites (carts, falcon, status, numbers) by name. | Emote 0 plays "surprise"; cart 1 shows. |
| FR-7 | M | Skill icons: found by skill id; static (one frame). | Skill 5 (Bash) shows its icon. |
| FR-8 | M | Organised output: entries are grouped in one folder per category (monsters, NPCs, homunculi, bodies, heads, headgears, garments, weapons, shields, ground items, skill icons, effects, palettes), with shared image sheets kept apart from entries. | The project shows one folder per category; no image sheet is duplicated. |
| FR-9 | M | Import by category: the import menu offers each category on its own, any selection of categories, and everything. A run imports only what it needs from SPEC-0007 (shared sheets converted once even across categories) and reports per category: entries, sheets, actions, warnings, time. | Importing only monsters touches no equipment; importing equipment after monsters reuses no stale data and re-imports nothing already current. |
| FR-10 | M | Gaps are warnings, not failures: a table name without a file, a file no table references, an orphan ACT or SPR, a stray legacy folder each give one warning; the rest continues. | A full run completes and lists each gap once, grouped by category. |
| FR-11 | S | Lookup preview: in the editor, enter an id (job id, item id, view id + class + sex) and see the resolved entry play. | Entering item 2220 with knight male shows the hat on its own. |
| FR-12 | M | Item table choice: a project setting selects which language's item table feeds ground items and equipment lookups (Portuguese, English or Spanish). Portuguese is the default. | With the setting on English, item 501 resolves from the English table; a fresh project uses Portuguese. |

## 5. Success criteria

- The reference lookups (FR-1 to FR-6 acceptance) resolve to the files the original client uses.
- A full import completes; per category it records entries, distinct sheets, stored actions, warnings and time, and texture memory is within a target the owner sets from these numbers.

## 6. Assumptions and constraints

- Ids and names come from the GRF's lub tables and the client's `System` item table, read through the GRF library; the reference document lists every table used.
- The item table (`itemInfo`) comes in three languages; only its resource names and view ids are used here. Portuguese is the default, chosen by a project setting (FR-12).
- For weapons and shields the view id wins, then the item id, then the base type (OQ-3). This is recorded as Candidate until checked against the server's look packets.
- The sprite direction is the network direction (`UninarokDirection`).

## 7. Open questions

All resolved on 2026-11-20.

| # | Question | Decision |
|---|---|---|
| OQ-1 | Class → body file name and shield view id → name are not in any lub. | Keep our own small tables in the shared data docs, built from folder names and `itemInfo`, reviewed by the owner. |
| OQ-2 | Names for the extra animations of monsters with 48–72 actions. | `Extra5`, `Extra6`, … |
| OQ-3 | Does the item id win over the view id for weapon/shield files named by item id? | View id first, then item id, then base type. |
| OQ-4 | Which item table? | A project setting chooses the language; Portuguese by default (FR-12). |
| OQ-5 | Effect sprites in scope. | The whole effect folder, by name. |
| OQ-6 | Stray or legacy files. | Ignored, one warning each. |
