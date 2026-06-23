# Hard-cut On-Air TimingFrame into synced sequencing

Status: ready-for-human

## What to build

Introduce the On-Air Timing seam as a real deep module, not a compatibility wrapper. A synced tick should produce one Director-facing Timing Frame from the current live beat and Track Phase query state. The Director should consume that frame for Phase Anchor/status/cue timing instead of owning raw Track Phase interpretation, PhaseInput construction, selected Phase Boundary plan state, or selected-boundary cursor fields directly.

This first slice should cut through the runtime path end-to-end: live rhythm state is applied before the Director tick, On-Air Timing interprets the current musical structure, the Director reports status from the Timing Frame, and existing transition cue behavior still lands a Transition-local Impact Point on the selected Phase Boundary. Keep the existing Director/Switcher contract: the Director decides and the Mechanical Switcher executes.

Use Matt/codebase-design as the design gate: the timing module must hide meaningful behavior behind a small interface, use project glossary names, and be tested through the same seam the Director uses. Use polish as part of the slice: remove moved/dead Director timing code instead of leaving duplicate internal pathways.

Current codebase scan found the relevant starting points: the Controller already applies Rave OSC before BeatManager update and Director tick; the Director currently owns PhaseInput, PhaseReading, Phase Anchor fields, selected Phase Boundary planning fields, raw BeatManager query reads, cue timing, staging, logging, and status projection; existing pure modules already cover PhaseClock, PhraseWindow, SelectedPhaseBoundaryPlan, TransitionBeatPlan, SyncedCueDecision, ChangeCadence, EffectDeckSelection, Switcher, and TransitionSettings.

## Acceptance criteria

- [x] A synced Director tick obtains one Timing Frame from an On-Air Timing module and uses that frame for Phase Anchor/status/cue timing behavior.
- [x] The Director no longer owns raw Track Phase interpretation, PhaseInput construction, selected Phase Boundary arrays, Phrase Window identity fields, or selected-boundary cursor fields.
- [x] The Timing Frame exposes the current beat, Phase reading, Phase Anchor availability, Phase Confidence, selected Phase Boundary, beats until selected boundary, Phrase Window identity when available, and a stable domain-facing source/reason.
- [x] Existing structural Track Phase behavior is preserved: Track Phase derives a Phrase Window, selected Phase Boundaries are chosen for that window, and the final phrase boundary remains mandatory.
- [x] Existing transition cue behavior is preserved: the selected Transition's Runway still determines when the A-to-B move starts so its Impact Point lands on the selected Phase Boundary.
- [x] Tests cover the new On-Air Timing seam directly without Unity GameObject setup, Controller singleton reflection, Switcher rendering, or Unity time.
- [x] Director-level tests cover the integration path: the Director consumes the Timing Frame and exposes correct status/cue behavior.
- [x] No alternate timing-source adapters, service layer, event bus, dependency-injection framework, or migration/compatibility bridge is introduced.
- [x] The scoped diff is polished after behavior is green: old duplicate timing paths are removed, names use the project glossary, and the module passes the deletion test.
- [x] Focused timing/Director/Switcher/transition tests and the documented Unity compile/test wrapper are run or explicitly reported if impractical.

## Blocked by

None - can start immediately

## Comments

- Implemented in `0469a935f27b8628b2be9d14e66aaf6dba2057dc` (`refactor(director): introduce on-air timing frame`). On-Air Timing now owns the Director-facing `TimingFrame`, raw timing snapshot interpretation, PhaseClock/PhraseWindow use, selected Phase Boundary cursor state, and substantial beat rewind detection; Director consumes the frame for synced status and cue timing.
- Validation passed: `./scripts/unity-compile.sh` (0 C# warnings), focused `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests' ./scripts/unity-tests.sh` (10/10), and full `./scripts/unity-tests.sh` (164/164).
- Accepted by Hunter on 2026-06-22.
