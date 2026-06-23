# Phrase Cue Sheets drive Synced Mode; the Switcher owns cue lifecycle

ADR-0004 split musical direction from mechanical execution, but the Synced Mode cue flow still needed sharper ownership: phrase planning belongs to Cue Sheets, cue direction belongs to the Director, and cue lifecycle/execution belongs to the Switcher. We decide that On-Air Timing derives simple per-Phrase Cue Sheets from Phrase length. The Director configures one cue direction at a time for the next Cue Mark: target Cue Mark, destination Performer, and selected Transition. At the cue window, the Director sends that cue direction to the Mechanical Switcher fire-and-forget and records pass-local Cue Mark consumption from its own command. The Switcher holds/schedules the Loaded Cue, derives the Transition-specific Lock Point, refuses conflicting updates once locked, and executes the resulting Armed Cue so the Transition Impact Point lands on the Cue Mark.

This keeps Cue Sheet planning tied to Phrase structure, keeps Effect/Transition casting one cue at a time, and keeps Lock Point, Runway/Tail start/progress mechanics, and Unity-time conversion out of the Director.

## Amendment 2026-06-23

We refined the cue handoff to be fire-and-forget from the Director's perspective. The original ownership decision still stands, but the Director no longer polls Switcher mutability or lifecycle state; it sends cue-window directions and records pass-local consumption from its own command while the Switcher schedules, locks, starts, and completes the cue internally.

## Considered options

- **Keep Selected Phase Boundary planning as the canonical model** — rejected because the name makes a Phrase-level cue plan sound like a Phase implementation detail, and it encourages transition timing mechanics to leak back into the Director.
- **Put a multi-cue preload queue in the Switcher** — rejected because only one mutable cue direction is needed at a time; the Director advances through the Cue Sheet one Cue Mark at a time instead of preloading a full future queue.
- **Keep Loaded Cue state in the Director and call `ArmCue` at Lock Point** — rejected because it leaves the Director deciding Lock Point and arming, which is execution lifecycle policy. The Director may author and update the cue direction, but the Switcher owns Loaded → Locked/Armed → Executing.
- **Put Effect and Transition choices inside the Cue Sheet** — rejected because the Cue Sheet is only a Phrase timing plan. Performer and Transition casting depend on the current wall state, Repertoire, and live musical events, so the Director decides them one cue at a time.

## Consequences

- A Cue Sheet is identified by the Phrase's total beat length and relative Cue Marks, not phrase name or absolute start/end beat.
- **Cue Mark** is the canonical Phrase-level term; **Selected Phase Boundary** remains current implementation vocabulary to retire or translate at the seam.
- The Director chooses Cue Mark, destination Performer, and Transition, then sends that cue direction to the Switcher fire-and-forget at the cue window.
- The Director records pass-local Cue Mark consumption from its own sent command and must not inspect Switcher Loaded/Locked/Started state as scheduling input.
- The Switcher owns the Loaded Cue slot, derives Lock Point from the selected Transition's Runway, locks/refuses conflicting updates, and executes the Armed Cue using Runway/Tail so the Transition's Impact Point lands on the Cue Mark.
- The Switcher must not read Track Phase, choose Cue Sheets, choose Cue Marks, or cast Performers/Transitions.
- The Director must not compute or choose Lock Point, Unity start time, duration seconds, transition progress, or other mechanical execution timing for Synced Mode.
- Tests should cross the real seams: Cue Sheet length reuse and promotion, Director fire-and-forget cue commands and pass-local consumption, Switcher refusing mutation after lock, and Switcher execution that does not depend on Director-computed transition timing.
