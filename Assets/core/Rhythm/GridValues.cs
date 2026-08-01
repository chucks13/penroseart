// Captures timing-grid wire facts and direct position conveniences.
#nullable enable

using System;
using UnityEngine;

/// <summary>How much the sender trusts the current phrase-relative timing-grid placement.</summary>
public enum GridState
{
    /// <summary>The grid is freshly placed and trusted.</summary>
    Locked,
    /// <summary>The source is holding its last good offset.</summary>
    Coasting,
    /// <summary>A fresh observation disagrees with the held offset.</summary>
    Disputed,
}

/// <summary>Immutable phrase-relative timing-grid wire facts and derived position.</summary>
public readonly struct GridValues
{
    /// <summary>Nominal number of positions in one complete timing-grid cycle.</summary>
    private const int CycleBeats = 16;
    /// <summary>Continuous elapsed position retained for Build and Decay.</summary>
    private readonly float? elapsedBeats;

    /// <summary>Captures the wire Grid placement and its direct derived values.</summary>
    internal GridValues(GridState? state, int? beat, int? bar, float? progress, float? elapsedBeats, int startsSeen)
    {
        State = state;
        Beat = beat;
        Bar = bar;
        Progress = progress;
        this.elapsedBeats = elapsedBeats;
        StartsSeen = startsSeen;
    }

    /// <summary>The sender's trust state.</summary>
    public GridState? State { get; }
    /// <summary>One-based position within the phrase-relative 16-position timing-grid cycle.</summary>
    public int? Beat { get; }
    /// <summary>One-based four-beat subdivision within the timing-grid cycle.</summary>
    public int? Bar { get; }
    /// <summary>Derived 0..1 position through the grid.</summary>
    public float? Progress { get; }

    /// <summary>
    /// How many Grid starts this run has seen. The wire publishes positions, not events, so BeatManager
    /// derives the crossing once, here, for every consumer: compare successive reads to notice a new
    /// Grid — the absolute number means nothing.
    /// </summary>
    public int StartsSeen { get; }

    /// <summary>
    /// Rises across the nominal 16-beat cycle or requested window of whole beats; a phrase boundary
    /// may restart the observed grid before that nominal cycle completes.
    /// </summary>
    /// <param name="windowBeats">Window in whole beats; omit to use the nominal cycle.</param>
    public float Build(int? windowBeats = null) =>
        StockEnvelopes.Build(elapsedBeats, windowBeats ?? CycleBeats);

    /// <summary>
    /// Falls across the nominal 16-beat cycle or requested window of whole beats; a phrase boundary
    /// may restart the observed grid before that nominal cycle completes.
    /// </summary>
    /// <param name="windowBeats">Window in whole beats; omit to use the nominal cycle.</param>
    public float Decay(int? windowBeats = null) =>
        StockEnvelopes.Decay(elapsedBeats, windowBeats ?? CycleBeats);
}

public partial class BeatManager
{
    /// <summary>The phrase-relative timing grid and its derived values.</summary>
    public GridValues Grid { get; private set; }

    /// <summary>Grid position at the last per-beat comparison; the Grid-start detector's memory.</summary>
    private int? lastGridWireBeat;

    /// <summary>Track beat of the last comparison, so datagram repeats and heartbeats compare nothing.</summary>
    private int? lastGridComparedTrackBeat;

    /// <summary>Running count of Grid starts noticed; surfaced as <see cref="GridValues.StartsSeen"/>.</summary>
    private int gridStartsSeen;

    /// <summary>Captures the settled timing-grid wire lane, noticing any Grid start that crossed by.</summary>
    private GridValues CaptureGrid()
    {
        var wire = wireSnapshot.timingGrid;
        NoticeGridStart(wire.beat);
        if (!TryParseGridState(wire.state, out var state))
        {
            // The count survives an unavailable lane so consumers never see it dip and spring back.
            return new GridValues(null, null, null, null, null, gridStartsSeen);
        }

        float? elapsed = IsSynced && wire.beat >= 1
            ? wire.beat - 1 + IntraBeatFraction()
            : null;
        return new GridValues(
            state,
            wire.beat >= 1 ? wire.beat : null,
            wire.bar >= 1 ? wire.bar : null,
            elapsed is { } value ? Mathf.Clamp01(value / 16f) : null,
            elapsed,
            gridStartsSeen);
    }

    /// <summary>
    /// Turns the wire's grid position into the Grid-start fact. Compared once per track beat, a new Grid
    /// began when the position went down — or held at the One while the beat advanced, which is a phrase
    /// exactly one beat past a boundary restarting the grid (seen live 2026-07-31, beats 657/658).
    /// Comparing per track beat is what keeps datagram repeats and heartbeats from counting anything.
    /// </summary>
    /// <param name="gridBeat">The wire's current 1..16 grid position, or a negative unavailable value.</param>
    private void NoticeGridStart(int gridBeat)
    {
        if (gridBeat < 1 || Timing.Beat is not { } trackBeat || trackBeat == lastGridComparedTrackBeat)
        {
            return;
        }

        var crossed = lastGridWireBeat is { } last
            ? gridBeat < last || (gridBeat == 1 && last == 1)
            : gridBeat == 1;
        if (crossed)
        {
            gridStartsSeen++;
        }

        lastGridWireBeat = gridBeat;
        lastGridComparedTrackBeat = trackBeat;
    }

    /// <summary>Translates the wire's closed timing-grid state labels.</summary>
    private static bool TryParseGridState(string? value, out GridState state)
    {
        if (string.Equals(value, "locked", StringComparison.OrdinalIgnoreCase)) { state = GridState.Locked; return true; }
        if (string.Equals(value, "coasting", StringComparison.OrdinalIgnoreCase)) { state = GridState.Coasting; return true; }
        if (string.Equals(value, "disputed", StringComparison.OrdinalIgnoreCase)) { state = GridState.Disputed; return true; }
        state = default;
        return false;
    }
}
