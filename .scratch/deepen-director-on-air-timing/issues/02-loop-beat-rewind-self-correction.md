# Move Loop and Beat Rewind self-correction into On-Air Timing

Status: ready-for-human

## What to build

Make same-window Loop and substantial Beat Rewind behavior the responsibility of On-Air Timing. When the live beat jumps backward by a substantial amount inside the same Phrase Window, On-Air Timing should keep the existing selected Phase Boundary plan, move its cursor back to the next selected Phase Boundary after the current beat, and report that state through the Timing Frame. The Director should consume the updated frame normally; it should not carry separate loop/rewind correction logic.

This is a vertical slice through the synced runtime path: beat sequence changes enter through the timing input, On-Air Timing corrects the selected-boundary cursor and pass-local state, Director status/cue decisions reflect the corrected selected Phase Boundary, and tests verify the behavior through the timing seam plus one Director integration check.

Use Matt/codebase-design as the design gate: loop behavior belongs where Phase/Phrase and selected-boundary cursor state live, not scattered across Director cue code. Use polish as part of the slice: if old Director rewind helpers or tests now preserve the wrong seam, remove or rewrite them rather than layering new checks beside them.

## Acceptance criteria

- [x] A substantial Beat Rewind inside the same Phrase Window preserves the existing selected Phase Boundary plan instead of rerolling it.
- [x] After the rewind, the Timing Frame targets the next selected Phase Boundary after the current beat.
- [x] The same selected Phase Boundary can become eligible again on a later Loop pass when cadence allows.
- [x] Stale pass-local cue/cadence state is cleared only where it would incorrectly block the current pass.
- [x] Small one- or two-beat backsteps are treated as jitter and do not reset the selected Phase Boundary plan or cursor.
- [x] The Director no longer owns separate substantial-rewind or same-window loop correction helpers once On-Air Timing owns the behavior.
- [x] On-Air Timing tests cover substantial rewind, cursor reset, same-boundary reuse, stale state clearing, and jitter without creating a broad loop scheduler matrix.
- [x] A Director integration test proves the corrected Timing Frame is used for observable status/cue behavior.
- [x] No loop scheduler, loop-window model, pass-ID system, playback history, transport state machine, or speculative loop plan is introduced.
- [x] The scoped diff is polished after behavior is green: duplicate rewind logic is removed and names distinguish Loop, Beat Rewind, Phrase Window, and selected Phase Boundary.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/01-hard-cut-on-air-timing-frame.md` (accepted)

## Comments

- Implemented in the issue 02 slice on branch `refactor/deepen-director-on-air-timing`: On-Air Timing now owns `PassLocalTimingState`, preserves same-window selected Phase Boundary plans through substantial Beat Rewinds, rewinds the cursor to the next selected boundary after the current beat, and returns corrected pass-local cue/cadence memory to the Director.
- Director no longer clears Beat Rewind state directly; it consumes the corrected Timing Frame and uses the frame's pass-local state for cue decisions.
- Validation passed: `./scripts/unity-compile.sh` (0 C# warnings), focused `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests' ./scripts/unity-tests.sh` (14/14), broader Director/timing/switcher slice (77/77), full `./scripts/unity-tests.sh` (168/168), and `git diff --check`.
- Accepted by Hunter on 2026-06-22.
