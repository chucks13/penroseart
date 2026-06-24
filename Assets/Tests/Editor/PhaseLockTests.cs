using NUnit.Framework;

/// <summary>
/// Encodes the locked Phase contract (ADR-0006 + on-air-timing-redesign-2026-06-24.html) as
/// scripted DJ timelines driven through <see cref="PhaseTimelineHarness"/>. Slice 01 is the
/// TDD red phase: <see cref="PhaseLock"/> is a skeleton, so these assert the externally visible
/// <see cref="PhaseReading"/> contract and run red until slice 02 lands the held-offset model.
///
/// Phase math under test: offset = (phraseStart − 1) mod 16, and
/// position = ((beat − 1) − offset) mod 16 + 1. A frame on a Phrase start always reads position 1.
/// </summary>
public sealed class PhaseLockTests
{
    [Test]
    public void SteadyAdvance_HoldsOffsetAndAdvancesPosition()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 132, phraseStartBeat: 129, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 136, phraseStartBeat: 129, phraseLengthBeats: 64));

        var now = readings[1];
        Assert.That(now.Offset, Is.EqualTo(0), "Phrase start 129 sits on the grid, so the held offset is 0.");
        Assert.That(now.Position, Is.EqualTo(8), "Beat 136 is the 8th grid position.");
        Assert.That(now.State, Is.EqualTo(PhaseLockState.Locked));
        Assert.That(now.IsContradicted, Is.False);
    }

    [Test]
    public void OneBarLoop_StepsPositionBackFourWithMusic()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 136, phraseStartBeat: 129, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 132, phraseStartBeat: 129, phraseLengthBeats: 64));

        var afterLoop = readings[1];
        Assert.That(afterLoop.Offset, Is.EqualTo(0), "A bar-aligned backward jump holds the offset.");
        Assert.That(afterLoop.Position, Is.EqualTo(4), "Beat 132 rewinds the position to 4, tracking the music.");
        Assert.That(afterLoop.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void TwoBarLoop_StepsPositionBackEightWithMusic()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 144, phraseStartBeat: 129, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 136, phraseStartBeat: 129, phraseLengthBeats: 64));

        var afterLoop = readings[1];
        Assert.That(afterLoop.Offset, Is.EqualTo(0));
        Assert.That(afterLoop.Position, Is.EqualTo(8), "An 8-beat loop rewinds the position to 8.");
        Assert.That(afterLoop.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void FourBarLoop_LeavesPhaseUndisturbed()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 129, phraseStartBeat: 129, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 144, phraseStartBeat: 129, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 129, phraseStartBeat: 129, phraseLengthBeats: 64));

        var afterLoop = readings[2];
        Assert.That(afterLoop.Offset, Is.EqualTo(0));
        Assert.That(afterLoop.Position, Is.EqualTo(1), "A 16-beat loop is in-phase: the position is identical.");
        Assert.That(afterLoop.State, Is.EqualTo(PhaseLockState.Locked));
        Assert.That(afterLoop.IsContradicted, Is.False);
    }

    [Test]
    public void EightBarLoop_LeavesPhaseUndisturbed()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 176, phraseStartBeat: 129, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 144, phraseStartBeat: 129, phraseLengthBeats: 64));

        var afterLoop = readings[1];
        Assert.That(afterLoop.Offset, Is.EqualTo(0));
        Assert.That(afterLoop.Position, Is.EqualTo(16), "A 32-beat loop is in-phase: the position is identical.");
        Assert.That(afterLoop.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void SixteenBarLoop_LeavesPhaseUndisturbed()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 200, phraseStartBeat: 129, phraseLengthBeats: 128),
            DjFrame.InPhrase(beat: 136, phraseStartBeat: 129, phraseLengthBeats: 128));

        var afterLoop = readings[1];
        Assert.That(afterLoop.Offset, Is.EqualTo(0));
        Assert.That(afterLoop.Position, Is.EqualTo(8), "A 64-beat loop is in-phase: the position is identical.");
        Assert.That(afterLoop.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void LeadIn_LandsPositionOneViaTheSameOffsetMechanism()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.BeforePhrase(beat: 49, phraseStartBeat: 53, upcomingLengthBeats: 64),
            DjFrame.InPhrase(beat: 53, phraseStartBeat: 53, phraseLengthBeats: 64));

        var atFirstOne = readings[1];
        Assert.That(atFirstOne.Offset, Is.EqualTo(4), "Phrase start 53 is 4 beats off the grid, so the offset latches to 4.");
        Assert.That(atFirstOne.Position, Is.EqualTo(1), "The Phrase start is a one: position 1, no special case.");
        Assert.That(atFirstOne.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void NonPowerOfTwoPhrase_ReAnchorsTheGridAndFlagsIrregular()
    {
        // Phrase 1 is 24 beats (not a multiple of 16); the next boundary shifts the offset by a whole bar.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 24),
            DjFrame.InPhrase(beat: 73, phraseStartBeat: 73, phraseLengthBeats: 64));

        var atBoundary = readings[1];
        Assert.That(atBoundary.Offset, Is.EqualTo(8), "Phrase start 73 re-anchors the grid; offset becomes 8.");
        Assert.That(atBoundary.Position, Is.EqualTo(1));
        Assert.That(atBoundary.IrregularPhrase, Is.True, "A phrase whose length is not a multiple of 16 is flagged irregular.");
        Assert.That(atBoundary.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void ForwardSeek_ReEstablishesPhaseAtTheNextBoundary()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 173, phraseStartBeat: 173, phraseLengthBeats: 64));

        var reEstablished = readings[1];
        Assert.That(reEstablished.Offset, Is.EqualTo(12), "The next Phrase boundary re-establishes the offset (172 mod 16 = 12).");
        Assert.That(reEstablished.Position, Is.EqualTo(1));
        Assert.That(reEstablished.BeatsSinceAnchor, Is.EqualTo(0), "A fresh re-latch resets the staleness count.");
        Assert.That(reEstablished.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void PhraseDataDropout_CoastsAndHoldsTheOffset()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64),
            DjFrame.BeatOnly(beat: 64),
            DjFrame.BeatOnly(beat: 68));

        var coasting = readings[2];
        Assert.That(coasting.Offset, Is.EqualTo(0), "The offset is held through the dropout.");
        Assert.That(coasting.Position, Is.EqualTo(4), "Position keeps recomputing off the clock: beat 68 is position 4.");
        Assert.That(coasting.State, Is.EqualTo(PhaseLockState.Coasting));
        Assert.That(coasting.BeatsSinceAnchor, Is.GreaterThan(0), "Dead-reckoning accrues staleness.");
        Assert.That(coasting.StandAloneFloor, Is.False, "A clock still exists, so we stay synced.");
    }

    [Test]
    public void TrackChange_SoftHoldsThenReLatchesAtNextBoundary()
    {
        // The totalBeats jump (384 -> 512) is a Slice 01 stand-in for the track change. The real
        // signal is the /rave/onair/track title (already in RaveOscSnapshot/BeatManager, not yet
        // surfaced); Slice 02 surfaces it and drives this fixture off it. See issue 02.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64, totalBeats: 384),
            DjFrame.BeatOnly(beat: 1, totalBeats: 512),
            DjFrame.InPhrase(beat: 1, phraseStartBeat: 1, phraseLengthBeats: 64, totalBeats: 512));

        var softHold = readings[1];
        Assert.That(softHold.Offset, Is.EqualTo(0), "A track change soft-holds the offset rather than dropping the one.");
        Assert.That(softHold.State, Is.EqualTo(PhaseLockState.Coasting), "Confidence drops pending a re-latch.");

        var reLatched = readings[2];
        Assert.That(reLatched.Offset, Is.EqualTo(0));
        Assert.That(reLatched.Position, Is.EqualTo(1));
        Assert.That(reLatched.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void ClockLoss_SignalsStandAloneFloor()
    {
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64),
            DjFrame.NoClock());

        var floor = readings[1];
        Assert.That(floor.StandAloneFloor, Is.True, "No beat_in_bar (-1) exits synced mode to stand-alone timing.");
        Assert.That(floor.Position, Is.EqualTo(-1), "There is no Phase position without a clock.");
    }

    [Test]
    public void SubBarFlub_BreaksTheFourCountAndContradicts()
    {
        // Beat jumps back 3 (sub-bar) and the 4-count breaks: expected 1 for beat 57, but the feed says 2.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 57, phraseStartBeat: 49, phraseLengthBeats: 64, beatInBar: 2));

        var anomaly = readings[1];
        Assert.That(anomaly.IsContradicted, Is.True, "A broken 4-count is a Layer-0 anomaly: hold the offset, flag CONTRADICTED.");
        Assert.That(anomaly.Offset, Is.EqualTo(0), "The last good offset is held through the anomaly.");
    }

    [Test]
    public void AnotherLoopScenario_IsAddedAsDataWithoutNewHarnessPlumbing()
    {
        // A 2-bar loop on a different, off-grid Phrase — expressed purely as data through the same harness.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 70, phraseStartBeat: 55, phraseLengthBeats: 32),
            DjFrame.InPhrase(beat: 62, phraseStartBeat: 55, phraseLengthBeats: 32));

        // offset = (55 − 1) mod 16 = 6; position at beat 62 = ((62 − 1) − 6) mod 16 + 1 = 55 mod 16 + 1 = 8.
        var afterLoop = readings[1];
        Assert.That(afterLoop.Offset, Is.EqualTo(6));
        Assert.That(afterLoop.Position, Is.EqualTo(8));
        Assert.That(afterLoop.State, Is.EqualTo(PhaseLockState.Locked));
    }
}
