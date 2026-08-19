---
name: effect-ticket
description: Per-effect runbook for the Musicality campaign. Use when a session implements, resumes, or reviews a musicality ticket, or when the task names an effect or mixer under that epic.
---

# Effect Musicality ticket runbook

This skill is the process for one ticket. One ticket is one Effect and one branch. A ticket
routinely outlives a session — the Status and resume section is the path back in.

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
but two rules keep their force: a scope question stops the work until the maintainer decides, and
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

- Implementation workers run Codex `gpt-5.6-sol --effort xhigh`, always. Do not lower this
  selection.
- Standards and Spec review workers run Codex `gpt-5.6-sol --effort xhigh`, always.

## Commit before the next change

This rule holds in every phase, and it has no exception.

1. When a worker returns, validate the work. Compile to zero warnings and run the tests to green.
2. Commit the validated work before you make the next change.
3. Start no worker while the tree is dirty.

Wall approval is not part of this gate. The maintainer judges the look after the commit. A
rejected look becomes a follow-up commit, or a revert by hash.

A worker that edits a dirty tree mixes two changes into one diff. Nobody can tell the two apart
after that, and nobody can revert one of them. The goal is reversibility, not commit count:
never build new work on uncommitted work. One piece of work is one commit — a wall tuning loop
commits when the maintainer settles, not once per nudge.

When a change reshapes a settings class, update its `.asset` files in the same change — they are
plain text, and a stale asset loads a new field as zero and keeps a removed field as drift.
`.meta` files stay Unity-owned without exception.

## Musical claims

This rule holds in every phase, and it has no exception. A musical claim states what a musical
fact means, does, or can do. Findings, diff reviews, and answers to the maintainer all carry
musical claims. `CONTEXT.md` and `docs/osc-client-contract.md` settle every musical claim. Code
alone cannot settle one, because the runtime only shows what it does with the values that
arrive. The sender lives in RaveSystem, so no code in this repo says what the wire sends.

1. Before you state a musical claim, read the `CONTEXT.md` entry for every musical term in it.
2. Read the _Avoid_ list of each entry.
3. If the claim depends on what the wire sends, read that lane in `docs/osc-client-contract.md`.
4. Cite the entries and the lanes where you state the claim.
5. If an _Avoid_ item names the scenario in the claim, discard the claim as that named error.
6. If code appears to contradict either document, report the mismatch as the finding and stop
   for a maintainer decision.

## Documentation order

Canonical application documents describe implemented and maintainer-approved behavior. Do not
change them first to support a proposed code change. After the maintainer approves the behavior,
update the related documents before the ticket lands. Code comments and XML documentation move
with the code that they explain.

## Status and resume

Git and the ticket are the records that survive a session. Post a status comment at every phase
boundary, and post one before the session ends with the ticket open. The comment states what
landed by hash, what the maintainer decided, and what comes next. Record a finding on the ticket
when you find it. Debt on this ticket belongs to this ticket, not to a later session that has
never heard of it.

A resumed ticket starts from state, not from the Phase A reading list. The reading list is
ticket-scoped. Phase A steps 2 and 3 are session-scoped, so complete them again in every
session.

Git says what landed. The comments on the ticket say what the maintainer decided. Read a Phase A
document again only when the current step needs it. The Musical claims rule names one such need.
Rejoin the phase the evidence names, not the phase the last comment names.

## Phase A — Set up

1. If this session already ran a ticket, stop and tell the maintainer.

2. Pull Memory Vault context for this repo and the campaign.

3. Load `domain-modeling` and `codebase-design` with the Skill tool. Stage discussions and
   Repertoire classifications are design judgments; work with that vocabulary loaded, not from
   memory of it.

4. Read the ticket **and every comment on it**, `CONTEXT.md` — musicality work
   spans its Rhythm, Waveform, and Effect-configuration vocabulary — every ADR in `docs/adr/`,
   `docs/effect-authoring.md`, and `docs/osc-client-contract.md`. The ADRs are terse and all of
   them stand.

   The comments are not optional and the body is not a summary of them. A ticket's design is settled
   incrementally at the wall, so a decision in the body is routinely withdrawn by a later comment, and
   a "still open" line is routinely stale the moment the next session lands that work. Reading the
   body alone, or only the newest comments, produces confident wrong statements about what is left
   to build. Read every comment oldest to newest, and treat the newest statement on any point as the
   live one. When the body disagrees with the comments, fix the body in the same session.

5. With the `using-git-branch` skill, create branch `feat/<effect>-musicality` from master.

6. Triage the ticket's findings with the maintainer before any implementation. Classify each as:
   fix during musicality, fix during polish (look-preserving), or look-changing — the maintainer
   must see the wall before and after a look-changing fix. Record the classification on the
   ticket.

## Phase B — Settings check

The Phase B build-out — Standalone Settings and the range audit of both surfaces (ADR-0013) —
has run for every Effect and Mixer in the catalog, so no ticket builds those again. What
remains is a check each ticket runs once, before any change builds on the settings.

The check: each of the Effect's Sync and Standalone `.asset` files is in sync with its
settings class — every field the class declares is present in the asset, and no removed field
lingers. Unity loads a missing field as zero, so an absent field is a silent wrong value on
the wall. Seed missing fields with their authored defaults, delete leftovers, and report what
changed. An Effect missing a whole surface is a maintainer question, not a rebuild from this
runbook.

## Phase C — Reshape

Run this phase only when triage or a maintainer decision calls for structural or overall-look
changes to the Effect's body; otherwise skip to the musicality loop. The rework comes before the
musicality stages because their work lands on the final structure — building a stage on a shape
about to be recut is the same work done twice.

1. Discuss the target shape with the maintainer: what the structure becomes and what the new
   baseline look is. The maintainer owns the intent; you own the design conversation.

2. Brief one implementation worker, review the diff, and validate, exactly as the musicality
   loop's steps 2–4 prescribe.

3. The maintainer judges the new baseline look on the wall. That approved look is the baseline
   every later phase preserves, except where a later stage decision changes it again.

## Phase D — Musicality loop

Phase D runs as three stages in a fixed order. **Basic musicality** comes first: the Effect's
always-on response while synced. It can draw on Levels, Energy, waveform-driven motion — all of
them, some, or none, depending on the Effect and what it is trying to achieve. Fill follows, and
Drop lands last; each is a special sequence that runs independent of the basic musicality. The
maintainer may add or skip a stage, and a stage discussion that reopens the Effect's shape
is a return to Reshape, judged the same way.

Run the loop below once per stage. Offer musicality ideas inside step 1 of the stage at hand —
a suggestion about Drop belongs in the Drop stage, not in the basic-musicality discussion.

Vocabulary: **Levels**, **Energy**, **Waveform**, **Fill**, **Drop**, and **Data Surface** are
`CONTEXT.md` terms.
Use them exactly as the glossary defines them, and sharpen the glossary when a discussion refines
one.

1. Discuss with the maintainer what this Effect should do in the stage at hand. The maintainer
   owns the intent; you own the design conversation; the worker implements.

2. With the `codex-worker` skill, brief one implementation worker (`--mode implement`). The
   brief carries the goals and the design agreed in step 1 — the what. The how is the worker's:
   do not prescribe the code. While the worker runs, wait for completion or do
   work that leaves the tree alone — inspection, reading, preparing the next discussion; the
   `codex-worker` skill owns how to wait. The worker owns the tree until its diff is validated
   and committed. A followed progress stream fills the
   coordinator's context and buys nothing; read the activity log only to diagnose a run that
   failed or returned a doubtful claim.

3. Review the diff yourself against the checklist below. The worker report is a claim, not
   evidence.

4. Validate yourself, never through a worker: `scripts/unity-compile.sh` to zero warnings,
   `scripts/unity-tests.sh` to all green. Both Editor states run the complete suite; the script
   prints the path and totals. While iterating, run the affected tests through the filter
   argument; run the full unfiltered suite once when the stage's work is done.

5. The maintainer plays, tweaks the settings live, and judges. When the wall result misses
   the agreed design, send the rework back with `codex exec resume`. When the design itself
   fails at the wall, return to step 1 and agree the new design before a fresh brief. Fix small
   defects directly. A wall tuning loop may run through the
   coordinator: adjusting authored defaults, adding a single settings slot, and baking the
   maintainer's saved asset values back as defaults are direct edits. Structural or sizable
   implementation still goes to a worker — the coordinator does not take over development.

6. When the stage lands, update the Effect's `Repertoire` flags so they advertise honestly
   what it now handles.

## Phase E — Polish and optimize

The look and the features stay exactly as the maintainer approved them in Phases C and D. The
scope is the branch, never the Effect alone: every file the branch touched, whole files, tests
and side quests included. The coordinator does not narrow this scope.

1. Confirm that a commit contains the last wall-approved change and that the tree is clean.

2. With the `codex-worker` skill, brief one implementation worker for polish and optimization
   together. Give the worker these goals:

   - Polish every file that the branch changed.
   - Remove unnecessary runtime work and allocations from the changed hot paths.
   - Simplify the changed code where one direct design can replace avoidable complexity.
   - Preserve the approved look, features, live settings behavior, and Random behavior.

3. State the goals, acceptance criteria, and behavior-bearing boundaries. Let the worker propose
   the implementation. Add these requirements to the brief:

   - Load `polish` in addition to the standard worker skills.

   - Limit architecture analysis to branch-changed files and dependencies that the diff exposes.
     Apply YAGNI before the worker widens that scope.

   - Apply the deletion test to each structural proposal. Accept new structure only when the same
     change deletes the old form and leaves one simpler interface.

   - Use the Microsoft Learn MCP as the primary source for C# optimization claims.

   - Use static inspection and code reasoning. Do not run a profiler.

   - Keep architecture work inside the changed scope. Do not add speculative architecture.

4. Review the diff and validate it as steps 3 and 4 of the musicality loop prescribe. Run the
   full suite once, unfiltered, and record the printed path and test total. Both paths run the
   complete suite, so the Editor may stay open.

5. Ask the maintainer to judge the wall. Phase E completes only when the wall still matches the
   approved Phase D result.

## Phase F — Land and close

1. With the `commit` skill, make logical commits. Include the Unity `.meta` and `.asset` files.

2. Resolve the branch fixed point and confirm that the three-dot diff is not empty. Use the
   Standards and Spec axis definitions from `code-review`. With the `codex-worker` skill, launch
   two independent, visible Codex workers in parallel. Use the read-only research shape so each
   worker receives its own axis brief:

   - The Standards worker reads the complete diff, the repository standards, and the smell
     baseline from `code-review`.
   - The Spec worker reads the complete diff, the ticket body, and every ticket comment from
     oldest to newest.

3. Present the two reports separately. Resolve hard violations in logical follow-up commits.

   If a follow-up commit can change the evidence or conclusion for one axis, re-run only that
   axis. For changes to standards compliance or code structure, re-run Standards. For changes to
   requirements, behavior, acceptance evidence, or ticket interpretation, re-run Spec. Baked
   defaults and `.asset` value changes are tuning, never a re-run trigger.

   If a bounded correction cannot change either conclusion, review the final diff and keep both
   reports valid. Record why each existing report remains valid. A final `HEAD` can land only with
   valid reports for both axes.

4. Record the final `HEAD` and the valid reports. Merge to master by ref update —
   `git fetch . <branch>:master` —
   so no file churn hits the open Editor. Switch to master, delete the branch, and push.

5. Confirm that `master` points to the final commit, the push succeeded, and the worktree is
   clean. Close the ticket only after all three checks pass. A finding that surfaced here but
   stays unfixed lands on the epic issue, not in comments on the closed ticket.

6. Promote a durable finding to Memory Vault as a pointer to its primary source. Update this
   skill only for a maintainer decision or a process failure that repeated.

## Worker briefs

Every brief carries the `AGENTS.md` framing, the goals, the agreed design when the phase produced
one, the acceptance criteria, the vocabulary, this reading list, and these boundaries. The
vocabulary is the `CONTEXT.md` entries and this runbook's terms that the work touches.

Reading list — the worker reads all of it before touching code:

- `AGENTS.md`, `CONTEXT.md`, every ADR in `docs/adr/`, `docs/effect-authoring.md`, and
  `docs/osc-client-contract.md`.
- The Effect's own source and settings assets, and `Assets/core/Rhythm/BeatManager.cs` for the
  surface it may read.
- Load `domain-modeling`, `codebase-design`, `unity`, and `csharp` before touching code. Where a
  skill names a harness tool, the worker maps that tool to its available equivalent. The skill
  content binds.

Boundaries — in every brief:

- Every musical fact comes from the Data Surface, BeatManager's read-only face. Never read OSC
  directly, and never derive a musical fact locally.
- Trust contract-valid RaveSystem frames. `BeatManager.IsSynced` is the only mode decision. A false
  value means Standalone, where every musical group rests.
- Implement directly in this session — never delegate to another worker or agent.
- Never open or close Unity — the maintainer owns the Editor state. The `scripts/unity-*.sh`
  scripts work with the Editor open or closed, so you may run them in either state.
- Never create or edit `.meta` files — Unity owns them. `.asset` files are text: when a change
  reshapes a settings class, update its `.asset` in the same pass so the two never drift.
- A value chosen by taste is a setting, never a literal in the code. Thresholds, rates, ranges,
  weights — anything the maintainer could want to tweak at the wall — land on the settings
  surface its mode owns, Sync or Standalone, with the authored value as the default. Values the
  structure fixes, like tile counts and math constants, stay in code.
- When one consumer uses two numeric settings as endpoints for interpolation, selection, or
  randomization, they are one `FloatRange` or `IntRange`, never two scalars.
- Do not commit.
- After the final edit, run `git diff --check` exactly once. Report the result, then stop without
  another edit or check.
- Write XML docs on every touched symbol.
- Never delete or compress an authored doc comment. Carry every WHY clause — tuning pointers,
  value derivations, rationale — onto whatever replaces its symbol.
- Change the Standalone look only where the maintainer has classified that stage or finding as
  look-changing; everywhere else the look stays identical.

## Diff review checklist

- The vocabulary matches `CONTEXT.md` exactly.
- Every musical claim cites its `CONTEXT.md` entries and its `docs/osc-client-contract.md` lanes.
- Every musical read traces to the Data Surface; no OSC access, no locally computed musical
  fact.
- Settings resolution consumes no Random.
- Every taste-chosen value the diff introduces lives on a settings surface with an authored
  default; no literal in the code encodes one.
- No Sync or Standalone Setting is baked into a cache that `Init` builds once — such a cache
  makes the setting half-live under a Play Mode edit.
- The worker added no guard that did not exist before, and no defensive layer the framing
  paragraph forbids.
- No check survives whose removal leaves behavior identical. The contract carries the fact,
  never the check.
- Tests sit on agreed seams only, and no test pins an authored tuning value — an assertion that
  encodes one is at the wrong seam and is deleted, not preserved.
- No authored WHY documentation was lost or compressed.
