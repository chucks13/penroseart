using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Projects a DJ-domain moment into the BeatManager integer frame <see cref="PhaseLock"/>
/// consumes (<see cref="OnAirTimingInput"/>). Scripted-DJ-timeline scenarios are written as
/// data through these factories; the projection plumbing lives here once, so a new scenario
/// is a list of frames, not new test plumbing.
/// </summary>
public static class DjFrame
{
    /// <summary>
    /// Inside an active Phrase whose start beat and length are known, at the given beat.
    /// Derives the boundary countdown so the on-air <c>phraseStart = beat − (lengthBeats −
    /// beatsUntilNext)</c> resolves back to <paramref name="phraseStartBeat"/>.
    /// </summary>
    public static OnAirTimingInput InPhrase(
        int beat,
        int phraseStartBeat,
        int phraseLengthBeats,
        int beatInBar = 0,
        int trackOrdinal = 1)
    {
        return new OnAirTimingInput(
            beat,
            BeatInBarOnGrid(beat, phraseStartBeat, beatInBar),
            trackPhaseActive: 1,
            beatsUntilPhraseBoundary: phraseStartBeat + phraseLengthBeats - beat,
            phraseLengthBeats: phraseLengthBeats,
            trackOrdinal: trackOrdinal);
    }

    /// <summary>Counting down to an upcoming Phrase that has not started yet (lead-in / look-ahead).</summary>
    public static OnAirTimingInput BeforePhrase(
        int beat,
        int phraseStartBeat,
        int upcomingLengthBeats,
        int beatInBar = 0,
        int trackOrdinal = 1)
    {
        return new OnAirTimingInput(
            beat,
            BeatInBarOnGrid(beat, phraseStartBeat, beatInBar),
            trackPhaseActive: 0,
            beatsUntilPhraseBoundary: phraseStartBeat - beat,
            phraseLengthBeats: upcomingLengthBeats,
            trackOrdinal: trackOrdinal);
    }

    /// <summary>A beat clock with no Phrase data: the Phrase feed dropped out but the clock continues.</summary>
    public static OnAirTimingInput BeatOnly(int beat, int beatInBar = 0, int trackOrdinal = 1)
    {
        return new OnAirTimingInput(
            beat,
            BeatInBarOr(beat, beatInBar),
            trackPhaseActive: -1,
            beatsUntilPhraseBoundary: -1,
            phraseLengthBeats: -1,
            trackOrdinal: trackOrdinal);
    }

    /// <summary>No clock at all (every beat signal -1): the stand-alone floor.</summary>
    public static OnAirTimingInput NoClock() => OnAirTimingInput.Unavailable;

    /// <summary>The 1-based 4-count for an absolute beat (beat 1 → 1, beat 5 → 1) — the same grid math PhaseLock uses.</summary>
    public static int FourCount(int beat) => PhaseGrid.FourCount(beat);

    /// <summary>The explicit beat-in-bar when one is supplied (&gt; 0), else the 4-count derived from the beat.</summary>
    private static int BeatInBarOr(int beat, int beatInBar) => beatInBar > 0 ? beatInBar : FourCount(beat);

    /// <summary>
    /// The beat-in-bar consistent with the Phrase grid: a real Phrase start is a downbeat, so the tick
    /// is derived from the Phrase's own offset, not from assuming beat 1 is the downbeat. This keeps
    /// fixtures honest for Phrases whose grid is sub-bar-shifted from the running beat counter. An
    /// explicit override (&gt; 0) wins, so a fixture can deliberately break the 4-count.
    /// </summary>
    private static int BeatInBarOnGrid(int beat, int phraseStartBeat, int beatInBar) =>
        beatInBar > 0 ? beatInBar : PhaseGrid.BarPositionFor(beat, PhaseGrid.OffsetForPhraseStart(phraseStartBeat));
}

/// <summary>
/// Drives a single <see cref="PhaseLock"/> instance across a scripted DJ timeline and
/// surfaces the <see cref="PhaseReading"/> emitted for each frame, so a scenario asserts the
/// externally visible Phase contract frame by frame rather than reaching into PhaseLock state.
/// </summary>
public static class PhaseTimelineHarness
{
    public static IReadOnlyList<PhaseReading> Run(params OnAirTimingInput[] frames)
    {
        var phaseLock = new PhaseLock();
        return frames.Select(frame => phaseLock.Read(frame)).ToList();
    }
}
