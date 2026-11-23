T8.1 live check of CombatDebugView, 2026-11-23, local Hercules (prt_fild08), Play Mode from the Bootstrap scene (dev auto login),
game view 1920 x 1080. The character (base 13, two knives) was already in game. A scratch script (not kept) did:
  - subscribed handlers for DAMAGE, PARAMETER_CHANGE, LONG_PARAMETER_CHANGE, EXP_GAIN and ATTACK_TOO_FAR on the NetworkClient and
    wrote each packet's own fields (not the HUD's) to packet_trace.txt;
  - set CombatDebugView.Shown (what F10 does), sent "@monster 1002 6" through the dev command line, and attacked the nearest living
    monster with OwnCombat.Attack(id, continuous) whenever the state was Idle, for 6 porings;
  - took the screenshot with ScreenCapture, and wrote the HUD text (hud_after_fight.txt) after the fight.

hud_during_fight.png   the HUD (top right) while attacking; 2 blows and the first exp notes so far.
packet_trace.txt       the packets as received during the fight (from the second blow on; the first blow, dst 110030692, came
                       before the trace was subscribed, and the HUD lists it as its oldest row).
hud_after_fight.txt    the HUD text at the end.

Comparison (HUD row = trace line; attacker > target, dmg + left, type, hits, attackMT, attackedMT)
  all 13 blows of the trace appear in the HUD in the same order (newest first), with equal attacker, target, damage, left damage,
  wire type (8 = MultiHit, 0 = Normal), hits (count), attackMT and attackedMT. Examples:
    trace  DAMAGE src=2000000 dst=110030759 dmg=68 left=15 count=2 type=8 attackMT=520 attackedMT=480
    HUD    2000000 > 110030759  dmg 68 + 15  MultiHit (type 8)  hits 2  attackMT 520  attackedMT 480
    trace  DAMAGE src=110030761 dst=2000000 dmg=0 count=1 type=0 attackMT=672 attackedMT=0
    HUD    110030761 > 2000000  dmg 0  Normal (type 0)  hits 1  attackMT 672  attackedMT 0   (a miss on the character)
  EXP: the trace has 7 EXP_GAIN pairs (base +36, job +20 each); the notes ring holds 8 rows, so the HUD ends with the 4 newest
  pairs (base +36, job +20), equal to the trace's last 4. The last LONG_PARAMETER_CHANGE values (base 932, job 613) equal the HUD's
  "EXP base 932/6100  job 613/1600".
  HP 200/200 and SP 62/62: no PARAMETER_CHANGE of HP or SP arrived (the porings missed), so the HUD's 200/200 is the unchanged value.
  The HUD's time column is the client's server-clock estimate when the packet was handled (Blow.At), not the packet's tick: it is
  within a few ms of the trace tick (e.g. 982320979 vs 982320984).
  Not seen in this run: too-far replies, pickups, a target HP (the HUD shows "hp ?" while the server sent none), HP loss.
