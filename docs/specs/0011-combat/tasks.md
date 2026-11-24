# TASKS-0011: Combat

| | |
|---|---|
| **Implements** | [plan.md](plan.md) (rev. 2026-11-23) → [spec.md](spec.md) (rev. 2026-11-23) |
| **Status** | Implementing (approved by the owner; owner, 2026-11-23: revision with phase 10 for FR-15 and the plan's revision; checked tasks unchanged; earlier version approved by the owner 2026-11-22) |

**How to use this file**
- Work on branch `feature/0011-combat`. Do tasks **in order**, except T0.3 and T0.4: they wait for the owner and do not block phases 1 to 8; phase 9 does not close without them.
- One task = one reviewable commit. Tick the box in the same commit. Commit message format: `feat(0011): T3.3 approach the target` (CLAUDE.md §1.4).
- Each task lists the requirements it serves (`FR-n`) and a **Done when** check. Do not tick without verifying it and reporting the evidence (command + result).
- Tests come before implementation. A "tests" task is committed with its tests marked `[Ignore("pending Tn")]`; the implementation task removes the attribute and the tests must pass. "Tests, then implementation" in one task means one commit holding both.
- Through the Unity MCP, run tests in groups (BACKLOG: one full run exceeds the tool timeout). Tests marked `NeedsData` are ignored with a message when their data is absent.
- "No new warnings" is checked in `Logs/Editor.log` after a refresh, not in the console.
- Paths are relative to `Uninarok-client/Assets/` unless written in full. Class names and contracts are in `plan.md` §4; do not redesign them here. If a check shows a plan rule is wrong, stop and update `plan.md` first with a note under Decisions.
- Values marked *(lead)* in the plan are implemented as settings or table rows with that value and corrected in T9.3.
- Reference documents are updated in the same commit as the code they describe: packet docs and `INDEX.md`, `combat-reference.md`, the fixtures `README.md`.
- Evidence goes to `docs/specs/0011-combat/evidence/` and is linked from the task's report. Scratch code is deleted when its task is done.
- The uncommitted imports of int_land and iz_int in the working tree stay out of every commit (plan §7).
- Unrelated problems go to `docs/specs/BACKLOG.md` as one line each.

---

## Phase 0: Prepare

- [x] **T0.1** Verify every packet of plan §4.1 against Hercules for PACKETVER 20220406. (FR-1, FR-2, FR-3, FR-5, FR-8, FR-9, FR-10, FR-11)
  ID, size and layout in `packets_struct.h`/`clif.cpp`; the samples in SPEC-0010's captures; the result codes of `ITEM_PICKUP_ACK`; that `0x00B1` is not sent.
  *Done when:* a results table is in plan §4.1 (a Decisions note for any difference).

- [x] **T0.2** Extend the plugin `uninarok_pathdump`. (FR-1, FR-3, FR-13)
  HP and max HP per monster in the view lines, one `S` line per second with the player's HP, SP, levels and EXP; a line-of-sight flag (`path->search_long`, `CELL_CHKWALL`) per pair in the path fixtures, which are made again with the same seeds.
  *Done when:* the plugin builds and loads; the three path files differ from the committed ones only in the new flag and SPEC-0010's pathfinder tests still pass on them; a sample of the new lines is in the report; `docs/specs/0010-world-session/fixtures/README.md` describes both formats.

- [x] **T0.3** Owner: record the reference footage; then measure it. (FR-2, FR-4 to FR-7, FR-9, FR-10, FR-14)
  The 17 shots of plan §6.3. The owner's footage of 2026-11-22 (`C:\Users\catdo\Videos\Captures`, four files, listed in plan §6.3) covers shots 1, 3, 4, 11, 13, 14, 16 and 17 in part; dual wield (shots 5, 6) comes from three YouTube Shorts the owner named, as files the owner provides. FR-16's mark (shot 17) is measured here too.
  *Done when:* every row of `combat-reference.md` (T4.1) has a measured value or "as lead", with the shot and frame numbers, and the frames used are in `evidence/`.

- [ ] **T0.4** Owner: the restart prompt of the original client. (FR-8)
  The labels, their order, a screenshot, Esc and close; the `esc_*.bmp` images if they can be extracted.
  *Done when:* labels, order and layout are in `combat-reference.md`, and whether the images exist (D8).

## Phase 1: Packets

- [x] **T1.1** Write the new packet docs and update the existing ones. (FR-1, FR-2, FR-3, FR-5, FR-8, FR-10, FR-11)
  New: `ATTACK_TOO_FAR`, `ATTACK_RANGE`, `ACTION_FAILURE`, `LONG_PARAMETER_CHANGE`, `EXP_GAIN`, `RESURRECTION`, `STOP_ATTACK`. Existing: `DAMAGE` (types, times), `UNIT_ACT` (action 1), `PARAMETER_CHANGE` (vars), `ITEM_PICKUP_ACK` (results), `UNIT_HP` (never sent), `UNIT_MOVE_ENTRY` (HP re-sent, not at full), `REQUEST_ACT`, `PICKUP_ITEM`, `RESTART`.
  *Done when:* every doc follows Packet-Implementation.md with T0.1's layout; `INDEX.md` and `CATALOG.md` list them.

- [x] **T1.2** Tests, then structs: `DAMAGE`, `LONG_PARAMETER_CHANGE`, `EXP_GAIN`. (FR-1, FR-5, FR-11, FR-12)
  *Done when:* size and decode tests pass (real samples from the captures), each leaves no unread byte, and 1,000 decodes of `DAMAGE`, `PARAMETER_CHANGE` and `LONG_PARAMETER_CHANGE` allocate 0 B.

- [x] **T1.3** Tests, then structs: `ATTACK_TOO_FAR`, `ATTACK_RANGE`, `ACTION_FAILURE`, `RESURRECTION`; `UNIT_ACT.TargetId`. (FR-2, FR-3, FR-8, FR-9, FR-10)
  *Done when:* size and decode tests pass, the sit and stand tests of `UNIT_ACT` too.

- [x] **T1.4** Tests, then structs: `STOP_ATTACK`, `PICKUP_ITEM`, `RESTART`, the `REQUEST_ACT` constants. (FR-2, FR-3, FR-8, FR-10)
  *Done when:* golden-byte and size tests pass.

- [x] **T1.5** Record the fights of plan §6.2. (FR-1, FR-13)
  On the local server with the capture tee and T0.2's plugin; a scratch script sends the requests. The second client of (d) moved to T1.6 (2026-11-23: it needs the owner's second Multiplayer Play Mode player).
  *Done when:* the captures (a) to (d) and their server lines are under `fixtures/` with a `README.md`, and none contains the account name or password (searched).

- [ ] **T1.6** Owner and AI: record (e), the second client of plan §6.2 (d). (FR-2, FR-9, FR-10, FR-13)
  The owner turns on the second Multiplayer Play Mode player (`login:testeAdmin2`); a scratch script makes the second character fight, pick up, die and be raised in view of the first, and leaves loot reserved for one of them.
  *Done when:* `combat-second.bin` (the first client's connection) and its server lines are under `fixtures/`, listed in its `README.md`, searched for both accounts' names and passwords. Does not block T2.4.

## Phase 2: Combat state

- [x] **T2.1** Tests for `Blow`, the model's combat state and `OwnStatus` (plan §4.2). (FR-1, FR-5, FR-9)
  *Done when:* the `Blow_*`, `WorldModel_*` and `OwnStatus_*` tests of plan §6.1 exist, compile and are ignored as pending.

- [x] **T2.2** `Blow`, the `Entity` fields, `SetHp`, `Strike`, `ClearTargets`, `Feigning`, `RESURRECTION` in the model. (FR-1, FR-4, FR-9)
  *Done when:* the `Blow_*` and `WorldModel_*` tests of T2.1 pass, SPEC-0010's model tests too.

- [x] **T2.3** `OwnStatus` and the session's status, HP, damage and resurrection handlers (plan §4.2, §4.3). (FR-1, FR-5, FR-7, FR-9, FR-11)
  *Done when:* the `OwnStatus_*` tests pass; handling 1,000 damage and status packets after warm-up allocates 0 B.

- [x] **T2.4** `WorldSession_CombatReplay_*` on the captures of T1.5. (FR-1, FR-13)
  *Done when:* own HP, SP, levels and EXP and every damaged monster's HP equal the server's lines at the end of each capture.

## Phase 3: Own combat

- [x] **T3.1** Tests, then implementation: `IWalkGrid.BlocksSight`, `HerculesPathfinder.LineOfSight`, `AttackRange` (plan §4.4). (FR-3, FR-10, FR-13)
  *Done when:* the `AttackRange_*` and `LineOfSight_MatchesServer_<map>` tests pass (30,000 pairs), SPEC-0010's pathfinder tests too.

- [x] **T3.2** Tests for `OwnCombat` (plan §4.5). (FR-2, FR-3, FR-10)
  *Done when:* every `OwnCombat_*` test of plan §6.1 exists, compiles and is ignored as pending.

- [x] **T3.3** `OwnCombat`: attack and approach. (FR-2, FR-3)
  *Done when:* the click, approach, target-moves and target-dies tests pass.

- [x] **T3.4** `OwnCombat`: too far, silence, no ammunition, ground click, cancel. (FR-2, FR-3, FR-8)
  *Done when:* the remaining attack tests pass, SPEC-0010's `OwnCharacter_*` tests too.

- [x] **T3.5** `OwnCombat`: pickup. (FR-10)
  *Done when:* the `OwnCombat_PickUp_*` tests pass.

## Phase 4: Timeline

- [x] **T4.1** `CombatSettings` with the first values, and `combat-reference.md`. (FR-4, FR-6, FR-7, FR-14)
  *Done when:* `Settings/World/CombatSettings.asset` exists; every value of plan §4.5 to §4.9 is a field of it or a table row, and a row of `combat-reference.md` with its source.

- [x] **T4.2** Tests for `CombatTimeline` (plan §4.6). (FR-4, FR-5, FR-9, FR-10)
  *Done when:* every `CombatTimeline_*` test of plan §6.1 exists, compiles and is ignored as pending.

- [x] **T4.3** `CombatTimeline`: hit moments and motions. (FR-4)
  *Done when:* the tests of plan §6.1's FR-4 row pass.

- [x] **T4.4** `CombatTimeline`: kinds of blow and numbers. (FR-5, FR-6)
  *Done when:* the tests of plan §6.1's FR-5 row pass.

- [x] **T4.5** `CombatTimeline`: death after the hit, pickup motion. (FR-9, FR-10)
  *Done when:* the `_DeathHeldUntilHit`, `_MonsterHurt250`, `_OwnDeath` and `_PickUpMotion` tests pass.

- [x] **T4.6** Weapon types in the composition tables (plan §4.7). (FR-4, FR-14)
  *Done when:* builder tests pass (Main Gauche → short sword, a bow, an unknown id → 0); the asset is rebuilt and only it changed.

- [x] **T4.7** Tests, then implementation: `AttackMotions` (plan §4.7, D7). (FR-4)
  *Done when:* `AttackMotions_Rows` passes; the rows are in `combat-reference.md`.

- [x] **T4.8** Tests, then implementation: `ProjectileRules` and the timeline's projectiles. (FR-13, FR-14)
  *Done when:* the `ProjectileRules_*` tests pass, with a blow of each of the cases (bow, archer monster, other, none) through the timeline.

- [x] **T4.9** `Combat_ZeroAllocation_100Fighting`. (FR-12)
  *Done when:* it passes: 0 B over 600 frames after warm-up.

## Phase 5: Units and presenter

- [x] **T5.1** Tests, then implementation: `UnitClock.FitMs`, `UnitView.PlayFor`. (FR-4)
  *Done when:* `UnitClock_FitMs_*` and `UnitView_PlayFor_*` pass, SPEC-0009's clock and view tests too.

- [x] **T5.2** Tests, then implementation: `PickRect` of units and ground items. (FR-2, FR-10)
  *Done when:* the rect of a known frame equals its layers' rectangle (test), it is empty when nothing is shown, and it is not computed on frames without `Compose`.

- [x] **T5.3** Presenter: motions, base animation, death after the hit. (FR-4, FR-9, FR-10)
  *Done when:* `WorldPresenter_PlaysMotionThenBase` and `_DeathAfterHurt` pass, `WorldPresenter_ReadsOnly` and the zero-allocation test too.

- [x] **T5.4** `CatalogWeaponTypes` and the wiring in `WorldBootstrap` (order of plan §2, §5). (FR-4, FR-14)
  *Done when:* a view id, an item id and an unknown value give their types (test); on the local server attacks and hurts of monsters and players are seen (clip).

## Phase 6: Drawing

- [x] **T6.1** `Uninarok/Overlay` shader and `QuadMesh`. (FR-6, FR-7, FR-12)
  *Done when:* a test mesh of 512 quads is written each frame at 0 B and shows its texture and colours (screenshot).

- [x] **T6.2** Tests, then implementation: `NumberMotion` and the numbers of `CombatOverlay`. (FR-6)
  *Done when:* `NumberMotion_Table` passes; each kind is seen on the local server or from test packets (screenshots).

- [x] **T6.3** HP and SP bars of `CombatOverlay`. (FR-7)
  *Done when:* `_BarsFollowAtFourTurns` and `_BarHiddenAtFullAndWhenDead` pass; a hit poring shows its bar, an MVP none, the own bars shrink and refill (screenshots).

- [x] **T6.4** Convert `sprite/npc/skel_archer_arrow`; `ProjectileView`. (FR-14)
  *Done when:* an archer's and an archer skeleton's arrow fly to their targets on the local server, turned along their flight at four camera turns (screenshots).

- [x] **T6.5** `CombatOverlay_ZeroAllocation_100Numbers300Units`. (FR-12)
  *Done when:* it passes.

## Phase 7: Input and restart

- [x] **T7.1** Tests, then implementation: `UnitPicker`. (FR-2, FR-10)
  *Done when:* `UnitPicker_NearestUnit_PlayerHidesMonster` passes, with the own character, dead units and items; 300 units allocate nothing.

- [x] **T7.2** `OwnInput` and the attack cursor (plan §4.10). (FR-2, FR-8, FR-10)
  *Done when:* input tests pass for click, ctrl, hold, shift, player, NPC, item, ground while attacking, and dead; SPEC-0010's input tests too; the attack cursor shows over a monster (frames checked as pictures).

- [x] **T7.3** `/noctrl`. (FR-2)
  *Done when:* a test shows that it toggles a plain click to continuous and back, and says so.

- [x] **T7.4** Tests, then implementation: `RestartPrompt`. (FR-8)
  *Done when:* the `RestartPrompt_*` tests pass.

- [x] **T7.5** `RestartPromptView` in UI Toolkit. (FR-8)
  First check UI Toolkit input with a button in an empty panel in Bootstrap (plan §9).
  *Done when:* `RestartPromptView_TwoButtons` passes; mouse and Esc work in game; labels from T0.4, or marked as provisional until it is done.

- [x] **T7.6** Own death end to end. (FR-8)
  *Done when:* on the local server: death to monsters, prompt, clicks and turns send nothing (trace), close and Esc, return to the save point alive with input, `@raise` stands the character up in place and closes the prompt (evidence).

## Phase 8: Dev tools

- [x] **T8.1** `CombatDebugView` (plan §4.12). (FR-11)
  *Done when:* `CombatDebugView_ListsBlowsAsReceived` passes; during a fight its numbers equal the packet trace (screenshot and trace).

- [x] **T8.2** Release check. (FR-8, FR-11)
  *Done when:* the non-development player scripts contain no `Uninarok.World.Dev` type and contain `RestartPromptView` (reflection, command and result in the report).

## Phase 9: Verify and close

- [x] **T9.1** Live checks with one client. (FR-2, FR-3, FR-10)
  *Done when:* recorded in `evidence/`: a poring at 8 cells attacked 10 times, at most one try with a too-far reply; a walking monster followed and killed; a bow stops at 9 cells; a town NPC and the own character never attacked; a potion at 6 cells picked up.

- [ ] **T9.2** Live checks with a second client. (FR-2, FR-4, FR-9, FR-10)
  *Done when:* recorded in `evidence/`: the other character's attacks, hurts, pickup motion, death and raise are seen; clicking it sends no attack; its loot stays and one info line names the code.

- [ ] **T9.3** Correct the leads with T0.3's and T0.4's results. (FR-2, FR-4 to FR-10, FR-14)
  *Done when:* no row of `combat-reference.md` is a bare lead; settings, tables and tests follow it; each rule changed has a Decisions note.

- [ ] **T9.4** Side by side with the original client. (FR-6, FR-7, FR-14)
  *Done when:* the same blows filmed in both clients are in `evidence/` (numbers, 20 numbers per second over one monster, bars, arrows at 2, 5 and 9 cells, an archer skeleton, one attack of each other weapon type), and the owner accepts them.

- [x] **T9.5** Cost (plan §6.1). (FR-12)
  300 units, 100 fighting, 100 numbers on screen.
  *Done when:* a profiler capture in `evidence/` shows 0 B GC per frame from the combat update; its frame time is recorded (guide 0.5 ms).

- [x] **T9.7** [core] Multi-hit colours (D28; spec FR-5, FR-6 revision 2026-11-23). Tests first, one commit: `CombatTimeline_MultiHit_TotalOnlyOverNonPlayer` (a multi-hit over a monster has the parts and a `Total`; over a player the parts and no `Total`) and `CombatOverlay` colour choice (parts white over a monster, red over a player; `Total` in `CombatSettings.NumberTotal`, (0.9, 0.9, 0.15)). `CombatSettings.NumberTotal` and its asset value; `combat-reference.md` row already written. Done after phase 10; it does not wait for T9.6's run. (FR-5, FR-6)
  *Done when:* the new tests pass, the existing `CombatTimeline_*` and overlay tests pass (a test that expected a total over a player is changed with a note); a screenshot of a multi-hit over a monster (white parts, yellow-green total) and over the own character (red parts, no total) is in `evidence/`.

- [ ] **T9.6** Spec §5 run and close the spec. (all)
  Bootstrap to prt_fild08, 20 monsters with continuous attack, their loot, death, save point.
  *Done when:* no more than one too-far reply per target; own HP, SP and EXP equal `@stats` and `@exp`; every monster's last bar equals its HP; no unit is left behind; spec, plan and tasks are set to `Implemented`; leftovers are in `BACKLOG.md`.

## Phase 10: Revision 2026-11-23 (FR-15): job families and both hands

Rework and new work for plan §4.6, §4.7, §6.1, §7 and §8 (D18, D20 to D27). Do **before T9.6**: the spec does not close without this phase (owner, 2026-11-23: run it now, before T9.1 to T9.5). Checked tasks keep their commits; each rework is a new task that names the task it redoes. Test-first: a tests task is committed with `[Ignore("pending Tn.m")]`, and the implementation task removes it. T10.3 and T10.4 are one commit each, because a changed signature must compile with its callers and its tests.

- [x] **T10.1** [core] Tests for `JobFamilies` in `EditMode/Units`: `JobFamilies_RowJobOf_Table`, `JobFamilies_RowSexOf_SoulReaperMale`, and `JobFamilies_IsAssassinFamily_EveryIdAndNotRogue` extended with the assassin ids above 4242 (plan §6.1, §4.7 family table). Committed with `[Ignore("pending T10.2")]`. (FR-15)
  *Done when:* the three tests exist and compile and are ignored; the rest of the `Units` suite passes unchanged.

- [x] **T10.2** [critical] `JobFamilies`: the sorted `(job, rowJob, flag)` table as a `ReadOnlySpan<byte>` (no static field), `RowJobOf`, `RowSexOf` (flag bit 0, D24), `IsAssassinFamily` = `RowJobOf(job) == 12`. Ids above 4242 copied from roBrowserLegacy (`DB/Jobs/JobConst.js`, `WeaponAction.js`), each with its constant name in a comment; the mounted Crusader choices of D25 recorded per id. Removes the ignore of T10.1. Reference: `combat-reference.md`, "Attack animation": the family table (plan §4.7), the ids above 4242 marked "copied from roBrowserLegacy, not run" (D27), the Soul Reaper male row (D24), the Taekwon `Attack2` rule (D22). (FR-15, FR-4)
  *Done when:* the three `JobFamilies_*` tests pass; the `WeaponHands` and `IsAssassinFamily` tests of SPEC-0009 pass unchanged; `combat-reference.md` has the family table; Logs/Editor.log has no new warning (UAL0010 checked).

- [x] **T10.3** [critical] Rework of **T4.7** (`AttackMotions`): `NumberOf(rowJob, sex, weaponType)` without `secondHand` (D21); rows 25 to 30 found by the combined type; job 4046 with any weapon → `Attack2` (D22); no row for Gangsi, Death Knight, Dark Collector, Wedding, Xmas, Summer (D26); Druid rows from roBrowserLegacy; `HitFrameOf` takes the same arguments. `AttackMotionsTests` rewritten to plan §6.1's FR-15 list: `AttackMotions_Rows` (changed), `_JobFamilies_EveryWeaponType`, `_TaekwonFamily_Attack2EveryWeapon`, `_SoulReaper_MaleWizardRowEitherSex`, `_NoRowJobs_Attack1`, `_Hands_EachCase`, `_ShieldNeverChangesMotion`. The one call in `CombatTimeline` drops `secondHand` so the project compiles. Reference: the attack-number rows in `combat-reference.md`. One commit (signature and its tests). (FR-4, FR-15)
  *Done when:* the `AttackMotions_*` tests pass (`_Hands_EachCase` through `WeaponHands.Combine` with synthetic hands); `_ShieldNeverChangesMotion` passes for every row job × weapon type 0 to 30; the `CombatTimeline` tests of T4.3 and T4.8 still pass; the rows in `combat-reference.md` equal the test's table.

- [x] **T10.4** [critical] Rework of **T5.4** (interfaces and `CatalogWeaponTypes`): `IWeaponTypes.TypeOf(job, right, left)` (plan §4.7); `IJobBases` replaced by `IJobRows` (`RowJobOf`, `RowSexOf`). `CatalogWeaponTypes` implements both: `TypeOf(job, right, left)` = `HandsOf(job, right, left).WeaponType` with unknown values traced once; `RowJobOf` and `RowSexOf` from `JobFamilies`; `BaseJobOf` and its two asserts in `CatalogWeaponTypesTests` removed. The test fakes migrated (`ProjectileRulesTests.SameTypes`, `WorldPresenterPlayTests.BowWeapons`, and any other fake of either interface). `CombatTimeline.Weapons` and `Jobs` typed to the new interfaces, and `Add` asks `TypeOf(job, right, left)`; the rest of the timeline follows in T10.6. Tests in `CatalogWeaponTypesTests`: `CatalogWeaponTypes_TypeOf_BothHands_IsHandsOf`, `_LeftShieldViewId_IsShield`, `_RowJobOf_IsJobFamilies` (`NeedsData`). `combat-reference.md`: "the caller maps them" sentence replaced by a pointer to the hands table (plan §4.7) and `composition-reference.md` "Hands". One commit (interface, implementation, fakes). (FR-15, FR-4)
  *Done when:* the project compiles; the `CatalogWeaponTypes_*` tests pass (or are reported as not run if their data is absent, which blocks the task); the `ProjectileRules` and `WorldPresenter` tests pass; `BaseJobOf` and `IJobBases` are gone from `Assets` (grep); `git status` shows no asset changed (no rebuild, plan §7).

- [x] **T10.5** [core] Tests, then implementation (part 1): `CombatTimeline_AttackNumber_BothHandsAndRowJob` with fake `IWeaponTypes` and `IJobRows`: the server's job, `Look.Weapon` and `Look.Shield` reach `TypeOf`; the row job and row sex reach `NumberOf`; the same type reaches `ProjectileRules`. Committed with `[Ignore("pending T10.6")]`. (FR-15, FR-4)
  *Done when:* the test exists, compiles and is ignored; the rest of the `Combat` suite passes unchanged.

- [x] **T10.6** [critical] Rework of **T4.3** and **T4.8** (`CombatTimeline` both hands): `Add` computes the both-hands type once per blow and passes it to `AttackNumber` (`NumberOf(Jobs?.RowJobOf(job) ?? job, Jobs?.RowSexOf(job, sex) ?? sex, type)`), `HitFrameOf` and `ProjectileRules.Of`; `AddProjectile` takes the same type, not the right hand alone (D23). Removes the ignore of T10.5. (FR-4, FR-14, FR-15)
  *Done when:* `CombatTimeline_AttackNumber_BothHandsAndRowJob` passes; the `ProjectileRules_*` and `CombatTimeline_*` tests of T4.3 and T4.8 pass; `Combat_ZeroAllocation_100Fighting` (T4.9) still passes at 0 B over 600 frames.

- [x] **T10.7** [core] Dual-wield replay: `WorldSession_CombatReplay_DualWield` replays `fixtures/combat-dual.bin` into a session with the timeline and the real `CatalogWeaponTypes` (`NeedsData`). The assassin's own attack motions are `Attack2` while only the left hand holds a knife, `Attack3` once both hands hold knives, and those blows carry `LeftDamage`. (FR-5, FR-15)
  *Done when:* the test passes on the committed fixture, and its motion sequence is written in the test with the blow numbers it is read from (the capture's server lines are listed in `fixtures/README.md`).

- [x] **T10.8** [quick] Rework of **T5.4** (wiring): `WorldBootstrap.UseAssets` sets `timeline.Weapons` and `timeline.Jobs` from one `CatalogWeaponTypes` instance (plan §7). Only the assignments change; no other line of the bootstrap is touched. (FR-15)
  *Done when:* `WorldBootstrap.cs` assigns both members from the same instance (grep shows both lines); the project compiles; Logs/Editor.log has no new warning after a refresh.

## Phase 11: Revision 2026-11-23 (FR-16): the target mark

New work for plan §4.5, §4.8, §6.1 and D29. Independent of phase 10 and of T9.1 to T9.6; do it **before T9.6**, because the spec does not close without it. Test-first as in phase 10.

- [x] **T11.1** [core] Tests, then implementation: `OwnCombat.MarkedTarget(now)` (plan §4.5). `OwnCombat_Mark_ContinuousStaysSingleExpires`, `_ClearedByGroundClickSitDeathTargetGone`, `_OnlyAttackableMonster`; `CombatSettings.TargetMarkSingleMs` (first value 0 after the single blow's hit moment, from `evidence/T0.3/README.md`; a row of `combat-reference.md`). One commit: tests, state and setting. (FR-16)
  *Done when:* the three tests pass, the `OwnCombat_*` tests of T3.3 to T3.5 pass unchanged, and `combat-reference.md` has the row.

- [x] **T11.2** [core] The mark in `CombatOverlay` (plan §4.8, D29). First find the picture: `evidence/T0.3/README.md` describes it (a white faceted diamond pointing down, about 22 × 15 px, about 66 px above the standing sprite top, no float of its own) with the frames; search the extracted client data (sprites of effects, cursors, the UI) for that picture; record the values in `combat-reference.md` with their source. Then draw it with `CombatSettings` fields (`TargetMark` sprite, `TargetMarkHeight`, `TargetMarkBobPixels`, `TargetMarkBobMs`; `TargetMarkSingleMs` corrected from the footage); tests `CombatOverlay_TargetMark_AboveHeadAtFourTurns`, `_OnlyOneMark`, `_HiddenWhenDead` and a mark inside `CombatOverlay_ZeroAllocation_100Numbers300Units`. If the sprite cannot be identified from the footage and the data, stop and ask the owner. (FR-16, FR-12)
  *Done when:* the PlayMode tests pass, the zero-allocation test still passes, and screenshots of a continuous attack (mark stays until the kill) and of a single attack (mark goes away after its time) are in `evidence/`, beside the original client's frames.
