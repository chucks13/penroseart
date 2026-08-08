---
name: effect-ticket
description: Per-ticket runbook for the Effect Settings conversion (spec #111, tickets #112-#139). Use when a session implements, resumes, or reviews an Effect Settings ticket, or when the task names a ticket or effect under that spec.
---

# Effect Settings ticket runbook

This skill is the process for one ticket under spec #111. Ticket #112 (Tunnel, commit ed350581) proved it.

The roles do not change. You are the coordinator and you own every judgment in this file. Codex workers implement. Hunter judges the wall and decides scope questions, not programming questions. A session runs one ticket.

Three gates divide the work. At each gate, end the turn and wait until Hunter replies. Work that follows a gate stays unauthorized until Hunter speaks at that gate.

## Model policy

Hunter set this policy on 2026-08-07. Do not lower either selection.

- Implementation workers run Codex `gpt-5.6-sol --effort max`, always.
- Review sub-agents run `opus-5` (Agent tool `model: "opus"`), always. Cross-family review holds because Opus reviews Sol-authored code.

## Phase A - Set up

1. If this session already ran a ticket, stop and tell Hunter.

2. Complete the repo startup gates and pull Memory Vault context.

3. Read `memory:penroseart-effect-settings-machinery`, the ticket, the "Effect configuration" section of `CONTEXT.md`, and ADR-0012.

4. Seed a worklog for the ticket.

5. With the `using-git-branch` skill, create branch `refactor/effect-settings-<effect>` from master.

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

1. With the `codex-worker` skill, brief one implementation worker (mode implement, `--events`).

2. Put in the brief the goals, the acceptance criteria, the vocabulary, ADR-0012, the Tunnel precedent, and the boundaries below.

3. State goals, never a design. The worker proposes the design and you judge it.

4. Give every brief these boundaries:
   - Implement directly in this session - never delegate to another worker or agent. (#115: a resumed worker followed the global delegation guidance and spawned its own nested implementer, doubling Sol-max cost for nothing.)
   - Never run Unity or `scripts/unity-*.sh`.
   - Never create or edit `.meta` or `.asset` files.
   - Do not commit.
   - Write XML docs on every touched symbol.
   - Never delete or compress an authored doc comment. Carry every WHY clause - tuning pointers, value derivations, rationale - onto the new const docs. (#114: the worker restated them as terse "Authored X" lines and a ~30-edit restoration pass followed.)
   - Keep the Standalone look identical - rolled values, Random call order, rendered distribution.

5. Review the diff yourself. The worker report is a claim, not evidence.
   - Confirm the diff does not change the Standalone Random call order or values.
   - Confirm no authored WHY documentation was lost or compressed.
   - Confirm resolution consumes no Random.
   - Confirm the vocabulary matches `CONTEXT.md` exactly.
   - Confirm tests stay on the agreed seam - resolution and restore only, no rendering asserts, no pinned authored values.
   - Confirm the worker added no guard that did not exist before. (#116: it added five
     `if (!IsSynced) return 0f;` short-circuits. `IsSynced` is beat position only, so levels
     keep streaming when the transport stops - the guards changed the Standalone look. Compile,
     355 tests, and the diff review all passed them; the Spec-axis review caught it.)
   - Confirm no Standalone branch passes an inline literal. `docs/effect-authoring.md:68`
     requires both authored values to live in their default blocks, the inert identity included.

6. Fix small defects directly. Send design-level rework back to the worker with `codex exec resume`.

7. Validate yourself, never through a worker. Run `scripts/unity-compile.sh` to zero warnings. Run `scripts/unity-tests.sh` to all green.

## Gate 1 - Findings. End the turn.

1. Present to Hunter the extraction table, the findings, and the validation results.
   - The extraction table lists each captured literal, its new name, and its block.
   - The findings list everything questionable that this ticket reports instead of fixes.

2. End the turn. Code review, commits, and pushes wait until Hunter rules here.

## Gate 2 - Wall. Hunter acts.

1. Hunter creates the asset through the Effects tab.

2. Hunter live-edits the Sync Settings in Play Mode, then confirms persistence and Restore.

3. Hunter judges the look in both modes.

4. If Hunter reports a defect, return to Phase B. Nothing proceeds without wall approval.

## Phase C - Code review

1. Run the two-axis `/code-review` (standards and spec) with opus-5 sub-agents.

2. If a finding removes a feature or changes scope, ask Hunter before you act.

3. Apply the agreed fixes. Run the compile and test scripts again.

## Gate 3 - Landing word. End the turn.

1. Report the review results and the fix state.

2. End the turn. Wait until Hunter gives the word to land.

## Phase D - Land and close

1. With the `commit` skill, make one logical commit. Include the Unity `.meta` files and the `.asset`.

2. Merge to master by ref update - `git fetch . <branch>:master` - so no file churn hits the open Editor.

3. Switch to master and delete the branch.

4. Push and close the ticket.

5. Promote durable findings to Memory Vault. If the process changed, update this skill. Retire the worklog.
