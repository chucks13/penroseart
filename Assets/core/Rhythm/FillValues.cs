// Captures Fill wire facts and their direct musical interpretations.
#nullable enable

/// <summary>Immutable fill wire facts with readable current/upcoming interpretations.</summary>
public readonly struct FillValues
{
    /// <summary>Continuous elapsed position retained for Build and Decay.</summary>
    private readonly float? elapsedBeats;

    /// <summary>Creates a Fill reading from the shared countdown translation.</summary>
    internal FillValues(CountdownValues values)
    {
        Active = values.Active;
        LengthBeats = values.LengthBeats;
        Remaining = values.Remaining;
        BeatsRemaining = values.BeatsRemaining;
        BeatsUntil = values.BeatsUntil;
        Progress = values.Progress;
        elapsedBeats = values.ElapsedBeats;
    }

    /// <summary>
    /// Whether the fill is active now. False covers both "upcoming" and "no fill data";
    /// <see cref="BeatsUntil"/> is non-null only while a real fill is upcoming.
    /// </summary>
    public bool Active { get; }

    /// <summary>The current or upcoming fill length from the wire.</summary>
    public int? LengthBeats { get; }

    /// <summary>Fills remaining on the selected player's track.</summary>
    public int? Remaining { get; }

    /// <summary>Beats remaining in the active fill; null while inactive.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>Beats until the upcoming fill; null while active.</summary>
    public int? BeatsUntil { get; }

    /// <summary>Derived 0..1 position through the active fill.</summary>
    public float? Progress { get; }

    /// <summary>Rises across the full active fill or requested beat duration.</summary>
    public float Build(float? durationBeats = null) =>
        StockEnvelopes.Build(elapsedBeats, durationBeats ?? LengthBeats);

    /// <summary>Falls across the full active fill or requested beat duration.</summary>
    public float Decay(float? durationBeats = null) =>
        StockEnvelopes.Decay(elapsedBeats, durationBeats ?? LengthBeats);
}

public partial class BeatManager
{
    /// <summary>The fill wire lane and its direct derived values.</summary>
    public FillValues Fill { get; private set; }

    /// <summary>Captures the settled Fill lane.</summary>
    private FillValues CaptureFill() => new(CaptureCountdown(wireSnapshot.fillState));
}
