# PLAN-0008: Sprite Catalog (units, equipment, items, effects)

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0008, approved 2026-11-20) |
| **Status** | Approved (owner, 2026-11-20) |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Where each table, folder and file pattern lives comes from [sprite-catalog.md](sprite-catalog.md) (normative for its topic; updated in the same commit as the code that uses it). Converting one sprite (sheets, animations, palettes, the player) is [PLAN-0007](../0007-spr-act-pal-sprites/plan.md); shared import rules (paths, source, ledger, results, batch, menu) are [PLAN-0002](../0002-ro-import-foundation/plan.md). This plan adds the layer above them: **ids and names → game objects → the sprites SPEC-0007 already converts**.

## 1. Constraints and assumptions

- **One new package, no third-party library.** `com.unity.addressables` **4.2.0** is added to the project (the newest registry version on Unity 6000.7.0b7, checked in T0.1 on 2026-11-20; it resolves and compiles with no warnings; D13). Lub files are read through GRF (`Lub.Decompile()`, already in `GRF.dll`), which returns Lua source text. The data in these files is plain Lua table constructors, so a small reader of that subset (§4.1) is part of this plan.
- **Nothing here converts pixels or ACT bytes.** Every sheet, animation and palette comes from SPEC-0007's `RoSpriteConverter.Convert(ctx, act, spr, options)` and `RoPaletteConverter`. This plan decides *which ACT is paired with which SPR* and *which game object owns the result*.
- **Pure planning, editor writing.** A planner turns tables and the list of source files into a **plan** (plain data, no Unity types). The editor layer executes a plan. So the lookup rules (the hard part) are tested on synthetic tables and file lists, and the import is tested on a few real files.
- **Run time needs only assets and Addressables.** The lookups the game uses (job id, view id, item id, skill id, emote id, name) resolve from assets the import writes, with no lub, GRF or editor code (§4.5). Every reference inside catalog data is a typed Addressables reference (`AssetReferenceT<SpriteAnimation>`, `AssetReferenceSprite`, `AssetReferenceTexture2D`, §4.4), so loading an index or an entry never loads the sprites it names. Bundle layout, packing and remote hosting are the later loading-strategy spec; this plan only makes the data addressable (§4.6).
- **Decided by the owner (spec §7):** own small tables for class → body and shield view id → name (OQ-1); `Extra5`, `Extra6`, … for unnamed animations (OQ-2; SPEC-0007 stores animation numbers, not names, so this only fixes the wording of the doc, §4.4); weapon and shield lookup order **view id → item id → base type** (OQ-3); item table language from a project setting, Portuguese by default (OQ-4, FR-12); the whole effect folder by name (OQ-5); stray files ignored with one warning (OQ-6).
- Facts used (sprite-catalog.md): id ranges for the unit folders (§3.1), file patterns per category (§3.1–§3.10), `ClassNum` meanings (§2.1), the 29 distinct shield sheets and 294 garment sheets (the content index of SPEC-0007 shares them, so nothing here deduplicates pixels).
- Direction is `UninarokDirection` (SPEC-0007 plan §1); actions are addressed as `(animation, direction)` by `SpriteAnimation.TryGetAction`.
- Counts that size the work (sprite-catalog.md §3): about 95,000 sprite files, of which about 81,000 are garment ACTs. Everything runs per category (FR-9), and the garment category is meant to run last, on purpose, and only once.

## 2. Architecture

Satisfies FR-1 to FR-12.

```
 System/*.lub, data/luafiles514/**/*.lub                          data/sprite/**, data/palette/**
        │  IRoSource (data) + IRoSource (System)                              │  IRoSource.Enumerate
        ▼                                                                      │
  RoLubReader (GRF adapter) ──► text ──► RoLuaReader (pure, Lua subset) ──► RoLuaValue     Core/Formats, Core/Catalog/Lua
        │                                                                      │
        ▼                                                                      │
  RoCatalogTables (jobs, names, view ids, items, emotes, skills, scales)      │          Core/Catalog/Tables
  + own tables (class-bodies.csv, shield-names.csv)                           │
        └──────────────────────────────┬───────────────────────────────────────┘
                                       ▼
        IRoCatalogPlanner × 12  (monsters, npcs, homunculi, bodies, heads, headgears,
                                 garments, weapons, shields, ground items, skill icons, effects)
                                       ▼
        RoCatalogPlan  = records and entries (key, file pairs, values, fingerprint) + gaps (warnings)   Core/Catalog/Plan
                                       ▼  (editor)
   RoCatalogImporter: plan → jobs → RoBatch ──► RoCatalogEntryConverter ──► ctx.Sprite(act, spr, …)   [SPEC-0007]
                                       │                                  ► RoPaletteConverter       [SPEC-0007]
                                       ▼
        entry assets (bodies, heads, equipment)  +  category index assets  +  Addressables groups (folder entries)
                                       ▼
   runtime (Uninarok.SpriteCatalog): SpriteCatalog.LoadAsync → TryGetUnit / TryFindBody / TryFindHead / TryFindEquipment /
                                     TryGetGroundItem / TryGetSkillIcon / TryGetEmote / TryGetEffect → Load...(handle)
```

Data flow of `RoImport.Catalog(SpriteCatalogCategory.Monsters)`:

1. **Tables.** Load only the tables the chosen categories need (§4.2), once per run, memoised. A table that cannot be read is one `CorruptFile`/`SourceMissing` issue and the categories that need it are skipped; the others continue.
2. **Plan.** Each chosen planner enumerates its source folders and joins them with the tables. The plan lists every record to import and every gap (FR-10). Planning does no conversion and writes nothing.
3. **Jobs.** One `RoJobKind.CatalogEntry` job per record (path `catalog/<category>/<key>`). Order is deterministic (`RoBatch.Order`). Progress, cancel, 200-job checkpoint, ledger and error handling are `RoBatch`'s (PLAN-0002 §4.10).
4. **Record job.** Check the catalog ledger (fingerprint of the planned record plus its ACT/SPR hashes). Current → `Skipped`. Otherwise ask SPEC-0007 for each ACT/SPR pair (sheets shared and ledger-skipped there), write the entry asset when the category has entry assets, record the ledger.
5. **Index and addressing.** After the batch, a second small batch (one job per category, kind `CatalogIndex`) builds the category's index asset from the plan and the output paths of the converted records. Before it, `RoCatalogAddressables.Ensure()` makes sure the groups and folder entries of §4.6 exist. The index is rewritten only when its content changed.
6. **Report.** `RoCatalogReport`: per category entries, distinct sheets, stored actions, warnings, time (FR-9).

## 3. Assemblies and file layout

Satisfies FR-1 to FR-12.

| Path | Contents | Assembly |
|---|---|---|
| `Assets/Scripts/SpriteCatalog/Uninarok.SpriteCatalog.asmdef` | runtime assembly, references `Uninarok.Sprites`, `Uninarok.Networking` (for `AccountSex` and `PlayerJob`), `Unity.Addressables` and `Unity.ResourceManager` | `Uninarok.SpriteCatalog` |
| `Assets/Scripts/SpriteCatalog/` `SpriteRef.cs`, `SpriteAnimationReference.cs`, `PaletteReference.cs`, `BodyEntryReference.cs`, `HeadEntryReference.cs`, `EquipmentEntryReference.cs`, `SpriteRace.cs`, `UnitRecord.cs`, `UnitIndex.cs`, `BodyEntry.cs`, `BodyIndex.cs`, `HeadEntry.cs`, `HeadIndex.cs`, `EquipmentEntry.cs`, `EquipmentIndex.cs`, `ItemSpriteTable.cs`, `GroundItemIndex.cs`, `SkillIconIndex.cs`, `EffectIndex.cs`, `ClassTable.cs`, `PaletteSet.cs`, `SpriteCatalogCategory.cs`, `SpriteCatalogKeys.cs`, `SpriteCatalog.cs` | data types, indices and the lookup facade (§4.4, §4.5) | `Uninarok.SpriteCatalog` |
| `Assets/Scripts/Networking/Runtime/Data/PlayerJob.cs` and `docs/data/PlayerJob.md` | the shared enum of player classes, hand-written (§4.4.1), beside `AccountSex` | `Uninarok.Networking` |
| `Assets/Editor/RoImport/Core/Formats/RoLubReader.cs` | GRF adapter for `.lub` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Catalog/Lua/` | `RoLuaLexer`, `RoLuaReader`, `RoLuaValue` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Catalog/Tables/` | `RoCatalogTables`, `RoJobTables`, `RoViewIdTables`, `RoItemTable`, `RoEmoteTable`, `RoSkillTable`, `RoMonsterScaleTable`, `RoOwnTables` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Catalog/Planning/` | `IRoCatalogPlanner`, one planner per category, `RoUnitFolders`, `RoCatalogPlan`, `RoCatalogRecord`, `RoCatalogGap`, `RoFileNames` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Catalog/Data/class-bodies.csv`, `shield-names.csv` | the owner-reviewed own tables (OQ-1), read by `RoOwnTables` | data files |
| `Assets/Editor/RoImport/Editor/Catalog/` | `RoCatalogImporter`, `RoCatalogEntryConverter`, `RoCatalogIndexConverter`, `RoCatalogEntryWriter`, `RoCatalogIndexWriter`, `RoCatalogOutputPaths`, `RoCatalogAddressables`, `RoCatalogReport`, `RoImportSettings`, `RoImportSettingsProvider`, `RoCatalogWindow`, `RoCatalogLookupWindow` | `Uninarok.RoImport.Editor` (references `Uninarok.SpriteCatalog` and `Unity.Addressables.Editor`) |
| `Assets/Tests/EditMode/RoImport/Catalog/` | planner, table, Lua and importer tests | `Uninarok.RoImport.Tests` |
| `Assets/Tests/EditMode/SpriteCatalog/` | lookup tests on hand-built entries | new `Uninarok.SpriteCatalog.Tests` |
| `Assets/Tests/PlayMode/SpriteCatalog/` | one play-mode test that loads through Addressables | new `Uninarok.SpriteCatalog.PlayTests` |

Outputs (roots of PLAN-0002 §4.5; the animations and sheets stay where SPEC-0007 puts them, D4):

| Output | Path |
|---|---|
| Entry assets | `Art/Imported/catalog/<category>/<key>.asset` (bodies, heads, headgears, garments, weapons, shields) |
| Category index | `Art/Imported/catalog/<category>/_index.asset` (every category; for units, ground items, skill icons and effects it also holds the records) |
| Addressables settings | `Assets/AddressableAssetsData/` (created by the package; groups and folder entries of §4.6) |
| Item table, class table (§4.5) | `Art/Imported/catalog/items/_index.asset`, `Art/Imported/catalog/classes/_index.asset` |
| Sheets, animations, palettes | as SPEC-0007 (`sprite/_sheets/…`, `sprite/<folders>/<name>.asset`, `palette/<folders>/<name>.png`) |
| Ledger | `Assets/Editor/RoImport/Ledger/catalog.json` |

`<category>` is one of `monsters`, `npcs`, `homunculi`, `bodies`, `heads`, `headgears`, `garments`, `weapons`, `shields`, `ground-items`, `skill-icons`, `effects`; palettes stay in `palette/` (SPEC-0007's folder is already one per category). `<key>` is a stable ASCII name built from ids (`headgear-0016`, `garment-0001`, `body-0007`, `head-01`).

## 4. Component contracts

### 4.1 Reading lub files: `RoLubReader`, `RoLuaReader`

Satisfies FR-1 to FR-7, FR-10, FR-12.

```csharp
public static class RoLubReader
{
    // bytes of a .lub (bytecode or plain text); never throws
    public static bool TryRead(RoPath path, byte[] bytes, RoLubEncoding encoding, out string lua, out string reason);
}
public enum RoLubEncoding { Utf8, Cp949 }

public static class RoLuaReader
{
    public static bool TryRead(string lua, RoLuaOptions options, IReadOnlyDictionary<string, RoLuaValue> imports,
                               out RoLuaChunk chunk, out string reason);
}
public sealed class RoLuaOptions { public Func<string, bool> SkipField; }        // skips a field by name while parsing (item descriptions)
public sealed class RoLuaChunk { public IReadOnlyDictionary<string, RoLuaValue> Globals; }
public abstract class RoLuaValue { /* RoLuaNumber, RoLuaString, RoLuaTable (array part + keyed part) */ }
```

- **Adapter.** GRF decompiles bytecode (`ESC Lua`) to text; a file already in plain text (12 of 379) is decoded as given. The encoding is passed per file because the client's files differ (`iteminfo_new.lub` and its language copies are UTF-8; the `data/luafiles514` tables and older item tables are CP949). Code page 949 comes from Unity's Mono; `CodePagesEncodingProvider` is not available in Unity's API profile and is not needed (T2.3). The exact GRF members and the way the encoding is set are fixed by a **GRF mapping check** (TASKS phase 0), the same way PLAN-0007 §4.1 fixed the SPR/ACT/PAL mapping.
- **Reader scope: the subset these files use**, nothing more: global assignments (`Name = {…}`, `Name.Field = value`), table constructors (positional values, `name = value`, `[expr] = value`), numbers (including negative), strings (with escapes), `true`/`false`/`nil`, and **references** (`jobtbl.JT_PORING`, `SKID.SM_BASH`) resolved against `imports` and earlier globals of the same chunk. A bare name that is neither is `nil`, as in the client's Lua; a field of an unknown name is rejected (Lua raises an error there too); a positional `nil` leaves a hole (D17). `function … end` bodies and the loader calls after the data (`main()`, `AddItem(…)`) are **skipped** by balanced keyword counting; they are not evaluated. An unknown construct inside a table is a rejected file with the line number, not a silent miss.
- **Large tables.** The item table is 14 million characters; `SkipField` drops the description and display-name fields while parsing (only `identifiedResourceName`, `unidentifiedResourceName` and `ClassNum` are kept), so memory stays small.
- **Import order.** Some tables name constants of another (`jobname.lub` uses `jobtbl` from `npcidentity.lub`, `accname.lub` uses `ACCESSORY_IDs`). `RoCatalogTables` reads the defining file first and passes its globals as `imports` (§4.2).
- Adapters never throw (PLAN-0007 §4.1 rule); a rejected read is `false` with a reason, reported once as `CorruptFile` naming the file.

**GRF mapping check (TASKS T0.3, 2026-11-20).** GRF members used: `GRF.FileFormats.LubFormat.Lub.IsCompiled(byte[])`, `new Lub((GRF.MultiType)bytes)` and `Lub.Decompile()` (returns the Lua text). String constants are decoded with **`Utilities.Services.EncodingService.DisplayEncoding`**, so the adapter sets it for the call and restores the previous value in `finally`, as `RoActReader` does. `EncodingService.Korean` is code page 949 and the default display encoding is 1252, which would mis-decode every Korean string. **T2.3:** `CodePagesEncodingProvider` is not in Unity's API profile (CS0103 in `Uninarok.RoImport.Core`); Unity's Mono provides code page 949 by itself, so no provider is registered (the T0.3 scratch script compiled outside that profile).

| File | Bytes | Kind | Encoding | Text (chars) | Known string decoded | Global names (top-level constructs) |
|---|---|---|---|---|---|---|
| `datainfo/npcidentity.lub` | 290,337 | bytecode | CP949 (ASCII only) | 139,674 | `JT_PORING = 1002` | `jobtbl = { NAME = n, … }` |
| `datainfo/jobidentity.lub` | 228,752 | bytecode | CP949 (ASCII only) | 108,166 | `JT_KNIGHT = 7` | `JTtbl = { … }` |
| `datainfo/jobname.lub` | 377,295 | bytecode | CP949 | 225,809 | `PORING` (167 Hangul; UTF-8 gives 30, wrong) | `JobNameTable = { [jobtbl.JT_X] = "NAME", … }` |
| `datainfo/accessoryid.lub` | 178,836 | bytecode | CP949 (ASCII only) | 99,525 | `ACCESSORY_GOGGLES = 1` | `ACCESSORY_IDs = { … }` |
| `datainfo/accname.lub` | 256,623 | bytecode | CP949 | 172,864 | `_고글` (UTF-8: not found) | `AccNameTable = { [ACCESSORY_IDs.X] = "_stem", … }` |
| `datainfo/spriterobeid.lub` | 17,897 | bytecode | CP949 (ASCII only) | 10,059 | `ROBE_WINGS = 1` | `SPRITE_ROBE_IDs = { … }` |
| `datainfo/spriterobename.lub` | 44,152 | bytecode | CP949 | 50,996 | `천사날개` (UTF-8: not found) | `RobeNameTable`, `RobeNameTable_Eng`, `RobeTopLayer` (positional list of references) |
| `datainfo/weapontable.lub` | 13,007 | bytecode | CP949 | 14,638 | `_검` (UTF-8: not found) | `Weapon_IDs`, `WeaponNameTable`, `Expansion_Weapon_IDs = { [Weapon_IDs.A] = Weapon_IDs.B }`, `WeaponHitWaveNameTable`, `BowTypeList` |
| `emotion/emotionlist.lub` | 18,050 | bytecode | CP949 | 17,346 | `ET_SURPRISE = 0` | top-level `ET_X = n`, `MSI_EMOTION_X = n`, `EMOTION_ORDERLIST = {…}`, `EmotionList = {}`, then calls `InsertEmotionListTable(…)`, `InsertEmotionMsgListTable(…)` (skipped) |
| `skillinfoz/skillid.lub` | 96,570 | bytecode | CP949 (ASCII only) | 44,511 | `SM_BASH = 5` | `SKID = { … }` |
| `System/monster_size_effect_new.lub` | 126,556 | bytecode | CP949 (ASCII only) | 75,523 | `MonsterSize = 1.2` (408 entries of 1.x) | `EFFECT = {}`, dotted `EFFECT.EF_X = n`, `tbl = { [id] = { MonsterSize = f, MonsterEff = { EFFECT.EF_X } } }`, `main = function() … end` |
| `System/iteminfo_new.lub` (Portuguese) | 7,308,863 | bytecode | **UTF-8** | 14,179,615 | `빨간포션` (CP949: not found) | `tbl = { [id] = { …DisplayName, …ResourceName, …DescriptionName = {…}, slotCount, ClassNum, costume } }` |
| `System/english/iteminfo_new.lub` | 7,667,154 | bytecode | **UTF-8** | 14,314,903 | `빨간포션` | as above |
| `System/spanish/iteminfo_new.lub` | 7,851,144 | bytecode | **UTF-8** | 14,697,567 | `빨간포션` | as above |
| `quest/localquest/local_dewataquest_list.lub` (one of the 12 plain-text files) | 3,879 | plain text (`IsCompiled` false; `Decompile` throws `GrfException: Unrecognized file format`) | CP949 bytes (strict UTF-8 rejects them) | — | `데와타` | uses long strings `[[…]]`; no plain-text file is a table of §4.2 |

- **Decompile time:** under 0.1 s for each `data/` table; about 1.6–1.8 s for each item table (GRF only, before our reader).
- **Bad input:** empty → `IndexOutOfRangeException`; 256 bytes of garbage → `GrfException: Unrecognized file format`; a bytecode file cut to 100 bytes → `IndexOutOfRangeException`. All are caught by the adapter and become `false` with the exception text as reason; `IsCompiled` is false for garbage and is checked first so plain-text files take the text path.
- **Consequences for the reader:** the item table global is **`tbl`** (not `itemInfo`); top-level dotted assignments (`EFFECT.EF_X = n`), function assignments (`main = function() … end`) and bare calls (`InsertEmotionListTable(…)`) occur and are skipped or applied as §4.1 says; `[[…]]` long strings occur only in plain-text files outside §4.2, so the reader rejects them with a line number rather than supporting them.

### 4.2 Tables: `RoCatalogTables`

Satisfies FR-1 to FR-7, FR-12.

```csharp
public sealed class RoCatalogTables
{
    public static RoCatalogTables Load(IRoSource data, IRoSource system, RoCatalogNeeds needs, RoItemTableLanguage language, RoReport report);
    public RoJobTables Jobs { get; }                // jobtbl, JTtbl, JobNameTable
    public RoViewIdTables ViewIds { get; }          // ACCESSORY_IDs, AccNameTable, SPRITE_ROBE_IDs, RobeNameTable, Weapon_IDs, WeaponNameTable, Expansion_Weapon_IDs
    public RoItemTable Items { get; }               // itemInfo (language of the setting)
    public RoEmoteTable Emotes { get; }             // ET_* ids
    public RoSkillTable Skills { get; }             // SKID constants -> skill id
    public RoMonsterScaleTable Scales { get; }      // monster_size_effect_new.lub
    public RoOwnTables Own { get; }                 // class-bodies.csv, shield-names.csv
}
[Flags] public enum RoCatalogNeeds { None = 0, Jobs = 1, ViewIds = 2, Items = 4, Emotes = 8, Skills = 16, Scales = 32, Own = 64 }
```

| Table | File (data root unless noted) | Encoding | Needed by |
|---|---|---|---|
| `jobtbl` | `luafiles514/lua files/datainfo/npcidentity.lub` | CP949 | monsters, NPCs, homunculi |
| `JTtbl` | `…/datainfo/jobidentity.lub` | CP949 | bodies, heads, equipment classes |
| `JobNameTable` | `…/datainfo/jobname.lub` | CP949 | monsters, NPCs, homunculi |
| `ACCESSORY_IDs`, `AccNameTable` | `…/datainfo/accessoryid.lub`, `accname.lub` | CP949 | headgears |
| `SPRITE_ROBE_IDs`, `RobeNameTable` | `…/datainfo/spriterobeid.lub`, `spriterobename.lub` | CP949 | garments |
| `Weapon_IDs`, `WeaponNameTable`, `Expansion_Weapon_IDs` | `…/datainfo/weapontable.lub` | CP949 | weapons |
| `ET_*`, `EMOTION_ORDERLIST` | `…/emotion/emotionlist.lub` | CP949 | effects (emotes) |
| `SKID` | `…/skillinfoz/skillid.lub` | CP949 | skill icons |
| `monster_size_effect_new` | `System/monster_size_effect_new.lub` | CP949 | monsters |
| `item_db` (D21: `Id`, `Type`, `Loc` of each item) | `RoImportSettings.ItemDbPath` (Hercules `db/re/item_db.conf`) | UTF-8 | the item table asset (`ItemSpriteTable.Kinds`) |
| `itemInfo` (global `tbl`, T0.3) | `System/iteminfo_new.lub`, `System/english/iteminfo_new.lub`, `System/spanish/iteminfo_new.lub` | UTF-8 | ground items, headgears, garments, weapons, shields |

- Each category declares the `RoCatalogNeeds` it uses; a run loads the union, so "import only monsters" reads no item table (FR-9).
- **Item table (FR-12).** The file is chosen by `RoItemTableLanguage` (Portuguese `iteminfo_new.lub`, English `english/…`, Spanish `spanish/…`), taken from `RoImportSettings` unless `RoImportOptions.ItemTableLanguage` overrides it (tests, scripts). `RoItemTable` exposes `TryGet(itemId, out RoItemInfo)` with `{ IdentifiedResource, UnidentifiedResource, ClassNum }` and the sorted item ids. Only these three fields are kept.
- **Own tables (OQ-1).** `class-bodies.csv`: one row per player body file name (`BodyName, Job, Race, Kind, BaseJob`, where `Job` and `BaseJob` are `PlayerJob` member names), where `Kind` is `Base`, `Mount`, `Outfit` or `Costume<n>`, `Race` is `Human` or `Doram`. `shield-names.csv`: `ViewId, Name` (1 `가드`, 2 `버클러`, 3 `쉴드`, 4 `미러쉴드`). They are plain CSV beside the code, reviewed by the owner, and documented in sprite-catalog.md §5 (added by this plan's first task). A first draft is generated once from the folder names and `JTtbl`; after review the CSV is the source and nothing regenerates it (it is data, not generated code).
- A missing or unreadable table is reported once (`SourceMissing` or `CorruptFile`, item = file) and every category that needs it is skipped with one warning each; the other categories run.
- **T2.9:** `Load` takes an optional last parameter, the folder of the own CSV tables (default `RoOwnTables.Folder` under the project), so tests can point it at a temp folder. `RoCatalogTables.Missing` lists the groups that could not be read and `Has(needs)` tells a planner whether its groups are loaded; skipping a category with its warning is the importer's (T5.9).

### 4.3 Plan: `RoCatalogPlan` and the planners

Satisfies FR-1 to FR-8, FR-10.

```csharp
// SpriteCatalogCategory is the runtime enum of 4.4 (Core references Uninarok.SpriteCatalog)
public sealed class RoCatalogPlan
{
    public IReadOnlyList<RoCatalogRecord> Records { get; }     // sorted by (category, key)
    public IReadOnlyList<RoCatalogGap> Gaps { get; }           // FR-10, one per (code, item), grouped by category
}
public sealed class RoCatalogRecord
{
    public SpriteCatalogCategory Category; public string Key;      // "monsters", "1002"
    public string Fingerprint;                                  // SHA-256 over the canonical text of this record (§4.7)
    public RoCatalogPair[] Pairs;                               // the sprites to convert
    public RoCatalogValue[] Values;                             // ids, names, scale, palette numbers (a small tagged list)
}
public readonly struct RoCatalogPair { public RoPath Act, Spr; public string Role; }   // Role: "unit", "body:male:human", "garment:7:female", "doram-sheet" …
public readonly struct RoCatalogGap { public RoIssueCode Code; public SpriteCatalogCategory Category; public string Item; public string Message; }

public interface IRoCatalogPlanner
{
    SpriteCatalogCategory Category { get; }
    RoCatalogNeeds Needs { get; }
    void Plan(RoCatalogContext ctx, RoCatalogPlan.Builder plan);
}
public sealed class RoCatalogContext { public IRoSource Data; public RoCatalogTables Tables; /* + file sets */ }
```

- `RoIssueCode` gains `CatalogNameWithoutFile` (a table name with no SPR/ACT), `CatalogFileWithoutName` (a file no table references), `CatalogOrphanAct`, `CatalogOrphanSpr` (one of the pair missing), `CatalogStrayFile` (legacy or misspelt folders and names, OQ-6). All are warnings (FR-10); the report groups them by category (`RoIssue.Referrer` = category name).
- **One plan, two views of the files.** Each planner builds the set of file stems it expects from the tables, walks `IRoSource.Enumerate` of its folders once, and reports the difference both ways. A file is "claimed" by exactly one planner.
- Pair rule: an ACT and an SPR are paired by the same folder and base name (the SPEC-0007 default), except where a rule below picks another SPR (garments, §4.3.5).
- `RoFileNames` holds the one-line file-name builders (`<folder>/<stem>.act`) so no planner concatenates paths by hand.

**4.3.1 Units: monsters, NPCs, homunculi (FR-1).** Records per job id from `JobNameTable`. The folder is chosen by **id range** (`RoUnitFolders`: 45–999 and 10001–19999 → `npc/`; 1001–3999 and 20000+ → `몬스터/`; 6001–6099 → `homun/`), never by which file exists; an id outside every range is a `CatalogStrayFile` warning. File = `<folder>/<JobNameTable[id]>.spr/.act`. Display scale from `RoMonsterScaleTable` (`MonsterSize`; default 1). Names with no file (3D `.gr2`, other regions) → `CatalogNameWithoutFile`. ACT without SPR in `몬스터/` (pet accessories) → `CatalogOrphanAct`. Fallback policy of the record: `SpriteMissingAction.Fallback` (idle, SPEC-0007 §4.5).

**4.3.2 Bodies (FR-2).** One record per base class (key = the base `PlayerJob`). Rows of `class-bodies.csv` give each body file name its job id, race and kind. Files are `인간족/몸통/<sex>/<name>_<sex>.spr` and `도람족/몸통/<sex>/…`; `costume_1…4` subfolders give `Costume<n>` variants. Palettes are `palette/몸/<name>_<sex>_<colour>.pal` (and `costume_n`, `도람족/body`), listed by colour number and attached per (variant, sex). Files that break the pattern (weapons in the body folder, stray quotes, `_여` folder) → `CatalogStrayFile`.

**4.3.3 Heads (FR-3).** One record per hair style number: `인간족/머리통/<sex>/<n>_<sex>.spr`, `도람족/머리통/<sex>/…`; palettes `palette/머리/머리<n>_<sex>_<colour>.pal` and the doram folders.

**4.3.4 Headgears (FR-4).** One record per headgear view id from `AccNameTable` (stem without the leading `_`): `악세사리/<sex>/<sex>_<stem>.spr`, doram `악세사리/<sex>_doram/…`. Slots: (any class, sex, human) and, where a doram file exists, (any class, sex, doram). Ids without a file (regional items) → `CatalogNameWithoutFile`; `<stem>_<sex>` files no table names → `CatalogFileWithoutName`.

**4.3.5 Garments (FR-4).** One record per garment view id (`RobeNameTable`, **not** `_Eng`), folder `로브/<folder>/`. Root sheets: `<folder>.spr` + `<folder>.act` (common) and `<folder>_doram.spr` (doram). For every `<sex>/<bodyname>_<sex>.act`: the animation is that ACT and the sheet is the **job's own SPR if it exists, else the root sheet** (`_doram` for a doram body, from the class table). A job SPR that is a byte copy of the root needs no special case: SPEC-0007's content index maps it to the root's sheet, so "common and doram sheets stored once" holds by construction (checked by the reference test, §6). An ACT for a body name not in the class table → `CatalogStrayFile`, **one per (body name, sex) across every garment** (item `sprite/로브/*/<sex>/<body>_<sex>`, the first file in the message; T4.9: per file it was 26,107 warnings, mostly 4th-class bodies present in most garments), its own SPR going with it.

**4.3.6 Weapons (FR-4, OQ-3).** One record per weapon view id (`Weapon_IDs` / `WeaponNameTable`), plus one record per **item id** that has its own file (suffix `_<digits>` not claimed by a view id). Files `인간족/<job>/<job>_<sex><suffix>.spr` and `도람족/<job>/…`; `<job>` is the base job folder and the body variant name is the file prefix, so each file is a slot (class, sex). The trail files (`…<suffix>_검광`, `…광`) are extra pairs of the same slot with role `trail`. **Fallback rule, applied at import so run time stays a plain lookup:** for slot (class, sex) of view id V, pick the first existing file of: suffix(V), then `_<itemId>` (when the record is item-keyed), then suffix(`Expansion_Weapon_IDs[V]`) (base type). A slot whose chosen file is a fallback records `FallbackOf = V`.

**4.3.7 Shields (FR-4, OQ-3).** One record per shield view id from `shield-names.csv`, plus item-id records for `<id>_방패` files. Files `방패/<job>/<job>_<sex>_<name>.spr`. The same lookup order as weapons: view-id name, then item-id name. (Mad bunny `매드바니` has ClassNum 1 and is reached through its item-id record.)

**4.3.8 Ground items and skill icons (FR-5, FR-7).** Static records. Ground items: one record per distinct resource name used by the item table (`identified` and `unidentified`), pair `아이템/<resource>.spr/.act`; the item → record mapping goes in the item table asset (§4.5). Resources with no file → `CatalogNameWithoutFile`; ACT without SPR and SPR without ACT in the folder → `CatalogOrphanAct`/`CatalogOrphanSpr`. Skill icons: one record per `SKID` entry whose lower-case constant has a file `아이템/<name>.spr` (monster skills `NPC_*` have none: counted, not warned individually; the report gives the total). Both folders are scanned once and shared.

**4.3.9 Effects (FR-6, OQ-5).** One record per sprite in `이팩트/` by name (the whole folder). `emotion` is also an **emote set**: action index = `ET_*` id (Candidate, sprite-catalog.md §3.10; confirmed against the owner's viewer in the reference test). Carts, falcon, status icons and numbers are found by name.

**4.3.11 Keys and roles (T4.6).** Record keys are the ASCII names of §3: units, skill icons by id (`1002`, `5`), ground items and effects by their stem; `body-0007` (base `PlayerJob` value), `head-01`, `headgear-0016`, `garment-0001`, `weapon-0001`, `weapon-item-1117`, `shield-0001`, `shield-item-2189` (`RoCatalogKeys`). A pair's role names its slot (`RoCatalogRoles`): `body:<Job>:<sex>:<race>:<kind>`, `head:<sex>:<race>`, `headgear:any:<sex>:<race>`, `garment:<Job>:<sex>:<race>` and `garment:any:any:<race>` for the root ACT, `weapon:<Job>:<sex>:<race>` with its `trail:<Job>:<sex>:<race>`, `shield:<Job>:<sex>:<race>`; `<Job>` is the `PlayerJob` member name, sex `male`/`female`, race `human`/`doram`, kind the `BodyVariantKind`. Palettes are values `palette:<role>:<colour>` = the `.pal` path; a body variant's file name is the value `name:<role>`; a fallback slot has the value `fallback:<role>` = the view id it stands in for.
- **Weapon and shield files (T4.6, from the data).** The class folders of `인간족/` and `도람족/` are named after body variants, not base jobs (`페코페코_기사/`, `구페코크루세이더/`, `meister_madogear/`), and a file's prefix is its body name: a file is `<BodyName>_<sex><suffix>` where `<BodyName>` is the longest `class-bodies.csv` name that matches, whatever folder it is in (`성직자/프리스트_남_1531` is a priest weapon). A file whose prefix matches no body name (`페코페코기사_남_1185`, `검용병_검`, `wolf_windhawk_남_검광`) is `CatalogFileWithoutName`. `몸통/` and `머리통/` are not weapon folders.

**4.3.10 Palettes.** The Palettes category plans every `.pal` under `palette/`; the body and head planners plan only the palettes of their own records, and both go through `RoPaletteConverter` and the same ledger, so a palette is converted once.

### 4.4 Entries and run-time data: `Uninarok.SpriteCatalog`

Satisfies FR-1 to FR-7, FR-8.

```csharp
public enum SpriteCatalogCategory { Monsters, Npcs, Homunculi, Bodies, Heads, Headgears, Garments, Weapons, Shields, GroundItems, SkillIcons, Effects, Palettes }
public enum SpriteRace { Human, Doram }          // sex is AccountSex (SEX_FEMALE, SEX_MALE; Uninarok.Networking.Data); player class is PlayerJob (section 4.4.1)

// Typed Addressables references. Concrete subclasses, because Unity cannot serialize a generic field. A reference holds a GUID
// (and for a sprite the sub-asset name); holding it loads nothing and makes no hard dependency.
[Serializable] public sealed class SpriteAnimationReference : AssetReferenceT<SpriteAnimation> { public SpriteAnimationReference(string guid) : base(guid) { } }
[Serializable] public sealed class PaletteReference         : AssetReferenceTexture2D          { public PaletteReference(string guid) : base(guid) { } }
[Serializable] public sealed class BodyEntryReference       : AssetReferenceT<BodyEntry>       { public BodyEntryReference(string guid) : base(guid) { } }
[Serializable] public sealed class HeadEntryReference       : AssetReferenceT<HeadEntry>       { public HeadEntryReference(string guid) : base(guid) { } }
[Serializable] public sealed class EquipmentEntryReference  : AssetReferenceT<EquipmentEntry>  { public EquipmentEntryReference(string guid) : base(guid) { } }
// static sprites use the package's AssetReferenceSprite as is: the GUID of the sheet and the sub-asset name ("f12", SPEC-0007 section 4.2)

// What SPEC-0007 produced for one sprite: an animation, or a static sprite (SPEC-0007 FR-11). One of the two is set.
[Serializable] public struct SpriteRef
{
    public SpriteAnimationReference Animation;
    public AssetReferenceSprite Still;
    public bool IsEmpty { get; }                                       // neither reference has a valid key
    public AsyncOperationHandle<SpriteAnimation> LoadAnimation();      // Addressables.LoadAssetAsync<SpriteAnimation>(Animation.RuntimeKey); the caller owns the handle
    public AsyncOperationHandle<Sprite> LoadStill();                    // Addressables.LoadAssetAsync<Sprite>(Still.RuntimeKey)
}

[Serializable] public struct UnitRecord { public int JobId; public string Name; public SpriteRef Sprite; public float Scale; }     // monster/NPC ids are plain ints: not PlayerJob

public sealed class BodyEntry : ScriptableObject
{
    public PlayerJob BaseJob;
    public BodyVariant[] Variants;
    public bool TryGetVariant(PlayerJob job, AccountSex sex, out BodyVariant variant);   // the variant of that job (mount, outfit) or the base one
    [Serializable] public struct BodyVariant { public string Name; public PlayerJob Job; public SpriteRace Race; public BodyVariantKind Kind; public AccountSex Sex; public SpriteRef Sprite; public PaletteSet Palettes; }
}
public enum BodyVariantKind { Base, Mount, Outfit, Costume1, Costume2, Costume3, Costume4 }

public sealed class HeadEntry : ScriptableObject
{
    public int Style;
    public HeadVariant[] Variants;      // (sex, race) -> sprite + hair palettes
    public bool TryGetVariant(AccountSex sex, SpriteRace race, out HeadVariant variant);
}

[Serializable] public struct PaletteRef { public int Colour; public PaletteReference Palette; }
[Serializable] public struct PaletteSet { public PaletteRef[] Colours; }                   // sorted by Colour

public sealed class EquipmentEntry : ScriptableObject
{
    public EquipmentKind Kind;          // Headgear, Garment, Weapon, Shield
    public int ViewId;                  // -1 when keyed by item id only
    public int ItemId;                  // 0 when keyed by view id
    public string Name;
    public EquipmentSlot[] Slots;
    public SpriteAnimationReference[] Trails;   // weapons only, parallel to Slots (empty array for other kinds; a slot without a trail has an empty reference)
    public bool TryResolve(PlayerJob job, AccountSex sex, ClassTable classes, out SpriteAnimationReference animation, out SpriteAnimationReference trail);   // O(1) after the first call (below)
    [Serializable] public struct EquipmentSlot { public bool AnyClass; public PlayerJob Job; public AccountSex Sex; public SpriteRace Race; public int FallbackOf; public SpriteAnimationReference Animation; }   // FallbackOf: the view id this slot stands in for, -1 when none. Animation only: every equipment sprite is animated (104 actions), so no `Still` reference is stored (D16)
}
```

- **Resolution (FR-4).** `EquipmentEntry.TryResolve(job, sex, …)` returns, in order: the slot for exactly (job, sex); else the `AnyClass` slot for (sex, the job's race); else the `AnyClass` human slot for the sex. (`AnyClass` is a flag, not job 0, because `JOB_NOVICE` is 0.) The animation found is that class's, already bound to the right sheet (the pairing was decided at import, §4.3.5). It never falls back to another *action*: an empty or missing action draws nothing, enforced by `SpriteMissingAction.DrawNothing`, which the callers put on every equipment actor; units (`UnitRecord`) use `Fallback` (FR-4, SPEC-0007 §4.5).
- **Lookup cost (D16).** `TryResolve` is O(1): on its first call an entry builds two non-serialized `Dictionary<int,int>` (key `(job, sex)` → slot, key `(race, sex)` → any-class slot) and the three-step chain above becomes up to three dictionary reads. They live and die with the loaded entry (unloading the entry frees them). `BodyEntry` and `HeadEntry` have a few dozen variants at most and scan them linearly.
- **Typed references, no strings (D3, D13).** Every reference in catalog data is a typed Addressables reference to an asset by GUID. A catalog asset therefore has no hard dependency on what it points to: loading an index or an entry loads that one asset and no sprite, animation or texture, and a slot is loaded only when a caller asks for it (§4.5). The catalog does not decide bundles, packing or remote hosting; that is the later loading-strategy spec.
- **Saved numbers (T5.7, D12).** `BodyEntry`, `HeadEntry` and `EquipmentEntry` also carry `StoredActions` and `SheetCount`, written by the importer, so a run that skips a current entry reports the same figures (§4.7).
- **Names of extra animations (OQ-2).** SPEC-0007 stores animations by number; the doc (sprite-catalog.md §3.1) names the unit animations and gives `Extra5`, `Extra6`, … for the rest. Nothing is stored for the names here.

#### 4.4.1 `PlayerJob` (shared enum)

```csharp
namespace Uninarok.Networking.Data { public enum PlayerJob : short { JOB_NOVICE = 0, JOB_SWORDMAN = 1, /* … */ JOB_KNIGHT = 7, JOB_KNIGHT2 = 13, /* … */ JOB_SUMMONER = 4218 /* … */ } }
```

- **Why an enum.** `BodyEntry`, the class table and the equipment slots are keyed by the player's class, which the server sends as a `short` (`CharInfo.Class`). A named type reads and tests better than a bare number and replaces `JobConstant` text in the own tables.
- **Hand-written**, like `AccountSex` and the `SpriteAction` enums, in `Uninarok.Networking.Data`, documented in `docs/data/PlayerJob.md` in the same format as `docs/data/AccountSex.md` (kind, underlying type `short`, source of the values, one table row per member). The source of the values is the three headers of Hercules' `e_class` enum (`mmo.h`): `src/common/class.h`, `class_hidden.h` and `class_special.h` (`JOB_ENUM_VALUE(NAME, id, …)`, 172 ids of players including mounted forms such as `KNIGHT2 = 13`, the sex-specific classes such as `BARD = 19`, and two range markers; D18); member names follow the wire names like `AccountSex` does (`JOB_<NAME>`).
- **Players only.** Monster, NPC, homunculus and mercenary ids (`UnitRecord.JobId`, `JobNameTable`) stay `int`: they are a different id space.
- **Not in the enum:** which class is the base of a mount or outfit (that is the `BaseJob` column of `class-bodies.csv`, §4.2).
- A test (`PlayerJobTests`, `NeedsData`) compares the enum with the client's `JTtbl` and with `class-bodies.csv` (every job name parses). **T2.9:** the client's `JTtbl` lacks 13 of the ids: `REBELLION` 4215 and `SUMMONER` 4218 (only their baby forms are there), Hercules' `DUMMY` placeholders 4219, 4221, 4230–4237 and `BABY_STAR_GLADIATOR2` 4238. The test asserts that exact list, so any other difference fails.

### 4.5 Indices and the lookup facade: `SpriteCatalog`, `SpriteCatalogKeys`

Satisfies FR-1 to FR-7.

```csharp
public sealed class UnitIndex : ScriptableObject { public int[] JobIds; public UnitRecord[] Records; public bool TryGet(int jobId, out UnitRecord r); }   // sorted, binary search
public sealed class BodyIndex : ScriptableObject { public PlayerJob[] Jobs; public BodyEntryReference[] Entries; }       // sorted: every job of every variant -> its class entry
public sealed class HeadIndex : ScriptableObject { public int[] Styles; public HeadEntryReference[] Entries; }          // sorted by style
public sealed class EquipmentIndex : ScriptableObject
{
    public EquipmentKind Kind;
    public int[] ViewIds; public EquipmentEntryReference[] ByView;       // sorted: records keyed by view id
    public int[] ItemIds; public EquipmentEntryReference[] ByItem;       // sorted: records keyed by item id (weapon/shield files named by item id)
}
public sealed class ItemSpriteTable : ScriptableObject                              // from the item table of the chosen language
{
    public int[] ItemIds; public int[] ViewIds; public byte[] Kinds;                // ClassNum and what it means (headgear/garment/weapon/shield/none)
    public int[] GroundSlot; public int[] UnidentifiedGroundSlot;                  // index into GroundItemIndex.Resources, -1 none
}
public sealed class GroundItemIndex : ScriptableObject { public string[] ResourceNames; public SpriteRef[] Resources; }          // one per resource: items sharing a sprite share it
public sealed class SkillIconIndex : ScriptableObject { public int[] SkillIds; public SpriteRef[] Icons; }
public sealed class EffectIndex : ScriptableObject
{
    public string[] Names; public SpriteRef[] Effects;                                // by name (sorted)
    public SpriteRef EmoteSet; public int[] EmoteIds; public int[] EmoteActions;      // the emote sprite and the action of each emote id
}
public sealed class ClassTable : ScriptableObject { /* PlayerJob -> race, base job, body name; from class-bodies.csv */ }

public static class SpriteCatalogKeys      // the fixed addresses of the assets nothing else references; the importer and the runtime share them
{
    public static string Index(SpriteCatalogCategory category);                       // "Assets/Art/Imported/catalog/<category>/_index.asset"
    public static string Items();                                                      // the ItemSpriteTable
    public static string Classes();                                                    // the ClassTable
}

public sealed class SpriteCatalog     // facade; lookups on loaded indices are synchronous, loading is Addressables
{
    public static AsyncOperationHandle<SpriteCatalog> LoadAsync(params SpriteCatalogCategory[] categories);   // loads those categories' index assets (and the class and item tables when needed)
    public void Release();                                                                                     // releases what LoadAsync loaded

    // single-sprite categories: the index holds the record
    public bool TryGetUnit(int jobId, out UnitRecord unit);
    public bool TryGetGroundItem(int itemId, bool identified, out SpriteRef sprite);
    public bool TryGetSkillIcon(int skillId, out SpriteRef icon);
    public bool TryGetEmote(int emoteId, out SpriteRef emoteSet, out int action);
    public bool TryGetEffect(string name, out SpriteRef effect);

    // entry categories: the index finds the entry's reference, the entry loads, then the entry resolves synchronously
    public bool TryFindBody(PlayerJob job, out BodyEntryReference entry);
    public bool TryFindHead(int style, out HeadEntryReference entry);
    public bool TryFindEquipment(EquipmentKind kind, int viewId, int itemId, out EquipmentEntryReference entry);
    public bool TryFindEquipmentByItem(int itemId, out EquipmentKind kind, out EquipmentEntryReference entry);
    public AsyncOperationHandle<BodyEntry> LoadBody(BodyEntryReference entry);
    public AsyncOperationHandle<HeadEntry> LoadHead(HeadEntryReference entry);
    public AsyncOperationHandle<EquipmentEntry> LoadEquipment(EquipmentEntryReference entry);
    public ClassTable Classes { get; }
}
```

- **Lookup cost (D16).** The arrays above are the serialized form (sorted, so the file is deterministic and can be binary-searched). `SpriteCatalog.LoadAsync` builds one `Dictionary<int,int>` (id → position) for each loaded index and for the `ItemSpriteTable`, once, so every `TryGet…`/`TryFind…` is O(1) with no allocation. Their size is small (about 24 bytes per element, plus 100 bytes fixed): units, NPCs and homunculi about 130 KB together, the item table about 420 KB; the references stored in the entries weigh much more (risk R10).
- **Equipment lookup order (OQ-3).** `TryFindEquipment(kind, viewId, itemId, …)`: the record keyed by `viewId`; if absent, the record keyed by `itemId`; if absent, the base type was already folded into the record at import (§4.3.6). A weapon is therefore found for a view id whose own file is missing only through its item-id record or its base type, as the owner decided. This order is recorded as Candidate until checked against the server's look packets (spec §6).
- **By item id (FR-4).** `TryFindEquipmentByItem(itemId, …)` uses `ItemSpriteTable` (`Kinds`, `ViewIds`) then `TryFindEquipment`. Item 1201 (Faca, ClassNum 1) → weapon view id 1 → short sword for a swordman; 2220 → headgear view id 16.
- **Ground items (FR-5).** `ItemSpriteTable.GroundSlot[i]` indexes `GroundItemIndex.Resources`; identified and unidentified names usually resolve to one resource. Item 501 → `빨간포션`.
- **Emotes (FR-6).** `TryGetEmote` returns the emote sprite and `action = emoteId` (`ET_SURPRISE = 0`) through `EmoteIds`/`EmoteActions`; the caller plays `(action)` as a non-directional animation.
- **Loading and release.** Handles come from Addressables, loaded with the reference's `RuntimeKey`; whoever calls `LoadAnimation`, `LoadStill`, `LoadBody`, `LoadHead` or `LoadEquipment` owns that handle and releases it (`Addressables.Release`), and no code relies on the handle a reference caches inside itself (several callers may load the same reference). `SpriteCatalog.Release()` releases only the index and table assets that `LoadAsync` loaded. A miss in `TryFind…`/`TryGet…` is `false`, never an exception; a failed load is the handle's failed status.
- `SpriteCatalogKeys` exists only for the three kinds of asset that no other asset references (the indices, the item table, the class table); everything else is reached through a typed reference.
- **T3.2 additions** (no new behaviour, only names for what the bullets above already require): `SpriteCatalog.MissingActionOf(category)` returns the `SpriteMissingAction` an actor of that category uses (units `Fallback`, everything else `DrawNothing`, §4.4); `internal static SpriteCatalog Create(indices, items, classes)` builds the facade over assets already in memory and is what `LoadAsync` calls once its handles complete (the tests use it, no Addressables load); `SpriteCatalogKeys.Folder(category)` gives the folder name of §3. `ClassTable` is parallel arrays sorted by job (`Jobs`, `Races`, `BaseJobs`, `BodyNames`) with `TryGet(job, out race, out baseJob)` and `RaceOf(job)` (`Human` for a job it does not list), which `EquipmentEntry.TryResolve` uses for the any-class step. `UnitIndex` carries its `Category`, so one facade merges the unit indices it loads. `PaletteSet.TryGet(colour, out palette)` reads the sorted colours.

### 4.6 Output layout and writers: `RoCatalogOutputPaths`, `RoCatalogEntryWriter`, `RoCatalogIndexWriter`

Satisfies FR-8, FR-9.

- `RoCatalogOutputPaths.Entry(category, key)` and `Index(category)` build the §3 paths through PLAN-0002's `RoOutputPaths` (so the same collision and "reserved path" rules apply). `Art/Imported/catalog/**/*.asset` is covered by the existing `Art/Imported/**/*.asset` LFS rule.
- Entry assets are written in place through `RoAssetWriter` (GUID kept, PLAN-0002 §4.7). Entry assets are **owned by the ledger** (one source = one record). Index assets are written by the index step, rewritten only when their content (ordered ids, names, keys) changed; they are owned by a ledger entry keyed `catalog/<category>/_index` with the plan's category fingerprint as its hash.
- "No image sheet is duplicated" (FR-8) is a consequence of D2 (sheets only come from SPEC-0007's content index) and is asserted by the reference test (distinct sheet paths = distinct SPR hashes).
- **Addressables (`RoCatalogAddressables`, D13).** The package's settings asset is created if missing. `Ensure()` is idempotent and creates **folder entries** (one entry for a whole folder, whose assets are addressable by their own project path) in these groups, and nothing else:

  | Group | Folder entry (address = project path) |
  |---|---|
  | `catalog-<category>` (12) | `Assets/Art/Imported/catalog/<category>` |
  | `catalog-tables` | `.../catalog/items`, `.../catalog/classes` |
  | `sprite-sheets` | `Assets/Art/Imported/sprite/_sheets` |
  | `sprites-monster`, `-npc`, `-homun`, `-human`, `-doram`, `-garment`, `-headgear`, `-shield`, `-item`, `-effect` | `Assets/Art/Imported/sprite/<folder>` for the mirrored folders (`몬스터`, `npc`, `homun`, `인간족`, `도람족`, `로브`, `악세사리`, `방패`, `아이템`, `이팩트`) |
  | `palettes` | `Assets/Art/Imported/palette` |

  Folder entries keep the group count small and mean no per-asset marking is ever written (about 100,000 assets would make the settings asset and every editor operation slow). **Sheets are in their own group on purpose**: an animation depends on the sheet it draws, and a sheet shared by animations in different groups must be an explicit entry, or each bundle would carry its own copy of it. `sprites-human` holds bodies, heads and weapons together because `인간족/` mixes them; a finer split is a packing decision for the loading-strategy spec. Groups use the package's default schemas; pack mode, compression and hosting are not set here.
- **Addresses and references.** The address of every asset in a folder entry is its project path, and the typed references of §4.4 point at assets by **GUID** (`AssetDatabase.AssetPathToGUID` of the output path SPEC-0007 reports, no asset loaded); a static sprite's reference adds the sub-asset name of the sheet (`f0`). Only the indices and the two tables are reached by address (`SpriteCatalogKeys`, e.g. `Assets/Art/Imported/catalog/monsters/_index.asset`).
- **Check.** `RoCatalogAddressables.Verify(plan)` (run at the end of an import and by a test) confirms that every reference written into an index or entry has a valid key, resolves to an existing asset inside one of the folder entries above (`FindAssetEntry` on its GUID), and, for a sprite, that the sub-asset name exists in the sheet; a reference that does not is a `CatalogStrayFile` warning naming it.
- **Reserved paths.** `Art/Imported/catalog/` joins the reserved generated paths of PLAN-0002 §4.8, so hand-made assets elsewhere stay untouched.

### 4.7 Importer and ledger: `RoCatalogImporter`, `RoCatalogEntryConverter`, `RoCatalogIndexConverter`

Satisfies FR-9, FR-10, FR-12 (uses SPEC-0002 FR-5, FR-6).

```csharp
public enum RoJobKind { /* … */ CatalogEntry, CatalogIndex }                                // PLAN-0002 enum gains two kinds
public sealed class RoCatalogOptions { public SpriteCatalogCategory[] Categories; public RoItemTableLanguage? ItemTableLanguage; }
public sealed class RoCatalogReport
{
    public IReadOnlyList<RoCategoryReport> Categories { get; }
    public RoReport Report { get; }
    public string Summarize();                              // one block per category
}
public sealed class RoCategoryReport { public SpriteCatalogCategory Category; public int Entries, Sheets, Actions, Warnings; public TimeSpan Time; public bool Skipped; }

// RoImport gains:
public static RoCatalogReport Catalog(SpriteCatalogCategory[] categories, RoImportOptions options = null);
public static RoCatalogReport CatalogAll(RoImportOptions options = null);
public sealed class RoCatalogEntryConverter : IRoConverter { /* RoJobKind.CatalogEntry; Handles(catalog/…) */ }
public sealed class RoCatalogIndexConverter : IRoConverter { /* RoJobKind.CatalogIndex */ }
```

- **Run.** `RoCatalogImporter.Run`: load tables for the union of `Needs`, build the plan for the chosen categories only, report its gaps once (`RoReport.Warn`, deduplicated by (code, item)), run the entry batch, run the index batch, assemble `RoCatalogReport`. The plan is passed to the converters through `RoImportOptions.CatalogPlan` (internal) and reached from `RoImportContext.CatalogPlan`.
- **Entry job.** Path `catalog/<category>/<key>`. It looks up its record in the plan, then:
  1. ledger check with `SourceHash` = record fingerprint, `Rules` = `RoCatalogEntryConverter.RulesVersion`, dependencies = the ACT/SPR (and PAL) hashes of its pairs (a changed sprite re-converts its record);
  2. for each pair `ctx.Sprite(act, spr, options)` (palettes requested for bodies and heads: `RoSpriteOptions.Palettes`) and `ctx.Palette` for palette files; SPEC-0007 skips what is current and shares sheets;
  3. a pair that fails becomes `Failed` for this record with SPEC-0007's issue (one issue, no partial entry; FR-10 continues the run);
  4. write the entry asset (entry categories) or nothing (inline categories), record the ledger entry.
- **Fingerprint.** SHA-256 of the canonical text of the record (category, key, values, pairs with roles) plus `RulesVersion`. Anything derived from a lub table (scale, view id, names, the chosen item language) is inside the text, so a table or setting change re-imports exactly the records it touches without a ledger dependency on files outside the data root (the `System` folder is a second source, §4.2).
- **Index job.** One per chosen category. The references come from the output paths and sprite names that SPEC-0007 reported for the category's records (kept in a small per-run table of strings; the GUID is read from the path, no asset is loaded), so no asset is loaded to build an index. It writes the index, runs `RoCatalogAddressables.Ensure()`/`Verify`, and, after the last category, updates the item and class tables. Palettes have no index.
- **Per-category numbers (FR-9).** `Entries` = records; `Sheets` = distinct sheet textures among the animations/sprites referenced; `Actions` = sum of stored (deduplicated) actions of the referenced animations; `Warnings` = the category's gaps plus issues; `Time` = wall time of its jobs. A record skipped as current contributes the numbers saved in its entry asset (`StoredActions`, `SheetCount`; for inline categories, in the index asset), so a second run reports the same figures.
- **Order and independence (FR-9).** Categories never depend on each other's outputs: bodies and heads request their own palettes themselves (§4.3.10), so importing monsters touches no equipment and importing equipment after monsters re-imports nothing current.
- **Menu and window.** `Tools/Ragnarok/Import Catalog…` opens `RoCatalogWindow`: one checkbox per category, "All", "Force", **Import**, the per-category result table. `Tools/Ragnarok/Catalog/<category>` has one item per category and `…/All`. Both call `RoImport.Catalog` only (PLAN-0002 §4.11: UI and code paths cannot differ).

### 4.8 Settings: `RoImportSettings` (FR-12)

Satisfies FR-12.

```csharp
public enum RoItemTableLanguage { Portuguese, English, Spanish }
[FilePath("ProjectSettings/UninarokRoImport.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class RoImportSettings : ScriptableSingleton<RoImportSettings>
{
    public RoItemTableLanguage ItemTableLanguage = RoItemTableLanguage.Portuguese;
    public static string ItemTableFile(RoItemTableLanguage language);      // iteminfo_new.lub | english/… | spanish/…
}
// SettingsProvider "Project/Uninarok/Ragnarok Import": a dropdown for the language
```

- **Placement (T2.6).** `RoItemTableLanguage` lives in `Uninarok.RoImport.Core` (`Core/Catalog/Tables/RoItemTable.cs`), beside `RoItemTable`, so the pure table reader can take it; `RoItemTable.FileName(language)` maps it to the file under `System/`, and `RoImportSettings.ItemTableFile` (editor, T5.1) returns the same value.

- The setting lives in the project (committed in `ProjectSettings/`), so a fresh clone uses the same table as the rest of the team; a fresh project (no file) uses Portuguese. `RoImportOptions.ItemTableLanguage` overrides it for one run (tests, scripts).
- Changing the language changes the item table content and so the fingerprints of records that depend on it (ground items, headgears, garments, weapons, shields) and the `ItemSpriteTable`; SPEC-0007's sprite conversions are untouched.

### 4.9 Lookup preview: `RoCatalogLookupWindow` (FR-11, should)

Satisfies FR-11.

- `Tools/Ragnarok/Catalog Lookup…`: a mode popup (job id, item id, view id + kind, skill id, emote id, effect name), the inputs the mode needs (class, sex, race for equipment), a **Resolve** button that calls `SpriteCatalog`, and the result opened in SPEC-0007's `RoSpritePreviewWindow` (animation, direction and speed controls reused). For a static result it shows the sprite. Editor only; it needs the catalog already imported, and says so if it is not.

## 5. Threading and lifecycle

Satisfies FR-9.

- Import: main thread, synchronous, inside `RoBatch` (PLAN-0002 §4.10); planning is pure and allocates its tables once per run. The item table is parsed with `SkipField`, so its working set is the three kept fields per item (about 17,500 items), not the 14 MB text. Pixel work is SPEC-0007's and bounded there.
- Memory across ~20,000 jobs: records hold paths, not objects; the checkpoint every 200 jobs saves ledgers and unloads unused assets (PLAN-0002 §4.10); the index step loads nothing.
- Cancel stops after the current job and saves the ledgers; running the same categories again resumes because finished records are `Skipped` (FR-9).
- Run time: `TryGet...`/`TryFind...` are synchronous O(1) dictionary reads over indices already loaded by `SpriteCatalog.LoadAsync` (dictionaries built once at load, D16); loading a sprite, animation or entry is an Addressables operation and completes on the main thread (Unity API on the main thread, CLAUDE.md §2). The facade is an instance holding its handles, with no static state.
- Domain reload: the importer keeps no static state beyond constants (no `AutoStaticsCleanup` needed).

## 6. Test plan

Satisfies every FR. EditMode NUnit. Data-dependent tests are `[Category("NeedsData")]`; the rest build tables and file lists in code.

| Test class | FR | Checks |
|---|---|---|
| `RoLuaReaderTests` | 1–7 | tables, nested tables, `[ref.Name] = "x"` keys, negative numbers, escapes, references to imports, `function … end` and `main()` skipped, `SkipField`, unknown construct → rejected with a line number |
| `RoLubReaderTests` (`NeedsData`) | 1–7 | one bytecode file and one plain-text file read with the recorded sizes and a known Korean string in each encoding; garbage and empty input return false, never throw |
| `RoCatalogTablesTests` (`NeedsData`) | 1–7, 12 | counts recorded by the survey (`JobNameTable` 5,300; `Weapon_IDs`; `SKID` 1,806; items 17,482 in Portuguese, 17,906 in English); missing table → one issue and the dependent categories skipped |
| `RoUnitPlannerTests` | 1, 10 | id ranges choose the folder; a name in both folders resolves by range; `.gr2`/missing name → one warning; ACT without SPR → orphan warning; scale default 1 |
| `RoBodyPlannerTests`, `RoHeadPlannerTests` | 2, 3, 10 | class table rows → variants (mount, outfit, costume, doram); palettes by colour number; stray names ignored with one warning |
| `RoEquipmentPlannerTests` | 4, 10 | headgear sex/doram slots; garment root sheet vs own SPR vs `_doram`; weapon suffix → item-id → base type order (OQ-3); trail pairs; shield names; item-id records; unreferenced files reported |
| `RoStaticPlannerTests` | 5, 6, 7, 10 | items sharing a resource share one record; identified vs unidentified; skill icon by lower-case `SKID`; effects by name; emote set |
| `EquipmentEntryTests`, `SpriteCatalogTests` (`Uninarok.SpriteCatalog.Tests`; the facade is built from in-memory indices, no Addressables load) | 4, 5, 6, 7 | exact class slot beats any-class doram beats any-class human; `AnyClass` is not job 0 (a Novice slot and an any-class slot do not collide); missing action draws nothing; unit falls back; item id → kind and view id; ground slot; emote action = id; a miss returns false; lookups after load allocate nothing; the entry dictionaries are built once |
| `RoCatalogImporterTests` | 8, 9, 10, 12 | synthetic source: one category imports only its own sheets; second run `Skipped`; a second category reuses a shared sheet; changed table or setting changes only the dependent fingerprints; a failing pair fails one record only; per-category report numbers (same on the second run); entries land in the §3 folders |
| `RoCatalogAddressablesTests` | 8 | `Ensure` creates the groups and folder entries of 4.6 and is idempotent; every reference in a synthetic index or entry has a valid key and resolves inside a folder entry; the static-sprite reference carries the sheet GUID and the sub-asset name; a dangling reference is one warning |
| `PlayerJobTests` (`NeedsData`) | 2 | every `PlayerJob` member exists in the client's `JTtbl` with the same id; every job name in `class-bodies.csv` parses; the doc table of `PlayerJob.md` lists every member |
| `SpriteCatalogPlayTests` (`Uninarok.SpriteCatalog.PlayTests`, play mode, Addressables with the Asset Database play-mode script) | 1 | `LoadAsync(Monsters)` then Poring 1002 → `LoadAnimation` returns the animation; `Release()` leaves no open handles |
| `RoImportSettingsTests` | 12 | default Portuguese; each language maps to its file; the override option beats the setting |
| `RoCatalogReferenceTests` (`NeedsData`) | 1–7, 10 | the acceptance lookups of spec §4: Poring 1002 at scale 1; novice male, knight on peco female, summoner male bodies; hair style 1 male; garment with 178 classes: distinct sheet paths = distinct SPR hashes, right animation for a knight and a summoner; item 1201 → short sword for a swordman; item 501; emote 0; cart by name; skill 5 (Bash); item 2220 hat; a full run lists each gap once |

**Reference set and counts (TASKS T0.4).** The constants of `RoCatalogReferenceTests` and the `NeedsData` table tests: unit `8W_SOLDIER` (105 → `npc/`, 2245 → `몬스터/`); Poring 1002; garment `천사날개` (`기사_남` own SPR, `abyss_chaser_남` root sheet, `summoner_남` own SPR, 178/188 class ACTs); weapon item-id file `인간족/검사/검사_남_1117` (item 1117); item 1201 → `_단검`; shield item-id file `방패/검사/검사_남_2189_방패` (item 2189); counts `JobNameTable` 5,300, `SKID` 1,806, `Weapon_IDs` 103, `ACCESSORY_IDs` 2,859, `SPRITE_ROBE_IDs` 330, items 17,482 (pt) / 17,906 (en) / 17,905 (es). The full list is at the head of tasks.md.

**Manual checks** (PR evidence, Unity MCP or owner): each acceptance lookup through `RoCatalogLookupWindow` against the reference viewer; a full import per category with the report numbers recorded (the owner sets the texture memory target from them, spec §5); the project window shows one folder per category.

## 7. Migration and compatibility

- Nothing is deleted. `PlayerJob` (code and `docs/data/PlayerJob.md`) is new, beside `AccountSex`; `AccountSex` is reused as is. `RoJobKind` gains `CatalogEntry` and `CatalogIndex`; `RoIssueCode` gains the five codes of §4.3; `RoImportOptions` gains `ItemTableLanguage`, `SystemSource` (default: the `System` folder next to the data root) and an internal `CatalogPlan`. `Uninarok.RoImport.Editor` references `Uninarok.SpriteCatalog` and `Unity.Addressables.Editor`; `com.unity.addressables` is added to `Packages/manifest.json` (D13); `RoArchitectureTests` gets the new asmdefs added to its checks (the runtime assembly must not reference the importer, and `Uninarok.RoImport.Core` must not reference `UnityEditor`).
- SPEC-0007 is used as is: `RoSpriteConverter.Convert(ctx, act, spr, options)` and `RoPaletteConverter` are its documented entry points for "a later spec (grouping by game object, import by category)" (PLAN-0007 §7). No change to its outputs.
- The ledger gains `catalog.json`. `Art/Imported/catalog/` outputs use the existing LFS rule for `.asset`. `ProjectSettings/UninarokRoImport.asset` and `Assets/AddressableAssetsData/` are committed.
- Entry and index assets are generated: never hand-edited (CLAUDE.md §2). The two own tables are data (CSV) and are edited by hand and reviewed by the owner.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Plan first, write second: planners are pure and produce a `RoCatalogPlan`; the editor only executes it | Planners that call the converter directly (rules untestable without Unity and 18 GB of data) |
| D2 | Sheets and animations come only from SPEC-0007 (content index, per-ACT assets); this plan chooses the pairs. A job SPR that equals the root is deduplicated by the content hash, with no special code | A catalog-owned sheet per equipment (duplicates 60,438 garment sheets); comparing SPR bytes in the planner |
| D3 (owner, 2026-11-20) | References inside catalog data are **typed Addressables references**: `AssetReferenceT<SpriteAnimation>`, `AssetReferenceSprite` (GUID plus sub-asset name) and `AssetReferenceTexture2D`, and `AssetReferenceT<Entry>` for entries, each as a concrete subclass (Unity cannot serialize a generic field); loading is `Addressables.LoadAssetAsync(reference.RuntimeKey)` and the caller owns the handle | Address strings (weak: tied to a path, no type check, no editor validation); `ComponentReference<T>` of the Addressables samples (accepts only a prefab with a component); `LazyLoadReference` (withdrawn by the owner); direct object references (loading one index would load every sprite) |
| D4 | Animations keep SPEC-0007's mirrored paths (`sprite/<game folder>/…`); the catalog folders hold entries and indices | An output-folder override per category in `RoSpriteConverter` (changes SPEC-0007 and breaks ledger ownership when the same ACT is also converted by the folder command); the cost is that `인간족/` holds bodies, heads and weapons together |
| D5 | Inline records for single-sprite categories (units, ground items, skill icons, effects); entry assets only for bodies, heads and equipment, whose entries hold many slots | One ScriptableObject per record everywhere (about 15,000 tiny assets for no benefit); everything inline (a garment index would hold 80,000 references) |
| D6 | The job unit is one record; a second small batch builds the indices | One job per category (no checkpoint or resume inside a 1,500-monster category); building the index inside the last entry job (ordering by path is not reliable) |
| D7 | Lub text parsed by a small own reader of the Lua subset the files use, input from GRF `Decompile` | A Lua interpreter (MoonSharp/NLua): a new dependency for data-only files (CLAUDE.md §2); regex over the text (breaks on nesting) |
| D8 | The item table language is a project setting (`ScriptableSingleton`, in `ProjectSettings/`), overridable per run; Portuguese default (owner) | An `EditorPrefs` value (per machine, not shared); a constant |
| D9 | Weapon and shield fallback (specific → item-id → base type) is resolved at import, so the run-time lookup is a plain slot search | Resolving the chain at run time (needs the lub tables in the game) |
| D10 | Lookup order **view id → item id → base type** (owner, OQ-3) | Item id first (the client's probable order; kept as the documented alternative until the server's look packets are checked) |
| D11 | Entry identity is ids and names from the tables, not file names; an unresolvable file is a warning (FR-10) | Entries built by scanning folders (unit folders mix NPC and monster files; the id range must decide, FR-1) |
| D12 | Per-category numbers saved in the entries and indices, so a second run reports the same figures | Recomputing by loading every animation on every run |
| D13 (owner, 2026-11-20) | Addressables is the run-time loading mechanism; the package is added with this plan, groups hold **folder entries** (never one entry per asset), sheets live in their own group | Per-asset entries (about 100,000 entries: slow settings asset and editor); no explicit sheet group (each bundle would carry a copy of a shared sheet); deciding bundle layout here (later spec) |
| D14 (owner, 2026-11-20) | `PlayerJob : short` is a shared enum in `Uninarok.Networking.Data`, **written by hand** (like `AccountSex` and the `SpriteAction` enums) and documented in `docs/data/PlayerJob.md`; values from Hercules `class.h`/`class_hidden.h`; guarded by a test against the client's `JTtbl` | A generated enum and generator (owner: no); plain `int` ids (no type, no names in the own tables); one enum for players and monsters (different id spaces) |
| D15 (owner, 2026-11-20) | Sex is the existing `AccountSex`; the catalog assembly references `Uninarok.Networking`. `SpriteRace` (Human, Doram) is new and stays in the catalog | A parallel `SpriteSex` (a second enum for the same wire value); extracting the shared enums to a neutral assembly now (revisit if the coupling hurts) |
| D16 (owner, 2026-11-20) | Lookups are O(1) at run time: dictionaries built once when an index is loaded and lazily when an equipment entry first resolves; equipment slots hold `SpriteAnimationReference` only (no `Still`), the weapon trail is a parallel array that is empty for other kinds | Binary search and linear scans (cheap but not O(1)); `SpriteRef` in every slot (two reference objects per slot, about 300 bytes, half unused); a dense array by id (only `PlayerJob` is dense enough) |
| D17 (T2.2, 2026-11-20) | A bare unknown name in a value is `nil` and a positional `nil` leaves a hole, the Lua semantics the client runs with: `monster_size_effect_new.lub` line 3253 has `MonsterEff = { EF_LEVEL99_RED }` (no `EFFECT.` prefix), which the client reads as an empty effect list. A field of an unknown name (`jobtbl.JT_X` without `jobtbl`) is still rejected with its line | Rejecting the file (one slip in the client's data would drop the whole monster scale table); resolving the name against other tables (guessing) |
| D18 (T2.8, 2026-11-20) | `PlayerJob` takes its values from all three headers of Hercules' `e_class` (`class.h`, `class_hidden.h`, `class_special.h`): 172 members. The plan named only the first two (154), which left out the sex-specific classes (`BARD`, `DANCER`, `CLOWN`, `GYPSY`, `MINSTREL`, `WANDERER`, `KAGEROU`, `OBORO` and their baby and `_T` forms) that bodies need. Hercules 2026.09 has no 4th classes, so the client's 4th-class bodies have no `PlayerJob` and are listed as unmatched by the class table | Keeping 154 (bodies of bard, dancer and others would have no class); adding the 4th classes from the client's `JTtbl` (not Hercules ids, D14) |
| D22 (T7.2, 2026-11-21) | An SPR without its ACT (skill icons `bs_skintemper` 109 and `ht_falcon` 127, effect `bluemsg`) is a `CatalogOrphanSpr` warning and gives **no record**, as §4.3.8 and FR-10 say; the skill-icon and effect planners had planned it as a still record that the entry converter then failed (T5.6 note), which broke "no failure" on the real data. The converter keeps its check as a guard | Converting a lone SPR into a `Still` (SPEC-0007 converts ACT/SPR pairs only: a new conversion path for three files); keeping the failure (contradicts FR-10) |
| D21 (owner, T5.8, 2026-11-20) | The **kind** of an item (`ItemSpriteTable.Kinds`) comes from the server's item database, Hercules `db/re/item_db.conf` (`Type`, `Loc`): `IT_WEAPON` → weapon; `IT_ARMOR` with `EQP_SHIELD` → shield, else with a head location (`EQP_HEAD_TOP/MID/LOW`, `EQP_HELM`, `EQP_COSTUME_HEAD_*`) → headgear, else with `EQP_GARMENT`/`EQP_COSTUME_GARMENT` → garment; anything else (shadow gear, cards, usables) → none. The client's item table has no type and a view id alone is ambiguous (view id 1 is a weapon and a headgear). The file is read by a small libconfig-subset reader in Core (`RoItemDb`, only `Id`, `Type`, `Loc`); its path is a project setting (`RoImportSettings.ItemDbPath`, default `../Hercules-2026.09/db/re/item_db.conf` from the Unity project, the checkout in this repository), overridable per run (`RoImportOptions.ItemDbPath`). A missing file is one `SourceMissing` warning and every item's kind is none | An own id-range CSV (ranges have exceptions); kinds only for item-id files (item 2220 → hat would fail, FR-4); the view id alone (ambiguous) |
| D20 (T4.9, 2026-11-20) | An equipment slot is (job, sex, race) (§4.4); when several body names of the class table share it (the class body `기사`, its outfit `기사_h`, the all-class mount `사자기사`, all `JOB_KNIGHT`), the slot takes the file of the **Base** row, else the first row in table order; the others are not planned and are counted in one `CatalogStrayFile` summary per category (item `<category>: shared slots`), not one warning each | A kind in the equipment slot (changes §4.4 and the run-time lookup, which receives only job and sex from the server); one warning per dropped file (thousands, mostly outfits) |
| D19 (T3.1, 2026-11-20) | The runtime assembly `Uninarok.SpriteCatalog` uses the namespace **`Uninarok.Catalog`**: a type `SpriteCatalog` inside a namespace `Uninarok.SpriteCatalog` makes every caller in `Uninarok.*` that writes `SpriteCatalog` hit CS0118 (the name resolves to the namespace first). `EquipmentKind` is `byte` with `None = 0` before `Headgear, Garment, Weapon, Shield`, so a default value means nothing and `ItemSpriteTable.Kinds` stores the enum as is | Namespace `Uninarok.SpriteCatalog` with the facade renamed (the plan's public name would change); `EquipmentKind` without `None` (a default slot or item would read as a headgear) |

## 9. Risks

| Risk | Mitigation |
|---|---|
| R1 Garment volume: about 81,000 ACT → as many animation assets, the largest import by far | Category on its own (garments last, once); SPEC-0007's ledger makes it resumable; the run records time and sizes; deduplicating identical ACTs across files stays a later option (sprite-catalog.md §4) |
| R2 The Lua subset reader misses a construct in a table not yet read | One test per table on the real files (`NeedsData`); a rejected file names the line; phase 0 reads every table of §4.2 before any planner is written |
| R3 GRF `Decompile` fails or mis-decodes a file (367 of 379 decompile; encodings differ per file) | The GRF mapping check records each file's encoding; plain-text files read directly; a failing table skips only the categories that need it |
| R4 Addressables on Unity 6000.7 beta: the package version; whether a typed reference by **GUID** works for an asset that is addressable only through a **folder entry**; **sub-asset** references (`AssetReferenceSprite` with `f0`) inside such an entry; the asset-database play mode | A phase-0 spike before the data types are written: add the package, create a folder entry, make a `SpriteAnimationReference` and an `AssetReferenceSprite` from GUIDs of assets inside it, load both in the editor and in a play-mode test (the animation's sheet comes from the `sprite-sheets` group, one copy); fallbacks: an explicit entry for each referenced asset (more entries, only for what is referenced), or storing a static sprite as a one-frame animation **Spike result (T0.2, 2026-11-20, Addressables 4.2.0): works, no fallback needed.** Folder entries only (one per folder, four groups); `FindAssetEntry(guid, true)` finds each child with address = project path. References built from GUIDs of folder children (`AssetReferenceT<SpriteAnimation>` subclass, `AssetReferenceSprite` with `SubObjectName = "f1"`, `AssetReferenceTexture2D`) have valid keys (the sprite key is `<guid>[f1]`) and load in the editor (`WaitForCompletion`, play mode script *Use Asset Database*) and in a play-mode test; the sub-asset load returns `f1`. Two animations in different groups share one sheet: `CheckBundleDupeDependencies` reports no duplicate with the sheet folder in its own group, and reports the sheet copied into both bundles when that entry is removed (so the `sprite-sheets` group is required). Note: `AssetReferenceSprite.editorAsset` returns the first sprite, not the sub-asset; `Verify` (§4.6) must check the sub-asset name against the sheet's sprites, not through `editorAsset`. |
| R9 Editor cost of Addressables with ~100,000 assets under folder entries (group build, analyze, content update) | Only folder entries exist (D13); the catalog never builds bundles; settings are touched once per import and saved once |
| R10 Memory of the stored references: every `AssetReference` is a managed object with its GUID string (about 150 bytes). Garments hold about 81,000 slots (about 14 MB if every entry were loaded at once, about 45 KB for one entry of 245 slots); the dictionaries add about 1.9 MB for all garments and about 420 KB for the item table (estimates by count, not measured) | Entries load one at a time and free their dictionaries with them; slots are animation-only (D16); the phase-0 spike measures one 245-slot garment entry in the Profiler and the owner decides from the number (a more compact reference form is the fallback) **Measured (T0.2, `GC.GetTotalMemory` over 400 synthetic entries):** one 245-slot entry with its two dictionaries is **85 KB** (355 B per slot: the reference object, its GUID string and the slot), about twice the estimate; a 245-key `Dictionary<int,int>` is 11 KB of that. Every garment entry loaded at once would be about 29 MB (81,000 slots). One entry at a time stays small; the owner decides whether a more compact reference form is needed. |
| R5 The class and shield tables are hand-made and can be wrong for rare bodies | Owner review (OQ-1); unmatched files are warnings, not failures; the lookup window makes a wrong pairing visible |
| R6 View id versus item id (OQ-3) may not match what the server sends | Recorded as Candidate; one place to change (§4.3.6, §4.5); revisit when the look packets are implemented |
| R7 Time and memory of a full run (~95,000 sprite files, 14 MB item text) | Per-category runs; item table parsed with `SkipField`; 200-job checkpoints; the report records time per category; the owner sets the texture target from the numbers (spec §5) |
| R8 Gaps are numerous (319 unit names without files, 593 headgear ids, orphans) and drown real problems | One warning per gap, grouped by category in the summary, and the category counts in the report |

## 10. Traceability

| FR | Plan sections |
|---|---|
| FR-1 | 4.1, 4.2, 4.3.1, 4.4, 4.5, 6 |
| FR-2 | 4.2, 4.3.2, 4.4, 4.5, 6 |
| FR-3 | 4.3.3, 4.4, 4.5, 6 |
| FR-4 | 4.3.4, 4.3.5, 4.3.6, 4.3.7, 4.4, 4.5, 6 |
| FR-5 | 4.3.8, 4.5, 6 |
| FR-6 | 4.3.9, 4.5, 6 |
| FR-7 | 4.3.8, 4.5, 6 |
| FR-8 | 3, 4.6, 6 |
| FR-9 | 2, 4.7, 5, 6 |
| FR-10 | 4.3, 4.7, 6 |
| FR-11 | 4.9, 6 |
| FR-12 | 4.2, 4.8, 6 |
