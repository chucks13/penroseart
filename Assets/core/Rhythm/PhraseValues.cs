// Captures current and explicitly named next Phrase values.
#nullable enable

/// <summary>Immutable current-phrase wire facts and derived position.</summary>
public readonly struct PhraseValues
{
    /// <summary>Continuous elapsed position retained for Build and Decay.</summary>
    private readonly float? elapsedBeats;

    /// <summary>Captures the current Phrase and its direct derived values.</summary>
    internal PhraseValues(string? name, int? beatsRemaining, int? lengthBeats, bool irregular,
        float? progress, float? elapsedBeats)
    {
        Name = name;
        BeatsRemaining = beatsRemaining;
        LengthBeats = lengthBeats;
        Irregular = irregular;
        Progress = progress;
        this.elapsedBeats = elapsedBeats;
    }

    /// <summary>The phrase name exactly as broadcast.</summary>
    public string? Name { get; }

    /// <summary>Beats remaining in the phrase, including the current beat.</summary>
    public int? BeatsRemaining { get; }

    /// <summary>Total phrase length in beats.</summary>
    public int? LengthBeats { get; }

    /// <summary>Whether the phrase length breaks the sender's sixteen-beat grid; false when unknown.</summary>
    public bool Irregular { get; }

    /// <summary>Derived 0..1 position through the phrase.</summary>
    public float? Progress { get; }

    /// <summary>Rises from zero to one across the full phrase or requested beat duration.</summary>
    public float Build(float? durationBeats = null) =>
        StockEnvelopes.Build(elapsedBeats, durationBeats ?? LengthBeats);

    /// <summary>Falls from one to zero across the full phrase or requested beat duration.</summary>
    public float Decay(float? durationBeats = null) =>
        StockEnvelopes.Decay(elapsedBeats, durationBeats ?? LengthBeats);
}

/// <summary>Immutable facts from the wire's explicitly named next-phrase lane.</summary>
public readonly struct NextPhraseValues
{
    /// <summary>Captures the explicitly named next Phrase lane.</summary>
    internal NextPhraseValues(string? name, int? beatsUntil, int? lengthBeats)
    {
        Name = name;
        BeatsUntil = beatsUntil;
        LengthBeats = lengthBeats;
    }

    /// <summary>The next phrase name exactly as broadcast.</summary>
    public string? Name { get; }

    /// <summary>Beats until the next phrase begins, including the current beat.</summary>
    public int? BeatsUntil { get; }

    /// <summary>Total length of the next phrase in beats.</summary>
    public int? LengthBeats { get; }
}

public partial class BeatManager
{
    /// <summary>The current phrase's wire facts and derived values.</summary>
    public PhraseValues Phrase { get; private set; }

    /// <summary>The wire's explicitly named next phrase.</summary>
    public NextPhraseValues NextPhrase { get; private set; }

    /// <summary>Captures current and explicitly named next Phrase lanes.</summary>
    private void CapturePhrases()
    {
        var state = wireSnapshot.phraseState;
        var name = string.IsNullOrEmpty(state.label) ? null : state.label;
        var elapsed = name != null ? ElapsedInDuration(state.countBeats, state.lengthBeats) : null;
        Phrase = new PhraseValues(
            name,
            name != null ? NonNegativeOrNull(state.countBeats) : null,
            name != null ? NonNegativeOrNull(state.lengthBeats) : null,
            name != null && TriStateTrue(state.irregular),
            ProgressOverLength(elapsed, state.lengthBeats),
            elapsed);

        var next = wireSnapshot.nextPhraseState;
        var nextName = string.IsNullOrEmpty(next.label) ? null : next.label;
        NextPhrase = new NextPhraseValues(
            nextName,
            nextName != null ? NonNegativeOrNull(next.countBeats) : null,
            nextName != null ? NonNegativeOrNull(next.lengthBeats) : null);
    }
}
