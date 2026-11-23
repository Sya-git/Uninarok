T7.6 own death end to end, 2026-11-23, local server, game view 1920 x 1080. Input through state events queued on the real
Mouse and Keyboard devices; packets sent read from NetworkClient.TracePackets ([Net.Packet] ->).

1. Death to monsters: "@monster 1039 1" next to the character on prt_fild08 161,330; 4 blows on it, the last from a
   Baphomet Jr. (1101); dead at 1.1 s, prompt shown at 1.5 s.                                     1_killed_prompt.png
2. While dead: a ground click, a click and a ctrl click on the Baphomet (110024819), a shift click (turn), Insert (sit):
   packets sent 0; the character stays dead at 161,330.
3. Cancel: prompt hidden, character lying dead.                                                   2_closed_lying_dead.png
   Esc: prompt shown again.
4. Return to Save Point: packets RESTART (3 B), then MAP_LOADED and HEARTBEAT_REQUEST of the map change; the character
   stands on its save cell int_land 77,101 with 1/200 HP (the server's restart_hp_rate 0 = 1 HP), prompt closed.
   A ground click then walks it (REQUEST_MOVE, to 82,107).                                         3_save_point_alive.png
5. Earlier in the run, after "@die": "@raise" stood it up in place (161,330, 200/200 HP) and closed the prompt.
                                                                                                    6_raised_in_place.png
Afterwards: "@warp prt_fild08 161 330", "@heal". "@killmonster2" was sent; 5 Baphomet units were still in view right after
(not re-checked).
