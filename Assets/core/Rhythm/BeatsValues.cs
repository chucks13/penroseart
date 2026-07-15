// Captures the four On Beat wire lanes as immutable per-frame values.

#nullable enable

using System;

/// <summary>Immutable per-count On Beat wire values captured for one BeatManager update.</summary>
public readonly struct BeatsValues
{
    /// <summary>The captured four-lane storage.</summary>
    private readonly BeatCountLanes lanes;

    /// <summary>Creates one public On Beat reading from captured lane storage.</summary>
    internal BeatsValues(BeatCountLanes lanes)
    {
        this.lanes = lanes;
    }

    /// <summary>Milliseconds until musical count 1..4 next lands.</summary>
    public int? OnBeatMs(int count) => lanes.Milliseconds(count);

    /// <summary>Whether musical count 1..4 is inside its quarter-beat wire trigger window; false when unavailable or out of range.</summary>
    public bool OnBeat(int count) => lanes.Active(count);
}

/// <summary>Immutable storage shared by the wire On Beat lanes and derived Offbeat lanes.</summary>
internal readonly struct BeatCountLanes
{
    /// <summary>Captured milliseconds-until values in count order.</summary>
    private readonly int[]? milliseconds;

    /// <summary>Captured active-window values in count order.</summary>
    private readonly bool[]? active;

    /// <summary>Captures four aligned countdown and active-window lanes.</summary>
    internal BeatCountLanes(int[] milliseconds, bool[] active)
    {
        this.milliseconds = milliseconds;
        this.active = active;
    }

    /// <summary>Reads milliseconds until one musical count, using one-based addressing.</summary>
    internal int? Milliseconds(int count)
    {
        var slot = count - 1;
        return milliseconds != null && slot >= 0 && slot < milliseconds.Length && milliseconds[slot] >= 0
            ? milliseconds[slot]
            : null;
    }

    /// <summary>Reads one musical count's active window, using one-based addressing; false when unavailable or out of range.</summary>
    internal bool Active(int count)
    {
        var slot = count - 1;
        return Milliseconds(count) != null && active != null && slot < active.Length && active[slot];
    }
}

public partial class BeatManager
{
    /// <summary>The four On Beat countdowns and trigger windows received from the wire.</summary>
    public BeatsValues Beats { get; private set; }

    /// <summary>Captures owned copies of the mutable OSC lane arrays.</summary>
    private BeatsValues CaptureBeats()
    {
        var snapshot = wireSnapshot;
        return new BeatsValues(new BeatCountLanes(
            CopyCountdownLanes(snapshot.beatsCountMs),
            CopyActiveLanes(snapshot.onBeats)));
    }

    /// <summary>Copies countdown values into a complete four-slot owned array.</summary>
    private static int[] CopyCountdownLanes(int[]? source)
    {
        var copy = CreateUnavailableCountdowns();
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    /// <summary>Copies active-window values into a complete four-slot owned array.</summary>
    private static bool[] CopyActiveLanes(bool[]? source)
    {
        var copy = new bool[BeatSlotCount];
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }
}
