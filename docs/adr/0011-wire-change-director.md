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

## Amendment 2026-07-05 — announcement values are identity; positions are watched by expectation

The first cue-log session showed the Director re-rolling the next Cue Sheet ~30 times in three minutes while the announced label and length never changed. The cause was that sheet identity was still a *derived* number — `phraseStartBeat = beat + beatsUntilChange` — and turnover was the Director's own `beat >= PhraseEndBeat` arithmetic: a one-beat snapshot skew between the wire's beat counter and its countdown reads as a changed announcement and re-rolls the sheet. Both re-derived a position and treated it as truth, which this ADR's own thesis forbids.

This amendment makes the split exact. A Cue Sheet's identity is the **announced label and length, nothing else**. The Director watches three lanes by expectation — grid (16 / beat / —), phrase_state (length / beats-until-next / label), and next_phrase_state (length / beats-until-change / label) — where each wake the position should advance by one and everything else should hold. **Boundary values are wraps, never zeros: there is no zero in music counting; the count after 16 is 1 of the next Grid, and the beat a countdown "would hit 0" is beat 1 of the next Phrase.** An expected phrase wrap is the turnover — the next sheet shifts to current and the emptied slot refills — decided by the countdown wrapping, never by an end-beat comparison. An unexpected announcement change rebuilds only the lane that changed: a changed next announcement rebuilds the next sheet; a changed current announcement re-evaluates the current sheet, keeping it when its label and length still match the wire. A sheet holds only Phrase-relative Cue Mark offsets; its roll seed is deterministic from the announcement, and **no absolute anchor is captured — not at build, not at shift, not ever**. Position is read live from the phrase lane at each wake, so position wobble on an unchanged announcement has nothing stored to invalidate and can never re-roll the sheet. The one absolute beat a Cue needs is minted at the Switcher seam when the offer is made — mark placement, never identity and never a decision input.

The re-check narrows back to a single fire-and-forget seam. Identity on the Director → Switcher seam is the **Cue Mark alone**: `UpsertLoadedCue` treats an offer at the same Cue Mark as a **keep** — the loaded cue rides unchanged and is never re-flavored — replaces the loaded cue on a different mark when it can still commit and is not locked, and otherwise rejects. The Switcher answers in one call (kept / loaded / rejected), so the Director no longer runs its own keep-guard: the `IsLoadedCueWorkable` re-check and its `LoadedCueStatus` decision-read are deleted (the Observatory's read-only `LoadedCueStatus` view stays — it is a view, not a decision). The `Grid == null` special case in the cast path is deleted too: a null grid means the wall is not in Synced Mode, which the ADR-0007 mode fallthrough at the tick entry owns.

## Amendment 2026-07-24 — announcement-keyed empty sheets and the loaded-cue lock are superseded by ADR-0019

The two announcement-keyed Cue Sheet slots (current and next, repaired every wake with empty marks Cast at Grid entry), the lazy preference-based casting, and the `UpsertLoadedCue` keep/loaded/rejected lock seam are all superseded by ADR-0019, whose Director/Switcher split is in turn revised by ADR-0020. With the complete per-player song structure on the wire, the Director builds one track-scoped Cue Sheet per player with Effect and Transition assignments baked in and hands the on-air sheet to the Switcher, which performs it — no loaded cue, no lock, no verdict. What survives from this ADR is its thesis, and it is now total: position comes from the wire and nothing keeps a self-ticked count of its own, so the drift this ADR set out to kill cannot return.

## Note 2026-07-25 — energy casting came back once, and has been removed again

This ADR's "Casting is lazy and preference-based" rule — energy is a Performer input read from BeatManager by
the Performers themselves, not a Director casting input — was reversed without being amended. ADR-0019's
`TrackCueSheet.Build` (`8c8dd38f`, 2026-07-24) reintroduced an energy preference as one of the things living
behind the builder, and because a track-scoped plan is built at load time from structure alone, it could not
read the energy lane the deleted `EnergyCasting` had read. It substituted a phrase-label proxy instead:
`Drop`/`Chorus` prefer High, `Up`/`Verse` prefer Mid, everything else prefers Low.

That is a weaker instrument than the one this ADR deleted. `EnergyCasting` read the live level, the queued
next level, and beats-until-change, and cast ahead so a Performer was chosen for the energy it would actually
spend its stint in. The proxy instead asserts that a phrase *labelled* Drop is high energy, and then prefers
the fourteen of twenty-seven Effects that claim a High affinity at every Drop and every Chorus. The result was
the failure this ADR's own paragraph warned about one sentence earlier — variety collapsing onto the same few
Performers — arriving through a soft preference rather than the mandate that sentence forbade.

Removed. Effects are dealt from bag order at every mark; capability is asked of a ride-through carrier and of
an Anchor's Transition, and nothing else filters a deal. `EnergyFlagFor` and the phrase-type lookups that fed
it are deleted, and a test now pins that two catalogs differing only in energy affinity plan an identical
show. The affinity flags stay on the Performers as advertised declarations with no casting consumer.

This rule has now been undone twice by work that did not notice it. If energy should influence casting, it
needs a decision that replaces this one, and it needs the live lane rather than a label.

## Amendment 2026-07-05 — irregular phrase lengths are first-class; the boundary read is wire-first

The 15:33 cue-log session announced phrase lengths that are not Grid multiples (`Up/24`, `Down/8`, `Outro/41`, `Chorus/56`): these are sender truth — `phrase_state` carries the length verbatim and ships a tri-state `irregular` flag meaning "not divisible by 16" — yet the Director refused them, building no sheet and riding the no-sheet fallback so the wall only changed at the phrase end. The same log proved the fact that makes real support clean: the wire **re-anchors the timing grid at every phrase boundary** (the 24-beat `Up` phrase turned over at `grid=1/16`), so a phrase end is always the next phrase's downbeat — a Grid Boundary — even when the phrase's own length is not a 16-multiple. Irregular lengths are therefore first-class: `CueSheet.Build` accepts any positive length, keeping interior marks on 16-multiples (the regular roll stream is byte-identical) and letting one rolled run-out Grid absorb the odd tail before the mandatory end mark, which is a Grid Boundary by re-anchor. And the Director's next-boundary read stops extrapolating a full Grid: it is now the **min of the grid lane's extrapolation and the phrase countdown**, so on an irregular phrase's final partial grid the boundary arrives when the wire says it does — the re-anchor read straight off the lanes, never our own grid synthesis. The usability guard that skipped irregular lengths is deleted.
