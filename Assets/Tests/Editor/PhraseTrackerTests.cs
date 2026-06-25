using NUnit.Framework;

/// <summary>
/// Encodes the <see cref="PhraseTracker"/> reading contract (slice 03 of the On-Air Timing
/// phase/phrase redesign). PhraseTracker is the PHRASE-layer reader: it rides on the PHASE-layer
/// <see cref="PhaseReading"/> from <see cref="PhaseLock"/> and reports where the current frame sits
/// inside the live Phrase, whether that Phrase is irregular, and a one-Phrase look-ahead while the
/// feed is counting down to the next Phrase.
///
/// These construct a <see cref="PhaseReading"/> directly and pass the three integer Phrase numerics
/// (the same values <see cref="OnAirTimingInput"/> projects) — no DJ-timeline harness, because the
/// Read is a pure per-frame mapping with no held state of its own.
/// </summary>
public sealed class PhraseTrackerTests
{
    /// <summary>An acquired Phase reading (offset on the grid) the tracker can ride on.</summary>
    private static PhaseReading Acquired =>
        new PhaseReading(0, 1, PhaseLockState.Locked, 0, false, false);

    [Test]
    public void InPhrase_PositionAdvancesAsBeatsUntilNextCountsDown()
    {
        // 64-beat Phrase, 57 beats still to run: 64 − 57 + 1 = 8th beat into the Phrase.
        var reading = new PhraseTracker().Read(Acquired, trackPhaseActive: 1, beatsUntilNext: 57, activeOrUpcomingLengthBeats: 64);

        Assert.That(reading.IsAcquired, Is.True);
        Assert.That(reading.PositionInPhrase, Is.EqualTo(8));
        Assert.That(reading.PhraseLengthBeats, Is.EqualTo(64));
        Assert.That(reading.BeatsUntilNextPhrase, Is.EqualTo(57));
        Assert.That(reading.HasLookAhead, Is.False, "A look-ahead is only emitted while counting down to the next Phrase.");
    }

    [Test]
    public void InPhrase_AtTheBoundaryBeatReportsNoPosition()
    {
        // beatsUntilNext == 0 is the boundary itself — that beat belongs to the next Phrase, so the
        // position must read none (-1), not length + 1 past the end of this Phrase.
        var reading = new PhraseTracker().Read(Acquired, trackPhaseActive: 1, beatsUntilNext: 0, activeOrUpcomingLengthBeats: 64);

        Assert.That(reading.PositionInPhrase, Is.EqualTo(-1));
    }

    [Test]
    public void UnacquiredPhase_ReadsNone()
    {
        var unacquired = new PhaseReading(-1, -1, PhaseLockState.Coasting, 0, false, false);

        var reading = new PhraseTracker().Read(unacquired, trackPhaseActive: 1, beatsUntilNext: 57, activeOrUpcomingLengthBeats: 64);

        Assert.That(reading, Is.EqualTo(PhraseTrackerReading.None));
        Assert.That(reading.IsAcquired, Is.False);
    }

    [Test]
    public void StandAloneFloor_ReadsNoneEvenWithAHeldOffsetAndActivePhrase()
    {
        // The 4-count clock is gone (StandAloneFloor) but PhaseLock still carries a held offset. That is
        // a mode exit (ADR-0004), so the Phrase layer must not emit phrase structure against a dead clock.
        var clockLost = new PhaseReading(0, -1, PhaseLockState.Coasting, 0, false, standAloneFloor: true);

        var reading = new PhraseTracker().Read(clockLost, trackPhaseActive: 1, beatsUntilNext: 57, activeOrUpcomingLengthBeats: 64);

        Assert.That(reading, Is.EqualTo(PhraseTrackerReading.None));
        Assert.That(reading.IsAcquired, Is.False);
    }

    [Test]
    public void IrregularPhrase_FlagsLengthNotMultipleOfSixteen()
    {
        // 24 beats does not subdivide into whole 16-beat Phases — phase ≠ phrase.
        var reading = new PhraseTracker().Read(Acquired, trackPhaseActive: 1, beatsUntilNext: 1, activeOrUpcomingLengthBeats: 24);

        Assert.That(reading.IsIrregular, Is.True);
    }

    [Test]
    public void RegularPhrase_DoesNotFlagIrregular()
    {
        // Re-derived phrase-locally from the length, so even the very first Phrase reads correctly
        // (no offset-shift history is needed, unlike PhaseReading.IrregularPhrase).
        var sixteen = new PhraseTracker().Read(Acquired, trackPhaseActive: 1, beatsUntilNext: 1, activeOrUpcomingLengthBeats: 16);
        var thirtyTwo = new PhraseTracker().Read(Acquired, trackPhaseActive: 1, beatsUntilNext: 1, activeOrUpcomingLengthBeats: 32);

        Assert.That(sixteen.IsIrregular, Is.False);
        Assert.That(thirtyTwo.IsIrregular, Is.False);
    }

    [Test]
    public void CountingDownToNextPhrase_PredictsTheUpcomingLength()
    {
        // Track Phase present but not yet started (tri-state 0) = counting down to the next Phrase.
        var reading = new PhraseTracker().Read(Acquired, trackPhaseActive: 0, beatsUntilNext: 8, activeOrUpcomingLengthBeats: 32);

        Assert.That(reading.HasLookAhead, Is.True);
        Assert.That(reading.PredictedUpcomingLengthBeats, Is.EqualTo(32));
        Assert.That(reading.PositionInPhrase, Is.EqualTo(-1), "No position inside a Phrase that has not started yet.");
        Assert.That(reading.PhraseLengthBeats, Is.EqualTo(-1));
    }

    [Test]
    public void NoPhrase_RidesOnPhaseButReportsNoPhraseStructure()
    {
        // Phase is acquired, but the Phrase feed is absent (tri-state −1).
        var reading = new PhraseTracker().Read(Acquired, trackPhaseActive: -1, beatsUntilNext: -1, activeOrUpcomingLengthBeats: -1);

        Assert.That(reading.IsAcquired, Is.True, "The reading still rides on the acquired Phase.");
        Assert.That(reading.PositionInPhrase, Is.EqualTo(-1));
        Assert.That(reading.PhraseLengthBeats, Is.EqualTo(-1));
        Assert.That(reading.HasLookAhead, Is.False);
        Assert.That(reading.IsIrregular, Is.False);
    }
}
