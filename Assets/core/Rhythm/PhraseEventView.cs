// Presentation of the contrived phrase-event rhythm query (ADR-0002), co-located with the
// PhraseEventInfo data it formats so the inspector and any future telnet/OSC/debug readout share one
// display vocabulary.

#nullable enable

/// <summary>
/// Three-way display state of a <see cref="PhraseEventInfo"/>: playing <see cref="Now"/>, counting
/// down to a known start (<see cref="Soon"/>), or <see cref="Idle"/> (nothing upcoming). Callers map
/// the state onto their own presentation — e.g. a chip color in an editor drawer.
/// </summary>
public enum PhraseEventState
{
    Now,
    Soon,
    Idle,
}

/// <summary>
/// The display model of a <see cref="PhraseEventInfo"/> (Fill or Drop): the status chip label, the
/// meter fill in [0..1], the one-line readout, and the <see cref="PhraseEventState"/> classifying it.
/// Built once by <see cref="Of"/> so every surface — the BeatManager inspector and any future
/// telnet/OSC/debug readout — formats a phrase event identically.
/// </summary>
public readonly struct PhraseEventView
{
    /// <summary>Status chip text: "NOW" while playing, "IN {beats}" while counting down, "—" when idle.</summary>
    public readonly string Chip;

    /// <summary>Meter fill in [0..1]: progress while playing, anticipation while counting down, 0 when idle.</summary>
    public readonly float Meter;

    /// <summary>One-line readout of timing, length, and remaining count.</summary>
    public readonly string Readout;

    /// <summary>Now / Soon / Idle classification, for callers that color or branch on the state.</summary>
    public readonly PhraseEventState State;

    private PhraseEventView(string chip, float meter, string readout, PhraseEventState state)
    {
        Chip = chip;
        Meter = meter;
        Readout = readout;
        State = state;
    }

    /// <summary>Builds the display model for <paramref name="info"/>.</summary>
    public static PhraseEventView Of(PhraseEventInfo info)
    {
        var state = info.inProgress ? PhraseEventState.Now
            : info.beatsUntilStart != null ? PhraseEventState.Soon
            : PhraseEventState.Idle;

        return new PhraseEventView(BuildChip(info), BuildMeter(info), BuildReadout(info), state);
    }

    private static string BuildChip(PhraseEventInfo info)
    {
        if (info.inProgress)
        {
            return "NOW";
        }

        return info.beatsUntilStart is { } beats ? $"IN {beats}" : "—";
    }

    private static float BuildMeter(PhraseEventInfo info)
    {
        return info.inProgress ? info.progress ?? 0f : info.anticipation ?? 0f;
    }

    private static string BuildReadout(PhraseEventInfo info)
    {
        if (info.inProgress)
        {
            return $"ends in {RhythmText.Beats(info.beatsUntilEnd)} · len {RhythmText.Count(info.lengthBeats)} · ×{RhythmText.Count(info.remaining)}";
        }

        if (info.beatsUntilStart is { } beats)
        {
            var head = info.msUntilStart is { } ms ? $"in {ms / 1000f:0.0}s" : $"in {beats}b";
            return $"{head} · len {RhythmText.Count(info.lengthBeats)} · ×{RhythmText.Count(info.remaining)}";
        }

        return $"next — · ×{RhythmText.Count(info.remaining)}";
    }
}
