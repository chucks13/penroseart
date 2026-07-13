// The Grid doorway: the wall's 16-beat alignment clock, always present with nullable facts
// (beat-data spec — the reshape of the old nullable info-struct query).

#nullable enable

using System;
using UnityEngine;

/// <summary>
/// How much to trust where the One sits: a fresh locked anchor, a held coasting anchor, or a
/// disputed fresh observation. Losing the clock is represented by an unavailable Grid doorway,
/// not by another enum value.
/// </summary>
public enum GridState
{
    /// <summary>The grid is freshly anchored and trusted.</summary>
    Locked,

    /// <summary>No fresh anchor is available, so the source holds the last good offset.</summary>
    Coasting,

    /// <summary>A fresh offset disagreed with the held one and awaits a clean re-latch.</summary>
    Disputed,
}

/// <summary>
/// The wall's 16-beat alignment clock, one Data Surface doorway. Cyclic, so Started/Ended
/// collapse into the one wrap Edge: the 16-count coming back to the One. Grid State is served
/// data — trust is the consumer's to weigh, never a gate the hub applies; losing the clock is a
/// null <see cref="Current"/> read (a Standalone read, not an error), never a fourth state. The
/// doorway itself is always present, so its Edge and Stock Envelopes outlive the facts.
/// </summary>
public readonly struct GridView
{
    /// <summary>One Grid is exactly 16 beats — the envelopes' default window and the progress denominator.</summary>
    internal const float CycleBeats = 16f;

    /// <summary>Beats elapsed since the One, smoothed by the shared sub-beat clock; null when no beat is placed.</summary>
    private readonly float? elapsedBeats;

    /// <summary>
    /// Grid facts; null when the wire places no grid. The contract's partial shape — a focus
    /// player coasting with no placeable beat — is real data: facts present with a State but null
    /// position, distinct from the complete unavailable shape's null read.
    /// </summary>
    public GridFacts? Current { get; }

    /// <summary>
    /// Edge: the 16-count wrapped back to the One this frame — a new Grid began. Never null. The
    /// current placed beat returning to the One after a different placed beat is the wrap. A
    /// backward position that does not reach the One is not a wrap; the grid appearing is a mode
    /// change, not a boundary the music crossed.
    /// </summary>
    public bool Wrapped { get; }

    /// <summary>
    /// Stock Envelope: rises 0→1 across the Grid, re-anchoring at each wrap. Duration in beats;
    /// null = the full 16-beat cycle. Rests at 0 while no beat is placed.
    /// </summary>
    public float Build(float? durationBeats = null)
    {
        return StockEnvelopes.Build(elapsedBeats, durationBeats ?? CycleBeats);
    }

    /// <summary>
    /// Stock Envelope: peaks at 1 on the One and falls to 0 across its window, re-anchoring at
    /// each wrap. Same duration rule as <see cref="Build"/>.
    /// </summary>
    public float Decay(float? durationBeats = null)
    {
        return StockEnvelopes.Decay(elapsedBeats, durationBeats ?? CycleBeats);
    }

    /// <summary>Built only by the hub's per-update capture, with the wrap edge already evaluated.</summary>
    internal GridView(GridFacts? current, bool wrapped, float? elapsedBeats)
    {
        Current = current;
        Wrapped = wrapped;
        this.elapsedBeats = elapsedBeats;
    }
}

/// <summary>
/// Facts while the wire serves a grid. All three states are on-grid readings differing only in
/// trust; the position facts are null in the contract's partial shape (a coasting focus player
/// with no placeable beat).
/// </summary>
public readonly struct GridFacts
{
    /// <summary>How much to trust where the One sits: Locked, Coasting, or Disputed. Served data, never a gate.</summary>
    public GridState State { get; }

    /// <summary>One-based 1..16 grid beat; 1 is the One. Null when no beat can be placed.</summary>
    public int? Beat { get; }

    /// <summary>One-based 1..4 bar within the grid cycle. Null when no beat can be placed.</summary>
    public int? Bar { get; }

    /// <summary>0..1 position through the 16-beat Grid, smoothed by the shared sub-beat clock. Null when no beat can be placed.</summary>
    public float? Progress { get; }

    /// <summary>Built only by the hub's per-update capture.</summary>
    internal GridFacts(GridState state, int? beat, int? bar, float? progress)
    {
        State = state;
        Beat = beat;
        Bar = bar;
        Progress = progress;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Grid doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public GridView Grid { get; private set; }

    /// <summary>
    /// Prior placed grid beat, retained between hub updates so the wrap edge genuinely witnesses
    /// the 16-count returning to the One (ADR-0015). Null while no beat was placed, so the grid
    /// appearing never reads as a wrap.
    /// </summary>
    private float? previousGridBeat;

    /// <summary>
    /// Captures the Grid doorway from the settled transport state. Losing the clock (Standalone
    /// Mode) is a null Grid read whatever the lane carries; with the clock running, an empty or
    /// unrecognized state word is no usable grid, the contract's partial shape (-1 -1 "coasting")
    /// serves state-only facts, and a placed beat serves the full position.
    /// </summary>
    private GridView CaptureGrid()
    {
        var timingGrid = beatData.snapshot.timingGrid;

        GridFacts? facts = null;
        float? elapsed = null;
        if (IsSynced && TryParseGridState(timingGrid.state, out var gridState))
        {
            float? progress = null;
            if (timingGrid.beat >= 1)
            {
                elapsed = (timingGrid.beat - 1) + IntraBeatFraction();
                progress = Mathf.Clamp01(elapsed.Value / GridView.CycleBeats);
            }

            facts = new GridFacts(
                gridState,
                timingGrid.beat >= 1 ? timingGrid.beat : (int?)null,
                timingGrid.bar >= 1 ? timingGrid.bar : (int?)null,
                progress);
        }

        var phase = facts?.Beat is { } placedBeat ? (float?)placedBeat : null;
        var wrapped = previousGridBeat is { } previousBeat
            && phase == 1f
            && previousBeat != 1f;
        previousGridBeat = phase;

        return new GridView(facts, wrapped, elapsed);
    }

    /// <summary>Parses the closed wire grid-state vocabulary without applying trust policy.</summary>
    private static bool TryParseGridState(string? state, out GridState gridState)
    {
        if (string.Equals(state, "locked", StringComparison.OrdinalIgnoreCase))
        {
            gridState = GridState.Locked;
            return true;
        }

        if (string.Equals(state, "coasting", StringComparison.OrdinalIgnoreCase))
        {
            gridState = GridState.Coasting;
            return true;
        }

        if (string.Equals(state, "disputed", StringComparison.OrdinalIgnoreCase))
        {
            gridState = GridState.Disputed;
            return true;
        }

        gridState = default;
        return false;
    }
}
