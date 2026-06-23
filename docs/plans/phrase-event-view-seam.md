# Design: move phrase-event presentation to the data (Candidate 3)

Status: Implemented
Source: architecture review `docs/architecture-reviews/architecture-review-penroseart-editor-2026-06-17T2125.html`, Candidate 3

## Problem

Three formatting functions live in `BeatManagerDrawer` and are `internal static` **purely so the tests
can reach them** — `BuildPhraseEventChipLabel`, `GetPhraseEventMeterValue`, `BuildPhraseEventReadout`.
That is the textbook "pure functions extracted for testability, but the bug lives in how they're
called" smell: `BeatManagerDrawerVisualModelTests` crosses *past* the drawer's real interface into its
private rendering helpers. The logic is presentation of a `PhraseEventInfo` (Fill/Drop), yet it sits
~830 lines away from the data it formats (`PhraseEventInfo` in `Assets/core/Rhythm/BeatManagerQueries.cs`).

Two facts the review card didn't surface, found by grounding it against the code:

- `BuildPhraseEventReadout` depends on `FormatBeats`/`FormatCount`, and those two are **also** used by
  `DrawEnergyRow` and `DrawPhaseRow`. They are shared rhythm-query value-formatters, not
  phrase-event-private — so moving phrase-event presentation forces a decision about them.
- The chip *color* (now/soon/idle) is decided in the drawer from `info.inProgress` /
  `info.beatsUntilStart`. The *classification* is data logic, duplicated between the chip label and the
  chip color; the *colors* are editor concerns.

This is a **locality + honest-test-surface** win. Unlike Candidate 1 it carves out no new policy, but
it does pull a real seam (the display model) back next to its data and gives the tests a legitimate
interface to target.

## Decision (locked)

Co-locate the phrase-event display model with `PhraseEventInfo` in the **runtime** assembly so the
inspector and any future telnet/OSC/debug readout share one vocabulary, and lift the shared nullable
formatters alongside.

- **Shape:** a combined `readonly struct PhraseEventView` built by `PhraseEventView.Of(info)`, holding
  `Chip` / `Meter` / `Readout` / `State`. One call replaces three; the now/soon/idle classification —
  previously duplicated between the chip label and the chip color — lives once as `State`.
- **State enum:** `PhraseEventState { Now, Soon, Idle }`. The view classifies; the drawer maps
  `State → chip color`, keeping editor colors in the editor.
- **Assembly:** runtime core (`Assets/core/`), not editor-only — that runtime reuse is the candidate's
  leverage argument. Pure string/float logic, zero `UnityEditor` dependency.
- **Shared formatters:** move `FormatBeats`/`FormatCount` out of the drawer into a small
  `RhythmText.Beats(int?)` / `RhythmText.Count(int?)` helper in core, and repoint `DrawEnergyRow` /
  `DrawPhaseRow` at it. One formatter vocabulary for all three rhythm-query rows; no duplication left
  behind (the failure mode Candidate 2 just deleted).

## Shape

### `Assets/core/Rhythm/PhraseEventView.cs` (new — runtime)

```csharp
public enum PhraseEventState { Now, Soon, Idle }

public readonly struct PhraseEventView
{
    public readonly string Chip;            // "NOW" / "IN {beats}" / "—"
    public readonly float Meter;            // progress | anticipation | 0
    public readonly string Readout;         // "ends in 9b · len 16 · ×1", etc.
    public readonly PhraseEventState State; // Now | Soon | Idle
    public static PhraseEventView Of(PhraseEventInfo info);
}
```

### `Assets/core/Rhythm/RhythmText.cs` (new — runtime)

```csharp
public static class RhythmText
{
    public static string Beats(int? value); // "{n}b" or "—"
    public static string Count(int? value); // "{n}"  or "—"
}
```

### Call sites

- `BeatManagerDrawer.DrawPhraseEventContent` → `var view = PhraseEventView.Of(info)`, then a
  `view.State` switch picks `nowColor` / `soonColor` / `PhraseEventIdleChipColor`; draws `view.Chip`,
  `view.Meter`, `view.Readout`.
- `BeatManagerDrawer.DrawEnergyRow` / `DrawPhaseRow` → `RhythmText.Beats` / `RhythmText.Count`.

### Deletions (drawer)

`BuildPhraseEventChipLabel`, `GetPhraseEventMeterValue`, `BuildPhraseEventReadout`, `FormatBeats`,
`FormatCount`. `FormatMs` stays (separate consumers).

### Tests

Split the four phrase-event tests out of `BeatManagerDrawerVisualModelTests` into new
`Assets/Tests/Editor/PhraseEventViewTests.cs`, retargeted at `PhraseEventView.Of(...)` and gaining a
`State` classification test (5 total). The beat-dot-glyph and eighth-pulse tests stay in
`BeatManagerDrawerVisualModelTests` — they are genuinely drawer-owned and have no co-located data type.

## Behavior notes

Behavior-preserving: the chip/meter/readout strings are byte-identical to the prior drawer output (the
existing test expectations port verbatim), and the `State`→color mapping reproduces the prior inline
color branch exactly.

## Validation

`scripts/unity-compile.sh` (0/0) and `scripts/osc-tests.sh` / the EditMode runner — the ported
phrase-event tests plus the new state test must pass. No visual change to the inspector.

## Out of scope

- Energy/Phase/Levels row *bodies* (they keep rendering inline; they only adopt the shared
  `RhythmText` formatters).
- Actually wiring a telnet/OSC readout to `PhraseEventView` — the point is that it is now possible, not
  that it is done.
