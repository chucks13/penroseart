# Refresh Director runtime architecture docs after the hard cut

Status: ready-for-agent

## What to build

Refresh the canonical runtime architecture documentation so it describes the final Director shape after the hard-cut timing and cue/casting work lands. The docs should teach the same model as the code and tests: Controller applies live OSC and advances BeatManager before the Director tick; On-Air Timing interprets live musical structure into a Timing Frame; the Director decides from timing/cue/casting state; the Mechanical Switcher executes A-to-B moves; Effects express their own Repertoire; Standalone Mode remains intentional when no live source exists.

This is a documentation vertical slice: update the runtime architecture story, align vocabulary with the glossary and ADR, and remove stale descriptions of the old timer-transition loop. Do not duplicate the architecture review report; promote the durable model into canonical docs.

Use Matt/codebase-design as the documentation lens: explain modules, interfaces, seams, and responsibilities in the domain language. Use polish as part of the slice: remove stale or contradictory wording rather than adding a new paragraph that leaves the old model nearby.

## Acceptance criteria

- [ ] Runtime architecture docs no longer describe the old Controller timer transition loop as the active sequencing model.
- [ ] Docs describe the current sequencing flow: live OSC application, BeatManager update, Director tick, On-Air Timing Timing Frame, cue/casting, Mechanical Switcher rendering, and hardware/preview output.
- [ ] Docs clearly distinguish Phase, Phrase Window, Phase Boundary, Selected Phase Boundary, Phase Anchor, Coast, Re-anchor, Loop, Beat Rewind, Runway, Tail, Impact Point, Cue, Repertoire, Performer, Director, and Mechanical Switcher.
- [ ] Docs state that On-Air Timing interprets live musical structure and that the Director consumes Timing Frames rather than raw Track Phase fields.
- [ ] Docs state that cue/casting uses Timing Frame and Repertoire without making Effects' expression internals a Director responsibility.
- [ ] Docs state that the Mechanical Switcher remains execution-only and that active transition progress/Tail completion are not musical scheduling inputs.
- [ ] Docs align with the Director/Switcher ADR; if implementation changes the durable decision beyond the ADR, create or update an ADR separately using the repo ADR conventions.
- [ ] Historical architecture review content is referenced only where helpful; it is not duplicated wholesale into canonical docs.
- [ ] Documentation wording is polished: stale contradictions are removed, not patched around, and the code/test vocabulary matches the docs.
- [ ] A lightweight validation pass confirms the referenced commands/docs paths are accurate; no Play Mode validation is required for this docs-only slice.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/01-hard-cut-on-air-timing-frame.md`
- `.scratch/deepen-director-on-air-timing/issues/02-loop-beat-rewind-self-correction.md`
- `.scratch/deepen-director-on-air-timing/issues/03-coast-and-reanchor-recovery.md`
- `.scratch/deepen-director-on-air-timing/issues/04-cue-intent-repertoire-casting.md`
