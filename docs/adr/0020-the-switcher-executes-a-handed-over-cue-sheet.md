# The Switcher executes a handed-over Cue Sheet; the Director never times a cast

Status: accepted — supersedes ADR-0019

The Director builds one track-scoped Cue Sheet per player at track load and hands the on-air player's sheet to the Mechanical Switcher when the focus changes; the Switcher executes that sheet against the on-air beat, owning Runway, Impact Point, and Tail, and checking off each Cue Mark as it fires. ADR-0019 split transition timing across both — the Director computed each mark's Runway start in order to decide when to Cast — which placed a second, invisible boundary inside every segment, so a loop straddling one re-fired the same Transition every pass. Runway is transition timing: it belongs wholly to the Switcher, and the Director selects Performers without timing anything.

## Loops, and the one condition that needs a decision

A checked-off Cue Mark that comes around again is suppressed silently — that is what stops a loop repeating a Transition. Check-offs clear on backward motion only when the loop is not rolling: rolling backward is a loop and keeps them, not rolling is a back-cue and re-performs the arrival, so a re-drop still lands. Staleness is the only condition that asks for a decision. The Switcher exposes the fact that nothing has fired since a given beat; the Director reads that on its tick and casts a one-off, which it alone can deal correctly because it alone knows the overrides a one-off must respect. The Director pulls the fact rather than the Switcher pushing it, so the executor never holds a reference to the decider.

## Considered options

- **Decide-at-cast at the last responsible moment (ADR-0019)** — rejected. It protected no edge case. `Cast` executed immediately, so the Director had to call at exactly the Runway start; "read the then-current sheet" was a justification written around that constraint rather than a requirement. Casting early is safe because a focus handover, needle-drop, or new track simply casts again, and last-cast-wins.
- **Bake each mark's Runway-start beat into the sheet** so the Director stops reading the transition catalog — rejected. It launders the coupling while keeping the second boundary, so the flicker survives behind tidier code.
- **Infer loops from beat movement** — rejected. The wire states loop status outright on a lane already decoded. The inference needed a suppression predicate plus an "early cast whose mark was never reached" flag to approximate what one boolean says.

## Consequences

- Deleted: the Director's runtime half — position following, Runway arithmetic, cast memory, and the loop-straddle suppression added to defend ADR-0019's claim that loops behaved sanely by construction.
- The Switcher reads BeatManager for beat position and loop state, and holds the in-force sheet with its check-offs. The retired loaded-cue **protocol** stays retired: no verdicts, no locks, no revocation window, and no caller mirroring its state. Holding a plan is not a lifecycle.
- The Switcher reads clock and loop lanes only. Phrase, Drop, Fill, and Energy remain casting material and stay out of it.
- Unresolved and blocking implementation: where a one-shot override mask applies now that no per-cue cast exists, and where Standalone Mode lives once the Director's runtime half is gone.
