# Name the cyclic 16-beat unit "Grid", keep song-structure "Phrase"

The timing layer carried a one-letter twin: `PhaseInfo` (the wall's cyclic 16-beat unit, read by effects) versus `PhraseInfo` (the song-structure section). They are different concepts from different registers, they co-occur in the same Switching signatures (`CuePlanner`, `OnAirTiming`, and `Director` each carry a `PhaseReading` *and* a `PhraseWindow`), and "phase"/"phrase" are trivially fat-fingered. We renamed the **cyclic** family `Phase*` → `Grid*` and kept `Phrase*`, because "phrase" is the canonical song-structure term (Pioneer PSSI / Rekordbox; RaveSystem is itself migrating its wire toward `phrase_state`), whereas the wall's 16-beat "Phase" is a PenroseArt-coined cyclic-position term with no external anchor — so the source-of-truth-aligned move is to rename the coined side, not the borrowed one.

## Scope

In scope — the cyclic family: `PhaseLock`→`GridSync`, `PhaseLockState`→`GridSyncState`, `PhaseGrid`→`Grid`, `PhaseReading`→`GridReading`, `PhaseInfo`→`GridInfo`, `BeatManager.Phase`→`BeatManager.Grid`, and the Grid Anchor / Grid Boundary / Grid Count / Grid Confidence vocabulary. This renames the term in ADR-0006 (the determiner) without changing that decision.

Out of scope — three genuinely different, correctly-named uses of "phase" that stay: **Bar Phase** and Waveform **Phase Offset** (DSP waveform terms), and **Track Phase** plus the OSC wire (`phrase_state`, `LegacyPhraseStateAddress`), which is governed by ADR-0003.

## Considered Options

- **Rename `Phrase*` → `Section`** (the original plan). Rejected: drifts from the canonical Rekordbox/PSSI "phrase" the wire is converging on, and invents a third word for a concept that already has the right one.
- **Name the cyclic family `BeatGrid`.** Rejected: "Beat Grid" is canonical upstream for the *per-beat → time* map (`BeatGridParser`); reusing it for the 16-beat cycle would re-create the same cross-register collision we are removing.
- **`GridLock` for the determiner.** Rejected: reads as "gridlock" and loses the phase-locked-loop meaning. Chose `GridSync`.
