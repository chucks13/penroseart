# Install the Switcher-held Loaded Cue to Armed Cue lifecycle

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

ADR 0005 splits ownership across three seams:

1. On-Air Timing derives Cue Sheets and current Cue Marks.
2. The Director configures one cue direction at a time for the next Cue Mark: target Cue Mark, destination Performer/Effect, and selected Transition.
3. The Mechanical Switcher owns the inserted Cue lifecycle: mutable Loaded Cue, Switcher-derived Lock Point, locked/Armed Cue, transition start, progress, completion, and Unity-time execution.

The current branch still jumps directly from `SyncedCueIntent.Evaluate(...)` to `Switcher.StartTransition(...)` inside the runway. That preserves timing today, but it does not model a Switcher-held Loaded Cue that the Director may update before lock or an Armed Cue that is immutable afterward.

## Current code findings

- `Director.TryStartSyncedCue(TimingFrame)` evaluates cue intent every synced tick from the current `TimingFrame`, staged Effect, staged Transition, Drop data, Effect deck, and Repertoire.
- `SyncedCueIntent.Evaluate(...)` builds a `TransitionBeatPlan` from the selected boundary/Cue Mark and the selected Transition's `TransitionRepertoire`.
- `TransitionBeatPlan.FromCueMark(...)` already has the beat-domain math the Switcher can reuse internally: `StartBeat = ImpactBeat - RunwayBeats`, `ImpactBeat = Cue Mark`, `CompleteBeat = ImpactBeat + TailBeats`.
- `Director.StartSyncedTransition(...)` immediately calls `switcher.StartTransition(...)`, marks cadence at the impact beat, consumes the cue beat, and stages the following move.
- The current shallow seam also makes `Director.StartSyncedTransition(...)` compute `secondsPerBeat`, beat fraction, elapsed beats, and Unity `startTime`, then pass `TransitionStartTiming.FromBeatClock(...)` to the Switcher. Issue 07 must move that execution timing behind the Switcher seam.
- `Switcher.StartTransition(...)` mutates transition A/B and starts rendering immediately; `Switcher.RenderAtTime(...)` then progresses by stored start time/duration. There is no inserted Loaded Cue, no Switcher-derived lock, and no single Armed Cue lifecycle.
- `SwitcherExecutionTests` already cover mechanical progress and zero-duration hard cuts; `DirectorSyncedTailTests` already cover tailed-transition scheduling and next-target behavior.

## What to build

Implement the core Cue lifecycle as one vertical slice through the Director and Mechanical Switcher.

The Director should configure a beat-domain cue direction for the next Cue Mark and insert/update that cue in the Switcher. The Switcher should hold that cue as its mutable Loaded Cue until the selected Transition's Runway says it is time to lock. Once locked, the Switcher must refuse further changes to that cue and execute the resulting Armed Cue.

Do not keep a Director-owned `LoadedCue` that computes Lock Point or calls `ArmCue`. Do not add a multi-cue Switcher queue. This issue is about one Switcher-held cue slot and one complete handoff pattern: Director-authored direction before lock, Switcher-owned immutability and execution after lock.

## Implementation guidance

- Add a small beat-domain cue direction value for the Director/Switcher seam. It should record at least: target Cue Mark/impact beat, destination Performer/Effect index, selected Transition index, and selected Transition Repertoire.
- The Director owns configuring that cue direction from the current Cue Mark, staged/manual choices, Repertoire, Drop preference, deck state, and current wall state.
- The Director inserts or updates the cue direction in the Switcher while the Switcher reports that the current Loaded Cue is still mutable.
- The Switcher owns the Loaded Cue state. It should decide whether an update is accepted/replaced or rejected because the current cue is already locked.
- The Switcher derives Lock Point from the selected Transition's Runway. Use the ADR/report meaning: the Switcher gets one committed beat before it must start the Transition. In beat terms, `StartBeat = CueMarkBeat - RunwayBeats`; the Lock Point is the beat before that start (`StartBeat - 1`).
- For zero-Runway hard cuts, `StartBeat == CueMarkBeat`; the Switcher should still lock before the impact beat when possible and execute on the Cue Mark.
- If Unity/OSC skips over the exact Lock Point or start beat, the Switcher should lock/start on the first tick that proves the cue is due and preserve the existing beat-clock backdating behavior so the Transition still lands on the Cue Mark when possible.
- Before the Switcher locks, replacing the Loaded Cue is allowed when the Cue Mark, selected Transition, selected Performer, Drop preference, staged manual choice, or Hold state changes.
- After the Switcher locks, later Director updates must not change the locked cue's Cue Mark, destination Performer, Transition, Runway, or Tail.
- The Switcher remains execution-only: no Track Phase reads, no Cue Sheet choice, no Performer casting, no Transition choice.
- The Synced Mode cue handoff must stay beat-domain. Do not pass `TransitionStartTiming`, Unity start times, duration seconds, or transition progress from the Director to the Switcher.
- Preserve the existing Runway/Tail behavior, but move Unity-time start/duration/progress math behind the Switcher-held cue seam.

## Acceptance criteria

- [ ] The Director configures one cue direction for the next Cue Mark and inserts/updates it in the Switcher.
- [ ] The Switcher holds the mutable Loaded Cue and exposes whether the current cue can still be updated.
- [ ] Before Switcher lock, Director updates can replace the Loaded Cue when timing or casting inputs change.
- [ ] The Lock Point is derived inside the Switcher from the selected Transition's Runway, not from a Director-owned/global rule.
- [ ] At or after Lock Point, the Switcher locks the cue and refuses mutations to Cue Mark, destination Performer, Transition, Runway, or Tail.
- [ ] The Switcher owns exactly one current cue lifecycle and uses Runway/Tail so the Transition Impact Point lands on the Cue Mark.
- [ ] The Synced Mode cue path does not pass `TransitionStartTiming`, Unity start times, duration seconds, or transition progress from the Director to the Switcher.
- [ ] The Switcher does not choose Cue Sheets, Cue Marks, Performers, or Transitions, and it does not read Track Phase/Phrase data.
- [ ] Hard cuts with zero Runway/Tail still lock and execute correctly.
- [ ] Existing Drop-aware casting, manual staged choices, Hold behavior, cadence blocking, and staged Next Effect/Next Transition behavior still determine the mutable cue direction before Switcher lock.
- [ ] Focused tests cover mutable-before-lock, rejected-after-lock update, zero-Runway hard cut, missed-exact-lock/start tick, and one complete Switcher-owned execution path.

## Test guidance

Add tests at the real seams, not against private fields:

- Director/Switcher seam before lock: a same/current Cue Mark gets a new staged choice or preferred Drop-capable Performer, the Director updates the Switcher-held Loaded Cue, and the Switcher accepts the replacement because it is still unlocked.
- Director/Switcher seam after lock: changed Track Phase, changed staged Effect, or changed Drop data causes the Director to offer an update, but the Switcher refuses it because the cue is locked.
- Switcher seam: one beat-domain Loaded Cue locks, starts, progresses, and completes using the selected Transition's Runway/Tail; the test should fail if the cue interface requires a Director-computed Unity start time.
- Hard-cut path: zero Runway/Tail locks and promotes the destination immediately on its Cue Mark.
- Regression path: existing `SyncedCueDoesNotRestartSameMandatoryBoundaryInsideRunway` behavior remains true under the new lifecycle.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='DirectorSyncedTailTests|SyncedCueIntentTests|SwitcherExecutionTests|DirectorStagingTests|TransitionBeatPlanTests|EffectDeckSelectionTests' ./scripts/unity-tests.sh`
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/06-make-cue-sheet-on-air-timing-plan.md`
