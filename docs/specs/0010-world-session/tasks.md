# TASKS-0010: World Session and Movement

| | |
|---|---|
| **Implements** | [plan.md](plan.md) → [spec.md](spec.md) |
| **Status** | Approved (owner, 2026-11-21) |

**How to use this file**
- Start only when SPEC-0009 is implemented and merged into `main` (its `UnitView`, `UnitSystem`, `RoCamera` with `SnapToTarget` and `AddressablesUnitAssetSource` are what this spec builds on). Work on branch `feature/0010-world-session`.
- Do tasks **in order**. One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0010): T4.2 walk entities along their paths` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence (command + result).
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass.
- Through the Unity MCP, run tests in groups (BACKLOG: one full run exceeds the tool timeout). Tests marked `NeedsData` need the imported maps or catalog, or the fixtures of T3.4/T0.5, and are ignored with a message when they are absent.
- "No new warnings" is checked in `Logs/Editor.log` after a refresh, not in the console.
- Paths are relative to `Uninarok-client/Assets/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a phase 0 check shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Phase 0 scripts are scratch code: delete them when the task is done.
- Reference documents are updated in the same commit as the code they describe: the packet docs and `INDEX.md` (`docs/specs/0001-networking-core/Packets/`), `docs/data/UninarokMoveData.md`, `docs/data/UninarokPosition.md`, the fixtures `README.md`.
- Evidence (screenshots, profiler captures, logs) goes to `docs/specs/0010-world-session/evidence/` and is linked from the task's report.
- Local server: the Hercules build in `Hercules-2026.09/` with the map caches of prontera, izlude and prt_fild08 rebuilt from the client data (BACKLOG). A second client for "seen by others" checks is a second editor player or a build connected with another account.
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Prepare

- [x] **T0.1** Read the merged SPEC-0009 code and fix the facts this plan relies on. (FR-1, FR-6, FR-12)
  The `DefaultExecutionOrder` of `UnitSystemDriver` and `SpriteSystemDriver`, the signatures of `UnitView` (`SetLook`, `Facing`, `HeadFacing`, `Play`, `PlaceOn`), `UnitClock.Speed`, `RoCamera.Follow` and `RoCamera.SnapToTarget`, `AddressablesUnitAssetSource` construction.
  *Done when:* plan §5 holds the execution order values; any signature that differs from plan §4.9/§4.10 is recorded under Decisions and the plan text follows the code.
  *Result (2026-11-22):* orders in plan §5 (`WorldBootstrap` 10, before `RoCamera` 50, brain 100, `UnitSystemDriver` 900, `SpriteSystemDriver` 1000); `SetLook`, `Facing`, `HeadFacing`, `Play`, `PlaceOn`, `RoCamera.Follow` and `SnapToTarget` as planned; `UnitView.Speed` multiplies the frame time; differences in D12 (loader, asynchronous source, ground items) and D13 (`UnitView.Clear`).

- [x] **T0.2** Verify every packet of plan §4.1 against Hercules for PACKETVER 20220406. (FR-4 to FR-9, FR-13, FR-15, FR-16, FR-19)
  For each: ID, size, field layout in `packets_struct.h` and `clif.cpp`; the look types of `0x01D7` (`enum look`, `map.h`); the `objecttype` values (`clif_bl_type`); how `UNIT_STAND_ENTRY`/`UNIT_NEW_ENTRY` carry sitting and dead; whether Hercules sends `0x009C` and `0x008A` back to the player who turned or sat (plan D9). Check against a real capture where the packet appears (T0.5 may provide it).
  *Done when:* a table of the results is written into plan §4.1 (a Decisions note for any difference); D9 says "echo" or "local on send".
  *Result (2026-11-22):* table in plan §4.1, from the sources and the real capture of SPEC-0001 (0x0091, 0x01D7, 0x09FF, 0x00B0 seen; the rest come with T0.5). Differences: no echo of turns (D9 is now "local on send" for turns, "echo" for sitting), north after a warp (D14), no `state` in 0x09FE/0x09FD (107 and 114 bytes), weapon values may be item ids, uninitialised bytes in 0x008A.

- [x] **T0.3** Read the display facts from roBrowser's source and the original client. (FR-5, FR-6, FR-16)
  Item sub-cell to position formula; walk animation speed as a function of walk speed; how long a dead monster stays before it disappears.
  *Done when:* the three rules are in plan §4.5/§4.9 with their source (file and line, or screenshot), `WorldSettings` defaults updated if they differ (Decisions note), and the item rule is in `ITEM_FALL_ENTRY.md`.
  *Result (2026-11-22):* from roBrowserLegacy's source (rules and file names in plan §4.5, §4.9, D8, and `ITEM_FALL_ENTRY.md`); the dead monster time changed from 1,500 to 5,000 ms. The original client is not available here: the owner sees the three in T9.1.

- [ ] **T0.4** Owner: compare the own-character turn controls with the original client. (FR-9, FR-13)
  Shift + click on each of the 8 neighbours; `/doridori` (order and timing); `/bangbang` (direction); the repeat rate when the left button is held.
  *Done when:* the results are written into plan §4.6/§4.11 (Decisions note for any difference from the spec's rule, which then goes to the owner as a spec change, CLAUDE.md §1.4 rule 6).
  *Open (2026-11-22):* the owner compared (2026-11-22): `/bangbang` is right; shift + click was not: the second click on the head's side turns the body (fixed, D28, FR-13 amended); `/doridori` could not be seen, the character had no head (fixed, D27). Second comparison (2026-11-22): the body turns on the second click, right; a click two or three steps away turned the body and the head the same amount, where the original leaves the body one step short (fixed, D31); `/doridori` ran its steps in one command, where the original does one for each (fixed, D32); FR-13 amended. Third (2026-11-22): `/doridori` went back to straight, where the original only goes between left and right (fixed, D32, FR-13). Left: the fixed `/doridori` and shift + click, and the repeat rate of a held button.

- [x] **T0.5** Record real sessions for replay tests. (FR-3, FR-18)
  With the SPEC-0001 capture tee and the dev auto-login on the local server: (a) enter prontera, stand 60 s near NPCs and players; (b) walk among monsters on prt_fild08 for 3 minutes, including monster deaths and a dropped item; (c) walk into prontera's south warp. Save the server's "who is here" state at the end of each (GM `@who`-style dump or the walk log of T3.8 once available).
  *Done when:* the captures are under `docs/specs/0010-world-session/fixtures/` with a `README.md` (date, server commit, account kind, what happens in each), and none contains credentials (checked by search for the account name and password).
  *Order (2026-11-22):* done after T3.8, whose plugin gives the server's state; it needs the packets of phase 1 to walk (reorder allowed by CLAUDE.md §1.4 rule 6).
  *Result (2026-11-22):* three captures under `fixtures/` with the server's last view of each (`README.md`): prontera 60 s, prt_fild08 nine minutes with chases, deaths and dropped items, the south warp. No account name or password in them (searched). Done after T3.4's plugin gave the views.

## Phase 1: Packets

- [x] **T1.1** Write the new packet docs and update the existing ones. (FR-4 to FR-9, FR-13, FR-15, FR-16, FR-19)
  New: `UNIT_MOVE` (0x0086), `UNIT_SLIDE` (0x01FF), `UNIT_DIRECTION` (0x009C), `UNIT_LOOK_CHANGE` (0x01D7), `UNIT_ACT` (0x008A), `ITEM_ENTRY` (0x009D), `MAP_CHANGE` (0x0091), `MAP_SERVER_CHANGE` (0x0AC7), `PARAMETER_CHANGE` (0x00B0). Existing: the `objecttype` table in `UNIT_MOVE_ENTRY.md`, sit and dead fields from T0.2.
  *Done when:* every doc follows Packet-Implementation.md with T0.2's verified layout; `INDEX.md` is regenerated and lists them.
  *Result (2026-11-22):* 9 new docs, the `objecttype` table and state notes; `INDEX.md` regenerated from the docs (78 rows, sorted by direction and ID). 0x00B0 linked in `CATALOG.md`.

- [x] **T1.2** Tests, then structs: unit entries, moves, stop, slide, vanish (`0x09FD`, `0x09FE`, `0x09FF`, `0x0086`, `0x0087`, `0x0088`, `0x01FF`, `0x0080`). (FR-4 to FR-7)
  *Done when:* size tests against the Hercules table and decode tests (real samples from T0.5 where present, else hand-built from the doc) pass, each leaves no unread byte, and the hot ones pass an allocation test of 1,000 decodes at 0 B; docs moved to `implemented`/`verified`.
  *Result (2026-11-22):* the 8 structs; `Uninarok.Networking.Tests` 460 of 461 pass (the failing one is the line-ending fixture of the BACKLOG), with size tests (`PacketSizeTests`), 9 decode tests (`UNIT_STAND_ENTRY` also from the real capture) and 1,000 decodes of each at 0 B; docs implemented, `UNIT_STAND_ENTRY` verified. No new warnings.

- [x] **T1.3** Tests, then structs: direction, look change, act, parameter change (`0x009C`, `0x01D7`, `0x008A`, `0x00B0`). (FR-8, FR-13, FR-19)
  *Done when:* as T1.2.
  *Result (2026-11-22):* 466 of 467 networking tests pass (the known line-ending one fails): decode tests (look and parameter bytes from the real capture), sizes, 1,000 decodes at 0 B; `UNIT_ACT` ignores the uninitialised bytes.

- [x] **T1.4** Tests, then structs: ground items (`0x009D`, `0x0ADD`, `0x00A1`). (FR-16)
  *Done when:* as T1.2.
  *Result (2026-11-22):* 471 of 472 networking tests pass (the known line-ending one fails): decode, size and 1,000 decodes at 0 B; docs implemented.

- [x] **T1.5** Tests, then structs: map change and map-server change (`0x0091`, `0x0AC7`, with the IP and port byte orders of plan §4.1). (FR-15)
  *Done when:* as T1.2, plus a decode test of `0x0AC7` giving host `127.0.0.1` and a port above 32767 from bytes built as `clif_changemapserver` writes them.
  *Result (2026-11-22):* 474 of 475 networking tests pass (the known line-ending one fails): `MAP_CHANGE` from the real capture; `MAP_SERVER_CHANGE` gives 127.0.0.1 and port 50121 from bytes built as `clif_changemapserver` writes them.

- [x] **T1.6** Tests, then the outgoing packets in use: `REQUEST_MOVE`, `CHANGE_DIRECTION`, `REQUEST_ACT` (sit 2, stand 3). (FR-9, FR-13, FR-19)
  *Done when:* serialization tests give the bytes of the docs; send allocates 0 B; docs moved to `implemented`.
  *Result (2026-11-22):* 478 of 479 networking tests pass (the known line-ending one fails): golden bytes of the docs, 0 B over 1,000 serializations and sends.

## Phase 2: Networking flow

- [x] **T2.1** Tests, then implementation: `ZoneManager.HeartbeatAnswered` and the extra heartbeat right after `MAP_LOADED` (plan §4.2, D6). (FR-2)
  *Done when:* `ZoneManager_HeartbeatAnswered_RttFromSendTime` passes; an answer with no outstanding request raises nothing; the SPEC-0001 keepalive tests are updated for the one extra heartbeat and pass (interval still 10 s).
  *Result (2026-11-22):* `ZoneManager_HeartbeatAnswered_RttFromSendTime` and two more pass; an answer with no outstanding request raises nothing; the keepalive tests count the extra heartbeat (481 of 482, the known line-ending one fails). The loopback journey test reads it too.

- [x] **T2.2** Tests, then implementation: `MAP_CHANGE` in `ZoneManager` and the `InGame → LoadingMap → InGame` edge of `NetworkFlow` (plan §4.8). (FR-15)
  *Done when:* `NetworkFlow_MapChange_LoadingMapThenInGame` passes with the simulated connection (`Context.Map` name and `Context.Entry` updated, `MAP_LOADED` sent again, deadline armed); disconnect in the new `LoadingMap` still fails the flow.
  *Result (2026-11-22):* `NetworkFlow_MapChange_LoadingMapThenInGame` and 3 more pass (485 of 486, the known line-ending one fails). One event for the game, `MapLoadRequested` (D15); the facing is north (D14).

- [x] **T2.3** Tests, then implementation: `MAP_SERVER_CHANGE` and the reconnect to the new map server (plan §4.8). (FR-15)
  *Done when:* `NetworkFlow_MapServerChange_Reconnects` passes (old connection dropped silently, connect to the new endpoint, `REQUEST_ENTER_MAP` with the same account, character and auth code, then the first-entry path).
  *Result (2026-11-22):* `NetworkFlow_MapServerChange_Reconnects` and the failure test pass (487 of 488, the known line-ending one fails).

- [x] **T2.4** `DevAutoLoginDriver`: remove the automatic `Flow.MapLoaded()` and add `completeMapLoadWithoutWorld` for the network probe (plan §7). (FR-1)
  *Done when:* with the flag off, the flow stays in `LoadingMap` until something calls `MapLoaded`; with it on (probe scene), the `network` probe reaches `InGame` as before.
  *Result (2026-11-22):* 27 `DevAutoLoginTests` pass. Live (editor, local server): flag off, the flow stays in `LoadingMap`; flag on, `InGame`, also through the server's warp right after `MAP_LOADED` (the driver follows `MapLoadRequested`); the `network` probe, run in the editor, reports outcome=ok. The flag is also on when the player is started as a probe (D16). Not run in a built player.

## Phase 3: Time and pathfinding

- [x] **T3.1** Create the `Uninarok.World`, `Uninarok.World.View` and `Uninarok.World.Dev` assemblies, their test assemblies, and `WorldSettings` with its asset (plan §3). (FR-18)
  *Done when:* the project compiles with no new warnings; an empty EditMode test in `Tests/EditMode/World/` runs; `Settings/World/WorldSettings.asset` holds the plan's defaults (17, 14, 500 ms, 5,000 ms, 200 ms, walk animation 150).
  *Result (2026-11-22):* compiles with no new warnings (Editor.log); `WorldSettings_Asset_HoldsThePlanDefaults` runs and passes in `Uninarok.World.Tests`. Dev assembly excluded by its define constraint (D17).

- [x] **T3.2** Tests, then implementation: `ServerClock` (plan §4.2). (FR-2)
  *Done when:* `ServerClock_Offset5s_Rtt50To200_WithinHalfRttPlus10`, `_MonotonicOver10Minutes`, `_LowerCorrectionSlews`, `_Wraps` pass; `Sample` allocates 0 B.
  *Result (2026-11-22):* 20 `ServerClock_*` test cases pass (the four named ones, five seeds and both offset signs for the accuracy one); `Sample` 0 B over 1,000 calls.

- [x] **T3.3** `IWalkGrid`, `MapDataWalkGrid`, `WalkTiming` (plan §4.3). (FR-6, FR-10)
  *Done when:* `WalkTiming_200Straight5_Is1000ms`, `_OneDiagonal_Is1080ms` pass; `MapDataWalkGrid` agrees with `MapData.IsWalkable` on every cell of prontera (`NeedsData`), with `y = z`.
  *Result (2026-11-22):* the timing tests pass; `MapDataWalkGrid` agrees with `MapData.IsWalkable` on every cell of prontera. `UnknownWalkGrid` is the grid of a map with no data (plan §4.7).

- [x] **T3.4** Hercules plugin `uninarok_pathdump`, pair mode, and the reference fixtures (plan §4.4, D3). (FR-10)
  Built with the server's build; seed and pair count from its command line; both A* (flag 0) and easy path (flag 1) results, plus `path_search_long`.
  *Done when:* the plugin builds with no new warnings in the Hercules build, a normal server start does not load it, and `fixtures/paths-prontera.bin`, `paths-izlude.bin`, `paths-prt_fild08.bin` (10,000 pairs each, including "no path" and "too long" cases) are committed with seed and server commit in the fixtures `README.md`. (Fallback of plan §9 if the plugin cannot be built: record it under Decisions first.)
  *Result (2026-11-22):* the plugin builds with no warnings (`ninja plugin.uninarok_pathdump`), is not in `conf/plugins.conf`, and wrote 10,000 pairs for each map (counts in the fixtures README).

- [x] **T3.5** Tests for `HerculesPathfinder` (plan §4.4, §6). (FR-10, FR-17)
  `HerculesPathfinder_MatchesServer_<map>` and `_EasyPath_MatchesServer_<map>` over the T3.4 fixtures (`NeedsData`), and synthetic `_CornerRule`, `_SameCell`, `_NodeCollisionAborts`, `_Limits17And14`, `_ZeroAllocation1000Searches`.
  *Done when:* the tests compile and are ignored pending T3.6/T3.7.
  *Result (2026-11-22):* the 18 test cases compile and are ignored pending T3.6/T3.7; the fixture loader also checks that the client's cells hash to the server's.

- [x] **T3.6** Implement the A* port `Search` (plan §4.4). (FR-10, FR-17)
  *Done when:* `HerculesPathfinder_MatchesServer_*` pass for all 30,000 pairs step by step (result codes included), the synthetic A* tests pass, and 1,000 searches allocate 0 B; the mean search time on prontera is recorded in the report.
  *Result (2026-11-22):* `HerculesPathfinder_MatchesServer` passes for the 30,000 pairs of the three maps, step by step, with the result codes; the synthetic tests pass; 1,000 searches 0 B. Mean 4.3 µs per search over the prontera pairs (worst 162 µs, editor).

- [x] **T3.7** Implement `SearchEasy`, `StraightLine` and `SearchWalk` with the server's limits (plan §4.4). (FR-9, FR-10)
  *Done when:* `_EasyPath_MatchesServer_*` and `_Limits17And14` pass.
  *Result (2026-11-22):* `HerculesPathfinder_EasyPath_MatchesServer` and `_StraightLineAndWalkLimits_MatchServer` pass for the 30,000 pairs (easy path steps, straight line, accepted and too long); `_Limits17And14` passes.

- [x] **T3.8** Walk-log mode of the plugin and the D11 measurement; implement `SearchForUnit` (plan §4.4, D11). (FR-6, FR-10)
  Log a 10-minute session on prt_fild08 with monsters chasing and wandering.
  *Done when:* the log is a committed fixture; the report gives, for the default rule and for "always A*", the share of monster walks reproduced and the largest mid-walk distance on a mismatch; the better rule is in `SearchForUnit` (Decisions note if not the default); `SearchForUnit_WalkLog_*` passes at the measured share.
  *Order (2026-11-22):* waits for the owner to allow a restart of the local map-server with the plugin; phases 4 and 5 go on with the default rule of D11 (reorder allowed by CLAUDE.md §1.4 rule 6).
  *Result (2026-11-22):* nine-minute walk log on prt_fild08 committed. Always A*: 2,760 of 2,763 monster walks reproduced (99.89 %), largest mid-walk distance 1 cell; easy path first: 2,722 (98.52 %), 2 cells; players 273 of 273. `SearchForUnit` is now A*, with the easy path when A* finds none (D11); `SearchForUnit_WalkLog_PrtFild08` passes at 99.8 %.

## Phase 4: World model

- [x] **T4.1** Tests for `WorldModel` (plan §4.5, §6). (FR-3 to FR-8, FR-16, FR-17, FR-19)
  `WorldModel_UpsertTwice_OneEntity`, `_Walk_ArrivesAtServerTime`, `_ServerCellChangesAtStepEnd`, `_MidWalkEntryAppearsOnPath`, `_NewOrderWhileWalking_NoSnapWithinOneCell`, `_Stop_SnapsAndIdles`, `_Slide_KeepsFacing`, `_Vanish_<Reason>_<Kind>`, `_UnknownId_Ignored`, `_RaisesVanishedOnce`, `_SetLook_EachLookType`, `_Turn`, `_SitStand`, item add and remove, `_ZeroAllocation_300Walkers`.
  *Done when:* the tests compile and are ignored pending T4.2–T4.4.
  *Result (2026-11-22):* the 52 test cases compile and are ignored pending T4.2 to T4.4.

- [x] **T4.2** Implement storage, `Upsert`, `Walk` and `Advance` (plan §4.5, D4, D7). (FR-3, FR-4, FR-6)
  *Done when:* the upsert, walk, step-end, mid-walk and new-order tests pass.
  *Result (2026-11-22):* the upsert, walk, step-end, mid-walk, new-order, wrap and full-model tests pass (16 cases). `SearchForUnit` is in with the default rule of D11, measured in T3.8.

- [x] **T4.3** Implement `Stop`, `Slide`, `Turn`, `SetLook`, `SetState`, `SetSpeed`, `AddItem`, `Vanish` and the `Vanished` event (plan §4.5, D8). (FR-5, FR-7, FR-8, FR-16, FR-19)
  *Done when:* the remaining `WorldModel_*` tests except allocation pass.
  *Result (2026-11-22):* every `WorldModel_*` test but the allocation one passes (51 cases).

- [x] **T4.4** Allocation-free `Advance` and drains at 300 walkers (plan §4.5). (FR-17)
  *Done when:* `WorldModel_ZeroAllocation_300Walkers` passes (600 frames after warm-up, 0 B), and the mean `Advance` time for 300 walkers is recorded in the report.
  *Result (2026-11-22):* `WorldModel_ZeroAllocation_300Walkers` passes (600 frames, 0 B, with the walk orders inside). Mean `Advance` of 300 walkers: 3.1 µs, worst 112 µs (editor).

## Phase 5: Session and own character

- [x] **T5.1** Tests, then implementation: `WorldSession` handlers for every packet of plan §4.1 (plan §4.6). (FR-3 to FR-8, FR-15, FR-16, FR-19)
  *Done when:* one test per packet with the simulated connection shows the model change it causes; `WorldSession_ViewlessEntitiesStillUpdate` passes; handlers of hot packets allocate 0 B; handlers are released on `Dispose` (subscriber count back to zero).
  *Result (2026-11-22):* 15 `WorldSessionTests` pass: one test per packet, `WorldSession_ViewlessEntitiesStillUpdate`, handlers released on `Dispose` and on disconnect (count back to 0), 0 B over 1,400 deliveries of the hot packets.

- [x] **T5.2** Own character at entry from `CharInfo` and `Context.Entry`, dead on entry, speed from `PARAMETER_CHANGE` (plan §4.6). (FR-1)
  *Done when:* tests show the own entity with the selected character's look, cell, facing and speed; `OwnCharacter_DeadOnEntry_NoInput` passes (HP 0: state dead, every request refused, nothing sent).
  *Result (2026-11-22):* 8 `OwnCharacterTests` pass: look, cell, facing and speed at entry; `OwnCharacter_DeadOnEntry_NoInput` (extended by T5.3 to T5.5 as the requests come); after a map change the look and speed are what the server last said and the facing is north (D14).

- [x] **T5.3** Tests, then implementation: `OwnCharacter.RequestWalk` and its dry run (plan §4.6). (FR-9)
  *Done when:* `OwnCharacter_RequestWalk_<Result>` pass for every result, and only `Sent` sends a packet; the walk starts only on `PLAYER_MOVE`.
  *Result (2026-11-22):* `OwnCharacter_RequestWalk_*` pass for every result; only `Sent` sends (checked with the dry run), and the walk starts on `PLAYER_MOVE` only.

- [x] **T5.4** Tests, then implementation: `TurnTowards`, `Doridori`, `Bangbang` (plan §4.6, rules from T0.4). (FR-13)
  *Done when:* `OwnCharacter_TurnTowards_8Neighbours`, `_Doridori_Sequence`, `_Bangbang_Clockwise` pass with the `CHANGE_DIRECTION` bytes checked; the model follows the echo (or the send, per T0.2's D9).
  *Result (2026-11-22):* `OwnCharacter_TurnTowards_8Neighbours`, `_Doridori_Sequence`, `_Bangbang_Clockwise` pass with the `CHANGE_DIRECTION` bytes; the model turns when the packet was queued (D9). The rules are the spec's: T0.4 (the original client) is the owner's.
  *Changed (2026-11-22):* after the owner's comparisons the rules are those of D28, D31 and D32; `OwnCharacter_TurnTowards_8Neighbours`, `_TheHeadsSideAgain_TurnsTheBody`, `_Behind_TurnsBodyAndHead` and `_Doridori_Sequence` pass (World EditMode 161/161, PlayMode 44/44).

- [x] **T5.5** Tests, then implementation: `Sit`, `Stand`, `ToggleSit` (plan §4.6, D9). (FR-19)
  *Done when:* `OwnCharacter_Sit_TogglesOnConfirmOnly` passes (no state change without the server's `UNIT_ACT`; a walk request while sitting returns `Sitting` and sends nothing).
  *Result (2026-11-22):* `OwnCharacter_Sit_TogglesOnConfirmOnly` passes: no state change without `UNIT_ACT`, a walk request while sitting returns `Sitting` and sends nothing.

- [x] **T5.6** Replay tests over the T0.5 captures (plan §6). (FR-3, FR-4, FR-6, FR-18)
  *Done when:* `WorldSession_Replay_<capture>` pass for the three captures: the final entity set and cells equal the recorded state, no id appears twice, and every later stop or move finds its unit within one cell.
  *Result (2026-11-22):* `WorldSession_Replay` passes for the three captures, through the real framer and router: the final entity set and cells equal the server's last view (walkers within a cell), no decode error; on prt_fild08 1,466 later moves and stops, all within one cell of where the server had the unit.

## Phase 6: Map and views

- [x] **T6.1** Addressables group `maps` and `Tools/Ragnarok/Register Map Scenes` (plan §3, D5). (FR-1, FR-15)
  *Done when:* the menu adds prontera, izlude (with its additive scene) and prt_fild08 under `map/<name>` keys; running it twice changes nothing; the group asset is committed.
  *Result (2026-11-22):* the menu item added prontera, izlude (with its additive scene) and prt_fild08 under `map/<name>`; `RoMapSceneAddressablesTests` (3) pass: a second run changes nothing, a wrong address is corrected, the project's group holds the four entries; the group asset is committed.
  *Changed (2026-11-22):* an import registers its maps itself (D33), after the owner's int_land was imported and not found; `AnImport_RegistersItsMaps_OnlyInTheProjectsFolder` passes with the map batch, idempotency and architecture tests (29). int_land is registered in the owner's project (not committed, with the import).

- [x] **T6.2** `MapSceneLoader` and `WorldBootstrap` map entry, with the missing-scene case (plan §4.7). (FR-1)
  *Done when:* from Bootstrap with the dev auto-login, a character saved on prontera enters (the own unit on its saved cell and facing, then `MAP_LOADED` sent, then the area arrives); a PlayMode test with a fake flow shows one warning naming a missing map, the session kept and `MapLoaded` still called.
  *Result (2026-11-22):* 4 play mode tests pass (missing scene, entry, map change, disconnect). On the local server, from Bootstrap with the dev auto-login: DevHero enters iz_int and prontera on the saved cell, facing North, and the entity list then equals the server's view.

- [x] **T6.3** `EntityLookMapping` and `WorldPresenter` for units (plan §4.9). (FR-3 to FR-8)
  *Done when:* `WorldPresenter_ReadsOnly` and `_ZeroAllocationSteadyState` pass (PlayMode); on the local server, players, NPCs and monsters near the own character show with their looks, walk, turn and sit; a vanishing unit disappears as FR-5 says.
  *Result (2026-11-22):* `WorldPresenter_ReadsOnly` and `_ZeroAllocationSteadyState` pass with the other play mode tests. On the local server the NPCs and monsters near the own character show with their looks, walk, chase and vanish; with a second client (two Multiplayer Play Mode players) each sees the other's look, walk, turns, head and sitting. The head of hair style 0 was missing: D27.

- [x] **T6.4** `GroundItemView` (plan §4.9). (FR-16)
  *Done when:* a red potion dropped on the local server shows its ground sprite on the server's cell and sub-cell (T0.3 rule) and disappears when another client picks it up (screenshots in `evidence/`).
  *Result (2026-11-22):* play mode test passes (sprite, sub-cell, release). On the local server a red potion shows its ground sprite at its sub-cell and disappears when the other client picks it up (`evidence/t6.4-before-the-other-client-picks-up.png`, `t6.4-after-the-other-client-picked-up.png`).

- [x] **T6.5** Camera follow and snap on entry and map change (plan §4.10). (FR-12)
  *Done when:* walking across prontera keeps the own character centred; after a warp, the first rendered frame shows the character (frame capture in `evidence/`).
  *Result (2026-11-22):* walking across prontera the own character stays within 0.039 of the viewport's centre; the first frame after the south warp shows it (`evidence/t6.5-first-frame-after-warp.png`). That frame first lacked it: D24.

- [x] **T6.6** Map change end to end (plan §4.6–§4.8). (FR-15)
  *Done when:* walking into prontera's south warp loads prt_fild08 with the character on the server's cell and only that map's units (debug view list), prontera's scene unloaded; with two map servers on the local machine, a map-server move ends in game on the new server (evidence: log and screenshot).
  *Open (2026-11-22):* walking into prontera's south warp loads prt_fild08 with the character on 170,375 (the server's cell), only that map's units, prontera's scene unloaded (`evidence/t6.6-after-south-warp.png`). Left: the two map servers part cannot be run on this Hercules (D25).
  *Result (2026-11-22):* done on one map server as above. **The move to another map server was tested in simulation only (a simulated connection), not with two real servers**; the owner accepted that for now and FR-15 says it.

## Phase 7: Input

- [x] **T7.1** Tests, then implementation: `CellPicker` (plan §4.11, D10). (FR-11)
  *Done when:* `CellPicker_PrtFild08Slope_4Turns_MinMaxTilt` passes (PlayMode, `NeedsData`); picking allocates 0 B.
  *Result (2026-11-22):* `CellPicker_PrtFild08Slope_4Turns_MinMaxTilt` passes (the steepest walkable cell; 4 turns, tilts 20°, 50° and 65°; 441 rays each, against a brute-force triangle test); flat maps, a ground wall, misses; 0 B over 1,000 picks.

- [x] **T7.2** `CellCursor` (plan §4.11). (FR-11)
  *Done when:* on prt_fild08 the cursor lies on the hovered cell's slope and turns "not walkable" over a wall and over a cell too far to walk (screenshots in `evidence/`, both tilts).
  *Result (2026-11-22):* on prt_fild08 (simulated connection, real map) the cursor lies on the slope of cell 158,198 (0.45 units) and turns red over the rock at 305,312 and over 168,202, 18 cells away, at tilts 50° and 65°: `evidence/t7.2-*.png`. Depth bias instead of the lift alone (D21).

- [x] **T7.3** `OwnInput`: walk, hold to walk, shift + click turn, Insert sit (plan §4.11). (FR-9, FR-13, FR-19)
  *Done when:* Input System tests drive each action and check the call to `OwnCharacter`; holding the button re-asks at 500 ms and on cell change; on the local server a 10-cell click walks there and `@where` equals the debug view's cell at arrival.
  *Result (2026-11-22):* 9 Input System tests pass (walk, hold at 500 ms and on cell change, shift + click, Insert). On the local server: clicks on 156,181, 146,181 and 156,188 arrive on the server's cell, hold to walk, shift + click on the 8 neighbours, Insert sits and stands and a walk while sitting is refused (`evidence/t7.3-sitting.png`).

## Phase 8: Dev tools

- [x] **T8.1** `DevCommandLine` with `/doridori`, `/bangbang`, `/sit`, `/stand` (plan §4.12). (FR-13, FR-19)
  *Done when:* each command does what FR-13/FR-19 say on the local server and a second client sees it; game input is off while the field has focus.
  *Result (2026-11-22):* tests pass (commands, game input off while the field is open). On the local server each command acts on the own character and a second client sees it: `/bangbang` one step clockwise, `/doridori` (left, right, straight, 200 ms apart then; left and right, one for each command, since D32), sitting and standing (`evidence/t8.1-sitting-seen-by-second-client.png`), both ways between two players (the turns and `/doridori`) and from the own character (sitting).

- [x] **T8.2** `WorldDebugView` (plan §4.12). (FR-14)
  *Done when:* F9 shows the entity list, clock estimate and round trip, and draws the own path and server cell during a walk (screenshot); drawing allocates 0 B per frame.
  *Result (2026-11-22):* F9 shows the entity list, clock estimate and round trip, and draws the own path and the server cell during a walk (`evidence/t8.2-debug-view.png`). Drawing (`OnRenderObject`) 0 B per frame; only the text refresh, four times a second, allocates (11 of 179 frames; D26).

- [x] **T8.3** Release build check. (FR-13, FR-14)
  *Done when:* a non-development player build contains no `Uninarok.World.Dev` type and no dev command line (checked by reflection over the built assemblies, command and result in the report).
  *Result (2026-11-22):* `PlayerBuildInterface.CompilePlayerScripts` for StandaloneWindows64 without the development option writes 9 `Uninarok.*` assemblies and no `Uninarok.World.Dev.dll`; by reflection none of them has a type in a `.Dev` namespace (the same compile with the development option has `Uninarok.World.Dev.dll` with `DevCommandLine`, `WorldDebugView`, `WorldDevTools`). The dev tools are added at run time, so the scene holds no component that a release lacks. No full player was built (7 GB of Addressables content).

- [x] **T8.3** The client's cell cursor and mouse cursors (plan §4.11, D29). (FR-11)
  *Done when:* `TheCursor_MarksWalkableCells_AndTheMouseSaysTheOthers` and `TheMouseCursor_PlaysTheClientsCursors` pass; in game the grid shows on a walkable cell and not on another (screenshot).
  *Result (2026-11-22):* added after the owner compared T7.2 with the original client. Both tests pass; `evidence/t8.3-cell-cursor.png` (iz_int). The frames of the two mouse cursors were checked as pictures in the running game; a screenshot does not hold a hardware cursor, so the owner sees it in T9.1.
  *Changed (2026-11-22):* shape and speed corrected after the owner's check (D34); `TheMouseCursor_PlaysTheClientsCursors` checks size, mode and frame times. The owner sees both cursors again in T9.1: the software one was not seen running.

- [x] **T8.4** A dev account per play mode player (D30). (FR-8, FR-13)
  *Done when:* `Credentials_TheAccountOfAPlayersTag` passes and two players in one editor are in game on two accounts.
  *Result (2026-11-22):* added for T9.1, with the owner's Multiplayer Play Mode. The test passes; the main editor (account 2000000) and Player 2 (tag `login:testeAdmin2`, account 2000002) were in game together and see each other (`evidence/t6.3-second-player.png`).

## Phase 9: Verify and close

- [ ] **T9.1** Owner: second-client and side-by-side checks. (FR-1, FR-9, FR-13, FR-19)
  Entry on prontera; turns, commands and sitting seen by a second client; the turn controls against the original client.
  *Done when:* each check is recorded in `evidence/` with its result.
  *Open (2026-11-22):* done with two Multiplayer Play Mode players: entry, turns (head first, then the body), `/bangbang`, `/doridori`, sitting and walking seen by the other client. The owner compared (2026-11-22): the cursors (T8.3) and the shadow (PLAN-0009 D27; here 105,93,56 over 165,145,78, the original 108,98,58 over 178,157,83; `evidence/t9.1-shadow.png`) are right. Left for the owner, against the original client: what T0.4 leaves, the heads of hair styles 1 to 12 (D27), the dead monster time and the item sub-cell (without combat: the original client on a second account beside ours, where it kills the monster and drops the item, and both show it).

- [x] **T9.2** Cost at 300 walking units (plan §6). (FR-17)
  100 players (dev bots or replay) and 200 monsters walking on prt_fild08, development build, owner's PC.
  *Done when:* a profiler capture in `evidence/` shows the world update (`WorldModel.Advance` + `WorldPresenter.Sync` + `OwnCharacter.Tick`) at 1 ms or less and 0 B GC allocation per frame.
  *Measured (2026-11-22), not ticked:* this PC (i5-14600KF), in the editor with the profiler on (as TASKS-0009 T8.2), 100 players and 200 monsters walking on prt_fild08 through the simulated connection: `World.Update` 0.20 ms mean, 0.51 ms max over 300 frames, 0 B GC (`evidence/t9.2-world-update-profiler.csv`, `t9.2-300-walkers.png`). A development build needs the 7 GB of Addressables content built: owner to accept the editor numbers or ask for the build.
  *Result (2026-11-22):* the owner accepted the editor numbers (2026-11-22) in place of a development build.

- [ ] **T9.3** Spec §5 run and close the spec. (all)
  Enter prontera, walk to the south warp, arrive on prt_fild08, walk among monsters for 5 minutes.
  *Done when:* the own cell equals `@where` at every stop, every server stop or move finds a monster within one cell of where the client shows it (debug log), no unit is duplicated or left behind; spec, plan and tasks are set to `Implemented`; leftovers are in `BACKLOG.md`.
  *Open (2026-11-22):* run done on the local server: prontera, walk to the south warp, prt_fild08, 5 minutes (305 s) of walking among wandering and chasing monsters. Own cell equal to the server's (the server's own position, written every second by `uninarok_pathdump`, in place of `@where`) at 87 of 87 stops; 680 server moves and stops of other units, none over 1.05 cells from where the client showed them (worst 1.02: shown 138,187.98, the server 139,189); no duplicate. At 86 of 87 stops the client's units equal the server's list to within one unit (84 exactly); the other three times one, one and two units of the server's list were not shown. Those three were the comparison, not the client: the server's list is up to a second old. Traced in a second run (323 s, every arrival and departure in the client with its server time against every list): at the time of each list, 304 of 319 are equal; the 15 others differ by 18 units at the edge of the view or just spawned or killed, each one settled in the client within 25 ms of the list (the packet's way plus a frame). Compared half a second late, as the first run could be, 103 of the 319 would differ. Left: closing the spec, which waits for T0.4 and T9.1 (owner).
