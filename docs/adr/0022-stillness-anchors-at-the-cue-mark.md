# The stillness count anchors at the Cue Mark, not at cue fire

Status: accepted

ADR-0021's first ruling — fire math follows the Transition that will actually perform — stands unchanged and is carried forward here. Its second ruling anchored the run-time stillness count at cue start: `Switcher.boundariesSinceCue` reset the moment a cue fired, a Runway before its mark. A live run (session log `penrose-20260725-155541.log`, 2026-07-25) showed what that costs: the mark's own Grid Boundary is crossed *after* the reset and counted as a Grid of stillness, so the ceiling arms 48 beats past the mark instead of 64. At the one legal 64-beat gap in the plan, the ceiling dealt a certain Off-Plan Cue 16 beats before the plan's own mark — a spurious on-air transition, deterministic for every 64-beat gap the builder legally produces. The run-time bound was measuring a different interval than the plan-time `TrackCueSheet.MaximumGapBeats` it exists to enforce, which broke the promise that an off-plan cue never pre-empts a plan the playhead is still walking through.

We decide the stillness count anchors at the **Cue Mark the cue lands on**. The boundary crossing that belongs to an in-flight cue's own landing is not stillness; stillness begins at the mark. The ceiling therefore asks only when a full four Grids of music have passed since the last cue's mark with nothing performed — the same 64 beats the plan-time rule bounds — and a plan mark on the ceiling boundary always wins, because due marks are answered before the ceiling is consulted. A handover is the same rule at its start: the cast beat is the start line of the new plan's stillness, so a handover landing on a Grid Boundary does not count that crossing either — counting it armed the ceiling one Grid early against the plan's opening mark.

## Consequences

- ADR-0021 is superseded. Its first ruling continues here verbatim; only the anchor ruling is reversed.
- `SwitcherExecutionTests` pins the non-pre-emption case: a legal 64-beat gap between marks plays out with no off-plan ask. The stillness backstop itself is unchanged in purpose and stays — it covers jumps that leave a Missed Cue behind and loops trapped in markless gaps.
- The `SWITCHER_PERFORM` trace names the beat the cue is anchored to — its Cue Mark, or the boundary an off-plan deal borrowed as its mark — and its plan/off-plan provenance, rather than a computed `impact=` beat. The Impact Point stays transition-authoring vocabulary, out of runtime state and out of the trace, per the glossary.
