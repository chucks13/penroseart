using System;
using NUnit.Framework;

// Behaviour of the Director-owned CuePlanner. It plans strictly off Track Phase (OSC schema v2 always
// broadcasts current + next Phrase in sync mode): a fresh Cue Sheet on every Phrase change (same-length
// turnover included), the upcoming Phrase's own announced length for the look-ahead, and pass-local
// cue/cadence memory seeded through RecordCueIssued/MarkChanged.
public sealed class CuePlannerTests
{
    [Test]
    public void TrackPhaseFrameSelectsInteriorCueMarkBeforeMandatoryFinalCueMark()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 21, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasCueMark, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(609));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(5));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void SameLengthPhraseTurnoverBuildsAFreshCueSheet()
    {
        var randomCalls = 0;
        var cuePlanner = new CuePlanner((minInclusive, maxExclusive) =>
        {
            randomCalls++;
            return minInclusive;
        });

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        var callsAfterFirstSheet = randomCalls;
        Assert.That(callsAfterFirstSheet, Is.GreaterThan(0), "The first Phrase consults the random source to build its sheet.");

        // Same-length turnover to a new Phrase Window start: the sheet is rebuilt from scratch (no
        // length-identity reuse), so the random source is consulted again.
        cuePlanner.Plan(
            TrackPhaseInput(beat: 642, beatsUntilPhraseEnd: 63, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(randomCalls, Is.GreaterThan(callsAfterFirstSheet), "A same-length turnover must build a fresh Cue Sheet.");
    }

    [Test]
    public void DifferentLengthPhraseUpdateRegeneratesCueSheet()
    {
        var randomCalls = 0;
        var cuePlanner = new CuePlanner((minInclusive, maxExclusive) =>
        {
            randomCalls++;
            return randomCalls == 1 && maxExclusive > minInclusive + 1 ? minInclusive + 1 : minInclusive;
        });

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        var callsAfterFirstSheet = randomCalls;

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 600, beatsUntilPhraseEnd: 25, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(randomCalls, Is.GreaterThan(callsAfterFirstSheet), "A different Phrase length builds a new Cue Sheet.");
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(593));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(625));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(625));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void NewPhraseFrameArrivingExactlyOnTheMissedBoundaryPlansTheNewPhrase()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        // Beat 609 is at/past the 609 boundary's Lock Point for any runway: the mark is missed for
        // good. The frame on the boundary rebuilds the new Phrase's sheet (609..673), whose first
        // selected interior mark lands on the 16-grid at 625.
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 609, beatsUntilPhraseEnd: 64, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(625));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void PendingMandatoryBoundaryStaysTheTargetWhileTheCurrentPhraseNearsItsEnd()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        // The current Phrase is still live (counting down) with the next Phrase already announced: the
        // pending mandatory boundary stays the target.
        var frame = cuePlanner.Plan(
            PhraseWithNextInput(beat: 600, beatsUntilPhraseEnd: 9, phraseLengthBeats: 32, nextPhraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void FiredMandatoryBoundaryHandsOffToTheUpcomingPhraseCueMark()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        cuePlanner.RecordCueIssued(608);
        cuePlanner.MarkChanged(609);

        // The consumed sheet no longer drives: the next-phrase look-ahead builds the upcoming sheet and
        // its first mark past the fired boundary becomes the target.
        var frame = cuePlanner.Plan(
            UpcomingPhraseInput(beat: 607, nextPhraseStartInBeats: 2, nextPhraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(625));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void UpcomingPhraseBuildsItsSheetFromTheTrueNextLength()
    {
        // Pre-first-phrase countdown: no current Phrase, only the next announced with its own length. The
        // look-ahead window uses that true length (48), not a guess.
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            UpcomingPhraseInput(beat: 10, nextPhraseStartInBeats: 6, nextPhraseLengthBeats: 48),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasCueMark, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(16));
        Assert.That(frame.PhraseWindow.LengthBeats, Is.EqualTo(48));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(64));
    }

    [Test]
    public void UnavailableFrameIsUnlockedWithoutFakeTarget()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            OnAirTimingInput.Unavailable,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasCueMark, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
    }

    [Test]
    public void FrameWithNoCurrentAndNoNextPhraseIsUnlocked()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        // A running beat but neither a current nor an upcoming Phrase (brief, e.g. the first frames of a
        // track): idle unlocked rather than fabricate a target.
        var frame = cuePlanner.Plan(
            new OnAirTimingInput(beat: 589, beatsUntilPhraseEnd: -1, phraseLengthBeats: -1, nextPhraseStartInBeats: -1, nextPhraseLengthBeats: -1),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasCueMark, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsCueSheetAndMovesCursorBack()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
        Assert.That(frame.BeatRewoundToNewPass, Is.False);

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void SamePhraseWindowBeatRewindClearsCommitMemoryThatWouldBlockLoopPass()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        cuePlanner.RecordCueIssued(589);
        cuePlanner.MarkChanged(593);
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 587, beatsUntilPhraseEnd: 54, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(
            cuePlanner.EvaluateCueTiming(593, FourBeatRunwayRepertoire(), 587, 16),
            Is.EqualTo(CueTimingVerdict.Cue),
            "Cue/commit memory from the previous pass must not block the replayed mark.");
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsCommitMemoryThatCannotBlockCurrentPass()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        cuePlanner.RecordCueIssued(580);
        cuePlanner.MarkChanged(577);
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 589, beatsUntilPhraseEnd: 52, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(cuePlanner.CanChangeAt(592, minimumChangeCadenceBeats: 16), Is.False,
            "The pre-rewind commit on 577 still binds cadence.");
        Assert.That(cuePlanner.CanChangeAt(593, minimumChangeCadenceBeats: 16), Is.True);
        Assert.That(
            cuePlanner.EvaluateCueTiming(584, FourBeatRunwayRepertoire(), 580, 16),
            Is.EqualTo(CueTimingVerdict.Wait),
            "The pre-rewind cue issued on 580 still binds.");
    }

    [Test]
    public void SmallBeatBackstepIsJitterAndDoesNotResetSelectedBoundaryCursorOrCommitMemory()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 47, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));

        cuePlanner.RecordCueIssued(580);
        cuePlanner.MarkChanged(593);
        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 592, beatsUntilPhraseEnd: 49, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.False);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
        Assert.That(cuePlanner.CanChangeAt(608, minimumChangeCadenceBeats: 16), Is.False,
            "The commit on 593 still binds cadence across a jitter backstep.");
        Assert.That(cuePlanner.CanChangeAt(609, minimumChangeCadenceBeats: 16), Is.True);
        Assert.That(
            cuePlanner.EvaluateCueTiming(584, FourBeatRunwayRepertoire(), 580, 16),
            Is.EqualTo(CueTimingVerdict.Wait),
            "The cue issued on 580 still binds across a jitter backstep.");
    }

    [Test]
    public void MissedUnconsumedCueMarkIsSkippedOncePassed()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 47, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(641),
            "A mark that passed uncommitted is skipped, never held open or fired late.");
    }

    [Test]
    public void PhraseTurnoverSkipsTheMissedBoundaryAndPlansTheNewPhrase()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 610, beatsUntilPhraseEnd: 31, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(625),
            "A boundary that passed uncommitted is gone; the new Phrase Window plans its own marks.");
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void LoopRewindIntoAFiredCueMarkDoesNotRepresentIt()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsUntilPhraseEnd: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        cuePlanner.RecordCueIssued(589);
        cuePlanner.MarkChanged(593);
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsUntilPhraseEnd: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        // The loop replays from 594 — just past the fired mark. The pass-local commit memory
        // (593 < 594) survives the rewind, so the fired mark must not come back.
        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsUntilPhraseEnd: 47, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(cuePlanner.CanChangeAt(608, minimumChangeCadenceBeats: 16), Is.False, "The commit memory survives a rewind to after the fired mark.");
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641), "The fired mark must not be re-presented after it passed.");
    }

    [Test]
    public void IrregularPhraseStillLandsTheMandatoryCueAtThePhraseBoundary()
    {
        // The load-bearing invariant: the phrase-end (mandatory) cue is phrase-driven and always lands at
        // the boundary, even for a phrase whose length is not a multiple of 16 (the interior grid is in
        // dispute). Grid confidence only refines interior placement.
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        // A 56-beat phrase (56 % 16 = 8) runs 585..641; beat 636 is past the last interior mark, so the
        // mandatory phrase-end cue at 641 is the only target left.
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 636, beatsUntilPhraseEnd: 5, phraseLengthBeats: 56),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    // Per-beat cue timing verdict: the planner answers from its own pass-local memory, seeded through
    // RecordCueIssued/MarkChanged like the Director.

    [Test]
    public void EvaluateCueTimingCuesBeforeTheLockPoint()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);

        // Cue Mark 609, Runway 4: Start Beat 605, Lock Point 604 — beat 603 is the last commit chance.
        var verdict = cuePlanner.EvaluateCueTiming(609, FourBeatRunwayRepertoire(), 603, 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Cue));
    }

    [TestCase(604, TestName = "EvaluateCueTimingWaitsAtTheLockPoint")]
    [TestCase(605, TestName = "EvaluateCueTimingWaitsAtTheRunwayStart")]
    [TestCase(611, TestName = "EvaluateCueTimingWaitsInsideTheTail")]
    [TestCase(614, TestName = "EvaluateCueTimingWaitsPastCompleteBeat")]
    public void EvaluateCueTimingWaitsFromTheLockPointOn(int beat)
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);

        var verdict = cuePlanner.EvaluateCueTiming(609, FourBeatRunwayRepertoire(), beat, 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Wait), "A mark whose Lock Point arrived uncommitted is missed, never fired late.");
    }

    [Test]
    public void EvaluateCueTimingBlocksCadenceBeforeTheLockPoint()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.MarkChanged(600);

        var verdict = cuePlanner.EvaluateCueTiming(609, FourBeatRunwayRepertoire(), 603, 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.BlockedByCadence));
    }

    [Test]
    public void EvaluateCueTimingWaitsWhenCueMarkAlreadyCommitted()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.MarkChanged(609);

        var verdict = cuePlanner.EvaluateCueTiming(609, FourBeatRunwayRepertoire(), 603, 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Wait), "A committed Cue Mark is done, not paced.");
    }

    [Test]
    public void EvaluateCueTimingWaitsWhenBeatAlreadyIssuedCue()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.RecordCueIssued(603);

        var verdict = cuePlanner.EvaluateCueTiming(609, FourBeatRunwayRepertoire(), 603, 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Wait));
    }

    [Test]
    public void CanChangeAtEnforcesTheMinimumChangeCadenceFromTheLastCommit()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.MarkChanged(593);

        Assert.That(cuePlanner.CanChangeAt(608, minimumChangeCadenceBeats: 16), Is.False);
        Assert.That(cuePlanner.CanChangeAt(609, minimumChangeCadenceBeats: 16), Is.True);
    }

    private static TransitionRepertoire FourBeatRunwayRepertoire()
    {
        return TransitionRepertoire.FromRunwayAndTail(
            Repertoire.None,
            runwayBeats: 4,
            tailBeats: 4,
            TransitionShape.Dissolve,
            TransitionIntensity.High,
            defaultDurationSeconds: 4f);
    }

    /// <summary>Inside an active Phrase, given the countdown to its boundary (the Phrase start is back-derived).</summary>
    private static OnAirTimingInput TrackPhaseInput(int beat, int beatsUntilPhraseEnd, int phraseLengthBeats) =>
        new OnAirTimingInput(beat, beatsUntilPhraseEnd, phraseLengthBeats, -1, -1);

    /// <summary>Inside an active Phrase counting down to its end, with the next Phrase already announced.</summary>
    private static OnAirTimingInput PhraseWithNextInput(int beat, int beatsUntilPhraseEnd, int phraseLengthBeats, int nextPhraseLengthBeats) =>
        new OnAirTimingInput(beat, beatsUntilPhraseEnd, phraseLengthBeats, beatsUntilPhraseEnd, nextPhraseLengthBeats);

    /// <summary>No current Phrase, only the next one counting down to its own start with its own length.</summary>
    private static OnAirTimingInput UpcomingPhraseInput(int beat, int nextPhraseStartInBeats, int nextPhraseLengthBeats) =>
        new OnAirTimingInput(beat, -1, -1, nextPhraseStartInBeats, nextPhraseLengthBeats);

    private static Func<int, int, int> SelectFirstInteriorBoundary()
    {
        var callCount = 0;
        return (minInclusive, maxExclusive) =>
        {
            callCount++;
            return callCount == 1 && maxExclusive > minInclusive + 1 ? minInclusive + 1 : minInclusive;
        };
    }
}
