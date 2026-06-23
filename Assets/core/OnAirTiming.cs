using System;

/// <summary>Domain-facing source for the Director's current On-Air Timing Frame.</summary>
public enum TimingFrameSource
{
    /// <summary>No usable live timing target is available.</summary>
    Unlocked,

    /// <summary>The next target is inferred from the 16-beat Phase grid.</summary>
    PhaseClockGrid,

    /// <summary>The next target is an interior Cue Mark selected inside the current Phrase Window.</summary>
    CueMark,

    /// <summary>The next target is a mandatory structural Track Phase boundary.</summary>
    TrackPhaseBoundary,

    /// <summary>The target is coasting from the last known Phase Anchor.</summary>
    Coast,
}

/// <summary>The live rhythm snapshot On-Air Timing needs to interpret the current synced frame.</summary>
public readonly struct OnAirTimingInput
{
    public static OnAirTimingInput Unavailable { get; } = new OnAirTimingInput(-1, -1, -1, -1, -1, -1);

    /// <summary>Focused on-air absolute beat count, or -1 when unavailable.</summary>
    public readonly int Beat;

    /// <summary>Focused on-air track length in beats, or -1 when unavailable.</summary>
    public readonly int TotalBeats;

    /// <summary>Focused on-air 1-based beat label inside the current bar, or -1 when unavailable.</summary>
    public readonly int BeatInBar;

    /// <summary>Track Phase active tri-state: 1 active, 0 inactive-but-present, -1 unavailable.</summary>
    public readonly int TrackPhaseActive;

    /// <summary>Whole beats until the active Track Phase boundary, upcoming Track Phase start, or -1 when unavailable.</summary>
    public readonly int BeatsUntilPhraseBoundary;

    /// <summary>Total length of the active or upcoming Phrase Window in beats, or -1 when unavailable.</summary>
    public readonly int PhraseLengthBeats;

    public OnAirTimingInput(
        int beat,
        int totalBeats,
        int beatInBar,
        int trackPhaseActive,
        int beatsUntilPhraseBoundary,
        int phraseLengthBeats)
    {
        Beat = beat;
        TotalBeats = totalBeats;
        BeatInBar = beatInBar;
        TrackPhaseActive = trackPhaseActive;
        BeatsUntilPhraseBoundary = beatsUntilPhraseBoundary;
        PhraseLengthBeats = phraseLengthBeats;
    }

    /// <summary>Captures the nullable BeatManager rhythm queries without exposing raw Track Phase interpretation to callers.</summary>
    public static OnAirTimingInput From(BeatManager beatManager)
    {
        if (beatManager == null)
        {
            return Unavailable;
        }

        var phase = beatManager.Phase;
        return new OnAirTimingInput(
            beatManager.Beat ?? -1,
            beatManager.TotalBeats ?? -1,
            beatManager.BeatInBar ?? -1,
            phase is { inPhase: true } ? 1 : phase is { } ? 0 : -1,
            phase?.beatsUntilNext ?? -1,
            phase?.lengthBeats ?? -1);
    }

    /// <summary>Converts the snapshot into the low-level PhaseClock input kept behind On-Air Timing.</summary>
    public PhaseInput ToPhaseInput()
    {
        return new PhaseInput(
            Beat,
            TotalBeats,
            BeatInBar,
            TrackPhaseActive,
            BeatsUntilPhraseBoundary,
            PhraseLengthBeats);
    }
}

/// <summary>Director cue/cadence memory that is valid only within one forward pass through on-air timing.</summary>
public readonly struct PassLocalTimingState
{
    /// <summary>No prior cue or cadence memory is active for the current pass.</summary>
    public static PassLocalTimingState Empty { get; } = new PassLocalTimingState(null, null);

    /// <summary>The last absolute beat that issued a synced cue, or null when no cue from this pass should block.</summary>
    public readonly int? LastCueBeat;

    /// <summary>The last Cue Mark used for cadence, or null when this pass has no prior boundary.</summary>
    public readonly int? PreviousCueMarkBeat;

    /// <summary>Creates pass-local cue/cadence memory from the Director's current synced state.</summary>
    public PassLocalTimingState(int? lastCueBeat, int? previousCueMarkBeat)
    {
        LastCueBeat = lastCueBeat;
        PreviousCueMarkBeat = previousCueMarkBeat;
    }
}

/// <summary>Director-facing interpretation of one synced on-air frame.</summary>
public readonly struct TimingFrame
{
    public static TimingFrame Unavailable { get; } = new TimingFrame(
        OnAirTimingInput.Unavailable,
        PhaseReading.Unavailable,
        false,
        PhaseConfidence.Unlocked,
        -1,
        false,
        default,
        TimingFrameSource.Unlocked,
        false,
        PassLocalTimingState.Empty,
        false,
        false,
        false);

    /// <summary>The live rhythm snapshot that produced this frame.</summary>
    public readonly OnAirTimingInput Input;

    /// <summary>Current on-air beat, or -1 when unavailable.</summary>
    public readonly int CurrentBeat;

    /// <summary>The 16-beat Phase reading for this frame.</summary>
    public readonly PhaseReading Phase;

    /// <summary>Whether this frame has a Phase Anchor the Director can target.</summary>
    public readonly bool HasPhaseAnchor;

    /// <summary>Confidence for the current Phase Anchor.</summary>
    public readonly PhaseConfidence PhaseAnchorConfidence;

    /// <summary>Cue Mark where the Director should land its next Impact Point.</summary>
    public readonly int CueMarkBeat;

    /// <summary>Absolute beat where the current Phase Anchor lands, or -1 when unlocked.</summary>
    public int PhaseAnchorLandingBeat => CueMarkBeat;

    /// <summary>Beats until the Cue Mark, or -1 when unlocked.</summary>
    public readonly int BeatsUntilCueMark;

    /// <summary>Whether the frame includes a Track Phase-derived Phrase Window.</summary>
    public readonly bool HasPhraseWindow;

    /// <summary>Track Phase-derived Phrase Window, valid only when <see cref="HasPhraseWindow"/> is true.</summary>
    public readonly PhraseWindow PhraseWindow;

    /// <summary>Stable domain-facing source for the selected timing target.</summary>
    public readonly TimingFrameSource Source;

    /// <summary>True when the current beat substantially rewound into a new pass.</summary>
    public readonly bool BeatRewoundToNewPass;

    /// <summary>Cue/cadence memory after On-Air Timing removes stale state from earlier loop passes.</summary>
    public readonly PassLocalTimingState PassLocalState;

    /// <summary>True when a rewind made the previous cue beat stale for this pass.</summary>
    public readonly bool ClearedPassLocalCueState;

    /// <summary>True when a rewind made the previous cadence boundary stale for this pass.</summary>
    public readonly bool ClearedPassLocalCadenceState;

    /// <summary>True when any pass-local cue/cadence state was cleared for this timing frame.</summary>
    public bool ClearedPassLocalState => ClearedPassLocalCueState || ClearedPassLocalCadenceState;

    /// <summary>True when this frame is continuing from the last known Phase Anchor.</summary>
    public bool IsCoasting => Source == TimingFrameSource.Coast;

    /// <summary>True when fresh structural timing replaced a coasted or weaker anchor.</summary>
    public readonly bool Reanchored;

    public TimingFrame(
        OnAirTimingInput input,
        PhaseReading phase,
        bool hasPhaseAnchor,
        PhaseConfidence phaseAnchorConfidence,
        int cueMarkBeat,
        bool hasPhraseWindow,
        PhraseWindow phraseWindow,
        TimingFrameSource source,
        bool beatRewoundToNewPass,
        PassLocalTimingState passLocalState,
        bool clearedPassLocalCueState,
        bool clearedPassLocalCadenceState,
        bool reanchored)
    {
        Input = input;
        CurrentBeat = input.Beat;
        Phase = phase;
        HasPhaseAnchor = hasPhaseAnchor;
        PhaseAnchorConfidence = phaseAnchorConfidence;
        CueMarkBeat = cueMarkBeat;
        BeatsUntilCueMark = hasPhaseAnchor && input.Beat >= 1 ? cueMarkBeat - input.Beat : -1;
        HasPhraseWindow = hasPhraseWindow;
        PhraseWindow = phraseWindow;
        Source = source;
        BeatRewoundToNewPass = beatRewoundToNewPass;
        PassLocalState = passLocalState;
        ClearedPassLocalCueState = clearedPassLocalCueState;
        ClearedPassLocalCadenceState = clearedPassLocalCadenceState;
        Reanchored = reanchored;
    }
}

/// <summary>
/// Interprets live RaveSystem timing into one Director-facing Timing Frame.
/// Owns PhaseInput construction, Phase/Phrase interpretation, Cue Mark cursor state,
/// substantial beat rewind handling, and Phase Anchor coasting.
/// </summary>
public sealed class OnAirTiming
{
    private readonly Func<int, int, int> randomRange;

    private int lastBeat = -1;
    private bool hasPhaseAnchor;
    private PhaseConfidence phaseAnchorConfidence = PhaseConfidence.Unlocked;
    private int phaseAnchorLandingBeat = -1;
    private readonly CueSheetPlans cueSheetPlans = new CueSheetPlans();
    private TimingFrameSource lastSource = TimingFrameSource.Unlocked;

    private readonly struct FramePassLocalState
    {
        public readonly PassLocalTimingState State;
        public readonly bool ClearedCueState;
        public readonly bool ClearedCadenceState;

        public FramePassLocalState(PassLocalTimingState state, PassLocalTimingState originalState)
        {
            State = state;
            ClearedCueState = state.LastCueBeat != originalState.LastCueBeat;
            ClearedCadenceState = state.PreviousCueMarkBeat != originalState.PreviousCueMarkBeat;
        }

        public int? ConsumedCueMarkBeat => State.PreviousCueMarkBeat;
    }

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

        public static ResolvedTimingTarget PhaseClockGrid(int cueMarkBeat)
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.PhaseClockGrid,
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
            if (currentCueMark != beat || currentCueMark != PhraseEndBeat)
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

        private int CueMarkAt(int cueMarkIndex)
        {
            var cueMarkOffsets = CueMarkOffsets;
            return sheet.ToAbsoluteBeat(phraseStartBeat, cueMarkOffsets[ClampIndex(cueMarkIndex, cueMarkOffsets.Length)]);
        }

        private int[] CueMarkOffsets => sheet.CueMarkOffsets ?? Array.Empty<int>();
    }

    /// <summary>Creates On-Air Timing using Unity's runtime random source for boundary selection.</summary>
    public OnAirTiming()
        : this((minInclusive, maxExclusive) => UnityEngine.Random.Range(minInclusive, maxExclusive))
    {
    }

    /// <summary>Creates On-Air Timing with an explicit random source for deterministic seam tests.</summary>
    public OnAirTiming(Func<int, int, int> randomRange)
    {
        this.randomRange = randomRange ?? throw new ArgumentNullException(nameof(randomRange));
    }

    /// <summary>Clears all remembered timing state so the next synced frame starts a new interpretation.</summary>
    public void Reset()
    {
        lastBeat = -1;
        hasPhaseAnchor = false;
        phaseAnchorConfidence = PhaseConfidence.Unlocked;
        phaseAnchorLandingBeat = -1;
        cueSheetPlans.ResetAll();
        lastSource = TimingFrameSource.Unlocked;
    }

    /// <summary>Builds the current Timing Frame from one live rhythm snapshot.</summary>
    public TimingFrame ReadFrame(
        OnAirTimingInput input,
        PassLocalTimingState passLocalState,
        int minimumChangeCadenceBeats)
    {
        if (minimumChangeCadenceBeats <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumChangeCadenceBeats), minimumChangeCadenceBeats, "Minimum cadence must be positive.");
        }

        var phaseInput = input.ToPhaseInput();
        var phase = PhaseClock.Resolve(phaseInput);
        var beatRewoundToNewPass = BeatRewoundToNewPass(lastBeat, input.Beat, minimumChangeCadenceBeats);
        var passState = BuildFramePassLocalState(passLocalState, input.Beat, beatRewoundToNewPass);
        if (input.Beat >= 1)
        {
            lastBeat = input.Beat;
        }

        if (TrackPhaseUnavailable(input) && input.Beat >= 1)
        {
            if (HasCoastablePhaseAnchor())
            {
                return BuildCoastingFrame(input, phase, beatRewoundToNewPass, passState, minimumChangeCadenceBeats);
            }

            cueSheetPlans.ResetCurrent();
            return BuildUnlockedFrame(input, phase, beatRewoundToNewPass, passState);
        }

        if (phase.Confidence != PhaseConfidence.Unlocked && input.Beat >= 1)
        {
            var previousSource = lastSource;
            var target = ResolveCueMark(
                input.Beat,
                phaseInput,
                phase,
                beatRewoundToNewPass,
                passState.ConsumedCueMarkBeat,
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
        PhaseReading phase,
        bool beatRewoundToNewPass,
        FramePassLocalState passState,
        int minimumChangeCadenceBeats)
    {
        CoastPhaseAnchor(input.Beat, minimumChangeCadenceBeats);
        lastSource = TimingFrameSource.Coast;
        return CreateFrame(
            input,
            phase,
            true,
            phaseAnchorConfidence,
            phaseAnchorLandingBeat,
            false,
            default,
            TimingFrameSource.Coast,
            beatRewoundToNewPass,
            passState,
            false);
    }

    private TimingFrame BuildAnchoredFrame(
        OnAirTimingInput input,
        PhaseReading phase,
        ResolvedTimingTarget target,
        bool beatRewoundToNewPass,
        FramePassLocalState passState,
        bool reanchored)
    {
        hasPhaseAnchor = true;
        phaseAnchorConfidence = phase.Confidence;
        phaseAnchorLandingBeat = target.CueMarkBeat;
        lastSource = target.Source;
        return CreateFrame(
            input,
            phase,
            true,
            phaseAnchorConfidence,
            phaseAnchorLandingBeat,
            target.HasPhraseWindow,
            target.PhraseWindow,
            target.Source,
            beatRewoundToNewPass,
            passState,
            reanchored);
    }

    private TimingFrame BuildUnlockedFrame(
        OnAirTimingInput input,
        PhaseReading phase,
        bool beatRewoundToNewPass,
        FramePassLocalState passState)
    {
        hasPhaseAnchor = false;
        phaseAnchorConfidence = PhaseConfidence.Unlocked;
        phaseAnchorLandingBeat = -1;
        lastSource = TimingFrameSource.Unlocked;
        return CreateFrame(
            input,
            phase,
            false,
            PhaseConfidence.Unlocked,
            -1,
            false,
            default,
            TimingFrameSource.Unlocked,
            beatRewoundToNewPass,
            passState,
            false);
    }

    private static TimingFrame CreateFrame(
        OnAirTimingInput input,
        PhaseReading phase,
        bool hasPhaseAnchor,
        PhaseConfidence phaseAnchorConfidence,
        int cueMarkBeat,
        bool hasPhraseWindow,
        PhraseWindow phraseWindow,
        TimingFrameSource source,
        bool beatRewoundToNewPass,
        FramePassLocalState passState,
        bool reanchored)
    {
        return new TimingFrame(
            input,
            phase,
            hasPhaseAnchor,
            phaseAnchorConfidence,
            cueMarkBeat,
            hasPhraseWindow,
            phraseWindow,
            source,
            beatRewoundToNewPass,
            passState.State,
            passState.ClearedCueState,
            passState.ClearedCadenceState,
            reanchored);
    }

    private static bool ReanchoredFrom(TimingFrameSource previousSource, TimingFrameSource source, bool hadPhaseAnchor)
    {
        return hadPhaseAnchor
            && (source == TimingFrameSource.TrackPhaseBoundary || source == TimingFrameSource.CueMark)
            && (previousSource == TimingFrameSource.Coast || previousSource == TimingFrameSource.PhaseClockGrid);
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
        PhaseInput phaseInput,
        PhaseReading phase,
        bool beatRewoundToNewPass,
        int? consumedCueMarkBeat,
        int minimumChangeCadenceBeats)
    {
        if (PhraseWindow.TryFromUpcomingTrackPhase(
            beat,
            phaseInput.PhaseCountBeats,
            phaseInput.PhaseLengthBeats,
            out var upcomingPhraseWindow)
            && phaseInput.PhaseActive == 0)
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

        if (phaseInput.PhaseActive >= 1
            && PhraseWindow.TryFromTrackPhase(
                beat,
                phaseInput.PhaseCountBeats,
                phaseInput.PhaseLengthBeats,
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
        return ResolvedTimingTarget.PhaseClockGrid(GetLandingBeatFromPhasePosition(beat, phase.PhasePosition));
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
        var beatsUntilLanding = PhaseClock.PhraseBeats - phasePosition + 1;
        return beat + beatsUntilLanding;
    }

    private static bool BeatRewoundToNewPass(int previousBeat, int beat, int minimumChangeCadenceBeats)
    {
        return previousBeat >= 1
            && beat >= 1
            && beat < previousBeat
            && previousBeat - beat + 1 >= minimumChangeCadenceBeats;
    }

    private static FramePassLocalState BuildFramePassLocalState(
        PassLocalTimingState passLocalState,
        int beat,
        bool beatRewoundToNewPass)
    {
        if (!beatRewoundToNewPass || beat < 1)
        {
            return new FramePassLocalState(passLocalState, passLocalState);
        }

        var lastCueBeat = passLocalState.LastCueBeat is { } cueBeat && cueBeat >= beat
            ? (int?)null
            : passLocalState.LastCueBeat;
        var previousCueMarkBeat = passLocalState.PreviousCueMarkBeat is { } cueMarkBeat && cueMarkBeat >= beat
            ? (int?)null
            : passLocalState.PreviousCueMarkBeat;
        var correctedState = new PassLocalTimingState(lastCueBeat, previousCueMarkBeat);
        return new FramePassLocalState(correctedState, passLocalState);
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
