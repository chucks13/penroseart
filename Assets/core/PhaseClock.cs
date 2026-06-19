using System;

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
public readonly struct PhaseReading
{
    public static PhaseReading Unavailable { get; } =
        new PhaseReading(PhaseConfidence.Unlocked, -1, -1, -1, -1, -1, false, -1, -1, true);

    public readonly PhaseConfidence Confidence;
    public readonly int PhasePosition;
    public readonly int BarInPhrase;
    public readonly int BeatInBar;
    public readonly int OneOfCurrentPhrase;
    public readonly int Offset;
    public readonly bool CleanGrid;
    public readonly int PhrasesTotal;
    public readonly int PhrasesRemaining;
    public readonly bool BeatInBarAgrees;

    public PhaseReading(
        PhaseConfidence confidence,
        int phasePosition,
        int barInPhrase,
        int beatInBar,
        int oneOfCurrentPhrase,
        int offset,
        bool cleanGrid,
        int phrasesTotal,
        int phrasesRemaining,
        bool beatInBarAgrees)
    {
        Confidence = confidence;
        PhasePosition = phasePosition;
        BarInPhrase = barInPhrase;
        BeatInBar = beatInBar;
        OneOfCurrentPhrase = oneOfCurrentPhrase;
        Offset = offset;
        CleanGrid = cleanGrid;
        PhrasesTotal = phrasesTotal;
        PhrasesRemaining = phrasesRemaining;
        BeatInBarAgrees = beatInBarAgrees;
    }
}

/// <summary>
/// Resolves the 16-beat phrase-grid position from one RaveSystem OSC on-air frame.
/// This answers where the one is; signposts such as Fill, Drop, and Energy layer on top.
/// </summary>
public static class PhaseClock
{
    public const int PhraseBeats = 16;
    public const int BarBeats = 4;

    public static PhaseReading Resolve(in PhaseInput osc)
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
                return PhaseReading.Unavailable;
            }

            var phraseStart = osc.Beat - elapsedInPhrase;
            offset = Mod(phraseStart - 1, PhraseBeats);
            confidence = PhaseConfidence.Structural;
        }
        else if (hasBeat && hasTotal)
        {
            offset = Mod(osc.TotalBeats, PhraseBeats);
            confidence = PhaseConfidence.Closed;
        }
        else if (hasBeat)
        {
            offset = 0;
            confidence = PhaseConfidence.Open;
        }
        else if (osc.BeatInBar is >= 1 and <= BarBeats)
        {
            return new PhaseReading(
                PhaseConfidence.Provisional,
                osc.BeatInBar,
                1,
                osc.BeatInBar,
                -1,
                -1,
                false,
                -1,
                -1,
                true);
        }
        else
        {
            return PhaseReading.Unavailable;
        }

        var rel = Mod((osc.Beat - 1) - offset, PhraseBeats);
        var phasePosition = rel + 1;
        var oneOfCurrentPhrase = osc.Beat - rel;
        var barInPhrase = rel / BarBeats + 1;
        var beatInBar = rel % BarBeats + 1;
        var cleanGrid = hasTotal && osc.TotalBeats % PhraseBeats == 0;

        var phrasesTotal = -1;
        var phrasesRemaining = -1;
        if (hasTotal)
        {
            phrasesTotal = CeilDiv(osc.TotalBeats, PhraseBeats);
            var beatsLeft = osc.TotalBeats - osc.Beat;
            phrasesRemaining = beatsLeft <= 0 ? 0 : CeilDiv(beatsLeft, PhraseBeats);
        }

        var beatInBarAgrees = osc.BeatInBar < 1 || beatInBar == osc.BeatInBar;

        return new PhaseReading(
            confidence,
            phasePosition,
            barInPhrase,
            beatInBar,
            oneOfCurrentPhrase,
            offset,
            cleanGrid,
            phrasesTotal,
            phrasesRemaining,
            beatInBarAgrees);
    }

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;

    private static int CeilDiv(int dividend, int divisor) => (dividend + divisor - 1) / divisor;
}
