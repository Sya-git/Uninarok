T9.7 live check of the multi-hit colours, 2026-11-23, local Hercules (prt_fild08), Play Mode from the Bootstrap scene, game view 1920 x 1080.
A scratch script (not kept) spawned a poring with "@monster 1002 1" and then fed two synthetic blows (wire type 8 = MultiHit, damage 300,
hits 2) to the session's CombatTimeline (the same Add that DAMAGE packets reach); each shot was taken 1.1 s later with ScreenCapture.
The blows are synthetic: the server did not send them (no real multi-hit was produced).

multihit_over_monster.png       own character > poring: parts 150 and 150 white, the total 300 above them yellow-green.
multihit_over_own_character.png poring > own character: parts 150 and 150 red, no total.
