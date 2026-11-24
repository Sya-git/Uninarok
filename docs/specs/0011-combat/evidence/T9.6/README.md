# T9.6 evidence: the spec §5 run

2026-11-23, local Hercules (login, char, map), Play Mode from `Assets/Scenes/Bootstrap.unity` with the dev auto login (account `testeAdmin`, slot 1: DevHero, char 150005, Novice, base 99, job 3, 1012 HP, bare-handed, attack range 1), one client.

## How it was run
`driver.cs` (in this folder; run through the Unity MCP `script-execute`, hooked on `EditorApplication.update`, not part of the product) drove the client through the calls the mouse makes:
- a monster: `OwnCombat.Attack(id, continuous: true)` (what `OwnInput` calls on a ctrl-click or a held button); the nearest living monster each time;
- an item: `OwnCombat.PickUp(id)` (what `OwnInput` calls on an item click), for each item that fell within 4 cells of the dead monster's cell;
- the death prompt: a `NavigationSubmitEvent` sent to the `save-point` button of `RestartPromptView` (its `clicked` handler, `RestartPrompt.ReturnToSavePoint`);
- server commands: `OwnCharacter.Command` (the dev command line's path, SPEC-0010 FR-13/FR-20).
It wrote `run.log` (every own blow, too-far reply, pickup answer, EXP gain, server message, phase, and the unit counts) and `targets.tsv` (one row per target). `end-check.log` and `end-check-2.log` are the two status checks after the run (below). No product code was changed.

Test data: DevHero's save point was set in SQL to `prontera` 156,191 before the run (it was `iz_int` 18,27, inside the `startpoint` OnTouch area); the editor's dev login user and the scene's slot were set for the run and put back afterwards (`testeAdmin2`, slot 0; the scene was not saved).

## Steps
1. In game on prt_fild08 at 112,179 (`IN GAME` line); start status HP 1012/1012, SP 110/110, base 99 exp 30, job 3 exp 2.
2. 20 monsters killed with continuous attack (natural ones, and porings spawned with `@monster 1002 3` when none was within 14 cells): 20 of 20 killed, none abandoned, none re-clicked (`reclicks` 0: the continuous attack ran to the kill each time).
3. 26 items fell next to them; 26 picked up, 0 failures (`PICKUP_ACK` result 0 each time; the items leave the model).
4. `@monster 1039 1` (Baphomet): the character died 1.1 s later (`OWN DEAD`); the restart prompt was shown (`view.Shown` true, prompt open) 2 s later; the save-point button event returned the character to prontera 156,191, standing, prompt closed (`RESPAWNED`).
5. `@stats`, `@exp`, `@killmonster2 prt_fild08` (clean-up of the Baphomet and the spawned porings), 6 unit counts 1 s apart.

## Criteria

### No attack refused for distance more than once per target: pass
| Too-far replies | Targets |
|---|---|
| 0 | 17 |
| 1 | 3 (#6 lunatic, #10 and #16 porings; each answered by one more walk and request, then the kill) |
| 2 or more | 0 |

### Every monster's last bar equals its HP: pass
The bar is drawn from the model's `Hp`/`MaxHp` (`CombatOverlay.DrawBars`, alive and `0 < Hp < MaxHp`); `lastBar` is that value on the last frame the bar was drawn. Expected: the server's max HP (`db/re/mob_db.conf`) minus the damage of every own blow but the killing one (nobody else hit them).

| Class | Max HP (mob_db / client) | Targets | Damage per blow | Last bar | Expected |
|---|---|---|---|---|---|
| 1002 Poring | 60 / 60 | 13 | 50 | 10/60 | 60 − 50 = 10 |
| 1007 Fabre | 72 / 72 | 4 | 44 | 28/72 | 72 − 44 = 28 |
| 1063 Lunatic | 55 / 55 | 1 | 46 | 9/55 | 55 − 46 = 9 |
| 2398 Little Poring | 40 / 40 | 2 | 50 (one blow) | no bar (full HP until death) | no bar |

All 20 rows of `targets.tsv` agree; every HP the client saw ended at 0.

### Own HP, SP and EXP equal the server's: pass, with a note
| When | Client (`OwnStatus`) | Server |
|---|---|---|
| after the 20 kills, before death | HP 1012/1012, SP 110/110, job 7 exp 163/205, base 99 exp 786 | `@stats` HP 1012, MaxHp 1012, SP 110, MaxSp 110, job level 7 |
| 5 s after the respawn | HP 506/1012, SP 110/110 | `@stats` Hp 1012 (see the note) |
| after `@heal -337 -23`, then `@save` | HP 675/1012, SP 87/110, job 7 exp 163/205, base 99 exp 786 | `@stats` Hp 675, MaxHp 1012, Sp 87, MaxSp 110; `@exp` "Job Level: 7 (79.512%)" = 163/205; SQL `char` after `@save`: hp 675, sp 87, base_exp 786, job_exp 163 |
| after logout | (last client value before stopping) | SQL `char`: base 99, job 7, base_exp 786, job_exp 163, online 0 |

EXP: 30 + 756 base, 2 → 163 job (3 → 7) from the `EXP_GAIN` lines; the client's values equal SQL exactly.

Note on the respawn row: `@stats` prints `sd->status.hp`, which Hercules copies from the live HP only when it saves the character (`pc_makesavestatus`, `pc.cpp` 705/718). A novice respawns with half HP (`status_get_restart_hp`, `status.cpp` 1406: max 1012 / 2 = 506, also the HP the server shows while dead, `display_fake_hp_when_dead`), and the client then got the server's regeneration updates 530, 554. So `@stats` was stale there; with a save first (`@save`, which calls `chrif->save`) `@stats` and SQL equal the client, as the third row shows.

### No unit left behind: pass
28 checks (start, after each kill and its loot, before death, 6 after the respawn): model entities = units shown (`WorldPresenter.ShownCount`) + items shown (`ItemCount`) = active `UnitView` + active `GroundItemView` objects, every time (`COUNT ... OK`, 0 mismatches). After the respawn in prontera: 11 units, 0 items, stable over 6 s.

## Finding (not a §5 criterion; in `BACKLOG.md`)
Two targets (#2 and #14, porings spawned by `@monster` and attacked where they stood) had the cell **0,0** in the model when they died. Hercules re-sends `ZC_NOTIFY_MOVEENTRY` after each damage to update the HP (`mob.cpp` 2467 to 2471, SPEC-0011 H10) with `ud->to_x, ud->to_y` as the destination (`clif_set_unit_walking`, `clif.cpp` 1431), which is 0,0 for a monster that never walked. `WorldModel.Walk` finds no path there and places the unit on the destination ("the server got there, so the client must too"), so such a monster jumps to cell 0,0 after the first blow (out of sight) while the server still has it in place; the attack went on and killed it, and its bar and loot were not seen at its place. A monster that stopped short of an old destination would walk there instead. The fix changes the walking rule of SPEC-0010's model (a known unit's move entry), so it is not made here.

## Left behind
DevHero is in prontera 156,191 (save point prontera 156,191), HP 747/1012 at logout, base 99, job 7; the 26 picked items are in its inventory. prt_fild08's monsters were killed with `@killmonster2` (natural ones respawn). Play Mode was stopped; the editor's dev login user and the Bootstrap slot are back as they were.
