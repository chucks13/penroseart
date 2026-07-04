using System;
using NUnit.Framework;

// EnergyCasting maps live phrase Energy to the energy-affinity Repertoire the Director prefers when casting
// a Performer. The cast-ahead shape mirrors CueEventIntent.DropApproaching: a change landing within the
// cadence stint after the Impact Point casts for the incoming level.
public sealed class EnergyCastingTests
{
    private const int CastAheadBeats = 16;

    [Test]
    public void ReturnsNoneWhenEnergyIsUnavailable()
    {
        var preference = EnergyCasting.PreferredEnergyRepertoire(
            energy: null, currentBeat: 600, impactBeat: 608, CastAheadBeats);

        Assert.That(preference, Is.EqualTo(Repertoire.None));
    }

    [Test]
    public void PrefersTheCurrentLevelWhenNoChangeIsQueued()
    {
        var preference = EnergyCasting.PreferredEnergyRepertoire(
            Energy(EnergyLevel.High, next: null, beatsUntilChange: null),
            currentBeat: 600, impactBeat: 608, CastAheadBeats);

        Assert.That(preference, Is.EqualTo(Repertoire.EnergyHigh));
    }

    [Test]
    public void PrefersTheCurrentLevelWhenTheChangeLandsBeyondTheCastAheadWindow()
    {
        // Impact at +8; the change at +40 is well past the +8..+24 stint, so the Performer plays its whole
        // stint in the current (Low) energy.
        var preference = EnergyCasting.PreferredEnergyRepertoire(
            Energy(EnergyLevel.Low, EnergyLevel.High, beatsUntilChange: 40),
            currentBeat: 600, impactBeat: 608, CastAheadBeats);

        Assert.That(preference, Is.EqualTo(Repertoire.EnergyLow));
    }

    [Test]
    public void PrefersTheIncomingLevelWhenTheChangeLandsAtTheImpact()
    {
        // Impact at +8 and the change also at +8: the Performer spends its whole stint in the new (High) energy.
        var preference = EnergyCasting.PreferredEnergyRepertoire(
            Energy(EnergyLevel.Low, EnergyLevel.High, beatsUntilChange: 8),
            currentBeat: 600, impactBeat: 608, CastAheadBeats);

        Assert.That(preference, Is.EqualTo(Repertoire.EnergyHigh));
    }

    [Test]
    public void PrefersTheCurrentLevelWhenTheChangeAlreadyLandedBeforeTheImpact()
    {
        // Impact at +8; the change at +3 is before the Impact Point, so it is outside the at-or-after window.
        var preference = EnergyCasting.PreferredEnergyRepertoire(
            Energy(EnergyLevel.Low, EnergyLevel.High, beatsUntilChange: 3),
            currentBeat: 600, impactBeat: 608, CastAheadBeats);

        Assert.That(preference, Is.EqualTo(Repertoire.EnergyLow));
    }

    [Test]
    public void CastAheadWindowIsInclusiveAtExactlyCastAheadBeatsAndExclusiveBeyond()
    {
        // Impact at +8, cast-ahead 16 → the stint edge is +24. A change exactly at +24 still casts ahead;
        // one beat later (+25) falls back to the current level.
        var atBoundary = EnergyCasting.PreferredEnergyRepertoire(
            Energy(EnergyLevel.Low, EnergyLevel.High, beatsUntilChange: 24),
            currentBeat: 600, impactBeat: 608, CastAheadBeats);
        var pastBoundary = EnergyCasting.PreferredEnergyRepertoire(
            Energy(EnergyLevel.Low, EnergyLevel.High, beatsUntilChange: 25),
            currentBeat: 600, impactBeat: 608, CastAheadBeats);

        Assert.That(atBoundary, Is.EqualTo(Repertoire.EnergyHigh), "A change at exactly +castAheadBeats casts ahead.");
        Assert.That(pastBoundary, Is.EqualTo(Repertoire.EnergyLow), "A change one beat past the window keeps the current level.");
    }

    [Test]
    public void MapsEachLevelToItsOwnAffinityFlag()
    {
        Assert.That(
            EnergyCasting.PreferredEnergyRepertoire(Energy(EnergyLevel.Low, null, null), 600, 608, CastAheadBeats),
            Is.EqualTo(Repertoire.EnergyLow));
        Assert.That(
            EnergyCasting.PreferredEnergyRepertoire(Energy(EnergyLevel.Mid, null, null), 600, 608, CastAheadBeats),
            Is.EqualTo(Repertoire.EnergyMid));
        Assert.That(
            EnergyCasting.PreferredEnergyRepertoire(Energy(EnergyLevel.High, null, null), 600, 608, CastAheadBeats),
            Is.EqualTo(Repertoire.EnergyHigh));
    }

    private static EnergyInfo Energy(EnergyLevel level, EnergyLevel? next, int? beatsUntilChange)
    {
        var direction = next is { } n ? Math.Sign((int)n - (int)level) : 0;
        return new EnergyInfo(
            level,
            next,
            beatsUntilChange,
            (int)level * 0.5f,
            direction,
            runProgress: null,
            runLengthBeats: null,
            nextRunLengthBeats: null);
    }
}
