using System;

/// <summary>Domain-facing source for the Director's current On-Air Timing Frame.</summary>
public enum TimingFrameSource
{
    /// <summary>No usable live timing target is available.</summary>
    Unlocked,

    /// <summary>The next target is an interior Cue Mark selected inside the current Phrase Window.</summary>
    CueMark,

    /// <summary>The next target is a mandatory structural Track Phase boundary.</summary>
    TrackPhaseBoundary,
}

/// <summary>
/// The live rhythm snapshot On-Air Timing needs to interpret the current synced frame. A tiny integer
/// seam (−1 sentinels) kept purely for CuePlanner testability: every field is projected from the
/// nullable <see cref="BeatManager"/> phrase queries by <see cref="From"/>, so the wire's structure is
/// interpreted once here rather than inside the planner.
/// </summary>
public readonly struct OnAirTimingInput
{
    public static OnAirTimingInput Unavailable { get; } = new OnAirTimingInput(-1, -1, -1, -1, -1);

    /// <summary>Focused on-air absolute beat count, or -1 when unavailable.</summary>
    public readonly int Beat;

    /// <summary>Whole beats until the current Phrase ends (its boundary), or -1 when unavailable.</summary>
    public readonly int BeatsUntilPhraseEnd;

    /// <summary>Total length of the current Phrase in beats, or -1 when unavailable.</summary>
    public readonly int PhraseLengthBeats;

    /// <summary>Whole beats until the next Phrase starts (the current Phrase's change), or -1 when unavailable.</summary>
    public readonly int NextPhraseStartInBeats;

    /// <summary>Total length of the next Phrase in beats — its own length, not the current one's — or -1 when unavailable.</summary>
    public readonly int NextPhraseLengthBeats;

    public OnAirTimingInput(
        int beat,
        int beatsUntilPhraseEnd,
        int phraseLengthBeats,
        int nextPhraseStartInBeats,
        int nextPhraseLengthBeats)
    {
        Beat = beat;
        BeatsUntilPhraseEnd = beatsUntilPhraseEnd;
        PhraseLengthBeats = phraseLengthBeats;
        NextPhraseStartInBeats = nextPhraseStartInBeats;
        NextPhraseLengthBeats = nextPhraseLengthBeats;
    }

    /// <summary>
    /// Captures the nullable BeatManager phrase queries into the integer seam, mapping each null to -1.
    /// The current Phrase supplies the live window; the next Phrase supplies the pre-first-phrase and
    /// counting-down window (its own announced length).
    /// </summary>
    public static OnAirTimingInput From(BeatManager beatManager)
    {
        if (beatManager == null)
        {
            return Unavailable;
        }

        var phrase = beatManager.Phrase;
        var nextPhrase = beatManager.NextPhrase;
        return new OnAirTimingInput(
            beatManager.Beat ?? -1,
            phrase?.beatsUntilNext ?? -1,
            phrase?.lengthBeats ?? -1,
            nextPhrase?.beatsUntilChange ?? -1,
            nextPhrase?.lengthBeats ?? -1);
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
        false,
        -1,
        false,
        default,
        TimingFrameSource.Unlocked,
        false,
        CueSheetStatus.Empty);

    /// <summary>The live rhythm snapshot that produced this frame.</summary>
    public readonly OnAirTimingInput Input;

    /// <summary>Current on-air beat, or -1 when unavailable.</summary>
    public readonly int CurrentBeat;

    /// <summary>Whether this frame has a Cue Mark the Director can target.</summary>
    public readonly bool HasCueMark;

    /// <summary>Cue Mark where the Director should land its next Impact Point, or -1 when unlocked.</summary>
    public readonly int CueMarkBeat;

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

    public TimingFrame(
        OnAirTimingInput input,
        bool hasCueMark,
        int cueMarkBeat,
        bool hasPhraseWindow,
        PhraseWindow phraseWindow,
        TimingFrameSource source,
        bool beatRewoundToNewPass,
        CueSheetStatus cueSheet = default)
    {
        Input = input;
        CurrentBeat = input.Beat;
        HasCueMark = hasCueMark;
        CueMarkBeat = cueMarkBeat;
        BeatsUntilCueMark = hasCueMark && input.Beat >= 1 ? cueMarkBeat - input.Beat : -1;
        HasPhraseWindow = hasPhraseWindow;
        PhraseWindow = phraseWindow;
        CueSheet = cueSheet;
        Source = source;
        BeatRewoundToNewPass = beatRewoundToNewPass;
    }
}
