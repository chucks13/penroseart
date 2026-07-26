// Captures Drop wire facts and their direct musical interpretations.
#nullable enable

/// <summary>Private translation of the shared drop/fill countdown wire shape.</summary>
internal readonly struct CountdownValues
{
    /// <summary>Creates the shared direct and interpreted countdown values.</summary>
    internal CountdownValues(bool active, int? lengthBeats, int? remaining,
        int? beatsRemaining, int? beatsUntil, float? progress, float? elapsedBeats)
    {
        Active = active;
        LengthBeats = lengthBeats;
        Remaining = remaining;
        BeatsRemaining = beatsRemaining;
        BeatsUntil = beatsUntil;
        Progress = progress;
        ElapsedBeats = elapsedBeats;
    }

    /// <summary>Whether the event is active now; false when upcoming or unavailable.</summary>
    internal bool Active { get; }
    /// <summary>The current or upcoming event length.</summary>
    internal int? LengthBeats { get; }
    /// <summary>Occurrences whose marker has not passed.</summary>
    internal int? Remaining { get; }
    /// <summary>Beats remaining when active.</summary>
    internal int? BeatsRemaining { get; }
    /// <summary>Beats until the event when inactive.</summary>
    internal int? BeatsUntil { get; }
    /// <summary>Position through the active event.</summary>
    internal float? Progress { get; }
    /// <summary>Continuous beats elapsed through the active event.</summary>
    internal float? ElapsedBeats { get; }
}

/// <summary>Immutable drop wire facts with readable current/upcoming interpretations.</summary>
public readonly struct DropValues
{
    /// <summary>Creates a Drop reading from the shared countdown translation.</summary>
    internal DropValues(CountdownValues values)
    {
        Active = values.Active;
        LengthBeats = values.LengthBeats;
        Remaining = values.Remaining;
        BeatsRemaining = values.BeatsRemaining;
        BeatsUntil = values.BeatsUntil;
        Progress = values.Progress;
        Before = new BeforeSpan(values.BeatsUntil);
        In = new InSpan(values.ElapsedBeats, values.LengthBeats);
    }

    /// <summary>
    /// Whether the drop is active now. False covers both "upcoming" and "no drop data";
    /// <see cref="BeatsUntil"/> is non-null only while a real drop is upcoming.
    /// </summary>
    public bool Active { get; }

    /// <summary>The current or upcoming drop length from the wire.</summary>
    public int? LengthBeats { get; }

    /// <summary>Drop occurrences whose marker has not yet passed.</summary>
    public int? Remaining { get; }

    /// <summary>Beats remaining in the active drop; null while inactive.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>Beats until the upcoming drop; null while active.</summary>
    public int? BeatsUntil { get; }

    /// <summary>Derived 0..1 position through the active drop.</summary>
    public float? Progress { get; }

    /// <summary>Envelopes approaching the upcoming drop across a caller-named window of whole beats.</summary>
    public BeforeSpan Before { get; }

    /// <summary>Envelopes running through the active drop.</summary>
    public InSpan In { get; }
}

public partial class BeatManager
{
    /// <summary>The drop wire lane and its direct derived values.</summary>
    public DropValues Drop { get; private set; }

    /// <summary>Captures the settled Drop lane.</summary>
    private DropValues CaptureDrop() => new(CaptureCountdown(wireSnapshot.dropState));

    /// <summary>Translates the shared Drop/Fill wire shape once for either public group.</summary>
    private CountdownValues CaptureCountdown(PenroseArt.RaveOsc.CountdownState state)
    {
        var lane = TriStateOrNull(state.active);
        if (lane is null)
        {
            return default;
        }

        var active = lane.Value;
        var elapsed = active ? ElapsedInDuration(state.countBeats, state.lengthBeats) : null;
        return new CountdownValues(
            active,
            NonNegativeOrNull(state.lengthBeats),
            NonNegativeOrNull(state.remaining),
            active ? NonNegativeOrNull(state.countBeats) : null,
            active ? null : NonNegativeOrNull(state.countBeats),
            ProgressOverLength(elapsed, state.lengthBeats),
            elapsed);
    }
}
