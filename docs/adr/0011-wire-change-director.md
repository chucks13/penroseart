# The Director is a wire-change reducer; the planning layer is deleted

The Synced Mode decision path had grown a per-frame planning layer that re-derived phrase windows from arithmetic and answered "can this cue still change?" in four places (the CuePlanner's timing verdict and commit memory, a Switcher re-check, and a second lock clock in seconds), with every frame-ticked consumer growing its own already-did-it latch — and a beat-lane-fresh/phrase-lane-stale snapshot could re-roll a Cue Sheet mid-phrase. We replace the layer with a small reducer: the Director wakes once per new beat, keeps exactly two Cue Sheets alive by repair, casts a Cue when a Grid carrying a Cue Mark begins, and hands it to the Switcher, which alone owns commitment. Wire values changing — not re-derived arithmetic — drive every decision.

## One wake per beat

The Director ticks only when BeatManager reports a new beat; nothing in the decision path runs per frame. The latch fields that existed solely to suppress 60 fps re-entry of ~2 Hz decisions (`lastCueBeat`, `lastChangeBeat`, `lastDropProtectedBeat`, `lastLoggedSyncedBeat`, and kin) are deleted rather than maintained.

## Two Cue Sheet slots, repaired by invariant

The Director holds a current and a next Cue Sheet and repairs them on every wake: no current sheet → build from `phrase_state`; no next sheet, or the announcement it was built from changed → build from `next_phrase_state`; phrase turned over → next becomes current and the emptied slot refills by the same check. Startup, OSC dropout, a missed announcement, and normal turnover are all the same two checks — there is no cold-join case. This supersedes ADR-0010's rebuild-on-window-identity rule: sheets are keyed to the announcement values they were built from, so timing wobble cannot re-roll a sheet — only a changed announcement can.

## A Cue Sheet is empty marks under constraints

A sheet is an index of Cue Marks over the announced phrase length: marks sit on Grid Boundaries, consecutive gaps (including the run-in to the first mark) are at least 16 and at most 64 beats, and the phrase end always carries a mark. Layout within the constraints is a random roll (energy-weighted density is the named future knob). The change cadence is thereby a sheet-construction rule; no runtime cadence gate exists anywhere downstream.

## Casting is lazy and preference-based

Marks are empty until the Grid that loads them begins; casting then reads the freshest wire truth. A Fill on this Grid or a Drop on the next Grid makes capable Repertoire *preferred*, never required — a mandate would collapse variety onto the same few capable Performers. Energy and every other wire lane are Performer/Transition inputs read from BeatManager by the Performers themselves, not Director casting inputs — superseding ADR-0010's energy-affinity casting. `track_id` is consulted for nothing — superseding ADR-0010's track-change reset; the reducer holds no cross-track state to reset.

## The re-check is the cast decision replayed

When the grid reading moves in a way it shouldn't (a skipped beat, a forward or backward jump) or the Fill/Drop evidence changes while a Cue is loaded, the Director replays the same decision: is the current cast still workable? Keep it. Not workable and the Switcher is not locked? Recast. Locked? It rides — including through a Drop announced too late to commit cleanly, which retires the separate drop-protection machinery. Manually staged choices never re-aim a loaded Cue; they wait for the next Cue Mark.

## The Switcher alone owns commitment

One lock, in the beat domain — the parallel seconds-domain lock clock is deleted. Runway/tail/lock arithmetic is private Switcher math; `TransitionBeatPlan` stops being a public type. Loading a cue answers accepted-or-not, so the Director never mirrors commitment state, and deck cards are pulled only on acceptance (previously a rejected cue burned them). The Impact Point is transition-authoring vocabulary only; no runtime type, field, or parameter carries the name.

## No decision memory

The Director records no verdicts. The Observatory reads real state — the sheets, the cast for the coming mark, the Switcher's loaded-cue status — and the trace log narrates what happened. The `CueDecision` record is deleted.

## Considered options

- **Patch the planner incrementally** (beat-gate the tick, add ±1 window-identity hysteresis, gate re-aim on event evidence, return acceptance from the Switcher) — rejected: each patch added another copy of the lock/commit question to a smear that already answered it four times, and the window-identity arithmetic being patched is exactly what the announcement-keyed sheets make unnecessary.
- **Keep drop as a placement authority** (insert or move a Cue Mark onto an unsheeted drop) — rejected: marks are placed once at sheet build; Fill/Drop only flavor casting. Phrase ends always carry a mark and drops land on phrase turnovers, so the big moments get their preferred cast without placement surgery.

## Consequences

- Deleted with their tests: the CuePlanner's planning machinery (window derivation, `CoversWindow`, cursor rewind, `EvaluateCueTiming`, commit memory), drop protection, `EnergyCasting` in the cue path, the `CueDecision` surface, and the Director's mirrored transition-beat bookkeeping.
- ADR-0006's remaining active half (Cue Sheet derivation in a Director-owned CuePlanner) is superseded; ADR-0010's window-identity, energy-casting, and track-id sections are superseded. ADR-0010's core — grid and phrase truth come from the wire, not local synthesis — is the foundation this decision completes.
