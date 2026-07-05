using System;
using System.Collections.Generic;

/// <summary>
/// Phrase-relative plan of Cue Marks for one Phrase length. A Cue Sheet is a pure index of empty
/// Cue Marks over the Phrase — no Effect or Transition choice and no notion of "now" — built once
/// from a Phrase announcement and reused until that announcement changes.
/// </summary>
public readonly struct CueSheet
{
    /// <summary>
    /// Beats in one Grid — the 16-beat cycle Cue Marks land on. The change cadence lives entirely in this
    /// sheet geometry: consecutive gaps are whole Grid multiples, so every mark sits on a Grid Boundary.
    /// </summary>
    public const int GridBeats = 16;

    private const int MaximumCueMarkGapBeats = 64;
    private const int MaximumGapGrids = MaximumCueMarkGapBeats / GridBeats;

    /// <summary>Total length of the Phrase this sheet can be reused for, in beats.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Phrase-relative offsets of the Cue Marks the Director may target with a cast Cue.</summary>
    public readonly int[] CueMarkOffsets;

    private CueSheet(int phraseLengthBeats, int[] cueMarkOffsets)
    {
        PhraseLengthBeats = phraseLengthBeats;
        CueMarkOffsets = cueMarkOffsets;
    }

    /// <summary>
    /// Builds a Cue Sheet as a pure function of one Phrase announcement: its beat length, its offset
    /// (absolute start position), and a creative seed. This is the ADR-0011 canonical builder — an index
    /// of empty Cue Marks over the Phrase, with no Effect or Transition choice and no notion of "now".
    ///
    /// The constraints hold by construction: every mark sits on a Grid Boundary (a multiple of
    /// <see cref="GridBeats"/>), consecutive gaps — including the run-in from the Phrase start to the first
    /// mark — are at least one Grid and at most four Grids (16 to 64 beats), and the Phrase end always
    /// carries the final mark. The change cadence is therefore this construction rule alone; nothing
    /// downstream re-checks it.
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
        if (phraseLengthBeats <= 0 || phraseLengthBeats % GridBeats != 0)
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
            var gridsRemaining = (phraseLengthBeats - markOffset) / GridBeats;
            var maxGapGrids = gridsRemaining < MaximumGapGrids ? gridsRemaining : MaximumGapGrids;
            var gapGrids = 1 + (int)(NextRoll(ref rollState) % (uint)maxGapGrids);
            markOffset += gapGrids * GridBeats;
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

    /// <summary>Translates a Phrase-relative Cue Mark offset to its current absolute on-air beat.</summary>
    public int ToAbsoluteBeat(int phraseStartBeat, int cueMarkOffset)
    {
        return phraseStartBeat + cueMarkOffset;
    }
}
