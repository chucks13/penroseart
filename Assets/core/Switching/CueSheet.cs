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
    /// Builds a Cue Sheet as a pure function of one Phrase announcement: its beat length and a creative
    /// seed. This is the ADR-0011 canonical builder — an index of empty Cue Marks over the Phrase, with
    /// no Effect or Transition choice and no notion of "now".
    ///
    /// The constraints hold by construction: every mark sits on a Grid Boundary (a multiple of
    /// <see cref="GridBeats"/>), consecutive gaps — including the run-in from the Phrase start to the first
    /// mark — are at least one Grid and at most four Grids (16 to 64 beats), and the Phrase end always
    /// carries the final mark. The change cadence is therefore this construction rule alone; nothing
    /// downstream re-checks it.
    ///
    /// Layout within those bounds is a random roll keyed to (<paramref name="phraseLengthBeats"/>,
    /// <paramref name="seed"/>): the same announcement always rolls the identical sheet, so an
    /// announcement-keyed rebuild elsewhere can never re-roll it by accident, and only a changed
    /// announcement produces a different layout. Energy-weighted Cue Mark density is a named future knob
    /// and is deliberately not implemented here.
    /// </summary>
    /// <param name="phraseLengthBeats">Announced Phrase length in beats; must be a positive multiple of one Grid.</param>
    /// <param name="seed">Creative seed selecting one layout within the constraints.</param>
    /// <exception cref="ArgumentOutOfRangeException">The Phrase length is not a positive multiple of one Grid.</exception>
    /// <summary>
    /// Builds a Cue Sheet as a pure function of one Phrase announcement: its beat length and a creative
    /// seed. This is the ADR-0011 canonical builder — an index of empty Cue Marks over the Phrase, with
    /// no Effect or Transition choice and no notion of "now".
    ///
    /// The constraints hold by construction: every interior mark sits on a Grid Boundary (a multiple of
    /// <see cref="GridBeats"/>), consecutive gaps — including the run-in from the Phrase start to the first
    /// mark — are at least one Grid and at most four Grids (16 to 64 beats), and the Phrase end always
    /// carries the final mark. That end mark is itself a Grid Boundary: the wire re-anchors the timing grid
    /// at every Phrase boundary, so a Phrase end is always the next Phrase's downbeat even when the Phrase's
    /// own length is not a Grid multiple. The change cadence is therefore this construction rule alone;
    /// nothing downstream re-checks it.
    ///
    /// Irregular lengths (length % <see cref="GridBeats"/> != 0) are first-class: the interior marks stay on
    /// Grid Boundaries and a single random run-out Grid absorbs the odd tail, so the final gap stays in
    /// (16, 64]. The only min-gap exception is a Phrase shorter than one Grid, which carries just its
    /// mandatory end mark (an unavoidably short run-in).
    ///
    /// Layout within those bounds is a random roll keyed to (<paramref name="phraseLengthBeats"/>,
    /// <paramref name="seed"/>): the same announcement always rolls the identical sheet, so an
    /// announcement-keyed rebuild elsewhere can never re-roll it by accident, and only a changed
    /// announcement produces a different layout. For Grid-multiple lengths the roll stream is unchanged from
    /// the pre-irregular builder. Energy-weighted Cue Mark density is a named future knob and is deliberately
    /// not implemented here.
    /// </summary>
    /// <param name="phraseLengthBeats">Announced Phrase length in beats; must be positive. Any positive
    /// length is accepted — Grid-multiple and irregular alike.</param>
    /// <param name="seed">Creative seed selecting one layout within the constraints.</param>
    /// <exception cref="ArgumentOutOfRangeException">The Phrase length is not positive.</exception>
    public static CueSheet Build(int phraseLengthBeats, int seed)
    {
        if (phraseLengthBeats <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phraseLengthBeats),
                phraseLengthBeats,
                "Phrase length must be positive.");
        }

        var rollState = SeedRoll(phraseLengthBeats, seed);
        var cueMarkOffsets = new List<int>();

        // The interior walk always runs over a whole number of Grids. For a Grid-multiple length that is the
        // full Phrase and the roll stream is byte-identical to the pre-irregular builder. For an irregular
        // length, one random run-out Grid is peeled off first to carry the odd tail, and the walk covers the
        // rest; the mandatory end mark then closes the Phrase.
        var tailBeats = phraseLengthBeats % GridBeats;
        var walkBeats = phraseLengthBeats;
        if (tailBeats != 0)
        {
            var interiorBeats = phraseLengthBeats - tailBeats;
            var gridsAvailable = interiorBeats / GridBeats;
            if (gridsAvailable == 0)
            {
                // Shorter than one Grid: only the mandatory end mark, with an unavoidably short run-in.
                return new CueSheet(phraseLengthBeats, new[] { phraseLengthBeats });
            }

            // Roll a run-out of one to a few Grids so the final gap (run-out Grids + tail) stays in (16, 64].
            var maxRunOutGrids = (MaximumCueMarkGapBeats - tailBeats) / GridBeats;
            var runOutCap = maxRunOutGrids < gridsAvailable ? maxRunOutGrids : gridsAvailable;
            var runOutGrids = 1 + (int)(NextRoll(ref rollState) % (uint)runOutCap);
            walkBeats = interiorBeats - runOutGrids * GridBeats;
        }

        // A monotonic roll from the Phrase start: each gap is one to four Grids, so both the minimum and
        // maximum cadence hold by construction and the walk lands exactly on walkBeats (a Grid multiple).
        var markOffset = 0;
        while (markOffset < walkBeats)
        {
            var gridsRemaining = (walkBeats - markOffset) / GridBeats;
            var maxGapGrids = gridsRemaining < MaximumGapGrids ? gridsRemaining : MaximumGapGrids;
            var gapGrids = 1 + (int)(NextRoll(ref rollState) % (uint)maxGapGrids);
            markOffset += gapGrids * GridBeats;
            cueMarkOffsets.Add(markOffset);
        }

        if (tailBeats != 0)
        {
            cueMarkOffsets.Add(phraseLengthBeats);
        }

        return new CueSheet(phraseLengthBeats, cueMarkOffsets.ToArray());
    }

    /// <summary>Folds the two announcement dimensions into a non-zero deterministic roll state (FNV-1a).</summary>
    private static uint SeedRoll(int phraseLengthBeats, int seed)
    {
        unchecked
        {
            var state = 2166136261u;
            state = (state ^ (uint)phraseLengthBeats) * 16777619u;
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
}
