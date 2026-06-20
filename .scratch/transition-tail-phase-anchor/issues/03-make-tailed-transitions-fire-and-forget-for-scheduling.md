# Make tailed A-to-B Transitions fire-and-forget for musical scheduling

Status: ready-for-agent

## What to build

Fix the tailed-transition desync through the clarified Director/Switcher seam. The Director should select a Phase Boundary from the current Phrase Window, choose the next Transition, and start the A-to-B move early enough for that Transition's local Impact Point to hit the selected boundary. After the move starts, active Transition progress, Tail completion, and Switcher busy state are visual execution facts only; they must not re-anchor, re-plan, or mark a new musical cadence event.

Keep the existing red repro as the first feedback loop: after a tailed Transition finishes visually, the next Phase Anchor must still come from Track Phase / Phrase Window timing rather than the weaker inferred grid, and the next transition must be able to cue normally.

## Acceptance criteria

- [ ] The tailed-transition regression test is kept and passes, proving the post-tail Phase Anchor lands on the Track Phase-derived boundary.
- [ ] A started A-to-B Transition continues rendering through its Tail without feeding Tail completion back into Phrase Window planning, Phase Boundary selection, Phase Anchor updates, or cadence marking.
- [ ] Cadence is marked from the selected musical boundary / Transition-local Impact Point, not from Transition Completion.
- [ ] Switcher busy state no longer blocks the Director from maintaining current musical planning state.
- [ ] Valid Track Phase timing facts remain usable for Phrase Window anchoring after a tailed transition, so the Director does not fall back to a weaker grid while useful timing facts are present.
- [ ] A focused Director synced sequencing test proves the next transition can cue normally after a tailed transition completes.
- [ ] Standalone Mode still self-runs when no live OSC data is present, and Synced Mode remains active when live OSC is present even if Track Phase is temporarily unavailable.
- [ ] No queues, preemption, transition snapshots, restart machinery, or defensive scheduler layer are introduced.

## Blocked by

- `.scratch/transition-tail-phase-anchor/issues/02-plan-selected-phase-boundaries-from-phrase-window.md`
