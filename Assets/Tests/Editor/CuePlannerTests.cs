using System;
using NUnit.Framework;

// Behaviour of the Director-owned CuePlanner (formerly the cue half of OnAirTiming.ReadFrame).
// CuePlanner owns its pass-local cue/cadence memory outright, so fixtures that used to inject a
// PassLocalTimingState seed it with RecordCueIssued/MarkChanged before the Plan call instead.
public sealed class CuePlannerTests
{
    [Test]
    public void TrackPhaseFrameSelectsInteriorCueMarkBeforeMandatoryFinalCueMark()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 21, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.True);
        Assert.That(frame.Phase.State, Is.EqualTo(PhaseLockState.Locked));
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
        var cuePlanner = new CueHarness((minInclusive, maxExclusive) =>
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
        var cuePlanner = new CueHarness((minInclusive, maxExclusive) =>
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
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        Assert.That(frame.Phase.State, Is.EqualTo(PhaseLockState.Coasting));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(15));
        Assert.That(frame.Reanchored, Is.False);
    }

    [Test]
    public void FreshTrackPhaseAfterCoastingReanchorsToStructuralPhraseData()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        Assert.That(frame.Phase.State, Is.EqualTo(PhaseLockState.Locked));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
    }

    [Test]
    public void DifferentLengthFreshPhraseWindowReplacesCoastedCueSheet()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 588),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.False);
        Assert.That(frame.Phase.State, Is.EqualTo(PhaseLockState.Coasting));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(-1));
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsCueSheetAndMovesCursorBack()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        Assert.That(frame.PassLocalState.LastCueBeat, Is.Null);
        Assert.That(frame.PassLocalState.PreviousCueMarkBeat, Is.Null);
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsPassLocalStateThatCannotBlockCurrentPass()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        Assert.That(frame.PassLocalState.LastCueBeat, Is.EqualTo(580));
        Assert.That(frame.PassLocalState.PreviousCueMarkBeat, Is.EqualTo(577));
    }

    [Test]
    public void SmallBeatBackstepIsJitterAndDoesNotResetSelectedBoundaryCursorOrPassLocalState()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        Assert.That(frame.PassLocalState.LastCueBeat, Is.EqualTo(580));
        Assert.That(frame.PassLocalState.PreviousCueMarkBeat, Is.EqualTo(593));
    }

    [Test]
    public void BeatOnlyFrameUsesPhaseClockGridWhenNoPhraseWindowIsAvailable()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

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
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            OnAirTimingInput.Unavailable,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasPhaseAnchor, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
    }

    [Test]
    public void IrregularContradictedPhraseStillLandsTheMandatoryCueAtThePhraseBoundary()
    {
        // The load-bearing invariant: the phrase-end (mandatory) cue is phrase-driven and is NEVER gated
        // on phase being Locked. A phrase whose length is not a multiple of 16 reads Contradicted (the
        // 16-grid is in dispute), but the boundary is feed bedrock and always known, so the cue must
        // still land at the phrase end. Phase state only refines interior placement.
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        // A 56-beat phrase (56 % 16 = 8) cannot subdivide into whole 16-beat phases. It runs 585..641
        // with interior 16-beat marks at 585/601/617/633; beat 636 is past the last interior mark, so the
        // mandatory phrase-end cue at 641 is the only target left.
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 636, beatsToPhraseBoundary: 5, phraseLengthBeats: 56),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.Phase.State, Is.EqualTo(PhaseLockState.Contradicted),
            "A 56-beat phrase is irregular, so the phase grid is reported Contradicted.");
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641),
            "The mandatory phrase-end cue lands at the boundary despite the Contradicted phase.");
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    // Drives the CuePlanner through the same PHASE/PHRASE composition the Director performs: each frame
    // is read by a paired PhaseLock + PhraseTracker, then planned. Holds the stateful PhaseLock so the
    // held offset carries across a test's frame sequence, exactly as in the live Director.
    private sealed class CueHarness
    {
        private readonly CuePlanner cuePlanner;
        private readonly PhaseLock phaseLock = new PhaseLock();

        public CueHarness(Func<int, int, int> randomRange)
        {
            cuePlanner = new CuePlanner(randomRange);
        }

        public TimingFrame Plan(OnAirTimingInput input, int minimumChangeCadenceBeats)
        {
            var phase = phaseLock.Read(input);
            var phrase = PhraseTracker.Read(
                phase,
                input.TrackPhaseActive,
                input.BeatsUntilPhraseBoundary,
                input.PhraseLengthBeats);
            return cuePlanner.Plan(input, phase, phrase, minimumChangeCadenceBeats);
        }

        public void RecordCueIssued(int beat) => cuePlanner.RecordCueIssued(beat);

        public void MarkChanged(int beat) => cuePlanner.MarkChanged(beat);
    }

    // These adapt the cue-test's "beats-to-boundary" parameterization onto the one shared
    // OnAirTimingInput projection (DjFrame). They are NOT a second projection: the field layout and the
    // Track-Phase tri-state live solely in DjFrame, so a change to OnAirTimingInput's shape can't drift
    // between the two test suites. A Phrase start is a downbeat, so DjFrame derives a grid-consistent
    // beat_in_bar (BeatInBarOnGrid) rather than the offset-0 form these fixtures used before.

    /// <summary>Inside an active Phrase, given the countdown to its boundary (the Phrase start is back-derived).</summary>
    private static OnAirTimingInput TrackPhaseInput(int beat, int beatsToPhraseBoundary, int phraseLengthBeats) =>
        DjFrame.InPhrase(beat, phraseStartBeat: beat + beatsToPhraseBoundary - phraseLengthBeats, phraseLengthBeats: phraseLengthBeats);

    /// <summary>Counting down to an upcoming Phrase that has not started yet.</summary>
    private static OnAirTimingInput UpcomingTrackPhaseInput(int beat, int beatsToPhraseStart, int phraseLengthBeats) =>
        DjFrame.BeforePhrase(beat, phraseStartBeat: beat + beatsToPhraseStart, upcomingLengthBeats: phraseLengthBeats);

    /// <summary>A Phrase section is present but idle (tri-state 0, no window) — routes through the grid fallback.</summary>
    private static OnAirTimingInput BeatOnlyInput(int beat) => DjFrame.PhraseIdle(beat);

    /// <summary>The Track-Phase feed is absent (tri-state -1) — the coast/unlock path, distinct from an idle Phrase.</summary>
    private static OnAirTimingInput TrackPhaseUnavailableInput(int beat) => DjFrame.BeatOnly(beat);

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
