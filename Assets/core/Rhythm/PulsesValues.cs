// Exposes the wire pulse and tempo-derived musical subdivision pulses.

#nullable enable

using UnityEngine;

/// <summary>Immutable wire and tempo-derived pulse values captured for one BeatManager update.</summary>
public readonly struct PulsesValues
{
    /// <summary>Whether duration-based pulse reads are available.</summary>
    private readonly bool synced;
    /// <summary>Captured 0..1 position through the current bar.</summary>
    private readonly float barProgress;

    /// <summary>Captures the pulse values and duration clock for one update.</summary>
    internal PulsesValues(bool live, bool synced, float beat, float offBeat, float barProgress)
    {
        this.synced = synced;
        Beat = live ? beat : null;
        OffBeat = synced ? offBeat : null;
        this.barProgress = barProgress;
    }

    /// <summary>The wire's own beat triangle.</summary>
    public float? Beat { get; }

    /// <summary>Tempo-derived pulse centered on each Off Beat.</summary>
    public float? OffBeat { get; }

    /// <summary>Returns a tempo-based 1→0 pulse for every selected musical duration.</summary>
    public float? Every(Duration duration)
    {
        var phase = synced ? DurationClock.Phase(barProgress, duration) : null;
        return phase is { } value ? DurationClock.Pulse(value) : null;
    }

    /// <summary>Returns true for the first <paramref name="activeFor"/> fraction of every duration.</summary>
    public bool? On(Duration duration, float activeFor = 0.25f)
    {
        var phase = synced ? DurationClock.Phase(barProgress, duration) : null;
        return phase is { } value ? value < Mathf.Clamp01(activeFor) : null;
    }
}

/// <summary>Converts bar progress into tempo-based musical subdivision phases.</summary>
internal static class DurationClock
{
    /// <summary>Number of quarter-note beats in the common-time bar.</summary>
    private const float BeatsPerBar = 4f;

    /// <summary>Returns 0..1 position through the selected duration.</summary>
    internal static float? Phase(float barProgress, Duration duration)
    {
        var periodBeats = duration switch
        {
            Duration.Whole => BeatsPerBar,
            Duration.Half => 2f,
            Duration.Quarter => 1f,
            Duration.Eighth => 0.5f,
            Duration.Sixteenth => 0.25f,
            _ => (float?)null,
        };
        return periodBeats is { } period
            ? Mathf.Repeat(barProgress * BeatsPerBar / period, 1f)
            : null;
    }

    /// <summary>Shapes a duration phase into a smooth one-to-zero pulse.</summary>
    internal static float Pulse(float phase) =>
        1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
}

public partial class BeatManager
{
    /// <summary>Wire and tempo-derived pulses captured from the settled musical frame.</summary>
    public PulsesValues Pulses { get; private set; }

    /// <summary>Captures wire and derived pulse values for public reading.</summary>
    private PulsesValues CapturePulses()
    {
        return new PulsesValues(liveBeatActive, IsSynced, wireSnapshot.beatPulse, offBeatPulse, BarPhase);
    }
}
