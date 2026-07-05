# On-Air Timing is a Phase determiner; PhaseLock holds the one and Phrase wins the grid

_Superseded: the determiner half by ADR-0010 (2026-07-04), the CuePlanner half by ADR-0011 (2026-07-05)._

`OnAirTiming.cs` tangles a universal job — determining Phase, "where is the one" on the 16-beat grid — with a Director job: Cue Sheet planning (`CueSheetPlans`, `CueSheetCursor`, and pass-local cue memory round-tripped through the Director every frame). We split them. On-Air Timing becomes a **pure Phase determiner** exposed as two deep modules: a reusable **PhaseLock** core that answers where the one is, and a **PhraseTracker** that rides on it for song structure. Cue Sheet planning moves out to a Director-owned **CuePlanner** (relocating the derivation ADR-0005 placed in On-Air Timing). The full operational design lives in the companion doc `docs/architecture-reviews/on-air-timing-redesign-2026-06-24.html`; this ADR records the decisions and why.

## Status

Amended 2026-07-01 — the pass-local round-trip residue is deleted, finishing this ADR's relocation:

- The per-beat cue timing verdict (wait / cue / blocked-on-cadence) is now answered by the CuePlanner itself (`EvaluateCueTiming`) from its own pass-local cue/cadence memory. `PassLocalTimingState` no longer exists and the `TimingFrame` carries no pass-local echo — the frame said back to the Director what the planner already knew, and `SyncedCueIntent` re-derived the verdict from that echo. `SyncedCueIntent` is now the casting half only (event classification and Performer choice); it never answers "when".
- Change-cadence checks all live inside the CuePlanner (`CanChangeAt` and the Cue Sheet build predicate); the Director's status trace asks the planner instead of re-running the cadence rule.

Amended 2026-06-25 during slice 04c implementation:

- The phrase-absent fallback changed from the end-aligned `total_beats mod 16` grid to a start-aligned `beat mod 16` (offset 0). `total_beats` is end-of-track *length*, not phase information, and the running `beat` already rides the always-present 4-count — so grounding the fallback on the beat is the honest guess and let `total_beats` drop out of the Phase input entirely. The sections below describe the shipped `beat mod 16` fallback.
- The per-layer `beatsSinceAnchor` staleness count was dropped: no consumer ever thresholded it (the promised Director use never materialised), so it was speculative interface surface.

## The 4-count is bedrock; the running beat is position and the phrase-absent fallback

`beat_in_bar` (the 4-count) is the clock we ground on: always present, locally defined, and loop-invariant for any loop ≥ 1 bar. It is the driving pulse, not — as the deleted stateless `PhaseClock` ranked it — the weakest signal; that ranking judged only its weak second role as an anchor for *which* bar carries the one. The running `beat` counter supplies position. When Phrase data is absent the grid lines up on the running `beat` itself — offset 0, so position is `beat mod 16` — a best-guess fallback grounded on the always-present 4-count, never the track length. (`total_beats` is the track *length*, a per-track constant; it carries no running position and is not consulted for Phase.) Drop, energy, and fill carry no Phase information and are not consulted either.

## The one is a held offset; Phase position is recomputed every frame

Phase is `offset` applied to the running beat: `position = ((beat − 1) − offset) mod 16 + 1`. `offset` (where the one sits in the 16-grid) is **held** and re-latched only at structural triggers; `position` is **recomputed every frame** from the current beat against that held offset. Because position tracks true playback, a loop — a bar-aligned backward `beat` jump — is absorbed for free: there is **no explicit loop detection**, no rewind threshold, and no "dead zone." This replaces the deep-dive's brittle 16-beat backward-jump heuristic.

## Phrase decides the grid, grounded on the 4-count tick; boundaries are detected by phrase-start advance

The Phrase decides where the 16-grid starts: On-Air Timing latches `offset` from the Phrase boundary (the Phrase start *is* a one), and position then dead-reckons off the running `beat` until the next Phrase boundary re-latches. The re-latch is grounded on the **tick**, not on the previous offset: a real Phrase start is a downbeat, so the grid the new offset implies must agree with the feed's `beat_in_bar`. When it agrees the offset re-latches; when a Phrase start lands off the tick — or a held grid drifts off it (a sub-bar flub) — that is a Phrase-vs-pulse disagreement, held and flagged CONTRADICTED rather than silently applied. Anchoring on the tick rather than on "the offset shifted a whole bar from the last one" is what keeps this correct when the held offset is stale (a track change) and when the `beat` counter does not itself start on a downbeat. This makes lead-ins and non-power-of-two Phrases the same mechanism — re-latch every boundary, flag a Phrase that ends off the 16-grid as irregular — with no special case. A Phrase boundary is detected when the derived `phraseStart = beat − (lengthBeats − beatsUntilNext)` advances, not by label change (adjacent Phrases share labels) nor by a countdown reaching zero (fragile across dropouts). Phrase look-ahead (predict-then-confirm of the next boundary from `beatsUntilNext` + the upcoming Phrase's `lengthBeats`) is a PhraseTracker enhancement; PhaseLock reacts at the boundary.

## A track change is a clean re-acquisition, not a carry-over

`beat` is a per-track counter, so the held `offset` — meaningful only relative to the previous track's counter — carries no information about the new song's grid. On a track-title change On-Air Timing therefore **resets**: it drops the held offset, boundary memory, and anchor and re-acquires from the new song's own Phrase, exactly as it does at the start of any track. The `beat mod 16` fallback (offset 0) covers the gap until the new song's first Phrase boundary lands. Phase correctness no longer depends on any cross-track state.

## Degradation is per-layer with a stand-alone floor

Confidence degrades top-down and never loses the floor: the pulse is ≈always locked while a clock exists, while Phase and Phrase each carry their own LOCKED / COASTING / CONTRADICTED state. There is no global "Unlocked" inside synced mode. The floor is the clock itself: when the beat signals read `-1` (no `beat_in_bar`), On-Air Timing exits to stand-alone Mode (the Director's free-running timer, ADR-0004) — a mode exit, not a degraded Phase state. With a clock but no Phrase data we stay synced and COASTING/CONTRADICTED, and what to do there is the Director's decision, not the timing core's.

## All timing metrics are integer beats

Every value PhaseLock holds or emits is a whole-beat integer (`offset`, `position`, `beatsUntilNext`, `lengthBeats`, `phraseStart`). The model is correct because it is exact integer modular arithmetic; floats would invite drift and fuzzy comparisons in grid math that must be crisp. The rhythm system's existing floats (`progress`, `IntraBeatFraction`, anticipation ramps) are BeatManager presentation-smoothing and stay out of the Phase core.

## Considered options

- **A multi-signal voting / confidence-estimator arbiter** (the earlier locked framing) — rejected as more machinery than the problem needs. Phrase already encodes the downbeat and lead-in that beat-in-bar and drop edges would corroborate, so extra authorities are redundant and reintroduce the conflicting-winner problem. Two authorities — running `beat` for position, Phrase for the offset — grounded on the 4-count, suffice.
- **Keep the stateless per-frame `PhaseClock`** — rejected because it cannot coast through a Phrase-data dropout and cannot detect a contradiction (a freshly derived offset disagreeing with the held one), which is the tripwire for loops, non-power-of-two Phrases, and track changes.
- **Absolute-beat cue bookkeeping with explicit loop-folding** — moot once cue planning leaves On-Air Timing, but rejected in principle: held-offset + recomputed-position makes loops free, where absolute beats carry loop-detection logic and its dead-zone failure forever.
- **Soft-hold the offset across a track change** (an earlier draft of this ADR) — rejected once it was clear `beat` is a per-track counter. The held offset is meaningful only against the old track's `beat`; applied to the new song's counter it is a stale number, not a continuation. Soft-holding it forces special-case gate-bypass logic and can lock the new song onto a wrong one. We reset and re-acquire instead; the new song's first Phrase boundary re-anchors, with the `beat mod 16` fallback covering the gap.
- **Treat drop/energy edges as Phase re-anchors** (an earlier draft) — rejected: they are section signposts, not Phase information.

## Consequences

- Cue Sheet derivation, the cue cursor, cadence, and pass-local cue memory move from On-Air Timing to a Director-owned CuePlanner; ADR-0005's "On-Air Timing derives Cue Sheets" is relocated, not its ownership split. What the Director does with Phase is immaterial to the timing core, which only emits a read-only reading.
- PhaseLock consumes BeatManager's already-projected integer values (the path `OnAirTiming.ReadFrame` takes today), not raw OSC, and reads Phrase numerics directly so it does not depend on PhraseTracker.
- The reading seam exposes Phase and Phrase separately (`PhaseReading` + `PhraseTrackerReading` — named one word apart so the two never alias in a site that holds both); exact fields are implementation detail.
- The seam is justified by testability: PhaseLock can be property-tested against scripted DJ timelines (loops of every length, lead-ins, non-power-of-two Phrases, seeks, dropouts, track changes, clock loss) independent of the Director. Effect-level consumption of the one is allowed to emerge; no speculative extension points are added for it.
- Rig-empirical unknowns remain to verify but do not block the ADR or harness: whether Phrase data arrives before the boundary, whether Phrase data is always sent, the Phrase-boundary timing noise profile, how often the 4-count wobbles, and the unit of `phrase_state.remaining`.

## Amendment 2026-06-29

The cyclic 16-beat "Phase" vocabulary in this ADR was renamed to "Grid" (PhaseLock→GridSync, PhaseReading→GridReading, Phase Anchor→Grid Anchor) per ADR-0009, to end the PhaseInfo/PhraseInfo collision. The determiner decision is unchanged.

## Amendment 2026-07-04 — the determiner is superseded by ADR-0010

GridSync (the held-offset determiner) and PhraseTracker are deleted: OSC v2 broadcasts a source-computed `timing_grid` and always-present `phrase_state`/`next_phrase_state`, so the wall reads grid and phrase truth off the wire instead of determining them (ADR-0010). This ADR's other half stands — On-Air Timing's Cue Sheet derivation still lives in the Director-owned CuePlanner, now fed by a five-integer `OnAirTimingInput` mapping `BeatManager.Phrase`/`NextPhrase` rather than the deleted `GridReading`/`PhraseTrackerReading` seam.

## Amendment 2026-07-05 — the remaining half is superseded by ADR-0011

The Director-owned CuePlanner and its planning machinery (window derivation, cue cursor, cadence gate, pass-local cue memory) are deleted by ADR-0011: sheet building becomes a pure constraint-based function inside the wire-change Director, and the change cadence becomes a sheet-construction rule. Nothing of this ADR remains active; it is kept for the record of why the determiner and planner existed.
