T6.3 bars on the local server, 2026-11-23, prt_fild08, game view 1920 x 1080 (ScreenCapture). Lead sizes and colours (combat-reference.md).

1_pupa_hurt_and_own.png  a pupa (1031) attacked to 508/524 HP: its monster bar (60 x 5, #ff00e7) under it; the own HP and SP bar
                         (60 x 9, green over blue) at 200/200 and 62/62.
2_own_low.png            after "@heal -160 -40": own HP 40/200 (below 25 %: red) and SP 22/62; the server's own change shows as a red 160.
3_own_refilled.png       after "@heal": 200/200 and 62/62, the bars full again.
4_mvp_hit_no_bar.png     a Baphomet (1039, an MVP) next to the own character: no bar. Its HP in the model stayed unknown (-1): the server
                         sends none for a boss monster (H10, clif_show_monster_hp_bar). The own character missed it and was killed before
                         a blow landed (also with VIT +90 for the test, removed afterwards), so the MVP was not hit in this shot.
                         The own character lies dead there: no own bar while dead.
trace.txt                the values read from the status and the model at each shot.
