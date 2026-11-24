T9.3 live check of the corrected numbers, 2026-11-23, local Hercules (prt_fild08), Play Mode from the Bootstrap scene, game view 1920 x 1080.
A scratch script (not kept) spawned a poring with "@monster 1002 1", then fed synthetic blows to the session's CombatTimeline (the same Add
that DAMAGE packets reach) with the server clock's time, and took each shot with ScreenCapture when the timeline's clock reached the time
named below. The blows are synthetic: the server did not send them. The timeline's numbers at each shot were dumped by the same script.

miss_over_attacker.png, _crop2x.png  a poring misses the own character (0 damage, attackMT 400), and 200 ms later the own character misses the
                                     poring; shot 1.3 s after the first blow. White "Miss" over the poring (the attacker), red "Miss" over the
                                     own character's chest (the attacker of the second blow), rising from 0.4 cell (D31, D34).
                                     Timeline: Miss over 110033274 (the poring), Miss over 2000000 (own).
doubleattack_1_part1_total549.png    own character > poring, MultiHit 2 hits, 1098 damage, LeftDamage 163, attackMT 400. Hit moment + 60 ms:
                                     part 549 (4x, white) and the yellow-green total 549 high above (at the top edge: TotalHeight 7 cells).
doubleattack_2_part2_total1098.png   hit + 170 ms: parts 549 and 549, the total replaced by 1098 (one total on screen).
doubleattack_3_lefthand_total1261.png hit + 450 ms: the second hand's 163, the total 1261 = 549 + 549 + 163 (D32).
doubleattack_4_total1261_after1s.png hit + 1.5 s: the total 1261 still shown (TotalMs 2,500), the parts fading.

The poring wanders near the top of the view, so the 7-cell total sits near the top edge in shot 1.
The arrow number at launch + 192 ms (D33) was not shot live (no bow character on this server account): it is pinned by
ProjectileRules_NumberWaits.
