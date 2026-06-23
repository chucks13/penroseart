# Make Cue Sheet the On-Air Timing plan

Status: ready-for-human

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

ADR 0005 and the cue-sheet investigation settled the domain model: On-Air Timing should expose **Cue Sheet** / **Cue Mark** concepts, not make **Selected Phase Boundary** the canonical planning language. The old implementation had most of the behavior under `SelectedPhaseBoundaryPlan` and `OnAirTiming.SelectedPhaseBoundaryPlans`, but its identity and interface were still implementation-shaped.

The important mismatch was identity: Hunter wants a Cue Sheet to be reusable when the Phrase's total length is the same. The old `SelectedPhaseBoundaryPlan.Matches(PhraseWindow)` required exact absolute start, end, and length, so same-length Phrase metadata shifts could reroll the plan.

## What was built

Replaced the phrase-target planning concept with explicit **Cue Sheet** / **Cue Mark** behavior at the On-Air Timing seam.

`CueSheet` is generated from a Phrase's total beat length. It stores relative Cue Mark offsets inside the Phrase, always includes the final phrase boundary as the mandatory final Cue Mark, and is instantiated against the current on-air Phrase start only when an absolute beat is needed for the current `TimingFrame`.

This was a hard-cut vocabulary/model cleanup, not a compatibility layer. The existing selection behavior was preserved where correct, but absolute Selected Phase Boundary identity is no longer the canonical model.

## Implementation notes

- Renamed `Assets/core/SelectedPhaseBoundaryPlan.cs` to `Assets/core/Switching/CueSheet.cs` with Unity `.meta` GUID preservation.
- Renamed `Assets/Tests/Editor/SelectedPhaseBoundaryPlanTests.cs` to `Assets/Tests/Editor/CueSheetTests.cs` with Unity `.meta` GUID preservation.
- `CueSheet` now owns `PhraseLengthBeats` plus relative `CueMarkOffsets`; `CueSheet.Matches(...)` compares Phrase length only.
- On-Air Timing's current/upcoming state now stores Cue Sheets plus their current Phrase start, then translates relative Cue Marks to absolute `CueMarkBeat` values for `TimingFrame` and cue/cadence consumers.
- Same-length active/upcoming Phrase updates reuse existing Cue Mark offsets without rerolling while updating the active/upcoming Phrase start.
- Different-length Phrase updates regenerate the Cue Sheet.
- The mandatory final Cue Mark remains the final Phrase-length offset.
- The internal future phrase-start offset remains available only for turnover handling of upcoming plans; it is not treated as an ordinary random Cue Mark.
- `TimingFrame`, `TimingFrameSource`, `PassLocalTimingState`, `TransitionBeatPlan`, `SyncedCueIntent`, Director/Controller status formatting, and focused tests now use Cue Mark vocabulary.
- Fixed a cursor bug exposed by the new same-length test: the old mandatory-boundary preservation check advanced the current cursor even when it missed; it now restores the cursor index on a miss so same-length updates can re-instantiate the Cue Sheet against the new Phrase start.
- Preserved valid zero-Runway transition timing: `Runway=0,Tail>0` remains cueable through the Tail window so missed exact beat frames can still start the transition backdated to the Cue Mark. Tests cover `12/0`, `0/12`, and mixed Runway/Tail combinations while allowing transition settings to remain artist-tweakable.

## Acceptance criteria

- [x] On-Air Timing can derive a Cue Sheet from current or upcoming Phrase length.
- [x] Cue Marks are stored relative to the Phrase and translated to absolute on-air beat positions only when needed.
- [x] The final phrase boundary is always present as the mandatory final Cue Mark.
- [x] Same total Phrase length reuses the existing Cue Sheet, even if phrase labels or absolute start/end beats shift.
- [x] Different total Phrase length regenerates the Cue Sheet.
- [x] An upcoming Cue Sheet promotes to current without rerolling when the upcoming Phrase becomes current.
- [x] `TimingFrame`, `DirectorStatus`, trace/status formatting, and test names expose Cue Mark concepts instead of treating Selected Phase Boundary as the canonical domain term.
- [x] Existing beat-only, unavailable Track Phase, Coast, Re-anchor, and Beat Rewind behavior remains covered by focused tests.
- [x] No Switcher queue, Loaded Cue lifecycle, Effect/Transition casting rewrite, OSC protocol change, or Play Mode behavior change is introduced in this slice.

## Validation

- Red/green tracer: `UNITY_TEST_FILTER='CueSheetTests' ./scripts/unity-tests.sh` first failed because `CueSheet` did not exist, then passed 5/5 after implementation.
- Focused seam validation: `UNITY_TEST_FILTER='OnAirTimingTests|CueSheetTests' ./scripts/unity-tests.sh` passed 20/20.
- Focused Director/timing/cue validation: `UNITY_TEST_FILTER='OnAirTimingTests|CueSheetTests|PhraseWindowTests|DirectorSyncedTailTests|SyncedCueIntentTests|TransitionBeatPlanTests|ChangeCadenceTests|EffectDeckSelectionTests' ./scripts/unity-tests.sh` passed 61/61.
- Zero-Runway regression validation: focused transition/settings/zero-runway tests passed 36/36.
- Compile validation: `./scripts/unity-compile.sh` passed with C# warning count 0.
- Full EditMode validation: `./scripts/unity-tests.sh` passed 209/209.
- Whitespace validation: `git diff --check` produced no output.
- Source search validation: `global::` no longer appears under `Assets/**/*.cs`.

## Blocked by

None.
