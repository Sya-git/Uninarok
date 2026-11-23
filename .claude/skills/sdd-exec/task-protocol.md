# Task protocol (all executor agents)

You receive one task. How much freedom you have comes from your agent definition; this protocol is common to all.

1. Implement **only this task**. Follow the plan's contracts. A plan deviation is allowed only after `plan.md` is updated first with a note under §8 Decisions (only `sdd-exec-critical` may do it; the others **stop and report**).
2. **Tests first** where the task says so: the failing test is committed or enabled before the implementation.
3. **Verify "Done when"**: run the tests or the manual check. Use the Unity MCP for Unity commands, and run tests in groups. "No new warnings" is checked in `Logs/Editor.log` after a refresh, not in the console. Never weaken the check, never tick on assumption.
4. **Long runs** (tests, imports, builds): start them in the background and wait for the completion notification. Do not poll in a loop.
5. Reference `FR-n` in test names or comments where meaningful, not as noise.
6. **Stay in scope.** Unrelated problems go to `docs/specs/BACKLOG.md` as one line each (file, symptom); never fix them inline. Do not touch the spec; if it seems wrong or incomplete, **stop and report**. Do not guess an `OQ-n`.
7. Update reference documents that are normative for the topic in the same commit as the code they describe.
8. Tick the task's checkbox in `tasks.md` in the same commit.
9. Commit as `type(NNNN): Tn.m short description` (type: `feat|fix|test|refactor|docs|chore`). Never commit credentials. Stage only the task's files.
10. **Report**: files changed, the exact command or check run and its result (the evidence), the commit hash, any BACKLOG lines added. If you stop: start with **STOPPED**, say what blocks the task, what you changed or reverted, and commit nothing.

## Definition of done
Code compiles with no new warnings, relevant tests pass, "Done when" verified, checkbox ticked, commit follows the format above, no unrelated changes.
