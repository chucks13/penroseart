# Make Cue Sheet the On-Air Timing plan

Status: ready-for-agent

## Parent

- `.scratch/deepen-director-on-air-timing/PRD.md`

## Why this exists

ADR 0005 and the cue-sheet investigation settled the domain model: On-Air Timing should expose **Cue Sheet** / **Cue Mark** concepts, not make **Selected Phase Boundary** the canonical planning language. The current code already has most of the behavior under `SelectedPhaseBoundaryPlan` and `OnAirTiming.SelectedPhaseBoundaryPlans`, but its identity and interface are still implementation-shaped.

The important mismatch is identity: Hunter wants a Cue Sheet to be reusable when the Phrase's total length is the same. Current `SelectedPhaseBoundaryPlan.Matches(PhraseWindow)` requires exact absolute start, end, and length, so same-length Phrase metadata shifts can reroll the plan.

## Current code findings

- `Assets/core/SelectedPhaseBoundaryPlan.cs` builds absolute selected boundaries from a `PhraseWindow`; it randomly chooses eligible interior 16-beat Phase Boundaries and always appends the phrase end.
- `SelectedPhaseBoundaryPlan.Matches(...)` currently compares absolute `PhraseStartBeat`, `PhraseEndBeat`, and `PhraseLengthBeats`; this is the confirmed mismatch with length-only Cue Sheet reuse.
- `Assets/core/OnAirTiming.cs` owns one current cursor plus one upcoming plan in the private `SelectedPhaseBoundaryPlans` type.
- Upcoming Track Phase (`TrackPhaseActive == 0`) is already preplanned through `PlanUpcoming(...)`; active Track Phase (`TrackPhaseActive >= 1`) builds/promotes current plans.
- `TimingFrame` still exposes `SelectedPhaseBoundary`, `BeatsUntilSelectedPhaseBoundary`, and `TimingFrameSource.SelectedPhaseBoundary` as the Director-facing vocabulary.
- Existing focused coverage lives in `Assets/Tests/Editor/OnAirTimingTests.cs` and `Assets/Tests/Editor/SelectedPhaseBoundaryPlanTests.cs`.

## What to build

Replace the current phrase-target planning concept with explicit **Cue Sheet** / **Cue Mark** behavior at the On-Air Timing seam.

A Cue Sheet should be generated from a Phrase's total beat length. It stores relative Cue Mark offsets inside the Phrase, always includes the final phrase boundary as the mandatory final Cue Mark, and can be instantiated against the current on-air Phrase start only when an absolute beat is needed for the current `TimingFrame`.

This is a hard-cut vocabulary/model cleanup, not a compatibility layer. Reuse the existing selection behavior where it is still correct, but stop making absolute Selected Phase Boundary identity the canonical model.

## Implementation guidance

- Prefer a small deep timing-plan module/value, e.g. `CueSheet`, with `LengthBeats` plus relative marks. The caller should not need to know how random interior marks were chosen.
- Current and upcoming On-Air Timing state should hold Cue Sheets and cursor position, not absolute selected-boundary plan identity.
- Absolute beats should be derived from `PhraseWindow.StartBeat + CueMark.OffsetBeats` only for the current on-air frame/status/cue calculation.
- The final Cue Mark should be the Phrase length offset, not a Transition start or completion beat.
- Preserve the existing Track Phase tri-state behavior: active current Phrase (`1`), upcoming Phrase (`0`), unavailable (`-1`).
- Be explicit about the phrase-start edge case. Current code can include a future phrase start internally for turnover handling; do not expose phrase start (`offset 0`) as an ordinary random Cue Mark unless the glossary/ADR are deliberately updated. The previous Phrase's final Cue Mark already covers the shared boundary.
- `Selected Phase Boundary` may remain as a private implementation detail during the refactor only if the Director-facing interface and canonical status/docs use Cue Mark language.

## Acceptance criteria

- [ ] On-Air Timing can derive a Cue Sheet from current or upcoming Phrase length.
- [ ] Cue Marks are stored relative to the Phrase and translated to absolute on-air beat positions only when needed.
- [ ] The final phrase boundary is always present as the mandatory final Cue Mark.
- [ ] Same total Phrase length reuses the existing Cue Sheet, even if phrase labels or absolute start/end beats shift.
- [ ] Different total Phrase length regenerates the Cue Sheet.
- [ ] An upcoming Cue Sheet promotes to current without rerolling when the upcoming Phrase becomes current.
- [ ] `TimingFrame`, `DirectorStatus`, trace/status formatting, and test names expose Cue Mark concepts instead of treating Selected Phase Boundary as the canonical domain term.
- [ ] Existing beat-only, unavailable Track Phase, Coast, Re-anchor, and Beat Rewind behavior remains covered by focused tests.
- [ ] No Switcher queue, Loaded Cue lifecycle, Effect/Transition casting rewrite, OSC protocol change, or Play Mode behavior change is introduced in this slice.

## Test guidance

Add or rename focused seam tests before the implementation:

- `CueSheet.Build` returns relative marks and always includes the final phrase-length mark.
- Same Phrase length reuses the existing current/upcoming Cue Sheet despite shifted absolute start/end beats.
- Different Phrase length regenerates the sheet.
- Upcoming Cue Sheet promotes to current without rerolling.
- Existing `OnAirTimingTests` still pass for Coast, Re-anchor, unavailable Track Phase, same-window Beat Rewind, small jitter backsteps, and beat-only grid timing.
- Existing `SelectedPhaseBoundaryPlanTests` should either evolve into Cue Sheet tests or be removed when the old type is deleted.

## Suggested validation

- `./scripts/unity-compile.sh`
- `UNITY_TEST_FILTER='OnAirTimingTests|SelectedPhaseBoundaryPlanTests|PhraseWindowTests|DirectorSyncedTailTests' ./scripts/unity-tests.sh`
- `git diff --check`

## Blocked by

None - can start immediately
