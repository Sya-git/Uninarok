T7.5 the restart prompt in game, 2026-11-23, local server, prt_fild08, game view 1920 x 1080 (ScreenCapture).

UI Toolkit input (plan §9): no EventSystem in Bootstrap. RestartPromptView_TwoButtons clicks both buttons and presses Esc with the
Input System's simulated devices; in game, state events were queued on the real Mouse and Keyboard devices:

  "@die" -> own Dead, prompt shown
  click on Cancel at (960, 527)  -> shown False, prompt False
  Esc                            -> shown True,  prompt True

1_dead_prompt.png   after "@die": the prompt over the dead character.
2_closed.png        after the click on Cancel: no prompt, the character lies dead.
3_esc_again.png     after Esc: the prompt again.

Labels ("Return to Save Point", "Cancel"), order, size and look are provisional until T0.4 (combat-reference.md).
