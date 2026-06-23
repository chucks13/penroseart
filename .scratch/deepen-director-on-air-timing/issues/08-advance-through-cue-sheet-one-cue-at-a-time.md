# Advance through the Cue Sheet one Cue at a time

Status: approved

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

Cue Sheets are timing plans, not queues of fully chosen future moves. ADR 0005 deliberately keeps Effect and Transition choice in the Director because those choices depend on the current wall state, Repertoire, staged/manual choices, and live musical events.

After Issue 07 installs the fire-and-forget Switcher cue command, the next risk is accidentally precomputing every future cue on a sheet or restarting the same Cue Mark. This issue proves the lifecycle repeats correctly: one Cue Mark is selected, one cue direction is sent to the Switcher at the cue window, the Director consumes that Cue Mark for the current pass from its own command, and then the next Cue Mark becomes the next cue direction target through On-Air Timing.

## Current code findings

- `OnAirTiming.CueSheetPlans.ResolveCurrent(...)` and its `CueSheetCursor` already advance through the current Cue Sheet, promote preplanned upcoming Cue Sheets, and skip consumed Cue Marks.
- `PassLocalTimingState` already carries `LastCueBeat` and `PreviousCueMarkBeat`; `OnAirTiming.BuildFramePassLocalState(...)` clears stale cue/cadence state after substantial Beat Rewind so a loop pass can reuse the same Cue Mark.
- Issue 07 moved Cue Mark consumption to the Director's fire-and-forget cue command. `Director.CommitSentCue(...)` consumes the current Cue Mark by calling `MarkChangedOnBeat(beatPlan.ImpactBeat)` and recording `lastCueBeat = beat`, then stages the following move without reading Switcher lifecycle state.
- Existing regression tests already prove pieces of the desired lifecycle: `FiredMandatoryCueMarkImmediatelyPromotesPreplannedNextPhraseCueMark`, `FiredCueImmediatelyLoadsNextPreplannedPhraseBoundary`, `SyncedCueDoesNotRestartSameMandatoryBoundaryInsideRunway`, and tailed-transition next-target tests.
- `OnAirTimingTests` are the right seam for Cue Sheet advancement and loop/rewind cursor behavior. Do not duplicate Cue Sheet selection logic in the Switcher.

## What to build

Make the Director progress through the current Cue Sheet one Cue Mark at a time while the Switcher owns the current cue lifecycle.

The Director should not choose Effects and Transitions for every future Cue Mark upfront. It should configure only the next cue direction from the current wall state and current musical context when the cue window arrives, send it to the Switcher, and advance its pass-local Cue Mark state from that command. Switcher rendering/progress is execution state and not musical scheduling evidence for the Director.

## Implementation guidance

- Keep the Cue Sheet as a list of timing marks only. Do not attach Effect indexes, Transition indexes, Drop preferences, or Repertoire decisions to every mark on the sheet.
- The Director should configure only the next cue direction, not every remaining Cue Mark on the sheet.
- The Switcher should own at most one pending Loaded Cue direction at a time. If it also has a locked/executing cue, that is current execution state, not permission to build a multi-cue queue.
- Consume a Cue Mark exactly once per pass when the Director sends the cue-window command. The Director must not learn this from Switcher result/status.
- Feed that command consumption back through the existing pass-local language (`LastCueBeat` / `PreviousCueMarkBeat`) rather than creating a second consumed-cue ledger in the Director or Switcher.
- After consumption, call back through the existing `OnAirTiming.ReadFrame(...)` / `TimingFrame` path for the next Cue Mark from the same current Cue Sheet or from the promoted upcoming Cue Sheet.
- The mandatory final Cue Mark should consume just like an interior mark, while still allowing the preplanned upcoming sheet to promote without rerolling.
- Keep pass-local cadence state in the existing Cue Mark language (`LastCueBeat`, `PreviousCueMarkBeat`). Do not reintroduce `PreviousSelectedPhaseBoundary` or a second pass-local state model.
- Do not block configuring/inserting the next cue direction merely because the Switcher is rendering the previous Tail; Switcher progress and completion are not timing inputs.
- Do not make the Switcher inspect Cue Sheets, Phrase Windows, Beat Rewinds, or Track Phase to decide which Cue Mark comes next.
- Do not duplicate `CueSheetCursor.AdvanceTo(...)`, upcoming-sheet promotion, or Beat Rewind pass-local correction outside `OnAirTiming`.

## Acceptance criteria

- [x] Sending a cue-window command consumes exactly that Cue Mark for the current pass through the existing pass-local cue/cadence state.
- [x] The Director does not restart the same Cue Mark inside its runway or after it has been consumed.
- [x] After a Cue is consumed, the Director targets the next eligible Cue Mark through On-Air Timing.
- [x] The Director chooses Effect/Performer and Transition for the current cue-window command only, not for every remaining Cue Mark on the sheet.
- [x] Switcher execution status does not drive Director Cue Sheet advancement, and no multi-cue preload queue is created.
- [x] The mandatory final Cue Mark at phrase end locks/fires, consumes, and promotes/advances to the next sheet correctly.
- [x] Same-pass cadence still prevents Cue Marks closer than the 16-beat minimum unless the existing rules explicitly allow the move.
- [x] Focused tests cover at least two Cue Marks in one Phrase and prove only one pending cue direction is active at a time.
- [x] No duplicate Cue Sheet advancement, consumed-cue tracking, or pass-local rewind/cadence correction exists outside the current `OnAirTiming`/`PassLocalTimingState` seam.

## Test guidance

Add tests that exercise observable behavior:

- One Phrase with at least two Cue Marks: the first cue command is sent in its cue window, then the next eligible Cue Mark is selected through the existing `OnAirTiming`/`TimingFrame` path.
- The same Cue Mark cannot be restarted on the next beat inside its runway after the Director has sent the cue command.
- A final phrase-end Cue Mark consumes and promotes the preplanned upcoming Cue Sheet without rerolling.
- The Director stages/configures only the current cue-window command; future sheet marks have no preselected Effect/Transition choices.
- Existing same-window Beat Rewind tests continue to prove that a new loop pass can reuse a consumed Cue Mark after pass-local state is corrected.
- Add or update a seam test that proves the Director's sent cue command feeds existing pass-local state instead of a parallel consumed-cue list.

## Implementation notes

- Runtime code was adjusted after review so the Director no longer reads Switcher cue lifecycle state.
- `Director.CommitSentCue(...)` is the commit signal: the Director consumes the Cue Mark through existing pass-local cue/cadence state when it sends the cue-window command, and stages the following move without reading Switcher status.
- `OnAirTiming.CueSheetPlans.ResolveCurrent(...)` / `CueSheetCursor.AdvanceTo(...)` already advance through Cue Marks using `PassLocalTimingState.PreviousCueMarkBeat`, including final Cue Mark promotion to the preplanned upcoming sheet.
- Updated Director tests to assert cue-window command behavior (`DirectorWaitsBeforeTransitionRunway`, `DirectorUsesLatestStagedEffectWhenCueWindowArrives`, `SentCueStartsAtRunwayStartBeforeCueMark`) instead of Switcher Loaded Cue status.

## Validation evidence

- `./scripts/unity-compile.sh` passed with C# warning count 0.
- `UNITY_TEST_FILTER='DirectorSyncedTailTests|SwitcherExecutionTests|SyncedCueIntentTests|OnAirTimingTests|ChangeCadenceTests|EffectDeckSelectionTests' ./scripts/unity-tests.sh` passed 65/65.
- Full `./scripts/unity-tests.sh` passed 217/217.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests|SwitcherExecutionTests|SyncedCueIntentTests|ChangeCadenceTests' ./scripts/unity-tests.sh`
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/07-install-loaded-to-armed-cue-lifecycle.md`
