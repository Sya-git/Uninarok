# Fixtures of SPEC-0011

## Recorded fights: `combat-*.bin`, `combat-*.server.txt` (T1.5, FR-1, FR-13)

What the map server sent to the dev character `Boneco3` (account 2000000, a test account of the local server, group 99 for the session through the plugin), recorded with the capture tee of SPEC-0001 (`UNINAROK_NET_TRACE`) on 2026-11-22 and 2026-11-23: the received bytes of one map connection each, from its first byte. A scratch script in the editor sent the requests (attack, walk, pickup, equip, restart, GM commands); it was not kept.

Server: `Hercules-2026.09/` of this repository, `PACKETVER` 20220406, renewal, run with `bin/map-server.exe --load-plugin uninarok_pathdump --uninarok-walklog <file> --uninarok-gm 2000000` (plugin as of SPEC-0011 T0.2).

| File | Session |
|---|---|
| `combat-fight` | (a) prt_fild08, thief (base 1, job 1 at the start) with a knife and AGI, LUK and DEX raised: 20 porings killed with continuous attack (criticals, double attacks: wire type 8), base level 2 and job levels 2 to 4 reached by EXP; then AGI and DEX lowered, 5 more porings, the knife taken off, 5 more, LUK lowered, hornets: own misses (15), hornet hits and misses on the character, HP from 53 down to 20, then 90 s of rest with HP regenerating to 35; `@killmonster` at the end. 30 monster deaths in all |
| `combat-death` | (b) prt_fild08: `@kill` (own death), `@raise` (revived in place), 4 wolves spawned that never reached the character, `@kill` again, then `RESTART` 0: a map change to the save point (int_land 77,101) with 2 HP. A first `RESTART` was sent while standing and ignored by the server |
| `combat-bow` | (c) prt_fild08, archer: a bow that failed to equip at base level 3 (range stays 1, two too-far answers at range 1, an `ACTION_FAILURE` type 3 for the arrows), then `@blvl 10` (to base 13), bow and arrows equipped (`ATTACK_RANGE` 5), a too-far answer at 8 cells, porings killed at 1, 3 and 5 cells, the arrows taken off and an attack answered with `ACTION_FAILURE` type 0 |
| `combat-dual` | (d) prt_fild08, assassin: first a right-hand equip that failed (left knife only, 6 kills without second-hand damage; the porings looted their drops), then two new knives in both hands: lunatics killed with second-hand damage (`leftDamage`, also in multi-hits), one pickup from 6 cells refused (result 6), three own drops picked up (result 0), one more refused (result 6) |

The second client of plan §6.2 (d), another character seen fighting and picking up, and loot reserved for another player, are recorded separately in T1.6.

`<name>.server.txt` is the plugin's walk log for the time of that session, cut to whole blocks: once a second `S tick account hp max_hp sp max_sp base_level job_level base_exp job_exp next_base_exp next_job_exp` (the character's own status as the server holds it), then `A tick account map x y`, the `U id type class x y walking state [hp max_hp]` lines of what is in view (hp and max_hp for monsters), `E count`; and `W ...` lines of every walk that started (format in `docs/specs/0010-world-session/fixtures/README.md`). The last `S` line is the server's truth at the end of the session; for a monster, the `hp` of its last `U` line is its HP.

No file holds the account name or the password (searched for both on 2026-11-23, in the editor, without printing either).
