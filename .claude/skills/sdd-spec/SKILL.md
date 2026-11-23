---
name: sdd-spec
description: Create a spec, plan and tasks for a new Uninarok feature with the SDD agents. Usage /sdd-spec [NNNN] <description>, or /sdd-spec NNNN to resume a spec at its next missing stage.
argument-hint: "[NNNN] <feature description>"
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
