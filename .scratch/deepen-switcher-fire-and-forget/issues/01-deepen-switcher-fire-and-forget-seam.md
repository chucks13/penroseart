# Deepen Mechanical Switcher into fire-and-forget execution seam

Status: ready-for-human

## Parent

`.scratch/deepen-switcher-fire-and-forget/PRD.md`

## What to build

Deepen the Director/Switcher seam so the Director only makes musical decisions and the Mechanical Switcher fully owns A-to-B Transition execution after start.

The Director should use Phrase Window / Selected Phase Boundary / cadence / Repertoire Runway facts to decide when to start the next move. Once it starts the move, it should stage the following Next Effect and Next Transition and continue updating its decision matrix from musical timing. It must not wait for Tail completion, query active switching state, compute per-frame transition progress, or complete the Transition.

The Mechanical Switcher should receive the start command and enough timing context to render the move correctly. After that it owns progress, Tail, completion, and promotion of B to the current Effect. Active switching state may exist privately inside the Switcher implementation, but it must not be a public Director-facing seam.

## Acceptance criteria

- [x] The Director does not call or branch on any public `is switching` / active-transition flag from the Switcher.
- [x] Public Switcher status no longer exposes an `IsTransitioning` boolean as a scheduling-shaped interface.
- [x] `Transitioning` is no longer a Director decision state.
- [x] The Director does not own active Transition progress, Tail completion, or Transition completion in Synced Mode.
- [x] The Director does not call a Switcher completion method as part of normal musical scheduling.
- [x] The Mechanical Switcher owns Transition progress, Tail, completion, and promotion of B to the current Effect after `StartTransition`.
- [x] The Director reads Transition Repertoire only as much as needed to decide when to start the move, especially Runway.
- [x] The Director stages the following Next Effect and Next Transition when the current move starts, not when the Tail completes.
- [x] The Director decision matrix continues to update from Phrase Window / Phase Boundary / cadence state while a Tail is rendering.
- [x] A focused synced-tail regression proves the Director reports the next Selected Phase Boundary decision while a previous Tail is still mechanically rendering.
- [x] A focused staging regression proves consumed Next Effect / Next Transition choices are replaced at transition start.
- [x] Standalone Mode still self-runs without OSC data and does not use Switcher busy state as its ordinary decision input.
- [x] Hold / Show Now remain explicit override commands and may cancel or replace mechanical execution.
- [x] Editor/debug displays can still present what the Mechanical Switcher is rendering, but that display state is not used by Director decisions.
- [x] The 16-beat minimum cadence and `Runway >= 0`, `Tail >= 0`, `Runway + Tail <= 12` Transition Duration contract remain the overlap-prevention contract; no queues, preemption, overlap guards, snapshots, restart machinery, or scheduler layer are added.
- [x] Existing Transition Settings and Transition Repertoire validation remains at the authoring/settings seam; the Director does not compensate for invalid timing, and zero/zero hard cuts are valid.
- [x] Focused Director synced-tail tests pass.
- [x] Focused Director staging tests pass.
- [x] Unity compile passes with zero C# warnings.
- [x] The broader Unity test suite passes.

## Testing notes

Use the highest useful behavior seams:

- Director/Switcher integration through Director ticks, runtime status, staged choices, and Switcher presentation state.
- Switcher execution through its own public start/render surface, only where needed to prove it owns progress/completion internally.

Avoid tests that preserve the old shallow interface by asserting a public `IsTransitioning` flag. The behavior to prove is that mechanical transition execution does not affect Director planning or decisions.

## Comments

Created from the deepening discussion after editor testing showed tailed Transitions still influencing the decision matrix. The intended fix is not another busy-state condition; it is to make the Mechanical Switcher a deeper module and remove active transition execution from the Director's decision interface.

- Implemented on 2026-06-19. The Switcher now owns transition progress/completion/promotion after `StartTransition`; the Director no longer owns `SyncedTransitionPlan`, `TransitionProgress`, standalone active-transition state, or normal completion calls. Validation: `UNITY_TEST_FILTER=SwitcherExecutionTests`, `UNITY_TEST_FILTER=DirectorStagingTests`, `UNITY_TEST_FILTER=DirectorSyncedTailTests`, `scripts/unity-compile.sh`, and full `UNITY_EDITOR_TEST_TIMEOUT=180 scripts/unity-tests.sh` all passed.
- Review cleanup on 2026-06-19 kept the Switcher fire-and-forget: `StartTransition` is last-command-wins, inspector status no longer reports a stale active transition, existing Transition Settings assets are not silently constrained on load, and timing validation is the confirmed authoring contract `Runway >= 0`, `Tail >= 0`, `Runway + Tail <= 12` with zero/zero hard cuts. Final validation: focused Director/Switcher/Transition timing tests passed 42/42, `scripts/unity-compile.sh` reported 0 C# warnings, and full `UNITY_EDITOR_TEST_TIMEOUT=180 scripts/unity-tests.sh` passed 157/157.
