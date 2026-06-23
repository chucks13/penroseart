# Install the Switcher-held Loaded Cue to Armed Cue lifecycle

Status: ready-for-human

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

ADR 0005 splits ownership across three seams:

1. On-Air Timing derives Cue Sheets and current Cue Marks.
2. The Director configures one cue direction at a time for the next Cue Mark: target Cue Mark, destination Performer/Effect, and selected Transition.
3. The Mechanical Switcher owns the inserted Cue lifecycle: mutable Loaded Cue, Switcher-derived Lock Point, locked/Armed Cue, transition start, progress, completion, and Unity-time execution.

The current branch still jumps directly from `SyncedCueIntent.Evaluate(...)` to `Switcher.StartTransition(...)` inside the runway. That preserves timing today, but it does not model a Switcher-held Loaded Cue that the Director may update before lock or an Armed Cue that is immutable afterward.

## Current code findings

- `BeatManagerQueries` already exposes focused beat-clock facts (`Beat`, `TotalBeats`, `Bpm`, `BeatFraction`) over the raw Rave/OSC transport fields.
- `OnAirTimingInput.From(BeatManager)` is the existing adapter from `BeatManager` queries into timing input. It is already documented as capturing nullable rhythm queries without exposing raw Track Phase interpretation to callers.
- `OnAirTiming.ReadFrame(...)` already owns active/upcoming/unavailable Track Phase handling, Coast/Re-anchor, substantial Beat Rewind detection, pass-local cue/cadence cleanup, Cue Sheet promotion, and Cue Mark selection.
- `TimingFrame` already carries the Director-facing timing result: `CurrentBeat`, `CueMarkBeat`, `BeatsUntilCueMark`, `TimingFrameSource`, optional `PhraseWindow`, and corrected `PassLocalTimingState`.
- `Director.TickSyncedMode(...)` already calls `RefreshTimingFrame()` before cue work; `RefreshTimingFrame()` already calls `OnAirTimingInput.From(controller.beatManager)` and `onAirTiming.ReadFrame(...)`.
- `Director.TryStartSyncedCue(TimingFrame)` evaluates cue intent every synced tick from the current `TimingFrame`, staged Effect, staged Transition, Drop data, Effect deck, and Repertoire. This is the code path to evolve; do not build a parallel phrase/timing reader.
- `SyncedCueIntent.Evaluate(...)` builds a `TransitionBeatPlan` from the selected Cue Mark and selected Transition's `TransitionRepertoire`.
- Important: today's `SyncedCueIntent.Evaluate(...)` is a "cue now?" gate. It returns `Wait` before the current beat enters the Transition Runway/Tail cue window, and Drop-aware casting can mutate the Effect deck through `EffectDeckSelection.TryPullPreferred(...)`. Issue 07 should reuse these rules and inputs, but it must not blindly poll the current method earlier if that would rotate the deck repeatedly or keep the Loaded Cue empty until the old immediate-start window.
- `TransitionBeatPlan.FromCueMark(...)` already has the beat-domain Runway/Tail math the Switcher can reuse internally: `StartBeat = ImpactBeat - RunwayBeats`, `ImpactBeat = Cue Mark`, `CompleteBeat = ImpactBeat + TailBeats`.
- `Director.StartSyncedTransition(...)` immediately calls `switcher.StartTransition(...)`, marks cadence at the impact beat, consumes the cue beat, and stages the following move.
- The current shallow seam also makes `Director.StartSyncedTransition(...)` compute `secondsPerBeat`, beat fraction, elapsed beats, and Unity `startTime`, then pass `TransitionStartTiming.FromBeatClock(...)` to the Switcher. Issue 07 must move that execution timing behind the Switcher cue-lifecycle seam without moving `BeatManager`, OSC, Track Phase, Phrase Window, or Cue Sheet reading into the Switcher.
- `Switcher.StartTransition(...)` mutates transition A/B and starts rendering immediately; `Switcher.RenderAtTime(...)` then progresses by stored start time/duration. There is no inserted Loaded Cue, no Switcher-derived lock, and no single Armed Cue lifecycle.
- `SwitcherExecutionTests` already cover mechanical progress and zero-duration hard cuts; `OnAirTimingTests`, `SyncedCueIntentTests`, and `DirectorSyncedTailTests` already cover timing-frame, cue-intent, tailed-transition scheduling, and next-target behavior.

## What to build

Implement the core Cue lifecycle as one vertical slice through the Director and Mechanical Switcher.

The Director should use the existing `TimingFrame` inputs and `SyncedCueIntent` cue/casting rules to configure one beat-domain cue direction when the Cue Mark enters the selected Transition's cue window, then send that cue to the Switcher and move on. The Switcher is fire-and-forget: it stores/schedules the cue, refuses conflicting changes after its internal lock point, starts from the scheduled Runway start, and completes execution without reporting lifecycle state back to the Director.

Do not keep a Director-owned `LoadedCue` that computes Lock Point or calls `ArmCue`. Do not add a multi-cue Switcher queue. Do not make the Switcher read `BeatManager`, raw OSC/Rave fields, Track Phase, Phrase Window, or Cue Sheet state. Do not make the Director poll or react to Switcher Loaded/Locked/Started status. This issue is about one command handoff: Director-authored direction from the existing timing/casting path at the cue window, then Switcher-owned scheduling and execution.

## Implementation guidance

- Reuse the existing timing stack. `BeatManagerQueries` exposes beat-clock facts, `OnAirTimingInput.From(...)` adapts them, `OnAirTiming.ReadFrame(...)` owns phrase/phase/Cue Sheet/Cue Mark state, and `TimingFrame` is the Director-facing timing result.
- Evolve the current `Director.TryStartSyncedCue(TimingFrame)` / `SyncedCueIntent` rule path instead of adding a second cue planner or a Switcher-side timing reader. Keep `SyncedCueIntent.Evaluate(...)` as the cue-window gate so the Director sends a cue only when it is time to commit that cue command.
- Add or evolve one small cue direction value for the Director/Switcher seam only if the existing values cannot carry the caller-visible contract cleanly. It should record at least: target Cue Mark/impact beat from `TimingFrame.CueMarkBeat`, destination Performer/Effect index, selected Transition index, and selected Transition Repertoire. Prefer a local value beside the existing Director/Switcher seam over a new module unless that module hides real behavior and prevents duplicated rules.
- The Director owns configuring that cue direction from the current `TimingFrame`, staged/manual choices, Repertoire, Drop preference, deck state, and current wall state.
- If the Switcher needs current clock facts to decide lock/start/progress, pass a tiny plain clock snapshot from the existing caller context. It may contain current beat, beat fraction, seconds-per-beat/BPM, and Unity now time; it must not contain `BeatManager`, raw OSC/Rave payloads, Track Phase, Phrase Window, Cue Sheet, or other musical-structure state.
- The Director sends the cue direction to the Switcher and does not inspect whether the Switcher loaded, locked, started, or rejected it.
- The Switcher owns the Loaded Cue state. It may replace an unlocked pending cue or ignore conflicting updates after its internal lock point, but that decision is fire-and-forget from the Director's perspective.
- The Switcher derives Lock Point from the selected Transition's Runway. Use the ADR/report meaning: the Switcher gets one committed beat before it must start the Transition. In beat terms, `StartBeat = CueMarkBeat - RunwayBeats`; the Lock Point is the beat before that start (`StartBeat - 1`).
- For zero-Runway hard cuts, `StartBeat == CueMarkBeat`; the Switcher should still lock before the impact beat when possible and execute on the Cue Mark.
- If Unity/OSC updates skip over the exact Lock Point or start beat, the Switcher should lock/start on the first tick that proves the cue is due and preserve the existing beat-clock backdating behavior so the Transition still lands on the Cue Mark when possible.
- The Director should not poll Switcher mutability. It sends the cue selected from the current cue-window facts; later cue-window commands are separate musical decisions.
- After the Switcher internally locks a pending cue, later conflicting updates must not change that cue's Cue Mark, destination Performer, Transition, Runway, or Tail.
- The Switcher remains execution-only: no `BeatManager` dependency, no raw OSC/Rave reads, no Track Phase reads, no Phrase Window/Cue Sheet reads, no Cue Mark choice, no Performer casting, no Transition choice.
- The Synced Mode cue handoff must stay beat-domain. Do not pass `TransitionStartTiming`, precomputed Unity start times, duration seconds, or transition progress from the Director to the Switcher as the cue interface.
- Preserve the existing Runway/Tail behavior by reusing `TransitionBeatPlan.FromCueMark(...)`; do not reimplement equivalent beat math in the Director, Switcher callers, or tests.
- Preserve existing cadence and deck-selection behavior by reusing `ChangeCadence` and `EffectDeckSelection`; do not create parallel cadence arithmetic or preferred-Performer rotation rules.
- Preserve the existing Runway/Tail behavior, but move Unity-time start/duration/progress math behind the Switcher-held cue seam.

## Acceptance criteria

- [x] The Director configures one cue direction for the current Cue Mark from the existing `TimingFrame`/`SyncedCueIntent` path and sends it to the Switcher fire-and-forget.
- [x] The Switcher holds/schedules the Loaded Cue internally without requiring the Director to inspect its status.
- [x] The Director sends cue-window commands fire-and-forget; it does not update a Switcher cue based on Switcher mutability.
- [x] The Lock Point is derived inside the Switcher from the selected Transition's Runway, not from a Director-owned/global rule.
- [x] At or after Lock Point, the Switcher locks the cue and refuses mutations to Cue Mark, destination Performer, Transition, Runway, or Tail.
- [x] The Switcher owns exactly one current cue lifecycle and uses Runway/Tail so the Transition Impact Point lands on the Cue Mark.
- [x] The Synced Mode cue path does not pass `TransitionStartTiming`, precomputed Unity start times, duration seconds, or transition progress from the Director to the Switcher as the cue interface.
- [x] The Switcher does not depend on `BeatManager`, raw OSC/Rave payloads, Track Phase, Phrase Window, or Cue Sheet state.
- [x] The Switcher does not choose Cue Sheets, Cue Marks, Performers, or Transitions.
- [x] Hard cuts with zero Runway/Tail still lock and execute correctly.
- [x] Existing Drop-aware casting, manual staged choices, Hold behavior, cadence blocking, and staged Next Effect/Next Transition behavior determine the cue-window command the Director sends.
- [x] The cue-planning path mutates the deck only when the cue-window command is actually sent, so repeated waiting ticks cannot rotate the deck accidentally.
- [x] No duplicate implementations of Cue Sheet cursoring, `ChangeCadence`, `TransitionBeatPlan` Runway/Tail math, or `EffectDeckSelection` preferred casting are introduced.
- [x] Focused tests cover fire-and-forget Director cue commands, Switcher-owned scheduling, ignored locked updates, zero-Runway hard cut, missed-exact-start backdating, and one complete Switcher-owned execution path.

## Test guidance

Add tests at the real seams, not against private fields:

- On-Air Timing seam: keep using `OnAirTimingTests` for phrase/phase/Cue Sheet/Cue Mark behavior; do not re-test phrase selection through Switcher tests.
- Cue-planning seam: keep using/evolving `SyncedCueIntentTests` for cue-window eligibility, cadence, Drop-aware casting, and target selection from a `TimingFrame`.
- Director/Switcher seam: when the cue window arrives, the Director sends one cue command, records its own chosen Cue Mark for pass-local timing/cadence, and stages the following move without reading Switcher status.
- Switcher seam: one beat-domain Loaded Cue schedules, locks, starts, progresses, and completes using the selected Transition's Runway/Tail and a tiny plain clock snapshot; the test should fail if the cue interface requires a Director-computed Unity start time or a `BeatManager`/OSC/Track Phase dependency in the Switcher.
- Hard-cut path: zero Runway/Tail locks and promotes the destination immediately on its Cue Mark.
- Regression path: existing `SyncedCueDoesNotRestartSameMandatoryBoundaryInsideRunway` behavior remains true under the new lifecycle.

## Implementation notes

- Added `SwitcherCueDirection`, `SwitcherClockSnapshot`, and `SwitcherCueStatus` in `Assets/core/Switcher.cs` for the one-cue Switcher-held lifecycle.
- `Switcher.UpsertLoadedCue(...)` is fire-and-forget: it accepts a beat-domain cue command, schedules the Runway start from `TransitionBeatPlan.FromCueMark(...)` and the supplied clock snapshot, ignores conflicting updates after its internal lock point, and starts due cues from `RenderAtTime(...)` without a Director-computed `TransitionStartTiming` crossing the cue seam.
- `Director.TryStartSyncedCue(...)` now uses `SyncedCueIntent.Evaluate(...)` as the cue-window gate, sends the cue command, records its own chosen Cue Mark for pass-local timing/cadence, and stages the following move without reading Switcher lifecycle state; the old `Director.StartSyncedTransition(...)` path was removed.
- Removed the stale pre-lock cue planner/result surface (`SwitcherCueUpdateResult`, public `AdvanceLoadedCue(...)`, `SyncedCueIntent.EvaluateLoadedCue(...)`, and `EffectDeckSelection.TryPeekPreferred(...)`).

## Validation evidence

- `./scripts/unity-compile.sh` passed with C# warning count 0.
- `UNITY_TEST_FILTER='DirectorSyncedTailTests|SwitcherExecutionTests|SyncedCueIntentTests|OnAirTimingTests|ChangeCadenceTests|EffectDeckSelectionTests' ./scripts/unity-tests.sh` passed 65/65.
- Full `./scripts/unity-tests.sh` passed 217/217.
- `git diff --check` passed.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='DirectorSyncedTailTests|SyncedCueIntentTests|SwitcherExecutionTests|DirectorStagingTests|TransitionBeatPlanTests|EffectDeckSelectionTests' ./scripts/unity-tests.sh`
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/06-make-cue-sheet-on-air-timing-plan.md`
