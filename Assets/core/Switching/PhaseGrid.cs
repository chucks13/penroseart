/// <summary>
/// The 16-beat Phase grid arithmetic, in one place. Phase is exact integer modular math: the
/// one of a phrase sits at <see cref="OffsetForPhraseStart"/> within the 16-beat grid, and a
/// frame's grid <see cref="PositionFor"/> is recomputed from the running beat against that
/// offset. <see cref="PhaseLock"/> (stateful determiner) uses this, so the grid math is defined
/// exactly once.
/// </summary>
public static class PhaseGrid
{
    /// <summary>Beats in one Phase — the 16-beat grid the one repeats on.</summary>
    public const int PhraseBeats = 16;

    /// <summary>Beats in one bar — the 4-count.</summary>
    public const int BarBeats = 4;

    /// <summary>Floored modulo that stays non-negative for negative dividends (loops jump the beat backward).</summary>
    public static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    /// <summary>Where the one of a phrase starting at <paramref name="phraseStartBeat"/> sits in the 16-grid (0..15).</summary>
    public static int OffsetForPhraseStart(int phraseStartBeat) => Mod(phraseStartBeat - 1, PhraseBeats);

    /// <summary>The 1..16 grid position of <paramref name="beat"/> given a held <paramref name="offset"/>: <c>((beat − 1) − offset) mod 16 + 1</c>.</summary>
    public static int PositionFor(int beat, int offset) => Mod((beat - 1) - offset, PhraseBeats) + 1;

    /// <summary>
    /// The 1..4 bar position of <paramref name="beat"/> against a held <paramref name="offset"/>:
    /// <c>((beat − 1) − offset) mod 4 + 1</c>. This is the grid's own 4-count; a coherent feed's
    /// <c>beat_in_bar</c> tick must match it, so it is the single reference both the re-latch gate
    /// and the hold-time anomaly check compare against.
    /// </summary>
    public static int BarPositionFor(int beat, int offset) => Mod((beat - 1) - offset, BarBeats) + 1;

    /// <summary>The 1..4 count (beat-in-bar) of <paramref name="beat"/> from the running counter, grid offset 0: <c>(beat − 1) mod 4 + 1</c>.</summary>
    public static int FourCount(int beat) => BarPositionFor(beat, 0);

    /// <summary>
    /// Whether a Phrase of <paramref name="phraseLengthBeats"/> cannot subdivide into whole 16-beat
    /// phases — its length is not a multiple of <see cref="PhraseBeats"/>. The single definition of
    /// phrase irregularity, shared by PhaseLock (which drives CONTRADICTED) and PhraseTracker (the
    /// Director's diagnostic), so the two never silently diverge. Callers guard non-positive lengths.
    /// </summary>
    public static bool IsIrregularPhrase(int phraseLengthBeats) => phraseLengthBeats % PhraseBeats != 0;
}
