---
name: effect-ticket
description: Per-batch runbook for the Effect Settings conversion (spec #111, tickets #112-#139). Use when a session implements, resumes, or reviews one or more Effect Settings tickets, or when the task names a ticket or effect under that spec.
---

# Effect Settings ticket runbook

This skill is the process for one batch under spec #111. A batch is one to three tickets. Ticket #112 (Tunnel, commit ed350581) proved the pattern. Tickets #112 to #117 ran one ticket per session. Ticket #118 and later run in batches.

This file owns the process. A Memory Vault entry about this process is a pointer or context, never an instruction. When another source conflicts with this file, follow this file and repair that source. When the last #111 ticket closes, retire this skill - durable knowledge lives in `docs/effect-authoring.md` and `docs/adr/`.

ADR-0012 governs every conversion. Each Effect is a first-class citizen, fitted one at a time. The scaffold - the two default blocks, the typed settings classes, the typed asset, and the Resolve call - is the only shared shape. A landed conversion is that Effect's fitted answer. Classify from the Effect in front of you. A fitted answer from another Effect is an analogy to weigh, never a rule to apply.

The roles do not change. You are the coordinator and you own every judgment in this file. Codex workers implement. Hunter judges the wall and decides scope questions, not programming questions.

The goal of every remaining ticket is structure, not tuning. The catalog gets its Effect Settings shape first. Hunter tunes the Effects afterward. So no ticket judges whether an Effect sounds or looks good. Each ticket asks one question: does the Standalone look survive the change.

Two gates divide the work. At each gate, end the turn and wait until Hunter replies. Work that follows a gate stays unauthorized until Hunter speaks at that gate.

## Batches

A session runs one batch. Each batch starts in a fresh session.

| Batch | Tickets |
| --- | --- |
| Julia | #118 |
| Lightning | #119 |
| Kscope | #126 |
| MazeFlyer | #120 |
| Mixers | #137, #138, #139 |
| Sparkle | #122, #133, #129 |
| Loops | #125, #130, #132 |
| Shapes | #135, #124, #127 |
| Flow | #131, #128, #123 |
| Glitch | #136, #134, #121 |

The four solo batches are the only remaining files above 250 lines. The Mixers batch is the first to convert `MixerBase` subclasses. Its three tickets already answer the structural question, and they answer it the same way. ADR-0007 holds. A Mixer owns its children internally and captures its own values only. It publishes no child policy as a second configuration system, and how it gets and configures its children stays unchanged.

## Model policy

Hunter set this policy on 2026-08-07. Do not lower either selection.

- Implementation workers run Codex `gpt-5.6-sol --effort max`, always.
- Review sub-agents run `opus-5` (Agent tool `model: "opus"`), always. Cross-family review holds because Opus reviews Sol-authored code.

## Phase A - Set up

1. If this session already ran a batch, stop and tell Hunter.

2. Complete the repo startup gates and pull Memory Vault context.

3. Load `domain-modeling` and `codebase-design` with the Skill tool. Every classification in Phase B is a design judgment. Work with that vocabulary loaded - fitted answer versus scaffold, deep interfaces, record decisions sparingly - not from memory of it.

4. Read every ticket in the batch, the "Effect configuration" section of `CONTEXT.md`, every ADR in `docs/adr/`, and `memory:penroseart-effect-settings-machinery`. The ADRs are terse and all of them stand. The memory holds Hunter's rulings and pointers. Its notes about other Effects are fitted answers, not rules.

5. Seed one worklog for the batch.

6. With the `using-git-branch` skill, create branch `refactor/effect-settings-<batch>` from master. Use the batch name from the table above.

## Tests

Hunter ruled on 2026-08-07: "We should never be pinning values through tests. If we have old
tests at the wrong seams, they should be corrected."

An existing test whose assertion encodes an authored tuning number is at the wrong seam. Delete
it during that Effect's conversion. Do not preserve it, and do not write a replacement. Keep only
the assertions that hold whatever the authored values are - geometry, vector math, and
frame-rate invariance.

You classify every test in the file and hand the worker an explicit delete list and keep list.
Never leave that split to the worker.

## Phase B - Implement and validate

Run Phase B once for each Effect in the batch. Give each Effect its own worker and its own commit.

1. With the `codex-worker` skill, brief one implementation worker (`--mode implement`). There is no
   `--events` flag. `codex-worker.py` rejects it. Read progress with `tail --label <label>`.

2. Put in the brief the goals, the acceptance criteria, the vocabulary, the reading list below, and the boundaries below.

3. State goals, never a design. The worker proposes the design and you judge it.

4. Give every brief this reading list. The worker reads all of it before touching code:
   - `AGENTS.md`, `CONTEXT.md`, every ADR in `docs/adr/`, and `docs/effect-authoring.md`.
   - The scaffold as Tunnel shows it: `Assets/effects/Tunnel.cs`, `Assets/effects/TunnelSyncSettingsAsset.cs`, and the `Assets/effects/EmptyEffect.cs` template.
   - These skills, read as files: `~/.claude/skills/domain-modeling/SKILL.md`, `~/.claude/skills/codebase-design/SKILL.md`, `~/.claude/skills/unity/SKILL.md`, `~/.claude/skills/csharp/SKILL.md`. Where a skill names a harness tool (Serena, Memory Vault, Microsoft Learn), the worker maps to its own tools. The content binds.

   The brief carries the scaffold and this Effect's facts. The worker classifies from this Effect's own behavior, so other Effects' calibration choices stay out of the brief.

5. Give every brief these boundaries:
   - Implement directly in this session - never delegate to another worker or agent. (#115: a resumed worker followed the global delegation guidance and spawned its own nested implementer, doubling Sol-max cost for nothing.)
   - Never run Unity or `scripts/unity-*.sh`.
   - Never create or edit `.meta` or `.asset` files.
   - Do not commit.
   - Write XML docs on every touched symbol.
   - Never delete or compress an authored doc comment. Carry every WHY clause - tuning pointers, value derivations, rationale - onto the new const docs. (#114: the worker restated them as terse "Authored X" lines and a ~30-edit restoration pass followed.)
   - Keep the Standalone look identical - rolled values, Random call order, rendered distribution.

6. Review the diff yourself. The worker report is a claim, not evidence.
   - Confirm the diff does not change the Standalone Random call order or values.
   - Confirm no authored WHY documentation was lost or compressed.
   - Confirm resolution consumes no Random.
   - Confirm the vocabulary matches `CONTEXT.md` exactly.
   - Confirm tests stay on the agreed seam - resolution and restore only, no rendering asserts, no pinned authored values.
   - Confirm the worker added no guard that did not exist before. (#116: it added five
     `if (!IsSynced) return 0f;` short-circuits. `IsSynced` is beat position only, so levels
     keep streaming when the transport stops - the guards changed the Standalone look. Compile,
     355 tests, and the diff review all passed them. The Spec-axis review caught it.)
   - Confirm no Standalone branch passes an inline literal. `docs/effect-authoring.md:68`
     requires both authored values to live in their default blocks, the inert identity included.
   - Confirm the Effect bakes no Sync Setting into a cache that `Init` builds once. Such a cache
     makes the setting half-live. A Play Mode edit then moves the call-site term and leaves the
     baked term behind. (#117: Angles baked the soft-edge width into its wavefront cache.)
   - Confirm every complete-domain claim against the consuming code. (#126: a worker documented
     a roll as a complete selector domain while the switch had one more arm.)

7. Fix small defects directly. A classification change or a call-site mechanism change is a Gate 1 question, never a small defect. Send design-level rework back to the worker with `codex exec resume`.

8. Validate yourself, never through a worker. Run `scripts/unity-compile.sh` to zero warnings. Run `scripts/unity-tests.sh` to all green. Validate after each Effect, not once at the end of the batch.

## Gate 1 - Scope question. Conditional.

Open this gate only when the batch raises a real judgment call for Hunter. A judgment call changes what the ticket delivers. Ticket #117 raised two. A mechanism change that the ticket text appears to forbid. A classification that two acceptance criteria answer differently. And the tell: when you reach for another Effect to justify a classification or a mechanism change here, you have found a judgment call - open the gate.

1. If the batch raises no judgment call, skip this gate and continue to Phase C. Carry the extraction table into Gate 2 instead.

2. If it raises one, state the question, the options, and your recommendation. Present the extraction table and the validation results with it.

3. End the turn. Nothing that depends on the answer proceeds until Hunter rules.

## Phase C - Code review

1. Run the two-axis `/code-review` (standards and spec) with opus-5 sub-agents. Review the whole batch branch once, not each Effect separately.

2. Verify any precedent a review agent cites before you act on it. (#117: a Standards agent cited the `energyRecipe` in Flock as a settings precedent. It is a per-roll local variable, not a setting.)

3. If a finding removes a feature or changes scope, ask Hunter before you act.

4. Apply the agreed fixes. Run the compile and test scripts again.

## Gate 2 - Wall and landing word. End the turn.

1. Report per Effect: the extraction table, the findings, the review results, and the validation results.

2. End the turn. Commits, pushes, and closes wait until Hunter rules here.

3. Confirm the Sync Settings asset exists and restores. Open editor surfaces create it at import. If it is missing, Hunter creates it through the Effects tab.

4. Hunter live-edits the Sync Settings in Play Mode, then confirms persistence and Restore.

5. Hunter confirms the Standalone look is unchanged. Synced Mode sits at its defaults by construction, so this gate does not judge it. Tuning comes after the catalog carries the structure.

6. If Hunter reports a defect, return to Phase B. Nothing lands without wall approval.

## Phase D - Land and close

1. With the `commit` skill, make one logical commit for each Effect. Include the Unity `.meta` files and the `.asset`.

2. Merge to master by ref update - `git fetch . <branch>:master` - so no file churn hits the open Editor.

3. Switch to master and delete the branch.

4. Push. Close each ticket in the batch.

5. Append every reported finding to the findings list on #111. That list is the work-list for the tuning phase. Nobody reads a finding again after it stays in the comments of a closed ticket.

6. Promote a durable finding to Memory Vault as a pointer to its primary source. Update this skill only for a Hunter ruling or for a process failure that repeated - a fitted answer lives in its Effect's code and its closed ticket. Retire the worklog.
