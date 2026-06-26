using System;

/// <summary>
/// Director-owned cue planner: interprets one live rhythm snapshot into the Director-facing
/// <see cref="TimingFrame"/>. Owns Cue Sheet derivation, the Cue Mark cursor, change-cadence
/// memory, substantial beat-rewind handling, and Phase Anchor coasting.
///
/// Relocated from the former On-Air Timing core (slice 04b). On-Air Timing is now a pure
/// Phase/Phrase determiner (PhaseLock + PhraseTracker readings the Director composes); the cue
/// decisions live here, fed by plain data — never a reference back to the timing source. The
/// pass-local cue/cadence memory it used to round-trip through the Director is now owned outright.
/// </summary>
public sealed class CuePlanner
{
    /// <summary>
    /// Length in beats of the fabricated Phrase Window used for the beat-only fallback: four 16-beat
    /// Phases, so the random subset has real interior choices and the window ties to the 64-beat
    /// maximum Cue Mark gap (ADR-0008).
    /// </summary>
    private const int SyntheticPhraseLengthBeats = 64;

    private readonly Func<int, int, int> randomRange;

    private int lastBeat = -1;
    private bool hasPhaseAnchor;
    private int phaseAnchorLandingBeat = -1;
    private readonly CueSheetPlans cueSheetPlans = new CueSheetPlans();
    private readonly SyntheticCueGrid syntheticCueGrid = new SyntheticCueGrid();
    private TimingFrameSource lastSource = TimingFrameSource.Unlocked;

    // Pass-local cue/cadence memory, owned outright (no Director round-trip). lastChangeBeat is the
    // change cadence memory the Director also queries and marks (cue commits and manual ShowNow).
    private int lastCueBeat = -1;
    private int lastChangeBeat = int.MinValue;

    private readonly struct ResolvedTimingTarget
    {
        public readonly TimingFrameSource Source;
        public readonly int CueMarkBeat;
        public readonly bool HasPhraseWindow;
        public readonly PhraseWindow PhraseWindow;

        public ResolvedTimingTarget(
            TimingFrameSource source,
            int cueMarkBeat,
            bool hasPhraseWindow,
            PhraseWindow phraseWindow)
        {
            Source = source;
            CueMarkBeat = cueMarkBeat;
            HasPhraseWindow = hasPhraseWindow;
            PhraseWindow = phraseWindow;
        }

        public static ResolvedTimingTarget GridFallback(int cueMarkBeat)
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.GridFallback,
                cueMarkBeat,
                false,
                default);
        }
    }

    private sealed class CueSheetPlans
    {
        private readonly CueSheetCursor current = new CueSheetCursor();
        private bool hasUpcoming;
        private CueSheet upcoming;
        private int upcomingPhraseStartBeat;

        public CueSheetStatus Status(int currentCueMarkBeat)
        {
            return current.Status(currentCueMarkBeat);
        }

        public void ResetAll()
        {
            current.Reset();
            ClearUpcoming();
        }

        public void ResetCurrent()
        {
            current.Reset();
        }

        public void PlanUpcoming(
            int beat,
            PhraseWindow phraseWindow,
            int? consumedCueMarkBeat,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange)
        {
            if (hasUpcoming && upcoming.Matches(phraseWindow))
            {
                upcomingPhraseStartBeat = phraseWindow.StartBeat;
                return;
            }

            upcoming = CueSheet.Build(
                phraseWindow,
                beat,
                cueMarkBeat => ChangeCadence.CanChangeAt(cueMarkBeat, consumedCueMarkBeat, minimumChangeCadenceBeats),
                randomRange,
                includePhraseStart: true);
            upcomingPhraseStartBeat = phraseWindow.StartBeat;
            hasUpcoming = true;
        }

        public bool TryKeepUnconsumedMandatoryBoundaryAt(
            int beat,
            int? consumedCueMarkBeat,
            out int cueMarkBeat,
            out PhraseWindow phraseWindow)
        {
            return current.TryKeepUnconsumedMandatoryBoundaryAt(
                beat,
                consumedCueMarkBeat,
                out cueMarkBeat,
                out phraseWindow);
        }

        public bool TryGetActivePhraseWindow(int beat, out PhraseWindow phraseWindow)
        {
            return current.TryGetActivePhraseWindow(beat, out phraseWindow);
        }

        public bool HasUpcomingFor(PhraseWindow phraseWindow)
        {
            return hasUpcoming && upcoming.Matches(phraseWindow);
        }

        public bool TryGetUpcomingPhraseWindow(out PhraseWindow phraseWindow)
        {
            phraseWindow = default;
            return hasUpcoming
                && PhraseWindow.TryFromStartAndLength(upcomingPhraseStartBeat, upcoming.PhraseLengthBeats, out phraseWindow);
        }

        public void PromoteUpcoming()
        {
            if (hasUpcoming
                && PhraseWindow.TryFromStartAndLength(upcomingPhraseStartBeat, upcoming.PhraseLengthBeats, out var phraseWindow))
            {
                current.Replace(upcoming, phraseWindow);
            }
            else
            {
                current.Reset();
            }

            ClearUpcoming();
        }

        public void PromoteUpcoming(PhraseWindow phraseWindow)
        {
            if (hasUpcoming)
            {
                current.Replace(upcoming, phraseWindow);
            }
            else
            {
                current.Reset();
            }

            ClearUpcoming();
        }

        public TimingFrameSource ResolveCurrent(
            int beat,
            PhraseWindow phraseWindow,
            bool beatRewoundToNewPass,
            int? consumedCueMarkBeat,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange,
            out int cueMarkBeat)
        {
            if (current.NeedsSheet(phraseWindow))
            {
                BuildCurrent(
                    beat,
                    phraseWindow,
                    consumedCueMarkBeat,
                    minimumChangeCadenceBeats,
                    randomRange);
            }
            else
            {
                current.UpdatePhraseWindow(phraseWindow);
                if (beatRewoundToNewPass)
                {
                    current.RewindCursor();
                }
            }

            current.AdvanceTo(beat, consumedCueMarkBeat);
            if (current.IsConsumedThroughPhraseEnd(consumedCueMarkBeat) && hasUpcoming)
            {
                PromoteUpcoming();
                current.AdvanceTo(beat, consumedCueMarkBeat);
            }

            cueMarkBeat = current.CurrentCueMarkOr(phraseWindow.EndBeat);
            return cueMarkBeat == current.PhraseEndBeat
                ? TimingFrameSource.TrackPhaseBoundary
                : TimingFrameSource.CueMark;
        }

        private void BuildCurrent(
            int beat,
            PhraseWindow phraseWindow,
            int? consumedCueMarkBeat,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange)
        {
            var sheet = CueSheet.Build(
                phraseWindow,
                beat,
                cueMarkBeat => ChangeCadence.CanChangeAt(cueMarkBeat, consumedCueMarkBeat, minimumChangeCadenceBeats),
                randomRange);
            current.Replace(sheet, phraseWindow);
        }

        private void ClearUpcoming()
        {
            hasUpcoming = false;
            upcoming = default;
            upcomingPhraseStartBeat = -1;
        }
    }

    private sealed class CueSheetCursor
    {
        private CueSheet sheet;
        private int phraseStartBeat;
        private int index;

        public bool HasSheet { get; private set; }

        public int PhraseEndBeat => phraseStartBeat + sheet.PhraseLengthBeats;

        public void Reset()
        {
            HasSheet = false;
            sheet = default;
            phraseStartBeat = -1;
            index = 0;
        }

        public void Replace(CueSheet cueSheet, PhraseWindow phraseWindow)
        {
            HasSheet = true;
            sheet = cueSheet;
            phraseStartBeat = phraseWindow.StartBeat;
            index = 0;
        }

        public void UpdatePhraseWindow(PhraseWindow phraseWindow)
        {
            phraseStartBeat = phraseWindow.StartBeat;
        }

        public void RewindCursor()
        {
            index = 0;
        }

        public bool NeedsSheet(PhraseWindow phraseWindow)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return !HasSheet
                || cueMarkOffsets.Length == 0
                || !sheet.Matches(phraseWindow);
        }

        public bool TryGetActivePhraseWindow(int beat, out PhraseWindow phraseWindow)
        {
            phraseWindow = default;
            return HasSheet
                && PhraseEndBeat >= beat
                && PhraseWindow.TryFromStartAndLength(phraseStartBeat, sheet.PhraseLengthBeats, out phraseWindow);
        }

        public bool TryKeepUnconsumedMandatoryBoundaryAt(
            int beat,
            int? consumedCueMarkBeat,
            out int cueMarkBeat,
            out PhraseWindow phraseWindow)
        {
            cueMarkBeat = -1;
            phraseWindow = default;
            if (!HasSheet
                || IsConsumedThroughPhraseEnd(consumedCueMarkBeat)
                || !PhraseWindow.TryFromStartAndLength(phraseStartBeat, sheet.PhraseLengthBeats, out phraseWindow))
            {
                return false;
            }

            var originalIndex = index;
            AdvanceTo(beat, consumedCueMarkBeat);
            var currentCueMark = CurrentCueMarkOr(-1);
            // Keep the mark only when this beat IS the cursor's mark AND that mark is the phrase end —
            // i.e. beat == currentCueMark == PhraseEndBeat. Written as the chain so it doesn't read as
            // two independent checks (a foot-gun on this mandatory-cue path).
            if (currentCueMark != beat || beat != PhraseEndBeat)
            {
                index = originalIndex;
                return false;
            }

            cueMarkBeat = currentCueMark;
            return true;
        }

        public void AdvanceTo(int beat, int? consumedCueMarkBeat)
        {
            var cueMarkOffsets = CueMarkOffsets;
            while (index < cueMarkOffsets.Length - 1 && CueMarkAt(index) < beat)
            {
                index++;
            }

            while (index < cueMarkOffsets.Length - 1
                && consumedCueMarkBeat is { } firedCueMark
                && CueMarkAt(index) <= firedCueMark)
            {
                index++;
            }
        }

        public int CurrentCueMarkOr(int fallbackCueMark)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return cueMarkOffsets.Length > 0
                ? CueMarkAt(ClampIndex(index, cueMarkOffsets.Length))
                : fallbackCueMark;
        }

        public bool IsConsumedThroughPhraseEnd(int? consumedCueMarkBeat)
        {
            return consumedCueMarkBeat is { } firedCueMark && firedCueMark >= PhraseEndBeat;
        }

        public CueSheetStatus Status(int currentCueMarkBeat)
        {
            if (!HasSheet)
            {
                return CueSheetStatus.Empty;
            }

            var cueMarkOffsets = CueMarkOffsets;
            var currentCueMarkIndex = cueMarkOffsets.Length > 0 ? ClampIndex(index, cueMarkOffsets.Length) : -1;
            return new CueSheetStatus(
                true,
                phraseStartBeat,
                PhraseEndBeat,
                sheet.PhraseLengthBeats,
                currentCueMarkIndex,
                currentCueMarkBeat,
                (int[])cueMarkOffsets.Clone());
        }

        private int CueMarkAt(int cueMarkIndex)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return sheet.ToAbsoluteBeat(phraseStartBeat, cueMarkOffsets[ClampIndex(cueMarkIndex, cueMarkOffsets.Length)]);
        }

        private int[] CueMarkOffsets => sheet.CueMarkOffsets ?? Array.Empty<int>();
    }

    /// <summary>
    /// Fabricates a rolling Cue Sheet for the beat-only fallback (no Track Phase). It anchors a
    /// fixed-length synthetic Phrase Window on the grid one PhaseLock already holds, runs it through the
    /// real <see cref="CueSheet.Build"/> so the fallback inherits the same sparse-but-bounded Cue Mark
    /// character, and drives a real <see cref="CueSheetCursor"/> over it — so cursor advancement, the
    /// consumed-mark skip, beat-rewind handling, and Status all match the live-phrase path exactly rather
    /// than re-implementing them. It rolls the window forward by its length once consumed, re-rolling a
    /// fresh random subset only on that advance. Kept separate from <see cref="CueSheetPlans"/> so a
    /// fabricated sheet can never be reused for a real Phrase (ADR-0008).
    /// </summary>
    private sealed class SyntheticCueGrid
    {
        private readonly CueSheetCursor cursor = new CueSheetCursor();
        private int phraseStartBeat = -1;

        public void Reset()
        {
            cursor.Reset();
            phraseStartBeat = -1;
        }

        public bool TryResolve(
            int beat,
            int gridOffset,
            int lengthBeats,
            bool beatRewoundToNewPass,
            int? consumedCueMarkBeat,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange,
            out int cueMarkBeat,
            out PhraseWindow window,
            out CueSheetStatus status)
        {
            cueMarkBeat = -1;
            status = CueSheetStatus.Empty;

            // The most recent length-aligned grid one at or before this beat. Honours PhaseLock's held
            // offset so the window stays aligned through a transient mid-track Phrase dropout; for a track
            // that never had Phrase the offset is 0 and the one sits at beat ≡ 1 (mod 16).
            var startBeat = beat - PhaseGrid.Mod(beat - 1 - gridOffset, lengthBeats);
            if (!PhraseWindow.TryFromStartAndLength(startBeat, lengthBeats, out window))
            {
                return false;
            }

            if (!cursor.HasSheet || startBeat != phraseStartBeat)
            {
                // The window advanced (or this is the first frame): re-roll a fresh sparse subset. The
                // cadence predicate is built only here — not every frame — so steady-state fallback
                // allocates nothing on the timing path.
                var sheet = CueSheet.Build(
                    window,
                    beat,
                    candidateCueMarkBeat => ChangeCadence.CanChangeAt(candidateCueMarkBeat, consumedCueMarkBeat, minimumChangeCadenceBeats),
                    randomRange);
                cursor.Replace(sheet, window);
                phraseStartBeat = startBeat;
            }
            else if (beatRewoundToNewPass)
            {
                // A loop/rewind inside the same window re-arms the interior marks the looped section replays.
                cursor.RewindCursor();
            }

            cursor.AdvanceTo(beat, consumedCueMarkBeat);
            cueMarkBeat = cursor.CurrentCueMarkOr(window.EndBeat);
            status = cursor.Status(cueMarkBeat);
            return true;
        }
    }

    /// <summary>Creates a Cue Planner using Unity's runtime random source for boundary selection.</summary>
    public CuePlanner()
        : this((minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive))
    {
    }

    /// <summary>Creates a Cue Planner with an explicit random source for deterministic seam tests.</summary>
    public CuePlanner(Func<int, int, int> randomRange)
    {
        this.randomRange = randomRange ?? throw new ArgumentNullException(nameof(randomRange));
    }

    /// <summary>The last beat any change committed, or <see cref="int.MinValue"/> when none — the change cadence memory the Director queries.</summary>
    public int LastChangeBeat => lastChangeBeat;

    /// <summary>Records that a change committed on <paramref name="beat"/> (a synced cue impact or a manual show-now), feeding change cadence.</summary>
    public void MarkChanged(int beat)
    {
        lastChangeBeat = beat;
    }

    /// <summary>Records that a synced cue was issued on <paramref name="beat"/>, so the same pass does not re-cue it.</summary>
    public void RecordCueIssued(int beat)
    {
        lastCueBeat = beat;
    }

    /// <summary>Clears all remembered timing state so the next synced frame starts a new interpretation.</summary>
    public void Reset()
    {
        lastBeat = -1;
        hasPhaseAnchor = false;
        phaseAnchorLandingBeat = -1;
        cueSheetPlans.ResetAll();
        syntheticCueGrid.Reset();
        lastSource = TimingFrameSource.Unlocked;
    }

    /// <summary>
    /// Builds the current Timing Frame from one live rhythm snapshot. Owns the pass-local cue/cadence
    /// memory (no Director round-trip): it reads its own remembered cue/change beats, clears the stale
    /// ones when the beat rewound into a new pass, and remembers the corrected values for the next call.
    /// </summary>
    public TimingFrame Plan(
        OnAirTimingInput input,
        in PhaseReading phase,
        in PhraseTrackerReading phrase,
        int minimumChangeCadenceBeats)
    {
        if (minimumChangeCadenceBeats <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumChangeCadenceBeats), minimumChangeCadenceBeats, "Minimum cadence must be positive.");
        }

        var beatRewoundToNewPass = BeatRewoundToNewPass(lastBeat, input.Beat, minimumChangeCadenceBeats);

        var passLocalState = new PassLocalTimingState(
            lastCueBeat >= 0 ? lastCueBeat : (int?)null,
            lastChangeBeat == int.MinValue ? (int?)null : lastChangeBeat);
        var passState = BuildFramePassLocalState(passLocalState, input.Beat, beatRewoundToNewPass);
        lastCueBeat = passState.LastCueBeat ?? -1;
        lastChangeBeat = passState.PreviousCueMarkBeat ?? int.MinValue;

        if (input.Beat >= 1)
        {
            lastBeat = input.Beat;
        }

        if (TrackPhaseUnavailable(input) && input.Beat >= 1)
        {
            // A recently lost real anchor keeps coasting its known grid; a track that never had Track
            // Phase (no coastable anchor) cues from a fabricated rolling Phrase Window instead of idling,
            // so a phrase-less track still sequences until phrase data loads (ADR-0008).
            if (HasCoastablePhaseAnchor())
            {
                return BuildCoastingFrame(input, phase, beatRewoundToNewPass, passState, minimumChangeCadenceBeats);
            }

            return BuildSyntheticFrame(input, phase, beatRewoundToNewPass, passState, minimumChangeCadenceBeats);
        }

        // The gate is "do we have a usable grid position to plan interior cues against." A live phrase
        // always yields Position >= 1, so the mandatory phrase-end cue is never suppressed here. The
        // beat-only case (no Track Phase) is handled above, so this branch only sees a present Track
        // Phase channel — an active window or the idle tri-state-0 grid fallback.
        if (phase.Position >= 1 && input.Beat >= 1)
        {
            var previousSource = lastSource;
            var target = ResolveCueMark(
                input.Beat,
                phrase,
                phase,
                beatRewoundToNewPass,
                passState.PreviousCueMarkBeat,
                minimumChangeCadenceBeats);
            var reanchored = ReanchoredFrom(previousSource, target.Source, hasPhaseAnchor);
            return BuildAnchoredFrame(
                input,
                phase,
                target,
                beatRewoundToNewPass,
                passState,
                reanchored);
        }

        if (hasPhaseAnchor && input.Beat >= 1)
        {
            return BuildCoastingFrame(input, phase, beatRewoundToNewPass, passState, minimumChangeCadenceBeats);
        }

        return BuildUnlockedFrame(input, phase, beatRewoundToNewPass, passState);
    }

    private TimingFrame BuildCoastingFrame(
        OnAirTimingInput input,
        in PhaseReading phase,
        bool beatRewoundToNewPass,
        PassLocalTimingState passState,
        int minimumChangeCadenceBeats)
    {
        CoastPhaseAnchor(input.Beat, minimumChangeCadenceBeats);
        lastSource = TimingFrameSource.Coast;
        return CreateFrame(
            input,
            phase,
            true,
            phaseAnchorLandingBeat,
            false,
            default,
            TimingFrameSource.Coast,
            beatRewoundToNewPass,
            passState,
            false,
            CueSheetStatus.Empty);
    }

    private TimingFrame BuildAnchoredFrame(
        OnAirTimingInput input,
        in PhaseReading phase,
        ResolvedTimingTarget target,
        bool beatRewoundToNewPass,
        PassLocalTimingState passState,
        bool reanchored)
    {
        hasPhaseAnchor = true;
        phaseAnchorLandingBeat = target.CueMarkBeat;
        lastSource = target.Source;
        return CreateFrame(
            input,
            phase,
            true,
            phaseAnchorLandingBeat,
            target.HasPhraseWindow,
            target.PhraseWindow,
            target.Source,
            beatRewoundToNewPass,
            passState,
            reanchored,
            cueSheetPlans.Status(phaseAnchorLandingBeat));
    }

    private TimingFrame BuildUnlockedFrame(
        OnAirTimingInput input,
        in PhaseReading phase,
        bool beatRewoundToNewPass,
        PassLocalTimingState passState)
    {
        hasPhaseAnchor = false;
        phaseAnchorLandingBeat = -1;
        lastSource = TimingFrameSource.Unlocked;
        return CreateFrame(
            input,
            phase,
            false,
            -1,
            false,
            default,
            TimingFrameSource.Unlocked,
            beatRewoundToNewPass,
            passState,
            false,
            CueSheetStatus.Empty);
    }

    private TimingFrame BuildSyntheticFrame(
        OnAirTimingInput input,
        in PhaseReading phase,
        bool beatRewoundToNewPass,
        PassLocalTimingState passState,
        int minimumChangeCadenceBeats)
    {
        // Clear the real Cue Sheet lifecycle once on entering the fallback so a stale real sheet can't be
        // reused (CueSheet.Matches is length-only) when a Phrase track later loads. Edge-triggered: while
        // the synthetic grid drives nothing rebuilds the real plans, so re-clearing every frame is waste.
        if (lastSource != TimingFrameSource.SyntheticPhrase)
        {
            cueSheetPlans.ResetAll();
        }

        if (!syntheticCueGrid.TryResolve(
                input.Beat,
                phase.Offset,
                SyntheticPhraseLengthBeats,
                beatRewoundToNewPass,
                passState.PreviousCueMarkBeat,
                minimumChangeCadenceBeats,
                randomRange,
                out var resolvedCueMarkBeat,
                out var phraseWindow,
                out var cueSheetStatus))
        {
            // Too early in the track to anchor a full synthetic window (its start would precede beat 1).
            return BuildUnlockedFrame(input, phase, beatRewoundToNewPass, passState);
        }

        hasPhaseAnchor = true;
        phaseAnchorLandingBeat = resolvedCueMarkBeat;
        lastSource = TimingFrameSource.SyntheticPhrase;
        return CreateFrame(
            input,
            phase,
            true,
            resolvedCueMarkBeat,
            true,
            phraseWindow,
            TimingFrameSource.SyntheticPhrase,
            beatRewoundToNewPass,
            passState,
            false,
            cueSheetStatus);
    }

    private static TimingFrame CreateFrame(
        OnAirTimingInput input,
        in PhaseReading phase,
        bool hasPhaseAnchor,
        int cueMarkBeat,
        bool hasPhraseWindow,
        PhraseWindow phraseWindow,
        TimingFrameSource source,
        bool beatRewoundToNewPass,
        PassLocalTimingState passState,
        bool reanchored,
        CueSheetStatus cueSheet)
    {
        return new TimingFrame(
            input,
            phase,
            hasPhaseAnchor,
            cueMarkBeat,
            hasPhraseWindow,
            phraseWindow,
            source,
            beatRewoundToNewPass,
            passState,
            reanchored,
            cueSheet);
    }

    /// <summary>
    /// Re-aims a prior cue frame at the current beat for the Director's missed-zero-runway retarget: the
    /// cue's phase, anchor, mark, window, source, and sheet are preserved while the beat-relative fields
    /// (the current beat and the countdown to the mark) advance to <paramref name="currentFrame"/>.
    /// TimingFrame assembly stays here so the Director never hand-builds one.
    /// </summary>
    public static TimingFrame Retarget(in TimingFrame cueFrame, in TimingFrame currentFrame)
    {
        var input = new OnAirTimingInput(
            currentFrame.CurrentBeat,
            currentFrame.Input.BeatInBar,
            currentFrame.Input.TrackPhaseActive,
            cueFrame.CueMarkBeat - currentFrame.CurrentBeat,
            cueFrame.Input.PhraseLengthBeats,
            currentFrame.Input.TrackOrdinal);
        return CreateFrame(
            input,
            cueFrame.Phase,
            cueFrame.HasPhaseAnchor,
            cueFrame.CueMarkBeat,
            cueFrame.HasPhraseWindow,
            cueFrame.PhraseWindow,
            cueFrame.Source,
            currentFrame.BeatRewoundToNewPass,
            currentFrame.PassLocalState,
            currentFrame.Reanchored,
            cueFrame.CueSheet);
    }

    private static bool ReanchoredFrom(TimingFrameSource previousSource, TimingFrameSource source, bool hadPhaseAnchor)
    {
        return hadPhaseAnchor
            && (source == TimingFrameSource.TrackPhaseBoundary || source == TimingFrameSource.CueMark)
            && (previousSource == TimingFrameSource.Coast
                || previousSource == TimingFrameSource.GridFallback
                || previousSource == TimingFrameSource.SyntheticPhrase);
    }

    private bool HasCoastablePhaseAnchor()
    {
        return hasPhaseAnchor
            && (lastSource == TimingFrameSource.CueMark
                || lastSource == TimingFrameSource.TrackPhaseBoundary
                || lastSource == TimingFrameSource.Coast);
    }

    private static bool TrackPhaseUnavailable(OnAirTimingInput input)
    {
        return input.TrackPhaseActive < 0;
    }

    private void CoastPhaseAnchor(int beat, int minimumChangeCadenceBeats)
    {
        while (beat >= phaseAnchorLandingBeat)
        {
            phaseAnchorLandingBeat += minimumChangeCadenceBeats;
        }
    }

    private ResolvedTimingTarget ResolveCueMark(
        int beat,
        in PhraseTrackerReading phrase,
        in PhaseReading phase,
        bool beatRewoundToNewPass,
        int? consumedCueMarkBeat,
        int minimumChangeCadenceBeats)
    {
        if (phrase.HasLookAhead
            && PhraseWindow.TryFromUpcomingTrackPhase(
                beat,
                phrase.BeatsUntilNextPhrase,
                phrase.PredictedUpcomingLengthBeats,
                out var upcomingPhraseWindow))
        {
            cueSheetPlans.PlanUpcoming(
                beat,
                upcomingPhraseWindow,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats,
                randomRange);
        }

        if (cueSheetPlans.TryKeepUnconsumedMandatoryBoundaryAt(
            beat,
            consumedCueMarkBeat,
            out var cueMarkBeat,
            out var phraseWindow))
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.TrackPhaseBoundary,
                cueMarkBeat,
                true,
                phraseWindow);
        }

        if (phrase.PhraseLengthBeats >= 1
            && PhraseWindow.TryFromTrackPhase(
                beat,
                phrase.BeatsUntilNextPhrase,
                phrase.PhraseLengthBeats,
                out phraseWindow))
        {
            if (cueSheetPlans.HasUpcomingFor(phraseWindow))
            {
                cueSheetPlans.PromoteUpcoming(phraseWindow);
            }

            return ResolveCueMarkFromSheet(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats);
        }

        if (cueSheetPlans.TryGetActivePhraseWindow(beat, out phraseWindow))
        {
            return ResolveCueMarkFromSheet(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats);
        }

        if (cueSheetPlans.TryGetUpcomingPhraseWindow(out phraseWindow))
        {
            cueSheetPlans.PromoteUpcoming();
            return ResolveCueMarkFromSheet(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedCueMarkBeat,
                minimumChangeCadenceBeats);
        }

        cueSheetPlans.ResetCurrent();
        return ResolvedTimingTarget.GridFallback(GetLandingBeatFromPhasePosition(beat, phase.Position));
    }

    private ResolvedTimingTarget ResolveCueMarkFromSheet(
        int beat,
        PhraseWindow phraseWindow,
        bool beatRewoundToNewPass,
        int? consumedCueMarkBeat,
        int minimumChangeCadenceBeats)
    {
        var source = cueSheetPlans.ResolveCurrent(
            beat,
            phraseWindow,
            beatRewoundToNewPass,
            consumedCueMarkBeat,
            minimumChangeCadenceBeats,
            randomRange,
            out var cueMarkBeat);
        return new ResolvedTimingTarget(source, cueMarkBeat, true, phraseWindow);
    }

    private static int GetLandingBeatFromPhasePosition(int beat, int phasePosition)
    {
        var beatsUntilLanding = PhaseGrid.PhraseBeats - phasePosition + 1;
        return beat + beatsUntilLanding;
    }

    private static bool BeatRewoundToNewPass(int previousBeat, int beat, int minimumChangeCadenceBeats)
    {
        return previousBeat >= 1
            && beat >= 1
            && beat < previousBeat
            && previousBeat - beat + 1 >= minimumChangeCadenceBeats;
    }

    private static PassLocalTimingState BuildFramePassLocalState(
        PassLocalTimingState passLocalState,
        int beat,
        bool beatRewoundToNewPass)
    {
        if (!beatRewoundToNewPass || beat < 1)
        {
            return passLocalState;
        }

        var lastCueBeat = passLocalState.LastCueBeat is { } cueBeat && cueBeat >= beat
            ? (int?)null
            : passLocalState.LastCueBeat;
        var previousCueMarkBeat = passLocalState.PreviousCueMarkBeat is { } cueMarkBeat && cueMarkBeat >= beat
            ? (int?)null
            : passLocalState.PreviousCueMarkBeat;
        return new PassLocalTimingState(lastCueBeat, previousCueMarkBeat);
    }

    private static int ClampIndex(int index, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        if (index < 0)
        {
            return 0;
        }

        return index >= length ? length - 1 : index;
    }
}
