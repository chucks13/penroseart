# Phrase Cue Sheets drive Synced Mode; the Switcher owns cue lifecycle

ADR-0004 split musical direction from mechanical execution, but the Synced Mode cue flow still needed sharper ownership: phrase planning belongs to Cue Sheets, cue direction belongs to the Director, and cue lifecycle/execution belongs to the Switcher. We decide that On-Air Timing derives simple per-Phrase Cue Sheets from Phrase length. The Director configures one cue direction at a time for the next Cue Mark: target Cue Mark, destination Performer, and selected Transition. At the cue window, the Director sends that cue direction to the Mechanical Switcher fire-and-forget and records pass-local Cue Mark consumption from its own command. The Switcher holds/schedules the Loaded Cue, derives the Transition-specific Lock Point, refuses conflicting updates once locked, and executes the resulting Armed Cue so the Transition Impact Point lands on the Cue Mark.

This keeps Cue Sheet planning tied to Phrase structure, keeps Effect/Transition casting one cue at a time, and keeps Lock Point, Runway/Tail start/progress mechanics, and Unity-time conversion out of the Director.

## Amendment 2026-06-23

We refined the cue handoff to be fire-and-forget from the Director's perspective. The original ownership decision still stands, but the Director no longer polls Switcher mutability or lifecycle state; it sends cue-window directions and records pass-local consumption from its own command while the Switcher schedules, locks, starts, and completes the cue internally.

## Amendment 2026-06-24

We refined Synced Mode cue meaning without changing Cue Sheet ownership. A Cue Sheet remains a list of scheduled Cues derived from the Phrase's total beat length; each Cue has a phase-aligned Cue Mark where it lands. BeatManager Fill/Drop state does not create or move Cue Marks. `SyncedCueIntent` derives ordinary, Fill, or Drop Cue Intent by comparing the current Timing Frame's Cue Mark with BeatManager Fill/Drop phrase-event timing. The Director uses that intent to cast event-capable Effects and Transitions from Repertoire for the current scheduled Cue. If a preferred Transition cannot serve that Cue Mark now, the Director keeps the staged Transition instead of delaying the cue. The Switcher still receives only the chosen cue direction and executes it from Runway/Tail.

## Amendment 2026-06-24 — On-Air Timing becomes a Phase determiner; Cue Sheet derivation relocates to the Director (ADR-0006)

ADR-0006 makes On-Air Timing a pure Phase determiner and relocates Cue Sheet derivation — together with the cue cursor, change cadence, and pass-local cue memory — out of On-Air Timing into a Director-owned CuePlanner. This ADR's ownership split still stands: Cue Sheet planning stays tied to Phrase structure, the Director casts one cue at a time, and the Switcher owns cue lifecycle. Only the home of the derivation moves — On-Air Timing no longer builds Cue Sheets; the Director's CuePlanner does, consuming the read-only Phase/Phrase reading. The relocation lands during the ADR-0006 implementation.

## Amendment 2026-07-01 — One live Cue Sheet; the upcoming-sheet lifecycle is deleted

We removed the CuePlanner's pre-planned upcoming sheet, its promotion at Phrase turnover, and the cursor surgery that kept the mandatory boundary alive across that promotion. One live Cue Sheet now serves all planning:

- The sheet is **rebuilt** when the Phrase length changes, or when first adopting a Phrase that has not started yet (a look-ahead window). A sheet built before its Phrase starts rolls the start beat — the Track Phase boundary itself — in as a Cue Mark when cadence allows; this replaces the upcoming sheet's phrase-start option.
- The sheet is **reused by length identity** (`CueSheet.Matches`) and re-anchored with a cursor rewind for same-length windows already underway. Timing shifts and same-length turnover replay the same mark pattern from its first mark — no reroll.
- A **pending unconsumed mandatory boundary is held** as the frame target from its beat through the late-cue window before the sheet moves to the next window, so cueing on the exact boundary beat (or late, backdated) survives Track Phase promoting the next Phrase on that beat.
- A sheet whose mandatory end was **consumed stops driving**; the next window — live or look-ahead — takes over. Until the reading actually leaves the fired Phrase, the frame stays on the consumed mark (previously a cached preplanned sheet was promoted immediately); look-ahead interior marks start at least one Grid past the boundary, so no cue opportunity is lost in that gap.

Cue Sheet identity and ownership are unchanged: identity is still the Phrase's total beat length, planning still lives in the Director-owned CuePlanner, and the Switcher still owns cue lifecycle.

## Amendment 2026-07-04 — one fresh Cue Sheet per phrase change (ADR-0010)

The length-identity reuse from the 2026-07-01 amendment (`CueSheet.Matches`, the cursor reanchor) is deleted. With `next_phrase_state` always on the OSC v2 wire, the CuePlanner builds a fresh Cue Sheet on every phrase change — same-length turnover included — and the upcoming window uses the true next-phrase length instead of guessing it from the current length (ADR-0010). Ownership is unchanged: identity is still the Phrase's beat length, planning still lives in the CuePlanner, and the Switcher still owns cue lifecycle.

## Amendment 2026-07-05 — planning machinery superseded by ADR-0011

The CuePlanner and its lifecycle amendments above (2026-07-01's one-live-sheet rules, 2026-07-04's rebuild-per-phrase-change) are superseded by the wire-change Director: two announcement-keyed Cue Sheet slots (current and next) repaired on every beat, marks empty until Cast at Grid entry, and no pass-local consumption memory — the Director records no decisions. This ADR's ownership split is what survives and still governs: the Cue Sheet is a Phrase timing plan holding no Effect/Transition choices, the Director casts one Cue at a time and sends it fire-and-forget, and the Switcher alone owns Loaded → Locked → Executing.

## Amendment 2026-07-24 — the empty phrase-scoped Cue Sheet is superseded by ADR-0019

The phrase-scoped Cue Sheet this ADR defined — an index of empty Cue Marks over one Phrase, with Effect/Transition choices made one cue at a time — is superseded by ADR-0019's track-scoped Cue Sheet, which bakes Effect and Transition assignments into a full-length plan built once per track load. The Switcher's Loaded → Locked → Executing lifecycle is retired with it: casting is fire-and-forget with no loaded-cue lock protocol. What survives conceptually is the ownership split — planning is not the Switcher's job, and mechanical Runway/Impact/Tail timing is not the Director's.

## Considered options

- **Keep Selected Phase Boundary planning as the canonical model** — rejected because the name makes a Phrase-level cue plan sound like a Phase implementation detail, and it encourages transition timing mechanics to leak back into the Director.
- **Put a multi-cue preload queue in the Switcher** — rejected because only one mutable cue direction is needed at a time; the Director advances through the Cue Sheet one Cue Mark at a time instead of preloading a full future queue.
- **Keep Loaded Cue state in the Director and call `ArmCue` at Lock Point** — rejected because it leaves the Director deciding Lock Point and arming, which is execution lifecycle policy. The Director may author and update the cue direction, but the Switcher owns Loaded → Locked/Armed → Executing.
- **Put Effect and Transition choices inside the Cue Sheet** — rejected because the Cue Sheet is only a Phrase timing plan. Performer and Transition casting depend on the current wall state, Repertoire, and live musical events, so the Director decides them one cue at a time.

## Consequences

- A Cue Sheet is identified by the Phrase's total beat length and the relative Cue Marks for its scheduled Cues, not phrase name or absolute start/end beat.
- **Cue Mark** is the canonical Phrase-level term; **Selected Phase Boundary** remains current implementation vocabulary to retire or translate at the seam.
- The Director chooses Cue Mark, destination Performer, and Transition, then sends that cue direction to the Switcher fire-and-forget at the cue window.
- The Director records pass-local Cue Mark consumption from its own sent command and must not inspect Switcher Loaded/Locked/Started state as scheduling input.
- The Switcher owns the Loaded Cue slot, derives Lock Point from the selected Transition's Runway, locks/refuses conflicting updates, and executes the Armed Cue using Runway/Tail so the Transition's Impact Point lands on the Cue Mark.
- The Switcher must not read Track Phase, choose Cue Sheets, choose Cue Marks, or cast Performers/Transitions.
- The Director must not compute or choose Lock Point, Unity start time, duration seconds, transition progress, or other mechanical execution timing for Synced Mode.
- Tests should cross the real seams: Cue Sheet length reuse and promotion, Director fire-and-forget cue commands and pass-local consumption, Switcher refusing mutation after lock, and Switcher execution that does not depend on Director-computed transition timing.
