# Name the cyclic 16-beat unit "Grid", keep song-structure "Phrase"

The timing layer carried a one-letter twin: `PhaseInfo` (the wall's cyclic 16-beat unit, read by effects) versus `PhraseInfo` (the song-structure section). They are different concepts from different registers, they co-occur in the same Switching signatures (`CuePlanner`, `OnAirTiming`, and `Director` each carry a `PhaseReading` *and* a `PhraseWindow`), and "phase"/"phrase" are trivially fat-fingered. We renamed the **cyclic** family `Phase*` → `Grid*` and kept `Phrase*`, because "phrase" is the canonical song-structure term (Pioneer PSSI / Rekordbox; RaveSystem is itself migrating its wire toward `phrase_state`), whereas the wall's 16-beat "Phase" is a PenroseArt-coined cyclic-position term with no external anchor — so the source-of-truth-aligned move is to rename the coined side, not the borrowed one.

## Scope

In scope — the cyclic family: `PhaseLock`→`GridSync`, `PhaseLockState`→`GridSyncState`, `PhaseGrid`→`Grid`, `PhaseReading`→`GridReading`, `PhaseInfo`→`GridInfo`, `BeatManager.Phase`→`BeatManager.Grid`, and the Grid Anchor / Grid Boundary / Grid Count / Grid Confidence vocabulary. This renames the term in ADR-0006 (the determiner) without changing that decision.

Out of scope — three genuinely different, correctly-named uses of "phase" that stay: **Bar Phase** and Waveform **Phase Offset** (DSP waveform terms), and **Track Phase** plus the OSC wire (`phrase_state`, `LegacyPhraseStateAddress`), which is governed by ADR-0003.

## Considered Options

- **Rename `Phrase*` → `Section`** (the original plan). Rejected: drifts from the canonical Rekordbox/PSSI "phrase" the wire is converging on, and invents a third word for a concept that already has the right one.
- **Name the cyclic family `BeatGrid`.** Rejected: "Beat Grid" is canonical upstream for the *per-beat → time* map (`BeatGridParser`); reusing it for the 16-beat cycle would re-create the same cross-register collision we are removing.
- **`GridLock` for the determiner.** Rejected: reads as "gridlock" and loses the phase-locked-loop meaning. Chose `GridSync`.

## Amendment 2026-07-04 — Grid confidence is now wire-sourced

The `GridSyncState` renamed here died with GridSync (ADR-0010): grid confidence is no longer locally determined but read from the OSC v2 `timing_grid`, and a new `GridConfidence { Locked, Coasting, Disputed }` in the Rhythm layer carries the wire vocabulary for display (the old `Contradicted` value is now the wire's `Disputed`). The naming decision — cyclic "Grid" vs song-structure "Phrase" — is unchanged; only the confidence enum's home and source moved.

## Amendment 2026-07-05 — wire vocabulary is law at the surface

Now that grid state and grid position are wire-sourced, the surface types carry RaveSystem's own `timing_grid` field names verbatim: `GridConfidence` → `GridState`, `GridInfo.Confidence` → `GridInfo.State` (wire `state`), and `GridInfo.Count` → `GridInfo.Beat` (wire `beat`, 1..16). `GridInfo.Bar` already matched the wire; `GridInfo.Progress` stays as BeatManager's documented enrichment (wire beat plus the intra-beat fraction from one snapshot). The **Grid Confidence** / **Grid Count** vocabulary in `CONTEXT.md` becomes **Grid State** / **Grid Beat** to match. The rule this records: wire vocabulary is law at the surface — a boundary like BeatManager may type, validate, and enrich the lane, but it never re-words the wire's own field names. Members Locked/Coasting/Disputed are unchanged; this is a rename only, no behavior change.
