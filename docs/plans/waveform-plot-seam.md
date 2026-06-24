# Design: extract the shared waveform-plot primitive (Candidate 2, narrowed)

Status: Implemented
Source: architecture review `docs/architecture-reviews/architecture-review-penroseart-editor-2026-06-17T2125.html`, Candidate 2

## Problem (and how the code narrowed the candidate)

The review's Candidate 2 proposed lifting a whole "Penrose inspector vocabulary"
(meter · chip · swatch · panel · styles) into a shared `InspectorWidgets` module. Reading the
code, that seam is mostly **hypothetical**: meter/chip/swatch/panel have exactly **one** consumer
(`BeatManagerDrawer`), and `EffectSelectorDrawer` is just an `EditorGUI.Popup` — it shares no
visual primitives at all. Per codebase-design ("one adapter = hypothetical seam, two = real"),
lifting the single-consumer vocabulary now would buy nothing and add indirection.

The one **real** seam is the **waveform plot**. It is duplicated nearly line-for-line in two files,
with identical color constants:

- `BeatManagerDrawer.DrawWaveformStrip` — track + 4/4 gridlines + AA curve, **plus a live playhead +
  numeric readout** (dashboard).
- `WaveformPoolEditor.DrawPlot` — the same track + gridlines + AA curve, static (authoring view).

Shared `Track (0.055,0.065,0.085)`, `Grid (1,1,1,0.07)`, and `Curve (0.12,0.92,1)` appear verbatim
in both. They *will* drift.

This is a **locality/dedup** win, not a testability one: pure IMGUI drawing can't be unit-tested
through an interface. (Contrast Candidate 1, which carved out testable policy.)

## Decision (locked)

Narrow Candidate 2 to **only the waveform plot + its 3 shared colors**. Leave the single-consumer
meter/chip/swatch/panel primitives where they are; promote them later only if a second consumer
actually appears.

## Shape

### `WaveformPlot` (`Assets/Editor/Rhythm/Waveforms/WaveformPlot.cs`, editor-only static class)

One method, one public color; the value -> Y alignment invariant lives here once and never leaks:

```csharp
public static readonly Color Curve = new(0.12f, 0.92f, 1f);   // canonical "on" color
public static void Draw(Rect rect, Waveform wf, Color curveColor, float? playheadPhase = null);
```

`Draw` renders: dark `Track`, `Waveform.BeatsPerBar` gridlines, the AA curve of `wf.Evaluate` in
`curveColor` (vPad `3f`, 128 samples). When `playheadPhase` has a value it also draws the aligned
playhead line + dot. The numeric readout is the caller's job — it lives **outside** the plot rect.

- **Internal:** `Track`, `Grid`, `Playhead`, `PlayheadLine`; `vPad = 3f`; `Samples = 128`.
- **Public:** `Curve` (so both callers dedup the on-color).

### Call sites

- `WaveformPoolEditor.DrawPlot` → `WaveformPlot.Draw(rect, wf, wf.IsMalformed ? MalformedCurveColor : WaveformPlot.Curve)`.
  Keeps its own `MalformedCurveColor`.
- `BeatManagerDrawer.DrawWaveformStrip` → computes its narrower plot rect (reserving
  `WaveformValueWidth = 46f`), calls
  `WaveformPlot.Draw(plot, wf, active ? WaveformPlot.Curve : WaveformCurveIdleColor, active ? barPhase : null)`,
  then draws its own `0.00` / `--` readout. Keeps its own `WaveformCurveIdleColor`.

### Deletions

Pool: `TrackColor`, `GridColor`, `CurveColor`, `PlotSamples`, `BeatSlots`.
Dashboard: `WaveformTrackColor`, `WaveformGridColor`, `WaveformCurveColor`, `WaveformPlayheadColor`,
`WaveformPlayheadLineColor`, `WaveformPlotSamples`, and the track/grid/curve/playhead body of
`DrawWaveformStrip`. `BeatSlotCount` stays (other rows use it).

## Behavior notes

- Unify `vPad` to `3f` (pool shifts ~1px) and samples to `128` (dashboard smooths from 96). Both are
  imperceptible and were the point of de-duplicating.
- Grid uses `Waveform.BeatsPerBar` (the canonical constant the pool already referenced), not a literal `4`.
- The consolidation is behavior-preserving: track/grid/curve/playhead are all repaint-only operations,
  so drawing them all behind the single repaint guard matches the prior visible output.

## Validation

`scripts/unity-compile.sh` (0 errors / 0 warnings), then visually confirm both views render unchanged:
the BeatManager dashboard waveform strip (with live playhead) and the WaveformPool authoring plot.
No new tests — pure drawing, and project philosophy avoids test scaffolding for purely visual code.

## Out of scope

- The meter/chip/swatch/panel "vocabulary" (single consumer — deferred until a second one is real).
- Candidate 3 (rhythm-query presentation).
