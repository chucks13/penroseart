# Phrase Cue Sheets drive Synced Mode; the Switcher owns cue lifecycle

ADR-0004 split musical direction from mechanical execution, but the Synced Mode cue flow still needed sharper ownership: phrase planning belongs to Cue Sheets, cue direction belongs to the Director, and cue lifecycle/execution belongs to the Switcher. We decide that On-Air Timing derives simple per-Phrase Cue Sheets from Phrase length. The Director configures one cue direction at a time for the next Cue Mark: target Cue Mark, destination Performer, and selected Transition. The Director inserts or updates that cue direction in the Mechanical Switcher. The Switcher holds the mutable Loaded Cue, derives the Transition-specific Lock Point, refuses further updates once locked, and executes the resulting Armed Cue so the Transition Impact Point lands on the Cue Mark.

This keeps Cue Sheet planning tied to Phrase structure, keeps Effect/Transition casting one cue at a time, and keeps Lock Point, Runway/Tail start/progress mechanics, and Unity-time conversion out of the Director.

## Considered options

- **Keep Selected Phase Boundary planning as the canonical model** — rejected because the name makes a Phrase-level cue plan sound like a Phase implementation detail, and it encourages transition timing mechanics to leak back into the Director.
- **Put a multi-cue preload queue in the Switcher** — rejected because only one mutable cue direction is needed at a time; the Director advances through the Cue Sheet one Cue Mark at a time instead of preloading a full future queue.
- **Keep Loaded Cue state in the Director and call `ArmCue` at Lock Point** — rejected because it leaves the Director deciding Lock Point and arming, which is execution lifecycle policy. The Director may author and update the cue direction, but the Switcher owns Loaded → Locked/Armed → Executing.
- **Put Effect and Transition choices inside the Cue Sheet** — rejected because the Cue Sheet is only a Phrase timing plan. Performer and Transition casting depend on the current wall state, Repertoire, and live musical events, so the Director decides them one cue at a time.

## Consequences

- A Cue Sheet is identified by the Phrase's total beat length and relative Cue Marks, not phrase name or absolute start/end beat.
- **Cue Mark** is the canonical Phrase-level term; **Selected Phase Boundary** remains current implementation vocabulary to retire or translate at the seam.
- The Director chooses Cue Mark, destination Performer, and Transition, then inserts or updates that cue direction in the Switcher while the Switcher still reports it is mutable.
- The Switcher owns the Loaded Cue slot, derives Lock Point from the selected Transition's Runway, locks/refuses further updates, and executes the Armed Cue using Runway/Tail so the Transition's Impact Point lands on the Cue Mark.
- The Switcher must not read Track Phase, choose Cue Sheets, choose Cue Marks, or cast Performers/Transitions.
- The Director must not compute or choose Lock Point, Unity start time, duration seconds, transition progress, or other mechanical execution timing for Synced Mode.
- Tests should cross the real seams: Cue Sheet length reuse and promotion, Director updating a Switcher-held cue before lock, Switcher refusing mutation after lock, and Switcher execution that does not depend on Director-computed transition timing.
