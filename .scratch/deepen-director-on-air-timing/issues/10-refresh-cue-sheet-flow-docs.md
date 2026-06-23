# Refresh docs and status for the Cue Sheet flow

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

After Issues 06-09 land, the code will intentionally change the canonical sequencing vocabulary from Selected Phase Boundary planning to Cue Sheet / Cue Mark / Switcher-held Loaded Cue / Armed Cue. The durable docs must match the implemented seams so future agents do not revive the old model, put Loaded Cue state back in the Director, or add a Switcher preload queue.

This is a documentation/status slice only. It should remove stale implementation wording instead of adding new paragraphs beside contradictions.

## Current documentation findings

- `CONTEXT.md` defines Cue Sheet, Cue Mark, Loaded Cue, Armed Cue, Lock Point, On-Air Timing, Timing Frame, Director, Mechanical Switcher, Runway, Tail, and Impact Point, but some wording may still imply Director-held Loaded Cue or Director-owned arming. Refresh it only after the final implementation proves the exact code shape.
- `docs/adr/0005-phrase-cue-sheets-and-armed-cues.md` records the durable decision and rejected alternatives: no canonical Selected Phase Boundary wording, no multi-cue Switcher preload queue, no Director-held Loaded Cue/Director-called ArmCue lifecycle, and no Effect/Transition choices inside Cue Sheets.
- `docs/architecture-reviews/director-cue-sheet-investigation-2026-06-22.html` is a historical investigation/report. Keep it consistent enough to avoid obvious contradictions, but do not copy it wholesale into canonical docs.
- `docs/runtime-architecture.md` and `docs/code-map.md` were refreshed for the prior Timing Frame / Cue Intent shape and will need another pass after the implemented Cue Sheet lifecycle changes.
- Local issue tracker status should use the repo labels from `docs/agents/triage-labels.md`; implementation issues become `accepted` only when their checkboxes and validation evidence are present.

## What to build

Refresh the durable documentation and local issue tracker after the implemented Cue Sheet flow lands.

The final docs should agree with the code, tests, glossary, ADR 0005, and investigation report: On-Air Timing derives Cue Sheets; Cue Marks are Phrase-level impact targets; the Director configures one cue direction at a time and inserts/updates it in the Switcher; the Switcher holds the mutable Loaded Cue, derives the Transition-specific Lock Point, refuses updates after lock, and owns Armed Cue execution.

## Documentation targets

Update these if the implementation changed the relevant behavior:

- `docs/runtime-architecture.md` - current runtime flow and module/seam responsibilities.
- `docs/code-map.md` - source-file orientation for Cue Sheet, cue direction, Switcher-held Loaded Cue, Armed Cue execution, and tests.
- `CONTEXT.md` - glossary alignment only; keep it implementation-free.
- `docs/adr/0005-phrase-cue-sheets-and-armed-cues.md` - only if implementation intentionally changes the recorded decision. If the decision changes materially, create/update an ADR using repo ADR conventions instead of burying the decision in runtime docs.
- `.scratch/deepen-director-on-air-timing/PRD.md` and issues 06-09 - status, checkboxes, implementation notes, and validation evidence.

## Acceptance criteria

- [ ] Runtime architecture docs describe Cue Sheet, Cue Mark, Director-authored cue direction, Switcher-held Loaded Cue, Switcher-derived Lock Point, Armed Cue, and Switcher execution responsibilities as implemented.
- [ ] Code map / orientation docs point maintainers to the final Cue Sheet / cue direction / Switcher lifecycle seams and the focused tests that protect them.
- [ ] `CONTEXT.md` remains aligned with implemented behavior and does not keep stale Selected Phase Boundary language as the canonical domain term or stale Director-owned Loaded Cue/arming wording.
- [ ] ADR 0005 remains accurate, or a deliberate ADR update/new ADR records any changed decision.
- [ ] The investigation report is referenced or left historically consistent where useful; no historical report content is copied wholesale into canonical docs.
- [ ] Issues 06 through 09 are marked accepted only when their checkboxes, implementation notes, and validation evidence are present.
- [ ] The PRD status and notes reflect the final implementation/validation state.
- [ ] A lightweight stale-wording search finds no active-doc contradictions about Director-owned transition start/progress mechanics, Director-owned Lock Point/arming, Selected Phase Boundary as canonical domain language, or Switcher preload queues.
- [ ] No Play Mode validation is required for this docs/status slice.

## Stale-wording search guidance

Search active docs (`CONTEXT.md`, `docs/runtime-architecture.md`, `docs/code-map.md`, `AGENTS.md`, and the PRD/issues) for contradictions such as:

- `Selected Phase Boundary` used as the canonical domain name instead of historical/current-implementation context.
- `preload queue`, `queue`, or `Switcher chooses` wording that implies multiple future cues or Switcher-owned musical decisions.
- `Director starts/progresses transitions`, `Controller timer transition loop`, or similar old execution ownership language.
- Director-owned `Loaded Cue`, `Lock Point`, `ArmCue`, or arming wording that says the Director decides when a cue locks.
- `Impact Point` confused with Cue Mark, Transition start, or Transition completion.
- `Lock Point` described as global rather than Transition-specific.

Do not blanket-delete legitimate historical mentions in the HTML investigation report or ADR considered-options sections; make active guidance clear.

## Suggested validation

- `git diff --check`
- `./scripts/unity-compile.sh` only if docs comments/XML docs or code comments changed in compiled files.
- Focused docs/status check, for example:
  - `rg -n "Controller\.OnTimerFinished|SyncedCueDecision|preload queue|Selected Phase Boundary|Director.*Lock Point|Director.*Arm|ArmCue|TransitionStartTiming|Loaded Cue|Armed Cue" CONTEXT.md docs/runtime-architecture.md docs/code-map.md AGENTS.md .scratch/deepen-director-on-air-timing/PRD.md .scratch/deepen-director-on-air-timing/issues`
- No Play Mode run is required.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/06-make-cue-sheet-on-air-timing-plan.md`
- `.scratch/deepen-director-on-air-timing/issues/07-install-loaded-to-armed-cue-lifecycle.md`
- `.scratch/deepen-director-on-air-timing/issues/08-advance-through-cue-sheet-one-cue-at-a-time.md`
- `.scratch/deepen-director-on-air-timing/issues/09-preserve-live-change-behavior-around-cue-sheets.md`
