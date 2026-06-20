# Plan Selected Phase Boundaries from exact Phrase Window timing

Status: ready-for-agent

## What to build

Make Synced Mode phrase planning speak the clarified musical model end to end: Track Phase timing facts define the current Phrase Window, the Director derives Phase Boundaries from that window, and the selected target list is made of Selected Phase Boundaries. The final phrase boundary is always selected; interior Phase Boundaries are optional and chosen once for each Phrase Window. Phrase labels may remain useful display context, but scheduling identity must come from exact timing identity: start beat, end beat, and length.

This slice should make the planning path easier for later slices by removing fuzzy Phrase Window matching and retired Slot / Selected Impact Beat language from the touched planning behavior and tests.

## Acceptance criteria

- [ ] Track Phase timing plus current beat derives a Phrase Window whose scheduling identity is exact start beat, end beat, and length.
- [ ] A new Phrase Window builds a Selected Phase Boundary plan exactly once for that timing identity.
- [ ] The final phrase boundary is always present in the selected plan.
- [ ] Eligible interior Phase Boundaries are optional/random and the plan stays stable while the same Phrase Window remains current.
- [ ] Phrase labels do not affect scheduling identity; repeated labels are harmless.
- [ ] Touched runtime status, logs, and tests use Phase Boundary / Selected Phase Boundary vocabulary instead of Slot / Selected Impact Beat where the target belongs to the music grid.
- [ ] Focused Edit Mode tests cover Phrase Window identity, mandatory final boundary, optional interior boundaries, and same-window plan stability.

## Blocked by

None - can start immediately
