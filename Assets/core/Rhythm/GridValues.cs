// Captures timing-grid wire facts and direct position conveniences.
#nullable enable

using System;
using UnityEngine;

/// <summary>How much the sender trusts the current sixteen-beat grid placement.</summary>
public enum GridState
{
    /// <summary>The grid is freshly anchored and trusted.</summary>
    Locked,
    /// <summary>The source is holding its last good anchor.</summary>
    Coasting,
    /// <summary>A fresh observation disagrees with the held anchor.</summary>
    Disputed,
}

/// <summary>Immutable sixteen-beat timing-grid wire facts and derived position.</summary>
public readonly struct GridValues
{
    /// <summary>Number of beats in the timing-grid cycle.</summary>
    private const float CycleBeats = 16f;
    /// <summary>Continuous elapsed position retained for Build and Decay.</summary>
    private readonly float? elapsedBeats;

    /// <summary>Captures the wire Grid placement and its direct derived values.</summary>
    internal GridValues(GridState? state, int? beat, int? bar, float? progress, float? elapsedBeats)
    {
        State = state;
        Beat = beat;
        Bar = bar;
        Progress = progress;
        this.elapsedBeats = elapsedBeats;
    }

    /// <summary>The sender's trust state.</summary>
    public GridState? State { get; }
    /// <summary>One-based beat within the sixteen-beat grid.</summary>
    public int? Beat { get; }
    /// <summary>One-based bar within the four-bar grid.</summary>
    public int? Bar { get; }
    /// <summary>Derived 0..1 position through the grid.</summary>
    public float? Progress { get; }

    /// <summary>Rises across the full grid or requested beat duration.</summary>
    public float Build(float? durationBeats = null) =>
        StockEnvelopes.Build(elapsedBeats, durationBeats ?? CycleBeats);

    /// <summary>Falls across the full grid or requested beat duration.</summary>
    public float Decay(float? durationBeats = null) =>
        StockEnvelopes.Decay(elapsedBeats, durationBeats ?? CycleBeats);
}

public partial class BeatManager
{
    /// <summary>The sixteen-beat timing grid and its derived values.</summary>
    public GridValues Grid { get; private set; }

    /// <summary>Captures the settled timing-grid wire lane.</summary>
    private GridValues CaptureGrid()
    {
        var wire = wireSnapshot.timingGrid;
        if (!TryParseGridState(wire.state, out var state))
        {
            return default;
        }

        float? elapsed = IsSynced && wire.beat >= 1
            ? wire.beat - 1 + IntraBeatFraction()
            : null;
        return new GridValues(
            state,
            wire.beat >= 1 ? wire.beat : null,
            wire.bar >= 1 ? wire.bar : null,
            elapsed is { } value ? Mathf.Clamp01(value / 16f) : null,
            elapsed);
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
