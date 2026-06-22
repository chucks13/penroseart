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
            previousSelectedPhaseBoundary: null,
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
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsPlanAndMovesCursorBack()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(641));
        Assert.That(frame.BeatRewoundToNewPass, Is.False);

        frame = timing.ReadFrame(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            previousSelectedPhaseBoundary: null,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.SelectedPhaseBoundary, Is.EqualTo(593));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SelectedPhaseBoundary));
    }

    [Test]
    public void BeatOnlyFrameUsesPhaseClockGridWhenNoPhraseWindowIsAvailable()
    {
        var timing = new OnAirTiming(SelectFirstInteriorBoundary());

        var frame = timing.ReadFrame(
            BeatOnlyInput(beat: 589),
            previousSelectedPhaseBoundary: null,
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
            previousSelectedPhaseBoundary: null,
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

    private static OnAirTimingInput BeatOnlyInput(int beat)
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
