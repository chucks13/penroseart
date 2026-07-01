using System;
using System.Collections.Generic;

/// <summary>
/// Phrase-relative plan of Cue Marks for one Phrase length.
/// </summary>
public readonly struct CueSheet
{
    private const int MaximumCueMarkGapBeats = 64;

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
    /// Randomly selects eligible interior Cue Marks, fills generated gaps longer than 64 beats,
    /// and always includes the mandatory final Cue Mark. A sheet built before its Phrase starts
    /// also includes the start beat — the Track Phase boundary — as a Cue Mark when cadence allows.
    /// </summary>
    public static CueSheet Build(
        PhraseWindow window,
        int currentBeat,
        Func<int, bool> canChangeAtBeat,
        Func<int, int, int> randomRange)
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
        if (window.StartBeat > currentBeat && canChangeAtBeat(window.StartBeat))
        {
            cueMarkOffsets.Add(0);
        }

        var eligibleInteriorCueMarkOffsets = new List<int>();
        foreach (var candidateCueMarkBeat in window.GridBoundariesAfter(currentBeat))
        {
            var candidateCueMarkOffset = candidateCueMarkBeat - window.StartBeat;
            if (candidateCueMarkBeat < window.EndBeat && canChangeAtBeat(candidateCueMarkBeat))
            {
                eligibleInteriorCueMarkOffsets.Add(candidateCueMarkOffset);
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
        FillLongCueMarkGaps(cueMarkOffsets, window.StartBeat, currentBeat, canChangeAtBeat);
        cueMarkOffsets.Sort();
        return new CueSheet(window.LengthBeats, cueMarkOffsets.ToArray());
    }

    private static void FillLongCueMarkGaps(
        List<int> cueMarkOffsets,
        int phraseStartBeat,
        int currentBeat,
        Func<int, bool> canChangeAtBeat)
    {
        cueMarkOffsets.Sort();
        var filledCueMarkOffsets = new List<int>();
        var previousCueMarkOffset = 0;

        foreach (var cueMarkOffset in cueMarkOffsets)
        {
            while (cueMarkOffset - previousCueMarkOffset > MaximumCueMarkGapBeats)
            {
                var requiredCueMarkOffset = previousCueMarkOffset + MaximumCueMarkGapBeats;
                var requiredCueMarkBeat = phraseStartBeat + requiredCueMarkOffset;
                if (requiredCueMarkBeat > currentBeat && canChangeAtBeat(requiredCueMarkBeat))
                {
                    filledCueMarkOffsets.Add(requiredCueMarkOffset);
                }

                previousCueMarkOffset = requiredCueMarkOffset;
            }

            filledCueMarkOffsets.Add(cueMarkOffset);
            previousCueMarkOffset = cueMarkOffset;
        }

        cueMarkOffsets.Clear();
        cueMarkOffsets.AddRange(filledCueMarkOffsets);
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
