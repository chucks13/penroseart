# Transition tails can desynchronize phase anchor and prevent next transition

Status: ready-for-agent

## Report

Hunter observed that transitions with non-zero tails can sometimes leave the runtime out of sync with the musical phase when the tail completes. After that happens, the next transition does not cue. The suspected failure mode is that tail completion leaves the anchor landing out of sync with the phase.

This is currently treated as a pre-existing/runtime sequencing bug, not assumed to be caused by the Tuning Window Play Mode steering changes unless evidence later proves a connection.

## Symptoms

- A transition with a tail completes its tail.
- After completion, the phase/anchor relationship appears wrong.
- The next transition does not happen.
- The issue appears tied to transitions with tails rather than tail-less transitions.
- The behavior is intermittent / "sometimes" reproduces.

## Expected behavior

- Transition tail time should not desynchronize the Director's phase anchor from the current phase.
- After a tailed transition completes, the Director should still be able to stage and cue the next transition on the appropriate future phase/beat.
- Tail completion should leave the runtime in a coherent state for the next A-to-B move.

## Initial investigation targets

- `Assets/core/Switching/Director.cs`
  - synced transition start/completion
  - `transitionLandingBeat`
  - `phaseAnchorLandingBeat`
  - `StageNextChoices(...)`
  - phase anchor refresh/coasting after transition completion
- `Assets/core/Transitions/TransitionBeatPlan.cs`
- `Assets/core/SyncedTransitionPlan.cs`
- transition repertoires with `TailBeats > 0`
- existing synced transition / phase tests under `Assets/Tests/Editor/`

## Diagnostic plan

1. Build a tight feedback loop that can reproduce or simulate the exact symptom: a tailed transition completes and the next transition fails to cue because phase anchor / landing state is inconsistent.
2. Prefer a focused Edit Mode test around the Director / transition plan seam if it can exercise the real bug path.
3. If the bug only appears in Play Mode timing, add temporary tagged instrumentation around phase anchor, impact, completion, and next cue decisions.
4. Rank hypotheses only after the feedback loop can go red.
5. Fix through the real Director/Switcher/phase path; do not add a fake scheduler or defensive queue/preemption machinery.

## Acceptance criteria

- [ ] A repro or regression test demonstrates the tailed-transition desync / missed-next-transition failure.
- [ ] The root cause is identified with evidence from the repro or instrumentation.
- [ ] Tailed transitions complete without leaving `phaseAnchorLandingBeat` / phase state inconsistent.
- [ ] The next transition can cue normally after a tailed transition completes.
- [ ] Existing synced transition, phase, Director, and transition repertoire tests pass.
- [ ] Any temporary debug instrumentation is removed.

## Comments

- Created from Hunter's report on 2026-06-19 before diagnosis or code changes.
