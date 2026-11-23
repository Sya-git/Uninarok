T6.4 arrows on the local server, 2026-11-23, prt_fild08, game view 1920 x 1080 (ScreenCapture), each taken in the middle third of a flight.

skeleton_turn0..3.png  a real archer skeleton (1016) 6 cells away shooting at the own character, at camera yaws 0, 90, 180, 270:
                       its arrow (sprite/npc/skel_archer_arrow, converted with RoImport.Sprite) flies from it to the character, head first.
player_turn0..3.png    the own character's arrow at the same four yaws. The character carries a knife on the server, so for this shot
                       its look was given a bow (item 1701) in this client's model only, and blows were added to the timeline as if
                       received (attackMT 864: launched at 6 x 24 x 864 / 432 = 288 ms); the arrow flies to the skeleton, head first.
trace.txt              the cells at each shot.
