using System;
using NUnit.Framework;

// Behaviour of the Director-owned CuePlanner (formerly the cue half of OnAirTiming.ReadFrame).
// CuePlanner owns its pass-local cue/cadence memory outright — fixtures seed it with
// RecordCueIssued/MarkChanged and observe it through the EvaluateCueTiming/CanChangeAt verdicts.
public sealed class CuePlannerTests
{
    [Test]
    public void TrackPhaseFrameSelectsInteriorCueMarkBeforeMandatoryFinalCueMark()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 21, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasGridAnchor, Is.True);
        Assert.That(frame.Grid.State, Is.EqualTo(GridSyncState.Locked));
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
    public void SameLengthPhraseTurnoverReusesTheCueSheetFromItsFirstMark()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        // Phrase (577, 64): the deterministic roll selects one interior Cue Mark at 593 (offset 16).
        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        // Mid-phrase the cursor advances past 593 toward the mandatory end 641.
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        // Same-length turnover to (641, 64): the sheet is reused without a reroll and the cursor
        // rewinds, so the new Phrase's first mark (offset 16 -> 657) is the target — not the
        // mandatory end 705 a stale cursor would report.
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 642, beatsToPhraseBoundary: 63, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(657));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.CueMark));
    }

    [Test]
    public void UpcomingPhraseFrameKeepsThePendingMandatoryBoundaryLoaded()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        // Track Phase counts down to the next Phrase while this one's mandatory boundary is still
        // pending: the loaded Cue Mark must not be unset by the look-ahead.
        var frame = cuePlanner.Plan(
            UpcomingTrackPhaseInput(beat: 600, beatsToPhraseStart: 9, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    [Test]
    public void FiredMandatoryBoundaryHandsOffToTheUpcomingPhraseCueMark()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        cuePlanner.RecordCueIssued(608);
        cuePlanner.MarkChanged(609);

        // The consumed sheet no longer drives: the look-ahead window builds the next Phrase's sheet
        // and its first mark past the fired boundary becomes the target.
        var frame = cuePlanner.Plan(
            UpcomingTrackPhaseInput(beat: 607, beatsToPhraseStart: 2, phraseLengthBeats: 64),
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

        Assert.That(frame.HasGridAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.IsCoasting, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Coast));
        Assert.That(frame.Grid.State, Is.EqualTo(GridSyncState.Coasting));
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

        Assert.That(frame.HasGridAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.IsCoasting, Is.False);
        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.Grid.State, Is.EqualTo(GridSyncState.Locked));
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

        Assert.That(frame.HasGridAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(593));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.IsCoasting, Is.False);
        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641));
    }

    [Test]
    public void BeatOnlyWithoutPriorAnchorCuesFromSyntheticPhrase()
    {
        // No Track Phase and no coastable anchor (a track that never carried phrase data): instead of
        // idling Unlocked, the planner fabricates a 64-beat rolling Phrase Window anchored on the grid one
        // and cues from it (ADR-0008). At beat 589 the window is 577..641 and the first selected mark is
        // the interior 593.
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 589),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasGridAnchor, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.PhraseWindow.LengthBeats, Is.EqualTo(64));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(4));
    }

    [Test]
    public void SyntheticPhraseWindowReusesWithinWindowThenRollsForwardWithFreshSubset()
    {
        var randomCalls = 0;
        var cuePlanner = new CueHarness((minInclusive, maxExclusive) =>
        {
            randomCalls++;
            return randomCalls == 1 && maxExclusive > minInclusive + 1 ? minInclusive + 1 : minInclusive;
        });

        var frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 589), minimumChangeCadenceBeats: 16);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577));
        var callsAfterFirstWindow = randomCalls;

        // Same 64-window, beat advanced: the synthetic sheet is reused, not re-rolled.
        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 600), minimumChangeCadenceBeats: 16);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(577), "Within the window the synthetic sheet is reused.");
        Assert.That(randomCalls, Is.EqualTo(callsAfterFirstWindow), "A within-window advance must not re-roll the subset.");

        // Past the window end: the window rolls forward by its length and re-rolls a fresh subset.
        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 650), minimumChangeCadenceBeats: 16);
        Assert.That(frame.PhraseWindow.StartBeat, Is.EqualTo(641), "Crossing the window end rolls the synthetic window forward 64 beats.");
        Assert.That(frame.PhraseWindow.LengthBeats, Is.EqualTo(64));
        Assert.That(randomCalls, Is.GreaterThan(callsAfterFirstWindow), "A roll re-rolls a fresh random subset.");
    }

    [Test]
    public void SyntheticLoopStrandsTailCueMarkSoItGridFallsBackThenResumesWhenReached()
    {
        // An 8-bar (32-beat) loop in beat-only fallback replays only the front of the 64-beat synthetic
        // window, so a Cue Mark selected in the tail is never reached: each loop rewinds the beat before
        // it lands. The planner detects the stranding (the countdown to the same mark climbs on a rewind),
        // cues on the 16-beat Grid meanwhile, then resumes the synthetic mark once the loop releases
        // and the beat carries through to it (ADR-0008).
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        // Window 577..641; the first selected interior mark sits at 609, inside the looped-away tail.
        var frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 600), minimumChangeCadenceBeats: 16);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(9));

        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 608), minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(1));

        // The loop sends the beat back to 577 (a 31-beat rewind) before 609 lands: stranded. Cue on the
        // grid instead — a 16-grid one, not the synthetic 609.
        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 577), minimumChangeCadenceBeats: 16);
        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.GridFallback));
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.CueMarkBeat, Is.Not.EqualTo(609));

        // The loop releases and the beat carries through to 609: the synthetic mark lands and cueing resumes.
        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 609), minimumChangeCadenceBeats: 16);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(0));
    }

    [Test]
    public void SyntheticShortLoopBelowChangeCadenceStillStrandsAndGridsThenResumes()
    {
        // The stranding detection has no rewind-size gate: ANY climb in the countdown for the same mark
        // means the beat moved backward before the mark landed, so a loop shorter than the change cadence
        // is caught just like a long one. Window 577..641, tail mark 609.
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 599), minimumChangeCadenceBeats: 16);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));

        // A 7-beat loop sends the beat back to 592 — below the 16-beat cadence, so NOT counted as a new pass
        // (BeatRewoundToNewPass stays false) — yet the climbing countdown still strands 609 and grids on the grid.
        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 592), minimumChangeCadenceBeats: 16);
        Assert.That(frame.BeatRewoundToNewPass, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.GridFallback));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
        Assert.That(frame.CueMarkBeat, Is.Not.EqualTo(609));

        // The beat carries through to the mark: synthetic cueing resumes.
        frame = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 609), minimumChangeCadenceBeats: 16);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(0));
    }

    [Test]
    public void SyntheticPhraseFallbackReanchorsWhenRealPhraseReturns()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var synthetic = cuePlanner.Plan(TrackPhaseUnavailableInput(beat: 589), minimumChangeCadenceBeats: 16);
        Assert.That(synthetic.Source, Is.EqualTo(TimingFrameSource.SyntheticPhrase));

        // A phrase-bearing track loads: the real path replaces the fabricated anchor and reports Reanchored.
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.Reanchored, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary).Or.EqualTo(TimingFrameSource.CueMark));
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
    public void SamePhraseWindowBeatRewindClearsCommitMemoryThatWouldBlockLoopPass()
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
        Assert.That(
            cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 593), beat: 589, minimumChangeCadenceBeats: 16),
            Is.EqualTo(CueTimingVerdict.Cue),
            "Cue/commit memory from the previous pass must not block the replayed mark.");
    }

    [Test]
    public void SamePhraseWindowBeatRewindKeepsCommitMemoryThatCannotBlockCurrentPass()
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
        Assert.That(cuePlanner.CanChangeAt(592, minimumChangeCadenceBeats: 16), Is.False,
            "The pre-rewind commit on 577 still binds cadence.");
        Assert.That(cuePlanner.CanChangeAt(593, minimumChangeCadenceBeats: 16), Is.True);
        Assert.That(
            cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 584), beat: 580, minimumChangeCadenceBeats: 16),
            Is.EqualTo(CueTimingVerdict.Wait),
            "The pre-rewind cue issued on 580 still binds.");
    }

    [Test]
    public void SmallBeatBackstepIsJitterAndDoesNotResetSelectedBoundaryCursorOrCommitMemory()
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
        Assert.That(cuePlanner.CanChangeAt(608, minimumChangeCadenceBeats: 16), Is.False,
            "The commit on 593 still binds cadence across a jitter backstep.");
        Assert.That(cuePlanner.CanChangeAt(609, minimumChangeCadenceBeats: 16), Is.True);
        Assert.That(
            cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 584), beat: 580, minimumChangeCadenceBeats: 16),
            Is.EqualTo(CueTimingVerdict.Wait),
            "The cue issued on 580 still binds across a jitter backstep.");
    }

    [Test]
    public void MissedUnconsumedCueMarkStaysCueableInsideTheLateCueWindow()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 47, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16,
            lateCueWindowBeats: 4);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(593),
            "An unconsumed Cue Mark stays the target while a late cue can still complete inside the window.");
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(-1));
    }

    [Test]
    public void MissedMandatoryBoundaryStaysCueableAcrossPhraseTurnoverInsideTheLateCueWindow()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 15, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16);

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 610, beatsToPhraseBoundary: 31, phraseLengthBeats: 32),
            minimumChangeCadenceBeats: 16,
            lateCueWindowBeats: 8);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(609),
            "The missed boundary Cue Mark stays cueable across the turnover while a Tail can still complete.");
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
        Assert.That(frame.BeatsUntilCueMark, Is.EqualTo(-1));
    }

    [Test]
    public void LoopRewindIntoAFiredCueMarksLateWindowDoesNotRepresentIt()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        cuePlanner.RecordCueIssued(589);
        cuePlanner.MarkChanged(593);
        cuePlanner.Plan(
            TrackPhaseInput(beat: 620, beatsToPhraseBoundary: 21, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        // The loop replays from 594 — inside the fired mark's late window. The pass-local commit
        // memory (593 < 594) survives the rewind, so the fired mark must not come back.
        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 594, beatsToPhraseBoundary: 47, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16,
            lateCueWindowBeats: 8);

        Assert.That(frame.BeatRewoundToNewPass, Is.True);
        Assert.That(cuePlanner.CanChangeAt(608, minimumChangeCadenceBeats: 16), Is.False, "The commit memory survives a rewind to after the fired mark.");
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641), "The fired mark must not be re-presented inside its late window.");
    }

    [Test]
    public void MissedCueMarkPastItsLateWindowIsSkipped()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));

        frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 602, beatsToPhraseBoundary: 39, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16,
            lateCueWindowBeats: 8);

        Assert.That(frame.CueMarkBeat, Is.EqualTo(641), "A mark whose cue window has fully closed is skipped, not held.");
    }

    [Test]
    public void CoastedGridAnchorStaysCueableInsideTheLateCueWindow()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        cuePlanner.Plan(
            TrackPhaseInput(beat: 588, beatsToPhraseBoundary: 53, phraseLengthBeats: 64),
            minimumChangeCadenceBeats: 16);

        var frame = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 593),
            minimumChangeCadenceBeats: 16,
            lateCueWindowBeats: 4);
        Assert.That(frame.IsCoasting, Is.True);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593), "The coasted anchor holds at its landing beat while a cue can still land.");

        frame = cuePlanner.Plan(
            TrackPhaseUnavailableInput(beat: 598),
            minimumChangeCadenceBeats: 16,
            lateCueWindowBeats: 4);
        Assert.That(frame.CueMarkBeat, Is.EqualTo(609), "Past the late window the coast rolls to the next cadence landing.");
    }

    [Test]
    public void BeatOnlyFrameUsesGridFallbackWhenNoPhraseWindowIsAvailable()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            BeatOnlyInput(beat: 589),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasGridAnchor, Is.True);
        Assert.That(frame.HasPhraseWindow, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.GridFallback));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(593));
    }

    [Test]
    public void UnavailableFrameIsUnlockedWithoutFakeTarget()
    {
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        var frame = cuePlanner.Plan(
            OnAirTimingInput.Unavailable,
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.HasGridAnchor, Is.False);
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.Unlocked));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(-1));
    }

    [Test]
    public void IrregularContradictedPhraseStillLandsTheMandatoryCueAtThePhraseBoundary()
    {
        // The load-bearing invariant: the phrase-end (mandatory) cue is phrase-driven and is NEVER gated
        // on the grid being Locked. A phrase whose length is not a multiple of 16 reads Contradicted (the
        // 16-grid is in dispute), but the boundary is feed bedrock and always known, so the cue must
        // still land at the phrase end. Grid state only refines interior placement.
        var cuePlanner = new CueHarness(SelectFirstInteriorBoundary());

        // A 56-beat phrase (56 % 16 = 8) cannot subdivide into whole 16-beat grids. It runs 585..641
        // with interior 16-beat marks at 585/601/617/633; beat 636 is past the last interior mark, so the
        // mandatory phrase-end cue at 641 is the only target left.
        var frame = cuePlanner.Plan(
            TrackPhaseInput(beat: 636, beatsToPhraseBoundary: 5, phraseLengthBeats: 56),
            minimumChangeCadenceBeats: 16);

        Assert.That(frame.Grid.State, Is.EqualTo(GridSyncState.Contradicted),
            "A 56-beat phrase is irregular, so the grid is reported Contradicted.");
        Assert.That(frame.HasPhraseWindow, Is.True);
        Assert.That(frame.PhraseWindow.EndBeat, Is.EqualTo(641));
        Assert.That(frame.CueMarkBeat, Is.EqualTo(641),
            "The mandatory phrase-end cue lands at the boundary despite the Contradicted grid.");
        Assert.That(frame.Source, Is.EqualTo(TimingFrameSource.TrackPhaseBoundary));
    }

    // Drives the CuePlanner through the same PHASE/PHRASE composition the Director performs: each frame
    // is read by a paired GridSync + PhraseTracker, then planned. Holds the stateful GridSync so the
    // held offset carries across a test's frame sequence, exactly as in the live Director.
    // Per-beat cue timing verdict (moved here from SyncedCueIntent.Evaluate): the planner answers
    // from its own pass-local memory, seeded through RecordCueIssued/MarkChanged like the Director.

    [Test]
    public void EvaluateCueTimingWaitsBeforeRunway()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 604, minimumChangeCadenceBeats: 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Wait));
    }

    [Test]
    public void EvaluateCueTimingCuesInsideRunway()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 605, minimumChangeCadenceBeats: 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Cue));
    }

    [Test]
    public void EvaluateCueTimingCuesLateInsideTailWhenImpactWasMissed()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 611, minimumChangeCadenceBeats: 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Cue));
    }

    [Test]
    public void EvaluateCueTimingWaitsPastCompleteBeat()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 614, minimumChangeCadenceBeats: 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Wait));
    }

    [Test]
    public void EvaluateCueTimingBlocksCadenceInsideRunway()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.MarkChanged(600);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 605, minimumChangeCadenceBeats: 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.BlockedByCadence));
    }

    [Test]
    public void EvaluateCueTimingWaitsWhenCueMarkAlreadyCommitted()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.MarkChanged(609);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 611, minimumChangeCadenceBeats: 16);

        Assert.That(verdict, Is.EqualTo(CueTimingVerdict.Wait), "A committed Cue Mark is done, not paced.");
    }

    [Test]
    public void EvaluateCueTimingWaitsWhenBeatAlreadyIssuedCue()
    {
        var cuePlanner = new CuePlanner((minInclusive, _) => minInclusive);
        cuePlanner.RecordCueIssued(605);

        var verdict = cuePlanner.EvaluateCueTiming(FourBeatRunwayPlan(cueMarkBeat: 609), beat: 605, minimumChangeCadenceBeats: 16);

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

    private static TransitionBeatPlan FourBeatRunwayPlan(int cueMarkBeat)
    {
        return TransitionBeatPlan.FromCueMark(
            cueMarkBeat,
            TransitionRepertoire.FromRunwayAndTail(
                Repertoire.None,
                runwayBeats: 4,
                tailBeats: 4,
                TransitionShape.Dissolve,
                TransitionIntensity.High,
                defaultDurationSeconds: 4f));
    }

    private sealed class CueHarness
    {
        private readonly CuePlanner cuePlanner;
        private readonly GridSync gridSync = new GridSync();

        public CueHarness(Func<int, int, int> randomRange)
        {
            cuePlanner = new CuePlanner(randomRange);
        }

        public TimingFrame Plan(OnAirTimingInput input, int minimumChangeCadenceBeats, int lateCueWindowBeats = 0)
        {
            var grid = gridSync.Read(input);
            var phrase = PhraseTracker.Read(
                grid,
                input.TrackPhaseActive,
                input.BeatsUntilPhraseBoundary,
                input.PhraseLengthBeats);
            return cuePlanner.Plan(input, grid, phrase, minimumChangeCadenceBeats, lateCueWindowBeats);
        }

        public void RecordCueIssued(int beat) => cuePlanner.RecordCueIssued(beat);

        public void MarkChanged(int beat) => cuePlanner.MarkChanged(beat);

        public CueTimingVerdict EvaluateCueTiming(TransitionBeatPlan beatPlan, int beat, int minimumChangeCadenceBeats) =>
            cuePlanner.EvaluateCueTiming(beatPlan, beat, minimumChangeCadenceBeats);

        public bool CanChangeAt(int beat, int minimumChangeCadenceBeats) =>
            cuePlanner.CanChangeAt(beat, minimumChangeCadenceBeats);
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
