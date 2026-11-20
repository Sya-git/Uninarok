# Sprite catalog: what lives where in `data/sprite`, `data/palette` and the lub tables

| | |
|---|---|
| **Belongs to** | [SPEC-0008](spec.md) (reference material, normative for its topic) |
| **Status** | Draft (survey of 2026-11-19) |
| **Data set** | `Uninarok-client/Assets/Editor/GRF Resources~/data` and `…/System` (LATAM client, lub files dated 2025-04 to 2026-10) |
| **Method** | Lub files decompiled with GRF `Lub.Decompile()`; SPR/ACT read with GRF `Spr`/`Act`; file counts and MD5 duplicates with shell tools. Scratch tools only; nothing parses bytes by hand. |

Status of each claim: **Confirmed** (checked in the data), **Candidate** (consistent with the data, not yet checked in a viewer or against the server), **Open** (see spec §7). Facts about the SPR/ACT/PAL formats themselves (actions, layers, timing, duplicates) are in [SPEC-0007's reference](../0007-spr-act-pal-sprites/act-reference.md).

Korean folder names: 몬스터 monster, 인간족 human race, 도람족 doram race, 몸통 body, 머리통 head, 머리 hair, 몸 body (palettes), 남 male, 여 female, 로브 robe (garment), 악세사리 accessory (headgear), 방패 shield, 아이템 item, 이팩트 effect, 검광 sword light (weapon trail), 유저인터페이스 user interface.

---

## 1. Lub files (`data/luafiles514/lua files/`)

All `.lub` files are Lua 5.1 bytecode (header `ESC L u a Q`). GRF's `Lub` class decompiles 367 of the 379; 12 are plain-text Lua already (GRF reports "Unrecognized file format (Lua)": read them as text). Lub loading in .NET needs `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` before GRF's `EncodingService` is first touched (Korean code page 949).

| Table (global) | File | Maps | Used for |
|---|---|---|---|
| `jobtbl` | `datainfo/npcidentity.lub` | constant name → **job id** for NPCs, monsters, homunculi, mercenaries (`JT_PORING = 1002`) | monster/NPC id |
| `JTtbl` | `datainfo/jobidentity.lub` | constant name → job id for **player classes** and a few late NPCs (`JT_KNIGHT = 7`, `JT_4_*` = 10xxx) | player class id |
| `JobNameTable` | `datainfo/jobname.lub` | job id → **sprite base name** (`[jobtbl.JT_PORING] = "PORING"`); 5,300 entries | monster/NPC/homun file name |
| `ReqJobName(id)` | `datainfo/jobname_f.lub` | lookup function over `JobNameTable` | — |
| `ShadowFactorTable` | `datainfo/shadowtable.lub` | job id → shadow scale (0 = no shadow) | later spec (shadows) |
| `PCJobNameTable` | `datainfo/pcjobname.lub` | **empty** in this client | — |
| `PCJobNameTableMan`/`Woman` | `datainfo/pcjobnamegender*.lub` | player job id → **display name** (localised, not a file name) | UI only |
| `ACCESSORY_IDs` | `datainfo/accessoryid.lub` | constant → **headgear view id** (`ACCESSORY_GOGGLES = 1`), 2,859 entries | headgear id |
| `AccNameTable` | `datainfo/accname.lub` | view id → **headgear file stem** (`"_고글"`) | headgear file |
| `SPRITE_ROBE_IDs` | `datainfo/spriterobeid.lub` | constant → **garment view id** (`ROBE_WINGS = 1`), 331 ids | garment id |
| `RobeNameTable` (+ `_Eng`), `RobeTopLayer` | `datainfo/spriterobename.lub` | garment view id → **garment folder name** (`"천사날개"`); list of garments drawn above the body | garment folder; layer order (later spec) |
| `Weapon_IDs` | `datainfo/weapontable.lub` | constant → **weapon view id** (0–102 plus named expansions) | weapon id |
| `WeaponNameTable` | same file | weapon view id → **weapon file suffix** (`"_검"`, `"_1207"`) | weapon file |
| `Expansion_Weapon_IDs` | same file | specific view id → **base weapon type** (Main Gauche → SHORTSWORD) | fallback when a specific suffix has no file |
| `WeaponHitWaveNameTable` | same file | weapon type → hit sound | later spec (audio) |
| `TB_Layer_Priority` | `datainfo/tb_layer_priority.lub` | headgear id → draw priority (Default_Top 200, Mid 100, Bottom 300, Robe 400) | later spec (composition) |
| `_2dlayertoHead_list`, `BigLayerDir_*`, `SmallLayerDir_*`, `LAYER_ALL_TABLE_LIST` | `spreditinfo/*.lub` | per job, sex, action and frame: which layers swap order (weapon/garment behind or in front) | later spec (composition) |
| `SetHeadSprEditInfo(...)` | `ridingspreditinfo/ridingspreditinfo.lub` | per job and action: head offset correction when riding | later spec (composition) |
| `OffsetItemPos` | `offsetitempos/offsetitempos.lub` | per headgear id, direction, sex (and doram): pixel offset correction | later spec (composition) |
| `transparentItemlist` | `transparentitem/transparentitem.lub` | headgear id → alpha | later spec |
| `hatEffectTable` | `hateffectinfo/hateffectinfo.lub` | hat effect id → STR effect file | out of scope (STR) |
| `ET_*`, `EMOTION_ORDERLIST` | `emotion/emotionlist.lub` | emote id (`ET_SURPRISE = 0` … `ET_YUT7 = 87`) | emote action index (§3.10) |
| `SKID` | `skillinfoz/skillid.lub` | constant → **skill id** (`SM_BASH = 5`); the lower-case constant is the icon file name | skill icons (§3.2b) |
| `effectid`, `skilleffectinfo*`, `effecttool/*` | various | skill effects (STR/effect tool) | out of scope |

**Not in any lub file:**

- **Player job id → body sprite name** is hard-coded in the client executable (`PCJobNameTable` is empty). The names are known from the folders (§3.3). Open question OQ-1.
- **Shield view id → shield name** has no table either; the four basic ones follow from `itemInfo` (§3.7).

## 2. Client `System` folder (`GRF Resources~/System/`)

Lub files here are the client's own, outside the GRF. Same decompiler; **text encoding differs per file** (below), so the reader must be told the encoding per file.

### 2.1 itemInfo — Confirmed

| File | Items | Max id | Text encoding | Language |
|---|---:|---:|---|---|
| `iteminfo_new.lub` | 17,482 | 1,220,004 | UTF-8 | Portuguese (Brazil) |
| `english/iteminfo_new.lub` | 17,906 | 1,220,004 | UTF-8 | English |
| `spanish/iteminfo_new.lub` | 17,905 | 1,220,004 | UTF-8 | Spanish |
| `iteminfo_new_.lub`, `iteminfo_sak.lub`, `System/iteminfo.lub` | 16,035–16,712 | 1,220,000 | CP949 | older copies |
| `itemInfo.lub` | 0 | — | — | empty stub |
| `itemInfo.lua` (text) | — | — | CP949 | older copy |
| `iteminfo_f.lub` | — | — | — | loader: `main()` calls `AddItem(...)` for every entry |

`tbl[itemId]` fields (all items have the first six):

| Field | Meaning | Use |
|---|---|---|
| `identifiedResourceName` | Korean resource name; **ground sprite** `아이템/<name>.spr` (17,079 of 17,482 found) and the item's icon name | ground item lookup |
| `unidentifiedResourceName` | same, for an unidentified item | ground item lookup |
| `identifiedDisplayName`, `unidentifiedDisplayName`, `…DescriptionName` | localised name and description | UI (later spec) |
| `slotCount` | card slots | UI |
| `ClassNum` | **view id** of a visible equipment: weapon → `Weapon_IDs`, headgear → `ACCESSORY_IDs` (2,873 items match `AccNameTable` exactly), garment → `SPRITE_ROBE_IDs`, shield → 1 `가드`, 2 `버클러`, 3 `쉴드`, 4 `미러쉴드`. 0 = nothing visible. 5,044 items have one | equipment lookup |
| `costume` | costume item (2,253) | later spec |
| `EffectID` | hat effect id (1,110 items, `hatEffectTable`) | out of scope (STR) |

Examples: 501 Poção Vermelha → `빨간포션`, ClassNum 0; 1201 Faca → `나이프`, ClassNum 1 (short sword, suffix `_단검`); 1207 Main Gauche → ClassNum 31 (suffix `_1207`); 2220 Chapéu → ClassNum 16 (headgear); 2101 Vembrassa → `가드`, ClassNum 1.

**Item-id sprites (Candidate).** Some weapon and shield files are named by item id: `_1530` (Mjolnir, ClassNum 8) and `28901_방패` (ClassNum 1). The client appears to try the item-id file first and fall back to the ClassNum name; decision (OQ-3): view id first, then item id, then base type; still to be checked against the server's look packets.

### 2.2 Other System tables

| Table | File | Maps | Used for |
|---|---|---|---|
| `tbl[mobId] = { MonsterSize, MonsterEff }` | `monster_size_effect_new.lub` (and older `monster_size_effect.lub`) | monster id → **display scale** (for example 1.2) and attached effect ids (`EFFECT.EF_*`) | monster scale: this spec; effects: later spec |
| `mapTbl` (+ `_data`, `_enUS`, …) | `mapInfo*.lub` | map file → display name, loading background, enter notice | later spec (maps/UI) |
| `mapNPCInfoTable` | `Towninfo.lub` | town → NPC markers (name, x, y, type) | later spec (minimap) |
| quests, achievements, tips, attendance, pet evolution, airship | `OngoingQuestInfoList*`, `achievement_list`, `tipbox*`, `CheckAttendance`, `PetEvolutionCln`, `PrivateAirplane_true` | UI and game data | out of scope |
| `CmdOnOffList`, `SetupMSG` | `LuaFiles514/OptionInfo`, `MsgString` | client options and their texts | out of scope |

## 3. Sprite categories (`data/sprite/`)

Counts are SPR files; "unique" counts distinct SPR contents (MD5).

| Folder | SPR | Unique | ACT | Category |
|---|---:|---:|---:|---|
| `몬스터/` | 1,493 | 1,322 | 1,522 | monsters (and pet-accessory ACTs, §3.1) |
| `npc/` | 1,282 | 1,190 | 1,282 | NPCs |
| `homun/` | 21 | — | 21 | homunculi (`lif`, `filir`, …) and mercenaries (`mer_*`) |
| `인간족/몸통/` | ~548 | — | 548 | human player bodies |
| `인간족/머리통/` | 85 | — | 85 | human heads (hair styles) |
| `인간족/<job>/` | ~5,300 | — | — | human weapons per job |
| `도람족/` | 50 | — | 50 | doram bodies, heads and weapons |
| `로브/` | 60,438 | **294** | 80,963 | garments (3.9 GB of ACT) |
| `악세사리/` | 4,812 | 2,830 | 4,810 | headgears |
| `방패/` | 1,320 | **29** | 1,319 | shields per job |
| `아이템/` | 9,778 | 8,783 | 10,200 | items on the ground |
| `이팩트/` | 225 | — | 234 | sprite effects, emotes, carts, falcon, status icons, damage numbers |
| `book/`, `item/` | 28 | — | 28 | book UI animation; bullets and ammo |
| `sprite/`, `texture/`, `몸통/`, `악세서리/`, `여/`, `아이템/몸통`, `아이템/악세사리` | ~30 | — | — | stray or legacy copies (OQ-6) |

### 3.1 Monsters, NPCs, homunculi, mercenaries — Confirmed

- **Id:** a job id from `jobtbl` (`npcidentity.lub`). **File name:** `JobNameTable[id]` (`jobname.lub`), case-insensitive.
- **Folder by id range** (checked against all 5,300 names):

| Id range | Folder |
|---|---|
| 45–999, 10001–19999 | `npc/` |
| 1001–3999, 20000+ | `몬스터/` |
| 6001–6099 (homunculi, mercenaries) | `homun/` |

- 194 names exist in both `npc/` and `몬스터/` (151 byte-identical, 43 different): the **id range decides**, never the presence of the file.
- 319 names have no SPR: Granny 3D models (`*.gr2`, for example `Guildflag90_1.gr2`) and sprites of other regions. They get one "missing" warning each.
- 29 ACTs in `몬스터/` have no SPR (`poring_책가방.act`, `deviruchi_젖꼭지.act`, …): **pet accessories**. Out of scope; skipped with one warning.
- **Actions** (counts over all files): monsters are mostly 40 (5 animations × 8), then 64, 8, 48, 56, 72, 10, 16, 1. NPCs are mostly 8 (1 animation × 8), some 40. Homunculi 64 or 56. Layout in [SpriteAction](../../data/SpriteAction.md).

### 3.2 Ground items — Confirmed

- `아이템/<resource name>.spr` + `.act`; names are Korean (`사과`) or English (`c_divine_phoenix`).
- **One action, one direction** (1,439 of 1,440 sampled); usually one frame, one layer, no anchors.
- 60 ACTs without SPR and 6 SPR without ACT (typos such as `bs_enchntedstone`): orphans, one warning each.
- **Lookup by item id:** `itemInfo[id].identifiedResourceName` (or `unidentifiedResourceName`), §2.1; 17,079 of 17,482 items have a ground sprite. 7,491 distinct resource names, so many items share one sprite.

### 3.2b Skill icons — Confirmed

- Static skill icons are SPR in the same folder as ground items: `아이템/<skill name>.spr`, `<skill name>` = the `SKID` constant in lower case (`SKID.SM_BASH = 5` → `아이템/sm_bash.spr`), from `luafiles514/lua files/skillinfoz/skillid.lub`.
- 1,295 of 1,806 `SKID` entries have an icon; the rest are mostly monster skills (`NPC_*`), which have none.
- One action, one frame, like ground items. They are found by skill id, not item id.
- Item icons for the UI (`texture/유저인터페이스/item`, `collection`, `illust`) are BMP textures, not sprites; those folders are empty in this export and belong to a UI spec.

### 3.3 Player bodies — Confirmed

- `인간족/몸통/<sex>/<jobname>_<sex>.spr`, sex = `남` or `여`. Doram: `도람족/몸통/<sex>/<jobname>_<sex>.spr`.
- `<jobname>` is Korean for the classic jobs (`초보자` novice, `기사` knight, `페코페코_기사` knight on peco) and English for the newer ones (`cardinal`, `inquisitor`). Variants are separate names: mounts (`<job>_riding`, `사자…`, `타조…`, `켈베로스…`, `…알파카`, `…멧돼지`, `…포링`), outfits (`결혼` wedding, `산타`, `여름` summer, `한복`, `턱시도`, `옥토버패스트`), `_h` (transcendent alternate), `madogear1/2`, `frog_*`.
- `costume_1` … `costume_4` subfolders hold alternate body styles (`arch_mage_남_1.spr`): 102 files per sex.
- A few files break the pattern (`rebellion_남_권총.spr` weapons in the body folder, `로드나이트_남''.spr`, `_여` folder): reported, not imported (OQ-6).
- **Bodies contain only the body.** The head is a separate sprite (§3.4).
- **104 actions** (13 animations × 8 directions), one layer per frame, **one anchor** per frame (the head attach point), no true-colour frames.

### 3.4 Heads (hair styles) — Confirmed

- `인간족/머리통/<sex>/<n>_<sex>.spr`, n = hair style number. Doram: `도람족/머리통/<sex>/`.
- 104 actions; **two layers per frame**, the first is always the empty index −1; one anchor per frame.
- Hair colours are palettes (§3.9).

### 3.5 Headgears — Confirmed for 79 %

- **Id:** headgear view id (`ACCESSORY_IDs`); **stem:** `AccNameTable[id]` without the leading `_`.
- **File:** `악세사리/<sex>/<sex>_<stem>.spr` (2,266 of 2,859 ids found). Doram: `악세사리/<sex>_doram/<sex>_<stem>.spr` (85 ids have a doram file; others use the human file).
- 593 ids have no file (regional items): warning, skipped.
- **Lookup by item id:** `itemInfo[id].ClassNum` is the headgear view id (§2.1).
- The folder also has `<stem>_<sex>.spr` files that `AccNameTable` does not reference (a few dozen files); they are reported as unreferenced.
- 104 actions (some 1 or 40), one layer, one anchor (attach to the head's anchor). 109 files have true-colour frames.
- Male and female files are often identical (4,812 SPR, 2,830 unique).

### 3.6 Garments — Confirmed

- **Id:** garment view id (`SPRITE_ROBE_IDs`, = `itemInfo[id].ClassNum`); **folder:** `RobeNameTable[id]` (`천사날개`). Use `RobeNameTable`, not `RobeNameTable_Eng` (English names that do not match the folders).
- **Layout:** `로브/<folder>/<sex>/<jobname>_<sex>.act` per job, using the **body job names** of §3.3 (one file per job, mount and outfit).
- At the folder root: `<folder>.spr` + `<folder>.act` (common sheet) and usually `<folder>_doram.spr` (doram sheet).
- **20,901 job ACTs have no SPR of their own.** They use the root sheet (`<folder>.spr`, or `<folder>_doram.spr` for doram jobs): checked on `천사날개`, where every job ACT indexes only the 9 images of the root sheet.
- Where a job SPR exists it is almost always a **byte copy** of the root sheet (`천사날개`: 139 of 178 job SPR identical to the root; all of `로브/`: 60,438 SPR, 294 distinct).
- The job **ACTs are not copies** (80,963 ACT, 66,897 distinct): each job moves differently, so the animation is per item × job × sex, while the image sheet is shared.
- 104 actions, one layer, one anchor.

### 3.7 Shields — Confirmed

- `방패/<job>/<job>_<sex>_<name>.spr`, name is a shield type (`가드` guard, `버클러` buckler, `쉴드` shield, `미러쉴드` mirror shield, `매드바니`) or `<item id>_방패` (`2189_방패`).
- **Lookup by item id:** `itemInfo[id].ClassNum` 1 `가드`, 2 `버클러`, 3 `쉴드`, 4 `미러쉴드` (Confirmed for the basic shields 2101–2108). No lub table holds this list: it is a small hand-made table (OQ-1).
- Item-id files (`28901_방패`, `2189_방패`, `460020_방패`) and `매드바니` belong to items whose ClassNum is 1: decision (OQ-3): view id first, then item id, then base type.
- 1,320 SPR, **29 distinct**: one sheet per shield, the ACT differs per job.

### 3.8 Weapons — Confirmed

- `인간족/<job>/<job>_<sex><suffix>.spr`; doram `도람족/<job>/`. `<suffix>` = `WeaponNameTable[view id]`: a type (`_검` sword, `_단검` dagger, `_롯드` rod, `_활` bow, `_카타르_카타르` katar, dual wield `_단검_검`) or an item id (`_1207`).
- `…<suffix>_검광.spr` and `<job>_<sex>_검광.spr` are the **weapon trail** ("sword light") drawn during attacks; `…광` variants (`도끼광`, `권총광`) likewise.
- **Lookup by item id:** `itemInfo[id].ClassNum` is the weapon view id (§2.1). Unknown specific id → `Expansion_Weapon_IDs[id]` gives the base type to try next. Item-id files such as `_1530` (Mjolnir, ClassNum 8) suggest the item id is tried early; decision (OQ-3): view id first, then item id, then base type.
- Weapon folder names are the base job (`검사` swordman, `inquisitor`); mounts reuse the job folder (`페코페코_기사` is its own folder).
- 104 actions; frames without a weapon are layer index −1; scale and tint are set on every file sampled.

### 3.9 Palettes (`data/palette/`) — Confirmed

| Folder | Files | Pattern | Use |
|---|---:|---|---|
| `머리/` | 749 | `머리<style>_<sex>_<colour>.pal` | hair colour for head `<style>` |
| `몸/` (+ `costume_1..4`) | 3,375 | `<jobname>_<sex>_<colour>.pal` | cloth colour for body `<jobname>` |
| `도람족/머리`, `도람족/body` | 340 | same patterns | doram |

A palette replaces the 256-colour table of an indexed SPR; index 0 is transparent.

### 3.10 Effects and emotes (`이팩트/`) — Candidate

- Plain sprite effects: `emotion.spr` (98 actions, one per emote, no directions: action index = `ET_*` id, Candidate), carts (`손수레1..8`, `마도카트`, …), falcon and wolf (`매`, `windhawk_wolf`), status (`status-sleep`, `status-stun`), damage numbers (`숫자`, `newnumber*`), traps, firewall, `shield`, and so on.
- Action counts vary (1, 8, 5, 16, 98 …); many use true-colour frames and several layers.
- Skill effects mostly live in STR files (`data/texture/effect`), out of scope.

## 4. Consequences for the import (input to the plan)

1. **Deduplicate images by content.** Hash the SPR (after the palette is applied); identical sheets are imported once. Garments go from 60,438 SPR to 294 sheets, shields from 1,320 to 29.
2. **Equipment = shared images, per-class animations.** An equipment item has a common sheet, a doram sheet and occasional per-class sheets; the animations (ACT) are per class and sex and are the bulk of the data (3.9 GB of garment ACT).
3. **Deduplicate actions** inside a file (and, where cheap, identical ACTs across files): about half of all actions are repeats.
4. **Lookups need the lub tables** of §1, `itemInfo` (§2.1) and two small hand-made tables (player job names, shield names; OQ-1).
5. **Monster display scale** comes from `monster_size_effect_new.lub` (§2.2).
6. **Tolerate gaps:** missing animations, missing SPR (shared root sheet), orphans, regional names without files.
