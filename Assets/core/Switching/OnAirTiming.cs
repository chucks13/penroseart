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
    public static OnAirTimingInput Unavailable { get; } = new OnAirTimingInput(-1, -1, -1, -1, -1);

    /// <summary>Focused on-air absolute beat count, or -1 when unavailable.</summary>
    public readonly int Beat;

    /// <summary>Focused on-air 1-based beat label inside the current bar, or -1 when unavailable.</summary>
    public readonly int BeatInBar;

    /// <summary>Track Phase active tri-state: 1 active, 0 inactive-but-present, -1 unavailable.</summary>
    public readonly int TrackPhaseActive;

    /// <summary>Whole beats until the active Track Phase boundary, upcoming Track Phase start, or -1 when unavailable.</summary>
    public readonly int BeatsUntilPhraseBoundary;

    /// <summary>Total length of the active or upcoming Phrase Window in beats, or -1 when unavailable.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>
    /// Integer-pure track identity: a monotonically increasing ordinal that changes when the
    /// on-air track title changes, or -1 when no track is on air. PhaseLock detects a track
    /// change by a change in this value (the raw title stays out of the integer Phase seam).
    /// </summary>
    public readonly int TrackOrdinal;

    public OnAirTimingInput(
        int beat,
        int beatInBar,
        int trackPhaseActive,
        int beatsUntilPhraseBoundary,
        int phraseLengthBeats,
        int trackOrdinal = -1)
    {
        Beat = beat;
        BeatInBar = beatInBar;
        TrackPhaseActive = trackPhaseActive;
        BeatsUntilPhraseBoundary = beatsUntilPhraseBoundary;
        PhraseLengthBeats = phraseLengthBeats;
        TrackOrdinal = trackOrdinal;
    }

    /// <summary>Captures the nullable BeatManager rhythm queries without exposing raw Track Phase interpretation to callers.</summary>
    public static OnAirTimingInput From(BeatManager beatManager)
    {
        if (beatManager == null)
        {
            return Unavailable;
        }

        var phrase = beatManager.Phrase;
        return new OnAirTimingInput(
            beatManager.Beat ?? -1,
            beatManager.BeatInBar ?? -1,
            phrase is { inPhrase: true } ? 1 : phrase is { } ? 0 : -1,
            phrase?.beatsUntilNext ?? -1,
            phrase?.lengthBeats ?? -1,
            beatManager.TrackOrdinal ?? -1);
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

/// <summary>Read-only snapshot of the active Cue Sheet used by On-Air Timing.</summary>
public readonly struct CueSheetStatus
{
    /// <summary>No Cue Sheet is currently active.</summary>
    public static CueSheetStatus Empty { get; } = new CueSheetStatus(false, -1, -1, -1, -1, -1, Array.Empty<int>());

    /// <summary>Whether a current Cue Sheet exists.</summary>
    public readonly bool HasSheet;

    /// <summary>Absolute beat where the current Phrase starts.</summary>
    public readonly int PhraseStartBeat;

    /// <summary>Absolute beat where the current Phrase ends.</summary>
    public readonly int PhraseEndBeat;

    /// <summary>Total current Phrase length in beats.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Index into <see cref="CueMarkOffsets"/> for the selected current Cue Mark.</summary>
    public readonly int CurrentCueMarkIndex;

    /// <summary>Absolute beat of the selected current Cue Mark.</summary>
    public readonly int CurrentCueMarkBeat;

    /// <summary>Phrase-relative Cue Mark offsets for display.</summary>
    public readonly int[] CueMarkOffsets;

    public CueSheetStatus(
        bool hasSheet,
        int phraseStartBeat,
        int phraseEndBeat,
        int phraseLengthBeats,
        int currentCueMarkIndex,
        int currentCueMarkBeat,
        int[] cueMarkOffsets)
    {
        HasSheet = hasSheet;
        PhraseStartBeat = phraseStartBeat;
        PhraseEndBeat = phraseEndBeat;
        PhraseLengthBeats = phraseLengthBeats;
        CurrentCueMarkIndex = currentCueMarkIndex;
        CurrentCueMarkBeat = currentCueMarkBeat;
        CueMarkOffsets = cueMarkOffsets ?? Array.Empty<int>();
    }
}

/// <summary>Director-facing interpretation of one synced on-air frame.</summary>
public readonly struct TimingFrame
{
    public static TimingFrame Unavailable { get; } = new TimingFrame(
        OnAirTimingInput.Unavailable,
        PhaseReading.None,
        false,
        -1,
        false,
        default,
        TimingFrameSource.Unlocked,
        false,
        PassLocalTimingState.Empty,
        false,
        CueSheetStatus.Empty);

    /// <summary>The live rhythm snapshot that produced this frame.</summary>
    public readonly OnAirTimingInput Input;

    /// <summary>Current on-air beat, or -1 when unavailable.</summary>
    public readonly int CurrentBeat;

    /// <summary>The Phase Lock reading for this frame.</summary>
    public readonly PhaseReading Phase;

    /// <summary>Whether this frame has a Phase Anchor the Director can target.</summary>
    public readonly bool HasPhaseAnchor;

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

    /// <summary>Active Cue Sheet snapshot for inspector/status displays.</summary>
    public readonly CueSheetStatus CueSheet;

    /// <summary>Stable domain-facing source for the selected timing target.</summary>
    public readonly TimingFrameSource Source;

    /// <summary>True when the current beat substantially rewound into a new pass.</summary>
    public readonly bool BeatRewoundToNewPass;

    /// <summary>Cue/cadence memory after On-Air Timing removes stale state from earlier loop passes.</summary>
    public readonly PassLocalTimingState PassLocalState;

    /// <summary>True when this frame is continuing from the last known Phase Anchor.</summary>
    public bool IsCoasting => Source == TimingFrameSource.Coast;

    /// <summary>True when fresh structural timing replaced a coasted or weaker anchor.</summary>
    public readonly bool Reanchored;

    public TimingFrame(
        OnAirTimingInput input,
        PhaseReading phase,
        bool hasPhaseAnchor,
        int cueMarkBeat,
        bool hasPhraseWindow,
        PhraseWindow phraseWindow,
        TimingFrameSource source,
        bool beatRewoundToNewPass,
        PassLocalTimingState passLocalState,
        bool reanchored,
        CueSheetStatus cueSheet = default)
    {
        Input = input;
        CurrentBeat = input.Beat;
        Phase = phase;
        HasPhaseAnchor = hasPhaseAnchor;
        CueMarkBeat = cueMarkBeat;
        BeatsUntilCueMark = hasPhaseAnchor && input.Beat >= 1 ? cueMarkBeat - input.Beat : -1;
        HasPhraseWindow = hasPhraseWindow;
        PhraseWindow = phraseWindow;
        CueSheet = cueSheet;
        Source = source;
        BeatRewoundToNewPass = beatRewoundToNewPass;
        PassLocalState = passLocalState;
        Reanchored = reanchored;
    }
}

