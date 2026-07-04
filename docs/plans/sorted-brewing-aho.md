# Adopt RaveSystem OSC schema v2 — wire truth in, synthesized stand-ins out

## Context

RaveSystem shipped OSC schema v2 (their ADR-0021, one hard break): `phase_state` → `phrase_state` reshaped to `siii` (name, count_beats, length_beats, irregular), new `next_phrase_state` (`sii`), energy split into `energy_state`/`next_energy_state` (`sii`), new `loop_state` (`iifiii`), new source-computed `timing_grid` (`iis`: beat 1..16, bar 1..4, locked/coasting/disputed), new `track_id` (`i`). Drop/fill (`iiii`) and all scalar beat lanes are unchanged. RaveSystem now auto-generates phrases when a track has none, so **phrase and grid data are always available in sync mode** — live-mutable (DJ loops, hot-swaps, restructures), but always present.

Goal: bring in every new lane, and in sync mode rely on wire truth instead of the things we were faking in code until OSC support existed. **No functionality is removed** — every locally-synthesized stand-in is replaced by its wire truth; genuine enrichments of real data stay.

Decisions made (Hunter):
- Energy drives **effect casting** (prefer effects matching the level; cast ahead of a change). Energy does not create cue marks.
- Loop: keep simple — ingest + display; the Director keeps cueing while looping. No cue-hold.
- Phrase + grid always on the wire in sync mode → all fallback machinery goes (GridSync, coasting, synthetic phrase ADR-0008).
- **Standalone mode is untouched** — wall-clock timer path, ADR-0007 mode authority, null queries off-air. That's mode authority, not a fallback.
- Hard break: no legacy `phase_state`, no dual shapes.
- Effects standardization **split**: the declaration side (Repertoire flags) lands here (Slice B); the consumption side (one uniform way effects read rhythm data — today each effect reads BeatManager differently) is the immediate follow-up once this surface settles. Record it as a `.scratch/` issue in Slice D.

Goes (synthesized because the wire lacked it) → replaced by:
| Local stand-in | Wire truth |
|---|---|
| `GridSync.cs` + `Grid.cs` (guessing the One) | `timing_grid` |
| `PhraseTracker` look-ahead guess (next length = current length) | `next_phrase_state` |
| Synthetic 64-beat phrase fallback + loop-guard (ADR-0008) | phrase always broadcast |
| CuePlanner grid-anchor coasting | source-side confidence (`coasting`/`disputed`) |
| `TrackOrdinal` via track-title diffing | `track_id` |
| Director→BeatManager `PublishGrid` mirror | BeatManager reads grid off the snapshot |
| `EnergyInfo.direction` local `Sign()` guess | `next_energy_state` |

Stays (enrichment of real wire data, not fakes): off-beats, `BarPhase`, `IntraBeatFraction`, `PulseOf`/`GateOf`/subdivision family, levels smoothing, progress fractions — RaveSystem intentionally ships beat counts and lets clients derive fractions/ms.

## Slice A — Ingestion (wire → snapshot → BeatManager queries)

Boundary: `Assets/OSC/Rave/` + `Assets/core/Rhythm/` only (ADR-0003: generic `Assets/OSC/*.cs` untouched).

**Snapshot (`Assets/OSC/Rave/RaveOscSnapshot.cs`):**
- Delete `NamedState`. Keep `CountdownState`, `BeatPosition`, `BarPosition`, `Levels`.
- Add structs, each `[Serializable]` with an `Unavailable` default per existing convention:
  - `PhraseState { string label; int countBeats; int lengthBeats; int irregular /*tri-state*/ }`
  - `LabeledCountdown { string label; int countBeats; int lengthBeats }` — used by next_phrase / energy / next_energy
  - `LoopState { int active; int set; float lengthBeats; int lengthMs; int sizeNumerator; int sizeDenominator }`
  - `TimingGrid { int beat; int bar; string state }`
- `RaveOnAirSnapshot`: `phraseState:PhraseState`, `energyState:LabeledCountdown`, new `nextPhraseState`, `nextEnergyState`, `loopState`, `timingGrid`, `trackId:int=-1`. `Clone()` unchanged (value structs).

**Parser (`Assets/OSC/Rave/RaveOscPacketParser.cs`):**
- Delete `LegacyPhraseStateAddress` (line 20) + its registration + `RegisterNamedState`.
- Add `RegisterPhraseState` (`siii`), `RegisterLabeledCountdown` (`sii`, registered 3×), `RegisterLoopState` (`iifiii` — existing ReadNextInt/ReadNextFloat compose), `RegisterTimingGrid` (`iis`); `track_id` via existing `RegisterInt`.
- Tri-state ints pass through verbatim (1/0/-1), never `!= 0` (copy the existing tri-state comments).

**Receiver (`Assets/core/IO/RaveOscReceiver.cs`):** no change — whole-snapshot `CopyFrom` carries new lanes.

**BeatManager queries (`Assets/core/Rhythm/BeatManagerQueries.cs`; ADR-0002 null = unavailable; availability gating moves from `active < 0` to sentinels: empty label / -1):**
- `PhraseInfo` reshaped: `{ label, beatsUntilNext, lengthBeats, irregular:bool? (1→true, 0→false, -1→null), progress }`. Drop `inPhrase`, `remaining`.
- New `NextPhrase` query → `NextPhraseInfo { label, beatsUntilChange, lengthBeats }` (the next phrase's **own** length). Separate query, not folded — CuePlanner treats current/next as distinct windows.
- `EnergyInfo` reshaped: `{ level, next (real wire data), beatsUntilChange (from energyState.countBeats — current-run remainder), normalized, direction, runProgress, runLengthBeats, nextRunLengthBeats }`. Drop `changesRemaining`.
- New `LoopInfo { looping (active==1), regionSet (set==1), lengthBeats:float?, lengthMs?, sizeNumerator?, sizeDenominator? }` + `BeatManager.Loop`, null when `active < 0`. Doc comment states: display/telemetry only, drives no cue behavior.
- `GridInfo` becomes wire-fed: `{ Confidence:GridConfidence{Locked,Coasting,Disputed}, Count (1..16), Bar (1..4, new), Progress (enriched with IntraBeatFraction) }`. Null when: no beatData, `!IsSynced` (standalone floor), state empty/unparseable, or `beat < 1`. `GridConfidence` is a new enum here using wire vocabulary; `GridSyncState` dies with GridSync (Slice C).
- New `TrackId:int?` (null at -1). Delete `TrackOrdinal` + `DeriveTrackOrdinal` + backing fields.
- Delete `PublishGrid`/`lastGrid`.
- Coupling: the `Grid` swap, `PublishGrid` deletion, and `TrackOrdinal` deletion land **with Slice C** (Director is the only publisher; GridSync the only ordinal consumer). Everything else in Slice A is additive/independent and compiles alone.

**Tests:**
- `Assets/OSC/Tests/Editor/RaveOscPacketParserTests.cs`: rewrite writers (`WritePhraseState` siii, `WriteLabeledCountdown` sii, `WriteLoopState` iifiii, `WriteTimingGrid` iis; delete `WriteNamedState`); update dispatch counts (net +4 lanes); tri-state preservation incl. `irregular` and loop `active`/`set`; defaults for all new structs; wrong-type case on loop_state's float slot; new test — legacy `phase_state` is unrecognized (0 dispatched, nothing mutated, doesn't refresh liveness).
- `Assets/Tests/Editor/RaveOscIngestionRoundTripTests.cs`: rebuild full packet (21 addresses); delete the legacy-address test; per-lane unavailable round-trips for new lanes.
- `BeatManagerContrivedQueriesTests.cs`: label-based gating; `irregular` mapping; Loop tri-state trap (`0 1 …` → looping=false, regionSet=true).
- `BeatManagerGridQueryTests.cs`: snapshot-fed — locked/coasting/disputed parse, empty/unknown state → null, `-1 -1 "coasting"` → null, null when !IsSynced.
- `BeatManagerRaveOscIntegrationTests.cs`: `TrackId` changes on identity change; title change without id change is NOT a new track (inverse of the old ordinal test).

## Slice B — Energy-preference casting + Repertoire vocabulary

- Extend `Repertoire` flags (`Assets/core/Effects/Repertoire.cs`): `EnergyLow`, `EnergyMid`, `EnergyHigh` alongside `HandlesFill`/`HandlesDrop`. This is the declaration side of the effects contract — effects tell the Director what they support; the standardized consumption side is the follow-up.
- New pure static `EnergyCasting` (`Assets/core/Switching/EnergyCasting.cs`, ~30 lines): `PreferredEnergyRepertoire(EnergyInfo?, currentBeat, impactBeat, castAheadBeats)` — if the energy change lands at/within `castAheadBeats` (= MinimumChangeCadenceBeats, 16) after impact, prefer the incoming level (the cast performer spends its whole cadence stint in the new energy); else the current level. Same cast-ahead shape as `DropApproaching`.
- `SyncedCueIntent.Cast` gains `Repertoire energyPreference`; effective preference = event intent's preference (Drop/Fill outrank) else energy. Deck lookup via existing `Deck.TryFindPreferred`. Director computes preference in `TryStartSyncedCue` once `beatPlan.ImpactBeat` exists. `CueDecision.PreferredRepertoire` surfaces it in the Observatory for free. Drop-protect guard untouched, evaluated first.
- Declare affinities on natural first adopters (Angles, Flock, CrystalGrowth — they already read Energy/Levels).
- Tests: new `EnergyCastingTests` (level-at-impact, cast-ahead boundary at exactly +16 vs +17, null shapes); `SyncedCueIntentTests` cast cases (energy match found, Drop/Fill outrank energy, null energy = staged regression, `NoPreferredAvailable`).

## Slice C — Timing core simplification (Director/CuePlanner)

**Delete outright:**
- `Assets/core/Switching/GridSync.cs` (whole file, incl. `GridReading`, `GridSyncState`), `PhraseTracker.cs`, `Grid.cs` (after a repo-wide ref sweep; `ControllerEditor` `Grid.GridBeats` refs → `PhraseWindow.DefaultGridBeats`).
- CuePlanner synthetic apparatus: `SyntheticPhraseLengthBeats`, `SyntheticCueGrid`, `BuildSyntheticFrame`, `UpdateSyntheticLoopGuard`/`ResetSyntheticLoopGuard`/`syntheticCueMarkStranded`, `TrackPhaseUnavailable` branch.
- CuePlanner coasting: `BuildCoastingFrame`, `HasCoastableGridAnchor`, `CoastGridAnchor`, `ReanchoredFrom`.
- CuePlanner grid fallback: `ResolvedTimingTarget.GridFallback`, `GetLandingBeatFromGridPosition`, final fallback in `ResolveCueMark`.
- Look-ahead guess branch in `ResolveCueMark` (`HasLookAhead` over guessed length).
- Sheet length-identity reuse: `CueSheet.Matches`, `CueSheetCursor.NeedsSheet`, `Reanchor`.
- Director: `gridSync`/`gridReading`/`phraseTrackerReading` fields, `RefreshPhraseReading`, `PhraseReadingShifted`, the `PublishGrid` call + standalone grid reset, `FormatGridReading`/`FormatPhraseTracker`, `DirectorStatus.Grid/Phrase/TimingReanchored`.
- `TimingFrame.Grid/Reanchored/IsCoasting`; `TimingFrameSource` collapses to `Unlocked / CueMark / TrackPhaseBoundary`.

**New `OnAirTimingInput`** (integer seam, -1 sentinels, kept for CuePlanner testability): `{ Beat, BeatsUntilPhraseEnd, PhraseLengthBeats, NextPhraseStartInBeats, NextPhraseLengthBeats }`. `From(beatManager)` maps `Phrase`/`NextPhrase`. BeatInBar/TrackPhaseActive/TrackOrdinal leave the seam.

**`CuePlanner.Plan(input, minimumChangeCadenceBeats, lateCueWindowBeats)`** (~350 lines from 859). CuePlanner stays a small Director-owned component (the pure, testable half) — not folded into Director:
1. Keep `BeatRewoundToNewPass` + cue/cadence memory clear unchanged — loops rewind the beat counter, the Director keeps cueing through them; wire `Loop` is display-only.
2. `Beat < 1` → Unlocked frame.
3. Keep the late-cue Tail keep (`TryKeepUnconsumedMandatoryBoundaryAt`) — DirectorSyncedTailTests behavior.
4. Live window (`PhraseWindow.TryFromTrackPhase`): if `cursor.PhraseStartBeat != window.StartBeat` → `CueSheet.Build` + `cursor.Replace` — **fresh sheet on every phrase change, same-length turnover included**; else if rewound → `RewindCursor`. Then `AdvanceTo`; mark = `CurrentCueMarkOr(window.EndBeat)`; source = CueMark or TrackPhaseBoundary.
5. No live window, cursor still serving its own window → as today (`TryGetActivePhraseWindow`).
6. No live window, NextPhrase known → `PhraseWindow.TryFromUpcomingTrackPhase` with the **true** next length (covers pre-first-phrase countdown).
7. Else Unlocked (brief, e.g. first frames of a track — acceptable).
`EvaluateCueTiming`, `MarkChanged`, `RecordCueIssued`, `CanChangeAt`, `Reset` unchanged.

**Director:** `RefreshTimingFrame` collapses to build-input → `Plan` → trace. Track-change reset: hold `lastTrackId`; on `beatManager.TrackId` change → `cuePlanner.Reset()` (replaces GridSync's reset; keeps stale cadence memory from crossing tracks whose beat counters don't rewind). Honest renames ride along: `HasGridAnchor`→`HasCueMark`, `GridAnchorLandingBeat` alias deleted (callers: Director.cs, Controller.cs:1282–1292, ControllerEditor.cs:170–186).

**Tests:** delete `GridSyncTests` + `GridTimelineHarness` (keep `BeatClockFixture`), `PhraseTrackerTests`, `DirectorGridPublishTests`. Rewrite `CuePlannerTests` against the new `Plan`: fresh sheet on every phrase change incl. same-length (assert randomRange consulted again), upcoming sheet from true next length, rewind clears pass memory, tail keep, Unlocked when no phrase and no next. Update `DirectorSyncedTailTests` plumbing. Keep `PhraseWindowTests`, `ChangeCadenceTests`, `DeckTests`, `TransitionBeatPlanTests`, `SwitcherExecutionTests`, `DirectorStagingTests`.

## Slice D — Observatory/HUD + docs + follow-up seed

- `Assets/Editor/Controller/ControllerEditor.cs`: grid rows read `controller.beatManager.Grid` (Confidence/Count/Bar) instead of `status.Grid`; "Irregular Phrase" from `Phrase?.irregular`; new rows read straight from BeatManager — Phrase `label · pos/len`, Next Phrase `label · in Nb · Nb long`, Energy `level (→ next in Nb)`, Loop `rolling/set · Nb`.
- `Controller.cs` HUD helpers (`FormatTimingSource`, `FormatGridPosition`) → reduced `TimingFrameSource` + `beatManager.Grid`.
- ADRs: retire **0008** (superseded — RaveSystem auto-generates phrases); new **ADR-0010** "wire-fed timing" (grid/phrase truth from OSC v2; supersedes the determiner half of 0006; amends 0005's sheet-reuse rule to sheet-per-phrase; energy-affinity Repertoire flags); note in **0009** (grid confidence now wire-sourced, `GridConfidence` lives in Rhythm). ADR style per `memory:penroseart-adr-conventions`.
- Seed the follow-up: `.scratch/effects-rhythm-contract/` issue — standardize how effects consume BeatManager data (one uniform read pattern; make Repertoire declarations verifiable against actual consumption). Explicitly out of scope here.

## Ordering

1. **Slice A** additive parts (parser, snapshot, new queries, reshaped Phrase/Energy, Loop, TrackId) — independent, lands first.
2. **Slice B** energy casting — needs only A's Energy surface.
3. **Slice C** timing core — lands together with A's coupled seams (Grid swap + `PublishGrid`/`lastGrid` deletion, `TrackOrdinal` deletion) so every commit compiles.
4. **Slice D** observatory + ADRs + follow-up issue.

## Verification

- Editor test suite via `scripts/unity-tests.sh` after each slice (open-Editor bridge per `memory:penroseart-unity-test-runner-editor-open-options`; do not fight batchmode while the project is open).
- Live end-to-end: RaveSystem broadcasting v2 on the LAN → Play Mode: Observatory shows wire grid (Confidence/Count/Bar), current+next phrase, energy with change countdown, loop rolling/set; cue sheet visibly rebuilds on each phrase change (same-length included); energy-affinity casts appear under Last Cue Decision; standalone behavior unchanged when RaveSystem stops broadcasting (timer path, null queries).
- Sanity greps before deletes: no refs to `Grid.` statics, `GridSyncState`, `TrackOrdinal`, `PublishGrid` outside deleted code.
