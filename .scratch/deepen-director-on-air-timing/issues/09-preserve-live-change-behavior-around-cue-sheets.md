# Preserve live-change behavior around Cue Sheets and lock

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

Issues 06-08 change the naming and lifecycle of the most timing-sensitive part of Synced Mode. This follow-up slice is the regression net for live on-air behavior: Track Phase updates, loops, Beat Rewinds, Coast/Re-anchor, Drop-aware casting, cadence, manual staging, and Hold must survive the new Cue Sheet / Loaded Cue / Armed Cue model.

The rule is simple: before Lock Point the Director may adapt to better on-air facts; after Lock Point the Armed Cue fires unchanged.

## Current code findings

- `OnAirTiming.ReadFrame(...)` already distinguishes active Track Phase, upcoming Track Phase, unavailable Track Phase, beat-only grid, Coast, Re-anchor, and unlocked states.
- `OnAirTiming.BeatRewoundToNewPass(...)` treats substantial backward jumps of at least the minimum change cadence as a new pass and ignores small backsteps as jitter.
- `OnAirTiming.BuildFramePassLocalState(...)` clears stale cue/cadence memory only when it would block the current rewound pass.
- `Director.TickSyncedMode(...)` suspends cue work while Hold is active by returning early when a held Effect is present.
- `SyncedCueIntent.Evaluate(...)` currently protects Drop-aware casting, cadence blocking, manual/held staged choices, and no-deck-mutation when cadence blocks.
- Current focused tests already cover many of these cases under old vocabulary: `OnAirTimingTests`, `DirectorSyncedTailTests`, `SyncedCueIntentTests`, `EffectDeckSelectionTests`, and `DirectorStagingTests`.

## What to build

Verify and repair the live on-air edge cases after Cue Sheets, Loaded Cues, and Armed Cues are in place.

The wall should stay musically stable when Track Phase changes, loops, rewinds, disappears, or returns. Same-length updates should preserve Cue Sheets; different-length updates should replace mutable state before Lock Point; and no timing/casting change after Lock Point should mutate the Armed Cue.

This is a behavior-preservation slice, not a new feature surface.

## Implementation guidance

- Treat same-length Phrase changes as the same Cue Sheet identity even when absolute start/end beats shift.
- Treat different-length Phrase changes before Lock Point as new on-air evidence: replace the Cue Sheet and any mutable Loaded Cue that was based on the old sheet.
- Treat different-length or contradictory Phrase changes after Lock Point as future planning evidence only; the current Armed Cue's mark, destination Performer, Transition, Runway, and Tail remain immutable.
- Preserve same-window Loop / Beat Rewind behavior: keep the Cue Sheet, move the cursor back to the next valid Cue Mark for the new pass, and clear stale pass-local state that would block the pass.
- Preserve jitter behavior: small one- or two-beat backsteps must not reset the Cue Sheet, Loaded Cue, Armed Cue, deck state, or cadence state.
- Preserve Coast/Re-anchor semantics: temporary Track Phase disappearance coasts only from a known structural/coasted anchor; fresh structural evidence replaces weaker/coasted timing and reports Re-anchor.
- Preserve Drop/manual/Hold behavior around the new lifecycle: Drop-aware casting may affect mutable Loaded Cue choice before Lock Point; manual staged choices and Hold should not be silently overwritten; after Lock Point none of these can change the Armed Cue.
- Do not introduce loop windows, transport state machines, multi-cue queues, or fallback/degradation paths.

## Acceptance criteria

- [ ] Same-length Phrase updates preserve the Cue Sheet and do not reroll Cue Marks.
- [ ] Different-length Phrase updates replace the Cue Sheet and any mutable Loaded Cue before Lock Point.
- [ ] After Lock Point, Phrase changes do not alter the Armed Cue's Cue Mark, destination Performer, Transition, Runway, or Tail.
- [ ] Same-window Loop / Beat Rewind keeps the Cue Sheet and moves the cursor back to the next valid Cue Mark for the new pass.
- [ ] Small beat backsteps remain jitter and do not reset the Cue Sheet, Loaded Cue, Armed Cue, deck, or cadence lifecycle.
- [ ] Coast and Re-anchor still preserve or replace timing targets according to the on-air Phrase evidence.
- [ ] Drop-aware casting, cadence blocking, manual staged choices, and Hold behavior survive the Cue Sheet / Loaded Cue model.
- [ ] Focused tests cover each accepted live-change behavior without relying on Play Mode.
- [ ] No OSC protocol, serial/hardware output, PixelReceiver, drum/camera overlay, or telnet behavior changes are made.

## Test guidance

Port existing tests to the new vocabulary and add missing lock-specific tests:

- On-Air Timing seam: same-length update reuse, different-length regeneration, substantial Beat Rewind, small jitter, Coast, Re-anchor, unavailable Track Phase.
- Director seam before Lock Point: mutable Loaded Cue updates when Phrase length changes or Drop/manual inputs change.
- Director/Switcher seam after Lock Point: changed Track Phase, changed staged Effect, or changed Drop data does not alter the Armed Cue.
- Cue/casting seam: Drop-aligned preferred casting, no preferred Performer, manual staged preservation, cadence-blocked no deck mutation.
- Hold: held Effect suspends Director progression and does not arm/replace cues while held.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests|SyncedCueIntentTests|EffectDeckSelectionTests|DirectorStagingTests|SwitcherExecutionTests|ChangeCadenceTests|BeatManagerRaveOscIntegrationTests|BeatManagerContrivedQueriesTests' ./scripts/unity-tests.sh`
- Full `./scripts/unity-tests.sh` after the focused slice is green.
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/08-advance-through-cue-sheet-one-cue-at-a-time.md`
