# Move Coast and Re-anchor recovery into On-Air Timing

Status: ready-for-agent

## What to build

Make Coast and Re-anchor recovery the responsibility of On-Air Timing. When Track Phase disappears while live timing is still present, On-Air Timing should coast on the last known Phase Anchor if one exists and report that as an intentional Synced Mode timing frame. If no prior anchor exists, it should report an unlocked frame rather than inventing a target. When fresh Track Phase returns or contradicts the coasted grid, On-Air Timing should re-anchor to the current structural Phrase Window and selected Phase Boundary.

This is a vertical slice through the live synced path: nullable Track Phase input changes, On-Air Timing decides whether to coast, unlock, or re-anchor, Director status reflects that reason/confidence, and cue timing continues to use the selected Phase Boundary only when the Timing Frame has a valid anchor.

Use Matt/codebase-design as the design gate: Coast/Re-anchor policy belongs behind the timing seam because it is musical interpretation, not Director orchestration or Switcher execution. Use polish as part of the slice: replace old scattered anchor refresh/coast code with the new seam instead of retaining both.

## Acceptance criteria

- [ ] Track Phase disappearance after a prior structural anchor produces a coasting Timing Frame that preserves the next valid Phase Anchor.
- [ ] Track Phase disappearance with no prior anchor produces an unlocked Timing Frame, not a fake selected Phase Boundary.
- [ ] Coasting remains Synced Mode behavior when other live timing is present; it does not route the Director into Standalone Mode.
- [ ] Fresh Track Phase after coasting re-anchors to structural phrase data and reports a re-anchor/source reason through the Timing Frame.
- [ ] A contradictory fresh Phrase Window replaces the coasted anchor instead of layering multiple anchors.
- [ ] Director status exposes Phase Confidence and source/reason from the Timing Frame so HUD/inspector diagnostics can distinguish structural, coasted, re-anchored, grid, and unlocked states.
- [ ] Cue timing waits when the Timing Frame is unlocked and resumes from the selected Phase Boundary when a valid anchor exists.
- [ ] On-Air Timing tests cover coasting, no-prior-anchor unlock, fresh re-anchor, contradictory phrase replacement, and reason/confidence reporting.
- [ ] Director integration tests cover observable status/cue behavior for coasted and unlocked frames without private-field assertions.
- [ ] The scoped diff is polished after behavior is green: anchor/coast/re-anchor vocabulary is consistent and old Director-owned anchor refresh policy is removed.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/01-hard-cut-on-air-timing-frame.md`
