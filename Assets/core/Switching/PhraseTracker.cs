/// <summary>
/// Read-only Phrase reading <see cref="PhraseTracker"/> emits each frame. This is the PHRASE layer:
/// where the frame sits inside the live musical Phrase, how long that Phrase is, whether it is
/// irregular (its length is not a whole number of 16-beat Grids), and a one-Phrase look-ahead while
/// the feed is counting down to the next Phrase. Every value is a whole-beat integer, matching the
/// exact integer model of the GRID-layer <see cref="GridReading"/> it rides on.
/// </summary>
public readonly struct PhraseTrackerReading
{
    /// <summary>Sentinel "no Phrase resolved" reading, emitted whenever the underlying Grid is unacquired.</summary>
    public static PhraseTrackerReading None { get; } =
        new PhraseTrackerReading(-1, -1, false, -1, -1, false);

    /// <summary>1-based beat of this frame inside the current Phrase (1..length), or -1 when not in a Phrase.</summary>
    public readonly int PositionInPhrase;

    /// <summary>Length of the current Phrase in beats, or -1 when not in a Phrase.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>The current or upcoming Phrase does not subdivide into whole 16-beat Grids (length not a multiple of 16).</summary>
    public readonly bool IsIrregular;

    /// <summary>Whole beats until the next Phrase boundary (the next Phrase start, equivalently the current Phrase's end), or -1 when unavailable.</summary>
    public readonly int BeatsUntilNextPhrase;

    /// <summary>Length in beats of the Phrase being counted down to, or -1 when not counting down.</summary>
    public readonly int PredictedUpcomingLengthBeats;

    /// <summary>A look-ahead is available: the feed is counting down to a known upcoming Phrase.</summary>
    public bool HasLookAhead => PredictedUpcomingLengthBeats != -1;

    /// <summary>The underlying Grid is acquired, so this reading is meaningful (distinguishes a live no-Phrase frame from <see cref="None"/>).</summary>
    public readonly bool IsAcquired;

    public PhraseTrackerReading(
        int positionInPhrase,
        int phraseLengthBeats,
        bool isIrregular,
        int beatsUntilNext,
        int predictedUpcomingLengthBeats,
        bool isAcquired)
    {
        PositionInPhrase = positionInPhrase;
        PhraseLengthBeats = phraseLengthBeats;
        IsIrregular = isIrregular;
        BeatsUntilNextPhrase = beatsUntilNext;
        PredictedUpcomingLengthBeats = predictedUpcomingLengthBeats;
        IsAcquired = isAcquired;
    }
}

/// <summary>
/// Phrase-layer reader that rides on the GRID-layer <see cref="GridReading"/>. Given the Grid
/// reading plus the feed's three Phrase numerics (the tri-state Track-Phase-active flag, beats until
/// the next boundary, and the active-or-upcoming Phrase length), it places the frame inside the live
/// Phrase and projects one Phrase of look-ahead while the feed is counting down.
/// <para>
/// The Read is a stateless pure mapping exposed as a static method. It owns no
/// re-anchoring: where the 16-grid sits, and whether the Grid is trustworthy, belong to
/// <see cref="GridSync"/>; this reader rides on that <see cref="GridReading.State"/>.
/// </para>
/// <para>
/// <see cref="PhraseTrackerReading.IsIrregular"/> is derived phrase-locally from the length via
/// <see cref="Grid.IsIrregularPhrase"/> — the single definition of phrase irregularity — so
/// the grid and phrase layers agree on what counts as irregular.
/// </para>
/// </summary>
public sealed class PhraseTracker
{
    /// <summary>
    /// Reads one frame and emits the current Phrase reading. <paramref name="grid"/> is this frame's
    /// GRID reading; the three integers are the feed's Phrase numerics
    /// (<see cref="OnAirTimingInput.TrackPhaseActive"/>, <see cref="OnAirTimingInput.BeatsUntilPhraseBoundary"/>,
    /// <see cref="OnAirTimingInput.PhraseLengthBeats"/>).
    /// </summary>
    public static PhraseTrackerReading Read(in GridReading grid, int trackPhaseActive, int beatsUntilNext, int activeOrUpcomingLengthBeats)
    {
        // No acquired Grid to ride on: there is nothing to place inside a Phrase. StandAloneFloor is a
        // mode exit (the 4-count clock is gone, ADR-0004) — even with a held offset still on the reading
        // there is no live grid to ride, so the Phrase layer reports None too.
        if (grid.Offset < 0 || grid.StandAloneFloor)
        {
            return PhraseTrackerReading.None;
        }

        var inPhrase = trackPhaseActive == 1;
        var countdown = trackPhaseActive == 0;
        var hasLength = activeOrUpcomingLengthBeats >= 1;
        var length = activeOrUpcomingLengthBeats;

        var phraseLengthBeats = inPhrase && hasLength ? length : -1;

        // beats-into-phrase = length − beatsUntilNext; +1 makes the Phrase start read position 1. The
        // range is 1..length, so beatsUntilNext must be 1..length: at beatsUntilNext == 0 the boundary
        // has arrived (that beat belongs to the next Phrase), not position length+1.
        var positionInPhrase = inPhrase && hasLength && beatsUntilNext >= 1 && beatsUntilNext <= length
            ? length - beatsUntilNext + 1
            : -1;

        var predictedUpcomingLengthBeats = countdown && hasLength && beatsUntilNext > 0 ? length : -1;

        // Re-derived from the length so it catches the first Phrase; covers the current in-Phrase
        // length and the upcoming length during a countdown (both arrive in the same field).
        var isIrregular = hasLength && Grid.IsIrregularPhrase(length);

        return new PhraseTrackerReading(
            positionInPhrase,
            phraseLengthBeats,
            isIrregular,
            beatsUntilNext,
            predictedUpcomingLengthBeats,
            isAcquired: true);
    }
}
