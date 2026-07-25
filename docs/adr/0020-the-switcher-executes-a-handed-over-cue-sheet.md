# The Switcher executes a handed-over Cue Sheet; the Director never times a cast

Status: superseded by ADR-0021

This ADR supersedes ADR-0019's Director/Switcher split, and is itself superseded by ADR-0021, which extracts the two
rulings that survive its own reversing amendments. Read for the record: the sections below argue with each other,
and the Standby Cue and Missed Cue they describe are no longer in the code.

The Director builds one track-scoped Cue Sheet per player at track load and hands the on-air player's sheet to the Mechanical Switcher when the focus changes; the Switcher executes that sheet against the on-air beat, owning Runway, Impact Point, and Tail, and checking off each Cue Mark as it fires. ADR-0019 split transition timing across both — the Director computed each mark's Runway start in order to decide when to Cast — which placed a second, invisible boundary inside every segment, so a loop straddling one re-fired the same Transition every pass. Runway is transition timing: it belongs wholly to the Switcher, and the Director selects Performers without timing anything.

## A check-off is permanent, so backward motion means nothing

A performed Cue Mark is checked off for the life of the handover and nothing clears it. A loop, a back-cue, and a needle-drop are then the same uneventful thing: a stretch of plan that has already been performed. This amends the original rule, which cleared check-offs on backward motion when the loop was not rolling so a re-drop would "land" again. That rule quietly made the plan a statement of what must be on the wall at a given beat, and the Switcher enforced it — every back-cue dragged the wall back to whichever Effect owned that part of the track, and an operator's pick could not survive one. A Cue Sheet says what to perform *at* a beat, once. The Switcher switches from what is on the wall to what comes next; it never restores.

The Switcher therefore reads no loop lane at all, and the deleted rule takes the loop-straddle suppression with it. Staleness remains the only condition the plan itself cannot answer. When nothing has fired for the staleness window the Switcher asks the Director for a one-off, which the Director alone can deal correctly because it alone knows the overrides a one-off must respect — and after a back-cue into already-performed plan, that is the path that has to keep the wall alive until the next unfired mark. The section below amends how that asking works, because as first specified it did not.

## A staleness cue asks again, arrives with a Runway, and is dealt something new

Staleness was specified as one question asked at the ceiling, and a live run showed both halves of that to be wrong. The Switcher performed the one-off at the beat it was standing on, so the Runway start fell behind the playhead, the clamp put the start time in the past, and progress arrived at one — every staleness cue cut instantly, including the ones that did change the Effect. And the deal reads (structure, player, boundary beat) and nothing else, so a loop returning to the same boundary is dealt the same card every pass; the second ask of a rolling loop transitioned the on-air Effect to itself, which restarts it in place and moves nothing. The reset is unconditional, so that no-op spent the window and bought another window of stasis.

The Switcher therefore asks at every Grid start in the deficit rather than only at the last one, and carries how far the deficit has run so the Director can decline early and is obliged at the maximum spacing. A granted cue sets its Impact Point one Runway ahead of the Grid start, which is the whole of the fix for the cut: the Runway then begins exactly now and the Transition plays out in full. A staleness cue therefore *starts* on a Grid Boundary where a planned cue *lands* on one — an interjection announces itself, and the alternative is scheduling a start in the future, which the clamp in the performing path deliberately forbids.

Two counts travel with the question, because one cannot do both jobs. How far the current deficit has run drives the declining, and it restarts at one after every performed cue — which is exactly why it cannot also seed the deal: a loop starving twice at one boundary would ask with the same number both times and be handed the same card again, the original defect surviving its own fix. The total number of asks since the handover seeds the deal instead, and being monotonic it never repeats. A plan mark stays a pure function of (structure, seed); a staleness cue is a pure function of (structure, seed, boundary, ask sequence).

None of this reads a loop lane, and it does not reopen that question. A fired mark can only be approached a second time by moving backward, so the re-approach is the signal; and a loop sitting inside a legal gap contains no mark to approach at all, which the Grid-start deficit already covers. The plan is still never rewritten — a granted cue borrows the fired mark's beat as an Impact Point, and checks nothing off, because a check-off is still permanent.

## The immediate override is a performed move

Commands go down and questions go up. The Director hands over sheets and pushes the paths that bypass a sheet; the Switcher asks whenever performing the plan needs a decision, and performs the answer on its own timeline. Hold, one-shot overrides, Standalone, and staleness are one class of thing — control paths that used to work by suspending the Director — and they resolve through one authority rather than four mechanisms.

Of those pushed paths, Show Now — a keyboard jump, an OSC button, or engaging a Held Effect — starts a real Transition into the picked Effect, begun at that instant with the staged card and no Runway, because an off-grid interjection has no Impact Point to fly toward. It used to cut, and it used to clear the sheet slots and re-cast on the way past, which reset every check-off and let the plan re-fire on the very next frame and snap the wall back off the pick. Neither belonged: the Switcher has no cut path at all now, and an operator interjection leaves the plan in force to resume at its next unfired mark.

## Considered options

- **Decide-at-cast at the last responsible moment (ADR-0019)** — rejected. It protected no edge case. `Cast` executed immediately, so the Director had to call at exactly the Runway start; "read the then-current sheet" was a justification written around that constraint rather than a requirement. Casting early is safe because a focus handover, needle-drop, or new track simply casts again, and last-cast-wins.
- **Bake each mark's Runway-start beat into the sheet** so the Director stops reading the transition catalog — rejected. It launders the coupling while keeping the second boundary, so the flicker survives behind tidier code.
- **Push decisions down instead: the Director sets a freeze flag and one-shot override masks on the Switcher, and pulls the staleness fact back on its tick** — rejected, and this ADR originally specified the pull half of it. It keeps the executor free of any reference to the decider, but only by moving three pieces of policy into the executor: a frozen flag, mask slots, and the consume-on-first-fire rule. Overrides decided it. A one-shot mask has to apply at the moment a cue fires, and firing now belongs to the Switcher, so pushing the mask down puts selection policy in the thing that is supposed to select nothing. Asking costs a reference the Switcher already needs for nothing else; the Switcher asking is not the Switcher deciding.
- **Infer loops from beat movement** — rejected, and now moot. It was rejected because the wire states loop status outright on a lane already decoded, and the inference needed a suppression predicate plus an "early cast whose mark was never reached" flag to approximate what one boolean says. Permanent check-offs retire the whole question: neither the inference nor the boolean is read, because a loop needs no handling to behave.

## Consequences

- Deleted: the Director's runtime half — position following, Runway arithmetic, cast memory, and the loop-straddle suppression added to defend ADR-0019's claim that loops behaved sanely by construction.
- The Switcher reads BeatManager for beat position, and holds the in-force sheet with its check-offs. The retired loaded-cue **protocol** stays retired: no verdicts, no locks, no revocation window, and no caller mirroring its state. Holding a plan is not a lifecycle.
- The Switcher reads clock lanes only. Loop, Phrase, Drop, Fill, and Energy stay out of it — the first because it now decides nothing, the rest because they remain casting material.
- A back-cue into already-performed plan leaves a gap: marks behind the furthest point reached stay consumed, so nothing fires until the playhead reaches the next unfired mark. Marks ahead are unaffected, and staleness covers the gap at the maximum Cue Mark spacing. This is accepted rather than mitigated; re-performing on arrival is the behavior being removed.
- The Switcher holds a reference to the Director and asks it two questions: what to perform for a due Cue Mark, and what to perform when the wall has gone stale. A refusal is how Hold reaches the executor, so the freeze needs no path of its own. The reference is mutual, since the Director still pushes the immediate and Standalone paths down, and it is bound after construction rather than injected.
- One-shot override masks apply when the Director answers, not when a sheet is built, so the plan stays a pure function of (structure, seed) and an operator pick still lands on exactly the next cue.
- Staleness is a cadence rather than a single shot: every Grid start in the deficit is an ask, a decline costs nothing and does not reset the count, and only a performed cue does.
- A staleness cue takes the handover's running ask count as a seed input, so it stays reproducible without being constant. A loop held across several windows is dealt a different card each window, which is the only way the remedy can move a wall it has already moved once.
- Waiting is rolled from the sheet's own stream rather than judged from the music. The Director declines three asks in four at the first Grid start of a deficit and none at the ceiling, so the wall usually changes somewhere between half and all of the maximum spacing instead of on every boundary. Phrase and energy were the alternative and remain available; the roll was chosen because it needs no lane the Director does not already hold and keeps the escalation testable.
- A staleness cue is a performed Transition with a Runway, because it is anchored to a beat ahead of the playhead. Show Now still has no Impact Point to fly toward and still begins at the instant it is pushed; the two paths differ deliberately.
- Standalone Mode needs no home in the Switcher: no structure means no sheet, the Director hands over a default sheet that clears the plan in force, and it drives the cadence itself as before.

## Amendment 2026-07-24 — a cue stands by for its Runway beat, and one that missed it is missed

This ADR rejected ADR-0019's decide-at-cast as protecting no edge case, and treated the start-time clamp in
the performing path as a constraint that forbids waiting for a future beat. Both were wrong, and the live logs
showed the cost: every handover performed the furthest mark behind the playhead as an instant cut, four times
in twenty seconds, because clearing check-offs made the whole past due again and the clamp turned an elapsed
Runway into progress of one.

What decide-at-cast protected was the guarantee that a cue's Runway begins before its Impact Point. An
inequality — this mark's Runway beat has passed — cannot tell *beginning now* from *ended nine seconds ago*;
only waiting for the beat can. The clamp arrived with the immediate `Cast` entry point rather than being
derived from anything, so citing it as a law inverted a workaround into a constraint.

The Switcher therefore holds one **Standby Cue**: a decided cue plus the beat its Runway begins, fired when
the playhead reaches that beat. A Cue Mark stands by on `Impact − Runway` and so fires the same frame; a
staleness cue stands by at the Grid start that asked it and waits for the next Grid Boundary. That retires
this ADR's claim that a staleness cue *starts* on a Grid Boundary where a planned cue *lands* on one — both
land on one now, and the deliberate difference between them is gone. A mark whose Runway beat is already
behind the playhead is a **Missed Cue**: checked off unperformed, because a cue is its Runway, Impact Point,
and Tail, and one that cannot fly its Runway cannot be performed as written. The clamp is deleted — a cue only
fires once the playhead has reached its Runway beat, so the anchored start time is never ahead of now.

This is not the retired loaded-cue protocol returning. There is no lock, no verdict, and no revocation window;
holding a beat to wait for is not a lifecycle.

## Amendment 2026-07-25 — boundaries are counted, and an off-plan cue fires where it is asked

Two claims above are now wrong, and one name with them.

The first is the claim that a re-approached fired mark is signal enough: "a loop sitting inside a legal gap
contains no mark to approach at all, which the Grid-start deficit already covers." It did not cover it, because
after the Standby Cue amendment there was no deficit count left — the ask happened only where a fired mark was
re-crossed. A DJ looping a stretch the plan left empty, and an inspection freeze whose release leaves every
covered mark behind the playhead, both hold the wall still indefinitely. That breaks the plan's own spacing
rule, which is the promise that the wall changes at least every four Grids.

The Switcher therefore counts **Grid Boundaries crossed since the last Impact Point**, and asks at the boundary
that spends the fourth. Boundaries rather than beats, because a loop re-crosses the same beat numbers and only
crossings measure elapsed music; a Grid Boundary is the Grid lane returning to one, which is phrase-relative,
so a shortened phrase restarts the count and the ceiling follows the music. Below the ceiling nothing is asked
except at a re-crossed mark, so an off-plan cue can never pre-empt a plan the playhead is still walking through.
Both counters — this one and the running ask count that seeds the deal — belong to the handover and restart with
it.

The second wrong claim is that the deal needs a card at all costs: "the second ask of a rolling loop
transitioned the on-air Effect to itself." That defect outlived its own fix, because the ceiling deal is
*certain* and a certain deal can still hand back the Effect already on the wall — a live run at the ceiling
dealt A→A and bought another four Grids of stasis. The deal now excludes whatever the wall is showing or moving
toward, using the bag's existing preferred-card dig rather than a retry loop.

That also retires the two-counts distinction drawn above. How far the deficit has run and how many times the
handover has asked are still both passed, but the first is now the Switcher's boundary count rather than a
Director-side ride tally, and the Director keeps no state between asks at all: it declines by rolling the
sheet's own stream, so nothing has to be remembered to escalate.

Finally, **staleness** is retired as vocabulary. It named the deficit counter that no longer exists and
described a symptom on the wall; the cue is now named for what it is — an **Off-Plan Cue**, the one cue that
does not come from the sheet.
