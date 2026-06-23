using System;
using System.Collections.Generic;

/// <summary>
/// Phrase-relative plan of Cue Marks for one Phrase length.
/// </summary>
public readonly struct CueSheet
{
    /// <summary>Total length of the Phrase this sheet can be reused for, in beats.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Phrase-relative Cue Mark offsets where the Director may target transition Impact Points.</summary>
    public readonly int[] CueMarkOffsets;

    private CueSheet(int phraseLengthBeats, int[] cueMarkOffsets)
    {
        PhraseLengthBeats = phraseLengthBeats;
        CueMarkOffsets = cueMarkOffsets;
    }

    /// <summary>
    /// Randomly selects eligible interior Cue Marks and always includes the mandatory final Cue Mark.
    /// </summary>
    public static CueSheet Build(
        PhraseWindow window,
        int currentBeat,
        Func<int, bool> canChangeAtBeat,
        Func<int, int, int> randomRange,
        bool includePhraseStart = false)
    {
        if (canChangeAtBeat == null)
        {
            throw new ArgumentNullException(nameof(canChangeAtBeat));
        }

        if (randomRange == null)
        {
            throw new ArgumentNullException(nameof(randomRange));
        }

        var cueMarkOffsets = new List<int>();
        if (includePhraseStart && window.StartBeat > currentBeat && canChangeAtBeat(window.StartBeat))
        {
            cueMarkOffsets.Add(0);
        }

        var eligibleInteriorCueMarkOffsets = new List<int>();
        foreach (var phaseBoundary in window.PhaseBoundariesAfter(currentBeat))
        {
            if (phaseBoundary < window.EndBeat && canChangeAtBeat(phaseBoundary))
            {
                eligibleInteriorCueMarkOffsets.Add(phaseBoundary - window.StartBeat);
            }
        }

        var interiorCueMarkCount = eligibleInteriorCueMarkOffsets.Count > 0
            ? randomRange(0, eligibleInteriorCueMarkOffsets.Count + 1)
            : 0;
        for (var i = 0; i < interiorCueMarkCount; i++)
        {
            var chosenIndex = randomRange(0, eligibleInteriorCueMarkOffsets.Count);
            cueMarkOffsets.Add(eligibleInteriorCueMarkOffsets[chosenIndex]);
            eligibleInteriorCueMarkOffsets.RemoveAt(chosenIndex);
        }

        cueMarkOffsets.Add(window.LengthBeats);
        cueMarkOffsets.Sort();
        return new CueSheet(window.LengthBeats, cueMarkOffsets.ToArray());
    }

    /// <summary>
    /// Returns whether this sheet can be reused for the supplied Phrase Window's length.
    /// </summary>
    public bool Matches(PhraseWindow window)
    {
        return PhraseLengthBeats == window.LengthBeats;
    }

    /// <summary>Translates a Phrase-relative Cue Mark offset to its current absolute on-air beat.</summary>
    public int ToAbsoluteBeat(int phraseStartBeat, int cueMarkOffset)
    {
        return phraseStartBeat + cueMarkOffset;
    }
}
