// Routine values: four resolved one-bar Waveforms composing exactly one Grid.

#nullable enable

/// <summary>
/// A 16-beat choreography of exactly four resolved one-bar Waveforms. The value stores no
/// acquisition settings or lifecycle policy; callers compose another value explicitly when they
/// choose. The Waveforms surface only evaluates this value against the hub's captured Grid.
/// </summary>
public sealed class Routine
{
    /// <summary>One Routine is exactly four resolved one-bar Waveforms, one 16-beat Grid.</summary>
    internal const int SlotCount = 4;

    /// <summary>The four resolved Waveforms in Grid bar order.</summary>
    private readonly Waveform[] waveforms;

    /// <summary>Builds one immutable Routine from four privately owned resolved values.</summary>
    private Routine(Waveform[] waveforms)
    {
        this.waveforms = waveforms;
    }

    /// <summary>Composes exactly four already-resolved one-bar Waveforms into one immutable value.</summary>
    /// <param name="bar1">The Waveform for Grid bar 1.</param>
    /// <param name="bar2">The Waveform for Grid bar 2.</param>
    /// <param name="bar3">The Waveform for Grid bar 3.</param>
    /// <param name="bar4">The Waveform for Grid bar 4.</param>
    public static Routine Of(Waveform bar1, Waveform bar2, Waveform bar3, Waveform bar4)
    {
        return new Routine(new[] { bar1, bar2, bar3, bar4 });
    }

    /// <summary>Returns one zero-based Grid bar's resolved Waveform.</summary>
    internal Waveform WaveformAt(int index)
    {
        return waveforms[index];
    }
}
