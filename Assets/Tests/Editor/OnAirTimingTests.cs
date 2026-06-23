using System;
using NUnit.Framework;

public sealed class OnAirTimingTests
{
    [Test]
    public void TrackPhaseFrameSelectsInteriorPhaseBoundaryBeforeMandatoryPhraseBoundary()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 21, phraseLengthBeats: 32),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(609));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));
        Assert.That(frame.BeatsUntilSelectedPhaseBoundary, Is.EqualTo(5));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SelectedPhaseBoundary));

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void NewPhraseFrameAtMandatoryBoundaryKeepsPreviousPhraseBoundaryCueable()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 609, beatsToPhraseBoundary: 64, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void FiredMandatoryBoundaryImmediatelyPromotesPreplannedNextPhraseTarget()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        var currentFrame = timing.ReadFrame(
            UpcomingTrackPhaseInput(beat: 600, beatsToPhraseStart: 9, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        Assert.That(currentFrame.SelectedPhaseBoundary, Is.EqualTo(609), "Preplanning the next Phrase must not unset the loaded current boundary.");

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 605, beatsToPhraseBoundary: 4, phraseLengthBeats: 32),
            new PassLocalTimingState(lastCueBeat: 605, previousSelectedPhaseBoundary: 609),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(625));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SelectedPhaseBoundary));
    }

    [Test]
    public void TrackPhaseDisappearanceAfterStructuralAnchorCoastsOnNextValidPhaseAnchor()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        var frame = timing.ReadFrame(
            TrackPhaseUnavailableInput(beat: 594),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.IsCoasting, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Coast));
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilSelectedPhaseBoundary, Is.EqualTo(15));
        Assert.That(frame.Reanchored, Is.False);
    }

    [Test]
    public void FreshTrackPhaseAfterCoastingReanchorsToStructuralPhraseData()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        timing.ReadFrame(
            TrackPhaseUnavailableInput(beat: 594),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 600, beatsToPhraseBoundary: 41, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.IsCoasting, Is.False);
        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Structural));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(641));
    }

    [Test]
    public void ContradictoryFreshPhraseWindowReplacesCoastedAnchor()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        var coasted = timing.ReadFrame(
            TrackPhaseUnavailableInput(beat: 594),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        Assert.That(coasted.SelectedPhaseBoundary, Is.EqualTo(609));

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 600, beatsToPhraseBoundary: 57, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(593));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(657));
        Assert.That(frame.IsCoasting, Is.False);
        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(657));
    }

    [Test]
    public void TrackPhaseDisappearanceWithoutPriorStructuralAnchorUnlocks()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            TrackPhaseUnavailableInput(beat: 588),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.False);
        Assert.That(frame.Phase.Confidence, Is.EqualTo(PhaseConfidence.Open));
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Unlocked));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(-1));
        Assert.That(frame.BeatsUntilSelectedPhaseBoundary, Is.EqualTo(-1));
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsPlanAndMovesCursorBack()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(641));
        Assert.That(frame.BeatRewoundToNewPass, Is.False);

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SelectedPhaseBoundary));
    }

    [Test]
    public void SamePhraseWindowBeatRewindClearsPassLocalStateThatWouldBlockLoopPass()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        timing.ReadFrame(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 589, beatsToPhraseBoundary: 52, phraseLengthBeats: 64),
            new PassLocalTimingState(lastCueBeat: 589, previousSelectedPhaseBoundary: 593),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));
        Assert.That(frame.ClearedPassLocalCueState, Is.True);
        Assert.That(frame.ClearedPassLocalCadenceState, Is.True);
        Assert.That(frame.PassLocalState.LastCueBeat, Is.Null);
        Assert.That(frame.PassLocalState.PreviousSelectedPhaseBoundary, Is.Null);
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsPassLocalStateThatCannotBlockCurrentPass()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        timing.ReadFrame(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 589, beatsToPhraseBoundary: 52, phraseLengthBeats: 64),
            new PassLocalTimingState(lastCueBeat: 580, previousSelectedPhaseBoundary: 577),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));
        Assert.That(frame.ClearedPassLocalState, Is.False);
        Assert.That(frame.PassLocalState.LastCueBeat, Is.EqualTo(580));
        Assert.That(frame.PassLocalState.PreviousSelectedPhaseBoundary, Is.EqualTo(577));
    }

    [Test]
    public void SmallBeatBackstepIsJitterAndDoesNotResetSelectedBoundaryCursorOrPassLocalState()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 47, phraseLengthBeats: 64),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(641));

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 592, beatsToPhraseBoundary: 49, phraseLengthBeats: 64),
            new PassLocalTimingState(lastCueBeat: 580, previousSelectedPhaseBoundary: 593),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.False);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(641));
        Assert.That(frame.ClearedPassLocalState, Is.False);
        Assert.That(frame.PassLocalState.LastCueBeat, Is.EqualTo(580));
        Assert.That(frame.PassLocalState.PreviousSelectedPhaseBoundary, Is.EqualTo(593));
    }

    [Test]
    public void BeatOnlyFrameUsesPhaseClockGridWhenNoPhraseWindowIsAvailable()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            BeatOnlyInput(beat: 589),
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.PhaseClockGrid));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));
    }

    [Test]
    public void UnavailableFrameIsUnlockedWithoutFakeTarget()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            OnAirTimingInput.Unavailable,
            PassLocalTimingState.Empty,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.False);
        Assert.That(frame.PhaseAnchorConfidence, Is.EqualTo(PhaseConfidence.Unlocked));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(-1));
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
