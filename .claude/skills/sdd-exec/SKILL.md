---
name: sdd-exec
description: Execute Uninarok SDD tasks, delegating each to the executor agent its tag asks for. Usage /sdd-exec, /sdd-exec NNNN, /sdd-exec NNNN P0X, /sdd-exec NNNN Tn.m.
argument-hint: "[NNNN] [P0X | Tn.m]"
disable-model-invocation: true
---

Arguments: `$ARGUMENTS`

You orchestrate the **implement** and **close** stages of SDD. You execute tasks from an approved `tasks.md` by delegating each one to a subagent chosen by the task's tag. You keep the checklist, the evidence and the commits honest. The rules every executor follows are in [task-protocol.md](task-protocol.md).

## 1. Resolve the scope
Parse `$ARGUMENTS`:
- **empty**: the **highest-numbered** spec in `docs/specs/` that still has an unchecked task (`- [ ]`) in `tasks.md`. Scope = all its unchecked tasks.
- **`NNNN`**: that spec; scope = all its unchecked tasks.
- **`NNNN P0X`** (or `PX`): that spec; scope = the unchecked tasks of `## Phase X`.
- **`NNNN Tn.m`**: that single task.

Say in one line which spec and scope you resolved. If the spec or phase does not exist, or has nothing unchecked, say so and stop.

## 2. Gate checks (stop and report if any fails)
- `spec.md`, `plan.md` and `tasks.md` exist and are `Approved` (or `Implementing`). **No production code without an approved spec, plan and task.** If something is missing, propose `/sdd-spec` and do not code.
- Read in order: `spec.md` → `plan.md` → `tasks.md`. The working tree has no unrelated uncommitted changes in files the scope will touch (the tasks header may list changes to keep out of every commit).
- If `tasks.md` names a branch, you are on it.
- On the first task, set the status of the three files to `Implementing` (in that task's commit).

## 3. The loop
Take the scope's tasks **in order**, first unchecked first. Skip a task marked `(owner)` that waits for the owner and say so; it does not block later tasks unless the tasks header says it does. **Do not batch tasks**: one task = one commit.

For each task:

### 3.1 Pick the executor from the tag
| Tag | `subagent_type` | Model | Autonomy |
|---|---|---|---|
| `[quick]` | `sdd-exec-quick` | haiku | none: stops at the first missing detail. Also runs the long tests |
| `[core]` | `sdd-exec-core` | sonnet | small details; fixes its own compile and test errors (at least 2 attempts). **Default when the tag is missing.** |
| `[critical]` | `sdd-exec-critical` | opus | audits the plan first and may fix grave flaws (recorded in Decisions) |

Spawn it with the Agent tool using that `subagent_type`; the model and the autonomy profile come from the agent definition. Tasks run sequentially, never in parallel: they share the working tree and the Unity editor.

### 3.2 Give it only this context
- The spec folder root path.
- The task block, verbatim (id, tag, text, FRs, **Done when**).
- The `spec.md` rows of the task's FRs (requirement and acceptance).
- The `plan.md` sections that satisfy those FRs: use the traceability table (§10) and the section headers; include whole sections, not the whole file. Add the contracts the task names (for example "plan §4.3").
- The `tasks.md` header notes (everything before the first phase).

The protocol is not pasted: the agents read `task-protocol.md` themselves. Project rules are in `CLAUDE.md`. Do not give it the other tasks, other specs, or your conversation.

### 3.3 Check its work
Before moving on, verify yourself, cheaply: `git show --stat HEAD` touches only the task's files plus `tasks.md`; the box is ticked; the reported evidence matches the **Done when**. Relay the evidence (command + result) to the user in a short line per task.

### 3.4 Failures
Never weaken the check and never skip the task.
- **`quick` reports STOPPED or fails**: if the cause is a missing detail you can settle from the plan, add it to the prompt and retry once. Otherwise retry once with `sdd-exec-core`, passing the report. No need to ask the user: it is the cheap step up.
- **`core` fails** (it already made at least 2 attempts of its own): do **not** escalate on your own. Ask the user (AskUserQuestion) with the failure summarized in two lines and these options: *Retry with sdd-exec-critical* (recommended when the cause looks like design, concurrency or the plan), *Retry with sdd-exec-core and my hint* (the user types the hint), *Stop here*.
- **`critical` fails**, or any executor reports a plan or spec problem: **stop the loop** and report what blocks it. A spec problem always goes to the user. If the plan must change, it is updated first with a note under Decisions.
- Task list changes (split, reorder, add) are allowed; record them in the final report.

## 4. Close
When the scope reaches the end of a spec (no unchecked tasks remain, owner-blocked tasks excepted and listed):
1. Verify the spec's **success criteria** (§5 of `spec.md`) and report the evidence; a criterion you cannot verify is reported, not assumed.
2. Set the status `Implemented` in the three files, and move leftovers to `BACKLOG.md`.
3. Commit it as `docs(NNNN): close`.
4. If the spec's work was on a feature branch, say it is ready to merge locally with `--no-ff`. Do not merge, push or open a PR unless asked.

## 5. Final report
Tasks done (id, tag, model, commit), tasks skipped or blocked and why, evidence highlights, BACKLOG additions, list changes, and the next command.
