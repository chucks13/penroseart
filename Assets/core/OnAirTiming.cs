using System;

/// <summary>Domain-facing source for the Director's current On-Air Timing Frame.</summary>
public enum TimingFrameSource
{
    /// <summary>No usable live timing target is available.</summary>
    Unlocked,

    /// <summary>The next target is inferred from the 16-beat Phase grid.</summary>
    PhaseClockGrid,

    /// <summary>The next target is an interior Phase Boundary selected inside the current Phrase Window.</summary>
    SelectedPhaseBoundary,

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

    /// <summary>The last selected Phase Boundary used for cadence, or null when this pass has no prior boundary.</summary>
    public readonly int? PreviousSelectedPhaseBoundary;

    /// <summary>Creates pass-local cue/cadence memory from the Director's current synced state.</summary>
    public PassLocalTimingState(int? lastCueBeat, int? previousSelectedPhaseBoundary)
    {
        LastCueBeat = lastCueBeat;
        PreviousSelectedPhaseBoundary = previousSelectedPhaseBoundary;
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

    /// <summary>Selected Phase Boundary where the Director should land its next Impact Point.</summary>
    public readonly int SelectedPhaseBoundary;

    /// <summary>Absolute beat where the current Phase Anchor lands, or -1 when unlocked.</summary>
    public int PhaseAnchorLandingBeat => SelectedPhaseBoundary;

    /// <summary>Beats until the Selected Phase Boundary, or -1 when unlocked.</summary>
    public readonly int BeatsUntilSelectedPhaseBoundary;

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
        int selectedPhaseBoundary,
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
        SelectedPhaseBoundary = selectedPhaseBoundary;
        BeatsUntilSelectedPhaseBoundary = hasPhaseAnchor && input.Beat >= 1 ? selectedPhaseBoundary - input.Beat : -1;
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
/// Owns PhaseInput construction, Phase/Phrase interpretation, selected Phase Boundary cursor state,
/// substantial beat rewind handling, and Phase Anchor coasting.
/// </summary>
public sealed class OnAirTiming
{
    private readonly Func<int, int, int> randomRange;

    private int lastBeat = -1;
    private bool hasPhaseAnchor;
    private PhaseConfidence phaseAnchorConfidence = PhaseConfidence.Unlocked;
    private int phaseAnchorLandingBeat = -1;
    private readonly SelectedPhaseBoundaryPlans selectedPhaseBoundaryPlans = new SelectedPhaseBoundaryPlans();
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
            ClearedCadenceState = state.PreviousSelectedPhaseBoundary != originalState.PreviousSelectedPhaseBoundary;
        }

        public int? ConsumedSelectedPhaseBoundary => State.PreviousSelectedPhaseBoundary;
    }

    private readonly struct ResolvedTimingTarget
    {
        public readonly TimingFrameSource Source;
        public readonly int SelectedPhaseBoundary;
        public readonly bool HasPhraseWindow;
        public readonly PhraseWindow PhraseWindow;

        public ResolvedTimingTarget(
            TimingFrameSource source,
            int selectedPhaseBoundary,
            bool hasPhraseWindow,
            PhraseWindow phraseWindow)
        {
            Source = source;
            SelectedPhaseBoundary = selectedPhaseBoundary;
            HasPhraseWindow = hasPhraseWindow;
            PhraseWindow = phraseWindow;
        }

        public static ResolvedTimingTarget PhaseClockGrid(int selectedPhaseBoundary)
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.PhaseClockGrid,
                selectedPhaseBoundary,
                false,
                default);
        }
    }

    private sealed class SelectedPhaseBoundaryPlans
    {
        private readonly SelectedPhaseBoundaryCursor current = new SelectedPhaseBoundaryCursor();
        private bool hasUpcoming;
        private SelectedPhaseBoundaryPlan upcoming;

        public void ResetAll()
        {
            current.Reset();
            hasUpcoming = false;
            upcoming = default;
        }

        public void ResetCurrent()
        {
            current.Reset();
        }

        public void PlanUpcoming(
            int beat,
            PhraseWindow phraseWindow,
            int? consumedSelectedPhaseBoundary,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange)
        {
            if (hasUpcoming && upcoming.Matches(phraseWindow))
            {
                return;
            }

            upcoming = SelectedPhaseBoundaryPlan.Build(
                phraseWindow,
                beat,
                phaseBoundary => ChangeCadence.CanChangeAt(phaseBoundary, consumedSelectedPhaseBoundary, minimumChangeCadenceBeats),
                randomRange,
                includePhraseStart: true);
            hasUpcoming = true;
        }

        public bool TryKeepUnconsumedMandatoryBoundaryAt(
            int beat,
            int? consumedSelectedPhaseBoundary,
            out int selectedPhaseBoundary,
            out PhraseWindow phraseWindow)
        {
            return current.TryKeepUnconsumedMandatoryBoundaryAt(
                beat,
                consumedSelectedPhaseBoundary,
                out selectedPhaseBoundary,
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
                && PhraseWindow.TryFromStartAndLength(upcoming.PhraseStartBeat, upcoming.PhraseLengthBeats, out phraseWindow);
        }

        public void PromoteUpcoming()
        {
            if (hasUpcoming)
            {
                current.Replace(upcoming);
            }
            else
            {
                current.Reset();
            }

            hasUpcoming = false;
            upcoming = default;
        }

        public TimingFrameSource ResolveCurrent(
            int beat,
            PhraseWindow phraseWindow,
            bool beatRewoundToNewPass,
            int? consumedSelectedPhaseBoundary,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange,
            out int selectedPhaseBoundary)
        {
            if (current.NeedsPlan(phraseWindow))
            {
                BuildCurrent(
                    beat,
                    phraseWindow,
                    consumedSelectedPhaseBoundary,
                    minimumChangeCadenceBeats,
                    randomRange);
            }
            else if (beatRewoundToNewPass)
            {
                current.RewindCursor();
            }

            current.AdvanceTo(beat, consumedSelectedPhaseBoundary);
            if (current.IsConsumedThroughPhraseEnd(consumedSelectedPhaseBoundary) && hasUpcoming)
            {
                PromoteUpcoming();
                current.AdvanceTo(beat, consumedSelectedPhaseBoundary);
            }

            selectedPhaseBoundary = current.CurrentBoundaryOr(phraseWindow.EndBeat);
            return selectedPhaseBoundary == current.PhraseEndBeat
                ? TimingFrameSource.TrackPhaseBoundary
                : TimingFrameSource.SelectedPhaseBoundary;
        }

        private void BuildCurrent(
            int beat,
            PhraseWindow phraseWindow,
            int? consumedSelectedPhaseBoundary,
            int minimumChangeCadenceBeats,
            Func<int, int, int> randomRange)
        {
            var plan = SelectedPhaseBoundaryPlan.Build(
                phraseWindow,
                beat,
                phaseBoundary => ChangeCadence.CanChangeAt(phaseBoundary, consumedSelectedPhaseBoundary, minimumChangeCadenceBeats),
                randomRange);
            current.Replace(plan);
        }
    }

    private sealed class SelectedPhaseBoundaryCursor
    {
        private SelectedPhaseBoundaryPlan plan;
        private int index;

        public bool HasPlan { get; private set; }

        public int PhraseEndBeat => plan.PhraseEndBeat;

        public void Reset()
        {
            HasPlan = false;
            plan = default;
            index = 0;
        }

        public void Replace(SelectedPhaseBoundaryPlan selectedPlan)
        {
            HasPlan = true;
            plan = selectedPlan;
            index = 0;
        }

        public void RewindCursor()
        {
            index = 0;
        }

        public bool NeedsPlan(PhraseWindow phraseWindow)
        {
            var boundaries = Boundaries;
            return !HasPlan
                || boundaries.Length == 0
                || !plan.Matches(phraseWindow);
        }

        public bool TryGetActivePhraseWindow(int beat, out PhraseWindow phraseWindow)
        {
            phraseWindow = default;
            return HasPlan
                && plan.PhraseEndBeat >= beat
                && PhraseWindow.TryFromStartAndLength(plan.PhraseStartBeat, plan.PhraseLengthBeats, out phraseWindow);
        }

        public bool TryKeepUnconsumedMandatoryBoundaryAt(
            int beat,
            int? consumedSelectedPhaseBoundary,
            out int selectedPhaseBoundary,
            out PhraseWindow phraseWindow)
        {
            selectedPhaseBoundary = -1;
            phraseWindow = default;
            if (!HasPlan
                || IsConsumedThroughPhraseEnd(consumedSelectedPhaseBoundary)
                || !PhraseWindow.TryFromStartAndLength(plan.PhraseStartBeat, plan.PhraseLengthBeats, out phraseWindow))
            {
                return false;
            }

            AdvanceTo(beat, consumedSelectedPhaseBoundary);
            var currentBoundary = CurrentBoundaryOr(-1);
            if (currentBoundary != beat || currentBoundary != plan.PhraseEndBeat)
            {
                return false;
            }

            selectedPhaseBoundary = currentBoundary;
            return true;
        }

        public void AdvanceTo(int beat, int? consumedSelectedPhaseBoundary)
        {
            var boundaries = Boundaries;
            while (index < boundaries.Length - 1 && boundaries[index] < beat)
            {
                index++;
            }

            while (index < boundaries.Length - 1
                && consumedSelectedPhaseBoundary is { } firedBoundary
                && boundaries[index] <= firedBoundary)
            {
                index++;
            }
        }

        public int CurrentBoundaryOr(int fallbackBoundary)
        {
            var boundaries = Boundaries;
            return boundaries.Length > 0
                ? boundaries[ClampIndex(index, boundaries.Length)]
                : fallbackBoundary;
        }

        public bool IsConsumedThroughPhraseEnd(int? consumedSelectedPhaseBoundary)
        {
            return consumedSelectedPhaseBoundary is { } firedBoundary && firedBoundary >= plan.PhraseEndBeat;
        }

        private int[] Boundaries => plan.SelectedPhaseBoundaries ?? Array.Empty<int>();
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
        selectedPhaseBoundaryPlans.ResetAll();
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

            selectedPhaseBoundaryPlans.ResetCurrent();
            return BuildUnlockedFrame(input, phase, beatRewoundToNewPass, passState);
        }

        if (phase.Confidence != PhaseConfidence.Unlocked && input.Beat >= 1)
        {
            var previousSource = lastSource;
            var target = ResolveSelectedPhaseBoundary(
                input.Beat,
                phaseInput,
                phase,
                beatRewoundToNewPass,
                passState.ConsumedSelectedPhaseBoundary,
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
        phaseAnchorLandingBeat = target.SelectedPhaseBoundary;
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
        int selectedPhaseBoundary,
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
            selectedPhaseBoundary,
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
            && (source == TimingFrameSource.TrackPhaseBoundary || source == TimingFrameSource.SelectedPhaseBoundary)
            && (previousSource == TimingFrameSource.Coast || previousSource == TimingFrameSource.PhaseClockGrid);
    }

    private bool HasCoastablePhaseAnchor()
    {
        return hasPhaseAnchor
            && (lastSource == TimingFrameSource.SelectedPhaseBoundary
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

    private ResolvedTimingTarget ResolveSelectedPhaseBoundary(
        int beat,
        PhaseInput phaseInput,
        PhaseReading phase,
        bool beatRewoundToNewPass,
        int? consumedSelectedPhaseBoundary,
        int minimumChangeCadenceBeats)
    {
        if (PhraseWindow.TryFromUpcomingTrackPhase(
            beat,
            phaseInput.PhaseCountBeats,
            phaseInput.PhaseLengthBeats,
            out var upcomingPhraseWindow)
            && phaseInput.PhaseActive == 0)
        {
            selectedPhaseBoundaryPlans.PlanUpcoming(
                beat,
                upcomingPhraseWindow,
                consumedSelectedPhaseBoundary,
                minimumChangeCadenceBeats,
                randomRange);
        }

        if (selectedPhaseBoundaryPlans.TryKeepUnconsumedMandatoryBoundaryAt(
            beat,
            consumedSelectedPhaseBoundary,
            out var selectedPhaseBoundary,
            out var phraseWindow))
        {
            return new ResolvedTimingTarget(
                TimingFrameSource.TrackPhaseBoundary,
                selectedPhaseBoundary,
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
            if (selectedPhaseBoundaryPlans.HasUpcomingFor(phraseWindow))
            {
                selectedPhaseBoundaryPlans.PromoteUpcoming();
            }

            return ResolveSelectedPhaseBoundaryFromPlan(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedSelectedPhaseBoundary,
                minimumChangeCadenceBeats);
        }

        if (selectedPhaseBoundaryPlans.TryGetActivePhraseWindow(beat, out phraseWindow))
        {
            return ResolveSelectedPhaseBoundaryFromPlan(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedSelectedPhaseBoundary,
                minimumChangeCadenceBeats);
        }

        if (selectedPhaseBoundaryPlans.TryGetUpcomingPhraseWindow(out phraseWindow))
        {
            selectedPhaseBoundaryPlans.PromoteUpcoming();
            return ResolveSelectedPhaseBoundaryFromPlan(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                consumedSelectedPhaseBoundary,
                minimumChangeCadenceBeats);
        }

        selectedPhaseBoundaryPlans.ResetCurrent();
        return ResolvedTimingTarget.PhaseClockGrid(GetLandingBeatFromPhasePosition(beat, phase.PhasePosition));
    }

    private ResolvedTimingTarget ResolveSelectedPhaseBoundaryFromPlan(
        int beat,
        PhraseWindow phraseWindow,
        bool beatRewoundToNewPass,
        int? consumedSelectedPhaseBoundary,
        int minimumChangeCadenceBeats)
    {
        var source = selectedPhaseBoundaryPlans.ResolveCurrent(
            beat,
            phraseWindow,
            beatRewoundToNewPass,
            consumedSelectedPhaseBoundary,
            minimumChangeCadenceBeats,
            randomRange,
            out var selectedPhaseBoundary);
        return new ResolvedTimingTarget(source, selectedPhaseBoundary, true, phraseWindow);
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
        var previousSelectedPhaseBoundary = passLocalState.PreviousSelectedPhaseBoundary is { } phaseBoundary && phaseBoundary >= beat
            ? (int?)null
            : passLocalState.PreviousSelectedPhaseBoundary;
        var correctedState = new PassLocalTimingState(lastCueBeat, previousSelectedPhaseBoundary);
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
