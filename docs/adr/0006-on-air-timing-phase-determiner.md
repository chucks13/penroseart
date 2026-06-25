# On-Air Timing is a Phase determiner; PhaseLock holds the one and Phrase wins the grid

`OnAirTiming.cs` tangles a universal job — determining Phase, "where is the one" on the 16-beat grid — with a Director job: Cue Sheet planning (`CueSheetPlans`, `CueSheetCursor`, and pass-local cue memory round-tripped through the Director every frame). We split them. On-Air Timing becomes a **pure Phase determiner** exposed as two deep modules: a reusable **PhaseLock** core that answers where the one is, and a **PhraseTracker** that rides on it for song structure. Cue Sheet planning moves out to a Director-owned **CuePlanner** (relocating the derivation ADR-0005 placed in On-Air Timing). The full operational design lives in the companion doc `docs/architecture-reviews/on-air-timing-redesign-2026-06-24.html`; this ADR records the decisions and why.

## The 4-count is bedrock; the running beat is position; total_beats is track length

`beat_in_bar` (the 4-count) is the clock we ground on: always present, locally defined, and loop-invariant for any loop ≥ 1 bar. It is the driving pulse, not — as today's stateless `PhaseClock` ranks it — the weakest signal; that ranking judged only its weak second role as an anchor for *which* bar carries the one. The running `beat` counter supplies position. `total_beats` is the **track length** (a per-track constant), used only as an end-aligned fallback grid (`length mod 16`) when Phrase data is absent — it is not a running position. Drop, energy, and fill carry no Phase information and are not consulted.

## The one is a held offset; Phase position is recomputed every frame

Phase is `offset` applied to the running beat: `position = ((beat − 1) − offset) mod 16 + 1`. `offset` (where the one sits in the 16-grid) is **held** and re-latched only at structural triggers; `position` is **recomputed every frame** from the current beat against that held offset. Because position tracks true playback, a loop — a bar-aligned backward `beat` jump — is absorbed for free: there is **no explicit loop detection**, no rewind threshold, and no "dead zone." This replaces the deep-dive's brittle 16-beat backward-jump heuristic.

## Phrase decides the grid, grounded on the 4-count tick; boundaries are detected by phrase-start advance

The Phrase decides where the 16-grid starts: On-Air Timing latches `offset` from the Phrase boundary (the Phrase start *is* a one), and position then dead-reckons off the running `beat` until the next Phrase boundary re-latches. The re-latch is grounded on the **tick**, not on the previous offset: a real Phrase start is a downbeat, so the grid the new offset implies must agree with the feed's `beat_in_bar`. When it agrees the offset re-latches; when a Phrase start lands off the tick — or a held grid drifts off it (a sub-bar flub) — that is a Phrase-vs-pulse disagreement, held and flagged CONTRADICTED rather than silently applied. Anchoring on the tick rather than on "the offset shifted a whole bar from the last one" is what keeps this correct when the held offset is stale (a track change) and when the `beat` counter does not itself start on a downbeat. This makes lead-ins and non-power-of-two Phrases the same mechanism — re-latch every boundary, flag a Phrase that ends off the 16-grid as irregular — with no special case. A Phrase boundary is detected when the derived `phraseStart = beat − (lengthBeats − beatsUntilNext)` advances, not by label change (adjacent Phrases share labels) nor by a countdown reaching zero (fragile across dropouts). Phrase look-ahead (predict-then-confirm of the next boundary from `beatsUntilNext` + the upcoming Phrase's `lengthBeats`) is a PhraseTracker enhancement; PhaseLock reacts at the boundary.

## A track change is a clean re-acquisition, not a carry-over

`beat` is a per-track counter, so the held `offset` — meaningful only relative to the previous track's counter — carries no information about the new song's grid. On a track-title change On-Air Timing therefore **resets**: it drops the held offset, boundary memory, and anchor and re-acquires from the new song's own Phrase, exactly as it does at the start of any track. The end-aligned `total_beats` fallback covers the gap until the new song's first Phrase boundary lands. Phase correctness no longer depends on any cross-track state.

## Degradation is per-layer with a stand-alone floor

Confidence degrades top-down and never loses the floor: the pulse is ≈always locked while a clock exists, while Phase and Phrase each carry their own LOCKED / COASTING / CONTRADICTED plus a `beatsSinceAnchor` staleness count. There is no global "Unlocked" inside synced mode. The floor is the clock itself: when the beat signals read `-1` (no `beat_in_bar`), On-Air Timing exits to stand-alone Mode (the Director's free-running timer, ADR-0004) — a mode exit, not a degraded Phase state. With a clock but no Phrase data we stay synced and COASTING/CONTRADICTED, and what to do there is the Director's decision, not the timing core's.

## All timing metrics are integer beats

Every value PhaseLock holds or emits is a whole-beat integer (`offset`, `position`, `beatsSinceAnchor`, `beatsUntilNext`, `lengthBeats`, `phraseStart`). The model is correct because it is exact integer modular arithmetic; floats would invite drift and fuzzy comparisons in grid math that must be crisp. The rhythm system's existing floats (`progress`, `IntraBeatFraction`, anticipation ramps) are BeatManager presentation-smoothing and stay out of the Phase core.

## Considered options

- **A multi-signal voting / confidence-estimator arbiter** (the earlier locked framing) — rejected as more machinery than the problem needs. Phrase already encodes the downbeat and lead-in that beat-in-bar and drop edges would corroborate, so extra authorities are redundant and reintroduce the conflicting-winner problem. Two authorities — running `beat` for position, Phrase for the offset — grounded on the 4-count, suffice.
- **Keep the stateless per-frame `PhaseClock`** — rejected because it cannot coast through a Phrase-data dropout and cannot detect a contradiction (a freshly derived offset disagreeing with the held one), which is the tripwire for loops, non-power-of-two Phrases, and track changes.
- **Absolute-beat cue bookkeeping with explicit loop-folding** — moot once cue planning leaves On-Air Timing, but rejected in principle: held-offset + recomputed-position makes loops free, where absolute beats carry loop-detection logic and its dead-zone failure forever.
- **Soft-hold the offset across a track change** (an earlier draft of this ADR) — rejected once it was clear `beat` is a per-track counter. The held offset is meaningful only against the old track's `beat`; applied to the new song's counter it is a stale number, not a continuation. Soft-holding it forces special-case gate-bypass logic and can lock the new song onto a wrong one. We reset and re-acquire instead; the new song's first Phrase boundary re-anchors, with the `total_beats` fallback covering the gap.
- **Treat drop/energy edges as Phase re-anchors** (an earlier draft) — rejected: they are section signposts, not Phase information.

## Consequences

- Cue Sheet derivation, the cue cursor, cadence, and pass-local cue memory move from On-Air Timing to a Director-owned CuePlanner; ADR-0005's "On-Air Timing derives Cue Sheets" is relocated, not its ownership split. What the Director does with Phase is immaterial to the timing core, which only emits a read-only reading.
- PhaseLock consumes BeatManager's already-projected integer values (the path `OnAirTiming.ReadFrame` takes today), not raw OSC, and reads Phrase numerics directly so it does not depend on PhraseTracker.
- The reading seam exposes Phase and Phrase separately (`PhaseReading` + `PhraseReading`); exact fields are implementation detail.
- The seam is justified by testability: PhaseLock can be property-tested against scripted DJ timelines (loops of every length, lead-ins, non-power-of-two Phrases, seeks, dropouts, track changes, clock loss) independent of the Director. Effect-level consumption of the one is allowed to emerge; no speculative extension points are added for it.
- Rig-empirical unknowns remain to verify but do not block the ADR or harness: whether Phrase data arrives before the boundary, whether Phrase data is always sent, the Phrase-boundary timing noise profile, how often the 4-count wobbles, and the unit of `phrase_state.remaining`.
