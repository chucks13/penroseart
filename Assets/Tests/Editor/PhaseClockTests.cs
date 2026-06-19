using NUnit.Framework;

public sealed class PhaseClockTests
{
    [Test]
    public void StructuralPhaseFindsOneFromPhaseCountdown()
    {
        var reading = PhaseClock.Resolve(new PhaseInput(
            beat: 81,
            totalBeats: -1,
            beatInBar: 1,
            phaseActive: 1,
            phaseCountBeats: 16,
            phaseLengthBeats: 64));

        Assert.That(reading.Confidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(reading.PhasePosition, Is.EqualTo(1));
        Assert.That(reading.BarInPhrase, Is.EqualTo(1));
        Assert.That(reading.BeatInBar, Is.EqualTo(1));
        Assert.That(reading.OneOfCurrentPhrase, Is.EqualTo(81));
        Assert.That(reading.BeatInBarAgrees, Is.True);
    }

    [Test]
    public void StructuralPhaseReportsFourBeatRunwayAtPositionThirteen()
    {
        var reading = PhaseClock.Resolve(new PhaseInput(
            beat: 93,
            totalBeats: -1,
            beatInBar: 1,
            phaseActive: 1,
            phaseCountBeats: 4,
            phaseLengthBeats: 64));

        Assert.That(reading.Confidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(reading.PhasePosition, Is.EqualTo(13));
        Assert.That(reading.BarInPhrase, Is.EqualTo(4));
        Assert.That(reading.BeatInBar, Is.EqualTo(1));
        Assert.That(reading.OneOfCurrentPhrase, Is.EqualTo(81));
        Assert.That(reading.BeatInBarAgrees, Is.True);
    }

    [Test]
    public void ClosedPhaseRecoversLeadInFromTrackEnd()
    {
        var reading = PhaseClock.Resolve(new PhaseInput(
            beat: 5,
            totalBeats: 388,
            beatInBar: 1,
            phaseActive: -1,
            phaseCountBeats: -1,
            phaseLengthBeats: -1));

        Assert.That(reading.Confidence, Is.EqualTo(PhaseConfidence.Closed));
        Assert.That(reading.PhasePosition, Is.EqualTo(1));
        Assert.That(reading.OneOfCurrentPhrase, Is.EqualTo(5));
        Assert.That(reading.Offset, Is.EqualTo(4));
        Assert.That(reading.CleanGrid, Is.False);
    }

    [Test]
    public void BeatInBarAgreementFlagsGridContradictions()
    {
        var reading = PhaseClock.Resolve(new PhaseInput(
            beat: 6,
            totalBeats: -1,
            beatInBar: 3,
            phaseActive: -1,
            phaseCountBeats: -1,
            phaseLengthBeats: -1));

        Assert.That(reading.Confidence, Is.EqualTo(PhaseConfidence.Open));
        Assert.That(reading.BeatInBar, Is.EqualTo(2));
        Assert.That(reading.BeatInBarAgrees, Is.False);
    }

    [Test]
    public void UnavailableWhenNoBeatReferenceExists()
    {
        var reading = PhaseClock.Resolve(new PhaseInput(
            beat: -1,
            totalBeats: -1,
            beatInBar: -1,
            phaseActive: -1,
            phaseCountBeats: -1,
            phaseLengthBeats: -1));

        Assert.That(reading.Confidence, Is.EqualTo(PhaseConfidence.Unlocked));
        Assert.That(reading.PhasePosition, Is.EqualTo(-1));
    }
}
