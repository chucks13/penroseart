# Expose and steer Director Next Effect / Next Transition

Status: ready-for-agent

## Parent

`.scratch/tuning-window/PRD.md`

## What to build

Add explicit Director staging for Next Effect and Next Transition, expose that staged state through runtime status, and provide the small steering interface that authoring tools will use. The Director should stage both next choices on startup and after each completed A-to-B move, consume those staged choices when starting the next move, and support one-shot manual steering plus Hold Selected behavior without adding queues, preemption, restarts, snapshots, or mid-transition defensive state machines.

This slice should also correct runtime observability so current on-wall state, current/active Transition, Next Effect, and Next Transition are distinct. The Mechanical Switcher remains execution-only: it executes what the Director directs and does not decide what or when.

## Acceptance criteria

- [ ] Director status exposes the currently staged Next Effect and Next Transition separately from Mechanical Switcher current/active state.
- [ ] The Director stages both next choices on startup and after each completed move.
- [ ] The Director consumes staged next choices when starting the next A-to-B move instead of choosing the target Effect at the last moment.
- [ ] Runtime/editor-facing methods can set the staged Next Effect, set the staged Next Transition, and toggle Hold Selected behavior.
- [ ] Manual selection without Hold Selected is one-shot: after that staged move completes, normal random staging resumes.
- [ ] Hold Selected keeps the selected Effect or Transition staged after each move completes without freezing the wall.
- [ ] Existing Inspector/status readouts distinguish Current Effect, Next Effect, current/active Transition, and Next Transition.
- [ ] Focused tests cover staging, restaging after completion, one-shot steering, and Hold Selected behavior where practical.
- [ ] No queueing, preemption, restart, snapshot, or special mid-transition defensive machinery is added.

## Blocked by

None - can start immediately
