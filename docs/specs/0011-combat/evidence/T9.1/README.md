# T9.1 evidence: live checks with one client (FR-2, FR-3, FR-10)

2026-11-23, local Hercules (`prt_fild08`, `prontera` for item 4 and 5), Play Mode from the Bootstrap scene with the dev auto login, one client.
The character: assassin (job 12), base level 13, a knife in each hand, range 1 (a bow user for item 3).

## How it was run
A scratch script (not kept) through the Unity MCP:
- put a logging wrapper in place of the `INetworkClient` of `OwnCombat` and `OwnCharacter`, so every packet they send is written with the server time and its fields, and (REQUEST_ACT, PICKUP_ITEM) the own cell, the target cell and the distance at that moment (`clientDistance` is `AttackRange.ClientDistance`, `chebyshev` the plain cell distance);
- subscribed ATTACK_TOO_FAR, ITEM_PICKUP_ACK, DAMAGE and ACTION_FAILURE on the real `NetworkClient` and wrote their fields;
- spawned porings with `@monster 1002 n` through the dev command line, walked the character to the wanted distance with `OwnCharacter.RequestWalk`, and called `OwnCombat.Attack(id, true)` / `PickUp(id)`, the calls the click path makes.
The time of each line is the client's server clock estimate in ms. `REQUEST_MOVE` lines are the walks of `OwnCharacter` (the placement walks of the script and the approach walks of `OwnCombat`); `GLOBAL_CHAT` is the `@` command.
No code of the product was changed.

## 1. A poring at 8 cells, attacked 10 times (`item1_poring_8_cells.log`)
Each try: a new poring (class 1002, checked), the character on a cell at client distance 8 (`clientDistance=8`, by the script, standing target), `Attack(id, continuous)` -> `Approaching` for all 10, then the log until the poring died.
- Tries: 10. Killed: 10 of 10 (1.4 to 3.1 s from the click to the death).
- Tries with a too-far reply: **1** (try 8); other tries: 0. Too-far replies in total: 1. The bar was at most one.
- The try with the reply (try 8, lines `ATTEMPT 8`): the poring was walking when the first request was sent (own cell 159,349 while walking, target 156,349 walking, client distance 2); the server answered `ATTACK_TOO_FAR` (target 155,348, own server cell 158,349, range 1); the client walked once more (`REQUEST_MOVE` 157,348) and the second `REQUEST_ACT` at distance 1 landed. No loop: one reply, one more request.
- A try where the placement at 8 cells failed (monster 110033550, line `placement at 8 failed`) was skipped and not counted.
- The first 8-cell run of the same script (not kept in the repo) took 10 tries with the first monster in view and showed no too-far reply either; its monsters were not checked for class, so this log is the second, class-checked run.

## 2. A walking monster, followed and killed (`item2_walking_monster.log`)
3 runs, each against a poring that was walking when `Attack(id, true)` was called (`walking=True` in the start line), 3 to 8 cells away.
- Run 1: start at distance 3, walking target; `REQUEST_MOVE` towards it, `REQUEST_ACT` (type 7) at distance 2, killed after 1.2 s.
- Run 2: start at distance 8, walking target (21 of 25 samples at 100 ms had it walking): the first approach `REQUEST_MOVE` 149,343, a second one 150,344 (the target moved: the approach repeat), `REQUEST_ACT` at distance 2 (target walking, range 1 + 1), two blows, killed after 2.4 s.
- Run 3: start at distance 3; `REQUEST_MOVE`, `REQUEST_ACT` at distance 2, killed after 2.0 s.
- End state: all 3 killed, `OwnCombat.State` Idle, no too-far reply, no `STOP_ATTACK`.

## 3. A bow stops at the server's range (`item3a_bow_range5.log`, `item3b_bow_range9.log`)
Done as an archer: `@job 11`, the bow (1701) and arrows (1750) equipped with a scratch equip packet (0x0998), then the server's range read from `OwnStatus.AttackRange` (ATTACK_RANGE).
- `item3a`: the bow alone, server range **5**. Three porings placed 11, 11 and 14 cells away: the first `REQUEST_ACT` was sent at client distance **5** every time (own cell stands 6 cells from the target on the x axis, `chebyshev=6`), 0 too-far replies, killed (DAMAGE with arrow damage about 130).
- `item3b`: the spec's number 9. `@skpoint 20`, then Owl's Eye 3 and Vulture's Eye 4 by skill-up packets: the server sent range **9** (5 + 4). Three porings placed 12, 12 and 13 cells away: the first `REQUEST_ACT` was sent at client distance **9** (runs 1 and 3, standing target, `chebyshev=10`) and **10** (run 2, the poring was walking, range + 1); 0 too-far replies, no request loop, killed each time. The client distance is the rule of the server (`distance_client`, circular minus 0.0625), so 10 plain cells on one axis is distance 9.
- The Hercules database has no `@skill` command; the range 9 was reached with skill points.

## 4. A town NPC and the own character are never attacked (`item4_npc_and_own_never_attacked.log`)
In prontera (`@warp prontera 156 191`) 10 NPCs were in view (classes 92, 99, 105, 700 and 722; no Kafra among them, none of the classes is a Kafra sprite that I could name, so "a town NPC" is what was tested). For each NPC, for the own character (id 2000000), for id 0 and for an unknown id: `Attack(id, false)` and `Attack(id, true)`.
- 13 targets x 2 calls: all `NotAttackable`, `OwnCombat.State` stayed `Idle`.
- Packets sent in the log during all of it: **0 REQUEST_ACT, 0 REQUEST_MOVE** (the line `RESULT item4`).
- Not driven: the real click path (`OwnInput.Step` with a mouse press). Read in code: `OwnInput` calls `Attack` only for `EntityKind.Monster`; a click on an NPC or on the own character is a walk click (`Ask`), not an attack.

## 5. A potion at 6 cells, picked up (`item5_potion_6_cells.log`)
In prontera: `@item 501 3`, `/drop 501 1` (the dev command), the item fell at the character's cell (object 5631 at 157,192), the character walked to a cell at plain distance 6 (own 157,186) and `OwnCombat.PickUp(item)` was called. 3 runs:
- Run 1 (6 cells straight): `PickUp()` true, state `Fetching`, a walk to 157,190 (2 cells from the item), then `PICKUP_ITEM` sent with the own cell 157,190 standing; the server answered result **6** (refused, as with a stale server cell), the client asked again 400 ms later (`PickupRetryMs`), answer result **0** (itemId 501, amount 1), the item left the model 0.8 s after the call.
- Run 2: the same, a first refusal (result 6) and the second request with result 0; gone after 0.8 s.
- Run 3: one `PICKUP_ITEM`, result 0, gone after 0.5 s.
- In all 3 the request was sent only after the walk ended within 2 cells; the item was removed and the potion count in the dev inventory stayed 4 (3 from `@item` plus the one in the inventory; each dropped one was picked up again).
- The result 6 on a request sent right after the walk ended (2 of 3 runs) is the lag the plan's retry (D13) is for; it needed one extra request each time, no second failure.

## Left behind
The character was put back as an assassin (`@job 12`, a knife in each hand, `prt_fild08` 102,298, base level 13). Server side the Owl's Eye and Vulture's Eye points of item 3 and the `@skpoint` change stay in the dev character's data, and 4 red potions and the bow with arrows are in its inventory. Play Mode was stopped; nothing was created in the scene or in the settings.
