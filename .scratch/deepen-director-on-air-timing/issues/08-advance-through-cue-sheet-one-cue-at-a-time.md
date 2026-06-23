# Advance through the Cue Sheet one Cue at a time

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

Cue Sheets are timing plans, not queues of fully chosen future moves. ADR 0005 deliberately keeps Effect and Transition choice in the Director because those choices depend on the current wall state, Repertoire, staged/manual choices, and live musical events.

After Issue 07 installs the Switcher-held Loaded Cue / Locked Armed Cue lifecycle, the next risk is accidentally precomputing every future cue on a sheet or restarting the same Cue Mark. This issue proves the lifecycle repeats correctly: one Cue Mark is selected, one cue direction is inserted/updated in the Switcher, the Switcher locks/executes it, the Cue Mark is consumed for the current pass, and then the next Cue Mark becomes the next cue direction target.

## Current code findings

- `OnAirTiming.CueSheetPlans.ResolveCurrent(...)` and its `CueSheetCursor` already advance through the current Cue Sheet, promote preplanned upcoming Cue Sheets, and skip consumed Cue Marks.
- `PassLocalTimingState` already carries `LastCueBeat` and `PreviousCueMarkBeat`; `OnAirTiming.BuildFramePassLocalState(...)` clears stale cue/cadence state after substantial Beat Rewind so a loop pass can reuse the same Cue Mark.
- Current `Director.StartSyncedTransition(...)` consumes the current Cue Mark by calling `MarkChangedOnBeat(transitionLandingBeat)` and `lastCueBeat = lastSyncedBeat`, then immediately stages following choices. Issue 08 should preserve that consumption behavior while moving the commit signal to the Switcher-held lifecycle.
- Existing regression tests already prove pieces of the desired lifecycle: `FiredMandatoryCueMarkImmediatelyPromotesPreplannedNextPhraseCueMark`, `FiredCueImmediatelyLoadsNextPreplannedPhraseBoundary`, `SyncedCueDoesNotRestartSameMandatoryBoundaryInsideRunway`, and tailed-transition next-target tests.
- `OnAirTimingTests` are the right seam for Cue Sheet advancement and loop/rewind cursor behavior. Do not duplicate Cue Sheet selection logic in the Switcher.

## What to build

Make the Director progress through the current Cue Sheet one Cue Mark at a time while the Switcher owns the current cue lifecycle.

The Director should not choose Effects and Transitions for every future Cue Mark upfront. It should configure only the next cue direction from the current wall state and current musical context, insert/update it in the Switcher, and advance after the Switcher has committed the current cue. The Switcher may still be rendering the previous Transition Tail when the Director prepares the next cue direction, because Tail is execution state and not musical scheduling evidence.

## Implementation guidance

- Keep the Cue Sheet as a list of timing marks only. Do not attach Effect indexes, Transition indexes, Drop preferences, or Repertoire decisions to every mark on the sheet.
- The Director should configure only the next cue direction, not every remaining Cue Mark on the sheet.
- The Switcher should own at most one mutable pending Loaded Cue direction at a time. If it also has a locked/executing cue, that is current execution state, not permission to build a multi-cue queue.
- Consume a Cue Mark exactly once per pass when the Switcher commits that cue by locking/arming it. The Director should learn enough from the Switcher result/status to avoid restarting the same mark inside its runway.
- Feed that consumption back through the existing pass-local language (`LastCueBeat` / `PreviousCueMarkBeat`) rather than creating a second consumed-cue ledger in the Director or Switcher.
- After consumption, call back through the existing `OnAirTiming.ReadFrame(...)` / `TimingFrame` path for the next Cue Mark from the same current Cue Sheet or from the promoted upcoming Cue Sheet.
- The mandatory final Cue Mark should consume just like an interior mark, while still allowing the preplanned upcoming sheet to promote without rerolling.
- Keep pass-local cadence state in the existing Cue Mark language (`LastCueBeat`, `PreviousCueMarkBeat`). Do not reintroduce `PreviousSelectedPhaseBoundary` or a second pass-local state model.
- Do not block configuring/inserting the next cue direction merely because the Switcher is rendering the previous Tail; Switcher progress and completion are not timing inputs.
- Do not make the Switcher inspect Cue Sheets, Phrase Windows, Beat Rewinds, or Track Phase to decide which Cue Mark comes next.
- Do not duplicate `CueSheetCursor.AdvanceTo(...)`, upcoming-sheet promotion, or Beat Rewind pass-local correction outside `OnAirTiming`.

## Acceptance criteria

- [ ] Switcher lock/arming of a cue consumes exactly that Cue Mark for the current pass through the existing pass-local cue/cadence state.
- [ ] The Director does not restart the same Cue Mark inside its runway, while it is locked/armed/executing, or after it has been consumed.
- [ ] After a Cue is consumed, the Director configures/inserts the next cue direction from the next eligible Cue Mark.
- [ ] The Director chooses Effect/Performer and Transition for the next cue only, not for every remaining Cue Mark on the sheet.
- [ ] The next cue direction can be prepared while the previous Transition Tail is still rendering, without creating a multi-cue preload queue.
- [ ] The mandatory final Cue Mark at phrase end locks/fires, consumes, and promotes/advances to the next sheet correctly.
- [ ] Same-pass cadence still prevents Cue Marks closer than the 16-beat minimum unless the existing rules explicitly allow the move.
- [ ] Focused tests cover at least two Cue Marks in one Phrase and prove only one pending cue direction is active at a time.
- [ ] No duplicate Cue Sheet advancement, consumed-cue tracking, or pass-local rewind/cadence correction exists outside the current `OnAirTiming`/`PassLocalTimingState` seam.

## Test guidance

Add tests that exercise observable behavior:

- One Phrase with at least two Cue Marks: first cue direction is inserted, Switcher locks/fires it, then the next cue direction targets the second mark through the existing `OnAirTiming`/`TimingFrame` path.
- The same Cue Mark cannot be restarted on the next beat inside its runway or after Switcher lock.
- A final phrase-end Cue Mark consumes and promotes the preplanned upcoming Cue Sheet without rerolling.
- The Director stages/configures only the next cue; future sheet marks have no preselected Effect/Transition choices.
- The next cue direction appears while `Switcher.Status.CurrentEffectIndex` or equivalent status still indicates an in-flight tailed transition.
- Existing same-window Beat Rewind tests continue to prove that a new loop pass can reuse a consumed Cue Mark after pass-local state is corrected.
- Add or update a seam test that proves Switcher commit/lock feeds the existing pass-local state instead of a parallel consumed-cue list.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests|SwitcherExecutionTests|SyncedCueIntentTests|ChangeCadenceTests' ./scripts/unity-tests.sh`
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/07-install-loaded-to-armed-cue-lifecycle.md`
