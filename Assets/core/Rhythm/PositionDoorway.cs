// The Position doorway: track position facts (beat-data spec).

#nullable enable

/// <summary>
/// Track position facts, one Data Surface doorway — all wire facts. Positions, not counters:
/// seeks, scratches, and loops can move them backward, so derive bar/beat-in-bar from the absolute
/// beat when cross-fact coherence matters. Facts are nullable; wire sentinels never cross.
/// </summary>
public readonly struct PositionView
{
    /// <summary>One-based absolute track beat. With <see cref="TotalBeats"/>, a 0..1 track progress.</summary>
    public int? Beat { get; }

    /// <summary>Track length in beats. May arrive later than the live fields.</summary>
    public int? TotalBeats { get; }

    /// <summary>Absolute four-beat bar counter.</summary>
    public int? Bar { get; }

    /// <summary>The 1-2-3-4 count — the lane for triggering on set counts with no tempo aspect.</summary>
    public int? BeatInBar { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated to null.</summary>
    internal PositionView(int? beat, int? totalBeats, int? bar, int? beatInBar)
    {
        Beat = beat;
        TotalBeats = totalBeats;
        Bar = bar;
        BeatInBar = beatInBar;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Position doorway, captured once per hub update ahead of effect Draw — identical for
    /// every reader within a frame.
    /// </summary>
    public PositionView Position { get; private set; }

    /// <summary>
    /// Captures the Position doorway from the settled transport state. Position facts are clock
    /// facts: Standalone Mode reads all null (stale wire positions must never outlive the clock),
    /// and each lane's -1 sentinel translates to null on its own.
    /// </summary>
    private PositionView CapturePosition()
    {
        if (!IsSynced)
        {
            return default;
        }

        var snapshot = beatData.snapshot;
        return new PositionView(
            snapshot.beat.current >= 1 ? snapshot.beat.current : (int?)null,
            snapshot.beat.total >= 1 ? snapshot.beat.total : (int?)null,
            snapshot.bar.current >= 1 ? snapshot.bar.current : (int?)null,
            snapshot.beatInBar >= 1 && snapshot.beatInBar <= BeatSlotCount ? snapshot.beatInBar : (int?)null);
    }
}
