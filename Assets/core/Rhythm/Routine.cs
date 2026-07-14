// Composes four one-bar Waveforms across the current timing Grid.

using System;
using UnityEngine;

/// <summary>
/// An immutable 16-beat choreography of four resolved Waveforms. It reads exactly like a
/// Waveform while selecting its current shape from the shared Grid position.
/// </summary>
public sealed class Routine
{
    /// <summary>One Routine is exactly four resolved one-bar Waveforms, one 16-beat Grid.</summary>
    internal const int SlotCount = 4;

    /// <summary>The four resolved Waveforms in Grid bar order.</summary>
    private readonly Waveform[] waveforms;

    /// <summary>The shared musical source that places this Routine on the Grid.</summary>
    private readonly BeatManager clockSource;

    /// <summary>Builds one immutable Routine from four privately owned resolved values.</summary>
    private Routine(Waveform[] waveforms, BeatManager clockSource)
    {
        this.waveforms = waveforms;
        this.clockSource = clockSource;
    }

    /// <summary>Current Routine envelope in <c>[0..1]</c>; rests at 0 without a placed Grid.</summary>
    public float Envelope => TrySampleCurrent(out var envelope) ? envelope : 0f;

    /// <summary>
    /// Maps the current Routine envelope between caller-chosen endpoints. Without a placed Grid,
    /// returns <paramref name="to"/> so Standalone rendering keeps the established steady response.
    /// </summary>
    /// <param name="from">Value at the current Waveform's trough.</param>
    /// <param name="to">Value at its peak and the Standalone fallback.</param>
    public float Lerp(float from, float to)
    {
        return TrySampleCurrent(out var envelope) ? envelope.Lerp(from, to) : to;
    }

    /// <summary>Composes exactly four runtime-bound one-bar Waveforms into one immutable value.</summary>
    /// <param name="bar1">The Waveform for Grid bar 1.</param>
    /// <param name="bar2">The Waveform for Grid bar 2.</param>
    /// <param name="bar3">The Waveform for Grid bar 3.</param>
    /// <param name="bar4">The Waveform for Grid bar 4.</param>
    /// <returns>A held Routine that reads the shared Grid position of its Waveforms.</returns>
    public static Routine Of(Waveform bar1, Waveform bar2, Waveform bar3, Waveform bar4)
    {
        var resolvedBars = new[] { bar1, bar2, bar3, bar4 };
        var source = resolvedBars[0].ClockSource
            ?? throw new InvalidOperationException("Routine Waveforms must be acquired from Waveforms.");

        for (var i = 0; i < resolvedBars.Length; i++)
        {
            var waveform = resolvedBars[i];
            if (waveform.IsDisabled)
            {
                throw new InvalidOperationException("waveforms.None cannot be placed inside a Routine.");
            }

            if (!ReferenceEquals(source, waveform.ClockSource))
            {
                throw new InvalidOperationException(
                    "Every Waveform in a Routine must use the same musical source.");
            }
        }

        return new Routine(resolvedBars, source);
    }

    /// <summary>Samples the Waveform selected by the current one-based Grid bar.</summary>
    /// <param name="envelope">The sampled envelope, or 0 when the Grid is unavailable.</param>
    /// <returns>True when a valid Grid position was available and sampled.</returns>
    private bool TrySampleCurrent(out float envelope)
    {
        if (clockSource.Grid.Bar is not { } bar
            || clockSource.Grid.Progress is not { } progress
            || bar < 1
            || bar > SlotCount)
        {
            envelope = 0f;
            return false;
        }

        var barFraction = Mathf.Repeat(progress * SlotCount, 1f);
        envelope = waveforms[bar - 1].Sample(barFraction);
        return true;
    }
}
