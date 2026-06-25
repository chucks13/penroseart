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
    public void IrregularPhrase_ReportsContradictedThenReLatchesToLockedAtTheNextRegularBoundary()
    {
        // A 24-beat phrase is not a multiple of 16, so it cannot subdivide into whole 16-beat phases.
        // PhaseLock reports CONTRADICTED for the phrase's duration while still holding a usable position
        // (the wall keeps rendering); the next regular phrase boundary re-latches cleanly to LOCKED.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 24),
            DjFrame.InPhrase(beat: 73, phraseStartBeat: 73, phraseLengthBeats: 64));

        var irregular = readings[0];
        Assert.That(irregular.State, Is.EqualTo(PhaseLockState.Contradicted),
            "An irregular phrase is reported NOT-Locked (Contradicted) for its duration.");
        Assert.That(irregular.Offset, Is.EqualTo(0), "Phrase start 49 anchors the grid at offset 0.");
        Assert.That(irregular.Position, Is.EqualTo(12),
            "Position stays usable through the contradiction: beat 60 against offset 0.");

        var reLatched = readings[1];
        Assert.That(reLatched.State, Is.EqualTo(PhaseLockState.Locked),
            "The next regular phrase boundary re-latches cleanly to Locked.");
        Assert.That(reLatched.Offset, Is.EqualTo(8), "Phrase start 73 re-anchors the grid; offset becomes 8.");
        Assert.That(reLatched.Position, Is.EqualTo(1));
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
        Assert.That(coasting.StandAloneFloor, Is.False, "A clock still exists, so we stay synced.");
    }

    [Test]
    public void TrackChange_ResetsThenReAcquiresAtTheNewSongsFirstBoundary()
    {
        // The track change is the real signal: the /rave/onair/track title becomes a track ordinal
        // (BeatManager.TrackOrdinal -> OnAirTimingInput.TrackOrdinal). A new song is a clean slate —
        // `beat` is a per-track counter, so the old offset is meaningless. PhaseLock resets and
        // re-acquires from the new song's first Phrase.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64, trackOrdinal: 1),
            DjFrame.BeatOnly(beat: 1, trackOrdinal: 2),
            DjFrame.InPhrase(beat: 1, phraseStartBeat: 1, phraseLengthBeats: 64, trackOrdinal: 2));

        var reset = readings[1];
        Assert.That(reset.Offset, Is.EqualTo(0), "A track change drops the held offset; with nothing held the grid lines up on the running beat (offset 0), not the old song's grid.");
        Assert.That(reset.Position, Is.EqualTo(1), "Beat 1 against offset 0 is grid position 1.");
        Assert.That(reset.State, Is.EqualTo(PhaseLockState.Coasting), "A beat-only guess pending the new song's first Phrase is COASTING, not a lock.");

        var reAcquired = readings[2];
        Assert.That(reAcquired.Offset, Is.EqualTo(0), "The new song's Phrase data establishes the grid, the same way any Phrase does — no special first-Phrase bootstrap.");
        Assert.That(reAcquired.Position, Is.EqualTo(1));
        Assert.That(reAcquired.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void TransientTitleBlank_KeepsTheHeldOffsetWhenTheSameTrackResumes()
    {
        // A momentary /rave/onair/track title dropout reports TrackOrdinal -1 for a frame, then the
        // SAME track resumes with its unchanged ordinal. The -1 is "title unknown", not "new song",
        // so it must not poison the track-change detection: the held offset has to survive the blank.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64, trackOrdinal: 5),
            DjFrame.BeatOnly(beat: 61, trackOrdinal: -1),
            DjFrame.BeatOnly(beat: 62, trackOrdinal: 5));

        var resumed = readings[2];
        Assert.That(resumed.Offset, Is.EqualTo(0),
            "The same track resuming after a title blank keeps the held offset; the -1 sentinel is not a track change.");
        Assert.That(resumed.Position, Is.EqualTo(14), "Position keeps recomputing off the held offset: beat 62 is position 14.");
        Assert.That(resumed.State, Is.EqualTo(PhaseLockState.Coasting), "Phrase feed is still out, so it coasts on the held offset.");
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

    [Test]
    public void NoPhrase_FallsBackToTheRunningBeatGrid()
    {
        // No Phrase data and nothing held (a fresh track before its first boundary, or a feed that
        // never sends Phrase data): PhaseLock lines the grid up on the running beat itself — offset 0,
        // position = beat mod 16. The grid lines up on the running beat at offset 0. It is a guess,
        // so it stays COASTING and is never latched.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.BeatOnly(beat: 12));

        var fallback = readings[0];
        Assert.That(fallback.Offset, Is.EqualTo(0), "The fallback grid is offset 0 — lined up on the running beat, not the track length.");
        Assert.That(fallback.Position, Is.EqualTo(12), "Position is beat mod 16: beat 12 against offset 0.");
        Assert.That(fallback.State, Is.EqualTo(PhaseLockState.Coasting), "A beat-only guess is not a phrase lock.");
        Assert.That(fallback.StandAloneFloor, Is.False, "A clock still exists, so we stay synced.");
    }

    [Test]
    public void BackwardSeekToADifferentPhrase_ReLatchesInsteadOfHoldingTheStaleOffset()
    {
        // No track-change signal (same ordinal): a backward jump into an earlier Phrase with a
        // different offset must re-latch off the new Phrase start, not fall into the same-Phrase
        // branch and report Locked while computing the position from the old (wrong) offset.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 200, phraseStartBeat: 193, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 140, phraseStartBeat: 137, phraseLengthBeats: 64));

        var reLatched = readings[1];
        Assert.That(reLatched.Offset, Is.EqualTo(8), "Phrase start 137 re-anchors the grid (136 mod 16 = 8).");
        Assert.That(reLatched.Position, Is.EqualTo(4), "Beat 140 is grid position 4 against offset 8.");
        Assert.That(reLatched.State, Is.EqualTo(PhaseLockState.Locked));
    }

    [Test]
    public void Contradiction_ClearsToCoastingWhenThePulseRecoversDuringADropout()
    {
        // A sub-bar flub contradicts, then the Phrase feed drops out while the 4-count recovers. The
        // contradiction must not stick: it is a per-frame anomaly, so the dropout coasts cleanly.
        var readings = PhaseTimelineHarness.Run(
            DjFrame.InPhrase(beat: 60, phraseStartBeat: 49, phraseLengthBeats: 64),
            DjFrame.InPhrase(beat: 57, phraseStartBeat: 49, phraseLengthBeats: 64, beatInBar: 2),
            DjFrame.BeatOnly(beat: 61));

        Assert.That(readings[1].IsContradicted, Is.True, "The sub-bar flub contradicts.");

        var recovered = readings[2];
        Assert.That(recovered.IsContradicted, Is.False, "Once the 4-count agrees again the contradiction clears.");
        Assert.That(recovered.State, Is.EqualTo(PhaseLockState.Coasting), "With the Phrase feed gone it coasts on the held offset.");
        Assert.That(recovered.Offset, Is.EqualTo(0), "The offset is still held through the anomaly and the dropout.");
    }
}
