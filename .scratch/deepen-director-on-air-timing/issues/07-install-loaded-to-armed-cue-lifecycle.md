# Install the Loaded Cue to Armed Cue lifecycle

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

ADR 0005 splits ownership across three seams:

1. On-Air Timing derives Cue Sheets and current Cue Marks.
2. The Director prepares one mutable **Loaded Cue** for the next Cue Mark.
3. The Mechanical Switcher executes one committed **Armed Cue**.

The current branch still jumps directly from `SyncedCueIntent.Evaluate(...)` to `Switcher.StartTransition(...)` inside the runway. That preserves timing today, but it does not model a Loaded Cue that can change before a Lock Point or an Armed Cue that is immutable afterward.

## Current code findings

- `Director.TryStartSyncedCue(TimingFrame)` evaluates cue intent every synced tick from the current `TimingFrame`, staged Effect, staged Transition, Drop data, Effect deck, and Repertoire.
- `SyncedCueIntent.Evaluate(...)` builds a `TransitionBeatPlan` from the selected boundary/Cue Mark and the selected Transition's `TransitionRepertoire`.
- `TransitionBeatPlan.FromSelectedPhaseBoundary(...)` already has the right math to preserve: `StartBeat = ImpactBeat - RunwayBeats`, `ImpactBeat = Cue Mark`, `CompleteBeat = ImpactBeat + TailBeats`.
- `Director.StartSyncedTransition(...)` immediately calls `switcher.StartTransition(...)`, marks cadence at the impact beat, consumes the cue beat, and stages the following move.
- `Switcher.StartTransition(...)` mutates transition A/B and starts rendering immediately; `Switcher.RenderAtTime(...)` then progresses by stored start time/duration. There is no arm-vs-start distinction and no single Armed Cue state.
- `SwitcherExecutionTests` already cover mechanical progress and zero-duration hard cuts; `DirectorSyncedTailTests` already cover tailed-transition scheduling and next-target behavior.

## What to build

Implement the core Cue lifecycle as one vertical slice through the Director and Mechanical Switcher.

The Director should hold at most one mutable **Loaded Cue** for the next Cue Mark. It chooses the destination Performer and Transition from the current wall state, staged/manual choices, Repertoire, and live musical events. When that Loaded Cue reaches its Transition-specific **Lock Point**, it becomes one **Armed Cue** owned by the Switcher.

Do not add a multi-cue Switcher queue. This issue is about one complete handoff pattern: mutable Director intent before Lock Point, immutable Switcher execution after the Cue is armed.

## Implementation guidance

- A Loaded Cue should record at least: target Cue Mark/impact beat, destination Performer/Effect index, Transition index, Transition Repertoire, `TransitionBeatPlan`, and Lock Point.
- Lock Point is derived from the selected Transition's Runway. Use the ADR/report meaning: the Switcher gets one committed beat before it must start the Transition. In beat terms, `StartBeat = CueMarkBeat - RunwayBeats`; the Lock Point is the beat before that start (`StartBeat - 1`).
- For zero-Runway hard cuts, `StartBeat == CueMarkBeat`; the cue should still arm before the impact beat and execute on the Cue Mark.
- If Unity/OSC skips over the exact Lock Point, arm on the first tick at or after the Lock Point and preserve the existing beat-clock backdating behavior so the Transition still lands on the Cue Mark when possible.
- Before Lock Point, replacing the Loaded Cue is allowed when the Cue Mark, selected Transition, selected Performer, Drop preference, staged manual choice, or Hold state changes.
- At or after Lock Point, the Armed Cue must not change target Cue Mark, destination Performer, Transition, Runway, or Tail even if Track Phase changes before start/impact.
- The Switcher may gain an `ArmCue`/`ArmedCue` style interface, but it must remain execution-only: no Track Phase reads, no Cue Sheet choice, no Performer casting, no Transition choice.
- Preserve the existing `TransitionStartTiming`/Runway/Tail execution math unless the new Armed Cue interface cleanly absorbs it.

## Acceptance criteria

- [ ] The Director holds at most one Loaded Cue for the next Cue Mark.
- [ ] The Loaded Cue records the target Cue Mark/impact beat, destination Performer, selected Transition, selected Transition Repertoire, `TransitionBeatPlan`, and Lock Point.
- [ ] Before Lock Point, the Loaded Cue may be replaced when timing or casting inputs change.
- [ ] The Lock Point is derived from the selected Transition's Runway, not from a global one-size-fits-all rule.
- [ ] At or after Lock Point, the Loaded Cue becomes an Armed Cue and cannot change target Cue Mark, destination Performer, Transition, Runway, or Tail.
- [ ] The Switcher owns exactly one Armed Cue execution state and uses Runway/Tail so the Transition Impact Point lands on the Cue Mark.
- [ ] The Switcher does not choose Cue Sheets, Cue Marks, Performers, or Transitions, and it does not read Track Phase/Phrase data.
- [ ] Hard cuts with zero Runway/Tail still arm and execute correctly.
- [ ] Existing Drop-aware casting, manual staged choices, Hold behavior, cadence blocking, and staged Next Effect/Next Transition behavior still work for the Loaded Cue before Lock Point.
- [ ] Focused tests cover mutable-before-lock, immutable-after-lock, zero-Runway hard cut, missed-exact-lock tick, and one complete armed execution path.

## Test guidance

Add tests at the real seams, not against private fields:

- Director seam: a Loaded Cue changes before Lock Point when a same/current Cue Mark gets a new staged choice or preferred Drop-capable Performer.
- Director/Switcher seam: once Lock Point is reached, later Track Phase or staged-choice changes do not alter the Armed Cue handed to Switcher.
- Switcher seam: one Armed Cue starts/progresses/completes using the selected Transition's Runway/Tail and has no queue of future cues.
- Hard-cut path: zero Runway/Tail arms and promotes the destination immediately on its Cue Mark.
- Regression path: existing `SyncedCueDoesNotRestartSameMandatoryBoundaryInsideRunway` behavior remains true under the new lifecycle.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='DirectorSyncedTailTests|SyncedCueIntentTests|SwitcherExecutionTests|DirectorStagingTests|TransitionBeatPlanTests|EffectDeckSelectionTests' ./scripts/unity-tests.sh`
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/06-make-cue-sheet-on-air-timing-plan.md`
