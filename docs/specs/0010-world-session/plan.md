# PLAN-0010: World Session and Movement

| | |
|---|---|
| **Implements** | [spec.md](spec.md) (SPEC-0010, approved 2026-11-21) |
| **Status** | Approved (owner, 2026-11-21) |
| **Audience** | Architects, senior developers, AI agents |

> This file defines **how**. Requirement IDs (`FR-n`) refer to `spec.md`. The connection, flow, managers and packet rules are [PLAN-0001](../0001-networking-core/plan.md) (packet rules normative in [Packet-Implementation.md](../0001-networking-core/Packets/Packet-Implementation.md)); `MapData` is [PLAN-0005](../0005-gat-map-data/plan.md); map scenes [PLAN-0006](../0006-rsw-map-scene/plan.md); `SpriteCatalog` [PLAN-0008](../0008-sprite-catalog/plan.md); `UnitView`, `UnitLook`, `UnitSystem`, `RoCamera` and `IUnitAssetSource` [PLAN-0009](../0009-unit-display/plan.md). This plan adds the layer between them: **packets → one world model → views**, plus the own character's input.

## 1. Constraints and assumptions

- **Builds on SPEC-0009 being implemented.** Tasks start only after 0009 is merged; every display call below is a PLAN-0009 contract (`UnitView.SetLook/Facing/HeadFacing/Play/PlaceOn`, `RoCamera.Follow`, `AddressablesUnitAssetSource`). If 0009's implementation changed a signature, the plan follows the code and records it under Decisions.
- **No new third-party library.** Map scenes load through Addressables (already in the project, PLAN-0008), input through `com.unity.inputsystem` (manifest), the camera through Cinemachine (PLAN-0009).
- **Hercules is the reference** for every rule (spec §6). Facts read in `Hercules-2026.09/src/map` on 2026-11-21:
  - `path.cpp` `path_search`: A* over 8 neighbours, `MOVE_COST` 10, `MOVE_DIAGONAL_COST` 14, Manhattan heuristic ×10 (`heuristic`), a diagonal only if both orthogonal neighbours and the diagonal cell pass (`chk_dir`), neighbours pushed in the fixed order (+1,−1), (+1,0), (+1,+1), (0,+1), (−1,+1), (−1,0), (−1,−1), (0,−1); binary heap ordered by `f_cost` only (`NODE_MINTOPCMP`); node table of 32×32 indexed by `(x + y·32) & 1023`, a collision aborts the search (`add_path` error → "no path"); path at most `MAX_WALKPATH` 32 steps; start cell never checked; same start and end gives an empty path.
  - `unit.cpp` `unit_walk_toxy_sub`/`unit_walktoxy`: REQUEST_MOVE walks with flag 4 (A*, not the easy path); a path longer than `max_walk_path` (17, `conf/map/battle/client.conf`) is refused; with `OFFICIAL_WALKPATH` (defined, `src/config/core.h`) a path longer than 14 is refused when `path_search_long` (straight line) fails; NPCs are exempt. Each step's timer is `speed` (straight) or `speed × 14 / 10` (diagonal); **the unit's server cell changes when the step's timer fires, at the end of the step**.
  - **The easy path still exists** (`path_search` with `flag & 1`, the rAthena-era rule): it goes **diagonally first, then straight**, and fails at the first blocked cell, with no search. It is used when `ud->state.walk_easy` is set: monsters chasing a target or walking to a warp (`mob.cpp` `walk_tobl(…, 1, 1)`), pets fetching items, `unitwalkto` on a target in scripts, Wink of Charm. Players' clicks (flag 4), monster random walk (flags 0 and 8) and attack approach (flag 2) use A*. **The move packets do not say which one the server used**, and the original client always uses A* (comment in `path.cpp`), so a chasing monster can be shown on other cells than the server's mid-walk, ends equal. §4.4 and D11 handle it.
  - `clif.cpp` `clif_parse_WalkToXY`: no walk when dead, sitting (unit cannot move), vending or in an NPC dialog; a one-cell walk onto a cell already holding a character or NPC is ignored (`official_cell_stack_limit` 1, `conf/map/battle/misc.conf`).
  - `clif_move`: a unit already known to the area walks with **`ZC_NOTIFY_MOVE` 0x0086** (id, move data with sub-cell fixed to 8/8, server tick); `unit_walking` 0x09FD only when a walking unit enters view or its speed changed. 0x0086 is not yet in the packet index.
  - Vanish reasons (`enum clr_type`): 0 out of sight (also warp portals, `npc.cpp`), 1 dead, 2 respawn/logout (`map.cpp`), 3 teleport (fly and butterfly wing, script `warp` to save point), 4 trick dead.
  - `clif_changemap` 0x0091 (map name 16 B, x, y), `clif_changemapserver` 0x0AC7 (map, x, y, ip, port, dns host 128 B) for PACKETVER ≥ 20170315; `clif_changed_dir` 0x009C (id, head dir `ushort`, body dir `byte`); `clif_changelook` 0x01D7 (`ZC_SPRITE_CHANGE2`); `clif_sitting`/`clif_standing` 0x008A with action 2/3; `clif_slide` 0x01FF; `clif_fixpos` 0x0088; `clif_getareachar_item` 0x009D (item id 32-bit from 20181121); `clif_dropflooritem` 0x0ADD; `clif_clearflooritem` 0x00A1; `clif_notify_time` 0x007F answers the heartbeat 0x0360.
- **Coordinates.** A server cell `(x, y)` is `MapData` cell `(x, z)` with `z = y`: PLAN-0005 T8.1 found the cell map identical to the server's on prontera with that indexing. Only prontera, izlude and prt_fild08 have server caches built from the client data (BACKLOG), so all map-based tests use them.
- **Phase 0 checks** named in this plan ([tasks.md](tasks.md)): **T0.1** 0009's merged code (execution orders, camera snap call); **T0.2** every packet of §4.1 against `packets_struct.h`, `clif.cpp` and a real capture, plus the server echoes this plan relies on (direction, sit); **T0.3** display facts from roBrowser and the original client (item sub-cell, walk animation speed, dead monster time); **T0.4** `/doridori` and `/bangbang` side by side with the original client; **T0.5** recorded sessions for replay. The D11 measurement needs the pathfinder port and runs in **T3.8**. A check that contradicts this plan stops the task; the plan is updated first, with a note under Decisions.
- **Pure core, thin components** (as PLAN-0009 D3): the world model, the clock, the pathfinder and the own-character rules are plain C# with no `MonoBehaviour` and no scene, tested in EditMode (FR-18). Components only read the model and call 0009's display.
- **One update loop**, no `Update` per entity. Hot packets and the per-frame update allocate nothing (FR-17, SPEC-0001 FR-6).

## 2. Architecture

Satisfies FR-1 to FR-19.

```
 Networking (SPEC-0001, extended §4.1, §4.8)          World core (pure, §4.2–4.6)                     World view (§4.7–4.11)
 ───────────────────────────────────────────          ─────────────────────────────                   ────────────────────────
 NetworkClient ─► GeneratedPacketRouter ─► dispatch ─► WorldSession (packet → model ops) ──► WorldModel ◄── WorldPresenter.LateUpdate
                                                          │       ▲                        │ entities by id   │ reads, never writes
 ZoneManager ── HeartbeatAnswered(serverTick, rtt) ─► ServerClock ─┘ Now                  │ Vanished event   ├─► UnitView per unit (0009)
            ── MapChangeOrdered / ServerChangeOrdered                                       │ Advance(now)     ├─► GroundItemView per item
 NetworkFlow ── StateChanged(LoadingMap, InGame…) ──────────────────────────────────────────┼──────────────────┤
     ▲ MapLoaded()                                       HerculesPathfinder ◄── IWalkGrid ◄─ MapData (0005)  └─► RoCamera.Follow = own view
     │                                                        ▲                                    ▲
 MapSceneLoader (Addressables scenes) ◄── WorldBootstrap ─────┼── OwnCharacter (walk / turn / sit rules) ◄── OwnInput (Input System)
                                                              │        │ REQUEST_MOVE, CHANGE_DIRECTION, REQUEST_ACT      ▲ CellPicker + CellCursor
                                                              └────────┘                                                   DevCommandLine (dev)
```

Data flow per frame: the network client delivers packets (SPEC-0001 FR-5) → `WorldSession` turns each into one model operation, using `ServerClock.Now` for walk start times → `WorldBootstrap.LateUpdate` (before `UnitSystemDriver`, §5) calls `WorldModel.Advance(now)` (moves walkers along their paths, completes steps, ends walks, expires dead monsters) → `WorldPresenter` syncs every entity whose version changed into its view, places all moving views, and creates or releases views for added or removed entities. Input goes the other way: `OwnInput` → `OwnCharacter` (checks with the model and the pathfinder) → packets; the model changes only when the server answers.

## 3. Assemblies and file layout

Satisfies FR-14, FR-18 (placement), and the dev-only rule.

| Assembly | Path | References | Contents |
|---|---|---|---|
| `Uninarok.World` (new) | `Assets/Scripts/World/` | `Uninarok.Networking`, `Uninarok.Networking.Core`, `Uninarok.Map`, `Unity.Collections` | `ServerClock`, `IWalkGrid`, `MapDataWalkGrid`, `HerculesPathfinder`, `WalkPath`, `WalkTiming`, `WorldModel`, `Entity`, `EntityKind`, `EntityLook`, `EntityState`, `VanishReason`, `VanishInfo`, `WorldSession`, `OwnCharacter`, `WorldSettings` |
| `Uninarok.World.View` (new) | `Assets/Scripts/World/View/` | World, `Uninarok.Units`, `Uninarok.CameraRig`, `Uninarok.Map`, `Uninarok.SpriteCatalog`, `Uninarok.Sprites`, `Unity.InputSystem`, Addressables | `WorldBootstrap`, `MapSceneLoader`, `WorldPresenter`, `EntityLookMapping`, `GroundItemView`, `CellPicker`, `CellCursor`, `OwnInput`, `WorldInputActions.inputactions` |
| `Uninarok.World.Dev` (new) | `Assets/Scripts/World/Dev/` | World, World.View | `WorldDebugView` (IMGUI + gizmo lines), `DevCommandLine`. Every file wrapped in `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` (PLAN-0001 D11) |
| `Uninarok.Networking` (changed) | `Assets/Scripts/Networking/Runtime/` | — | new packet structs (§4.1) in `Packets/In/Map`, `Packets/Out/Map`; `ZoneManager` events (§4.8); `NetworkFlow` map-change transitions (§4.8); `DevAutoLoginDriver` (§7) |
| Hercules plugin (new, tool) | `Hercules-2026.09/src/plugins/uninarok_pathdump.c` | Hercules | Writes reference paths computed by the server's own `path->search` (§4.4, D3). Dev tool, never loaded by a normal server start |
| Tests | `Assets/Tests/EditMode/World/`, `Assets/Tests/PlayMode/World/`, `Assets/Tests/EditMode/Networking/` (new packets) | — | §6 |

Data and scenes:

- `docs/specs/0010-world-session/fixtures/paths-<map>.bin` (reference paths from the plugin, §4.4) and `fixtures/world-*.bin` (recorded sessions for replay, §6), with a `README.md` saying how each was made.
- Addressables group `maps` (new): each `Scenes/Maps/<map>.unity` with key `map/<map>`, and `<map>.additive.unity` with key `map/<map>.additive` when it exists (D5). The converter of PLAN-0006 is not changed; one editor menu item `Tools/Ragnarok/Register Map Scenes` adds every converted map scene to the group (idempotent).
- `Assets/Settings/World/WorldSettings.asset` (server rule values, §4.2).
- Packet docs: new docs in `docs/specs/0001-networking-core/Packets/In/MapServer/` and `Out/MapServer/`, `INDEX.md` regenerated, existing docs moved to `implemented`/`verified` (normative reference, same commit as the code).

## 4. Component contracts

### 4.1 Packets

Satisfies FR-1, FR-4 to FR-9, FR-13, FR-15, FR-16, FR-19.

Sizes below are from the Hercules length table for 20220406 (`fixtures/hercules-packetlen-20220406.json`, read 2026-11-21). Each packet follows Packet-Implementation.md (struct, doc, size test against the Hercules table, decode test with a real capture where one exists). **Every ID, size and layout is first checked against `packets_struct.h` and `clif.cpp` for PACKETVER 20220406** (BACKLOG), in T0.2; a difference stops the task and is written here under Decisions.

| Direction | ID | Struct (doc name) | Hot | New doc | Notes |
|---|---|---|---|---|---|
| in | `0x09FE` | `UNIT_NEW_ENTRY` | yes | — | spawn; variable (107 sent, T0.2); `name` as `FixedString32Bytes` |
| in | `0x09FF` | `UNIT_STAND_ENTRY` | yes | — | standing unit; carries `state` (sitting, dead) |
| in | `0x09FD` | `UNIT_MOVE_ENTRY` | yes | — | walking unit entering view; `UninarokMoveData` + `moveStartTime` |
| in | `0x0086` | `UNIT_MOVE` | yes | yes | `ZC_NOTIFY_MOVE`: id, move data, server tick (16 B) |
| in | `0x0087` | `PLAYER_MOVE` | warm | — | own walk confirmed |
| in | `0x0088` | `UNIT_STOP_MOVE` | yes | — | stop / fix position |
| in | `0x01FF` | `UNIT_SLIDE` | warm | yes | `ZC_HIGHJUMP`: id, x, y (10 B) |
| in | `0x0080` | `UNIT_VANISH` | yes | — | id, reason |
| in | `0x009C` | `UNIT_DIRECTION` | warm | yes | `ZC_CHANGE_DIRECTION`: id, head `ushort`, body `byte` (9 B) |
| in | `0x01D7` | `UNIT_LOOK_CHANGE` | warm | yes | `ZC_SPRITE_CHANGE2`: id, look type `byte`, value `int`, value2 `int` (15 B) |
| in | `0x008A` | `UNIT_ACT` | yes | yes | `ZC_NOTIFY_ACT` (29 B); this spec reads action 2 (sit) and 3 (stand) only; other actions are ignored here and belong to the combat spec |
| in | `0x009D` | `ITEM_ENTRY` | yes | yes | item already on the ground (19 B, item id `uint`) |
| in | `0x0ADD` | `ITEM_FALL_ENTRY` | yes | — | item dropped |
| in | `0x00A1` | `ITEM_DISAPPEAR` | yes | — | |
| in | `0x0091` | `MAP_CHANGE` | cold | yes | `ZC_NPCACK_MAPMOVE`: map name 16 B, x, y (22 B) |
| in | `0x0AC7` | `MAP_SERVER_CHANGE` | cold | yes | `ZC_NPCACK_SERVERMOVE`: map, x, y, ip, port, dns host 128 B (156 B); `ip` big-endian, `port` little-endian (comment in `clif.cpp`) |
| in | `0x00B0` | `PARAMETER_CHANGE` | warm | yes | `ZC_PAR_CHANGE`: var, value (8 B); this spec reads `SP_SPEED` (0) only |
| out | `0x035F` | `REQUEST_MOVE` | warm | — | |
| out | `0x0361` | `CHANGE_DIRECTION` | warm | — | |
| out | `0x0437` | `REQUEST_ACT` | warm | — | action 2 sit, 3 stand, target 0 |

**T0.2 results** (2026-11-22; `src/map/clif.cpp`, `packets_struct.h`, `clif.h`, `map.h` with `PACKETVER` 20220406 main, and the length table). "Capture": the real capture `Map-20261117-124252.bin` holds the packet and its bytes match the layout.

| ID | Size | Source | Capture | Layout after the header, notes |
|---|---:|---|---|---|
| `0x09FF` | 108 | `packet_idle_unit`, `clif_set_unit_idle` | yes | as the doc; `state` = `vd->dead_sit`: 0 standing, 1 dead (also feigned death), 2 sitting |
| `0x09FE` | 107 | `packet_spawn_unit`, `clif_spawn_unit` | no | as the idle entry **without `state`** (a spawned unit stands) |
| `0x09FD` | 114 | `packet_unit_walking`, `clif_set_unit_walking` | no | no `state`; `moveStartTime` is the server tick **when the packet is sent** and the source is the unit's server cell at that moment, so the path is the rest of the walk |
| `0x0086` | 16 | `clif_move` | no | id `uint`, move data 6 B, tick `uint` (send time); sent to the area without the unit itself |
| `0x0087` | 12 | `clif_walkok` | no | tick `uint`, move data 6 B |
| `0x0088` | 10 | `clif_fixpos` | no | id `uint`, x `ushort`, y `ushort`; area, the unit included |
| `0x01FF` | 10 | `clif_slide` | no | id `uint`, x `ushort`, y `ushort`; area, the unit included |
| `0x0080` | 7 | `clif_clearunit_area` | no | id `uint`, reason `byte` |
| `0x009C` | 9 | `clif_changed_dir` | no | id `uint`, head `ushort`, body `byte`. **`clif_parse_ChangeDir` sends it `AREA_WOS`: the player who turned gets no echo** (D9) |
| `0x01D7` | 15 | `PACKET_ZC_SPRITE_CHANGE`, `clif_sendlook` | yes | id `uint`, type `byte`, value `uint`, value2 `uint`. A shield change is sent as type weapon (value weapon, value2 shield) |
| `0x008A` | 29 | `clif_sitting`, `clif_standing` | no | id `uint` at 2, action `byte` at 26; **the other bytes are uninitialised** (`unsigned char buf[32]`) and must be skipped. Area, the player included: sitting and standing echo (D9) |
| `0x009D` | 19 | `PACKET_ZC_ITEM_ENTRY` | no | id `uint`, item `uint`, identified `byte`, x, y, amount `ushort`, sub x, sub y `byte` |
| `0x0ADD` | 24 | `packet_dropflooritem` | no | as the doc (amount after the sub cell, unlike `0x009D`) |
| `0x00A1` | 6 | `clif_clearflooritem` | no | id `uint` |
| `0x0091` | 22 | `clif_changemap` | yes | map 16 B (`izint.gat`), x, y `ushort`. Also sent for a move on the **same** map, and once right after `MAP_LOADED` when a script warped the player while loading (`state.rewarp`) |
| `0x0AC7` | 156 | `clif_changemapserver` | no | map 16 B, x, y, ip `htonl` (big-endian), port little-endian, dns 128 B |
| `0x00B0` | 8 | `PACKET_ZC_PAR_CHANGE` | yes | var `ushort`, value `int`; `SP_SPEED` 0 is the own walk speed |
| out `0x035F`, `0x0361`, `0x0437` | 5, 5, 7 | `packets_shuffle_main.h`, `clif_parse_WalkToXY`, `_ChangeDir`, `_ActionRequest` | - | as the docs; the body direction is stored as sent, nothing is refused |

- **Look types of 0x01D7** (`enum look` in Hercules `map.h`): 0 base (job), 1 hair, 2 weapon (value = weapon, value2 = shield), 3 head bottom, 4 head top, 5 head mid, 6 hair colour, 7 cloth colour, 8 shield (never sent: folded into 2), 9 shoes, 10 body (unknown use), 11 floor (unknown use), 12 robe, 13 body2 (costume style). Unknown types are ignored at trace level.
- **Weapon and shield values** are the item's view id when it has one, else the **item id** (`clif_get_weapon_view`; the capture's look change holds 1201, Knife): the look mapping (§4.9) passes the value as view id and as item id (`UnitLook.WeaponItem`/`ShieldItem`), which the catalog tries in that order (SPEC-0008 R6).
- **Walking resets the head** (`unit_walk_toxy_sub`: `sd->head_dir = 0`): `Walk` of a player sets the head straight.
- **Warp facing**: `pc_setpos` turns the player north unless `player_warp_keep_direction` is on (default off): D14.
- `objecttype` 0 (PC) is also sent for monsters, pets and homunculi that look like a player class (`clif_bl_type`); they carry `GID` 0 (a player's `GID` is its character id), so kind = player needs `objecttype` 0 **and** `GID` not 0, else monster.
- **Entity kinds** (`objecttype` of the entry packets, `enum clif_unittype`, `clif_bl_type`): PC 0, NPC 1, item 2, skill 3, unknown 4, monster 5, event NPC 6, pet 7, homunculus 8, mercenary 9, elemental 10; the doc of `UNIT_MOVE_ENTRY` gets the table.
- Hot packets read names only into `FixedString32Bytes` and keep no arrays (PLAN-0001 §4.10). `MAP_SERVER_CHANGE` is cold and may allocate.

### 4.2 Server time: `ServerClock`

Satisfies FR-2.

```csharp
namespace Uninarok.World
{
    public sealed class ServerClock
    {
        public ServerClock(IClock local);
        public bool IsSynced { get; }
        public uint Now { get; }                                    // estimated server tick (ms, wraps as the server's uint)
        public void Start(uint serverTick, long localReceivedMs);   // AUTHENTICATION_ACCEPTED
        public void Sample(uint serverTick, long localSentMs, long localReceivedMs);   // heartbeat answer
        public static int Diff(uint a, uint b);                     // a − b with wrap, as Hercules DIFF_TICK
    }
}
```

- `Now = (uint)(local.NowMs + _offset)`. `Start` sets `_offset = serverTick − received` (round trip unknown, taken as 0).
- `Sample` computes `rtt = received − sent` and the candidate offset `serverTick + rtt / 2 − received`. It keeps the candidate of the **lowest round trip of the last 8 samples** (the least delayed answer is the most accurate). A higher candidate is applied at once (time jumps forward by exactly the correction, FR-2). A lower one is applied by **slewing**: the offset falls by at most 0.5 ms per elapsed ms, so `Now` keeps rising at half speed until it is caught up and never runs backwards.
- `ZoneManager` remembers the local send time of the one outstanding heartbeat and raises `HeartbeatAnswered(serverTick, sentMs, receivedMs)`; an answer with no outstanding request is ignored. To sync quickly after entry, `ZoneManager.SendMapLoaded` also sends one heartbeat at once, then keeps the 10 s interval (§7, D6).
- No allocation; `IClock` is the existing SPEC-0001 interface (fakes in tests).

### 4.3 Walk grid and timing: `IWalkGrid`, `MapDataWalkGrid`, `WalkTiming`

Satisfies FR-6, FR-10.

```csharp
public interface IWalkGrid { int Width { get; } int Height { get; } bool IsPassable(int x, int y); }   // false outside
public sealed class MapDataWalkGrid : IWalkGrid { public MapDataWalkGrid(MapData data); }          // Walkable flag, z = y
public static class WalkTiming
{
    public static int StepMs(int speed, bool diagonal) => diagonal ? speed * 14 / 10 : speed;     // integer, as unit.cpp
}
```

`IsPassable` is Hercules `CELL_CHKNOPASS` negated: the cell's `Walkable` flag (PLAN-0005 D2 uses the server's `map_gat2cell`). Units do not block paths (the server's walk path ignores units; only the one-cell stack rule of §4.6 uses occupancy).

### 4.4 Pathfinding: `HerculesPathfinder`

Satisfies FR-9, FR-10, FR-17.

```csharp
public enum PathResult : byte { Found, SameCell, Blocked, NoPath, TooLong }

public struct WalkPath                         // fixed storage, no allocation
{
    public const int MaxSteps = 32;            // MAX_WALKPATH
    public int Length; public int StartX, StartY;
    public byte Step(int i);                   // direction 0..7 of step i (UninarokDirection values, walk_choices)
}

public sealed class HerculesPathfinder
{
    public HerculesPathfinder(IWalkGrid grid);                 // owns its node table and heap: allocated once
    public PathResult Search(int x0, int y0, int x1, int y1, ref WalkPath path);       // path_search, flag 0 (A*)
    public PathResult SearchEasy(int x0, int y0, int x1, int y1, ref WalkPath path);   // path_search, flag 1: diagonal first, then straight
    public PathResult SearchForUnit(EntityKind kind, int x0, int y0, int x1, int y1, ref WalkPath path);  // D11 rule
    public bool StraightLine(int x0, int y0, int x1, int y1);                         // path_search_long (Bresenham-like)
    public PathResult SearchWalk(int x0, int y0, int x1, int y1, ref WalkPath path, in WalkRules rules); // + limits
}
public readonly struct WalkRules { public readonly int MaxWalkPath, MaxWalkPathObstructed; }  // 17, 14 (WorldSettings)
```

- **A port, not a re-implementation.** `Search` reproduces `path_search` line by line: the 32×32 node table with the same index (and the same abort on collision), the same binary heap (`BHEAP_PUSH2`/`BHEAP_POP2` of `common/db.h`: sift-up on push, last element to the top and sift-down on pop, ties keep heap order, comparator `f_cost` only), the same neighbour order, the same corner rule and bounds (`x < xs − 1` etc.), and path reconstruction through parents. `StraightLine` ports `path_search_long`. Any deviation shows up as a different path in the comparison test, which is the acceptance (D3).
- `SearchEasy` ports the `flag & 1` branch: steps of `(sgn dx, sgn dy)` until one axis is reached, then straight; the first blocked cell fails (no fallback).
- **Which path for another unit's walk** (`SearchForUnit`, D11): players and NPCs use A*. For monsters, pets, homunculi, mercenaries and elementals the client cannot know the server's choice; the default rule is **"easy path if it reaches the destination, else A\*"** (a chase, the case that matters near the player, is then exact; a random walk is exact whenever both algorithms agree). T3.8 measures the rule with the walk log below on a real session (share of monster walks whose path the rule reproduces, and the largest mid-walk distance when it does not); if another rule (always A*) scores better, it replaces the default with a Decisions note.
- `SearchWalk` applies `unit_walktoxy`'s rules: `Blocked` when the destination is not passable; `TooLong` when the path exceeds `MaxWalkPath`, or `MaxWalkPathObstructed` when `StraightLine` fails. NPC paths are not searched by the client (NPCs that walk use the same A* without limits: `SearchWalk` is given `int.MaxValue` limits for `EntityKind.Npc`).
- Directions: `walk_choices` gives Hercules directions 0 N, 1 NW, 2 W, 3 SW, 4 S, 5 SE, 6 E, 7 NE with y growing north, the same values as `UninarokDirection`.
- **Reference paths (D3).** The plugin `uninarok_pathdump` (Hercules plugin API, loaded with `--load-plugin uninarok_pathdump` on a server whose map cache was rebuilt from the client data) reads a seed and a pair count from its command line, draws random walkable start cells and random end cells within 20 cells (so "too long" and "no path" occur), runs the server's own `path->search` (flag 0) and `path->search_long`, and writes per pair `x0 y0 x1 y1 result len steps[]` for both A* and the easy path into `paths-<map>.bin`. A second mode, **walk log**, hooks `unit->walk_toxy_sub` (HPM post-hook) during a normal server run and appends per walk `tick id kind walk_easy src dst steps[]` to a file: the ground truth of how each unit really walked, used by T3.8's measurement and by the replay test of §6. 10,000 pairs per reference map; the files are committed as test fixtures with the seed and the server commit in their README.
- Cost: one search touches at most 1,024 nodes; target ≤ 50 µs and 0 B per search (allocation test).

### 4.5 World model: `WorldModel`, `Entity`

Satisfies FR-3 to FR-8, FR-16, FR-17, FR-19.

```csharp
public enum EntityKind : byte { Player, Npc, Monster, Pet, Homunculus, Mercenary, Elemental, Item, Other }
public enum EntityState : byte { Standing, Sitting, Dead }
public enum VanishReason : byte { OutOfSight = 0, Died = 1, LoggedOut = 2, Teleported = 3, FeignedDeath = 4 }
public enum HeadDirection : byte { Straight = 0, Right = 1, Left = 2 }      // wire values of 0x009C / 0x0361

public struct EntityLook : IEquatable<EntityLook>      // raw server ids; mapped to UnitLook by the view (§4.9)
{ public short Job; public ushort HairStyle; public short HairColour, ClothColour, BodyStyle;
  public int Weapon, Shield; public ushort HeadTop, HeadMid, HeadBottom, Robe; public byte Sex; }

public struct Entity
{
    public uint Id; public EntityKind Kind; public EntityState State;
    public EntityLook Look; public int ItemId, ItemAmount; public bool Identified;   // items
    public short Speed;                                   // ms per straight cell
    public int CellX, CellY;                              // server cell: changes when a step completes (Hercules timing)
    public float PosX, PosY;                              // shown position in cells (fractional), for the view
    public UninarokDirection Facing; public HeadDirection Head;
    public bool Walking; public uint WalkStart; public int StepIndex; public uint StepEnds;  // path state
    public int Version;                                    // bumped on every change the view must apply
    public int AnimationVersion;                           // bumped on walk start/stop, sit, stand, die (view replays)
}

public sealed class WorldModel
{
    public WorldModel(IWalkGrid grid, WorldSettings settings, int capacity = 2048);   // pools allocated once
    public uint OwnId { get; }
    public int Count { get; }
    public ref readonly Entity this[int slot] { get; }
    public bool TryGetSlot(uint id, out int slot);
    public ref readonly WalkPath PathOf(int slot);

    // operations (called by WorldSession; each bumps Version where something changed)
    public int Upsert(uint id, EntityKind kind, in EntityLook look, short speed, int x, int y,
                      UninarokDirection dir, HeadDirection head, EntityState state);       // never two slots per id
    public void Walk(uint id, int sx, int sy, int dx, int dy, uint startTick, uint now);   // also places mid-walk (FR-4)
    public void Stop(uint id, int x, int y);
    public void Slide(uint id, int x, int y);
    public void Turn(uint id, UninarokDirection body, HeadDirection head);
    public void SetLook(uint id, LookType type, int value, int value2);
    public void SetState(uint id, EntityState state);
    public void SetSpeed(uint id, short speed);
    public void AddItem(uint id, int itemId, bool identified, int x, int y, byte subX, byte subY, int amount);
    public void Vanish(uint id, VanishReason reason, uint now);
    public void Clear();                                                   // map change, disconnect

    public void Advance(uint now);                                         // per frame, §4.5 rules
    public event Action<VanishInfo> Vanished;                              // FR-5 announcement
    public int DrainAdded(Span<uint> into); public int DrainRemoved(Span<uint> into);   // for the presenter
}
public readonly struct VanishInfo { public readonly uint Id; public readonly EntityKind Kind; public readonly VanishReason Reason; public readonly int X, Y; }
```

- **Storage** (FR-17): a struct array of `capacity` entities, a free list, a `Dictionary<uint,int>` created with the full capacity (no rehash in play), one `WalkPath` per slot, and two ring buffers of added and removed ids. A full model logs one warning and ignores the new entity (capacity 2048 is about 7 times what `area_size` 14 can show).
- **Walking** (FR-6): `Walk` searches the path with `HerculesPathfinder.SearchForUnit` (A* for players, D11 rule for other units) from the packet's source cell (the client does what the original client does: the move packet carries only the ends). If the search fails, the unit is placed on the destination at once (one trace line): the server reached it, so the client must too. Step `i` lasts `WalkTiming.StepMs(speed, step is odd)` (odd Hercules directions are diagonal). `Advance(now)` finds the current step from `now − WalkStart` and sets `PosX/PosY` linearly between the step's cells, `Facing` to the step's direction, and `CellX/CellY` to the cell reached when the step **ends** (the server's cell, §1). Arrival ends the walk and bumps `AnimationVersion` (idle).
- **Mid-walk entry** (FR-4): `Walk` with a start time in the past runs the same rule, so a unit entering view appears where its path says it is at `now`.
- **New order while walking:** the new path starts at the packet's source cell. If the shown position is within one cell of it, the first segment starts from the shown position (no snap); farther, the unit snaps (D7).
- **Stop** ends the walk on the given cell; **slide** moves position and cell at once and keeps `Facing` (FR-7).
- **Vanish** (FR-5): reasons 0, 2, 3 remove at once; 1 sets `Dead` (`AnimationVersion` bump) and, for monsters (and other non-player units), removes after `WorldSettings.DeadMonsterLingerMs` (default 1,500 ms, the dead animation's length is the view's business: the presenter holds the last frame, D8); players stay `Dead` until a new entry or a stand; 4 sets `Dead` and keeps the unit. Every vanish of a known id raises `Vanished` once, before removal. Unknown id: nothing, trace only.
- **Items** (FR-16): `Kind = Item`, shown at the cell plus a sub-cell offset. Hercules draws `subx`/`suby` from a small set (`map_addflooritem`); the formula that turns them into a position inside the cell (the move data uses `sub × 0.0625 − 0.5`, `UninarokMoveData.md`) is checked in T0.3 against roBrowser and the original client and written into `ITEM_FALL_ENTRY.md`.
- **Own character**: `Upsert` with `OwnId = AccountId` at entry (§4.6). Packets for the own id (look change, direction, stop, slide, vanish on death) go through the same operations.

### 4.6 Packets to model, own character: `WorldSession`, `OwnCharacter`

Satisfies FR-1, FR-4 to FR-9, FR-13, FR-15, FR-16, FR-19.

```csharp
public sealed class WorldSession : IDisposable
{
    public WorldSession(INetworkClient client, NetworkFlow flow, ServerClock clock, WorldSettings settings);
    public WorldModel Model { get; }                  // null until a map is entered
    public OwnCharacter Own { get; }
    public void BeginMap(MapData map);                // new model on this grid, own character from SessionContext
    public void EndMap();                             // Model.Clear, handlers stay
}

public sealed class OwnCharacter
{
    public bool CanAct { get; }                                      // alive, standing or sitting, model present
    public WalkRequestResult RequestWalk(int x, int y);              // checks, then REQUEST_MOVE
    public void TurnTowards(int x, int y);                           // shift + click rule
    public void Doridori(); public void Bangbang();                  // commands
    public void ToggleSit(); public void Sit(); public void Stand();  // REQUEST_ACT 2 / 3
    public void Tick(uint now);                                      // runs a pending doridori sequence
}
public enum WalkRequestResult : byte { Sent, Dead, Sitting, Blocked, NoPath, TooLong, Occupied, SameCell, NotInGame }
```

- **Handlers** (one `PacketHandler<T>` per packet, created once, registered through SPEC-0001 `Subscriptions` while the flow is `LoadingMap` after authentication or `InGame`). Each handler is one model call; entries map `objecttype` → `EntityKind`, `bodyState`/`healthState`/`action` → `EntityState` (`state` of `UNIT_STAND_ENTRY`: 0 standing, 1 dead, 2 sitting; the other two entries carry none and mean standing), `headDir` → `HeadDirection`. `UNIT_MOVE`/`UNIT_MOVE_ENTRY`/`PLAYER_MOVE` call `Walk` with the packet's server tick (`PLAYER_MOVE` and `UNIT_MOVE` carry it; `UNIT_MOVE_ENTRY` its `moveStartTime`). `PARAMETER_CHANGE` with var 0 sets the own speed. No string work, closures or LINQ in handlers (PLAN-0001 §4.10).
- **Own character at entry** (FR-1): look from the selected `CharInfo` (`Class`, `Hair`, `HairColor`, `ClothesColor`, `Body`, `Weapon`, `LookShield`, `LookHeadTop/Mid/Bottom`, `LookRobe`, `Sex`), speed `DefaultWalkSpeed`, cell and facing from `Context.Entry.Position`, state `Dead` when `CharInfo.Hp == 0` (FR-1: enters lying dead, no input).
- **Walk rules** (FR-9), in order: not in game → `NotInGame`; dead → `Dead`; sitting → `Sitting`; same cell → `SameCell`; then `SearchWalk` from the own **server cell** (`CellX/CellY`, which is where the server's path starts) → `Blocked`/`NoPath`/`TooLong`; a one-cell walk onto a cell where the model holds a player, NPC or monster → `Occupied` (stack limit 1); else send `REQUEST_MOVE` → `Sent`. Nothing but `Sent` sends a packet. The walk starts only on `PLAYER_MOVE` (no prediction).
- **Turn rule** (FR-13): target direction `t` from the own cell to the clicked cell (Hercules `map_calc_dir`, ported). With body `b`: `(t − b) mod 8 == 1` → head `Left`; `== 7` → head `Right`; `== 0` → head `Straight`; otherwise body `t`, head `Straight`. Sends `CHANGE_DIRECTION(head, body)`; because the server does not echo it to the sender (T0.2, D9), the same turn is applied to the model when the packet was queued; a turn the server sends for the own id is applied too.
- **`/doridori`**: head Left, then Right, then Straight, 200 ms apart (three `CHANGE_DIRECTION`); a new command restarts it. **`/bangbang`**: body one step clockwise, `(b + 7) mod 8`, head Straight. Both are checked against the original client in T0.4 (timing, order) and the constants live in `WorldSettings`.
- **Sit** (FR-19): `ToggleSit` sends `REQUEST_ACT(0, 3)` when sitting, `(0, 2)` otherwise; the model changes only on `UNIT_ACT` 2/3 for the own id. A refusal leaves the state unchanged (the server sends nothing).
- **Map change** (FR-15): `ZoneManager` events (§4.8) → `EndMap`; the flow goes to `LoadingMap` and the loader runs again (§4.7). The own entry position for the new map is the packet's `(x, y)`; facing is north, or kept when `WorldSettings.WarpKeepsDirection` is on (Hercules `player_warp_keep_direction`, default off; D14).

### 4.7 Map loading: `WorldBootstrap`, `MapSceneLoader`

Satisfies FR-1, FR-12, FR-15.

```csharp
public sealed class WorldBootstrap : MonoBehaviour   // in Bootstrap.unity, next to the network client and DevAutoLogin
{
    public RoCamera CameraRig;                      // PLAN-0009 rig prefab instance
    public WorldSettings Settings;
    // Owns: ServerClock, WorldSession, MapSceneLoader, WorldPresenter, AddressablesUnitAssetSource (D12), UnitLoader, OwnInput.
    public Material UnitMaterial;                   // sprite_unit.mat (UnitView.Create)
}

public sealed class MapSceneLoader
{
    public enum Result : byte { Loaded, MissingScene, Failed }
    public void Load(string mapName, Action<Result, MapRoot> done);     // unloads the previous map first
}
```

- `WorldBootstrap` listens to `NetworkFlow.StateChanged`. On `LoadingMap` with `Zone.Authenticated` (first entry, map change, or a new map server), it takes `Context.Map.MapName` without the `.gat` suffix and loads `map/<name>` and `map/<name>.additive` (if registered) **additively** through Addressables, keeping Bootstrap loaded. When the scene is loaded it finds its `MapRoot`, calls `WorldSession.BeginMap(root.Data)`, creates the own view, sets `RoCamera.Follow` to it and snaps the camera (§4.10), then calls `Flow.MapLoaded()`. Hercules then sends the area (FR-1 order).
- **Missing scene** (FR-1): the key is not in the catalog → one `LogWarning` naming the map; the session continues with a model on an empty grid (`IWalkGrid` of the size the server needs is unknown, so an "unknown grid" that refuses every own walk request), `Flow.MapLoaded()` is still called so the server keeps the session, and nothing is drawn.
- **Deadline**: SPEC-0001 already fails the flow if the load exceeds `MapLoadSeconds`.
- `MapSceneLoader` is the only code that loads or unloads map scenes. Unload releases the Addressables handles.

### 4.8 Networking changes: `ZoneManager`, `NetworkFlow`

Satisfies FR-2, FR-15.

```csharp
// ZoneManager (added)
public event Action<uint, long, long> HeartbeatAnswered;       // server tick, local sent ms, local received ms
public event Action MapChangeOrdered;                          // MAP_CHANGE: Context.Map name and Context.Entry updated first
public event Action MapServerChangeOrdered;                    // MAP_SERVER_CHANGE: Context.Map endpoint and Entry updated first
```

- `MAP_CHANGE`: `ZoneManager` writes the new map name into `Context.Map` (same host and port) and a new `Context.Entry` (tick = `ServerClock`-independent 0, position from the packet, previous facing), raises `MapChangeOrdered`. `NetworkFlow` (in `InGame`) stops the heartbeat, sets `_authenticated = true`, starts the `MapLoadSeconds` deadline and enters `LoadingMap`. `MapLoaded()` then sends `MAP_LOADED` again and returns to `InGame`, as on first entry.
- `MAP_SERVER_CHANGE`: `ZoneManager` stores the new endpoint (IP from big-endian, port as written, map name, DNS host) and raises `MapServerChangeOrdered`. `NetworkFlow` (in `InGame`) disconnects the current map connection silently, re-activates `ZoneManager`, `BeginConnecting(ConnectingToMap)` and connects to the new endpoint; `REQUEST_ENTER_MAP` with the same account, character and auth code follows as on first entry (the char server already handed the session over, Hercules `chrif_changemapserver`). From there the first-entry path runs unchanged.
- Both transitions are new edges of the SPEC-0001 state machine and get state-machine tests with the simulated connection (SPEC-0001 FR-20). `InGame` disconnects still go to `Disconnected`.

### 4.9 Model to views: `WorldPresenter`, `EntityLookMapping`, `GroundItemView`

Satisfies FR-3, FR-4 to FR-8, FR-16, FR-17.

```csharp
public sealed class WorldPresenter
{
    public WorldPresenter(UnitLoader loader, Material unitMaterial, Transform root, MapData map);   // D12; map null: draws nothing
    public void Sync(WorldModel model);        // per frame after Advance; allocates nothing in steady state
    public UnitView ViewOf(uint id);           // own view for the camera, debug view
    public void ReleaseAll();
}
public static class EntityLookMapping { public static UnitLook ToUnitLook(EntityKind kind, in EntityLook look); }
```

- **Read only** (FR-3): the presenter never calls a model operation. Per frame: drain removed ids → return their views to a pool (one pool of `UnitView` and one of `GroundItemView`, pre-warmed to 64 each, grown on demand; a view going back to the pool is cleared first, D13); drain added ids → take a view from the pool; for each slot whose `Version` differs from the view's copy: `SetLook` if the look changed (FR-8 via 0009 FR-9), `Facing`, `HeadFacing`; if `AnimationVersion` changed: `Play` walk (loop) / idle (loop) / sit (loop) / dead (once and hold). Every walking or moved entity gets `UnitView.PlaceOn(map, position)` with `position` from `PosX/PosY` (`MapData` origin, cell size and row sign, §1; height from `TryGetHeight`).
- **Walk animation speed**: `UnitView`'s clock speed is set so the walk cycle matches the walk speed as the client does (the original client scales the walk animation by speed / 150, roBrowser the same; checked in T0.3). `UnitView.Speed` is the hook (T0.1: it multiplies the frame time, 2 plays at half speed, so the walk cycle is `Speed = walk speed / 150`); every other animation plays at 1.
- **Look mapping**: players (`Kind == Player`, or any kind whose job id is a player class, 0009 FR-2) get a player `UnitLook` (job, sex, hair, colours, headgears, robe, weapon/shield view ids, body style); all other kinds `UnitLook.ForJob(job)`. Pets, homunculi, mercenaries and elementals are shown by job id (non-goal: no control).
- **Ground items**: `GroundItemView` is a small component with one driven `SpriteActor` (PLAN-0007), or one `SpriteRenderer` when the catalog's `SpriteRef` is a still (T0.1), showing the ground sprite from `SpriteCatalog.TryGetGroundItem(itemId, identified)`; an item without a sprite logs one warning per item id and shows nothing. Items lie on the ground (same camera-facing and ground placement as units, PLAN-0009 D7).
- **Vanish visuals**: none in this spec; the presenter only removes. `WorldModel.Vanished` is the effects spec's hook.

### 4.10 Camera follow

Satisfies FR-12.

`WorldBootstrap` sets `RoCamera.Follow` to the own `UnitView` transform after `BeginMap` and calls `RoCamera.SnapToTarget()` (PLAN-0009 §4.7, D13, task T6.5 of TASKS-0009: built with the camera in SPEC-0009) so the first frame after a map change already shows the character.

### 4.11 Input: `CellPicker`, `CellCursor`, `OwnInput`

Satisfies FR-9, FR-11, FR-13, FR-19.

```csharp
public sealed class CellPicker
{
    public CellPicker(MapData map);
    public bool TryPick(Ray ray, out int x, out int y);     // ground cell under the ray, heights included
}
public sealed class CellCursor : MonoBehaviour { public void Show(int x, int y, bool canWalk); public void Hide(); }
public sealed class OwnInput : MonoBehaviour { /* Input System actions: Walk (left button), Turn (shift + left), Sit (Insert) */ }
```

- **Picking** (FR-11): march the ray across the cell grid in its xz projection (DDA over cells, nearest first); for each cell, intersect the ray with the cell's two triangles (corner heights, diagonal rule of PLAN-0005 D11); the first hit is the cell. A ray that hits nothing returns false. Only `MapData` is used, no colliders (models such as bridges are not walkable cells unless the GAT says so). Allocates nothing.
- **Cursor**: a quad mesh built once with the four corner heights of the hovered cell, lifted 0.01 u; colour from `CanWalk` = `OwnCharacter` walk rules without sending (`RequestWalk` has a `dryRun` overload). Hidden when the pointer is over no cell.
- **Walk** (FR-9): press → `RequestWalk(cell)`; while held, again every `WorldSettings.HoldRepeatMs` (500) and when the hovered cell changes. **Turn**: shift + press → `TurnTowards(cell)`, no walk. **Sit**: Insert → `ToggleSit`. The camera keeps the right button and wheel (PLAN-0009), so the maps do not collide; when the dev command line has focus, game input is off.

### 4.12 Dev tools: `WorldDebugView`, `DevCommandLine`

Satisfies FR-13, FR-14, FR-19.

- `DevCommandLine`: Enter opens a one-line IMGUI text field at the bottom of the screen; commands `/doridori`, `/bangbang`, `/sit`, `/stand` call `OwnCharacter`; an unknown command prints one line. A table of commands, so later specs add theirs.
- `WorldDebugView` (F9 toggles): list of entities (id, kind, cell, state, walking), the server time estimate and its last round trip; on the map, the own path as lines and the own server cell as a square (`GL` lines in `OnRenderObject`, no allocation).
- Both are compiled out of release builds (§3).

## 5. Threading and lifecycle

- Main thread only. Packets arrive through SPEC-0001's per-frame dispatch.
- Order per frame: network dispatch (`NetworkClient`, early) → `OwnInput.Update` → `WorldBootstrap.LateUpdate`: `OwnCharacter.Tick`, `WorldModel.Advance(clock.Now)`, `WorldPresenter.Sync` → `RoCamera` / Cinemachine brain → `UnitSystemDriver` → `SpriteSystemDriver` (PLAN-0009 §5). Values (T0.1, from the merged code): `NetworkClient.Update` −100 (pump and dispatch); `OwnInput.Update` 0; **`WorldBootstrap.LateUpdate` 10**; `RoCamera.LateUpdate` 50 (eases its target to `Follow`, so the view must be placed before it); `CinemachineBrain.LateUpdate` 100; `UnitSystemDriver.LateUpdate` 900; `SpriteSystemDriver.LateUpdate` 1000. `WorldDebugView` draws after all of them (`OnRenderObject`).
- Lifetimes: `WorldBootstrap` lives with Bootstrap; one `WorldModel` per map (created in `BeginMap`, cleared in `EndMap` and on `Disconnected`/`Failed`); views pooled across maps. Handlers registered through `Subscriptions` are released with `WorldSession.Dispose` (SPEC-0001 FR-9).
- Statics: none in World. `AddressablesUnitAssetSource` is owned by `WorldBootstrap` (PLAN-0009 §5).

## 6. Test plan

EditMode unless marked. `NeedsData` tests need the imported maps/catalog and are ignored with a message without them.

| Test | Covers |
|---|---|
| `ServerClock_Offset5s_Rtt50To200_WithinHalfRttPlus10`, `_MonotonicOver10Minutes`, `_LowerCorrectionSlews`, `_Wraps` (fake `IClock`, simulated server) | FR-2 |
| `HerculesPathfinder_MatchesServer_<map>` (10,000 fixture pairs each for prontera, izlude, prt_fild08; step by step, result codes included) — `NeedsData` | FR-10 |
| `HerculesPathfinder_EasyPath_MatchesServer_<map>` (same fixtures), `SearchForUnit_WalkLog_<capture>` (share reproduced ≥ the value T3.8 records) — `NeedsData` | FR-6, FR-10 |
| `HerculesPathfinder_CornerRule`, `_SameCell`, `_NodeCollisionAborts`, `_Limits17And14`, `_ZeroAllocation1000Searches` | FR-10, FR-17 |
| `WalkTiming_200Straight5_Is1000ms`, `_OneDiagonal_Is1080ms`; `WorldModel_Walk_ArrivesAtServerTime`, `_ServerCellChangesAtStepEnd`, `_MidWalkEntryAppearsOnPath` | FR-4, FR-6 |
| `WorldModel_UpsertTwice_OneEntity`, `_NewOrderWhileWalking_NoSnapWithinOneCell`, `_Stop_SnapsAndIdles`, `_Slide_KeepsFacing` | FR-4, FR-6, FR-7 |
| `WorldModel_Vanish_<Reason>_<Kind>` (5 reasons × monster/player), `_UnknownId_Ignored`, `_RaisesVanishedOnce` | FR-5 |
| `WorldModel_SetLook_EachLookType`, `_Turn`, `_SitStand` | FR-8, FR-19 |
| `WorldModel_ZeroAllocation_300Walkers` (Advance over 600 frames after warm-up) | FR-17 |
| `WorldSession_Replay_<capture>` (recorded real session fed through the router into a model, no scene; final entity set and cells equal the capture's "who is here" list; every later stop/move finds its unit within one cell) | FR-3, FR-4, FR-6, FR-18 |
| `WorldSession_ViewlessEntitiesStillUpdate` | FR-3 |
| `OwnCharacter_RequestWalk_<Result>` (each result; only `Sent` sends), `_TurnTowards_8Neighbours`, `_Doridori_Sequence`, `_Bangbang_Clockwise`, `_Sit_TogglesOnConfirmOnly`, `_DeadOnEntry_NoInput` | FR-1, FR-9, FR-13, FR-19 |
| `NetworkFlow_MapChange_LoadingMapThenInGame`, `_MapServerChange_Reconnects`, `ZoneManager_HeartbeatAnswered_RttFromSendTime` | FR-2, FR-15 |
| Packet size and decode tests for every packet of §4.1 (PLAN-0001 rules) | FR-4 to FR-9, FR-15, FR-16, FR-19 |
| `CellPicker_PrtFild08Slope_4Turns_MinMaxTilt` (PlayMode, `NeedsData`: picked cell equals the cell whose triangles contain the ray hit computed independently) | FR-11 |
| `WorldPresenter_ReadsOnly` (model checksum unchanged by `Sync`), `_ZeroAllocationSteadyState` (PlayMode) | FR-3, FR-17 |

Manual checks on a local Hercules (evidence in `docs/specs/0010-world-session/evidence/`): entry on prontera (FR-1); 10-cell walk with the debug view and `@where` (FR-9, FR-14); cursor on prt_fild08 (FR-11); camera after a warp (FR-12); shift + click, commands and sit seen from a second client (FR-13, FR-19); south warp and, with two map servers, a server move (FR-15); a dropped potion (FR-16); the 300-unit profiler capture (FR-17, by `@monster` and dev bots or a replayed capture); spec §5 five-minute walk.

## 7. Migration and compatibility

- **`DevAutoLoginDriver`**: its `LoadingMap → Flow.MapLoaded()` action is removed; `WorldBootstrap` calls it after the scene loads. For runs without a world (the `NetworkProbe`), a serialized flag `completeMapLoadWithoutWorld` (default `false`, set on the probe's scene object) keeps the old behaviour.
- **Heartbeat at entry** (D6): one extra `HEARTBEAT_REQUEST` right after `MAP_LOADED`. The SPEC-0001 keepalive tests that count heartbeats get the extra one; FR-14 of SPEC-0001 ("every 10 s") still holds for the interval.
- **Bootstrap scene**: gains `WorldBootstrap`, the PLAN-0009 camera rig and an EventSystem-free input setup. The BACKLOG item on `DevAutoLogin` missing in release players also applies to `WorldBootstrap`'s dev children; not fixed here.
- **Packet docs** move from `documented` to `implemented`/`verified`; `INDEX.md` is regenerated.
- `RoCamera.SnapToTarget` was moved into PLAN-0009 (D13, TASKS-0009 T6.5) on 2026-11-21; this plan only calls it.

## 8. Decisions

| # | Chosen | Rejected | Why |
|---|---|---|---|
| D1 | One plain C# `WorldModel` (struct array by slot, id → slot map) that only `WorldSession` writes; views read it each frame by version numbers | Packets routed to entity `MonoBehaviour`s; events per change | Spec FR-3 (owner roadmap): one owner, testable without a scene, no per-change delegate traffic; versions make the sync cost proportional to changes. |
| D2 | The client computes paths for every walking unit with the same A* as the server, from the packet's two ends | Straight-line interpolation; asking the server for paths | The packets carry only ends (as for the original client); a straight line cuts walls and drifts from the server's cell. |
| D3 | Port `path_search` line by line (including its 32×32 node table and heap) and prove it with paths dumped by a Hercules plugin running the server's own code | A "clean" A* with the same costs; comparing against roBrowser | Ties and the node-table collision decide real paths; only the server's own function is a trustworthy reference, and a plugin runs it on the same map cache. |
| D4 | Server cell changes at the end of a step; shown position interpolates | Cell changes at mid-step | Matches `unit_walk_toxy_timer`; the own walk request must start where the server's path starts. |
| D5 | Map scenes through an Addressables group `maps`, loaded additively beside Bootstrap | Build-settings scene list; one scene per map loaded singly | The project already ships content through Addressables (PLAN-0008); additive keeps network and camera objects alive across map changes. |
| D6 | One heartbeat right after `MAP_LOADED`, then every 10 s; lowest-RTT of 8 samples; forward jumps, backward slew 0.5 ms/ms | Only the 10 s heartbeat; averaging samples | Walks arriving in the first 10 s need a measured clock; the least-delayed sample is the most accurate; slewing keeps time monotonic (FR-2). |
| D7 | A new walk order within one cell of the shown position starts from there; farther snaps | Always snap to the packet's source; always walk to the source first | Avoids visible jumps for normal re-orders without hiding real desyncs. |
| D8 | Dead monsters stay `DeadMonsterLingerMs` (1,500 ms) then are removed; the view plays dead once and holds | Removing when the animation ends (model would depend on the view) | Keeps the model independent of animation data; the value is a setting, checked against the original client in T0.3. |
| D9 | Own **sitting** changes the model only on the server's echo (`UNIT_ACT`); own **turns** are local on send (T0.2: `clif_parse_ChangeDir` sends `AREA_WOS`, no echo, and never refuses) | Local change on send for both; waiting for an echo of turns | Others see what the server accepted; a refused sit stays standing (FR-19). A turn is never refused, so what is sent is what the server has. |
| D10 | Cell picking on `MapData` triangles, no colliders | Physics raycast on the ground mesh | Walkability lives in `MapData`; no physics setup for map scenes; exact on slopes. |
| D11 | Other non-player units: easy path if it reaches the destination, else A*; measured in T3.8 with the server's walk log | Always A* (the original client's behaviour); changing the server so chases use A* (`walk_tobl` flag) or so move packets carry the path | The packets do not say which algorithm ran; chases are where position matters (combat). A server change is out of this spec (the owner plans server changes later, spec non-goals) and would make the rule unnecessary. |
| D12 | (T0.1) Views are made with `UnitView.Create(UnitLoader, Material, parent, name)`, so the presenter takes a `UnitLoader`; `AddressablesUnitAssetSource` is not constructed directly but by the asynchronous `AddressablesUnitAssetSource.LoadAsync`, which gets an optional list of extra catalog categories (ground items). `WorldBootstrap` starts it once and the presenter is created when both the map and the source are there | Constructor with `IUnitAssetSource`; a second catalog for the items | Follows the merged SPEC-0009 code (§1 rule); its category list has no ground items, and one catalog is enough. The session does not wait for the catalog: the model runs without views (FR-3) |
| D13 | (T0.1) `UnitView.Clear()` is added to SPEC-0009's component: it releases the parts, forgets the look and shows nothing | Pooling a view as it is; destroying and creating views | A pooled view keeps its old look until the next one is loaded (`SetLook`), so a reused view would show the previous unit for a moment, and the pool would keep its assets held |
| D14 | (T0.2) After a map change the own character faces north (`WorldSettings.WarpKeepsDirection` false, Hercules `player_warp_keep_direction` default) | Keeping the facing (§4.6 as first written) | `pc_setpos` sets `UNIT_DIR_NORTH`, and the move packet has no facing: the others see north |

## 9. Risks

| Risk | Mitigation |
|---|---|
| A packet layout differs at 20220406 from the docs (BACKLOG precedent) | T0.2 checks every packet in `packets_struct.h`/`clif.cpp` and a real capture before any handler is written |
| Client and server cell maps differ on a map | Tests use the three maps with rebuilt caches; BACKLOG item for the rest |
| Monsters walk with a path the server found differently (easy path for chases, confirmed in `mob.cpp`; chase trimming) | The packet's destination already includes the trimming; D11 rule, measured with the walk log in T3.8; a mismatch is bounded (same ends) and corrected by the next stop or move packet. Removing it for good needs a server change (D11, rejected alternative) |
| Clock error makes walks start late | Sync at entry (D6); FR-2 tests; debug view shows round trip |
| The plugin cannot be built on the owner's Windows setup | Fallback: a console program compiling `path.cpp` with a stub `map_data` read from the same `.mcache`; same output format |
| Known server-side position quirks: the A* node table (32×32) aborts some valid paths (`path.cpp` FIXME); a speed change mid-walk is resent as a full walking-unit entry (`clif_move`); knock-back is a stop plus a slide (`clif_blown`); the server cell lags the shown position by up to one step (D4) | The port reproduces the abort (the client refuses the same clicks the server would); entries for a known id update in place (FR-4); stop and slide are FR-7; range checks against the server cell are the combat spec's concern |
| 0009's display contracts change during its implementation | §1: follow the code, record in Decisions |
| Look-type numbers of 0x01D7 differ from the doc | T0.2 reads `enum look` and writes the table |

## 10. Traceability

| FR | Plan sections | Tests / evidence |
|---|---|---|
| FR-1 | 4.1, 4.6, 4.7 | `OwnCharacter_DeadOnEntry_NoInput`; manual entry on prontera; missing-scene warning test (PlayMode) |
| FR-2 | 4.2, 4.8 | `ServerClock_*`, `ZoneManager_HeartbeatAnswered_*` |
| FR-3 | 2, 4.5, 4.9 | `WorldSession_Replay_*`, `_ViewlessEntitiesStillUpdate`, `WorldPresenter_ReadsOnly` |
| FR-4 | 4.1, 4.5, 4.6 | `WorldModel_MidWalkEntryAppearsOnPath`, `_UpsertTwice_OneEntity` |
| FR-5 | 4.5, 4.9 | `WorldModel_Vanish_*` |
| FR-6 | 4.3, 4.5, 4.9 | `WalkTiming_*`, `WorldModel_Walk_*`, replay test |
| FR-7 | 4.5 | `WorldModel_Stop_*`, `_Slide_*` |
| FR-8 | 4.1, 4.5, 4.9 | `WorldModel_SetLook_*`, `_Turn`, `_SitStand` |
| FR-9 | 4.4, 4.6, 4.11 | `OwnCharacter_RequestWalk_*`; manual 10-cell walk |
| FR-10 | 4.4 | `HerculesPathfinder_MatchesServer_*`, `HerculesPathfinder_*` |
| FR-11 | 4.11 | `CellPicker_PrtFild08Slope_*`; manual cursor check |
| FR-12 | 4.7, 4.10 | manual camera check after a warp |
| FR-13 | 4.6, 4.11, 4.12 | `OwnCharacter_TurnTowards_*`, `_Doridori_*`, `_Bangbang_*`; second-client check |
| FR-14 | 4.12 | manual; release build contains no `Uninarok.World.Dev` type |
| FR-15 | 4.6, 4.7, 4.8 | `NetworkFlow_MapChange_*`, `_MapServerChange_*`; manual warp |
| FR-16 | 4.1, 4.5, 4.9 | item packet tests; manual drop |
| FR-17 | 4.4, 4.5, 4.9 | `_ZeroAllocation*` tests; profiler capture at 300 units |
| FR-18 | 1, 3, 6 | the EditMode suite and the replay test |
| FR-19 | 4.1, 4.6, 4.11, 4.12 | `OwnCharacter_Sit_TogglesOnConfirmOnly`; second-client check |
