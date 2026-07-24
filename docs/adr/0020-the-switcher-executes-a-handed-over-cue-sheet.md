# The Switcher executes a handed-over Cue Sheet; the Director never times a cast

Status: accepted, amended — supersedes ADR-0019

The Director builds one track-scoped Cue Sheet per player at track load and hands the on-air player's sheet to the Mechanical Switcher when the focus changes; the Switcher executes that sheet against the on-air beat, owning Runway, Impact Point, and Tail, and checking off each Cue Mark as it fires. ADR-0019 split transition timing across both — the Director computed each mark's Runway start in order to decide when to Cast — which placed a second, invisible boundary inside every segment, so a loop straddling one re-fired the same Transition every pass. Runway is transition timing: it belongs wholly to the Switcher, and the Director selects Performers without timing anything.

## Loops, and the one condition that needs a decision

A checked-off Cue Mark that comes around again is suppressed silently — that is what stops a loop repeating a Transition. Check-offs clear on backward motion only when the loop is not rolling: rolling backward is a loop and keeps them, not rolling is a back-cue and re-performs the arrival, so a re-drop still lands. Staleness is the only condition the plan itself cannot answer. When nothing has fired for the staleness window the Switcher asks the Director for a one-off, which the Director alone can deal correctly because it alone knows the overrides a one-off must respect.

Commands go down and questions go up. The Director hands over sheets and pushes the paths that bypass a sheet; the Switcher asks whenever performing the plan needs a decision, and performs the answer on its own timeline. Hold, one-shot overrides, Standalone, and staleness are one class of thing — control paths that used to work by suspending the Director — and they resolve through one authority rather than four mechanisms.

## Considered options

- **Decide-at-cast at the last responsible moment (ADR-0019)** — rejected. It protected no edge case. `Cast` executed immediately, so the Director had to call at exactly the Runway start; "read the then-current sheet" was a justification written around that constraint rather than a requirement. Casting early is safe because a focus handover, needle-drop, or new track simply casts again, and last-cast-wins.
- **Bake each mark's Runway-start beat into the sheet** so the Director stops reading the transition catalog — rejected. It launders the coupling while keeping the second boundary, so the flicker survives behind tidier code.
- **Push decisions down instead: the Director sets a freeze flag and one-shot override masks on the Switcher, and pulls the staleness fact back on its tick** — rejected, and this ADR originally specified the pull half of it. It keeps the executor free of any reference to the decider, but only by moving three pieces of policy into the executor: a frozen flag, mask slots, and the consume-on-first-fire rule. Overrides decided it. A one-shot mask has to apply at the moment a cue fires, and firing now belongs to the Switcher, so pushing the mask down puts selection policy in the thing that is supposed to select nothing. Asking costs a reference the Switcher already needs for nothing else; the Switcher asking is not the Switcher deciding.
- **Infer loops from beat movement** — rejected. The wire states loop status outright on a lane already decoded. The inference needed a suppression predicate plus an "early cast whose mark was never reached" flag to approximate what one boolean says.

## Consequences

- Deleted: the Director's runtime half — position following, Runway arithmetic, cast memory, and the loop-straddle suppression added to defend ADR-0019's claim that loops behaved sanely by construction.
- The Switcher reads BeatManager for beat position and loop state, and holds the in-force sheet with its check-offs. The retired loaded-cue **protocol** stays retired: no verdicts, no locks, no revocation window, and no caller mirroring its state. Holding a plan is not a lifecycle.
- The Switcher reads clock and loop lanes only. Phrase, Drop, Fill, and Energy remain casting material and stay out of it.
- The Switcher holds a reference to the Director and asks it two questions: what to perform for a due Cue Mark, and what to perform when the wall has gone stale. A refusal is how Hold reaches the executor, so the freeze needs no path of its own. The reference is mutual, since the Director still pushes the immediate and Standalone paths down, and it is bound after construction rather than injected.
- One-shot override masks apply when the Director answers, not when a sheet is built, so the plan stays a pure function of (structure, seed) and an operator pick still lands on exactly the next cue.
- Standalone Mode needs no home in the Switcher: no structure means no sheet, the Director hands over a default sheet that clears the plan in force, and it drives the cadence itself as before.
