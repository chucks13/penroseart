// Captures tempo, absolute track position, and derived beat/bar progress.

#nullable enable

/// <summary>Immutable tempo and musical-position values captured for one BeatManager update.</summary>
public readonly struct TimingValues
{
    /// <summary>Captures wire timing values and direct progress calculations.</summary>
    internal TimingValues(float? bpm, int? beatAverageMilliseconds, int? beat, int? totalBeats,
        int? bar, int? beatInBar, int? nextBarMilliseconds, float? beatProgress, float? barProgress)
    {
        Bpm = bpm;
        BeatAverageMilliseconds = beatAverageMilliseconds;
        Beat = beat;
        TotalBeats = totalBeats;
        Bar = bar;
        BeatInBar = beatInBar;
        NextBarMilliseconds = nextBarMilliseconds;
        BeatProgress = beatProgress;
        BarProgress = barProgress;
    }

    /// <summary>Pitch-adjusted playing tempo from the wire.</summary>
    public float? Bpm { get; }

    /// <summary>Mean beat interval from the wire, in milliseconds.</summary>
    public int? BeatAverageMilliseconds { get; }

    /// <summary>One-based absolute track beat from the wire.</summary>
    public int? Beat { get; }

    /// <summary>Track length in beats from the wire.</summary>
    public int? TotalBeats { get; }

    /// <summary>One-based absolute four-beat bar from the wire.</summary>
    public int? Bar { get; }

    /// <summary>Current musical count, from 1 through 4.</summary>
    public int? BeatInBar { get; }

    /// <summary>Milliseconds until the next bar line from the wire.</summary>
    public int? NextBarMilliseconds { get; }

    /// <summary>Derived 0..1 position through the current beat.</summary>
    public float? BeatProgress { get; }

    /// <summary>Derived 0..1 position through the current four-beat bar.</summary>
    public float? BarProgress { get; }
}

public partial class BeatManager
{
    /// <summary>Tempo and position values captured from the settled musical frame.</summary>
    public TimingValues Timing { get; private set; }

    /// <summary>Normalized position within the current four-beat bar.</summary>
    private float BarPhase
    {
        get
        {
            if (!IsSynced)
            {
                return 0f;
            }

            var count = wireSnapshot.beatInBar;
            return count is >= 1 and <= BeatSlotCount
                ? (count - 1 + IntraBeatFraction()) / BeatSlotCount
                : 0f;
        }
    }

    /// <summary>Fraction elapsed into the current beat, contrived from the next count's wire countdown.</summary>
    private float IntraBeatFraction()
    {
        if (!IsSynced)
        {
            return 0f;
        }

        var snapshot = wireSnapshot;
        var count = snapshot.beatInBar;
        if (count is < 1 or > BeatSlotCount)
        {
            return 0f;
        }

        var nextSlot = count % BeatSlotCount;
        var countdowns = snapshot.beatsCountMs;
        var nextCountMs = countdowns != null && nextSlot < countdowns.Length
            ? countdowns[nextSlot]
            : UnavailableMs;
        return snapshot.beatAverageMs > 0 && nextCountMs >= 0
            ? UnityEngine.Mathf.Clamp01((float)(snapshot.beatAverageMs - nextCountMs) / snapshot.beatAverageMs)
            : 0f;
    }

    /// <summary>Captures all tempo and absolute-position values without another public navigation layer.</summary>
    private TimingValues CaptureTiming()
    {
        var snapshot = wireSnapshot;
        return new TimingValues(
            snapshot.bpm > 0f ? snapshot.bpm : null,
            snapshot.beatAverageMs > 0 ? snapshot.beatAverageMs : null,
            snapshot.beat.current >= 1 ? snapshot.beat.current : null,
            snapshot.beat.total >= 1 ? snapshot.beat.total : null,
            snapshot.bar.current >= 1 ? snapshot.bar.current : null,
            snapshot.beatInBar is >= 1 and <= BeatSlotCount ? snapshot.beatInBar : null,
            snapshot.bar.nextMs >= 0 ? snapshot.bar.nextMs : null,
            IsSynced ? IntraBeatFraction() : null,
            IsSynced ? BarPhase : null);
    }
}
