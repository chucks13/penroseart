// Editor-side presentation model for the canonical Fill and Drop doorways. It lives beside its only
// consumer — the BeatManager dashboard — and never feeds state back into the runtime.

#nullable enable

/// <summary>
/// Three-way display state of a Fill or Drop: playing <see cref="Now"/>, counting
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
/// The display model of a Fill or Drop doorway: the status chip label, the
/// meter fill in [0..1], the one-line readout, and the <see cref="PhraseEventState"/> classifying it.
/// Built once by <see cref="Of"/> so the BeatManager inspector formats both doorway shapes identically.
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

    /// <summary>Builds the display model for the canonical Fill doorway.</summary>
    public static PhraseEventView Of(FillView fill)
    {
        var current = fill.Span.Current;
        return Of(
            current.HasValue,
            fill.NextInBeats,
            fill.NextLengthBeats,
            fill.RemainingOnTrack,
            current?.BeatsRemaining,
            current?.LengthBeats,
            fill.Span.Progress);
    }

    /// <summary>Builds the display model for the canonical Drop doorway.</summary>
    public static PhraseEventView Of(DropView drop)
    {
        var current = drop.Span.Current;
        return Of(
            current.HasValue,
            drop.NextInBeats,
            drop.NextLengthBeats,
            drop.RemainingOnTrack,
            current?.BeatsRemaining,
            current?.LengthBeats,
            drop.Span.Progress);
    }

    /// <summary>Formats either doorway through their shared countdown-span display shape.</summary>
    private static PhraseEventView Of(bool running, int? nextInBeats, int? nextLengthBeats,
        int? remainingOnTrack, int? beatsRemaining, int? runningLengthBeats, float? progress)
    {
        if (running)
        {
            return new PhraseEventView(
                "NOW",
                progress ?? 0f,
                $"ends in {RhythmText.Beats(beatsRemaining)} · len {RhythmText.Count(runningLengthBeats)} · ×{RhythmText.Count(remainingOnTrack)}",
                PhraseEventState.Now);
        }

        if (nextInBeats is { } beats)
        {
            return new PhraseEventView(
                $"IN {beats}",
                1f - UnityEngine.Mathf.Clamp01(beats / 32f),
                $"in {beats}b · len {RhythmText.Count(nextLengthBeats)} · ×{RhythmText.Count(remainingOnTrack)}",
                PhraseEventState.Soon);
        }

        return new PhraseEventView(
            "—",
            0f,
            $"next — · ×{RhythmText.Count(remainingOnTrack)}",
            PhraseEventState.Idle);
    }
}
