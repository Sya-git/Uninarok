---
name: sdd-spec
description: Create a spec, plan and tasks for a new Uninarok feature with the SDD agents. Usage /sdd-spec [NNNN] <description>; /sdd-spec NNNN resumes a spec at its next missing stage; /sdd-spec NNNN --revise <change> modifies an existing spec and propagates it to plan and tasks.
argument-hint: "[NNNN] <feature description> | NNNN --revise <change>"
disable-model-invocation: true
---

Arguments: `$ARGUMENTS`

You orchestrate the **specify → plan → break down** stages of SDD. You do not write the documents yourself: you delegate to the agents and handle numbering, human gates and open questions. You write no production code and you do not commit (the user decides).

## Layout
```
docs/specs/
  BACKLOG.md                 # unrelated issues noticed while working (one line each)
  NNNN-short-slug/           # NNNN zero-padded; slug in kebab-case
    spec.md   plan.md   tasks.md
```
Reference material (for example a packet reference under `Packets/`) may sit next to the spec. It is normative for its topic, linked from the plan, and does not replace spec, plan or tasks.

| File | Answers | Contains | Must NOT contain | Template |
|---|---|---|---|---|
| `spec.md` | What and why (about 2 pages) | Problem, goals, non-goals, `FR-n` with observable acceptance, success criteria, assumptions, open questions | Class names, file paths, libraries, code | [templates/spec.md](templates/spec.md) |
| `plan.md` | How (can be long) | Architecture, assemblies and paths, signatures, data structures, threading, errors, tests, decisions, risks, FR traceability | New requirements | [templates/plan.md](templates/plan.md) |
| `tasks.md` | In what order | Phased checklist; atomic tasks, each tagged `[quick]`/`[core]`/`[critical]`, with `FR-n` and **Done when** | Design discussion | [templates/tasks.md](templates/tasks.md) |

A task is one reviewable commit. The agents read the templates themselves; do not paste them.

## Lifecycle
`Draft → Approved → Implementing → Implemented` (or `Superseded by NNNN`). Status lives in the header of each file. **Only a human sets `Approved`**: either the user edits the file, or tells you in chat to mark it, and then you edit the header.

## 1. Resolve the number and the stage
1. List `docs/specs/` directories matching `^\d{4}-`. Also check branches: `git branch -a --list "*NNNN-*"`.
2. Parse `$ARGUMENTS`:
   - **`NNNN --revise <change>`**: revise mode, section 6. The folder must exist; if not, say so and stop.
   - **No leading 4-digit number**: new spec, `NNNN = highest existing + 1`. The rest is the description.
   - **Leading `NNNN` and a description, number free** (no folder, no branch): new spec with that number.
   - **Leading `NNNN` and a description, number taken**: stop and tell the user (name the existing folder or branch). Do not overwrite.
   - **Only `NNNN`, folder exists**: resume that spec (stage below).
   - **Nothing at all**: ask the user for a description.
3. Show the number and slug (a short kebab-case slug from the description) in one line, then continue without asking, unless the slug is ambiguous.

Stage for a resumed spec, by what exists and the `Status` in each header:
- no `spec.md` → stage A; `spec.md` Draft → ask whether it is approved (or apply feedback and redo A);
- `spec.md` Approved, no `plan.md` → stage B; `plan.md` Draft → same as above for B;
- `plan.md` Approved, no `tasks.md` → stage C; `tasks.md` exists → report and stop (execution is `/sdd-exec`).

## 2. Stage A: spec
Call the `sdd-spec-writer` agent with: the folder path, NNNN, the description, and any answers or feedback so far. Read its report.
- If it lists open questions that block requirements, ask the user (AskUserQuestion, with the agent's suggested default as the first option). Re-run the agent with the answers. Repeat until none block. Answered questions leave §7; the decision lives in the FR text.
- Do not invent answers: an open question the user does not answer keeps its recorded default only if the spec states one.
- **Gate:** show the user the path and a summary, and stop. Continue only when the user says the spec is approved (then set its status to `Approved`) or gives feedback (re-run A).

## 3. Stage B: plan
Call the `sdd-plan-writer` agent with the folder path. Read its report.
- If it reports **spec issues**, stop and take them to the user; do not edit the spec silently. Go back to A with the user's decision.
- If it reports risks needing a human decision, ask.
- **Gate:** same as stage A for the plan.

## 4. Stage C: tasks
Call the `sdd-task-writer` agent with the folder path. Read its report and open the file to check: every task has a tag, at least one `FR-n` and a **Done when**; every FR of the spec is covered. If the agent's FR coverage check lists gaps, re-run it with the gaps named.
- **Gate:** show task counts per tag and stop. The user approves; then set the status `Approved`.

## 5. Finish
Report: folder, status of each file, next command (`/sdd-exec NNNN`). Mention that the files are uncommitted.

## 6. Revise mode (`NNNN --revise <change>`)
Modifies an existing spec and propagates the change. The same agents run, each told `mode: revise`; nothing is rewritten from scratch and nothing is committed.
- **Before starting**: read the three headers. If the spec is `Implemented`, ask the user (AskUserQuestion): *revise this spec* or *create a new spec that supersedes it*. If it is `Implementing`, warn that code and ticked tasks exist and will be kept (rework becomes new tasks). Check the working tree has no uncommitted edits to the three files; if it does, ask first.
- **R1, spec**: call `sdd-spec-writer` with `mode: revise`, the change and the folder. Open questions are handled as in stage A. **Gate**: show the FRs added, changed and removed, and stop. Continue when the user approves the spec (you then set `Approved`) or gives feedback.
- **R2, plan**: if no FR changed in a way that touches the plan (pure wording), say so and skip to the end. Otherwise call `sdd-plan-writer` with `mode: revise` and the affected FRs. If it reports spec issues, go back to R1 with the user. **Gate**: show sections edited and the **implemented work affected**; the user approves.
- **R3, tasks**: call `sdd-task-writer` with `mode: revise`, the affected FRs and the plan report. Checked tasks are never altered: rework appears as new tasks naming the original, obsolete unchecked tasks are struck through. **Gate**: show tasks added, struck and edited, and the FR coverage; the user approves.
- **Finish**: report what changed in each file (suggest `git diff docs/specs/NNNN-*/` to review), the status of each file (`Draft` until approved), and the next command (`/sdd-exec NNNN`). Files stay uncommitted; the revision is one commit the user makes.
