# Refresh docs and status for the Cue Sheet flow

Status: ready-for-human

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

After Issues 06-09 land, the code will intentionally change the canonical sequencing vocabulary from Selected Phase Boundary planning to Cue Sheet / Cue Mark / Switcher-held Loaded Cue / Armed Cue. The durable docs must match the implemented seams so future agents do not revive the old model, put Loaded Cue state back in the Director, or add a Switcher preload queue.

This is a documentation/status slice only. It should remove stale implementation wording instead of adding new paragraphs beside contradictions.

## Current documentation findings

- `CONTEXT.md` defines Cue Sheet, Cue Mark, Loaded Cue, Armed Cue, Lock Point, On-Air Timing, Timing Frame, Director, Mechanical Switcher, Runway, Tail, and Impact Point. It needed a final alignment pass so Loaded Cue state is Switcher-held and the Director cue handoff is fire-and-forget.
- `docs/adr/0005-phrase-cue-sheets-and-armed-cues.md` records the durable decision and rejected alternatives: no canonical Selected Phase Boundary wording, no multi-cue Switcher preload queue, no Director-held Loaded Cue/Director-called ArmCue lifecycle, and no Effect/Transition choices inside Cue Sheets.
- Issue 07 should also be checked for implementation drift that puts `BeatManager`, raw OSC/Rave payloads, Track Phase, Phrase Window, or Cue Sheet state behind the Switcher seam. Those belong to existing `BeatManagerQueries`, `OnAirTimingInput`, `OnAirTiming`, `TimingFrame`, and Director/cue-planning code.
- Issue 07 should also be checked for duplicated rules: no second Cue Sheet cursor, pass-local consumed-cue ledger, cadence arithmetic, Runway/Tail beat math, or Effect deck/preferred-casting implementation should appear outside the current seams unless a deliberate refactor replaces the old seam.
- `docs/architecture-reviews/director-cue-sheet-investigation-2026-06-22.html` is a historical investigation/report. Keep it consistent enough to avoid obvious contradictions, but do not copy it wholesale into canonical docs.
- `docs/runtime-architecture.md` and `docs/code-map.md` were refreshed for the prior Timing Frame / Cue Intent shape and will need another pass after the implemented Cue Sheet lifecycle changes.
- Local issue tracker status should use the repo triage labels from `docs/agents/triage-labels.md`; completed slices use `ready-for-human` for maintainer review, while checkboxes and validation evidence record implementation completion.

## What to build

Refresh the durable documentation and local issue tracker after the implemented Cue Sheet flow lands.

The final docs should agree with the code, tests, glossary, ADR 0005, and investigation report: On-Air Timing derives Cue Sheets; Cue Marks are Phrase-level impact targets; the Director configures one cue direction at a time from the existing timing/casting seams and sends it to the Switcher fire-and-forget at the cue window; the Switcher holds/schedules the Loaded Cue, derives the Transition-specific Lock Point, refuses conflicting updates after lock, and owns Armed Cue execution.

## Documentation targets

Update these if the implementation changed the relevant behavior:

- `docs/runtime-architecture.md` - current runtime flow and module/seam responsibilities.
- `docs/code-map.md` - source-file orientation for Cue Sheet, cue direction, Switcher-held Loaded Cue, Armed Cue execution, and tests.
- `CONTEXT.md` - glossary alignment only; keep it implementation-free.
- `docs/adr/0005-phrase-cue-sheets-and-armed-cues.md` - only if implementation intentionally changes the recorded decision. If the decision changes materially, create/update an ADR using repo ADR conventions instead of burying the decision in runtime docs.
- `.scratch/deepen-director-on-air-timing/PRD.md` and issues 01-09 - triage status labels, implementation notes, and validation evidence.

## Acceptance criteria

- [x] Runtime architecture docs describe Cue Sheet, Cue Mark, Director-authored cue direction, Switcher-held Loaded Cue, Switcher-derived Lock Point, Armed Cue, and Switcher execution responsibilities as implemented.
- [x] Runtime architecture docs make clear that `BeatManagerQueries`/`OnAirTimingInput`/`OnAirTiming`/`TimingFrame` remain the timing/phrase seams; the Switcher does not read raw OSC/Rave, Track Phase, Phrase Window, or Cue Sheet state.
- [x] Runtime architecture docs point to the existing owners for shared rules: `CueSheet`/`OnAirTiming` for Cue Mark planning and advancement, `PassLocalTimingState` for consumed cue/cadence state, `SyncedCueIntent`/`EffectDeckSelection` for cue casting, and `TransitionBeatPlan` for Runway/Tail beat math.
- [x] Code map / orientation docs point maintainers to the final Cue Sheet / cue direction / Switcher lifecycle seams and the focused tests that protect them.
- [x] `CONTEXT.md` remains aligned with implemented behavior and does not keep stale Selected Phase Boundary language as the canonical domain term or stale Director-owned Loaded Cue/arming wording.
- [x] ADR 0005 remains accurate after a dated amendment for the fire-and-forget cue handoff refinement.
- [x] The investigation report is left historical; no historical report content was copied wholesale into canonical docs.
- [x] Issues 01 through 09 now use repo triage status labels; implementation completion remains captured by checked acceptance criteria, implementation notes, and validation evidence.
- [x] The PRD status and notes reflect the final implementation/validation state.
- [x] A lightweight stale-wording search found no active-doc contradictions about Director-owned transition start/progress mechanics, Director-owned Lock Point/arming, Selected Phase Boundary as canonical domain language, or Switcher preload queues after filtering legitimate historical/negative mentions.
- [x] No Play Mode validation is required for this docs/status slice.

## Stale-wording search guidance

Search active docs (`CONTEXT.md`, `docs/runtime-architecture.md`, `docs/code-map.md`, `AGENTS.md`, and the PRD/issues) for contradictions such as:

- `Selected Phase Boundary` used as the canonical domain name instead of historical/current-implementation context.
- `preload queue`, `queue`, or `Switcher chooses` wording that implies multiple future cues or Switcher-owned musical decisions.
- `Director starts/progresses transitions`, `Controller timer transition loop`, or similar old execution ownership language.
- Director-owned `Loaded Cue`, `Lock Point`, `ArmCue`, or arming wording that says the Director decides when a cue locks.
- Switcher-owned `BeatManager`, raw OSC/Rave, Track Phase, Phrase Window, or Cue Sheet reads.
- Duplicate implementations of Cue Sheet cursoring, consumed cue state, cadence arithmetic, Runway/Tail beat math, or preferred Performer deck rotation.
- `Impact Point` confused with Cue Mark, Transition start, or Transition completion.
- `Lock Point` described as global rather than Transition-specific.

Do not blanket-delete legitimate historical mentions in the HTML investigation report or ADR considered-options sections; make active guidance clear.

## Implementation notes

- Refreshed `CONTEXT.md` glossary/overview so the Director sends cue directions fire-and-forget and Loaded Cue state is Switcher-held.
- Refreshed `docs/runtime-architecture.md` so Synced Mode now flows through `OnAirTiming` / `TimingFrame` / `SyncedCueIntent` / `SwitcherCueDirection` / Switcher-held Loaded Cue execution.
- Refreshed `docs/code-map.md` to point from the retired Selected Phase Boundary file name to `CueSheet.cs`, Cue Mark planning, `TransitionBeatPlan`, and Switcher Loaded Cue scheduling.
- Amended ADR 0005 on 2026-06-23 to record the fire-and-forget cue handoff refinement without changing the core Director/Switcher ownership decision.
- Updated the PRD and issue status lines to repo triage labels; completed work is `ready-for-human` for maintainer review because the tracker has no done/accepted triage label.

## Validation evidence

- `git diff --check` passed.
- Focused stale-wording search passed after filtering legitimate historical/negative mentions in issue context and glossary avoid-notes.
- No Play Mode or Unity compile run was required because this slice changed markdown docs only.

## Suggested validation

- `git diff --check`
- `./scripts/unity-compile.sh` only if docs comments/XML docs or code comments changed in compiled files.
- Focused docs/status check, for example:
  - `rg -n "Controller\.OnTimerFinished|SyncedCueDecision|preload queue|Selected Phase Boundary|Director.*Lock Point|Director.*Arm|ArmCue|TransitionStartTiming|Switcher.*BeatManager|Switcher.*OSC|Switcher.*Track Phase|Switcher.*Phrase Window|Switcher.*Cue Sheet|duplicate.*Cue Sheet|duplicate.*cadence|duplicate.*Runway|Loaded Cue|Armed Cue" CONTEXT.md docs/runtime-architecture.md docs/code-map.md AGENTS.md .scratch/deepen-director-on-air-timing/PRD.md .scratch/deepen-director-on-air-timing/issues`
- No Play Mode run is required.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/06-make-cue-sheet-on-air-timing-plan.md`
- `.scratch/deepen-director-on-air-timing/issues/07-install-loaded-to-armed-cue-lifecycle.md`
- `.scratch/deepen-director-on-air-timing/issues/08-advance-through-cue-sheet-one-cue-at-a-time.md`
- `.scratch/deepen-director-on-air-timing/issues/09-preserve-live-change-behavior-around-cue-sheets.md`
