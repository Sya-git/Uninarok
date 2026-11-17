# CLAUDE.md: Uninarok

Unity (C#) custom client for Ragnarok Online, built to talk to **Hercules** servers (login → char → map). Target `PACKETVER` is **20220406**. This repo uses **Spec-Driven Development (SDD)**: every non-trivial change starts as a spec, becomes a plan, and is executed from a task list.

---

## 1. SDD workflow

### 1.1 Layout

```
docs/specs/
  BACKLOG.md                      # unrelated issues noticed while working (never fix them inline)
  NNNN-short-slug/                # NNNN = zero-padded sequence, e.g. 0001-networking-core
    spec.md                       # WHAT and WHY     (product / reviewer + AI)
    plan.md                       # HOW              (architect / senior dev + AI)
    tasks.md                      # EXECUTION        (atomic, ordered checklist)
```

Reference material (for example the packet reference under `Packets/`) may sit next to the spec. It is normative for its topic, is linked from the plan, and does not replace the spec, plan or tasks.

### 1.2 The three files

| File | Answers | Audience | Contains | Must NOT contain |
|---|---|---|---|---|
| `spec.md` | What and why | Product, reviewers, AI context | Problem, goals, non-goals, numbered requirements (`FR-n`) with observable acceptance, success criteria, assumptions, open questions | Class names, file paths, libraries, code |
| `plan.md` | How | Architects, senior devs, AI | Architecture, assemblies and file paths, interface contracts (signatures), data structures, threading, error handling, test plan, migration, decisions and rejected alternatives, risks, FR traceability table | New requirements (those go in the spec) |
| `tasks.md` | In what order | Whoever implements (human or AI) | Phased checklist; each task atomic, ordered, tagged with `FR-n`, with a **Done when** check | Design discussion (put it in the plan) |

Size guide: the spec stays brief (about 2 pages). The plan can be long. A task is one reviewable commit.

### 1.3 Lifecycle and status

`Draft → Approved → Implementing → Implemented` (or `Superseded by NNNN`). Status lives in the header of each file. **Only a human sets `Approved`.**

1. **Specify.** Write `spec.md`. Ask the human to resolve open questions that block requirements.
2. **Plan.** After the spec is approved, write `plan.md`. Every plan section states which `FR-n` it satisfies, and the traceability table at the end covers every FR.
3. **Break down.** After the plan is approved, write `tasks.md`. Every task references at least one `FR-n`. Every FR is covered by at least one task.
4. **Implement.** Execute tasks in order (section 1.4).
5. **Close.** Verify the spec's success criteria, set status `Implemented`, move leftovers to `BACKLOG.md`.

### 1.4 Rules for Claude

1. **No production code without an approved spec, plan and task.** If asked to implement something that has none, propose the spec first (short), do not start coding.
2. **Read in order:** `spec.md` → `plan.md` → `tasks.md` before touching code. Work on the **first unchecked task** unless told otherwise. Do not batch several tasks into one change.
3. **One task = one commit.** Tick the task's checkbox in the same commit. Commit message: `feat(0001): T2.4 swallow shutdown exceptions in read loop` (type: `feat|fix|test|refactor|docs|chore`).
4. **Verify "Done when" before ticking.** Run the tests or the manual check and report the evidence (command + result) in your reply. Never tick on assumption.
5. **Tests first** where the tasks say so: commit or enable the failing test, then implement.
6. **Don't change the spec silently.** If implementation reveals the spec is wrong or incomplete, stop and ask. Plan deviations: update `plan.md` first (with a short note under Decisions), then proceed. Task list changes (split, reorder, add) are allowed, but record them in the PR.
7. **No guessing on open questions** (`OQ-n`). Ask, or use the default recorded in the spec.
8. **Stay in scope.** Unrelated bugs or cleanups go to `docs/specs/BACKLOG.md` as one line each (file, symptom). Do not fix them in the same change.
9. **Keep traceability:** new code, tests and tasks reference `FR-n` where meaningful (test names or comments, not code noise).
10. **Report blockers early:** if a task cannot meet its "Done when", say what blocks it. Do not weaken the check.

### 1.5 Templates

**`spec.md`**
```markdown
# SPEC-NNNN: Title
| Status | Draft | Created | YYYY-MM-DD | Owner | name | Plan | plan.md | Tasks | tasks.md |
## 1. Why            <!-- problem, impact, why now -->
## 2. Goals
## 3. Non-goals
## 4. Requirements   <!-- table: ID | Pri (M/S) | Requirement | Acceptance (observable) -->
## 5. Success criteria
## 6. Assumptions and constraints
## 7. Open questions <!-- table: # | Question | Blocks -->
```

**`plan.md`**
```markdown
# PLAN-NNNN: Title
| Implements | spec.md | Status | Draft | Audience | Architects, senior devs, AI |
## 1. Constraints and assumptions
## 2. Architecture          <!-- diagram + data flow -->
## 3. Assemblies and file layout
## 4. Component contracts   <!-- each: "Satisfies FR-n", signatures, behaviour, error handling -->
## 5. Threading and lifecycle
## 6. Test plan
## 7. Migration and compatibility
## 8. Decisions             <!-- chosen vs rejected alternatives -->
## 9. Risks
## 10. Traceability         <!-- FR -> plan sections -->
```

**`tasks.md`**
```markdown
# TASKS-NNNN: Title
## Phase 0: Prepare
- [ ] **T0.1** Imperative description of one atomic change. (FR-n)
  *Done when:* observable, checkable condition.
## Phase 1: ...
```

---

## 2. Project conventions

Subsystem rules (threading, allocation, packet formats and so on) live in that subsystem's `plan.md` and reference docs, not here. Read the plan of the spec you are working on.

- **Unity API:** only on the main thread unless a plan says otherwise.
- **Logging levels:** `LogError` only for real errors. State changes and info use `Log`/`LogWarning`.
- **Generated code:** never hand-edit generated files; change the generator or its inputs.
- **Reference docs:** a doc that is normative for a topic (for example the packet reference of SPEC-0001) is updated in the same commit as the code it describes.
- **Dependencies:** no new third-party libraries without a plan entry.
- **Secrets:** never commit credentials. Dev-only credential fields must be stripped from release builds.

### Commands
Use Unity-MCP to refresh unity solutions and run commands

### Definition of done (any task)
Code compiles with no new warnings, relevant tests pass, "Done when" verified, checkbox ticked, commit follows the format in 1.4, no unrelated changes.
