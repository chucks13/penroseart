using System;
using System.Collections.Generic;

/// <summary>
/// Phrase-relative plan of Cue Marks for one Phrase length.
/// </summary>
public readonly struct CueSheet
{
    private const int MaximumCueMarkGapBeats = 64;
    private const int MaximumGapGrids = MaximumCueMarkGapBeats / PhraseWindow.DefaultGridBeats;

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

    /// <summary>
    /// Builds a Cue Sheet as a pure function of one Phrase announcement: its beat length, its offset
    /// (absolute start position), and a creative seed. This is the ADR-0011 canonical builder — an index
    /// of empty Cue Marks over the Phrase, with no Effect or Transition choice and no notion of "now".
    ///
    /// The constraints hold by construction: every mark sits on a Grid Boundary (a multiple of
    /// <see cref="PhraseWindow.DefaultGridBeats"/>), consecutive gaps — including the run-in from the
    /// Phrase start to the first mark — are at least one Grid and at most four Grids (16 to 64 beats),
    /// and the Phrase end always carries the final mark. The change cadence is therefore this
    /// construction rule alone; nothing downstream re-checks it.
    ///
    /// Layout within those bounds is a random roll keyed to (<paramref name="phraseLengthBeats"/>,
    /// <paramref name="phraseOffsetBeats"/>, <paramref name="seed"/>): the same announcement always rolls
    /// the identical sheet, so an announcement-keyed rebuild elsewhere can never re-roll it by accident,
    /// and only a changed announcement produces a different layout. Energy-weighted Cue Mark density is a
    /// named future knob and is deliberately not implemented here.
    /// </summary>
    /// <param name="phraseLengthBeats">Announced Phrase length in beats; must be a positive multiple of one Grid.</param>
    /// <param name="phraseOffsetBeats">The Phrase's offset (its absolute start beat); a roll dimension only, never geometry.</param>
    /// <param name="seed">Creative seed selecting one layout within the constraints.</param>
    /// <exception cref="ArgumentOutOfRangeException">The Phrase length is not a positive multiple of one Grid.</exception>
    public static CueSheet Build(int phraseLengthBeats, int phraseOffsetBeats, int seed)
    {
        if (phraseLengthBeats <= 0 || phraseLengthBeats % PhraseWindow.DefaultGridBeats != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phraseLengthBeats),
                phraseLengthBeats,
                "Phrase length must be a positive multiple of one Grid (16 beats).");
        }

        // A monotonic roll from the Phrase start: each gap is one to four Grids, so both the minimum
        // and maximum cadence hold by construction and the walk lands exactly on the Phrase end.
        var rollState = SeedRoll(phraseLengthBeats, phraseOffsetBeats, seed);
        var cueMarkOffsets = new List<int>();
        var markOffset = 0;
        while (markOffset < phraseLengthBeats)
        {
            var gridsRemaining = (phraseLengthBeats - markOffset) / PhraseWindow.DefaultGridBeats;
            var maxGapGrids = gridsRemaining < MaximumGapGrids ? gridsRemaining : MaximumGapGrids;
            var gapGrids = 1 + (int)(NextRoll(ref rollState) % (uint)maxGapGrids);
            markOffset += gapGrids * PhraseWindow.DefaultGridBeats;
            cueMarkOffsets.Add(markOffset);
        }

        return new CueSheet(phraseLengthBeats, cueMarkOffsets.ToArray());
    }

    /// <summary>Folds the three announcement dimensions into a non-zero deterministic roll state (FNV-1a).</summary>
    private static uint SeedRoll(int phraseLengthBeats, int phraseOffsetBeats, int seed)
    {
        unchecked
        {
            var state = 2166136261u;
            state = (state ^ (uint)phraseLengthBeats) * 16777619u;
            state = (state ^ (uint)phraseOffsetBeats) * 16777619u;
            state = (state ^ (uint)seed) * 16777619u;
            return state == 0u ? 0x9E3779B9u : state;
        }
    }

    /// <summary>Advances the deterministic roll state (xorshift32) and returns the next value.</summary>
    private static uint NextRoll(ref uint state)
    {
        unchecked
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
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

    /// <summary>Translates a Phrase-relative Cue Mark offset to its current absolute on-air beat.</summary>
    public int ToAbsoluteBeat(int phraseStartBeat, int cueMarkOffset)
    {
        return phraseStartBeat + cueMarkOffset;
    }
}
