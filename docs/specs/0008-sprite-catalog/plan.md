# PLAN-0008: Sprite Catalog (units, equipment, items, effects)

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0008, approved 2026-11-20) |
| **Status** | Draft |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Where each table, folder and file pattern lives comes from [sprite-catalog.md](sprite-catalog.md) (normative for its topic; updated in the same commit as the code that uses it). Converting one sprite (sheets, animations, palettes, the player) is [PLAN-0007](../0007-spr-act-pal-sprites/plan.md); shared import rules (paths, source, ledger, results, batch, menu) are [PLAN-0002](../0002-ro-import-foundation/plan.md). This plan adds the layer above them: **ids and names → game objects → the sprites SPEC-0007 already converts**.

## 1. Constraints and assumptions

- **No new third-party library.** Lub files are read through GRF (`Lub.Decompile()`, already in `GRF.dll`), which returns Lua source text. The data in these files is plain Lua table constructors, so a small reader of that subset (§4.1) is part of this plan.
- **Nothing here converts pixels or ACT bytes.** Every sheet, animation and palette comes from SPEC-0007's `RoSpriteConverter.Convert(ctx, act, spr, options)` and `RoPaletteConverter`. This plan decides *which ACT is paired with which SPR* and *which game object owns the result*.
- **Pure planning, editor writing.** A planner turns tables and the list of source files into a **plan** (plain data, no Unity types). The editor layer executes a plan. So the lookup rules (the hard part) are tested on synthetic tables and file lists, and the import is tested on a few real files.
- **Run time needs only assets.** The lookups the game uses (job id, view id, item id, skill id, emote id, name) resolve from assets the import writes, with no lub, GRF or editor code (§4.5). Loading strategy (Addressables, streaming) is a later spec; this plan makes every reference *lazy* so an index never loads the sprites it points to.
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
        entry assets (bodies, heads, equipment)  +  category index assets  +  SpriteCatalogRoot
                                       ▼
   runtime (Uninarok.SpriteCatalog): SpriteCatalog facade → TryGetUnit / TryGetBody / TryGetHead /
                                     TryGetEquipment / TryGetGroundItem / TryGetSkillIcon / TryGetEmote / TryGetEffect
```

Data flow of `RoImport.Catalog(RoCatalogCategory.Monsters)`:

1. **Tables.** Load only the tables the chosen categories need (§4.2), once per run, memoised. A table that cannot be read is one `CorruptFile`/`SourceMissing` issue and the categories that need it are skipped; the others continue.
2. **Plan.** Each chosen planner enumerates its source folders and joins them with the tables. The plan lists every record to import and every gap (FR-10). Planning does no conversion and writes nothing.
3. **Jobs.** One `RoJobKind.CatalogEntry` job per record (path `catalog/<category>/<key>`). Order is deterministic (`RoBatch.Order`). Progress, cancel, 200-job checkpoint, ledger and error handling are `RoBatch`'s (PLAN-0002 §4.10).
4. **Record job.** Check the catalog ledger (fingerprint of the planned record plus its ACT/SPR hashes). Current → `Skipped`. Otherwise ask SPEC-0007 for each ACT/SPR pair (sheets shared and ledger-skipped there), write the entry asset when the category has entry assets, record the ledger.
5. **Index.** After the batch, a second small batch (one job per category, kind `CatalogIndex`) builds the category's index asset from the plan and the converted outputs, and the `SpriteCatalogRoot`. The index is rewritten only when its content changed.
6. **Report.** `RoCatalogReport`: per category entries, distinct sheets, stored actions, warnings, time (FR-9).

## 3. Assemblies and file layout

Satisfies FR-1 to FR-12.

| Path | Contents | Assembly |
|---|---|---|
| `Assets/Scripts/SpriteCatalog/Uninarok.SpriteCatalog.asmdef` | runtime assembly, references `Uninarok.Sprites` only | `Uninarok.SpriteCatalog` |
| `Assets/Scripts/SpriteCatalog/` `SpriteRef.cs`, `SpriteSex.cs`, `SpriteRace.cs`, `UnitRecord.cs`, `UnitIndex.cs`, `BodyEntry.cs`, `HeadEntry.cs`, `EquipmentEntry.cs`, `EquipmentIndex.cs`, `ItemSpriteTable.cs`, `GroundItemIndex.cs`, `SkillIconIndex.cs`, `EffectIndex.cs`, `ClassTable.cs`, `PaletteSet.cs`, `SpriteCatalogRoot.cs`, `SpriteCatalog.cs` | data types, indices and the lookup facade (§4.5) | `Uninarok.SpriteCatalog` |
| `Assets/Editor/RoImport/Core/Formats/RoLubReader.cs` | GRF adapter for `.lub` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Catalog/Lua/` | `RoLuaLexer`, `RoLuaReader`, `RoLuaValue` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Catalog/Tables/` | `RoCatalogTables`, `RoJobTables`, `RoViewIdTables`, `RoItemTable`, `RoEmoteTable`, `RoSkillTable`, `RoMonsterScaleTable`, `RoOwnTables` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Core/Catalog/Planning/` | `IRoCatalogPlanner`, one planner per category, `RoUnitFolders`, `RoCatalogPlan`, `RoCatalogRecord`, `RoCatalogGap`, `RoFileNames` | `Uninarok.RoImport.Core` |
| `Assets/Editor/RoImport/Catalog/Data/class-bodies.csv`, `shield-names.csv` | the owner-reviewed own tables (OQ-1), read by `RoOwnTables` | data files |
| `Assets/Editor/RoImport/Editor/Catalog/` | `RoCatalogImporter`, `RoCatalogEntryConverter`, `RoCatalogIndexConverter`, `RoCatalogEntryWriter`, `RoCatalogIndexWriter`, `RoCatalogOutputPaths`, `RoCatalogReport`, `RoImportSettings`, `RoImportSettingsProvider`, `RoCatalogWindow`, `RoCatalogLookupWindow` | `Uninarok.RoImport.Editor` (references `Uninarok.SpriteCatalog`) |
| `Assets/Tests/EditMode/RoImport/Catalog/` | planner, table, Lua and importer tests | `Uninarok.RoImport.Tests` |
| `Assets/Tests/EditMode/SpriteCatalog/` | lookup tests on hand-built entries | new `Uninarok.SpriteCatalog.Tests` |

Outputs (roots of PLAN-0002 §4.5; the animations and sheets stay where SPEC-0007 puts them, D4):

| Output | Path |
|---|---|
| Entry assets | `Art/Imported/catalog/<category>/<key>.asset` (bodies, heads, headgears, garments, weapons, shields) |
| Category index | `Art/Imported/catalog/<category>/_index.asset` (every category; for units, ground items, skill icons and effects it also holds the records) |
| Root | `Art/Imported/catalog/SpriteCatalogRoot.asset` |
| Item table (§4.5) | `Art/Imported/catalog/items/_index.asset` |
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

- **Adapter.** GRF decompiles bytecode (`ESC Lua`) to text; a file already in plain text (12 of 379) is decoded as given. The encoding is passed per file because the client's files differ (`iteminfo_new.lub` and its language copies are UTF-8; the `data/luafiles514` tables and older item tables are CP949). `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` is called before GRF's `EncodingService` is first touched (sprite-catalog.md §1). The exact GRF members and the way the encoding is set are fixed by a **GRF mapping check** (TASKS phase 0), the same way PLAN-0007 §4.1 fixed the SPR/ACT/PAL mapping.
- **Reader scope: the subset these files use**, nothing more: global assignments (`Name = {…}`, `Name.Field = value`), table constructors (positional values, `name = value`, `[expr] = value`), numbers (including negative), strings (with escapes), `true`/`false`/`nil`, and **references** (`jobtbl.JT_PORING`, `SKID.SM_BASH`) resolved against `imports` and earlier globals of the same chunk. `function … end` bodies and the loader calls after the data (`main()`, `AddItem(…)`) are **skipped** by balanced keyword counting; they are not evaluated. An unknown construct inside a table is a rejected file with the line number, not a silent miss.
- **Large tables.** The item table is 14 million characters; `SkipField` drops the description and display-name fields while parsing (only `identifiedResourceName`, `unidentifiedResourceName` and `ClassNum` are kept), so memory stays small.
- **Import order.** Some tables name constants of another (`jobname.lub` uses `jobtbl` from `npcidentity.lub`, `accname.lub` uses `ACCESSORY_IDs`). `RoCatalogTables` reads the defining file first and passes its globals as `imports` (§4.2).
- Adapters never throw (PLAN-0007 §4.1 rule); a rejected read is `false` with a reason, reported once as `CorruptFile` naming the file.

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
| `itemInfo` | `System/iteminfo_new.lub`, `System/english/iteminfo_new.lub`, `System/spanish/iteminfo_new.lub` | UTF-8 | ground items, headgears, garments, weapons, shields |

- Each category declares the `RoCatalogNeeds` it uses; a run loads the union, so "import only monsters" reads no item table (FR-9).
- **Item table (FR-12).** The file is chosen by `RoItemTableLanguage` (Portuguese `iteminfo_new.lub`, English `english/…`, Spanish `spanish/…`), taken from `RoImportSettings` unless `RoImportOptions.ItemTableLanguage` overrides it (tests, scripts). `RoItemTable` exposes `TryGet(itemId, out RoItemInfo)` with `{ IdentifiedResource, UnidentifiedResource, ClassNum }` and the sorted item ids. Only these three fields are kept.
- **Own tables (OQ-1).** `class-bodies.csv`: one row per player body file name (`BodyName, JobConstant, Race, Kind, BaseJobConstant`), where `Kind` is `Base`, `Mount`, `Outfit` or `Costume<n>`, `Race` is `Human` or `Doram`. `shield-names.csv`: `ViewId, Name` (1 `가드`, 2 `버클러`, 3 `쉴드`, 4 `미러쉴드`). They are plain CSV beside the code, reviewed by the owner, and documented in sprite-catalog.md §5 (added by this plan's first task). A first draft is generated once from the folder names and `JTtbl`; after review the CSV is the source and nothing regenerates it (it is data, not generated code).
- A missing or unreadable table is reported once (`SourceMissing` or `CorruptFile`, item = file) and every category that needs it is skipped with one warning each; the other categories run.

### 4.3 Plan: `RoCatalogPlan` and the planners

Satisfies FR-1 to FR-8, FR-10.

```csharp
public enum RoCatalogCategory { Monsters, Npcs, Homunculi, Bodies, Heads, Headgears, Garments, Weapons, Shields, GroundItems, SkillIcons, Effects, Palettes }

public sealed class RoCatalogPlan
{
    public IReadOnlyList<RoCatalogRecord> Records { get; }     // sorted by (category, key)
    public IReadOnlyList<RoCatalogGap> Gaps { get; }           // FR-10, one per (code, item), grouped by category
}
public sealed class RoCatalogRecord
{
    public RoCatalogCategory Category; public string Key;      // "monsters", "1002"
    public string Fingerprint;                                  // SHA-256 over the canonical text of this record (§4.7)
    public RoCatalogPair[] Pairs;                               // the sprites to convert
    public RoCatalogValue[] Values;                             // ids, names, scale, palette numbers (a small tagged list)
}
public readonly struct RoCatalogPair { public RoPath Act, Spr; public string Role; }   // Role: "unit", "body:male:human", "garment:7:female", "doram-sheet" …
public readonly struct RoCatalogGap { public RoIssueCode Code; public RoCatalogCategory Category; public string Item; public string Message; }

public interface IRoCatalogPlanner
{
    RoCatalogCategory Category { get; }
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

**4.3.2 Bodies (FR-2).** One record per base class (key = base job id). Rows of `class-bodies.csv` give each body file name its job id, race and kind. Files are `인간족/몸통/<sex>/<name>_<sex>.spr` and `도람족/몸통/<sex>/…`; `costume_1…4` subfolders give `Costume<n>` variants. Palettes are `palette/몸/<name>_<sex>_<colour>.pal` (and `costume_n`, `도람족/body`), listed by colour number and attached per (variant, sex). Files that break the pattern (weapons in the body folder, stray quotes, `_여` folder) → `CatalogStrayFile`.

**4.3.3 Heads (FR-3).** One record per hair style number: `인간족/머리통/<sex>/<n>_<sex>.spr`, `도람족/머리통/<sex>/…`; palettes `palette/머리/머리<n>_<sex>_<colour>.pal` and the doram folders.

**4.3.4 Headgears (FR-4).** One record per headgear view id from `AccNameTable` (stem without the leading `_`): `악세사리/<sex>/<sex>_<stem>.spr`, doram `악세사리/<sex>_doram/…`. Slots: (any class, sex, human) and, where a doram file exists, (any class, sex, doram). Ids without a file (regional items) → `CatalogNameWithoutFile`; `<stem>_<sex>` files no table names → `CatalogFileWithoutName`.

**4.3.5 Garments (FR-4).** One record per garment view id (`RobeNameTable`, **not** `_Eng`), folder `로브/<folder>/`. Root sheets: `<folder>.spr` + `<folder>.act` (common) and `<folder>_doram.spr` (doram). For every `<sex>/<bodyname>_<sex>.act`: the animation is that ACT and the sheet is the **job's own SPR if it exists, else the root sheet** (`_doram` for a doram body, from the class table). A job SPR that is a byte copy of the root needs no special case: SPEC-0007's content index maps it to the root's sheet, so "common and doram sheets stored once" holds by construction (checked by the reference test, §6). An ACT for a body name not in the class table → `CatalogStrayFile`.

**4.3.6 Weapons (FR-4, OQ-3).** One record per weapon view id (`Weapon_IDs` / `WeaponNameTable`), plus one record per **item id** that has its own file (suffix `_<digits>` not claimed by a view id). Files `인간족/<job>/<job>_<sex><suffix>.spr` and `도람족/<job>/…`; `<job>` is the base job folder and the body variant name is the file prefix, so each file is a slot (class, sex). The trail files (`…<suffix>_검광`, `…광`) are extra pairs of the same slot with role `trail`. **Fallback rule, applied at import so run time stays a plain lookup:** for slot (class, sex) of view id V, pick the first existing file of: suffix(V), then `_<itemId>` (when the record is item-keyed), then suffix(`Expansion_Weapon_IDs[V]`) (base type). A slot whose chosen file is a fallback records `FallbackOf = V`.

**4.3.7 Shields (FR-4, OQ-3).** One record per shield view id from `shield-names.csv`, plus item-id records for `<id>_방패` files. Files `방패/<job>/<job>_<sex>_<name>.spr`. The same lookup order as weapons: view-id name, then item-id name. (Mad bunny `매드바니` has ClassNum 1 and is reached through its item-id record.)

**4.3.8 Ground items and skill icons (FR-5, FR-7).** Static records. Ground items: one record per distinct resource name used by the item table (`identified` and `unidentified`), pair `아이템/<resource>.spr/.act`; the item → record mapping goes in the item table asset (§4.5). Resources with no file → `CatalogNameWithoutFile`; ACT without SPR and SPR without ACT in the folder → `CatalogOrphanAct`/`CatalogOrphanSpr`. Skill icons: one record per `SKID` entry whose lower-case constant has a file `아이템/<name>.spr` (monster skills `NPC_*` have none: counted, not warned individually; the report gives the total). Both folders are scanned once and shared.

**4.3.9 Effects (FR-6, OQ-5).** One record per sprite in `이팩트/` by name (the whole folder). `emotion` is also an **emote set**: action index = `ET_*` id (Candidate, sprite-catalog.md §3.10; confirmed against the owner's viewer in the reference test). Carts, falcon, status icons and numbers are found by name.

**4.3.10 Palettes.** The Palettes category plans every `.pal` under `palette/`; the body and head planners plan only the palettes of their own records, and both go through `RoPaletteConverter` and the same ledger, so a palette is converted once.

### 4.4 Entries and run-time data: `Uninarok.SpriteCatalog`

Satisfies FR-1 to FR-7, FR-8.

```csharp
public enum SpriteSex { Male, Female }
public enum SpriteRace { Human, Doram }

// One reference to what SPEC-0007 produced; never loads on deserialization.
[Serializable] public struct SpriteRef
{
    public LazyLoadReference<SpriteAnimation> Animation;     // animated result
    public LazyLoadReference<Sprite> Still;                  // static ACT result (SPEC-0007 FR-11)
    public bool IsEmpty { get; }
}

[Serializable] public struct UnitRecord { public int JobId; public string Name; public SpriteRef Sprite; public float Scale; }

public sealed class BodyEntry : ScriptableObject
{
    public int BaseJobId;
    public BodyVariant[] Variants;
    [Serializable] public struct BodyVariant { public string Name; public int JobId; public SpriteRace Race; public BodyVariantKind Kind; public SpriteSex Sex; public SpriteRef Sprite; public PaletteSet Palettes; }
}
public enum BodyVariantKind { Base, Mount, Outfit, Costume1, Costume2, Costume3, Costume4 }

public sealed class HeadEntry : ScriptableObject
{
    public int Style;
    public HeadVariant[] Variants;      // (sex, race) -> sprite + hair palettes
}

[Serializable] public struct PaletteRef { public int Colour; public LazyLoadReference<Texture2D> Palette; }
[Serializable] public struct PaletteSet { public PaletteRef[] Colours; }       // sorted by Colour

public sealed class EquipmentEntry : ScriptableObject
{
    public EquipmentKind Kind;          // Headgear, Garment, Weapon, Shield
    public int ViewId;                  // 0 when keyed by item id only
    public int ItemId;                  // 0 when keyed by view id
    public string Name;
    public EquipmentSlot[] Slots;
    public SpriteRef[] Trails;          // weapons; parallel to Slots (empty ref where none)
    public bool TryResolve(int classId, SpriteSex sex, ClassTable classes, out SpriteRef sprite, out SpriteRef trail);
    [Serializable] public struct EquipmentSlot { public int ClassId; public SpriteSex Sex; public SpriteRace Race; public int FallbackOf; public SpriteRef Sprite; }
}
```

- **Resolution (FR-4).** `EquipmentEntry.TryResolve(classId, sex, …)` returns, in order: the slot for exactly (class, sex); else the any-class slot (`ClassId == 0`) for (sex, the class's race); else the any-class human slot for the sex. The sprite found already holds that class's animation bound to the right sheet (the pairing was decided at import, §4.3.5). It never falls back to another *action*: an empty or missing action draws nothing, enforced by `SpriteMissingAction.DrawNothing`, which the facade puts on every equipment result; units (`UnitRecord`) carry `Fallback` (FR-4, SPEC-0007 §4.5).
- **References are lazy** (`LazyLoadReference`), so loading an entry loads no animation until a slot is chosen and loading an index loads no sprite. (Spike in phase 0: lazy references to a sprite sub-asset and to a `ScriptableObject` survive re-import and `AssetDatabase` moves; risk R4.)
- **Names of extra animations (OQ-2).** SPEC-0007 stores animations by number; the doc (sprite-catalog.md §3.1) names the unit animations and gives `Extra5`, `Extra6`, … for the rest. Nothing is stored for the names here.

### 4.5 Indices and the lookup facade: `SpriteCatalog`

Satisfies FR-1 to FR-7.

```csharp
public sealed class UnitIndex : ScriptableObject { public int[] JobIds; public UnitRecord[] Records; public bool TryGet(int jobId, out UnitRecord r); }   // sorted, binary search
public sealed class BodyIndex : ScriptableObject { public int[] JobIds; public LazyLoadReference<BodyEntry>[] Entries; }       // every job id of every variant -> its class entry
public sealed class HeadIndex : ScriptableObject { public int[] Styles; public LazyLoadReference<HeadEntry>[] Entries; }
public sealed class EquipmentIndex : ScriptableObject
{
    public EquipmentKind Kind;
    public int[] ViewIds;  public LazyLoadReference<EquipmentEntry>[] ByView;       // sorted by view id
    public int[] ItemIds;  public LazyLoadReference<EquipmentEntry>[] ByItem;       // item-id keyed records (weapon/shield files named by item id)
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
    public EmoteSet Emotes;                                                            // animation + action index of each emote id
}
public sealed class ClassTable : ScriptableObject { /* jobId -> race, base job id, body name; from class-bodies.csv */ }
public sealed class SpriteCatalogRoot : ScriptableObject   // the one asset a game holds; lazy references to every index
{
    public LazyLoadReference<UnitIndex> Monsters, Npcs, Homunculi;
    public LazyLoadReference<BodyIndex> Bodies;  public LazyLoadReference<HeadIndex> Heads;
    public LazyLoadReference<EquipmentIndex> Headgears, Garments, Weapons, Shields;
    public LazyLoadReference<ItemSpriteTable> Items; public LazyLoadReference<GroundItemIndex> GroundItems;
    public LazyLoadReference<SkillIconIndex> SkillIcons; public LazyLoadReference<EffectIndex> Effects;
    public LazyLoadReference<ClassTable> Classes;
}

public sealed class SpriteCatalog     // facade; synchronous, no allocation per lookup
{
    public SpriteCatalog(SpriteCatalogRoot root);
    public bool TryGetUnit(int jobId, out UnitRecord unit);
    public bool TryGetBody(int jobId, SpriteSex sex, out BodyVariant body);                                   // human or doram from the class
    public bool TryGetHead(int style, SpriteSex sex, SpriteRace race, out HeadVariant head);
    public bool TryGetEquipment(EquipmentKind kind, int viewId, int itemId, int classId, SpriteSex sex, out SpriteRef sprite, out SpriteRef trail);
    public bool TryGetEquipmentByItem(int itemId, int classId, SpriteSex sex, out EquipmentKind kind, out SpriteRef sprite, out SpriteRef trail);
    public bool TryGetGroundItem(int itemId, bool identified, out SpriteRef sprite);
    public bool TryGetSkillIcon(int skillId, out SpriteRef icon);
    public bool TryGetEmote(int emoteId, out SpriteAnimation animation, out int action);
    public bool TryGetEffect(string name, out SpriteRef effect);
}
```

- **Equipment lookup order (OQ-3).** `TryGetEquipment(kind, viewId, itemId, …)`: the record keyed by `viewId`; if absent, the record keyed by `itemId`; if absent, the base type was already folded into the record at import (§4.3.6). A weapon is therefore found for a view id whose own file is missing only through its item-id record or its base type, as the owner decided. This order is recorded as Candidate until checked against the server's look packets (spec §6).
- **By item id (FR-4).** `TryGetEquipmentByItem(itemId, …)` uses `ItemSpriteTable` (`Kinds`, `ViewIds`) then `TryGetEquipment`. Item 1201 (Faca, ClassNum 1) → weapon view id 1 → short sword for a swordman; 2220 → headgear view id 16.
- **Ground items (FR-5).** `ItemSpriteTable.GroundSlot[i]` indexes `GroundItemIndex.Resources`; identified and unidentified names usually resolve to one resource. Item 501 → `빨간포션`.
- **Emotes (FR-6).** `TryGetEmote` returns the effect-set animation and `action = emoteId` (`ET_SURPRISE = 0`); the caller plays `(action)` as a non-directional animation.
- The facade holds the loaded indices after the first use; a miss is `false`, never an exception.

### 4.6 Output layout and writers: `RoCatalogOutputPaths`, `RoCatalogEntryWriter`, `RoCatalogIndexWriter`

Satisfies FR-8, FR-9.

- `RoCatalogOutputPaths.Entry(category, key)` and `Index(category)` build the §3 paths through PLAN-0002's `RoOutputPaths` (so the same collision and "reserved path" rules apply). `Art/Imported/catalog/**/*.asset` is covered by the existing `Art/Imported/**/*.asset` LFS rule.
- Entry assets are written in place through `RoAssetWriter` (GUID kept, PLAN-0002 §4.7). Entry assets are **owned by the ledger** (one source = one record). Index assets are written by the index step, rewritten only when their content (ordered ids, names, lazy-ref GUIDs) changed; they are owned by a ledger entry keyed `catalog/<category>/_index` with the plan's category fingerprint as its hash.
- "No image sheet is duplicated" (FR-8) is a consequence of D2 (sheets only come from SPEC-0007's content index) and is asserted by the reference test (distinct sheet paths = distinct SPR hashes).
- **Reserved paths.** `Art/Imported/catalog/` joins the reserved generated paths of PLAN-0002 §4.8, so hand-made assets elsewhere stay untouched.

### 4.7 Importer and ledger: `RoCatalogImporter`, `RoCatalogEntryConverter`, `RoCatalogIndexConverter`

Satisfies FR-9, FR-10, FR-12 (uses SPEC-0002 FR-5, FR-6).

```csharp
public enum RoJobKind { /* … */ CatalogEntry, CatalogIndex }                                // PLAN-0002 enum gains two kinds
public sealed class RoCatalogOptions { public RoCatalogCategory[] Categories; public RoItemTableLanguage? ItemTableLanguage; }
public sealed class RoCatalogReport
{
    public IReadOnlyList<RoCategoryReport> Categories { get; }
    public RoReport Report { get; }
    public string Summarize();                              // one block per category
}
public sealed class RoCategoryReport { public RoCatalogCategory Category; public int Entries, Sheets, Actions, Warnings; public TimeSpan Time; public bool Skipped; }

// RoImport gains:
public static RoCatalogReport Catalog(RoCatalogCategory[] categories, RoImportOptions options = null);
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
- **Index job.** One per chosen category. It reads the converted outputs of its records by **path and sub-asset name** (kept in a small per-run table, not as object references, so unloading between chunks keeps memory bounded), resolves them to `SpriteRef`s in chunks of 200 with unloads, and writes the index. Palettes have no index. After the last category the root and the item table are updated.
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

- The setting lives in the project (committed in `ProjectSettings/`), so a fresh clone uses the same table as the rest of the team; a fresh project (no file) uses Portuguese. `RoImportOptions.ItemTableLanguage` overrides it for one run (tests, scripts).
- Changing the language changes the item table content and so the fingerprints of records that depend on it (ground items, headgears, garments, weapons, shields) and the `ItemSpriteTable`; SPEC-0007's sprite conversions are untouched.

### 4.9 Lookup preview: `RoCatalogLookupWindow` (FR-11, should)

Satisfies FR-11.

- `Tools/Ragnarok/Catalog Lookup…`: a mode popup (job id, item id, view id + kind, skill id, emote id, effect name), the inputs the mode needs (class, sex, race for equipment), a **Resolve** button that calls `SpriteCatalog`, and the result opened in SPEC-0007's `RoSpritePreviewWindow` (animation, direction and speed controls reused). For a static result it shows the sprite. Editor only; it needs the catalog already imported, and says so if it is not.

## 5. Threading and lifecycle

Satisfies FR-9.

- Import: main thread, synchronous, inside `RoBatch` (PLAN-0002 §4.10); planning is pure and allocates its tables once per run. The item table is parsed with `SkipField`, so its working set is the three kept fields per item (about 17,500 items), not the 14 MB text. Pixel work is SPEC-0007's and bounded there.
- Memory across ~20,000 jobs: records hold paths, not objects; the checkpoint every 200 jobs saves ledgers and unloads unused assets (PLAN-0002 §4.10); the index step resolves references in chunks of 200 and unloads between them.
- Cancel stops after the current job and saves the ledgers; running the same categories again resumes because finished records are `Skipped` (FR-9).
- Run time: lookups are synchronous reads of already-loaded index arrays (binary search); no per-lookup allocation. Lazy references load their target when a caller reads `.asset`, on the caller's thread (Unity API on the main thread, CLAUDE.md §2).
- Domain reload: the facade and the importer keep no static state beyond constants (no `AutoStaticsCleanup` needed).

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
| `EquipmentEntryTests`, `SpriteCatalogTests` (`Uninarok.SpriteCatalog.Tests`) | 4, 5, 6, 7 | exact class slot beats any-class doram beats any-class human; missing action draws nothing; unit falls back; item id → kind and view id; ground slot; emote action = id; a miss returns false |
| `RoCatalogImporterTests` | 8, 9, 10, 12 | synthetic source: one category imports only its own sheets; second run `Skipped`; a second category reuses a shared sheet; changed table or setting changes only the dependent fingerprints; a failing pair fails one record only; per-category report numbers (same on the second run); entries land in the §3 folders |
| `RoImportSettingsTests` | 12 | default Portuguese; each language maps to its file; the override option beats the setting |
| `RoCatalogReferenceTests` (`NeedsData`) | 1–7, 10 | the acceptance lookups of spec §4: Poring 1002 at scale 1; novice male, knight on peco female, summoner male bodies; hair style 1 male; garment with 178 classes: distinct sheet paths = distinct SPR hashes, right animation for a knight and a summoner; item 1201 → short sword for a swordman; item 501; emote 0; cart by name; skill 5 (Bash); item 2220 hat; a full run lists each gap once |

**Manual checks** (PR evidence, Unity MCP or owner): each acceptance lookup through `RoCatalogLookupWindow` against the reference viewer; a full import per category with the report numbers recorded (the owner sets the texture memory target from them, spec §5); the project window shows one folder per category.

## 7. Migration and compatibility

- Nothing is deleted. `RoJobKind` gains `CatalogEntry` and `CatalogIndex`; `RoIssueCode` gains the five codes of §4.3; `RoImportOptions` gains `ItemTableLanguage`, `SystemSource` (default: the `System` folder next to the data root) and an internal `CatalogPlan`. `Uninarok.RoImport.Editor` references `Uninarok.SpriteCatalog`; `RoArchitectureTests` gets the new asmdefs added to its checks (the runtime assembly must not reference the importer, and `Uninarok.RoImport.Core` must not reference `UnityEditor`).
- SPEC-0007 is used as is: `RoSpriteConverter.Convert(ctx, act, spr, options)` and `RoPaletteConverter` are its documented entry points for "a later spec (grouping by game object, import by category)" (PLAN-0007 §7). No change to its outputs.
- The ledger gains `catalog.json`. `Art/Imported/catalog/` outputs use the existing LFS rule for `.asset`. `ProjectSettings/UninarokRoImport.asset` is committed.
- Entry and index assets are generated: never hand-edited (CLAUDE.md §2). The two own tables are data (CSV) and are edited by hand and reviewed by the owner.

## 8. Decisions

| # | Decision | Alternatives rejected |
|---|---|---|
| D1 | Plan first, write second: planners are pure and produce a `RoCatalogPlan`; the editor only executes it | Planners that call the converter directly (rules untestable without Unity and 18 GB of data) |
| D2 | Sheets and animations come only from SPEC-0007 (content index, per-ACT assets); this plan chooses the pairs. A job SPR that equals the root is deduplicated by the content hash, with no special code | A catalog-owned sheet per equipment (duplicates 60,438 garment sheets); comparing SPR bytes in the planner |
| D3 | Unity references from the index to its targets are `LazyLoadReference`; run-time data is arrays of ids and lazy refs | Direct object references (loading one index would load every sprite); Addressables now (a later spec chooses the loading strategy) |
| D4 | Animations keep SPEC-0007's mirrored paths (`sprite/<game folder>/…`); the catalog folders hold entries and indices | An output-folder override per category in `RoSpriteConverter` (changes SPEC-0007 and breaks ledger ownership when the same ACT is also converted by the folder command); the cost is that `인간족/` holds bodies, heads and weapons together |
| D5 | Inline records for single-sprite categories (units, ground items, skill icons, effects); entry assets only for bodies, heads and equipment, whose entries hold many slots | One ScriptableObject per record everywhere (about 15,000 tiny assets for no benefit); everything inline (a garment index would hold 80,000 references) |
| D6 | The job unit is one record; a second small batch builds the indices | One job per category (no checkpoint or resume inside a 1,500-monster category); building the index inside the last entry job (ordering by path is not reliable) |
| D7 | Lub text parsed by a small own reader of the Lua subset the files use, input from GRF `Decompile` | A Lua interpreter (MoonSharp/NLua): a new dependency for data-only files (CLAUDE.md §2); regex over the text (breaks on nesting) |
| D8 | The item table language is a project setting (`ScriptableSingleton`, in `ProjectSettings/`), overridable per run; Portuguese default (owner) | An `EditorPrefs` value (per machine, not shared); a constant |
| D9 | Weapon and shield fallback (specific → item-id → base type) is resolved at import, so the run-time lookup is a plain slot search | Resolving the chain at run time (needs the lub tables in the game) |
| D10 | Lookup order **view id → item id → base type** (owner, OQ-3) | Item id first (the client's probable order; kept as the documented alternative until the server's look packets are checked) |
| D11 | Entry identity is ids and names from the tables, not file names; an unresolvable file is a warning (FR-10) | Entries built by scanning folders (unit folders mix NPC and monster files; the id range must decide, FR-1) |
| D12 | Per-category numbers saved in the entries and indices, so a second run reports the same figures | Recomputing by loading every animation on every run |

## 9. Risks

| Risk | Mitigation |
|---|---|
| R1 Garment volume: about 81,000 ACT → as many animation assets, the largest import by far | Category on its own (garments last, once); SPEC-0007's ledger makes it resumable; the run records time and sizes; deduplicating identical ACTs across files stays a later option (sprite-catalog.md §4) |
| R2 The Lua subset reader misses a construct in a table not yet read | One test per table on the real files (`NeedsData`); a rejected file names the line; phase 0 reads every table of §4.2 before any planner is written |
| R3 GRF `Decompile` fails or mis-decodes a file (367 of 379 decompile; encodings differ per file) | The GRF mapping check records each file's encoding; plain-text files read directly; a failing table skips only the categories that need it |
| R4 `LazyLoadReference` to sprite sub-assets or across re-import behaves differently in Unity 6000.7 beta | A phase-0 spike (create, re-import, move, read back) before the data types are written; fallback: store GUID + file id strings and load through `AssetDatabase`/a loader interface |
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
