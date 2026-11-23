# CLAUDE.md: Uninarok

Unity (C#) custom client for Ragnarok Online, built to talk to **Hercules** servers (login → char → map). Target `PACKETVER` is **20220406**. This repo uses **Spec-Driven Development (SDD)**: every non-trivial change starts as a spec, becomes a plan, and is executed from a task list.

---

## 1. SDD workflow

Specs live in `docs/specs/NNNN-slug/` as `spec.md` (what and why) → `plan.md` (how) → `tasks.md` (ordered, tagged tasks). Status: `Draft → Approved → Implementing → Implemented`. **Only a human sets `Approved`.**

- **Create:** `/sdd-spec [NNNN] <description>` runs the agents `sdd-spec-writer`, `sdd-plan-writer` and `sdd-task-writer`, with a human gate between stages.
- **Execute:** `/sdd-exec [NNNN] [P0X | Tn.m]` runs tasks, one commit each, with the model the task tag asks for.
- **Gate:** no production code without an approved spec, plan and task. If asked for something that has none, propose the spec first.
- Templates, rules, numbering and commit format live in `.claude/skills/sdd-spec`, `.claude/skills/sdd-exec` and `.claude/agents/`, not here.

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
