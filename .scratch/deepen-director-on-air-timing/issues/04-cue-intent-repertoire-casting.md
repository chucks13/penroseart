# Connect TimingFrame to cue intent and Repertoire-aware casting

Status: accepted

## What to build

Use the Timing Frame as the input to a clearer cue/casting path. The Director should no longer compute musical cue intent in scattered private methods or compute Drop preference only to discard it. A cue/casting decision should combine the current Timing Frame, selected Transition timing declaration, live phrase-event data such as Drop/Fill, current staged choices, and Performer Repertoire into a small Director-facing cue intent.

This slice should be narrow but complete: a Drop-aligned selected Phase Boundary can produce cue intent, Repertoire-aware deck selection can prefer a suitable next Performer when one exists, the Director can stage/use that choice without micromanaging effect internals, and tests prove the behavior through the cue/casting seam plus Director integration. Effects still own how they express Fill, Drop, Energy, and Levels; the Director casts or cues, not configures pixels.

Use Matt/codebase-design as the design gate: cue intent should be a deep module or evolved existing cue seam, not another pile of Director helper methods. Use polish as part of the slice: remove the obsolete compute-and-discard preference path and keep the interface small enough that future Fill/Energy behavior can use it without another timing redesign.

## Acceptance criteria

- [x] Cue/casting logic consumes a Timing Frame rather than raw Track Phase or duplicated Phase Anchor fields.
- [x] Drop-aligned timing can produce a cue intent that prefers Performers advertising Drop-capable Repertoire when such Performers are available.
- [x] If no preferred Performer is available, deck selection remains intentional and does not fail or invent fake capabilities.
- [x] The existing staged Next Effect / Next Transition behavior remains correct for Hold Selected and one-shot manual steering.
- [x] Transition Repertoire remains the source of Runway/Tail timing; cue/casting does not duplicate TransitionBeatPlan arithmetic.
- [x] Effect Repertoire remains a Performer declaration. The Director does not set effect internals or issue pixel-level commands.
- [x] Existing EffectDeckSelection behavior is reused or deliberately deepened; deck preference rules are not duplicated across callers.
- [x] Tests cover cue intent through the cue/casting seam rather than private Director methods.
- [x] Director integration tests prove the cue intent affects staged/started choices in observable status or Switcher behavior.
- [x] The obsolete Drop-preference-computed-then-discarded path is removed.
- [x] No service layer, event bus, ScriptableObject registry, prefab architecture, or speculative adapter is introduced.
- [x] The scoped diff is polished after behavior is green: cue, casting, Repertoire, Performer, Drop, Fill, Runway, Tail, and Impact Point vocabulary is consistent.

## Implementation notes

- Replaced `SyncedCueDecision` with `SyncedCueIntent`, keeping Transition Runway/Tail math in `TransitionBeatPlan` and moving Drop-aware casting into the cue/casting seam.
- Deepened `EffectDeckSelection` with `TryPullPreferred`, so preferred Performer casting reuses the deck rotation rules and leaves the deck untouched when no suitable Performer exists.
- `Director.TryStartSyncedCue` now asks cue intent for the target Performer and immediately stages the following move after firing, while fired selected boundaries are consumed through On-Air Timing pass-local state.
- `OnAirTiming` now keeps current and upcoming Phrase Boundary plans. Upcoming Track Phase frames (`active == 0`) pre-plan the next Phrase from the countdown and length metadata, but do not replace an unfired current mandatory phrase boundary. Once a fired boundary is consumed, the pre-planned next Phrase can be promoted without rerolling on the turnover frame.

## Validation

- `./scripts/unity-compile.sh` passed with 0 C# warnings.
- `UNITY_TEST_FILTER='OnAirTimingTests|DirectorSyncedTailTests|DirectorStagingTests|PhaseClockTests|PhraseWindowTests|SelectedPhaseBoundaryPlanTests|TransitionBeatPlanTests|SyncedCueIntentTests|ChangeCadenceTests|EffectDeckSelectionTests|SwitcherExecutionTests|TransitionSettingsTests|BeatManagerRaveOscIntegrationTests|BeatManagerContrivedQueriesTests' ./scripts/unity-tests.sh` passed 129/129.
- `./scripts/unity-tests.sh` passed 189/189.
- `git diff --check` passed.

## Blocked by

- `.scratch/deepen-director-on-air-timing/issues/01-hard-cut-on-air-timing-frame.md`
- `.scratch/deepen-director-on-air-timing/issues/02-loop-beat-rewind-self-correction.md`
- `.scratch/deepen-director-on-air-timing/issues/03-coast-and-reanchor-recovery.md`
