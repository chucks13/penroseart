# Connect Tuning Window Play Mode steering and Hold Selected

Status: ready-for-human

## Parent

`.scratch/tuning-window/PRD.md`

## What to build

Connect the Tuning Window to the live Director in Play Mode. The Transitions list should follow the Director's staged Next Transition. Clicking a Transition should stage it as the Director's Next Transition through the real runtime interface. Hold Selected should keep the selected Transition staged after each completed move, and turning Hold Selected off should return that choice to normal random staging.

This slice must exercise the real Director and Mechanical Switcher path. It must not create a fake preview scheduler, and it must not add special mid-transition handling. Selection changes steer staged/future moves; if a transition is already in flight, the ordinary path continues and the next move reflects the staged choice.

## Acceptance criteria

- [x] In Play Mode, the Transitions tab selection follows the Director's staged Next Transition.
- [x] Clicking a Transition in Play Mode sets that Transition as the Director's staged Next Transition.
- [x] Hold Selected keeps the selected Transition staged after each move completes.
- [x] Turning Hold Selected off resumes normal random Transition staging.
- [x] The window presents Next Transition distinctly from current/active Transition state.
- [x] Play Mode steering uses the real Director runtime interface and does not implement a fake scheduler.
- [x] Mid-transition selection changes do not add queues, preemption, restarts, snapshots, or defensive state machines.
- [x] Settings edits made while running persist according to the saved settings asset workflow.
- [x] Unity compile succeeds; manual Play Mode review deferred until Hunter confirms it is safe to enter Play Mode with local hardware/ports.

## Blocked by

- `.scratch/tuning-window/issues/01-expose-and-steer-director-next-effect-next-transition.md`
- `.scratch/tuning-window/issues/03-add-tuning-window-transitions-edit-mode-settings.md`
