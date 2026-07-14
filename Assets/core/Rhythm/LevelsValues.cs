// Captures normalized audio bands and their shaped forms.
#nullable enable

using UnityEngine;

/// <summary>The low, mid, and high audio-frequency bands.</summary>
public enum Band
{
    /// <summary>The low-frequency band.</summary>
    Low,
    /// <summary>The mid-frequency band.</summary>
    Mid,
    /// <summary>The high-frequency band.</summary>
    High,
}

/// <summary>One immutable low/mid/high level set and its direct derived readings.</summary>
public readonly struct LevelBands
{
    /// <summary>Creates one clamped low/mid/high band set.</summary>
    internal LevelBands(float low, float mid, float high)
    {
        Low = Mathf.Clamp01(low);
        Mid = Mathf.Clamp01(mid);
        High = Mathf.Clamp01(high);
    }

    /// <summary>Low-band strength from zero to one.</summary>
    public float Low { get; }
    /// <summary>Mid-band strength from zero to one.</summary>
    public float Mid { get; }
    /// <summary>High-band strength from zero to one.</summary>
    public float High { get; }
    /// <summary>Mean band strength from zero to one.</summary>
    public float Average => (Low + Mid + High) / 3f;
    /// <summary>The strongest band's value.</summary>
    public float Strongest => Mathf.Max(Low, Mathf.Max(Mid, High));
    /// <summary>The strongest band, with ties resolved Low then Mid then High.</summary>
    public Band StrongestBand => Low >= Mid && Low >= High ? Band.Low : Mid >= High ? Band.Mid : Band.High;

    /// <summary>Spectral balance from zero at low through one at high; zero at silence.</summary>
    public float Centroid
    {
        get
        {
            var total = Low + Mid + High;
            return total <= 0f ? 0f : ((Mid * 0.5f) + High) / total;
        }
    }

    /// <summary>How strongly the loudest band exceeds the quietest; zero at silence.</summary>
    public float Dominance
    {
        get
        {
            var strongest = Strongest;
            return strongest <= 0f
                ? 0f
                : (strongest - Mathf.Min(Low, Mathf.Min(Mid, High))) / strongest;
        }
    }
}

/// <summary>The normalized wire levels and their smoothed and tempo-draining forms.</summary>
public readonly struct LevelsValues
{
    /// <summary>Captures the three forms of one level reading.</summary>
    internal LevelsValues(LevelBands normalized, LevelBands smoothed, LevelBands peak)
    {
        Normalized = normalized;
        Smoothed = smoothed;
        Peak = peak;
    }

    /// <summary>The wire values, or zero immediately when the wire level lane is unavailable.</summary>
    public LevelBands Normalized { get; }
    /// <summary>The attack/release follower.</summary>
    public LevelBands Smoothed { get; }
    /// <summary>Instant rise with a tempo-based linear fall.</summary>
    public LevelBands Peak { get; }
}

public partial class BeatManager
{
    /// <summary>Attack time constant in seconds for rising smoothed levels.</summary>
    [SerializeField]
    private float levelsAttackSeconds = 0.02f;

    /// <summary>Release time constant in seconds for falling smoothed levels.</summary>
    [SerializeField]
    private float levelsReleaseSeconds = 0.15f;

    /// <summary>Latest attack/release follower state.</summary>
    private LevelBands smoothedLevels;
    /// <summary>Latest tempo-draining peak state.</summary>
    private LevelBands peakLevels;
    /// <summary>Last clock sample used by both shaping algorithms.</summary>
    private float lastSmoothingTime;
    /// <summary>Whether a prior shaping clock sample exists.</summary>
    private bool hasSmoothingClock;
    /// <summary>Whether shaping state has been initialized from an input sample.</summary>
    private bool hasLevelShaping;

    /// <summary>Normalized, smoothed, and peak audio levels; never null.</summary>
    public LevelsValues Levels { get; private set; }

    /// <summary>Advances Smoothed and Peak from the current normalized wire levels.</summary>
    private void UpdateLevelsShaping(float timeSeconds)
    {
        var deltaSeconds = hasSmoothingClock ? Mathf.Max(0f, timeSeconds - lastSmoothingTime) : 0f;
        lastSmoothingTime = timeSeconds;
        hasSmoothingClock = true;

        var normalized = ReadNormalizedLevels();
        if (!hasLevelShaping)
        {
            smoothedLevels = normalized;
            peakLevels = normalized;
            hasLevelShaping = true;
            return;
        }

        smoothedLevels = new LevelBands(
            SmoothTowards(smoothedLevels.Low, normalized.Low, deltaSeconds),
            SmoothTowards(smoothedLevels.Mid, normalized.Mid, deltaSeconds),
            SmoothTowards(smoothedLevels.High, normalized.High, deltaSeconds));

        var drain = deltaSeconds / PeakDrainBeatSeconds();
        peakLevels = new LevelBands(
            Mathf.Max(peakLevels.Low - drain, normalized.Low),
            Mathf.Max(peakLevels.Mid - drain, normalized.Mid),
            Mathf.Max(peakLevels.High - drain, normalized.High));
    }

    /// <summary>Reads clamped wire levels, returning silence when any lane is unavailable.</summary>
    private LevelBands ReadNormalizedLevels()
    {
        var raw = wireSnapshot.levels;
        var available = raw.low >= 0f && raw.mid >= 0f && raw.high >= 0f;
        return available
            ? new LevelBands(raw.low, raw.mid, raw.high)
            : default;
    }

    /// <summary>Returns the Peak fall time, using one beat when tempo is usable.</summary>
    private float PeakDrainBeatSeconds() =>
        IsSynced && wireSnapshot.beatAverageMs > 0
            ? wireSnapshot.beatAverageMs / 1000f
            : 0.5f;

    /// <summary>Moves one band through the configured attack or release follower.</summary>
    private float SmoothTowards(float current, float target, float deltaSeconds)
    {
        var timeConstant = target > current ? levelsAttackSeconds : levelsReleaseSeconds;
        if (timeConstant <= 0f)
        {
            return target;
        }

        var alpha = 1f - Mathf.Exp(-deltaSeconds / timeConstant);
        return current + ((target - current) * alpha);
    }

    /// <summary>Captures all three level forms for public reading.</summary>
    private LevelsValues CaptureLevels() =>
        new(ReadNormalizedLevels(), smoothedLevels, peakLevels);
}
