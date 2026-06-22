# Move Loop and Beat Rewind self-correction into On-Air Timing

Status: ready-for-agent

## What to build

Make same-window Loop and substantial Beat Rewind behavior the responsibility of On-Air Timing. When the live beat jumps backward by a substantial amount inside the same Phrase Window, On-Air Timing should keep the existing selected Phase Boundary plan, move its cursor back to the next selected Phase Boundary after the current beat, and report that state through the Timing Frame. The Director should consume the updated frame normally; it should not carry separate loop/rewind correction logic.

This is a vertical slice through the synced runtime path: beat sequence changes enter through the timing input, On-Air Timing corrects the selected-boundary cursor and pass-local state, Director status/cue decisions reflect the corrected selected Phase Boundary, and tests verify the behavior through the timing seam plus one Director integration check.

Use Matt/codebase-design as the design gate: loop behavior belongs where Phase/Phrase and selected-boundary cursor state live, not scattered across Director cue code. Use polish as part of the slice: if old Director rewind helpers or tests now preserve the wrong seam, remove or rewrite them rather than layering new checks beside them.

## Acceptance criteria

- [ ] A substantial Beat Rewind inside the same Phrase Window preserves the existing selected Phase Boundary plan instead of rerolling it.
- [ ] After the rewind, the Timing Frame targets the next selected Phase Boundary after the current beat.
- [ ] The same selected Phase Boundary can become eligible again on a later Loop pass when cadence allows.
- [ ] Stale pass-local cue/cadence state is cleared only where it would incorrectly block the current pass.
- [ ] Small one- or two-beat backsteps are treated as jitter and do not reset the selected Phase Boundary plan or cursor.
- [ ] The Director no longer owns separate substantial-rewind or same-window loop correction helpers once On-Air Timing owns the behavior.
- [ ] On-Air Timing tests cover substantial rewind, cursor reset, same-boundary reuse, stale state clearing, and jitter without creating a broad loop scheduler matrix.
- [ ] A Director integration test proves the corrected Timing Frame is used for observable status/cue behavior.
- [ ] No loop scheduler, loop-window model, pass-ID system, playback history, transport state machine, or speculative loop plan is introduced.
- [ ] The scoped diff is polished after behavior is green: duplicate rewind logic is removed and names distinguish Loop, Beat Rewind, Phrase Window, and selected Phase Boundary.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/01-hard-cut-on-air-timing-frame.md`
