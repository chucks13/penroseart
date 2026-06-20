# Keep Selected Phase Boundary plans across same-window Loop rewinds

Status: ready-for-agent

## What to build

Handle substantial beat rewinds as simple self-correction from the current OSC facts. When playback rewinds or repeats inside the same Phrase Window, keep that Phrase Window's Selected Phase Boundary plan and move the Director cursor back to the next selected boundary after the current beat. Clear stale pass-local cadence/cue state only when it points into the old pass. Small beat backsteps should continue to be treated as jitter/out-of-order updates and should not reset planning.

This slice should not introduce a loop scheduler, loop-window model, pass-ID translation layer, or historical playback tracker. Current Track Phase and the selected boundary plan remain the source of truth.

## Acceptance criteria

- [ ] A substantial beat rewind inside the same Phrase Window preserves the existing Selected Phase Boundary plan instead of rerolling it.
- [ ] After a same-window rewind, the Director cursor points at the next selected boundary after the current beat.
- [ ] The same Selected Phase Boundary can be used again on a later loop pass when cadence allows.
- [ ] Stale pass-local cue/cadence state is cleared when it would block the current pass because it points to the old absolute beat position.
- [ ] Small one- or two-beat backsteps remain ignored as jitter and do not reset the selected plan.
- [ ] Tests cover the self-correction behavior without creating a broad loop scheduler matrix.
- [ ] No loop scheduler, loop-window model, pass-ID system, or historical playback tracker is introduced.

## Blocked by

- `.scratch/transition-tail-phase-anchor/issues/02-plan-selected-phase-boundaries-from-phrase-window.md`
- `.scratch/transition-tail-phase-anchor/issues/03-make-tailed-transitions-fire-and-forget-for-scheduling.md`
