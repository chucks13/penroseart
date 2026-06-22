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

    /// <summary>The next target is the mandatory final boundary of the current Phrase Window.</summary>
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

    /// <summary>Whole beats until the current Track Phase boundary, or -1 when unavailable.</summary>
    public readonly int BeatsUntilPhraseBoundary;

    /// <summary>Total length of the current Phrase Window in beats, or -1 when unavailable.</summary>
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
    private bool hasSelectedPhaseBoundaryPlan;
    private SelectedPhaseBoundaryPlan selectedPhaseBoundaryPlan;
    private int selectedPhaseBoundaryIndex;
    private TimingFrameSource lastSource = TimingFrameSource.Unlocked;

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
        hasSelectedPhaseBoundaryPlan = false;
        selectedPhaseBoundaryPlan = default;
        selectedPhaseBoundaryIndex = 0;
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
        var correctedPassLocalState = CorrectPassLocalStateForBeatRewind(passLocalState, input.Beat, beatRewoundToNewPass);
        var clearedCueState = correctedPassLocalState.LastCueBeat != passLocalState.LastCueBeat;
        var clearedCadenceState = correctedPassLocalState.PreviousSelectedPhaseBoundary != passLocalState.PreviousSelectedPhaseBoundary;
        if (input.Beat >= 1)
        {
            lastBeat = input.Beat;
        }

        if (TrackPhaseUnavailable(input) && input.Beat >= 1)
        {
            if (HasCoastablePhaseAnchor())
            {
                CoastPhaseAnchor(input.Beat, minimumChangeCadenceBeats);
                lastSource = TimingFrameSource.Coast;
                return new TimingFrame(
                    input,
                    phase,
                    true,
                    phaseAnchorConfidence,
                    phaseAnchorLandingBeat,
                    false,
                    default,
                    TimingFrameSource.Coast,
                    beatRewoundToNewPass,
                    correctedPassLocalState,
                    clearedCueState,
                    clearedCadenceState,
                    false);
            }

            ResetSelectedPhaseBoundaryPlan();
            hasPhaseAnchor = false;
            phaseAnchorConfidence = PhaseConfidence.Unlocked;
            phaseAnchorLandingBeat = -1;
            lastSource = TimingFrameSource.Unlocked;
            return new TimingFrame(
                input,
                phase,
                false,
                PhaseConfidence.Unlocked,
                -1,
                false,
                default,
                TimingFrameSource.Unlocked,
                beatRewoundToNewPass,
                correctedPassLocalState,
                clearedCueState,
                clearedCadenceState,
                false);
        }

        if (phase.Confidence != PhaseConfidence.Unlocked && input.Beat >= 1)
        {
            var previousSource = lastSource;
            var source = ResolveSelectedPhaseBoundary(
                input.Beat,
                phaseInput,
                phase,
                beatRewoundToNewPass,
                correctedPassLocalState.PreviousSelectedPhaseBoundary,
                minimumChangeCadenceBeats,
                out var selectedPhaseBoundary,
                out var hasPhraseWindow,
                out var phraseWindow);

            var reanchored = hasPhaseAnchor
                && (source == TimingFrameSource.TrackPhaseBoundary || source == TimingFrameSource.SelectedPhaseBoundary)
                && (previousSource == TimingFrameSource.Coast || previousSource == TimingFrameSource.PhaseClockGrid);

            hasPhaseAnchor = true;
            phaseAnchorConfidence = phase.Confidence;
            phaseAnchorLandingBeat = selectedPhaseBoundary;
            lastSource = source;

            return new TimingFrame(
                input,
                phase,
                true,
                phaseAnchorConfidence,
                phaseAnchorLandingBeat,
                hasPhraseWindow,
                phraseWindow,
                source,
                beatRewoundToNewPass,
                correctedPassLocalState,
                clearedCueState,
                clearedCadenceState,
                reanchored);
        }

        if (hasPhaseAnchor && input.Beat >= 1)
        {
            CoastPhaseAnchor(input.Beat, minimumChangeCadenceBeats);
            lastSource = TimingFrameSource.Coast;
            return new TimingFrame(
                input,
                phase,
                true,
                phaseAnchorConfidence,
                phaseAnchorLandingBeat,
                false,
                default,
                TimingFrameSource.Coast,
                beatRewoundToNewPass,
                correctedPassLocalState,
                clearedCueState,
                clearedCadenceState,
                false);
        }

        hasPhaseAnchor = false;
        phaseAnchorConfidence = PhaseConfidence.Unlocked;
        phaseAnchorLandingBeat = -1;
        lastSource = TimingFrameSource.Unlocked;
        return new TimingFrame(
            input,
            phase,
            false,
            PhaseConfidence.Unlocked,
            -1,
            false,
            default,
            TimingFrameSource.Unlocked,
            beatRewoundToNewPass,
            correctedPassLocalState,
            clearedCueState,
            clearedCadenceState,
            false);
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

    private TimingFrameSource ResolveSelectedPhaseBoundary(
        int beat,
        PhaseInput phaseInput,
        PhaseReading phase,
        bool beatRewoundToNewPass,
        int? previousSelectedPhaseBoundary,
        int minimumChangeCadenceBeats,
        out int selectedPhaseBoundary,
        out bool hasPhraseWindow,
        out PhraseWindow phraseWindow)
    {
        if (PhraseWindow.TryFromTrackPhase(
            beat,
            phaseInput.PhaseCountBeats,
            phaseInput.PhaseLengthBeats,
            out phraseWindow))
        {
            hasPhraseWindow = true;
            return ResolveSelectedPhaseBoundaryFromPlan(
                beat,
                phraseWindow,
                beatRewoundToNewPass,
                previousSelectedPhaseBoundary,
                minimumChangeCadenceBeats,
                out selectedPhaseBoundary);
        }

        ResetSelectedPhaseBoundaryPlan();
        hasPhraseWindow = false;
        selectedPhaseBoundary = GetLandingBeatFromPhasePosition(beat, phase.PhasePosition);
        return TimingFrameSource.PhaseClockGrid;
    }

    private TimingFrameSource ResolveSelectedPhaseBoundaryFromPlan(
        int beat,
        PhraseWindow phraseWindow,
        bool beatRewoundToNewPass,
        int? previousSelectedPhaseBoundary,
        int minimumChangeCadenceBeats,
        out int selectedPhaseBoundary)
    {
        if (NeedsSelectedPhaseBoundaryPlan(phraseWindow))
        {
            BuildSelectedPhaseBoundaryPlan(beat, phraseWindow, previousSelectedPhaseBoundary, minimumChangeCadenceBeats);
        }
        else if (beatRewoundToNewPass)
        {
            selectedPhaseBoundaryIndex = 0;
        }

        var selectedPhaseBoundaries = selectedPhaseBoundaryPlan.SelectedPhaseBoundaries ?? Array.Empty<int>();
        while (selectedPhaseBoundaryIndex < selectedPhaseBoundaries.Length - 1
            && selectedPhaseBoundaries[selectedPhaseBoundaryIndex] <= beat)
        {
            selectedPhaseBoundaryIndex++;
        }

        selectedPhaseBoundary = selectedPhaseBoundaries.Length > 0
            ? selectedPhaseBoundaries[ClampIndex(selectedPhaseBoundaryIndex, selectedPhaseBoundaries.Length)]
            : phraseWindow.EndBeat;
        return selectedPhaseBoundary == selectedPhaseBoundaryPlan.PhraseEndBeat
            ? TimingFrameSource.TrackPhaseBoundary
            : TimingFrameSource.SelectedPhaseBoundary;
    }

    private bool NeedsSelectedPhaseBoundaryPlan(PhraseWindow phraseWindow)
    {
        var selectedPhaseBoundaries = selectedPhaseBoundaryPlan.SelectedPhaseBoundaries;
        return !hasSelectedPhaseBoundaryPlan
            || selectedPhaseBoundaries == null
            || selectedPhaseBoundaries.Length == 0
            || !selectedPhaseBoundaryPlan.Matches(phraseWindow);
    }

    private void BuildSelectedPhaseBoundaryPlan(
        int beat,
        PhraseWindow phraseWindow,
        int? previousSelectedPhaseBoundary,
        int minimumChangeCadenceBeats)
    {
        selectedPhaseBoundaryPlan = SelectedPhaseBoundaryPlan.Build(
            phraseWindow,
            beat,
            phaseBoundary => ChangeCadence.CanChangeAt(phaseBoundary, previousSelectedPhaseBoundary, minimumChangeCadenceBeats),
            randomRange);
        hasSelectedPhaseBoundaryPlan = true;
        selectedPhaseBoundaryIndex = 0;
    }

    private void ResetSelectedPhaseBoundaryPlan()
    {
        hasSelectedPhaseBoundaryPlan = false;
        selectedPhaseBoundaryPlan = default;
        selectedPhaseBoundaryIndex = 0;
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

    private static PassLocalTimingState CorrectPassLocalStateForBeatRewind(
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
        var previousSelectedPhaseBoundary = passLocalState.PreviousSelectedPhaseBoundary is { } phaseBoundary && phaseBoundary >= beat
            ? (int?)null
            : passLocalState.PreviousSelectedPhaseBoundary;
        return new PassLocalTimingState(lastCueBeat, previousSelectedPhaseBoundary);
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
