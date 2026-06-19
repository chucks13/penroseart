using System.Collections.Generic;

/// <summary>
/// Musical phrase span derived from Track Phase, with 16-beat impact slots for Director scheduling.
/// </summary>
public readonly struct PhraseWindow
{
    /// <summary>Default length of one 16-Beat Phase / Phase Slot.</summary>
    public const int DefaultSlotBeats = 16;

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
    /// Enumerates future Phase Boundaries after the supplied beat, including the phrase boundary.
    /// </summary>
    public IEnumerable<int> ImpactSlotsAfter(int beat, int slotBeats = DefaultSlotBeats)
    {
        if (slotBeats <= 0)
        {
            yield break;
        }

        var slotBeat = FirstSlotBoundaryAfter(beat, slotBeats);
        for (; slotBeat <= EndBeat; slotBeat += slotBeats)
        {
            yield return slotBeat;
        }
    }

    private int FirstSlotBoundaryAfter(int beat, int slotBeats)
    {
        var firstBoundary = StartBeat + slotBeats;
        if (beat < firstBoundary)
        {
            return firstBoundary;
        }

        var beatsSinceFirstBoundary = beat - firstBoundary;
        var slotsToSkip = beatsSinceFirstBoundary / slotBeats + 1;
        return firstBoundary + (slotsToSkip * slotBeats);
    }
}
