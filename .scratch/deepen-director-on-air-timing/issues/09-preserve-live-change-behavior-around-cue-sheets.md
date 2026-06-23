# Preserve live-change behavior around Cue Sheets and fire-and-forget cues

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

Issues 06-08 change the naming and lifecycle of the most timing-sensitive part of Synced Mode. This follow-up slice is the regression net for live on-air behavior: Track Phase updates, loops, Beat Rewinds, Coast/Re-anchor, Drop-aware casting, cadence, manual staging, and Hold must survive the new Cue Sheet / Switcher-held Loaded Cue / Armed Cue model.

The rule is simple: before the cue window, the Director may revise staged/casting choices from better on-air facts; when the cue window arrives, the Director sends one fire-and-forget cue command and consumes that Cue Mark for the pass. After that command, later on-air changes can only affect future cue commands; the Director must not inspect Switcher lock/start status.

## Current code findings

- `BeatManagerQueries` and `OnAirTimingInput.From(BeatManager)` already adapt raw live/simulated beat data into focused timing input.
- `OnAirTiming.ReadFrame(...)` already distinguishes active Track Phase, upcoming Track Phase, unavailable Track Phase, beat-only grid, Coast, Re-anchor, and unlocked states.
- `OnAirTiming.BeatRewoundToNewPass(...)` treats substantial backward jumps of at least the minimum change cadence as a new pass and ignores small backsteps as jitter.
- `OnAirTiming.BuildFramePassLocalState(...)` clears stale cue/cadence memory only when it would block the current rewound pass.
- `Director.TickSyncedMode(...)` suspends cue work while Hold is active by returning early when a held Effect is present.
- `SyncedCueIntent.Evaluate(...)` protects cue-window eligibility, Drop-aware casting, cadence blocking, manual/held staged choices, and no-deck-mutation when cadence blocks. Preserve those protections in the existing cue/casting seam instead of recreating them in Director or Switcher code.
- Current focused tests already cover many of these cases under current Cue Mark vocabulary: `OnAirTimingTests`, `DirectorSyncedTailTests`, `SyncedCueIntentTests`, `EffectDeckSelectionTests`, and `DirectorStagingTests`.

## What to build

Verify and repair the live on-air edge cases after Cue Sheets, Switcher-held Loaded Cues, and Armed Cues are in place.

The wall should stay musically stable when Track Phase changes, loops, rewinds, disappears, or returns. Same-length updates should preserve Cue Sheets; different-length updates before the cue window should affect the upcoming command; and no timing/casting change after the Director sends a cue command should mutate that already-sent command.

This is a behavior-preservation slice, not a new feature surface.

## Implementation guidance

- Treat same-length Phrase changes as the same Cue Sheet identity even when absolute start/end beats shift.
- Treat different-length Phrase changes before the cue window as new on-air evidence: replace the Cue Sheet so the eventual fire-and-forget cue command uses the current timing facts.
- Treat different-length or contradictory Phrase changes after a cue command as future planning evidence only; the already-sent cue's mark, destination Performer, Transition, Runway, and Tail remain immutable inside the Switcher.
- Preserve same-window Loop / Beat Rewind behavior: keep the Cue Sheet, move the cursor back to the next valid Cue Mark for the new pass, and clear stale pass-local state that would block the pass.
- Preserve jitter behavior: small one- or two-beat backsteps must not reset the Cue Sheet, pending cue direction, locked/armed cue, deck state, or cadence state.
- Preserve Coast/Re-anchor semantics inside `OnAirTiming`: temporary Track Phase disappearance coasts only from a known structural/coasted anchor; fresh structural evidence replaces weaker/coasted timing and reports Re-anchor.
- Preserve Drop/manual/Hold behavior around the fire-and-forget cue command: Drop-aware casting may affect the command selected in the cue window; manual staged choices and Hold should not be silently overwritten; after the command is sent none of these can change that command.
- Preserve the existing `SyncedCueIntent`/`EffectDeckSelection` ownership of preferred Performer casting. Waiting ticks must not rotate the deck; deck mutation happens only when `SyncedCueIntent.Evaluate(...)` returns an actual cue command.
- Do not introduce Switcher-side `BeatManager` reads, raw OSC/Rave reads, loop windows, transport state machines, multi-cue queues, duplicate cue planners, or fallback/degradation paths.

## Acceptance criteria

- [ ] Same-length Phrase updates preserve the Cue Sheet and do not reroll Cue Marks.
- [ ] Different-length Phrase updates before the cue window replace the Cue Sheet so the eventual cue command uses current timing.
- [ ] After the Director sends a cue command, Phrase changes do not alter that command's Cue Mark, destination Performer, Transition, Runway, or Tail.
- [ ] Same-window Loop / Beat Rewind keeps the Cue Sheet and moves the cursor back to the next valid Cue Mark for the new pass.
- [ ] Small beat backsteps remain jitter and do not reset the Cue Sheet, pending cue direction, locked/armed cue, deck, or cadence lifecycle.
- [ ] Coast and Re-anchor still preserve or replace timing targets according to the on-air Phrase evidence.
- [ ] Drop-aware casting, cadence blocking, manual staged choices, and Hold behavior survive the Switcher-held Loaded Cue model without duplicate casting/cadence implementations.
- [ ] Focused tests cover each accepted live-change behavior without relying on Play Mode.
- [ ] No OSC protocol, serial/hardware output, PixelReceiver, drum/camera overlay, or telnet behavior changes are made.

## Test guidance

Port existing tests to the new vocabulary and add missing lock-specific tests:

- On-Air Timing seam: same-length update reuse, different-length regeneration, substantial Beat Rewind, small jitter, Coast, Re-anchor, unavailable Track Phase.
- Director/Switcher seam: the Director sends a cue-window command and records pass-local state without reading Switcher lock/start/status.
- Cue/casting seam: Drop-aligned preferred casting, no preferred Performer, manual staged preservation, cadence-blocked no deck mutation, and waiting ticks without accidental deck rotation.
- Switcher seam: use only cue direction plus minimal clock facts; do not set up BeatManager/OSC/Track Phase fixtures in Switcher tests.
- Hold: held Effect suspends Director progression and does not insert/replace cues while held.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests|SyncedCueIntentTests|EffectDeckSelectionTests|DirectorStagingTests|SwitcherExecutionTests|ChangeCadenceTests|BeatManagerRaveOscIntegrationTests|BeatManagerContrivedQueriesTests' ./scripts/unity-tests.sh`
- Full `./scripts/unity-tests.sh` after the focused slice is green.
- `git diff --check`

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/08-advance-through-cue-sheet-one-cue-at-a-time.md`
