# BeatManager exposes shallow musical values

Status: accepted

BeatManager receives Rave OSC wire state, derives reusable musical values, and exposes both through one shallow immutable surface grouped by meaning: Timing, Track, Beats, Offbeats, Pulses, Phrase/NextPhrase, Drop, Fill, Energy/NextEnergy, Loop, Grid, and Levels. We rejected the public `View`/`Facts`/`Span`/`Current`/`Run` hierarchy, hub-owned one-frame edges, and Levels color policy because they obscured simple data and made BeatManager responsible for consumer behavior; consumers compare their own prior values when they need an onset, while `Build()` and `Decay()` remain direct readability conveniences on duration-bearing groups.

## Amendment — 2026-07-26: the Before/In span pair carries envelopes only

Nine handles now reach their `Build`/`Decay` through two named spans, `Before` (approaching the piece across a caller-named window of whole beats) and `In` (through the active piece): the event-fed `Drop` and `Fill`, plus the seven typed Phrase handles of the Song Structure — `Intro`, `Up`, `Down`, `Verse`, `Bridge`, `Chorus`, and `Outro` — which are fed positionally from the Focus player's structure cursor. This narrows the rejection above rather than reversing it: what was rejected was a public `View`/`Facts`/`Span`/`Current`/`Run` hierarchy standing between a caller and the *facts*. Every fact — `Active`, `LengthBeats`, `Remaining`, `BeatsRemaining`, `BeatsUntil`, `Progress` — stays flat on the handle exactly as before, and the spans hold nothing but the two envelopes. The Phrase handles serve no facts at all: a structure position is not a wire lane, so they carry the two spans and nothing else. The pair exists because a piece with a beginning genuinely has two distinct durations a caller can shape against, and both can be live in the same frame; one flat `Build`/`Decay` could only ever serve the active one, which is why twelve call sites had hand-rolled the approach themselves.

This supersedes ADR-0015 and the conflicting public-interface portions of ADR-0012 and ADR-0013. Their durable boundary rules remain: wire sentinels stay private, wire facts are not dropped, captured values are read-only, and raw versus derived provenance does not create two consumer-facing trees. `IsSynced` reports whether the running one-through-four beat count is usable; it gates only calculations that require that count, never otherwise-valid wire facts. `Levels` is deliberately non-null: missing normalized input reads zero immediately while Smoothed and Peak fall according to their algorithms.

## Amendment — 2026-07-26: Stock Envelopes are continuous and linear

Every Stock Envelope now has one contract: `Build` is the continuous normalized position across its window, and `Decay` is one minus that position. Whole beats name the window's duration, not its sampling rate. `BeforeSpan` subtracts the current intra-beat fraction from the wire's whole-beat countdown, removing the nine-value staircase previously emitted across an eight-beat window, while `InSpan` remains continuous.

## Considered Options

- **Preserve rendering parity.** Commit `1360d16c` deliberately preserved the whole-beat staircase and `SmoothStep` for bit-for-bit rendering parity, on the premise that the Span migration could change how effect code was written but never how an effect rendered. That trade is now reversed because the staircase was a defect, and preserving it in the shared API made it a documented contract pinned by tests and by the two-shapes language in `docs/beat-manager.md` after it had already survived a prior correction attempt.
- **Keep easing in BeatManager.** Rejected. The deleted `Rise`/`Fall` helpers no longer impose `Mathf.SmoothStep` on Phrase, Energy, Grid, or any `In` envelope. Easing dictates how a musical value is perceived and is therefore an artistic decision under ADR-0017: BeatManager serves only the position, while the Performer decides what shape to make of it and applies easing itself when wanted.

## Consequences

- Rendering changes across the affected Performers are intended. Neither `SmoothStep` nor the whole-beat approach may be reintroduced for parity reasons.
- This amendment narrows the earlier 2026-07-26 span amendment and the Stock Envelope ruling inherited from ADR-0015. It applies rather than supersedes ADR-0017. The boundary rules from ADR-0012 and ADR-0013 remain intact: wire sentinels stay private, wire facts are not dropped, captured values are read-only, and raw versus derived provenance does not create two consumer-facing trees.
