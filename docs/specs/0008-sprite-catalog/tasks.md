# TASKS-0008: Sprite Catalog (units, equipment, items, effects)

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved (owner, 2026-11-20) |

**How to use this file**
- Start only when SPEC-0007 is implemented (it is) and its converter entry points are available (`RoImportContext.Sprite(act, spr, options)`, `RoPaletteConverter`). Work on branch `feature/0008-sprite-catalog`.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0008): T3.4 add the SpriteCatalog facade` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence.
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Test command (batch): `Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml -quit`. Tests marked `NeedsData` need the source data folder and are ignored with a message when it is absent. Through the Unity MCP, run tests in groups (BACKLOG: one full run exceeds the tool timeout).
- Paths are relative to `Uninarok-client/Assets/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a phase 0 check shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Phase 0 scripts are scratch code: delete them when the task is done. Production code never parses `.lub` bytes (GRF does) and never parses SPR, ACT or PAL bytes (PLAN-0002 §1).
- Reference documents are updated in the same commit as the code they describe: [sprite-catalog.md](sprite-catalog.md) and `docs/data/PlayerJob.md`.
- Importing a category from the real data writes many assets. Phase 7 runs are done by the owner or through the Unity MCP, one category at a time, and the generated assets are committed by the owner (not in a task commit).
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

Reference set (spec §4 acceptance), used by every check below: Poring (job id 1002); a unit name that exists in both `npc/` and `몬스터/` (chosen in T0.4); bodies of novice male, knight on peco female and summoner male; hair style 1 male with a hair palette; the garment `천사날개` (view id 1, 178 class files); item 1201 (Faca) for a swordman; item 501 on the ground; emote 0; cart 1 (`손수레1`); skill 5 (Bash); item 2220 with knight male.

---

## Phase 0: Verify before building

- [x] **T0.1** Add the package `com.unity.addressables` to `Uninarok-client/Packages/manifest.json`, with the newest version that supports Unity 6000.7.0b7. Record the version in `plan.md` §1 (D13). (FR-8)
  *Done when:* the project compiles with the package, no new warnings (Logs/Editor.log), and the version is in `plan.md`.
- [x] **T0.2** Addressables spike (plan §4.6, R4, R10). In a scratch editor script: make a folder entry over a scratch folder holding a `ScriptableObject` animation stand-in, a sheet PNG sliced into named sprites and a Texture2D; create a `SpriteAnimationReference`-like `AssetReferenceT<…>` and an `AssetReferenceSprite` (with a sub-asset name) **from GUIDs of assets that are addressable only through the folder entry**; load each in the editor (asset-database play mode) and in a play-mode test; check two animations in different groups share one sheet from the `sprite-sheets` group (one copy); measure the managed memory of one synthetic equipment entry with 245 `SpriteAnimationReference` slots and its two dictionaries (Profiler or `GC.GetTotalMemory`).
  *Done when:* the results (works / does not, the fallback chosen if not, the measured memory) are written in `plan.md` R4 and R10, the scratch assets and script are deleted.
- [ ] **T0.3** GRF `Lub` mapping check (plan §4.1, R3). In a scratch editor script, load every table file of plan §4.2 (including the three item tables and `monster_size_effect_new.lub`) and one plain-text `.lub` through GRF; print for each: decompile success, text length, the way the text encoding is set or must be handled (UTF-8 for the item tables, CP949 for the rest), a known Korean string decoded right, and what happens on garbage and empty input. Register `CodePagesEncodingProvider` before GRF's `EncodingService` is first touched.
  *Done when:* each file's encoding and the GRF members used are written in `plan.md` §4.1 (a mapping table like PLAN-0007 §4.1), every table of §4.2 decompiles, and the scratch script is deleted.
- [ ] **T0.4** Choose and record the reference set. With a scratch script over the source folders, list: a unit name present in both `npc/` and `몬스터/`, a garment whose class ACT has its own SPR and one that uses the root sheet, a weapon with an item-id file, a shield with an item-id file, and the counts of `JobNameTable`, `SKID`, `Weapon_IDs`, items per language. Write them at the head of this file and in the reference tests' constants.
  *Done when:* the set and counts are written here and in `plan.md` §6, and the scratch script is deleted.

## Phase 1: Shared enum

- [ ] **T1.1** Add `PlayerJob : short` (hand-written) in `Scripts/Networking/Runtime/Data/PlayerJob.cs`, namespace `Uninarok.Networking.Data`, beside `AccountSex`; write `docs/data/PlayerJob.md` in the format of `docs/data/AccountSex.md` (kind, underlying type, source of the values = Hercules `src/common/class.h` and `class_hidden.h`, one table row per member). (FR-2)
  *Done when:* the enum has all 154 members with the Hercules ids (a scratch count against both `.h` files), the doc table lists every member, the project compiles with no new warnings.

## Phase 2: Reading the tables (Core, pure)

- [ ] **T2.1** Write `RoLuaReaderTests` (plan §6): tables, nested tables, `[ref.Name] = "x"` keys, negative numbers, string escapes, references to imports and earlier globals, `function … end` and `main()` skipped, `SkipField`, unknown construct → rejected with a line number. Marked `[Ignore("pending T2.2")]`. (FR-1 to FR-7)
  *Done when:* the tests compile and are listed as ignored.
- [ ] **T2.2** Implement `RoLuaLexer`, `RoLuaReader`, `RoLuaValue`, `RoLuaOptions`, `RoLuaChunk` in `Editor/RoImport/Core/Catalog/Lua/`; remove the ignore. (FR-1 to FR-7)
  *Done when:* `RoLuaReaderTests` pass (command + result reported); the reader parses the decompiled item table of T0.3 in under 10 seconds and keeps only the three fields asked for (reported).
- [ ] **T2.3** Implement `RoLubReader` (GRF adapter, plan §4.1) and `RoLubReaderTests` (`NeedsData`): one bytecode file and one plain-text file with a known Korean string in each encoding; garbage and empty input return false and never throw. (FR-1 to FR-7)
  *Done when:* the tests pass; `RoArchitectureTests` still pass (only the adapter touches GRF).
- [ ] **T2.4** Implement `RoJobTables` (`jobtbl` from `npcidentity.lub`, `JTtbl` from `jobidentity.lub`, `JobNameTable` from `jobname.lub`, read in dependency order) with tests (`NeedsData`: `JobNameTable` has 5,300 entries, Poring 1002 → `PORING`; a synthetic case for the import order). (FR-1, FR-2)
  *Done when:* the tests pass and report the counts.
- [ ] **T2.5** Implement `RoViewIdTables` (`ACCESSORY_IDs`, `AccNameTable`, `SPRITE_ROBE_IDs`, `RobeNameTable`, `Weapon_IDs`, `WeaponNameTable`, `Expansion_Weapon_IDs`) with tests (`NeedsData`: headgear 1 → goggles stem, garment 1 → `천사날개`, a specific weapon id → its base type). (FR-4)
  *Done when:* the tests pass and report the counts (headgear ids 2,859; garment ids 331).
- [ ] **T2.6** Implement `RoItemTable` (`SkipField` keeps `identifiedResourceName`, `unidentifiedResourceName`, `ClassNum`) and the enum `RoItemTableLanguage` (in Core, so the reader can take it; the settings of T5.1 use it; note this placement under `plan.md` §4.8) with tests (`NeedsData`: Portuguese 17,482 items, English 17,906; item 501 → `빨간포션`, 1201 → ClassNum 1, 2220 → ClassNum 16). (FR-4, FR-5, FR-12)
  *Done when:* the tests pass; parse time and managed memory of the Portuguese table are reported.
- [ ] **T2.7** Implement `RoEmoteTable` (`ET_*` ids), `RoSkillTable` (`SKID` constants → ids, lower-case icon names) and `RoMonsterScaleTable` (`MonsterSize`, default 1) with tests (`NeedsData`: `ET_SURPRISE` = 0; `SM_BASH` = 5 → `sm_bash`; a scale of 1.2 exists). (FR-1, FR-6, FR-7)
  *Done when:* the tests pass.
- [ ] **T2.8** Own tables (OQ-1): write a first draft of `Editor/RoImport/Catalog/Data/class-bodies.csv` (`BodyName, Job, Race, Kind, BaseJob`, `Job` and `BaseJob` as `PlayerJob` names) from the body folder names and the enum, and `shield-names.csv` (`ViewId, Name`); implement `RoOwnTables` and its tests; add §5 "Own tables" to `sprite-catalog.md` describing both files. (FR-2, FR-4)
  *Done when:* every body file of `인간족/몸통` and `도람족/몸통` matches a row or is listed as unmatched in the commit message, every `Job` parses as `PlayerJob`, the tests pass, and **the owner has reviewed the CSV** (the commit message says so).
- [ ] **T2.9** Implement `RoCatalogTables.Load` with `RoCatalogNeeds` (plan §4.2): loads only what the chosen categories need, reads the `System` folder through a second `IRoSource`, and reports a missing or unreadable table once. Add `RoCatalogTablesTests` and `PlayerJobTests` (`NeedsData`: every `PlayerJob` member exists in `JTtbl` with the same id; every job name in the CSV parses; `PlayerJob.md` lists every member). (FR-1 to FR-7, FR-10, FR-12)
  *Done when:* the tests pass; a run with `Needs = Jobs` reads no item table (asserted); a deleted table gives one issue and the dependent categories skipped (test).

## Phase 3: Runtime assembly `Uninarok.SpriteCatalog`

- [ ] **T3.1** Create `Scripts/SpriteCatalog/Uninarok.SpriteCatalog.asmdef` (references `Uninarok.Sprites`, `Uninarok.Networking`, `Unity.Addressables`, `Unity.ResourceManager`) and the types without logic: `SpriteCatalogCategory`, `SpriteRace`, the typed references (`SpriteAnimationReference`, `PaletteReference`, `BodyEntryReference`, `HeadEntryReference`, `EquipmentEntryReference`), `SpriteRef`, `UnitRecord`, `PaletteRef`, `PaletteSet`, `EquipmentKind`. Add the new asmdefs to `RoArchitectureTests` (the runtime assembly must not reference the importer; Core must not reference `UnityEditor`). (FR-8)
  *Done when:* the project compiles with no new warnings and `RoArchitectureTests` pass with the new checks.
- [ ] **T3.2** Create the test assembly `Uninarok.SpriteCatalog.Tests` and write `EquipmentEntryTests`, `BodyEntryTests`, `HeadEntryTests` and `SpriteCatalogTests` (plan §6, hand-built entries and in-memory indices): exact (job, sex) slot beats any-class doram beats any-class human; `AnyClass` does not collide with `JOB_NOVICE`; a missing action draws nothing; a unit falls back; item id → kind and view id; ground slot; emote action = id; a miss returns false; lookups after load allocate nothing; the entry dictionaries are built once. Marked `[Ignore("pending T3.3")]` / `T3.4`. (FR-1 to FR-7)
  *Done when:* the tests compile and are listed as ignored.
- [ ] **T3.3** Implement `BodyEntry`, `HeadEntry`, `EquipmentEntry` (slots hold `SpriteAnimationReference` only; `TryResolve` with the lazily built dictionaries) and `PaletteSet`; remove the matching ignores. (FR-2, FR-3, FR-4)
  *Done when:* the entry tests pass (command + result reported).
- [ ] **T3.4** Implement `UnitIndex`, `BodyIndex`, `HeadIndex`, `EquipmentIndex`, `ItemSpriteTable`, `GroundItemIndex`, `SkillIconIndex`, `EffectIndex`, `ClassTable`, `SpriteCatalogKeys` and the `SpriteCatalog` facade (dictionaries built once at load, plan §4.5); remove the remaining ignores. (FR-1 to FR-7)
  *Done when:* `SpriteCatalogTests` pass; the zero-allocation lookup test passes.

## Phase 4: Planning (Core, pure)

- [ ] **T4.1** Add the plan types (`RoCatalogPlan` and its builder, `RoCatalogRecord`, `RoCatalogPair`, `RoCatalogValue`, `RoCatalogGap`, `IRoCatalogPlanner`, `RoCatalogContext`, `RoFileNames`), the fingerprint (SHA-256 over the canonical record text) and the five `RoIssueCode` values of plan §4.3 with tests (a gap is reported once per `(code, item)`; the fingerprint changes with any value or pair; records sorted by `(category, key)`). (FR-10)
  *Done when:* the tests pass.
- [ ] **T4.2** Write `RoUnitPlannerTests`, `RoStaticPlannerTests` (synthetic tables and file lists, plan §6): id ranges choose the folder; a name in both folders resolves by range; a `.gr2` or missing name → one warning; an ACT without SPR → orphan; scale default 1; items sharing a resource share one record; identified vs unidentified; skill icon by lower-case `SKID`; effects by name; the emote set. Marked `[Ignore("pending T4.3")]`. (FR-1, FR-5, FR-6, FR-7, FR-10)
  *Done when:* the tests compile and are listed as ignored.
- [ ] **T4.3** Implement `RoUnitFolders` and the unit planners (monsters, NPCs, homunculi). (FR-1, FR-10)
  *Done when:* the unit tests pass; a `NeedsData` run over the real data reports the counts of plan §4.3.1 (records, names without a file, orphan ACTs).
- [ ] **T4.4** Implement the planners of ground items and skill icons (one scan of `아이템/` shared). (FR-5, FR-7, FR-10)
  *Done when:* the static tests for these two pass; the real-data run reports records and gaps (17,079 of 17,482 Portuguese items have a ground sprite; 1,295 of 1,806 `SKID` entries have an icon).
- [ ] **T4.5** Implement the effects planner (whole `이팩트/` by name, emote set). (FR-6, FR-10)
  *Done when:* the remaining static tests pass; the real-data run reports the record count.
- [ ] **T4.6** Write `RoBodyPlannerTests`, `RoHeadPlannerTests`, `RoEquipmentPlannerTests`: class table rows → variants (mount, outfit, costume, doram); palettes by colour number; stray names ignored with one warning; headgear sex and doram slots; garment root sheet vs own SPR vs `_doram`; weapon suffix → item-id → base type order (OQ-3); trail pairs; shield names and item-id records; unreferenced files reported. Marked `[Ignore("pending T4.7")]` and so on. (FR-2, FR-3, FR-4, FR-10)
  *Done when:* the tests compile and are listed as ignored.
- [ ] **T4.7** Implement the body and head planners (palettes of their own records only). (FR-2, FR-3, FR-10)
  *Done when:* the body and head tests pass; the real-data run reports records, palettes and stray files.
- [ ] **T4.8** Implement the headgear planner. (FR-4, FR-10)
  *Done when:* the headgear tests pass; the real-data run reports 2,266 of 2,859 ids with a file and 593 warnings (plan §4.3.4).
- [ ] **T4.9** Implement the garment planner (root sheet or doram sheet unless the job has its own SPR). (FR-4, FR-10)
  *Done when:* the garment tests pass; on `천사날개` the plan has the pairs of every class file and the root sheet for those without their own SPR (reported).
- [ ] **T4.10** Implement the weapon planner (view-id records, item-id records, trails, fallback to the base type). (FR-4, FR-10)
  *Done when:* the weapon tests pass; item 1201 plans the short-sword weapon for a swordman; the item-id file of T0.4 gets its own record (reported).
- [ ] **T4.11** Implement the shield planner. (FR-4, FR-10)
  *Done when:* the shield tests pass; the four basic shields and the item-id file of T0.4 are planned (reported).
- [ ] **T4.12** Implement the palettes planner (every `.pal` under `palette/`). (FR-2, FR-3, FR-9)
  *Done when:* the real-data run plans 4,464 palettes (749 + 3,375 + 340) with no orphan (reported).

## Phase 5: Import (Editor)

- [ ] **T5.1** Implement `RoImportSettings` (`ScriptableSingleton`, `ProjectSettings/UninarokRoImport.asset`), `RoImportSettingsProvider` (`Project/Uninarok/Ragnarok Import`), `ItemTableFile(language)`, the `RoImportOptions.ItemTableLanguage` override and `RoImportSettingsTests`. (FR-12)
  *Done when:* the tests pass (default Portuguese, each language maps to its file, the override beats the setting); the settings page shows the dropdown (screenshot or owner check).
- [ ] **T5.2** Pipeline hooks: `RoJobKind.CatalogEntry` and `CatalogIndex`, `RoImportOptions.SystemSource` (default: `System` next to the data root) and the internal `CatalogPlan`, `RoImportContext.CatalogPlan`, `RoConverterRegistry` entries (stub converters), `RoLedger` category `catalog`. (FR-9)
  *Done when:* the existing importer tests still pass (groups reported); a job of the new kinds reaches its stub converter (test).
- [ ] **T5.3** Implement `RoCatalogOutputPaths` (entry, index, tables, the 12 folders) and add `Art/Imported/catalog/` to the reserved generated paths, with tests. (FR-8)
  *Done when:* the tests pass: the paths of plan §3, collision and reserved-path rules apply.
- [ ] **T5.4** Implement `RoCatalogAddressables` (`Ensure`, `Verify`; groups and folder entries of plan §4.6) with `RoCatalogAddressablesTests` (idempotent; every reference in a synthetic index or entry resolves inside a folder entry; the sprite reference carries the sheet GUID and the sub-asset name; a dangling reference is one warning). (FR-8)
  *Done when:* the tests pass; the Addressables settings show the groups once after two runs (reported).
- [ ] **T5.5** Write `RoCatalogImporterTests` on a synthetic source (plan §6): one category imports only its own sheets; the second run is `Skipped`; a second category reuses a shared sheet; a changed table or setting changes only the dependent fingerprints; a failing pair fails one record only; the per-category report numbers (and the same on the second run); entries land in the §3 folders. Marked `[Ignore("pending T5.6")]`. (FR-8, FR-9, FR-10, FR-12)
  *Done when:* the tests compile and are listed as ignored.
- [ ] **T5.6** Implement `RoCatalogEntryConverter` for the categories without entry assets (units, ground items, skill icons, effects): ledger check with fingerprint and ACT/SPR/PAL dependencies, `ctx.Sprite(act, spr, options)` per pair, one issue and no partial record on a failed pair, the output paths remembered as strings for the index. (FR-1, FR-5, FR-6, FR-7, FR-9, FR-10)
  *Done when:* the entry-converter tests for these categories pass; running the same job twice gives `Skipped` the second time.
- [ ] **T5.7** Extend the entry converter and add `RoCatalogEntryWriter` for bodies, heads and equipment: writes `BodyEntry`, `HeadEntry`, `EquipmentEntry` in place (GUID kept) with typed references built from output paths (no asset loaded), requests the palettes of its own records, and saves the numbers (`StoredActions`, `SheetCount`) in the entry. (FR-2, FR-3, FR-4, FR-8, FR-9)
  *Done when:* the entry tests for these categories pass; a forced re-run keeps GUIDs and bytes.
- [ ] **T5.8** Implement `RoCatalogIndexConverter` and `RoCatalogIndexWriter` (indices of every category, `ItemSpriteTable`, `ClassTable`; references from output paths; rewritten only when the content changed; calls `Ensure`/`Verify`). (FR-1 to FR-8)
  *Done when:* the index tests pass; a second run rewrites no index (file times unchanged, reported).
- [ ] **T5.9** Implement `RoCatalogImporter`, `RoCatalogReport` and `RoCategoryReport`, and `RoImport.Catalog(categories, options)` / `CatalogAll`: tables for the union of needs, plan for the chosen categories, gaps reported once, the entry batch, then the index batch, the per-category numbers (entries, sheets, stored actions, warnings, time). Remove the remaining ignores. (FR-9, FR-10)
  *Done when:* all of `RoCatalogImporterTests` pass; importing only monsters on the synthetic source writes no equipment asset (asserted).
- [ ] **T5.10** Menu and window: `Tools/Ragnarok/Import Catalog…` (`RoCatalogWindow`: a checkbox per category, "All", Force, Import, the per-category result table) and `Tools/Ragnarok/Catalog/<category>` and `…/All`; all call `RoImport.Catalog`. (FR-9)
  *Done when:* each menu item appears and runs a category on the synthetic project data (screenshot or owner check); the window and the code path give the same report (test).

## Phase 6: Lookup preview and run-time check

- [ ] **T6.1** Implement `RoCatalogLookupWindow` (`Tools/Ragnarok/Catalog Lookup…`): mode popup, class, sex, race, Resolve through `SpriteCatalog`, the result opened in `RoSpritePreviewWindow` (a static result shows the sprite); a clear message when the catalog is not imported. (FR-11)
  *Done when:* on the Phase 7 data (or the T5.9 synthetic data), entering item 2220 with knight male shows the hat on its own (screenshot saved as evidence).
- [ ] **T6.2** Add the play-mode assembly `Uninarok.SpriteCatalog.PlayTests` and `SpriteCatalogPlayTests`: `LoadAsync(Monsters)` then Poring 1002 → `LoadAnimation` returns the animation; `Release()` leaves no open handles. (FR-1)
  *Done when:* the play-mode test passes (command + result reported).

## Phase 7: Data runs, reference checks and close

- [ ] **T7.1** Write `RoCatalogReferenceTests` (`NeedsData`, plan §6): the acceptance lookups of spec §4 and the reference set above; a garment with 178 class files has distinct sheet paths = distinct SPR hashes and returns the right animation for a knight and a summoner; item 501 → red potion; emote 0; cart by name; skill 5; item 2220; a full run lists each gap once. Marked `[Ignore("pending T7.2")]`. (FR-1 to FR-7, FR-10)
  *Done when:* the tests compile and are listed as ignored.
- [ ] **T7.2** Import the small categories from the real data, one at a time, in this order: units (monsters, NPCs, homunculi), ground items, skill icons, effects, headgears, shields. Record per category entries, distinct sheets, stored actions, warnings and time in `plan.md` §9 (R7). Remove the ignore from the reference tests of these categories. (FR-1, FR-4 to FR-10)
  *Done when:* the run completes with no failure; the second run of each category is all `Skipped` (reported); the reference tests for these categories pass.
- [ ] **T7.3** Import bodies, heads, palettes and weapons the same way; record the numbers. Remove the matching ignores. (FR-2, FR-3, FR-4, FR-9)
  *Done when:* no failure; the second run is all `Skipped`; the body, head and weapon reference tests pass.
- [ ] **T7.4** Import garments last, in one run (resumable): record entries, distinct sheets (expected 294 for the full folder), stored actions, asset count and time; record the texture memory and the Addressables editor behaviour with the full asset count (R1, R9). Remove the remaining ignores. (FR-4, FR-8, FR-9)
  *Done when:* no failure; distinct sheet paths equal distinct SPR hashes; the second run is all `Skipped`; the numbers are in `plan.md` and the owner has the data to set the texture-memory target (spec §5).
- [ ] **T7.5** Manual checks (plan §6): each acceptance lookup through `RoCatalogLookupWindow` compared by the owner with the reference viewer (poring, the three bodies, hair 1 with a palette, a garment on a knight and a summoner, the weapon of item 1201 with its trail, a hidden weapon while walking, item 501, emote 0, cart 1, skill 5, item 2220); the project window shows one folder per category. Save images or recordings as evidence; update the Candidate marks in `sprite-catalog.md` (emote index, item-id order) to Confirmed or Open. (FR-1 to FR-8, FR-11)
  *Done when:* the owner confirms each lookup (or each difference is recorded in `plan.md` Decisions or `BACKLOG.md`).
- [ ] **T7.6** Close: verify the success criteria of spec §5 (lookups resolve to the files the original client uses; full import with per-category numbers recorded; texture memory target set by the owner), fill the traceability table of `plan.md` §10 with the passing tests and recorded evidence, set the statuses (spec and plan `Implemented`, this file `Implemented`), move leftovers to `docs/specs/BACKLOG.md`. (FR-1 to FR-12)
  *Done when:* the full test groups pass (reported), every FR has evidence in `plan.md` §10, and the statuses are set.
