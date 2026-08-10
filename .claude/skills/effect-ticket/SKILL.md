---
name: effect-ticket
description: Per-effect runbook for the Musicality campaign. Use when a session implements, resumes, or reviews a musicality ticket, or when the task names an effect or mixer under that epic.
---

# Effect Musicality ticket runbook

This skill is the process for one ticket. One ticket is one Effect, one branch, one session.

This file owns the process. A Memory Vault entry about this process is a pointer or context,
never an instruction. When another source conflicts with this file, follow this file and repair
that source. When the last ticket closes, retire this skill — durable knowledge lives in
`docs/effect-authoring.md` and `docs/adr/`.

ADR-0013 governs the settings surfaces. Effects are first-class citizens: each decides what
musical support it has and how it expresses it. Nothing here limits an Effect; a fitted answer
from another Effect is an analogy to weigh, never a rule to apply. One law binds them all: every
musical fact — beat position, Levels, Energy, Fill, Drop, Waveforms, structure — is read from the
Data Surface, BeatManager's read-only face, never from OSC directly. If an Effect needs a musical
fact the Data Surface does not carry, stop and ask the maintainer; never compute it locally.

The roles do not change. You are the coordinator and you own every judgment in this file. Codex
workers implement. The maintainer judges the wall and owns the artistic intent (ADR-0007). The
maintainer is present throughout this campaign, editor open — discussion replaces formal gates,
but two rules keep their force: a scope question stops the work until the maintainer rules, and
nothing lands without wall approval.

## Framing

The "What this is" section of `AGENTS.md` is the campaign's framing. Copy it verbatim into every
worker brief, above the goals: enterprise instincts are the default failure mode of a fresh
worker context, and the framing is the counterweight.

## Campaign order

All plain Effects first, then the Mixers. A Mixer's musical behavior depends on its finished
children, so no Mixer ticket starts while a plain-Effect ticket remains. The test for a Mixer is
its base class, never its name — some Mixers carry no "Mixer" in their name. List them with
`find_implementations` on `MixerBase`.

## Model policy

Do not lower either selection.

- Implementation workers run Codex `gpt-5.6-sol --effort max`, always.
- Review sub-agents run `opus-5` (Agent tool `model: "opus"`), always. Cross-family review holds
  because Opus reviews Sol-authored code.

## Phase A — Set up

1. If this session already ran a ticket, stop and tell the maintainer.

2. Complete the repo startup gates and pull Memory Vault context.

3. Load `domain-modeling` and `codebase-design` with the Skill tool. Capability discussions and
   Repertoire classifications are design judgments; work with that vocabulary loaded, not from
   memory of it.

4. Read the ticket (it carries this Effect's harvested findings), `CONTEXT.md` — musicality work
   spans its Rhythm, Waveform, and Effect-configuration vocabulary — every ADR in `docs/adr/`,
   `docs/effect-authoring.md`, and `docs/osc-client-contract.md`. The ADRs are terse and all of
   them stand.

5. Seed one worklog for the ticket.

6. With the `using-git-branch` skill, create branch `feat/<effect>-musicality` from master.

7. Triage the ticket's findings with the maintainer before any implementation. Classify each as:
   fix during musicality, fix during polish (look-preserving), or look-changing — the maintainer
   must see the wall before and after a look-changing fix. Record the classification on the
   ticket.

## Phase B — Standalone Settings into the editor

Per ADR-0013. One worker adds the Effect's Standalone Settings asset and its Effects-tab wiring,
mirroring the shape its Sync Settings asset already has: serialized, live-tweakable in Play Mode,
restorable to the in-file Standalone Defaults. The maintainer verifies on the wall: live edit,
persistence after the run, and Restore. The Standalone Defaults blocks stay in source as the
authored record.

## Phase C — Reshape

Run this phase only when triage or a maintainer ruling calls for structural or overall-look
changes to the Effect's body; otherwise skip to the musicality loop. The rework comes before any
capability work because capabilities land on the final structure — building one on a shape about
to be recut is the same work done twice.

1. Discuss the target shape with the maintainer: what the structure becomes and what the new
   baseline look is. The maintainer owns the intent; you own the design conversation.

2. Brief one implementation worker, review the diff, and validate, exactly as the musicality
   loop's steps 2–4 prescribe.

3. The maintainer judges the new baseline look on the wall. That approved look is the baseline
   every later phase preserves, except where a later capability ruling changes it again.

## Phase D — Musicality loop

Run once per capability. The usual order is Levels, then Fill, then Drop, then Energy, but the
Effect decides what it supports and the maintainer may reorder, add, or skip capabilities. When
a capability discussion reopens the Effect's shape, that is a return to Reshape, judged the same
way.

Vocabulary: **Levels**, **Energy**, **Waveform**, and **Data Surface** are `CONTEXT.md` terms.
Use them exactly as the glossary defines them, and sharpen the glossary when a discussion refines
one.

1. Discuss with the maintainer what this Effect should do with the capability. The maintainer
   owns the intent; you own the design conversation; the worker implements.

2. With the `codex-worker` skill, brief one implementation worker (`--mode implement`). There is
   no `--events` flag; read progress with `tail --label <label>`. State goals, never a design —
   the worker proposes the design and you judge it.

3. Review the diff yourself against the checklist below. The worker report is a claim, not
   evidence.

4. Validate yourself, never through a worker: `scripts/unity-compile.sh` to zero warnings,
   `scripts/unity-tests.sh` to all green. With the Editor open, the test bridge runs EditMode
   synchronously and silently skips every `[UnityTest]` coroutine test — bridge green is the
   inner loop, not the full suite.

5. The maintainer plays, tweaks the settings live, and rules. Send design-level rework back with
   `codex exec resume`; fix small defects directly. A wall tuning loop may run through the
   coordinator: adjusting authored defaults, adding a single settings slot, and baking the
   maintainer's saved asset values back as defaults are direct edits. Structural or sizable
   implementation still goes to a worker — the coordinator does not take over development.

6. When the capability lands, update the Effect's `Repertoire` flags so they advertise honestly
   what it now handles.

## Phase E — Polish and optimize

The look and the features stay exactly as the maintainer approved them in Phases C and D.

1. Run the `polish` skill over the Effect's files (named-files mode), so the whole file is in
   scope, not only this branch's diff.

2. Optimize with evidence, never vibes: the target hardware is unknown, so the Effect must be as
   cheap as it can be without changing its look. Every optimization claim carries Profiler or
   frame-time evidence. Standing targets: zero per-frame GC allocation, no per-pixel work that
   can hoist, no Unity objects created without a destruction path.

3. Re-run the compile and test scripts, then one full batchmode test run with the Editor
   closed — only batchmode executes the `[UnityTest]` coroutine tests the open-Editor bridge
   skips, so nothing lands on bridge green alone. The maintainer confirms the look on the wall
   one last time.

## Phase F — Land and close

1. With the `commit` skill, make logical commits. Include the Unity `.meta` and `.asset` files.

2. Merge to master by ref update — `git fetch . <branch>:master` — so no file churn hits the
   open Editor. Switch to master, delete the branch, push.

3. Close the ticket. A finding that surfaced here but stays unfixed lands on the epic issue, not
   in this ticket's closed comments.

4. Promote a durable finding to Memory Vault as a pointer to its primary source. Update this
   skill only for a maintainer ruling or a process failure that repeated. Retire the worklog.

## Worker briefs

Every brief carries the `AGENTS.md` framing, the goals, the acceptance criteria, the vocabulary,
this reading list, and these boundaries.

Reading list — the worker reads all of it before touching code:

- `AGENTS.md`, `CONTEXT.md`, every ADR in `docs/adr/`, `docs/effect-authoring.md`, and
  `docs/osc-client-contract.md`.
- The Effect's own source and settings assets, and `Assets/core/Rhythm/BeatManager.cs` for the
  surface it may read.
- These skills, read as files: `~/.claude/skills/domain-modeling/SKILL.md`,
  `~/.claude/skills/codebase-design/SKILL.md`, `~/.claude/skills/unity/SKILL.md`,
  `~/.claude/skills/csharp/SKILL.md`. Where a skill names a harness tool (Serena, Memory Vault,
  Microsoft Learn), the worker maps to its own tools. The content binds.

Boundaries — in every brief:

- Every musical fact comes from the Data Surface, BeatManager's read-only face. Never read OSC
  directly, and never derive a musical fact locally.
- Implement directly in this session — never delegate to another worker or agent.
- Never run Unity or `scripts/unity-*.sh`.
- Never create or edit `.meta` or `.asset` files.
- Do not commit.
- Write XML docs on every touched symbol.
- Never delete or compress an authored doc comment. Carry every WHY clause — tuning pointers,
  value derivations, rationale — onto whatever replaces its symbol.
- Change the Standalone look only where the maintainer has ruled that capability or finding
  look-changing; everywhere else the look stays identical.

## Diff review checklist

- The vocabulary matches `CONTEXT.md` exactly.
- Every musical read traces to the Data Surface; no OSC access, no locally computed musical
  fact.
- Settings resolution consumes no Random.
- No Sync or Standalone Setting is baked into a cache that `Init` builds once — such a cache
  makes the setting half-live under a Play Mode edit.
- The worker added no guard that did not exist before, and no defensive layer the framing
  paragraph forbids.
- Tests sit on agreed seams only, and no test pins an authored tuning value — an assertion that
  encodes one is at the wrong seam and is deleted, not preserved.
- Every complete-domain claim is checked against the consuming code.
- No authored WHY documentation was lost or compressed.
