using System.Collections.Generic;

/// <summary>
/// Musical phrase span derived from Track Phase, with Phase Boundaries for Director scheduling.
/// </summary>
public readonly struct PhraseWindow
{
    /// <summary>Default length of one Phase: four bars / sixteen beats.</summary>
    public const int DefaultPhaseBeats = 16;

    /// <summary>Absolute beat where the Phrase Window starts.</summary>
    public readonly int StartBeat;

    /// <summary>Absolute beat where the Phrase Window ends.</summary>
    public readonly int EndBeat;

    /// <summary>Total length of the Phrase Window in beats.</summary>
    public readonly int LengthBeats;

    private PhraseWindow(int startBeat, int endBeat, int lengthBeats)
    {
        StartBeat = startBeat;
        EndBeat = endBeat;
        LengthBeats = lengthBeats;
    }

    /// <summary>
    /// Builds a Phrase Window from the current Track Phase countdown and length.
    /// </summary>
    public static bool TryFromTrackPhase(
        int beat,
        int beatsToPhraseBoundary,
        int phraseLengthBeats,
        out PhraseWindow window)
    {
        window = default;
        if (beat < 1 || beatsToPhraseBoundary <= 0 || phraseLengthBeats <= 0)
        {
            return false;
        }

        var endBeat = beat + beatsToPhraseBoundary;
        var startBeat = endBeat - phraseLengthBeats;
        if (startBeat < 1 || startBeat > beat)
        {
            return false;
        }

        window = new PhraseWindow(startBeat, endBeat, phraseLengthBeats);
        return true;
    }

    /// <summary>
    /// Returns whether two Phrase Windows have the same scheduling identity.
    /// </summary>
    public bool HasSameTimingIdentity(PhraseWindow other)
    {
        return StartBeat == other.StartBeat
            && EndBeat == other.EndBeat
            && LengthBeats == other.LengthBeats;
    }

    /// <summary>
    /// Enumerates future Phase Boundaries after the supplied beat, including the phrase boundary.
    /// </summary>
    public IEnumerable<int> PhaseBoundariesAfter(int beat, int phaseBeats = DefaultPhaseBeats)
    {
        if (phaseBeats <= 0)
        {
            yield break;
        }

        var boundaryBeat = FirstPhaseBoundaryAfter(beat, phaseBeats);
        for (; boundaryBeat <= EndBeat; boundaryBeat += phaseBeats)
        {
            yield return boundaryBeat;
        }
    }

    private int FirstPhaseBoundaryAfter(int beat, int phaseBeats)
    {
        var firstBoundary = StartBeat + phaseBeats;
        if (beat < firstBoundary)
        {
            return firstBoundary;
        }

        var beatsSinceFirstBoundary = beat - firstBoundary;
        var boundariesToSkip = beatsSinceFirstBoundary / phaseBeats + 1;
        return firstBoundary + (boundariesToSkip * phaseBeats);
    }
}
