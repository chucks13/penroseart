using System;
using NUnit.Framework;

// Behaviour of the Director-owned CuePlanner (formerly the cue half of OnAirTiming.ReadFrame).
// CuePlanner owns its pass-local cue/cadence memory outright, so fixtures that used to inject a
// PassLocalTimingState seed it with RecordCueIssued/MarkChanged before the Plan call instead.
public sealed class OnAirTimingTests
{
    [Test]
    public void TrackPhaseFrameSelectsInteriorCueMarkBeforeMandatoryFinalCueMark()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 21, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(609));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(5));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void SameLengthPhraseUpdateReusesCueSheetWithoutRerollingCueMarks()
    {
        var randomCalls = 0;
        var cuePlanner = new CuePlanner((minInclusive, maxExclusive) =>
        {
            randomCalls++;
            return randomCalls == 1 && maxExclusive > minInclusive + 1 ? minInclusive + 1 : minInclusive;
        });

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        var callsAfterFirstSheet = randomCalls;

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 600, beatsToPhraseBoundary: 57, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(randomCalls, Is.EqualTo(callsAfterFirstSheet), "A same-length Phrase update should reuse Cue Mark offsets without rerolling.");
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(593));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
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
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        var callsAfterFirstSheet = randomCalls;

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 600, beatsToPhraseBoundary: 25, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(randomCalls, Is.GreaterThan(callsAfterFirstSheet), "A different Phrase length should build a new Cue Sheet.");
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(593));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(625));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(625));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void NewPhraseFrameAtMandatoryBoundaryKeepsPreviousCueMarkCueable()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 609, beatsToPhraseBoundary: 64, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void FiredMandatoryCueMarkImmediatelyPromotesPreplannedNextPhraseCueMark()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);
        var currentFrame = cuePlanner.Plan(
            UpcomingTrackPhaseInput(beat: 600, beatsToPhraseStart: 9, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(currentFrame.CueMarkBeat, Is.EqualTo(609), "Preplanning the next Phrase must not unset the loaded current Cue Mark.");

        cuePlanner.RecordCueIssued(605);
        cuePlanner.MarkChanged(609);
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 605, beatsToPhraseBoundary: 4, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(625));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void TrackPhaseDisappearanceAfterStructuralAnchorCoastsOnNextValidPhaseAnchor()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        var frame = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 594),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.IsCoasting, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Coast));
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(15));
        Assert.That(frame.Reanchored, Is.False);
    }

    [Test]
    public void FreshTrackPhaseAfterCoastingReanchorsToStructuralPhraseData()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 594),
            minimumChangeCadenceBeats: 16);

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 600, beatsToPhraseBoundary: 41, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.IsCoasting, Is.False);
        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
    }

    [Test]
    public void DifferentLengthFreshPhraseWindowReplacesCoastedCueSheet()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        var coasted = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 594),
            minimumChangeCadenceBeats: 16);
        Assert.That(coasted.CueMarkBeat, Is.EqualTo(609));

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 600, beatsToPhraseBoundary: 41, phraseLengthBeats: 48),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(593));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.IsCoasting, Is.False);
        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
    }

    [Test]
    public void TrackPhaseDisappearanceWithoutPriorStructuralAnchorUnlocks()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 588),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.False);
        Assert.That(frame.Phase.Confidence, Is.EqualTo(PhaseConfidence.Open));
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Unlocked));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(-1));
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsCueSheetAndMovesCursorBack()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
        Assert.That(frame.BeatRewoundToNewPass, Is.False);

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void SamePhraseWindowBeatRewindClearsPassLocalStateThatWouldBlockLoopPass()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        cuePlanner.RecordCueIssued(589);
        cuePlanner.MarkChanged(593);
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 589, beatsToPhraseBoundary: 52, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.ClearedPassLocalCueState, Is.True);
        Assert.That(frame.ClearedPassLocalCadenceState, Is.True);
        Assert.That(frame.PassLocalState.LastCueBeat, Is.Null);
        Assert.That(frame.PassLocalState.PreviousCueMarkBeat, Is.Null);
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsPassLocalStateThatCannotBlockCurrentPass()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        cuePlanner.RecordCueIssued(580);
        cuePlanner.MarkChanged(577);
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 589, beatsToPhraseBoundary: 52, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.ClearedPassLocalState, Is.False);
        Assert.That(frame.PassLocalState.LastCueBeat, Is.EqualTo(580));
        Assert.That(frame.PassLocalState.PreviousCueMarkBeat, Is.EqualTo(577));
    }

    [Test]
    public void SmallBeatBackstepIsJitterAndDoesNotResetSelectedBoundaryCursorOrPassLocalState()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 47, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));

        cuePlanner.RecordCueIssued(580);
        cuePlanner.MarkChanged(593);
        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 592, beatsToPhraseBoundary: 49, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.False);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
        Assert.That(frame.ClearedPassLocalState, Is.False);
        Assert.That(frame.PassLocalState.LastCueBeat, Is.EqualTo(580));
        Assert.That(frame.PassLocalState.PreviousCueMarkBeat, Is.EqualTo(593));
    }

    [Test]
    public void BeatOnlyFrameUsesPhaseClockGridWhenNoPhraseWindowIsAvailable()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            BeatOnlyInput(beat: 589),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.PhaseClockGrid));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
    }

    [Test]
    public void UnavailableFrameIsUnlockedWithoutFakeTarget()
    {
        var cuePlanner = new CuePlanner(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            OnAirTimingInput.Unavailable,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.False);
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Unlocked));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
    }

    private static OnAirTimingInput TrackPhaseInput(int beat, int beatsToPhraseBoundary, int phraseLengthBeats)
    {
        return new OnAirTimingInput(
            beat,
            totalBeats: -1,
            beatInBar: ((beat - 1) % 4) + 1,
            trackPhaseActive: 1,
            beatsUntilPhraseBoundary: beatsToPhraseBoundary,
            phraseLengthBeats: phraseLengthBeats);
    }

    private static OnAirTimingInput UpcomingTrackPhaseInput(int beat, int beatsToPhraseStart, int phraseLengthBeats)
    {
        return new OnAirTimingInput(
            beat,
            totalBeats: -1,
            beatInBar: ((beat - 1) % 4) + 1,
            trackPhaseActive: 0,
            beatsUntilPhraseBoundary: beatsToPhraseStart,
            phraseLengthBeats: phraseLengthBeats);
    }

    private static OnAirTimingInput BeatOnlyInput(int beat)
    {
        return new OnAirTimingInput(
            beat,
            totalBeats: -1,
            beatInBar: ((beat - 1) % 4) + 1,
            trackPhaseActive: 0,
            beatsUntilPhraseBoundary: -1,
            phraseLengthBeats: -1);
    }

    private static OnAirTimingInput TrackPhaseUnavailableInput(int beat)
    {
        return new OnAirTimingInput(
            beat,
            totalBeats: -1,
            beatInBar: ((beat - 1) % 4) + 1,
            trackPhaseActive: -1,
            beatsUntilPhraseBoundary: -1,
            phraseLengthBeats: -1);
    }

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
