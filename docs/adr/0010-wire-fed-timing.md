# Timing grid and phrase truth come from the OSC v2 wire, not local synthesis

RaveSystem OSC schema v2 broadcasts source-computed timing that PenroseArt previously reconstructed downstream: a `timing_grid` (locked/coasting/disputed confidence with a 1..16 grid count and 1..4 bar), `phrase_state` and `next_phrase_state` that are **always present** because RaveSystem auto-generates phrases when a track has none, plus `loop_state`, split `energy_state`/`next_energy_state`, and a stable `track_id`. We deleted every local stand-in that guessed these before the wire carried them — GridSync's held-offset determiner, PhraseTracker's next-length guess, the synthetic 64-beat phrase fallback (ADR-0008), CuePlanner grid-anchor coasting, `TrackOrdinal` title-diffing, and the Director→BeatManager grid mirror — and read wire truth instead, because in Synced Mode the source computes this timing better than we can rebuild it and keeping both paths doubles the timing surface for no gain. Standalone Mode is untouched — its wall-clock timer and null off-air queries are mode authority (ADR-0007), not a fallback this replaces.

## Grid confidence is wire-sourced; GridConfidence lives in the Rhythm layer

`BeatManager.Grid` reads `snapshot.timingGrid` directly, and a new `GridConfidence { Locked, Coasting, Disputed }` in the Rhythm layer replaces `GridSyncState` (deleted with GridSync). The three values are the source's own determination, not a local re-derivation; losing the clock still surfaces as a null `Grid`, not a fourth state (ADR-0009).

## Cue Sheets rebuild on every phrase change (supersedes ADR-0005's length-identity reuse)

Because `phrase_state` and `next_phrase_state` are always on the wire, the CuePlanner builds a fresh Cue Sheet whenever the phrase window changes — same-length turnover included — rather than reusing a sheet by length identity. A phrase window is identified by both its start beat **and** its length, so a same-start length change (e.g. a 64→32-beat phrase both starting on the same beat) rebuilds rather than serving the stale sheet — a start-only identity would silently keep the old sheet, which is exactly the length-identity reuse this removes. The upcoming window uses the true next-phrase length from `next_phrase_state`, replacing PhraseTracker's "next length = current length" guess; the length-identity reuse machinery (`CueSheet.Matches`, cursor reanchor) is deleted.

## The determiner half of ADR-0006 is superseded

GridSync's held-offset determiner and PhraseTracker are deleted — the source computes the grid now — so `OnAirTimingInput` collapses to five honest integers (`Beat`, `BeatsUntilPhraseEnd`, `PhraseLengthBeats`, `NextPhraseStartInBeats`, `NextPhraseLengthBeats`) mapping `BeatManager.Phrase`/`NextPhrase`, kept only as a testable integer seam for the CuePlanner. ADR-0006's other half — the Director-owned CuePlanner split from On-Air Timing — still stands.

## Energy casts effects through Repertoire affinity flags

`Repertoire` gains `EnergyLow`/`EnergyMid`/`EnergyHigh`, and the Director prefers casting a Performer whose affinity matches the current energy level — or the incoming level when a change lands within the cast Performer's cadence stint. Energy drives effect casting only: it never creates a Cue Mark and never selects a Transition.

## Track change is signalled by the wire track_id

`BeatManager.TrackId` (from `track_id`) replaces the `TrackOrdinal` track-title diffing; the Director resets the CuePlanner's cadence memory on a `track_id` change so stale cadence never crosses into a new track whose beat counter does not rewind.

## Loop state is ingest and display only

`loop_state` is ingested and surfaced (`BeatManager.Loop`, the Observatory Loop row) but drives no cue behavior: the Director keeps cueing through a DJ loop. A loop rewinds the beat counter, which the CuePlanner's existing beat-rewind handling already absorbs, so there is no cue-hold. Loop availability gates on the wire tri-state `active < 0` (never `!= 0`), so a set-but-idle region stays real, non-null data.

## Considered options

- **Keep the local determiner and synthetic fallback as a dual path behind the wire.** Rejected in favour of a hard break: maintaining both the wire lanes and the GridSync/PhraseTracker/synthetic-phrase machinery doubles the timing surface and its test matrix, and RaveSystem now always broadcasts phrases in Synced Mode, so the fallback guards a case that no longer occurs. We accept a dependency on the wire carrying phrase/grid data in Synced Mode; Standalone Mode remains the honest floor when no clock is present.
- **Keep `total_beats`/title-derived track identity.** Rejected once `track_id` exists: a stable source id changes exactly once per track, where title diffing mis-fires on same-title adjacent tracks and needs the raw title to cross the integer seam.

## Amendment 2026-07-05 — three sections superseded by ADR-0011

The wire-change Director (ADR-0011) supersedes: the **Cue Sheets rebuild on every phrase change** section (sheets are now two announcement-keyed slots repaired per beat — only a changed announcement rebuilds one, so window identity by start-and-length is gone with the CuePlanner); the **energy casting** section (energy is no longer a Director casting input — Performers and Transitions read it from BeatManager themselves); and the **track_id** section (the reducer holds no cross-track state, so nothing resets on a track change; `track_id` remains ingested for display only). The core decision — grid and phrase truth come from the wire, not local synthesis — stands and is the foundation ADR-0011 builds on.
