# Hard-cut On-Air TimingFrame into synced sequencing

Status: ready-for-agent

## What to build

Introduce the On-Air Timing seam as a real deep module, not a compatibility wrapper. A synced tick should produce one Director-facing Timing Frame from the current live beat and Track Phase query state. The Director should consume that frame for Phase Anchor/status/cue timing instead of owning raw Track Phase interpretation, PhaseInput construction, selected Phase Boundary plan state, or selected-boundary cursor fields directly.

This first slice should cut through the runtime path end-to-end: live rhythm state is applied before the Director tick, On-Air Timing interprets the current musical structure, the Director reports status from the Timing Frame, and existing transition cue behavior still lands a Transition-local Impact Point on the selected Phase Boundary. Keep the existing Director/Switcher contract: the Director decides and the Mechanical Switcher executes.

Use Matt/codebase-design as the design gate: the timing module must hide meaningful behavior behind a small interface, use project glossary names, and be tested through the same seam the Director uses. Use polish as part of the slice: remove moved/dead Director timing code instead of leaving duplicate internal pathways.

Current codebase scan found the relevant starting points: the Controller already applies Rave OSC before BeatManager update and Director tick; the Director currently owns PhaseInput, PhaseReading, Phase Anchor fields, selected Phase Boundary planning fields, raw BeatManager query reads, cue timing, staging, logging, and status projection; existing pure modules already cover PhaseClock, PhraseWindow, SelectedPhaseBoundaryPlan, TransitionBeatPlan, SyncedCueDecision, ChangeCadence, EffectDeckSelection, Switcher, and TransitionSettings.

## Acceptance criteria

- [ ] A synced Director tick obtains one Timing Frame from an On-Air Timing module and uses that frame for Phase Anchor/status/cue timing behavior.
- [ ] The Director no longer owns raw Track Phase interpretation, PhaseInput construction, selected Phase Boundary arrays, Phrase Window identity fields, or selected-boundary cursor fields.
- [ ] The Timing Frame exposes the current beat, Phase reading, Phase Anchor availability, Phase Confidence, selected Phase Boundary, beats until selected boundary, Phrase Window identity when available, and a stable domain-facing source/reason.
- [ ] Existing structural Track Phase behavior is preserved: Track Phase derives a Phrase Window, selected Phase Boundaries are chosen for that window, and the final phrase boundary remains mandatory.
- [ ] Existing transition cue behavior is preserved: the selected Transition's Runway still determines when the A-to-B move starts so its Impact Point lands on the selected Phase Boundary.
- [ ] Tests cover the new On-Air Timing seam directly without Unity GameObject setup, Controller singleton reflection, Switcher rendering, or Unity time.
- [ ] Director-level tests cover the integration path: the Director consumes the Timing Frame and exposes correct status/cue behavior.
- [ ] No alternate timing-source adapters, service layer, event bus, dependency-injection framework, or migration/compatibility bridge is introduced.
- [ ] The scoped diff is polished after behavior is green: old duplicate timing paths are removed, names use the project glossary, and the module passes the deletion test.
- [ ] Focused timing/Director/Switcher/transition tests and the documented Unity compile/test wrapper are run or explicitly reported if impractical.

## Blocked by

None - can start immediately
