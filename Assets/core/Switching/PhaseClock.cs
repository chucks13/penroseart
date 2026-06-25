/// <summary>Confidence in where beat 1 of the 16-beat phrase grid sits.</summary>
public enum PhaseConfidence
{
    /// <summary>No usable beat reference; phase is unknown.</summary>
    Unlocked,

    /// <summary>Only beat-in-bar is known; the 16-beat phase is a guess.</summary>
    Provisional,

    /// <summary>Absolute beat is known, with beat 1 assumed to be the phrase-grid one.</summary>
    Open,

    /// <summary>Absolute beat and total beats are known, so the grid is checked from the track end.</summary>
    Closed,

    /// <summary>Anchored to current Track Phase data, so the one is structurally pinned.</summary>
    Structural,
}

/// <summary>The RaveSystem OSC on-air fields needed to locate the 16-beat phrase grid.</summary>
public readonly struct PhaseInput
{
    public readonly int Beat;
    public readonly int TotalBeats;
    public readonly int BeatInBar;
    public readonly int PhaseActive;
    public readonly int PhaseCountBeats;
    public readonly int PhaseLengthBeats;

    public PhaseInput(int beat, int totalBeats, int beatInBar, int phaseActive, int phaseCountBeats, int phaseLengthBeats)
    {
        Beat = beat;
        TotalBeats = totalBeats;
        BeatInBar = beatInBar;
        PhaseActive = phaseActive;
        PhaseCountBeats = phaseCountBeats;
        PhaseLengthBeats = phaseLengthBeats;
    }
}

/// <summary>Where the current on-air frame sits on the 16-beat phrase grid.</summary>
public readonly struct PhaseClockReading
{
    public static PhaseClockReading Unavailable { get; } =
        new PhaseClockReading(PhaseConfidence.Unlocked, -1, -1, -1, -1, -1, false, true);

    public readonly PhaseConfidence Confidence;
    public readonly int PhasePosition;
    public readonly int BarInPhrase;
    public readonly int BeatInBar;
    public readonly int OneOfCurrentPhrase;
    public readonly int Offset;
    public readonly bool CleanGrid;
    public readonly bool BeatInBarAgrees;

    public PhaseClockReading(
        PhaseConfidence confidence,
        int phasePosition,
        int barInPhrase,
        int beatInBar,
        int oneOfCurrentPhrase,
        int offset,
        bool cleanGrid,
        bool beatInBarAgrees)
    {
        Confidence = confidence;
        PhasePosition = phasePosition;
        BarInPhrase = barInPhrase;
        BeatInBar = beatInBar;
        OneOfCurrentPhrase = oneOfCurrentPhrase;
        Offset = offset;
        CleanGrid = cleanGrid;
        BeatInBarAgrees = beatInBarAgrees;
    }
}

/// <summary>
/// The 16-beat Phase grid arithmetic, in one place. Phase is exact integer modular math: the
/// one of a phrase sits at <see cref="OffsetForPhraseStart"/> within the 16-beat grid, and a
/// frame's grid <see cref="PositionFor"/> is recomputed from the running beat against that
/// offset. Both <see cref="PhaseClock"/> (legacy stateless resolver) and <see cref="PhaseLock"/>
/// (stateful determiner) share this, so the grid math is defined exactly once.
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

/// <summary>
/// Legacy stateless resolver: locates the 16-beat phrase-grid position from one RaveSystem OSC
/// on-air frame. This answers where the one is; signposts such as Fill, Drop, and Energy layer
/// on top. The grid arithmetic now lives in <see cref="PhaseGrid"/>, and the stateful successor
/// is <see cref="PhaseLock"/>. This adapter remains only until the Director's cue path
/// (OnAirTiming.ReadFrame → TimingFrame → ControllerEditor) is rewired off it; that retargeting
/// and this resolver's removal land in slice 04. Slice 03 composes PhaseLock + PhraseTracker in
/// alongside it as a separate reading fragment, but deliberately leaves the cue path on this resolver.
/// </summary>
public static class PhaseClock
{
    public static PhaseClockReading Resolve(in PhaseInput osc)
    {
        var hasBeat = osc.Beat >= 1;
        var hasTotal = osc.TotalBeats >= 1;
        var hasPhrase = osc.PhaseActive >= 1 && osc.PhaseCountBeats > 0 && osc.PhaseLengthBeats >= 1;

        int offset;
        PhaseConfidence confidence;

        if (hasBeat && hasPhrase)
        {
            var elapsedInPhrase = osc.PhaseLengthBeats - osc.PhaseCountBeats;
            if (elapsedInPhrase < 0)
            {
                return PhaseClockReading.Unavailable;
            }

            var phraseStart = osc.Beat - elapsedInPhrase;
            offset = PhaseGrid.OffsetForPhraseStart(phraseStart);
            confidence = PhaseConfidence.Structural;
        }
        else if (hasBeat && hasTotal)
        {
            offset = PhaseGrid.Mod(osc.TotalBeats, PhaseGrid.PhraseBeats);
            confidence = PhaseConfidence.Closed;
        }
        else if (hasBeat)
        {
            offset = 0;
            confidence = PhaseConfidence.Open;
        }
        else if (osc.BeatInBar is >= 1 and <= PhaseGrid.BarBeats)
        {
            return new PhaseClockReading(
                PhaseConfidence.Provisional,
                osc.BeatInBar,
                1,
                osc.BeatInBar,
                -1,
                -1,
                false,
                true);
        }
        else
        {
            return PhaseClockReading.Unavailable;
        }

        var rel = PhaseGrid.Mod((osc.Beat - 1) - offset, PhaseGrid.PhraseBeats);
        var phasePosition = rel + 1;
        var oneOfCurrentPhrase = osc.Beat - rel;
        var barInPhrase = rel / PhaseGrid.BarBeats + 1;
        var beatInBar = rel % PhaseGrid.BarBeats + 1;
        var cleanGrid = hasTotal && osc.TotalBeats % PhaseGrid.PhraseBeats == 0;

        var beatInBarAgrees = osc.BeatInBar < 1 || beatInBar == osc.BeatInBar;

        return new PhaseClockReading(
            confidence,
            phasePosition,
            barInPhrase,
            beatInBar,
            oneOfCurrentPhrase,
            offset,
            cleanGrid,
            beatInBarAgrees);
    }
}
