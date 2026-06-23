# Phrase Cue Sheets drive Synced Mode; the Switcher executes Armed Cues

ADR-0004 split musical direction from mechanical execution, but the Synced Mode cue flow still needed sharper ownership: phrase planning belongs to Cue Sheets, cue preparation belongs to the Director, and transition mechanics belong to the Switcher. We decide that On-Air Timing derives simple per-Phrase Cue Sheets from Phrase length, the Director keeps one mutable Loaded Cue at a time for the next Cue Mark, and crossing the Transition-specific Lock Point turns that Loaded Cue into an Armed Cue whose execution is owned by the Mechanical Switcher. This keeps Cue Sheet planning tied to Phrase structure, keeps Effect/Transition casting one cue at a time, and keeps Runway/Tail start/progress mechanics out of the Director.

## Considered options

- **Keep Selected Phase Boundary planning as the canonical model** — rejected because the name makes a Phrase-level cue plan sound like a Phase implementation detail, and it encourages transition timing mechanics to leak back into the Director.
- **Put a multi-cue preload queue in the Switcher** — rejected because only one Armed Cue is needed; the Director can decide the next Loaded Cue after the previous one fires, even while the previous Transition Tail is still rendering.
- **Put Effect and Transition choices inside the Cue Sheet** — rejected because the Cue Sheet is only a Phrase timing plan. Performer and Transition casting depend on the current wall state, Repertoire, and live musical events, so the Director decides them one cue at a time.

## Consequences

- A Cue Sheet is identified by the Phrase's total beat length and relative Cue Marks, not phrase name or absolute start/end beat.
- **Cue Mark** is the canonical Phrase-level term; **Selected Phase Boundary** remains current implementation vocabulary to retire or translate at the seam.
- The Switcher may use Transition Runway and Tail to execute an Armed Cue so the Transition's Impact Point lands on the Cue Mark, but it must not read Track Phase, choose Cue Marks, or cast Performers/Transitions.
- Tests should cross the real seams: Cue Sheet length reuse and promotion, Loaded Cue mutation before Lock Point, Armed Cue immutability after Lock Point, and Switcher execution that does not change timing after a Cue is armed.
