using System.Collections.Generic;

/// <summary>
/// Musical phrase span derived from Track Phase, with Grid Boundaries for Director scheduling.
/// </summary>
public readonly struct PhraseWindow
{
    /// <summary>Default length of one Grid: four bars / sixteen beats.</summary>
    public const int DefaultGridBeats = 16;

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
    /// Builds an upcoming Phrase Window from a Track Phase countdown to its start and announced length.
    /// </summary>
    public static bool TryFromUpcomingTrackPhase(
        int beat,
        int beatsToPhraseStart,
        int phraseLengthBeats,
        out PhraseWindow window)
    {
        window = default;
        if (beat < 1 || beatsToPhraseStart < 0 || phraseLengthBeats <= 0)
        {
            return false;
        }

        var startBeat = beat + beatsToPhraseStart;
        return TryFromStartAndLength(startBeat, phraseLengthBeats, out window);
    }

    /// <summary>
    /// Builds a Phrase Window from its start beat and length.
    /// </summary>
    public static bool TryFromStartAndLength(int startBeat, int phraseLengthBeats, out PhraseWindow window)
    {
        window = default;
        if (startBeat < 1 || phraseLengthBeats <= 0)
        {
            return false;
        }

        window = new PhraseWindow(startBeat, startBeat + phraseLengthBeats, phraseLengthBeats);
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
    /// Enumerates future Grid Boundaries after the supplied beat, including the phrase boundary.
    /// </summary>
    public IEnumerable<int> GridBoundariesAfter(int beat, int gridBeats = DefaultGridBeats)
    {
        if (gridBeats <= 0)
        {
            yield break;
        }

        var boundaryBeat = FirstGridBoundaryAfter(beat, gridBeats);
        for (; boundaryBeat <= EndBeat; boundaryBeat += gridBeats)
        {
            yield return boundaryBeat;
        }
    }

    private int FirstGridBoundaryAfter(int beat, int gridBeats)
    {
        var firstBoundary = StartBeat + gridBeats;
        if (beat < firstBoundary)
        {
            return firstBoundary;
        }

        var beatsSinceFirstBoundary = beat - firstBoundary;
        var boundariesToSkip = beatsSinceFirstBoundary / gridBeats + 1;
        return firstBoundary + (boundariesToSkip * gridBeats);
    }
}
