# Fire math follows the selected Transition; the run-time ceiling measures stillness

Status: superseded by ADR-0022

ADR-0020 established that the Mechanical Switcher performs a handed-over Cue Sheet against the on-air beat, but its amendments reversed its own body in place until the ADR argued with itself, and it still describes a Standby Cue and a Missed Cue the code no longer has. These two rulings supersede it; everything else it settled — the permanent check-off, the Off-Plan Cue, commands down and questions up — is carried forward unchanged.

## Fire math follows the Transition that will actually perform

The Switcher asks the Director what will perform at a Cue Mark (`Director.PeekTransitionIndex`) **before** it does the due-beat arithmetic, so the Runway it counts back from the mark belongs to the selected Transition rather than to the plan's baked card. A staged override therefore leaves on its own Runway beat and its Impact Point still lands exactly on the Cue Mark; an override staged too late to fly a whole Runway into the upcoming mark performs nothing there and takes effect at the following mark instead. A cue *is* its Runway, Impact Point, and Tail, so one that cannot fly its Runway would have to jump-cut, and the wall does not jump-cut.

## The run-time ceiling measures stillness, anchored at cue start

`Switcher.boundariesSinceCue` counts Grid Boundaries crossed since the wall last *started* changing — reset the moment a cue's Runway begins, never at its Impact Point — so the ceiling measures stillness rather than time since the last landing. This is the run-time loop backstop for the cases the plan cannot answer (a DJ looping a stretch the plan left empty; an inspection freeze ending with every covered mark behind the playhead), and it is a distinct rule from the Director's plan-time `TrackCueSheet.MaximumGapBeats` of 64, which bounds gaps while a sheet is being built. Fire-and-forget is affirmed with it: nothing revokes, re-aims, or re-times a Transition once it is in flight.

## Consequences

- ADR-0020 is superseded and kept for the record of how the Switcher arrived here.
- `SwitcherExecutionTests` pins both halves: `ACueFiresSoItsImpactPointLandsOnTheCueMark` and `AStagedOverrideLeavesOnItsOwnRunwayAndStillLandsOnTheCueMark` pin Impact on `mark.Beat` for the baked card and for an override, and `ALateEntryPerformsNothing` pins that a passed Runway beat starts no Transition.
