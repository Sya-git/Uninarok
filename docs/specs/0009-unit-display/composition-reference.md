# Composition reference: how the client puts a unit together

| | |
|---|---|
| **Belongs to** | [SPEC-0009](spec.md) (reference material, normative for its topic) |
| **Status** | Draft (2026-11-22, T0.2) |
| **Data set** | `Uninarok-client/Assets/Editor/GRF Resources~/data/luafiles514/lua files/` (LATAM client), decompiled with GRF `Lub.Decompile()` through `RoLubReader` |
| **Other evidence** | the client's own accessor functions (`*_f.lub`, quoted below), the imported ACT data, roBrowserLegacy `EntityRender.js` (another client, named **roBrowser** here), Hercules `unit.cpp` |

**Confirmed**: read in the data or in the client's own function. **Candidate**: consistent with the data, not yet compared with the reference viewer (owner, T8.1). Nothing here was compared with the original client or the reference viewer in T0.2: neither runs on the implementer's side.

Naming: *file action* = animation × 8 + shown direction; *frame* = the client's "motion"; *shown direction* 0 to 7 = the ACT's direction.

## 1. Directions (FR-6)

| Claim | Status | Evidence |
|---|---|---|
| Shown directions are 0 front, 1 front-left, 2 left, 3 back-left, 4 back, 5 back-right, 6 right, 7 front-right (screen left and right, as the viewer sees it) | Confirmed | `offsetitempos.lub`: `OffsetItemPos.Direction = { FRONT = 0, FRONTLEFT = 1, LEFT = 2, BACKLEFT = 3, BACK = 4, BACKRIGHT = 5, RIGHT = 6, FRONTRIGHT = 7 }`; body images (seen from behind at 4) |
| Network directions are 0 North, then counter-clockwise (1 north-west, 2 west, 4 south, 6 east) | Confirmed | Hercules `dirx = {0,-1,-1,-1,0,1,1,1}`, `diry = {1,1,0,-1,-1,-1,0,1}` |
| At the default camera (looking north) **shown = (4 − network) mod 8** | Confirmed by the two rows above | a unit facing south (4) shows its front (0); west (2) shows left (2); east (6) shows right (6) |
| The camera's turn is subtracted: **shown = (4 − network − sector) mod 8** | Confirmed (geometry) | a camera turned 90° looks east, so a unit facing south shows its right side (6) |

The two orders run in **opposite** senses: no constant offset turns one into the other (PLAN-0009 D15).

## 2. Tables

### 2.1 `datainfo/tb_layer_priority.lub` — headgear draw priority (FR-3)

`TB_Layer_Priority`: `Default_Mid = 100`, `Default_Top = 200`, `Default_Bottom = 300`, `Default_Robe = 400`; `Items_List[ACCESSORY_IDs.X] = { Default = n, Direction = { [0..7] = n }, isIgnoreRiding = true }`.

- `Default` from −300 to 404; `Direction` filled on 2 items (value −100 on front-facing directions 0, 1, 2, 6, 7), empty on 439; `isIgnoreRiding` on 83. **Confirmed** (shape and counts).
- Lookup (client function): `Direction[direction]` if given, else `Default`, else by layer: 2 bottom, 3 top, 4 mid, 8 robe. **Confirmed**.
- Parts are drawn in ascending priority: mid, top, bottom, robe, then items above 400; a **negative** priority is drawn behind the body. **Confirmed** for the order of the defaults (roBrowser draws mid, top, bottom, robe); **Candidate** for "negative = behind the body".
- `isIgnoreRiding`: the item's priority is ignored while riding (the layer default is used). **Candidate**.

### 2.2 `datainfo/spriterobename.lub` `RobeTopLayer` — garments on the top layer (FR-3)

A list of garment view ids, read by `IsTopLayer(RobeID)`.

- It does **not** put the garment over the body in every direction: drawn over the body facing front, the adventurer's backpack (view 2) and the bear bag (60) cover the chest (composite image of T0.2). PLAN-0009 §4.5 rule 3 was wrong (D16).
- **Candidate** rule used: when §2.3 puts the garment in front, a listed garment is drawn above every headgear; another one takes the robe priority (400). Only for views from behind: see §2.3.

### 2.3 `spreditinfo/_new_biglayerdir_{male,female}.lub` — garment behind or in front (FR-3)

`_New_BigLayerDir_M/F[jobId][fileAction] = { frames }`. Client function `_New_DrawOnTop(robe, sex, job, action, motion)`: the garment is **on top unless its frame is listed**, in which case it is behind. **Confirmed** (the function's text).

- Job inheritance (`LAYER_ALL_TABLE_LIST` in `_new_2dlayerdir_f.lub`: baby, high, riding, outfit → base job) is applied first. **Confirmed**.
- A job with no table, or an action not listed, is on top in the client. **Confirmed** (text). A job with **no** table uses the default of §3 here (behind when facing front), so a garment never covers a chest for a class the client does not list.
- Size: `GetLayerSizeType` returns `LAYER_BIG` for every garment (`LayerSizeTypeList` has one entry, also big): the `smalllayerdir` tables are never read. **Confirmed**; only the big tables are imported (D16).
- The old tables (`biglayerdir_*`, `smalllayerdir_*`, `2dlayerdir_f.lub`) have the same shape and are superseded by the `_new_` set. **Candidate** (both are in the data; the new one lists the third classes).
- In front of the body, but the view shows the face: the garment is **under the head** (D26). Directions 0, 1, 7, and 6 except in idle, walk and sit, where 6 is a pure side like 2. Every other animation has two groups of views, 0, 1, 6, 7 (front) and 2 to 5 (behind), and the rows of the tables repeat inside each group. **Confirmed** by the owner for the acolyte (Attack2, frames 5 to 8); **Candidate** for the other classes (the same rows).
- Sex: `SEX_FEMALE = 0`, `SEX_MALE = 1`, as `AccountSex`. **Confirmed**.

### 2.4 `spreditinfo/_2dlayer_headtobody.lub` — garment under the head (FR-3)

19,801 rows `{ sex, job, garment view id, file action, frame }`, the last one a terminator `{10,0,0,0,0}`; 19,009 distinct with it. Jobs: 0 (novice) and 4218 (summoner) only; garments 1–21 and 23–25; actions are the back-facing directions (3, 4, 5, and 2 to 5 for pick up, standby, attack, hurt and cast). **Confirmed** (shape: each value range fits only that column).

Meaning used: in those frames the garment is drawn above the body but **under the head**. **Candidate** (from the name: the 2D layer goes from head level to body level). It is the garment that moves, not the head (D16).

### 2.5 `ridingspreditinfo/ridingspreditinfo.lub` — riding corrections (FR-4)

Calls `SetHeadSprEditInfo(job, firstAction, lastAction, firstMotion, lastMotion, x, y)` and `SetBodySprEditInfo(...)`, plus `SetBodySprEditInfo_THIEFS()` / `_ACOLYTES()`, whose bodies (`_f.lub`) call `SETA` (y 15), `SETB` (y 5 to 15) and `SETC` (y 10) for a list of jobs. Jobs are both the mount ids (4124 Poring novice, 4142 Cerberus rogue) and the class ids (0 novice, 17 rogue), so they apply to riding units. **Confirmed** (columns, from the setter's text).

**Candidate, not applied** (D17): the values contradict our mount bodies. `(novice, action 4, frames 0–2, y 35)` would move the head 35 px on a body whose anchors are right (Poring novice action 4: (11, −78), (−10, −78), (11, −78), the same 4 to 5 px above the plain novice's as in the other directions); the Cerberus bodies already reach 10 to 28 px below the origin, and 15 more would sink them. The direct calls are imported and the composer can apply them, off by default until the owner sees what the original client does with them. The two group calls carry no arguments (their bodies are skipped, D4) and are counted as a note.

### 2.6 `offsetitempos/offsetitempos.lub` — headgear offsets (FR-4)

`AddOffsetForDoram(viewId, direction, { x =, y = } [, sex])`: sex 1 male, 2 female, absent both. Client function: the first matching row; x, y in pixels, y down. **Confirmed**. **Doram only**; there is no human table. `AddUppingBottomItem(viewId [, near, far])`: calls with no values: bottom headgears drawn higher for dorams. **Candidate, not used** (the order it changes is not known).

### 2.7 `transparentitem/transparentitem.lub` — transparent garments

122 rows `{ id, a, b, c }` and a terminator. The ids are **garment** view ids, not headgears (1 to 120; the only rows other than `255, 255, 25500` are 21 *Blue Fairy Wing* `255, 255, 2000`, 36 *Gold Butterfly Wing* and 38 *Pink Butterfly Wing* `100, 200, 25500`). **Candidate**: alpha pulses between a and b over c; the mean is used as a constant alpha (the pulse belongs to the effects spec, D18).

### 2.8 `datainfo/shadowtable.lub` — shadow scale (FR-11)

`ShadowFactorTable[jobtbl.X | JTtbl.X] = scale`; 987 rows, 67 zeros, others 0.2 to 5; `ReqshadowFactor` returns −1 for a job not listed = default 1. **Confirmed**. First baby classes have 0.75, second and third baby classes **0.82**; mounts 1.5 to 2; fabre 0.5; warp NPC 0; red plant 0.

Baby classes: the **body** scale is 0.8 (roBrowser `EntityView`: size 4 where other classes have 5; **Confirmed** by the owner's size comparison image, 2026-11-22), and the shadow's is this table's times that (roBrowser draws the shadow at the entity's size). The table's 0.75 and 0.82 are shadow factors only. Baby, high and transcendent classes take their sprites from the base job of `LAYER_ALL_TABLE_LIST` (§2.3).

### 2.9 Shadow sprite

`sprite/shadow.spr` + `.act` at the root of the sprite folder, **not** in `이팩트/`, so the catalog has no effect of that name (D12 corrected, D20). The Composition import converts it and stores its reference. roBrowser hides it while sitting or dead (**Candidate**, followed).

## 3. Default order (no table needed)

Back to front. **Candidate** (roBrowser; the client hard-codes it).

| Facing | Order |
|---|---|
| Front and sides (0, 1, 2, 6, 7) | shadow, (shield at 2), garment, headgears below 0, body, head, mid, top, bottom, weapon, trail, shield |
| Back (3, 4, 5) | shadow, shield, headgears below 0, body, head, mid, top, bottom, garment, weapon, trail |

- Shield: behind the body for directions 2 to 5, else last.
- Weapon and trail: after the head in every direction (BACKLOG: facing front the sword is in front of the head).
- Garment: behind for directions 0, 1, 2, 6, 7; in front for 3, 4, 5, until §2.3 says otherwise for the job.

## 4. Attachment (FR-4)

Head: placed at `bodyAnchor − headAnchor` (anchor 0 of the frame shown of each). Headgears: the same with their own anchor 0 (the head's anchor lands on the body's once the head is placed). Garment, weapon, trail and shield have no attachment: they are drawn at the unit's origin. **Confirmed** by SPEC-0007 T7.1 (head) and roBrowser (headgears).

## 5. Frames (FR-5, FR-8)

| Rule | Status | Evidence |
|---|---|---|
| In idle and sit the body, head, weapons and shields have 3 frames, one per head facing (straight, right, left): the frame **is** the head facing | Confirmed for the counts (61 of 61 bodies, 42 of 42 heads, every weapon and shield sampled); Candidate for which of frames 1 and 2 is right | anchors; roBrowser returns `headDir` |
| Animated headgears and garments have 24 or 48 idle frames: three groups, one per head facing, played inside the group | Candidate | 28 of 189 headgears and 1 of 28 garments sampled; roBrowser divides the count by 3 |
| Inside a group the frame follows the body's idle frame time | Decision (FR-5: the body's clock) | — |
| Every other animation: frame of the body, modulo the part's own frame count | Candidate (R2) | roBrowser wraps; walk counts agree in every sampled file |

## 6. Job ids drawn as nothing (FR-2)

- Warp NPC 45, hidden NPC 111, hidden warp NPC 139 (45 and 111 share the placeholder sprite `1_ETC_01`). **Candidate** (spec FR-2; names `JT_WARPNPC`, `JT_HIDDEN_NPC`, `JT_HIDDEN_WARP_NPC`).
- Every job id whose sprite name is `INVISIBLE` (hidden monsters 2337, 2343, 2495, 2536, 20395, 20562, …). **Confirmed** names. `JT_HIDDEN_TEST` (3296) has the Poring sprite and is shown.

## 7. Reference set

| Member | Ids |
|---|---|
| Novice male | `JOB_NOVICE` 0 |
| Swordsman male, full set | `JOB_SWORDMAN` 1; hair style 2, hair colour 3; top headgear item 2220 (view 16), low headgear item 2218 flu mask (view 8); garment view 1 (`천사날개`); sword item 1101 (view 2); shield item 2101 (view 1) |
| Knight on peco female | `JOB_KNIGHT2` 13 |
| Summoner male (doram) | `JOB_SUMMONER` 4218 |
| Monsters | Poring 1002 (72 actions), Lunatic 1063 (64 actions) |
| Town NPC | Kafra 117 (`4_F_KAFRA1`, 16 actions) |
| Shadow checks | warp NPC 45, red plant 1078, fabre 1007 |

## 8. Import result

`Tools/Ragnarok/Catalog/Composition` on the full data (T1.4, 2026-11-22): `Created 1, Warnings 0` in 8.6 s, then `Skipped 1` in 3.6 s; `composition.asset` is 0.7 MB (text).

| Table | Rows |
|---|---:|
| Headgear priorities | 568 (2 with directions, 83 ignored while riding) |
| `RobeTopLayer` | 226 |
| Layer swaps | 15,482 over 284 (job, sex) tables; 192 inherited jobs |
| Head to body | 3,936 (sex, job, garment, action) rows with frame masks |
| Head edits / body edits | 20 / 4 direct calls |
| Doram headgear offsets | 135 |
| Transparent garments | 2 |
| Shadow scales | 987, of which 67 are 0 |
| Hidden job ids | 27: 45, 111, 139 and 24 `INVISIBLE` ids |
| Ground shadow | `sprite/shadow`, static (one frame), `sprite/_sheets/0d/shadow_0dbb9009.png` |

`JOBID` is defined in `skillinfoz/jobinheritlist.lub` and `pcJobTbl` in `admin/pcidentity.lub`; `JTtbl` must also answer the NPC constants (`JTtbl.JT_EDDGA3` in the shadow table).
