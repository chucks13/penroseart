// The Clock doorway: tempo facts and the offered Bar Phase / Beat Fraction clock (beat-data spec).

#nullable enable

/// <summary>
/// Tempo facts and the offered clock, one Data Surface doorway. Bar Phase is the clock every
/// Waveform is evaluated against; consumers read it instead of hand-rolling a private metronome
/// from wire facts. Facts are nullable — null is the only spelling of "not available" (Standalone
/// Mode, or a lane the wire is not supplying); wire sentinels never cross the surface.
/// </summary>
public readonly struct ClockView
{
    /// <summary>Pitch-adjusted playing tempo. Wire fact; null without a usable tempo.</summary>
    public float? Bpm { get; }

    /// <summary>
    /// The beat-interval yardstick in ms (≈ 60000 ÷ BPM; live-set mean). Sizes anything
    /// duration-relative. Wire fact.
    /// </summary>
    public float? BeatAverageMs { get; }

    /// <summary>
    /// 0..1 position through the current measure; 0 on the downbeat. The offered clock — read it
    /// instead of hand-rolling a metronome.
    /// </summary>
    public float? BarPhase { get; }

    /// <summary>0..1 position within the current beat — Bar Phase's sub-beat half.</summary>
    public float? BeatFraction { get; }

    /// <summary>Built only by the hub's per-update capture, with sentinels already translated to null.</summary>
    internal ClockView(float? bpm, float? beatAverageMs, float? barPhase, float? beatFraction)
    {
        Bpm = bpm;
        BeatAverageMs = beatAverageMs;
        BarPhase = barPhase;
        BeatFraction = beatFraction;
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Clock doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public ClockView Clock { get; private set; }

    /// <summary>Normalized position within the current four-beat bar, or zero without a live clock.</summary>
    private float BarPhase
    {
        get
        {
            if (!IsSynced)
            {
                return 0f;
            }

            var label = beatData.snapshot.beatInBar;
            return label >= 1 && label <= BeatSlotCount
                ? ((label - 1) + IntraBeatFraction()) / BeatSlotCount
                : 0f;
        }
    }

    /// <summary>
    /// Fraction elapsed into the current beat. Reads the next label's countdown so the clock does
    /// not jump while the current on-beat gate holds its own countdown at zero.
    /// </summary>
    private float IntraBeatFraction()
    {
        if (!IsSynced)
        {
            return 0f;
        }

        var snapshot = beatData.snapshot;
        var label = snapshot.beatInBar;
        if (label < 1 || label > BeatSlotCount)
        {
            return 0f;
        }

        var nextSlot = label % BeatSlotCount;
        var countdowns = snapshot.beatsCountMs;
        var msToNext = countdowns != null && nextSlot < countdowns.Length
            ? countdowns[nextSlot]
            : UnavailableMs;
        return snapshot.beatAverageMs > 0 && msToNext >= 0
            ? UnityEngine.Mathf.Clamp01((float)(snapshot.beatAverageMs - msToNext) / snapshot.beatAverageMs)
            : 0f;
    }

    /// <summary>
    /// Captures the Clock doorway from the settled transport state. In Standalone Mode every fact
    /// is null; in Synced Mode the tempo sentinels (-1 / non-positive) still translate to null
    /// per lane, while the Bar Phase clock is always present (a running 4-count is what Synced
    /// Mode means).
    /// </summary>
    private ClockView CaptureClock()
    {
        if (!IsSynced)
        {
            return default;
        }

        var snapshot = beatData.snapshot;
        return new ClockView(
            snapshot.bpm > 0f ? snapshot.bpm : (float?)null,
            snapshot.beatAverageMs > 0 ? snapshot.beatAverageMs : (float?)null,
            BarPhase,
            IntraBeatFraction());
    }
}
