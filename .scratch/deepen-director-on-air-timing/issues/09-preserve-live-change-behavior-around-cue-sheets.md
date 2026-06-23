# Preserve live-change behavior around Cue Sheets and Switcher lock

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

Issues 06-08 change the naming and lifecycle of the most timing-sensitive part of Synced Mode. This follow-up slice is the regression net for live on-air behavior: Track Phase updates, loops, Beat Rewinds, Coast/Re-anchor, Drop-aware casting, cadence, manual staging, and Hold must survive the new Cue Sheet / Switcher-held Loaded Cue / Armed Cue model.

The rule is simple: before the Switcher locks the current cue, the Director may update the cue direction with better on-air facts or casting choices; after the Switcher locks it, later Director updates must be rejected for that cue and can only affect future cue directions.

## Current code findings

- `OnAirTiming.ReadFrame(...)` already distinguishes active Track Phase, upcoming Track Phase, unavailable Track Phase, beat-only grid, Coast, Re-anchor, and unlocked states.
- `OnAirTiming.BeatRewoundToNewPass(...)` treats substantial backward jumps of at least the minimum change cadence as a new pass and ignores small backsteps as jitter.
- `OnAirTiming.BuildFramePassLocalState(...)` clears stale cue/cadence memory only when it would block the current rewound pass.
- `Director.TickSyncedMode(...)` suspends cue work while Hold is active by returning early when a held Effect is present.
- `SyncedCueIntent.Evaluate(...)` currently protects Drop-aware casting, cadence blocking, manual/held staged choices, and no-deck-mutation when cadence blocks.
- Current focused tests already cover many of these cases under old vocabulary: `OnAirTimingTests`, `DirectorSyncedTailTests`, `SyncedCueIntentTests`, `EffectDeckSelectionTests`, and `DirectorStagingTests`.

## What to build

Verify and repair the live on-air edge cases after Cue Sheets, Switcher-held Loaded Cues, and Armed Cues are in place.

The wall should stay musically stable when Track Phase changes, loops, rewinds, disappears, or returns. Same-length updates should preserve Cue Sheets; different-length updates should replace mutable timing/cue direction before Switcher lock; and no timing/casting change after Switcher lock should mutate the locked/armed cue.

This is a behavior-preservation slice, not a new feature surface.

## Implementation guidance

- Treat same-length Phrase changes as the same Cue Sheet identity even when absolute start/end beats shift.
- Treat different-length Phrase changes before Switcher lock as new on-air evidence: replace the Cue Sheet and offer an updated cue direction to the Switcher if the current Loaded Cue is still mutable.
- Treat different-length or contradictory Phrase changes after Switcher lock as future planning evidence only; the locked/armed cue's mark, destination Performer, Transition, Runway, and Tail remain immutable.
- Preserve same-window Loop / Beat Rewind behavior: keep the Cue Sheet, move the cursor back to the next valid Cue Mark for the new pass, and clear stale pass-local state that would block the pass.
- Preserve jitter behavior: small one- or two-beat backsteps must not reset the Cue Sheet, pending cue direction, locked/armed cue, deck state, or cadence state.
- Preserve Coast/Re-anchor semantics: temporary Track Phase disappearance coasts only from a known structural/coasted anchor; fresh structural evidence replaces weaker/coasted timing and reports Re-anchor.
- Preserve Drop/manual/Hold behavior around the new lifecycle: Drop-aware casting may affect mutable cue direction before Switcher lock; manual staged choices and Hold should not be silently overwritten; after Switcher lock none of these can change the locked/armed cue.
- Do not introduce loop windows, transport state machines, multi-cue queues, or fallback/degradation paths.

## Acceptance criteria

- [ ] Same-length Phrase updates preserve the Cue Sheet and do not reroll Cue Marks.
- [ ] Different-length Phrase updates replace the Cue Sheet and any mutable cue direction before Switcher lock.
- [ ] After Switcher lock, Phrase changes do not alter the locked/armed cue's Cue Mark, destination Performer, Transition, Runway, or Tail.
- [ ] Same-window Loop / Beat Rewind keeps the Cue Sheet and moves the cursor back to the next valid Cue Mark for the new pass.
- [ ] Small beat backsteps remain jitter and do not reset the Cue Sheet, pending cue direction, locked/armed cue, deck, or cadence lifecycle.
- [ ] Coast and Re-anchor still preserve or replace timing targets according to the on-air Phrase evidence.
- [ ] Drop-aware casting, cadence blocking, manual staged choices, and Hold behavior survive the Switcher-held Loaded Cue model.
- [ ] Focused tests cover each accepted live-change behavior without relying on Play Mode.
- [ ] No OSC protocol, serial/hardware output, PixelReceiver, drum/camera overlay, or telnet behavior changes are made.

## Test guidance

Port existing tests to the new vocabulary and add missing lock-specific tests:

- On-Air Timing seam: same-length update reuse, different-length regeneration, substantial Beat Rewind, small jitter, Coast, Re-anchor, unavailable Track Phase.
- Director/Switcher seam before lock: mutable cue direction updates when Phrase length changes or Drop/manual inputs change.
- Director/Switcher seam after lock: changed Track Phase, changed staged Effect, or changed Drop data is offered as an update but does not alter the Switcher-locked cue.
- Cue/casting seam: Drop-aligned preferred casting, no preferred Performer, manual staged preservation, cadence-blocked no deck mutation.
- Hold: held Effect suspends Director progression and does not insert/replace cues while held.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests|SyncedCueIntentTests|EffectDeckSelectionTests|DirectorStagingTests|SwitcherExecutionTests|ChangeCadenceTests|BeatManagerRaveOscIntegrationTests|BeatManagerContrivedQueriesTests' ./scripts/unity-tests.sh`
- Full `./scripts/unity-tests.sh` after the focused slice is green.
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/08-advance-through-cue-sheet-one-cue-at-a-time.md`
