# PLAN-0011: Combat

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0011, approved 2026-11-22) |
| **Status** | Approved (owner, 2026-11-22) |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. Packets and flow are [PLAN-0001](../0001-networking-core/plan.md) (packet rules normative in [Packet-Implementation.md](../0001-networking-core/Packets/Packet-Implementation.md)); animations and unit display [PLAN-0009](../0009-unit-display/plan.md); the world model, the server clock, the own character, picking and the presenter [PLAN-0010](../0010-world-session/plan.md). This plan adds four things to them: **combat state in the world model**, **the own character's combat rules**, **a timeline that turns each blow into motions, numbers and projectiles**, and **what draws those**.

## 1. Constraints and assumptions

- **Builds on SPEC-0010** (merged into `main`, `97162719d`). Where this plan and the merged code differ on a signature, the code wins and the difference goes under Decisions (as PLAN-0010 §1).
- **No new third-party library.** UI Toolkit (`com.unity.modules.uielements`) is already in the manifest.
- **Pure core, thin components** (PLAN-0010 D1): range, approach, timeline, number motion, projectile choice and the restart rules are plain C# with no scene. Components read and draw.
- **Two times.** The model holds what the server said, when it said it. The timeline holds what is *shown*, which is later (a hit moment is after the blow's packet). Nothing in the model waits for the display (D2).
- **Hercules facts** (`Hercules-2026.09/src/map`, read 2026-11-22) beyond those in spec §6:

| # | Fact | Where | Effect here |
|---|---|---|---|
| H1 | An attack also needs a **line of sight**: `path_search_long(…, CELL_CHKWALL)` from the attacker's to the target's cell. When it fails the server drops the attack and **sends nothing** | `unit_attack_timer_sub`, `battle_check_range` | The client's range rule includes it (§4.4); a continuous attack that goes silent is asked for again (§4.5) |
| H2 | `CELL_CHKWALL` = neither walkable nor shootable; a cliff (type 5) does not block | `map_getcellp`, `map_gat2cell` | `IWalkGrid.BlocksSight` (§4.4) |
| H3 | The range is tested on full cells: `(int)(sqrt(dx² + dy²) − 0.0625) ≤ range`, `range + 1` while the target walks or for a request the server kept while the player walked (`step_attack`); targets more than 14 cells away (square) are refused | `path.cpp` `distance_client`, `battle_check_range` | §4.4 |
| H4 | A request while the player walks and not within `range − 1` is kept and run at the next full step, then tested with `range + 1`. A walk request deletes a kept one | `unit_attack`, `unit_steptimer`, `unit_walk_toxy` | The client may ask as soon as its own server cell is in range, also while walking, and sends no walk after the request (§4.5) |
| H5 | A walk request during the walk delay after a blow (`attack_walk_delay`: the attacker's `amotion`) is **kept by the server** and run when the delay ends (up to 2 s); every accepted walk stops the attack | `unit_walk_toxy` (flag 4), `unit_stop_attack` | The client sends the walk at once and buffers nothing (§4.5) |
| H6 | A continuous attack ends silently when: no line of sight, weight ≥ 90 %, warg riding, a status that forbids attacking. With no arrows the server answers `ZC_ACTION_FAILURE` `0x013B` (type 0) | `unit_attack`, `pc_can_attack`, `battle_check_arrows`, `clif_arrow_fail` | Watchdog with a limit (§4.5); `0x013B` ends the attack |
| H7 | An attack sends `UNIT_STOP_MOVE` for the player first (`clif_fixpos`) when it was asked for (not for the repeats) | `unit_attack_timer_sub` | Already handled (SPEC-0010 FR-7); it must not end a motion (§4.6) |
| H8 | Damage is applied `amotion` after the blow; for attackers that are not players `amotion` is capped at **1,000 ms** there (a constant in the code), while the packet carries the uncapped value. 268 of the 1,759 monsters of `db/re/mob_db.conf` have an attack motion above that (at most 2,000 after the server's limit). No packet says when the damage is applied: `DAMAGE` is sent once, at the start of the blow; after it only the new HP arrives | `battle_delay_damage`, `mob_db` | Shown by the packet's value, not by the cap (D3); a death that arrives earlier waits (§4.6) |
| H9 | A monster's death notice (`UNIT_VANISH` 1) is sent **250 ms after** the damage was applied; a player's at once. A monster that looks like a player class dies at once too, and vanishes (reason 0) 3 s later | `mob_dead` (`clearunit_delayed(…, tick + 250)`), `pc_dead` | A monster's hurt motion has about 250 ms before its death (§4.6, FR-9) |
| H10 | Each damage or heal of a monster below full HP re-sends `0x09FD` with `maxHP`, `HP`, the killing blow with HP 0. At full HP nothing is sent (a monster healed to full is **not** reported) | `mob_damage`, `mob_heal`, `clif_show_monster_hp_bar` | `HP` is taken from all three entry packets (§4.2); limit in §9 |
| H11 | Self damage (poison and others) is a damage packet with source = target, both times 0 and often `count` 0; `@heal` sends negative damage the same way; an absorbed blow is −1 | callers of `clif->damage` | §4.6 rules for `attackMT` 0, `count` 0 and negative damage |
| H12 | The restart answers `CZ_RESTART` 0 with a map change; with `ZC_RESURRECTION` when the warp fails | `pc_respawn` | §4.11 |
| H13 | `0x00B1` is never sent at this PACKETVER: EXP and zeny use `0x0ACB` | `clif_updatestatus` | Not implemented (§4.1) |

- **Client data** (extracted GRF, checked 2026-11-22): `sprite/이팩트/숫자` (digits 0 to 9 and two suffixes, 6 × 11 to 13 × 13 px) and `sprite/이팩트/msg` (0 miss, 1 guard, 2 critical, 3 critical background, 4 lucky background, 5 lucky; meanings from roBrowserLegacy) are there and **already imported** (effect category); `sprite/npc/skel_archer_arrow` (one frame, 8 × 61) is there and not imported yet; `sprite/cursors` action 5 is the attack cursor (lead). **The window textures of the restart prompt (`esc_*.bmp`) are not in the extracted data** and its label texts were not found in it (T0.4 asks the owner).
- **roBrowserLegacy** (read 2026-11-22: `Entity.js` `onEntityAction`, `Damage.js`, `EntityLife.js`, `WeaponAction.js`, `EquipmentRules.js`, `ArrowFlight.js`, `AttackEffectTable.js`, `Escape.html`) gives the first values of §4.6 to §4.9. They are **leads**: each is one setting or one table row, marked *(lead)*, and replaced by what the original client's footage shows (T0.3). A correction is a setting change plus a Decisions note, not a redesign.
- **Phase 0 checks** ([tasks.md](tasks.md)): **T0.1** packets of §4.1 against `packets_struct.h`, `clif.cpp` and captures; **T0.2** the plugin's new output (the fights are recorded in T1.5, when the packets exist); **T0.3** measurements on the owner's footage of the original client (shot list in §6.3); **T0.4** restart prompt: labels, images and layout from the original client. T0.3 and T0.4 depend on the owner and do not block the code phases; the evidence phase does not close without them.

## 2. Architecture

Satisfies FR-1 to FR-14.

```
 packets ─► WorldSession ──► WorldModel (Entity: + Hp, MaxHp, TargetId, LastBlow)  ◄── read ── WorldPresenter
               │  │  └─────► OwnStatus  (HP, SP, levels, EXP, range)               ◄── read ── CombatOverlay (bars), dev HUD
               │  └────────► CombatTimeline ◄── AttackMotions, ProjectileRules (pure tables)
               │               pending hits → unit motions, numbers, projectiles   ◄── read ── WorldPresenter (motions)
               │                                                                   ◄── read ── CombatOverlay (numbers, projectiles)
               └── event BlowReceived(in Blow) ────────────────────────────────────────────── dev HUD
 input: OwnInput ─► UnitPicker ─► OwnCombat (idle / approaching / attacking / fetching) ─► REQUEST_ACT, STOP_ATTACK, PICKUP_ITEM, REQUEST_MOVE
        RestartPromptView ─► RestartPrompt (pure) ─► RESTART
```

Per frame, in `WorldBootstrap.LateUpdate` (order 10, as before): `WorldModel.Advance(now)` → **`OwnCombat.Tick(now)`** → **`CombatTimeline.Advance(now)`** → `RestartPrompt.Update()` → `WorldPresenter.Sync` (base animation, or the timeline's motion when one plays). After the camera (`RoCamera` 50, brain 100) and the units (900): **`CombatOverlay` (950)** writes the number and bar meshes and places the projectiles with the final camera.

## 3. Assemblies and file layout

Satisfies FR-13 (placement) and the dev-only rule.

| Assembly | Path | Contents |
|---|---|---|
| `Uninarok.Networking` (changed) | `Assets/Scripts/Networking/Runtime/Packets/` | new packets (§4.1), `PacketIds` |
| `Uninarok.World` (changed) | `Assets/Scripts/World/Combat/` | `Blow`, `BlowKind`, `OwnStatus`, `AttackRange`, `OwnCombat`, `CombatTimeline`, `UnitMotion`, `DamageNumber`, `NumberMotion`, `Projectile`, `ProjectileRules`, `AttackMotions`, `IWeaponTypes`, `RestartPrompt`, `CombatSettings` |
| | `Assets/Scripts/World/` | `Entity`, `WorldModel`, `WorldSession`, `OwnCharacter`, `WalkGrid`, `HerculesPathfinder` (additions) |
| `Uninarok.Units` (changed) | `Assets/Scripts/Units/` | `UnitClock.FitMs`, `UnitView.PlayFor`, `UnitView.PickRect`, weapon types in `CompositionTables` |
| `Uninarok.World.View` (changed) | `Assets/Scripts/World/View/Combat/` | `UnitPicker`, `CombatOverlay`, `QuadMesh`, `DamageNumberMesh`, `UnitBarMesh`, `ProjectileView`, `CatalogWeaponTypes`; `OwnInput`, `MouseCursor`, `GroundItemView`, `WorldPresenter`, `WorldBootstrap` (additions) |
| `Uninarok.World.UI` (new) | `Assets/Scripts/World/UI/` | `RestartPromptView` (UI Toolkit). References `Uninarok.World`, `Uninarok.World.View`, `Unity.InputSystem` |
| `Uninarok.World.Dev` (changed) | `Assets/Scripts/World/Dev/` | `CombatDebugView`, `/noctrl` in `DevCommandLine` |
| RoImport (changed) | `Assets/Editor/RoImport/Core/Catalog/Composition/` | weapon types into the composition tables (§4.7) |
| Hercules plugin (changed, dev tool) | `Hercules-2026.09/src/plugins/uninarok_pathdump/` | HP in the view lines and a status line of the player (§6.2) |
| Tests | `Assets/Tests/EditMode/World/Combat/`, `EditMode/Networking/`, `EditMode/Units/`, `PlayMode/World/` | §6 |

Data: `Assets/Settings/World/CombatSettings.asset` (every value of §4.5 to §4.9, the sprite references: digits, messages, arrow); `Assets/UI/RestartPrompt.uxml`, `.uss`, `Assets/UI/GamePanelSettings.asset` (and the theme it needs); `Assets/Shaders/World/Overlay.shader` (§4.8); `docs/specs/0011-combat/combat-reference.md` (normative for display values: one row per value, with source and evidence; updated in the same commit as `CombatSettings`); `docs/specs/0011-combat/fixtures/` and `evidence/`.

## 4. Component contracts

### 4.1 Packets

Satisfies FR-1, FR-2, FR-3, FR-5, FR-8, FR-9, FR-10, FR-11, FR-12.

Sizes from the Hercules length table for 20220406. "Seen": how often in SPEC-0010's captures (`fixtures/world-*.bin`, counted 2026-11-22). Each is checked in T0.1 before its struct is written; docs change in the same commit.

| Dir | ID | Struct | Size | Hot | Doc | Layout after the ID, notes | Seen |
|---|---|---|---:|---|---|---|---|
| in | `0x08C8` | `DAMAGE` | 34 | yes | exists | source `uint`, target `uint`, tick `uint`, attackMT `int`, attackedMT `int`, damage `int`, isSpDamage `byte`, count `short`, type `byte`, leftDamage `int` (`packet_damage`) | 119 |
| in | `0x008A` | `UNIT_ACT` (changed) | 29 | warm | exists | adds `TargetId` `uint` at 6, **valid for action 1 only** (`clif_takeitem`; uninitialised for 2 and 3) | no |
| in | `0x0139` | `ATTACK_TOO_FAR` | 16 | warm | new | target `uint`, target x, y, own x, y `short`, range `short` (`PACKET_ZC_ATTACK_FAILURE_FOR_DISTANCE`) | no |
| in | `0x013A` | `ATTACK_RANGE` | 4 | cold | new | range `short` | 6 |
| in | `0x013B` | `ACTION_FAILURE` | 4 | cold | new | type `ushort`: 0 no ammunition (H6) | no |
| in | `0x00B0` | `PARAMETER_CHANGE` | 8 | warm | exists | vars read: 0 speed (SPEC-0010), 5 HP, 6 max HP, 7 SP, 8 max SP, 11 base level, 55 job level | 72 |
| in | `0x0ACB` | `LONG_PARAMETER_CHANGE` | 12 | warm | new | var `ushort`, value `long`: 1 base EXP, 2 job EXP, 22 next base, 23 next job; 20 zeny ignored | 28 |
| in | `0x0ACC` | `EXP_GAIN` | 18 | warm | new | id `uint`, amount `long`, var `ushort` (1 base, 2 job), quest `ushort` | 16 |
| in | `0x0148` | `RESURRECTION` | 8 | cold | new | id `uint`, type `ushort` (always 0) | no |
| in | `0x09FD`, `0x09FE`, `0x09FF` | unit entries | – | yes | exist | `MaxHp`, `Hp` already decoded; −1 = not shown | 7 with HP |
| in | `0x0B41` | `ITEM_PICKUP_ACK` | 70 | warm | exists | `Result` ≠ 0 is a failure; the result table goes into its doc (T0.1) | 1 |
| in | `0x0080`, `0x00A1` | vanish, item gone | 7, 6 | yes | exist | unchanged | 422, 69 |
| out | `0x0437` | `REQUEST_ACT` | 7 | warm | exists | new constants `Attack` 0, `AttackRepeat` 7 | – |
| out | `0x0118` | `STOP_ATTACK` | 2 | warm | new | no payload (`clif_parse_StopAttack`) | – |
| out | `0x0362` | `PICKUP_ITEM` | 6 | warm | exists | item object id `uint` | – |
| out | `0x00B2` | `RESTART` | 3 | cold | exists | type `byte`: 0 save point (1 is never sent) | – |

`UNIT_HP` (`0x0977`) gets a note in its doc: never sent at this PACKETVER (H10). `0x00B1` is not implemented (H13).

**T0.1 results** (2026-11-22). Sources: lengths from `src/common/packets/packets2022_len_main.h`; the client-to-server IDs from the `PACKETVER == 20220406` block of `src/map/packets_shuffle_main.h` (`PACKETVER_RE` and `PACKETVER_ZERO` are off in `mmo.h`); layouts from `packets_struct.h` or the sender in `clif.cpp`; samples: SPEC-0010's three `world-*.bin` walked packet by packet with that length table, all three to their last byte.

| ID | Size (table) | Layout source | Layout | Capture | Result |
|---|---:|---|---|---|---|
| `0x08C8` | 34 | `packet_damage` (`PACKETVER` ≥ 20131223: `is_sp_damaged` present; ≥ 20071113: `int32` damages), `clif_damage` | as §4.1 | 119 in prt_fild08: 118 type 0 and one type 11, all on the own character, `attackMT` 912, `attackedMT` 0, damage 0, count 1 | ok |
| `0x008A` | 29 | `clif_takeitem`: id at 2, item id at 6, action at 26 = 1; `clif_sitting`, `clif_standing` write only id and action into an uninitialised buffer | as §4.1 | none | ok |
| `0x0139` | 16 | `PACKET_ZC_ATTACK_FAILURE_FOR_DISTANCE` | as §4.1 | none | ok |
| `0x013A` | 4 | `PACKET_ZC_ATTACK_RANGE` | as §4.1 | 6, range 1 (bare hands) | ok |
| `0x013B` | 4 | `clif_arrow_fail`: type `uint16` at 2 | as §4.1 | none | ok |
| `0x00B0` | 8 | `PACKET_ZC_PAR_CHANGE`, `clif_updatestatus` | vars 0, 5, 6, 7, 8, 11, 55 as §4.1; HP of a dead player is `get_restart_hp` (spec FR-1) | 72; vars 5 to 8 seen | ok |
| `0x00B1` | 8 | `clif_updatestatus` uses it for zeny and EXP only under `#else` of `PACKETVER_MAIN_NUM >= 20170906` | not sent | none | ok (H13) |
| `0x0ACB` | 12 | `PACKET_ZC_LONGLONGPAR_CHANGE`; vars 1, 2, 20, 22, 23 | as §4.1 | 28; vars 1, 2, 22, 23 with values 0 to 8, 350 and 10 | ok |
| `0x0ACC` | 18 | `clif_displayexp`: id at 2, amount `int64` at 6, var at 14, quest at 16 | as §4.1 | 16, amount 1, vars 1 and 2, quest 0 | ok |
| `0x0148` | 8 | `clif_resurrection`: id at 2, type 0 at 6 | as §4.1 | none | ok |
| `0x09FD`, `0x09FE`, `0x09FF` | variable | SPEC-0010 | unchanged | 186, 35, 184 | ok |
| `0x0B41` | 70 | `PACKET_ZC_ITEM_PICKUP_ACK`: `result` at 33 | result codes below | 1: red potion (501) × 5, result 0 | **differs** from spec §6 (D13) |
| `0x0080`, `0x00A1` | 7, 6 | SPEC-0010 | unchanged | 422, 69 | ok |
| `0x0437` out | 7 | `clif_parse_ActionRequest`, shuffle `packet(0x0437, pActionRequest, 2, 6)`: target at 2, action at 6; actions 0 attack, 1 pickup, 2 sit, 3 stand, 7 continuous | as §4.1 | – | ok |
| `0x0118` out | 2 | `clif_parse_StopAttack` | no payload | – | ok |
| `0x0362` out | 6 | shuffle `packet(0x0362, pTakeItem, 2)` | item object id at 2 | – | ok |
| `0x00B2` out | 3 | `clif_parse_Restart`: type at 2, 0 respawn, 1 character select | as §4.1 | – | ok |

Pickup results (`ITEM_PICKUP_ACK.Result`, `clif_additem`): **0** success; on failure the packet is all zero but `Index` 2 and `result`. **6** from `clif_parse_TakeItem` for every refusal before the item is given: dead, item gone or on another map, hiding and similar states, cannot act, too far (`check_distance_bl` 2), permission, loot reserved for someone else. From `pc_additem` through `party_share_loot`: **1** invalid item or amount, **2** overweight, **4** no free inventory slot, **5** maximum amount, **7** stack limit (also bound items).

### 4.2 Model: `Entity`, `WorldModel`, `OwnStatus`, `Blow`

Satisfies FR-1, FR-4 (facing), FR-7 (values), FR-9, FR-12.

```csharp
public enum BlowKind : byte { Normal, Critical, LuckyDodge, MultiHit }       // a miss is a blow with no damage

public readonly struct Blow
{
    public readonly uint AttackerId, TargetId, At;       // At: ServerClock.Now when the packet was handled
    public readonly int AttackMs, HurtMs, Damage, LeftDamage;
    public readonly short Hits;                          // at least 1
    public readonly BlowKind Kind; public readonly bool Endured; public readonly byte WireType;
    public bool IsMiss { get; }                          // Damage == 0 && LeftDamage == 0 && Kind != LuckyDodge
    public static Blow From(in DAMAGE p, uint now);
}

public struct Entity   // added
{ public int Hp, MaxHp;            // −1: not known
  public uint TargetId;            // who it attacks; 0: nobody
  public Blow LastBlow;            // the last one it struck
  public bool Feigning; }          // Dead by VanishReason.FeignedDeath (Trick Dead)

public sealed class WorldModel    // added
{
    public void SetHp(uint id, int hp, int maxHp);       // hp or maxHp < 0: not known
    public void Strike(in Blow blow);                    // target, last blow, facing
    public void ClearTargets(uint targetId);             // everyone who attacks it
}

public sealed class OwnStatus     // lives in WorldSession, across maps
{
    public long Hp, MaxHp, Sp, MaxSp, BaseExp, JobExp, NextBaseExp, NextJobExp;
    public int BaseLevel, JobLevel, AttackRange;         // AttackRange 1 until the server says
    public int Version { get; }
}
```

- **`Blow.From`**: wire types 0, 5, 6 and unknown ones → `Normal` (unknown: one trace line); 4 → `Normal` endured; 8 → `MultiHit`; 9 → `MultiHit` endured; 10 → `Critical`; 11 → `LuckyDodge`; 13 → `MultiHit` (D12). `Hits = max(1, count)`. Negative damage is kept; the timeline shows nothing for it (H11).
- **`Strike`**: sets the attacker's `TargetId` and `LastBlow`; when attacker and target are both known, differ, and their cells differ, the attacker's `Facing` becomes `DirectionTo` the target's cell (the port SPEC-0010 uses for turns), head straight, one `Version` bump. It does not stop a walk and moves nobody. When one of the two is unknown, the other's part is still applied.
- **HP**: `SetHp` is called by the three entry handlers right after `Upsert`/`UpsertWalking` with the packet's values (−1 clears), and for the own id by `PARAMETER_CHANGE` (so the own entity equals `OwnStatus`). Death is **never** derived from it (FR-1). `0x09FD` for a known unit keeps working as PLAN-0010 says (a re-sent entry of a standing monster has source = destination and ends as `SameCell`).
- **`OwnStatus`** starts from the selected `CharInfo` at entry (HP, SP, levels, EXP; the next-level values and the range come from the server) and is updated by `PARAMETER_CHANGE`, `LONG_PARAMETER_CHANGE`, `ATTACK_RANGE`, and the range in `ATTACK_TOO_FAR`, also before the map is loaded. It is kept through map changes and reset where `WorldSession` resets `_entered`.
- **Death and life**: `Vanish(Died)` sets `State = Dead`, `Feigning = false`; `Vanish(FeignedDeath)` sets `Dead`, `Feigning = true`. `RESURRECTION` → `SetState(id, Standing)`. `Feigning` goes back to false whenever the state leaves `Dead`. Every `Vanish` calls `ClearTargets(id)`, and a unit that died has its own `TargetId` cleared (FR-9).
- Storage stays the fixed struct array of PLAN-0010 §4.5; `Entity` grows by about 60 bytes. `ClearTargets` walks the used slots (a rare event).

### 4.3 Session: handlers

Satisfies FR-1, FR-5, FR-8, FR-9, FR-10, FR-11.

`WorldSession` gets one `PacketHandler<T>` per new packet, under the rules of PLAN-0010 §4.6 (created once, no closures). `OnDamage`: `Blow.From` → `Model.Strike` → `Timeline.Add` → `Combat.OnBlow` → `BlowReceived`. `OnAct` with action 1: `Timeline.AddPickUp(id, itemId)`. `OnTooFar`, `OnActionFailure`, `OnPickupAck` → `OwnCombat`. `OnExpGain` raises `ExpGained` (HUD only). New members:

```csharp
public OwnStatus Status { get; }  public OwnCombat Combat { get; }  public CombatTimeline Timeline { get; }  public RestartPrompt Restart { get; }
public delegate void BlowHandler(in Blow blow);
public event BlowHandler BlowReceived;  public event Action<int, long> ExpGained;  public event Action<uint, byte> PickupAnswered;  public event Action<uint> TooFarAnswered;
```

### 4.4 Range rule: `AttackRange`, `IWalkGrid.BlocksSight`

Satisfies FR-3, FR-10, FR-13.

```csharp
public interface IWalkGrid { /* … */ bool BlocksSight(int x, int y); }     // not walkable and not shootable; true outside
public sealed class HerculesPathfinder { /* … */ public bool LineOfSight(int x0, int y0, int x1, int y1); }
public static class AttackRange
{
    public static int ClientDistance(int dx, int dy);       // (int)max(0, sqrt(dx²+dy²) − 0.0625), in double as path.cpp
    public static bool CanStrike(HerculesPathfinder paths, int ax, int ay, int tx, int ty, int range, bool targetWalks);
    public static bool CanPickUp(int ax, int ay, int ix, int iy);            // max(|dx|, |dy|) ≤ 2
}
```

- `CanStrike` = `ClientDistance ≤ range + (targetWalks ? 1 : 0)`, and `max(|dx|, |dy|) ≤ 14`, and `LineOfSight` (the existing `path_search_long` port with `BlocksSight` instead of "not passable"; it and `StraightLine` share one loop) (H1 to H3).
- Both cells are the model's `CellX/CellY`: server cells (spec FR-3). `UnknownWalkGrid.BlocksSight` is true, so without map data nothing is in range and nothing is sent.

### 4.5 Own combat: `OwnCombat`, changes in `OwnCharacter`

Satisfies FR-2, FR-3, FR-8 (input), FR-10, FR-13.

```csharp
public enum CombatState : byte { Idle, Approaching, Attacking, Fetching }
public enum AttackResult : byte { Sent, Approaching, NotInGame, Dead, Sitting, NotAttackable, NoPath }

public sealed class OwnCombat
{
    public CombatState State { get; }  public uint TargetId { get; }  public bool Continuous { get; }
    public bool NoCtrl { get; set; }                             // /noctrl: plain clicks are continuous
    public AttackResult Attack(uint id, bool continuous);        // click on a monster
    public bool PickUp(uint itemId);                             // click on an item
    public void Cancel();                                        // sit, map change, death: forgets, sends nothing
    public void Tick(uint now);
    internal void OnBlow(in Blow blow, uint now);  internal void OnTooFar(in ATTACK_TOO_FAR p, uint now);
    internal void OnActionFailure(int type);       internal void OnPickupAnswer(int result, uint now);
}
```

- **Attackable** (FR-2): `Kind == Monster` and not `Dead`. Players, NPCs, pets, homunculi, mercenaries, elementals, items and the own character are not (spec non-goals). Own character dead or sitting, not in game, target gone → nothing is sent.
- **`Attack`**: in range (§4.4) → send `REQUEST_ACT(id, continuous ? 7 : 0)`, state `Attacking`; else approach. Asking again for the current target with the same kind while `Attacking` sends nothing; another kind (single → continuous) sends.
- **Approach**: A* from the own server cell to the target's cell; the destination is the **first cell on that path from which `CanStrike`** holds for a standing target (so range 9 stops 9 away). An internal walk request with `CheckWalk`'s rules (length, occupied cell: then the next cell of the path is tried) that does not cancel the attack. No usable cell → `NoPath`, `Idle`.
- **`Tick` while `Approaching`**: target gone or dead → `Idle`. `CanStrike` now (own server cell, also while still walking, H4) → send the request, `Attacking`, no further walk. Otherwise ask for a new walk when the target's cell changed or the own walk ended, at most every `ApproachRepeatMs` (200).
- **Too far** (`0x0139` for the current target): → `Approaching` again, with a range one cell stricter for this target (`_caution`, at most `range − 1`, back to 0 at the next own blow), so a disagreement about a cell cannot loop on one spot (D4). For a single blow the request is repeated too, until it lands or the player does something else.
- **Own blow on the target** (`OnBlow`, attacker = own id): `_caution = 0`, the silence starts over. A single attack (`Continuous` false) → `Idle` after its blow.
- **Watchdog** (H1, H6): `Attacking` and no own blow for `max(AttackSilenceMs (1,500), 2 × last AttackMs + 500)`: in range → ask again; out of range or no line of sight → approach; after `AttackRetries` (3) in a row without a blow → `Idle`, one info line.
- **No ammunition** (`0x013B` type 0) → `Idle`, one info line.
- **Ground click** while `Approaching`, `Attacking` or `Fetching`: when `CheckWalk` gives `Sent`, send `STOP_ATTACK` first if an attack request is outstanding, then `REQUEST_MOVE`, state `Idle`; the server runs the walk when its walk delay ends (H5). A click that cannot walk changes nothing. This is `OwnCharacter.RequestWalk`, the player's walk.
- **Pickup** (FR-10): `CanPickUp` and not walking → send `PICKUP_ITEM`; else walk to the first cell of the path within two cells, `Fetching`; when that walk ends in range → send. The walk must have ended because the server tests its own cell once and keeps nothing (unlike attacks, H4). An answer with `Result` ≠ 0 while the item is still there: the first time, ask once more after `PickupRetryMs` (400; a step of lag); then one info line with the code. Success needs nothing: the server removes the item.
- **Death, map change, disconnect** → `Cancel`. A **sit** request → `Cancel` (the server stops the attack, `clif_parse_ActionRequest`).
- The requests of `OwnCharacter` already refuse while dead (SPEC-0010); `OwnInput` adds: while dead no cell cursor, no attack cursor, no picking.

### 4.6 Timeline: `CombatTimeline`

Satisfies FR-4, FR-5, FR-6 (what and when), FR-9, FR-10 (motion), FR-12, FR-13, FR-14 (when).

```csharp
public enum MotionKind : byte { None, Attack, Hurt, PickUp, Standby }
public struct UnitMotion { public MotionKind Kind; public uint Starts; public int DurationMs; public int Version; public byte AttackNumber; }

public enum NumberKind : byte { Normal, Critical, Miss, Lucky, Total }
public struct DamageNumber { public uint Starts; public uint TargetId; public int Value; public NumberKind Kind; public bool OnPlayer; public float X, Y; }

public struct Projectile { public uint Launches, Arrives; public uint AttackerId, TargetId; public float FromX, FromY, ToX, ToY; public ProjectileKind Kind; }

public sealed class CombatTimeline
{
    public CombatTimeline(CombatSettings settings, int slots, int hits = 1024, int numbers = 512, int projectiles = 128);
    public void Use(WorldModel model);  public IWeaponTypes Weapons { get; set; }
    public void Add(in Blow blow);  public void AddPickUp(uint id, uint itemId, uint now);
    public void Advance(uint now);
    public ref readonly UnitMotion MotionOf(int slot);
    public bool HoldsDeath(int slot, uint now);          // FR-9: a hit that came before the death notice is not shown yet
    public ReadOnlySpan<DamageNumber> Numbers { get; }  public ReadOnlySpan<Projectile> Projectiles { get; }
    public void Clear();
}
```

- **Storage**: arrays allocated once; pending hits, numbers and projectiles are compact arrays with swap removal; `UnitMotion` per model slot with the slot's `Generation` (a reused slot has no motion). Full: the oldest entry is dropped, one warning per session.
- **Hit moment** (FR-4, D3): `At + AttackMs`, for every attacker. For a blow with a projectile the original client waits for: the projectile's arrival (§4.7). `AttackMs ≤ 0` (H11): at once.
- **Attack motion** at `At`, when the attacker is known, alive, differs from the target and `AttackMs > 0`: `Attack`, `DurationMs = AttackMs`, `AttackNumber` from `AttackMotions` (§4.7). It replaces whatever motion plays (FR-4: a new blow restarts it). After an attack or a hurt a player stands in `Standby` for `StandbyMs` *(lead: until it does something else; measured in T0.3; 0 turns it off)* (D11).
- **At each hit moment**: the number starts (kind, value, the target's shown position then), and the target plays `Hurt` for `HurtMs` unless: no damage, endured, lucky dodge, `HurtMs ≤ 0`, the target's death already shown. A hurt replaces an attack, standby or pickup motion. Walking goes on underneath: the model is not touched, so a walking unit keeps its path position and shows its walk again when the hurt ends.
- **Which numbers** (FR-5):

| Blow | Numbers (time after the hit moment) |
|---|---|
| Normal | value (0) |
| Critical | value as critical (0) |
| no damage, not lucky | miss (0) |
| Lucky dodge | lucky (0) |
| Multi-hit, `n` hits | `n` numbers, the `i`-th at `i × MultiHitMs` (200, lead); then, with the last, the total as `Total` *(where and how: T0.3)* |
| second hand (`LeftDamage` > 0), any kind | its own number at `LeftHandMs` (350, lead) |
| damage < 0 | nothing (H11; heals are SPEC-0012's) |

  Split of a multi-hit: each hit `damage / n`, the remainder on the last, so they always sum to the total (FR-5); a total smaller than `n` shows one number. Each split hit starts its own hurt.
- **Motion and base animation**: the presenter shows the motion while one plays, else the base animation of PLAN-0010 §4.9. A walk that *starts* while an attack, pickup or standby motion plays ends it; a hurt is not ended by a walk. Sitting and death end every motion, death subject to `HoldsDeath`. `Stop` and facing changes end nothing (H7).
- **Death after the hit** (FR-9): `Vanish(Died)` changes the model at once. `HoldsDeath(slot)` is true while a hit added before the death notice is pending, and until `HurtBeforeDeathMs` after that last hit moment for units that are not players (250, H9; 0 for players; T0.3). The presenter keeps showing the hurt until it is false, then plays the death. It delays neither the model nor the removal time. With a slow monster the notice can come up to 1 s before the hit moment (H8): the number, the hurt and the death are then shown in that order, and the HP bar falls before them, as the server sends it.
- **Pickup motion** (FR-10): players only, `PickUp` once at its own speed, facing the item when its cell differs. Monsters that loot have no such animation.
- **A target or attacker that is not in the model**: no number without a target; the motion of the known one still plays.

### 4.7 Attack motion and projectile rules: `AttackMotions`, `ProjectileRules`, `IWeaponTypes`

Satisfies FR-4 (which animation), FR-13, FR-14.

```csharp
public interface IWeaponTypes { int TypeOf(int weapon); }                        // server's weapon value → base type 0..30
public static class AttackMotions
{   public static int NumberOf(int job, byte sex, int weaponType, bool secondHand);   // 1..3: PcAnimation Attack1..3
    public static float HitFrameOf(int job, byte sex, int weaponType); }              // the motion's hit frame, in frames of 24 ms at 432 ms
public enum ProjectileKind : byte { None, Arrow, Other }
public static class ProjectileRules
{   public static ProjectileKind Of(EntityKind kind, int job, int weaponType);
    public static int FlightMs(float cells);
    public static void Times(in Blow blow, bool player, float cells, float hitFrame, out uint launches, out uint arrives, out bool numberWaits); }
```

- **Weapon type.** The importer writes the client's `weapontable.lub` `Expansion_Weapon_IDs` (view id → base type), which `RoViewIdTables` reads already, into `CompositionTables` (two parallel sorted arrays). `CatalogWeaponTypes` (view assembly) maps the server's value: ≥ 500 → item → its view id (`ItemSpriteTable`, PLAN-0010 D22) → base type; else view id → base type; unknown → 0 (bare hand). Until the catalog is loaded `Weapons` is null and every type is 0: a projectile may be missing in the first second of a session, nothing else.
- **Attack number** *(lead: `WeaponAction.js`, 0 to 2 → `Attack1` to `Attack3`)*: compact rows (job, sex or any, weapon type → number) in code as `ReadOnlySpan<byte>` data (no static mutable object, so no UAL warning). Missing row → `Attack1`. Units that are not players always play `Attack`. The rows and their source go into `combat-reference.md`.
- **Projectile by attacker** *(lead: spec §6)*: players with bow types → `Arrow`; monsters and mercenaries by a list of job ids → `Arrow`; the other monsters of that table → `Other`: **no sprite here, but the launch and hit moments are computed and kept** for SPEC-0013. Everything else → `None`. Guns, throwing weapons, whips and instruments are `None` until T0.3 says otherwise.
- **Times** *(lead: `ArrowFlight.js`)*: flight `192 ms × min(1, cells / 8)`, at least 1 ms, `cells` the distance between the shown positions at the launch. Player: launch at `At + hitFrame × 24 × AttackMs / 432` (hit frame 6 for nearly all), the number at launch + 192 ms. Monster: launch at the hit moment, the number does not wait. The launch is computed, not taken from the sprite's `atk` event, so it is the same without assets (FR-13).
- **Flight**: from the attacker's to the target's shown position at launch (whether it follows a moving target is a T0.3 question); linear in time, both ends at `ProjectileHeight` above the ground (T0.3). One projectile for each blow, also a multi-hit.

### 4.8 Drawing: `CombatOverlay`, numbers, bars, projectiles

Satisfies FR-6, FR-7, FR-12, FR-14.

```csharp
public sealed class CombatOverlay : MonoBehaviour      // [DefaultExecutionOrder(950)], created by WorldBootstrap
{ public void Use(WorldSession session, WorldPresenter presenter, CombatSettings settings); public void Clear(); }
sealed class QuadMesh : IDisposable                    // one dynamic Mesh over NativeArrays of a fixed capacity
{ public void Begin(); public void Quad(in Vector3 a, in Vector3 b, in Vector3 c, in Vector3 d, in Rect uv, Color32 colour); public void End(); }
public static class NumberMotion                       // pure, in Uninarok.World
{ public static NumberPose At(NumberKind kind, float t, CombatSettings settings); }   // offset in cells, scale, alpha; t 0..1
```

- **One mesh each** for digits, message sprites and bars: vertices in persistent `NativeArray`s, written with `Mesh.SetVertexBufferData` and `SetSubMesh`, validation and bounds recalculation off (fixed large bounds). Nothing per number or per bar is a GameObject (D6). Capacity: 512 numbers of at most 10 digits, 512 bars.
- **Digits** come from the `숫자` animation's sprites (frame = digit), messages from `msg`; UVs and pixel sizes from the sprites' rects and the sheet texture, read once. Spacing 2 px *(lead)*. A number is laid out centred, in the plane that faces the camera, `pixels / PixelsPerUnit × scale` large, so it zooms with the world as a sprite does *(T0.3: world or screen size)*.
- **Motion, first values** *(lead: `Damage.js`)*: normal and critical 1,500 ms, scale 4 → 0, drifting 4 cells to +x and 4 to −y over the life, height `2 + 5 · sin(−π/2 + π(0.5 + 1.5 t))`, alpha `1 − t`; miss and lucky over 800 ms, scale 0.5, rising 7 from 3.5; total 3,000 ms, above the target. Colours: on a monster white; on a player red; critical (0.9, 0.9, 0.15) over the `msg` critical background; miss and lucky are the `msg` pictures. Each is a row of `combat-reference.md`, measured in T0.3.
- **Order**: after the units, no depth test, newer over older *(lead; T0.3)*.
- **Bars** (FR-7): under the feet, on whole pixels, constant size in screen pixels *(lead: `EntityLife.js`: 60 × 5 for HP, 60 × 9 with SP; frame `#10189c`, empty `#424242`, own HP `#10ef21`, below 25 % `#ff0000`, SP `#1863de`, monster `#ff00e7`, below 25 % `#ffff00`; 0.5 below the feet)*. The corners are computed in screen pixels from the projected feet and unprojected in front of the near plane, so turn, tilt and zoom need nothing more. Which units: the own character always, from `OwnStatus`; a monster that is shown, not dead, with `0 < Hp < MaxHp`.
- **Projectile**: `ProjectileView`, a pooled object with one driven `SpriteActor` (`skel_archer_arrow`), turned in the camera plane along its flight on the screen, placed each frame from `CombatTimeline.Projectiles`; 32 made in advance.
- **Shader** `Uninarok/Overlay`: texture × vertex colour, alpha blend, `ZTest` and queue as material properties (Always, Transparent+200), `ZWrite Off`.

### 4.9 Units: `UnitClock.FitMs`, `UnitView.PlayFor`, `PickRect`; presenter

Satisfies FR-2 (picking), FR-4, FR-9, FR-10.

```csharp
public struct UnitClock { /* … */ public int FitMs; }                     // 0: the body's own frame time × Speed
public sealed class UnitView { /* … */ public void PlayFor(PcAnimation animation, int durationMs, PlayMode mode); public Rect PickRect { get; } }
```

- **`FitMs`**: while > 0 a frame lasts `FitMs / FrameCount` of the action shown, whatever the file's frame time and the direction (the count may differ between directions); `Play` sets it to 0. So an attack lasts exactly `attackMT`, ±1 frame (FR-4).
- **Presenter**: per slot it keeps the last `UnitMotion.Version` seen. Changed: `Attack` → `PlayFor(Attack1..3, DurationMs, OnceHold)` (non-players through the existing mapping to `Attack`); `Hurt` → `PlayFor(Hurt, …)`; `PickUp` → `Play(PickUp, OnceHold)`; `Standby` → `Play(Standby)`; `None` → the base animation (`Animate`). While a motion plays, `AnimationVersion` changes are noted and not played (§4.6 says when a motion ends). `Dead` is played when `HoldsDeath` is false. Still read-only (SPEC-0010 FR-3).
- **`PickRect`**: the rectangle of the body's layers of the frame shown, in the pivot's plane (Unity units), updated in `Compose` only; empty when nothing is shown. `GroundItemView` gets the same.

### 4.10 Input: `UnitPicker`, `OwnInput`, `MouseCursor`, `/noctrl`

Satisfies FR-2, FR-8, FR-10, FR-12.

```csharp
public sealed class UnitPicker
{ public bool TryPick(WorldPresenter presenter, WorldModel model, in Ray ray, out uint id); }   // nearest unit or item under the ray
```

- **Picking**: for each shown view, the ray against its pivot's plane (it faces the camera), the point inside `PickRect` (taken as at least `PickMinPx` (24) wide and high); the hit nearest to the camera wins. The own character never catches the pointer; other players and NPCs do (a player in front of a monster hides it: a click on a player never attacks). Invisible looks and dead units do not count. About 300 ray-plane tests for the frame's pointer position; no allocation.
- **Pointer over** a monster: attack cursor (action 5 of `cursors`), no cell cursor; over an item, a player or an NPC: as the ground below it, so a click on a player or an NPC still walks, as today.
- **Left button**: press on a monster → `Combat.Attack(id, continuous: ctrl held or NoCtrl)`; still held after `HoldAttackMs` (300; T0.3) on a plain press → `Attack(id, true)`. Releasing the button does not stop a continuous attack *(T0.3)*. Press on an item → `Combat.PickUp(id)`. Shift keeps its SPEC-0010 meaning (turn). Otherwise the ground rules of SPEC-0010; a hold that began on a monster never walks.
- New input actions in `WorldInputActions`: `AttackModifier` (ctrl), `Menu` (Esc). The camera's ctrl + right drag is another button.
- **`/noctrl`** in `DevCommandLine` toggles `Combat.NoCtrl` and prints the new state; it lasts for the session.

### 4.11 Death and restart: `RestartPrompt`, `RestartPromptView`

Satisfies FR-8.

```csharp
public sealed class RestartPrompt                      // pure
{
    public bool IsOpen { get; }                         // own character dead (not feigning) and its death shown, not closed, no answer awaited
    public event Action Changed;
    public void Update(uint now);                       // each frame, from the model's own state
    public void ToggleByEscape();                       // only while dead
    public void Close();  public bool ReturnToSavePoint();   // sends RESTART(0) once, then waits
}
```

- Opens when the own entity is `Dead`, not `Feigning`, and `HoldsDeath` is false (also when the character enters the map dead), closes when it stands (resurrection, respawn on the new map) or leaves the game. After `ReturnToSavePoint` it is hidden and waits; with neither a map change nor a resurrection within `RestartTimeoutMs` (3,000) it opens again.
- **View**: UI Toolkit (`UIDocument`, `RestartPrompt.uxml`/`.uss`), in every build (not in the Dev assembly), a scene object in Bootstrap. Two buttons with the labels and order of T0.4. Esc is ignored while the dev command line is open. If the owner can extract the `esc_*.bmp` images, window and buttons use them through the importer; if not, plain USS in the same size and layout (D8).
- While dead `OwnInput` sends nothing; the camera and the dev tools keep working.

### 4.12 Dev HUD: `CombatDebugView`

Satisfies FR-11.

F10 toggles. A uGUI text overlay refreshed four times a second (as `WorldDebugView`, PLAN-0010 D26), beside it: own HP, SP, levels, EXP, range; combat state, target id and its HP; the last 20 blows (attacker, target, damage + left, type, hits, `attackMT`, `attackedMT`) from `WorldSession.BlowReceived` in a ring; the last too-far replies, pickup answers and EXP gains. Dev assembly: not in release builds.

## 5. Threading and lifecycle

- Main thread only. Order per frame in §2. Values: `OwnInput.Update` 0, `WorldBootstrap.LateUpdate` 10, `RoCamera` 50, brain 100, `UnitSystemDriver` 900, `CombatOverlay` 950, `SpriteSystemDriver` 1000.
- `OwnStatus`, `OwnCombat`, `CombatTimeline` and `RestartPrompt` are owned by `WorldSession` and live as long as it. `EndMap` → `Combat.Cancel`, `Timeline.Clear`; numbers and projectiles of the old map go with it.
- `CombatOverlay` owns its meshes and native arrays and frees them in `OnDestroy`. No statics.
- Sprites of numbers, messages and arrow are direct references of `CombatSettings` (as the cursors, PLAN-0010 D29): small and always needed.

## 6. Test plan

### 6.1 Tests

EditMode unless marked. `NeedsData`: needs imported maps, the catalog or the fixtures, else ignored with a message.

| Test | Covers |
|---|---|
| Size and decode tests of every packet of §4.1 (real samples where seen); `_ZeroAllocation1000Decodes` for `DAMAGE`, `PARAMETER_CHANGE`, `LONG_PARAMETER_CHANGE` | FR-1, FR-5, FR-12 |
| `Blow_From_EachWireType`, `_CountZeroIsOneHit` | FR-5 |
| `WorldModel_Strike_TargetFacingLastBlow`, `_SetHp_FromEachEntry`, `_VanishClearsTargets`, `_FeignedDeathIsNotDeath`, `_ZeroHpDoesNotKill` | FR-1, FR-9 |
| `OwnStatus_FromCharInfoThenUpdates`, `_FakeHpWhileDead_StaysDead`, `_KeptAcrossMaps` | FR-1 |
| `AttackRange_ClientDistance_Table` (values computed with `path.cpp`'s formula), `_WalkingTargetOneMore`, `_WallBlocks_CliffDoesNot`, `_CanPickUp`; `LineOfSight_MatchesServer_<map>` (a flag added to the path fixtures by the plugin) — `NeedsData` | FR-3, FR-10, FR-13 |
| `OwnCombat_Click_SendsSingle`, `_CtrlHoldNoCtrl_SendContinuous`, `_PlayerNpcOwn_SendNothing`, `_SittingOrDead_SendNothing`, `_GroundClick_StopsThenWalks` | FR-2 |
| `OwnCombat_Approach_StopsAtRange1And9`, `_TargetMoves_WalksAgain`, `_AsksWhileWalkingOnceInRange`, `_TooFar_ApproachesAgainAndCloser`, `_Silence_AsksAgainThenGivesUp`, `_TargetDies_Idle`, `_NoAmmo_Idle` | FR-3 |
| `OwnCombat_PickUp_WalksWithinTwoThenAsks`, `_FailureRetriedOnce`, `_FailureReportedOnce` | FR-10 |
| `CombatTimeline_AttackLasts500_HitAt500_Hurt300`, `_EnduredHasNumberNoHurt`, `_MissNoHurt`, `_WalkKeepsPosition`, `_NewBlowRestarts`, `_SlowMonster1320_HitAt1320`, `_SelfDamage` | FR-4 |
| `CombatTimeline_Kinds` (each row of §4.6's table), `_DoubleAttack300_150_150_300`, `_DualWield_TwoNumbers`, `_SplitAlwaysSums` (1 to 100 hits) | FR-5 |
| `CombatTimeline_DeathHeldUntilHit` (also a notice at 1,000 for a hit at 1,320), `_MonsterHurt250`, `_OwnDeath`, `_PickUpMotion` | FR-9, FR-10 |
| `ProjectileRules_Players`, `_Monsters`, `_FlightTimes_2_5_9`, `_NumberWaits`; `AttackMotions_Rows` | FR-4, FR-14 |
| `NumberMotion_Table` (pose at fixed times against `combat-reference.md`) | FR-6 |
| `UnitClock_FitMs_LastsExactly_AnyFrameCount`; `UnitView_PlayFor_DirectionChangeKeepsTime` | FR-4 |
| `RestartPrompt_OpensOnDeath`, `_CloseThenEsc`, `_SendsOnce`, `_ResurrectionCloses`, `_FeignedDeathDoesNotOpen`, `_TimeoutReopens` | FR-8 |
| `WorldSession_CombatReplay_<capture>` (fight into a model, no scene: own HP, SP, levels, EXP and every damaged monster's HP equal the server's lines, §6.2) — `NeedsData` | FR-1, FR-13 |
| `Combat_ZeroAllocation_100Fighting` (session and timeline, 600 frames after warm-up) | FR-12 |
| `CombatDebugView_ListsBlowsAsReceived` | FR-11 |
| PlayMode: `CombatOverlay_ZeroAllocation_100Numbers300Units`, `_BarsFollowAtFourTurns`, `_BarHiddenAtFullAndWhenDead`; `WorldPresenter_PlaysMotionThenBase`, `_DeathAfterHurt`; `UnitPicker_NearestUnit_PlayerHidesMonster`; `RestartPromptView_TwoButtons` | FR-2, FR-4, FR-6 to FR-9, FR-12 |
| Release check: no type of `Uninarok.World.Dev` in a release build; the prompt is there | FR-8, FR-11 |

### 6.2 Fixtures (T0.2, T1.5)

`docs/specs/0011-combat/fixtures/combat-*.bin` with the capture tee (as PLAN-0010 T0.5), a README with how each was made, searched for credentials: **(a)** 20 kills on prt_fild08 with a dagger (normal, critical, miss, multi-hit), own HP falling and refilling, a level up; **(b)** own death, `@raise`, death again, restart; **(c)** a bow user with and without arrows, a too-far reply; **(d)** dual wield; pickup of own and of reserved loot; a second client seen fighting and picking up. The plugin `uninarok_pathdump` writes, beside its view lines, HP and max HP of each monster and one `S` line per second with the player's HP, SP, levels and EXP: the server's truth the replay test compares with (and with `@stats` and `@exp` typed at the end).

### 6.3 Reference footage of the original client (owner, T0.3)

1920 × 1080, 60 fps, default view, as for PLAN-0010 D37. Shots: **1** ten normal blows on a poring (path, size, colour and time of the numbers; hurt; bar; pose after the attack); **2** criticals; **3** misses in both directions (over whom are they shown); **4** own damage; **5** double attack (places and times of the parts and the total); **6** dual wield; **7** fast attacks, many numbers; **8** a kill (last number, hurt, death); **9** own death and the prompt, Esc, close; **10** HP bars at several zooms and at low HP; **11** bow at 2, 5 and 9 cells; **12** an archer skeleton; **13** one attack each with gun, throwing weapon, whip, instrument; **14** click, hold and release on a monster; a player standing in front of a monster; a ground click right after a blow; **15** picking up an item; **16** a monster slower than 1 s (Medusa, 1,320 ms): when its attack ends, when the number and the hurt start, when the own HP falls. Measured values go to `combat-reference.md` with the frame numbers; frames are kept in `evidence/`.

## 7. Migration and compatibility

- `IWalkGrid` gets a member: `MapDataWalkGrid` (from `MapCellFlags.Shootable`), `UnknownWalkGrid` and the test grids follow.
- `UNIT_ACT` gets a field and `REQUEST_ACT` constants; their sizes stay.
- `CompositionTables` gets two arrays: the composition tables asset is rebuilt once (`Tools/Ragnarok/Catalog/Composition`), no sprite is converted again. `sprite/npc/skel_archer_arrow` is converted with the importer as it is.
- `WorldModel.Checksum` includes the new fields.
- SPEC-0010's input tests stay valid: with no unit under the pointer nothing changes.
- The working tree holds uncommitted imports of int_land and iz_int and a package change (2026-11-22); they are not part of this spec and stay out of its commits.

## 8. Decisions

| # | Chosen | Rejected | Why |
|---|---|---|---|
| D1 | Combat values in `Entity`, `OwnStatus` beside the model, `OwnCombat` for the own character's intent | A separate combat world keyed by id | One owner per fact (PLAN-0010 D1); the own status outlives a map, the model does not |
| D2 | The model changes when the packet arrives; a separate timeline holds what is shown later | Delaying packets or model changes to the hit moment | The server's state must stay testable and immediate (range, death, HUD); only the display waits |
| D3 | Hit moment by the packet alone: `attackMT`, for every attacker, as FR-4 says | `min(attackMT, 1,000)` for attackers that are not players (first draft): the moment Hercules applies the damage (H8) | Owner's question, 2026-11-22: the cap is a constant inside the server that no packet carries, so copying it ties the client to one server's code, for 15 % of the monsters. The original client does not know it either. A cap of the client's own is possible (roBrowserLegacy has 864 ms) and is measured with shot 16; then it is one value of `CombatSettings` |
| D4 | After a too-far reply the client approaches one cell closer for that target | Retrying at the same cell; predicting the server's cell | Cannot loop, costs at most one step; prediction is a non-goal |
| D5 | The client reacts to too-far replies and to silence, not to its own idea that the target of a running attack moved away | Walking as soon as the model says out of range | A walk request ends the server's attack; the server's answer comes within one attack interval and is never wrong |
| D6 | Numbers and bars are two or three dynamic meshes | `SpriteRenderer`/`SpriteActor` per digit; UI Toolkit or uGUI elements per number and bar | 100 numbers and 300 bars at 0 B per frame (FR-12); moving UI elements every frame costs layout and style writes. The UI Toolkit rule (spec §6) is for game UI such as the prompt; the owner confirmed the meshes (2026-11-22) |
| D7 | Attack motion, hit frame and projectile lists are tables in code with a reference document | Read from the sprite's `atk` event at run time | Testable without assets (FR-13) |
| D8 | Restart prompt in UI Toolkit, with the client's images if the owner extracts them, else plain in the same layout | Waiting for the images | The images are not in the extracted data; the labels and the behaviour are what FR-8 asks for |
| D9 | `0x013B` is handled though the spec's packet list does not name it | Letting the watchdog find out | FR-2: no request that the server would refuse; without it an archer without arrows asks three times |
| D10 | Pick by the body's rectangle in the pivot plane, nearest to the camera | Alpha test; colliders | No physics, no texture reads; checked against the original (shot 14) |
| D11 | `Standby` pose of players after an attack or a hurt | Back to idle at once | Goal 2 ("as the original client shows it"); one setting turns it off if T0.3 shows otherwise |
| D12 | Wire type 13 (multi-hit critical) shown as a multi-hit | Ignoring it | Hercules 2026.09 has it only among the request actions and never sends it for a basic attack |
| D13 | (T0.1) Any pickup result ≠ 0 is a failure; its code is logged at info level as received | Treating only 6 as a failure, as spec §6 says ("a failure always answers with result 6") | `pc_additem` failures reach the client with their own code (2 overweight, 4, 5, 7) through `party_share_loot`; 6 covers only the refusals before that. FR-10 is unchanged: §4.1 already says "`Result` ≠ 0 is a failure" |

## 9. Risks

| Risk | Mitigation |
|---|---|
| The footage contradicts several leads | All display values are settings or table rows; `combat-reference.md` is the single list; the evidence phase measures again |
| The original's numbers or bars do not scale as assumed (world or screen) | Both placements go through one function per mesh (§4.8); decided by shots 1 and 10 |
| A monster healed to full keeps a bar (H10: not reported) | The client shows the last report; the bar goes at the next report or when the unit leaves. Heal skills only. Written into the packet doc; accepted by the owner, FR-7 amended (2026-11-22) |
| Client ahead of the server by up to a step (PLAN-0010 D36) gives too-far replies | H4 (the server keeps a request made while walking), D4; the success criterion is measured in the 20-kill run |
| UI Toolkit input beside the Input System package (no EventSystem in Bootstrap) | Checked first in the prompt's task with a button in an empty panel; fallback: an EventSystem in Bootstrap, recorded here |
| The server's `attack_walk_delay` or `delay_battle_damage` changed by configuration | Spec §6: defaults assumed only where nothing is sent; both only move what is shown |
| Weapon value forms (view id or item id) wrong for some weapons | PLAN-0010 D22 holds so far; unknown weapons fall back to bare hand and log one trace line per value |

## 10. Traceability

| FR | Plan sections | Tests / evidence |
|---|---|---|
| FR-1 | 4.1, 4.2, 4.3 | `WorldSession_CombatReplay_*`, `OwnStatus_*`, `WorldModel_ZeroHpDoesNotKill` |
| FR-2 | 4.1, 4.5, 4.9, 4.10 | `OwnCombat_Click_*` and following; `UnitPicker_*`; live check |
| FR-3 | 4.1, 4.4, 4.5 | `AttackRange_*`, `OwnCombat_Approach_*`; 10 tries live, bow live |
| FR-4 | 4.2, 4.6, 4.7, 4.9 | `CombatTimeline_*`, `UnitClock_FitMs_*` |
| FR-5 | 4.2, 4.6 | `Blow_From_*`, `CombatTimeline_Kinds` |
| FR-6 | 4.6, 4.8 | `NumberMotion_Table`; side by side (shots 1 to 7) |
| FR-7 | 4.2, 4.8 | `CombatOverlay_Bars*`; side by side (shot 10) |
| FR-8 | 4.1, 4.5, 4.10, 4.11 | `RestartPrompt_*`, `RestartPromptView_*`; live check, release build |
| FR-9 | 4.2, 4.6, 4.9 | `CombatTimeline_Death*`, `WorldPresenter_DeathAfterHurt`; second client |
| FR-10 | 4.1, 4.4, 4.5, 4.6, 4.10 | `OwnCombat_PickUp_*`; live check, second client |
| FR-11 | 4.3, 4.12 | `CombatDebugView_*`; live check; release build contains no dev type |
| FR-12 | 4.2, 4.6, 4.8, 4.10 | `Combat_ZeroAllocation_*`, `CombatOverlay_ZeroAllocation_*`; profiler capture |
| FR-13 | 3, 4.4 to 4.7, 6 | the EditMode suite and the replay test |
| FR-14 | 4.6, 4.7, 4.8 | `ProjectileRules_*`; side by side (shots 11 to 13) |
