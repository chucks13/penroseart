// Projects read-only sequencing status into immutable timeline display meaning.
#nullable enable

using System;
using System.Collections.Generic;

/// <summary>Read-only inputs captured from the live sequencing status surfaces for timeline projection.</summary>
internal readonly struct LiveTimelineInput
{
    /// <summary>Creates one frame-coherent set of inputs for the editor-only timeline.</summary>
    public LiveTimelineInput(
        bool isSynced,
        CueSheetView currentSheet,
        CueSheetView nextSheet,
        int? currentAbsoluteBeat,
        int? currentPhraseBeat,
        int? currentGridBeat,
        int? nextPhraseStartsInBeats,
        SwitcherCueStatus loadedCue,
        float? executionProgress)
    {
        IsSynced = isSynced;
        CurrentSheet = currentSheet;
        NextSheet = nextSheet;
        CurrentAbsoluteBeat = currentAbsoluteBeat;
        CurrentPhraseBeat = currentPhraseBeat;
        CurrentGridBeat = currentGridBeat;
        NextPhraseStartsInBeats = nextPhraseStartsInBeats;
        LoadedCue = loadedCue;
        ExecutionProgress = executionProgress;
    }

    /// <summary>Whether the runtime has a usable live beat clock.</summary>
    public bool IsSynced { get; }

    /// <summary>The current Phrase's Cue Sheet.</summary>
    public CueSheetView CurrentSheet { get; }

    /// <summary>The announced next Phrase's Cue Sheet.</summary>
    public CueSheetView NextSheet { get; }

    /// <summary>The current absolute beat reported by the wire, when available.</summary>
    public int? CurrentAbsoluteBeat { get; }

    /// <summary>The current one-based beat within the Phrase, when available.</summary>
    public int? CurrentPhraseBeat { get; }

    /// <summary>The current one-based beat within the Grid, when available.</summary>
    public int? CurrentGridBeat { get; }

    /// <summary>Wire countdown to the next Phrase's first beat, when available.</summary>
    public int? NextPhraseStartsInBeats { get; }

    /// <summary>The Switcher's currently loaded Cue.</summary>
    public SwitcherCueStatus LoadedCue { get; }

    /// <summary>Active normalized Transition progress directly exposed by the Switcher.</summary>
    public float? ExecutionProgress { get; }
}

/// <summary>Immutable display model for one live sequencing frame.</summary>
internal sealed class LiveTimelineModel
{
    /// <summary>Creates a model containing distinct current and next Phrase plans.</summary>
    public LiveTimelineModel(
        bool isSynced,
        bool currentPositionAvailable,
        bool hasLoadedCue,
        bool loadedCueTimingAvailable,
        LiveTimelinePhrase current,
        LiveTimelinePhrase next,
        float? executionProgress)
    {
        IsSynced = isSynced;
        CurrentPositionAvailable = currentPositionAvailable;
        HasLoadedCue = hasLoadedCue;
        LoadedCueTimingAvailable = loadedCueTimingAvailable;
        Current = current;
        Next = next;
        ExecutionProgress = executionProgress;
    }

    /// <summary>Whether the runtime is in Synced Mode.</summary>
    public bool IsSynced { get; }

    /// <summary>Whether current Phrase and Grid facts agree on one live cell.</summary>
    public bool CurrentPositionAvailable { get; }

    /// <summary>Whether the Switcher reports a Loaded Cue.</summary>
    public bool HasLoadedCue { get; }

    /// <summary>Whether the Loaded Cue exposes a self-consistent beat timing window.</summary>
    public bool LoadedCueTimingAvailable { get; }

    /// <summary>The current Phrase projection.</summary>
    public LiveTimelinePhrase Current { get; }

    /// <summary>The announced next Phrase projection.</summary>
    public LiveTimelinePhrase Next { get; }

    /// <summary>Active normalized Transition progress, or null when no execution is exposed.</summary>
    public float? ExecutionProgress { get; }
}

/// <summary>Immutable display projection of one Cue Sheet.</summary>
internal sealed class LiveTimelinePhrase
{
    /// <summary>The explicit absent state used when no Cue Sheet is available.</summary>
    public static LiveTimelinePhrase Unavailable { get; } =
        new(false, string.Empty, 0, Array.Empty<LiveTimelineBlock>());

    /// <summary>Creates one display-ready Phrase plan.</summary>
    public LiveTimelinePhrase(
        bool isAvailable,
        string label,
        int lengthBeats,
        IReadOnlyList<LiveTimelineBlock> blocks)
    {
        IsAvailable = isAvailable;
        Label = label ?? string.Empty;
        LengthBeats = lengthBeats;
        Blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
    }

    /// <summary>Whether a valid Cue Sheet exists.</summary>
    public bool IsAvailable { get; }

    /// <summary>The announced Phrase label.</summary>
    public string Label { get; }

    /// <summary>The Phrase length in beats.</summary>
    public int LengthBeats { get; }

    /// <summary>Consecutive full or partial Grid blocks.</summary>
    public IReadOnlyList<LiveTimelineBlock> Blocks { get; }
}

/// <summary>Immutable display projection of one full or partial sixteen-beat Grid block.</summary>
internal sealed class LiveTimelineBlock
{
    /// <summary>Creates a display block from ordered one-based beat cells.</summary>
    public LiveTimelineBlock(int startPhraseBeat, IReadOnlyList<LiveTimelineCell> cells)
    {
        StartPhraseBeat = startPhraseBeat;
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
    }

    /// <summary>The first one-based Phrase beat represented by this block.</summary>
    public int StartPhraseBeat { get; }

    /// <summary>The block's ordered beat cells.</summary>
    public IReadOnlyList<LiveTimelineCell> Cells { get; }
}

/// <summary>Background meaning after semantic precedence is applied to a timeline cell.</summary>
internal enum LiveTimelineFill
{
    /// <summary>Neutral Grid background.</summary>
    Base,

    /// <summary>Transition Runway before the Impact Point.</summary>
    Runway,

    /// <summary>Transition Tail after the Impact Point.</summary>
    Tail,

    /// <summary>Live current-beat fill, which overrides Transition timing colors.</summary>
    CurrentBeat,
}

/// <summary>Semantic display attributes for one musical beat.</summary>
internal readonly struct LiveTimelineCell
{
    /// <summary>Creates one Phrase-relative beat cell with independent timing and identity attributes.</summary>
    public LiveTimelineCell(
        int phraseBeat,
        bool isCueMark,
        bool isLoadedCue,
        bool isLocked,
        bool isImpactPoint,
        bool isRunway,
        bool isTail,
        bool isCurrentBeat,
        bool isExecuting)
    {
        PhraseBeat = phraseBeat;
        IsCueMark = isCueMark;
        IsLoadedCue = isLoadedCue;
        IsLocked = isLocked;
        IsImpactPoint = isImpactPoint;
        IsRunway = isRunway;
        IsTail = isTail;
        IsCurrentBeat = isCurrentBeat;
        IsExecuting = isExecuting;
        Fill = isCurrentBeat
            ? LiveTimelineFill.CurrentBeat
            : isRunway
                ? LiveTimelineFill.Runway
                : isTail
                    ? LiveTimelineFill.Tail
                    : LiveTimelineFill.Base;
    }

    /// <summary>The one-based beat within the Phrase.</summary>
    public int PhraseBeat { get; }

    /// <summary>Whether the Cue Sheet places an ordinary Cue Mark on this beat.</summary>
    public bool IsCueMark { get; }

    /// <summary>Whether the Switcher has loaded this beat's Cue Mark.</summary>
    public bool IsLoadedCue { get; }

    /// <summary>Whether the loaded Cue is locked.</summary>
    public bool IsLocked { get; }

    /// <summary>Whether this beat is the Transition Impact Point.</summary>
    public bool IsImpactPoint { get; }

    /// <summary>Whether this beat lies in the Transition Runway.</summary>
    public bool IsRunway { get; }

    /// <summary>Whether this beat lies in the Transition Tail.</summary>
    public bool IsTail { get; }

    /// <summary>Whether this is the live current beat.</summary>
    public bool IsCurrentBeat { get; }

    /// <summary>Whether an active Transition execution occupies this beat.</summary>
    public bool IsExecuting { get; }

    /// <summary>The resolved background fill after current-beat precedence.</summary>
    public LiveTimelineFill Fill { get; }
}

/// <summary>Pure editor-only projection from read-only runtime snapshots to timeline display meaning.</summary>
internal static class LiveTimelineProjection
{
    /// <summary>Projects the current and next Cue Sheets into ordered Grid blocks without changing runtime state.</summary>
    public static LiveTimelineModel Build(LiveTimelineInput input)
    {
        var currentPositionAvailable = input.IsSynced &&
            input.CurrentPhraseBeat is { } livePhraseBeat &&
            livePhraseBeat > 0 &&
            input.CurrentGridBeat is { } liveGridBeat &&
            liveGridBeat >= 1 &&
            liveGridBeat <= CueSheet.GridBeats &&
            liveGridBeat == (livePhraseBeat - 1) % CueSheet.GridBeats + 1;

        int? currentPhraseStartBeat = null;
        if (input.IsSynced &&
            input.CurrentAbsoluteBeat is { } absoluteBeat &&
            input.CurrentPhraseBeat is { } phraseBeat &&
            phraseBeat > 0)
        {
            currentPhraseStartBeat = absoluteBeat - phraseBeat + 1;
        }

        int? nextPhraseStartBeat = null;
        if (input.IsSynced &&
            input.CurrentAbsoluteBeat is { } currentBeat &&
            input.NextPhraseStartsInBeats is { } startsInBeats &&
            startsInBeats >= 1)
        {
            nextPhraseStartBeat = currentBeat + startsInBeats;
        }

        var executionProgress = input.IsSynced && input.ExecutionProgress is { } progress
            ? Math.Max(0f, Math.Min(1f, progress))
            : (float?)null;

        var hasLoadedCue = input.IsSynced && input.LoadedCue.HasCue;
        var loadedCueTimingAvailable = hasLoadedCue && HasConsistentTiming(input.LoadedCue);
        var loadedCue = loadedCueTimingAvailable ? input.LoadedCue : SwitcherCueStatus.Empty;
        return new LiveTimelineModel(
            input.IsSynced,
            currentPositionAvailable,
            hasLoadedCue,
            loadedCueTimingAvailable,
            BuildPhrase(
                input.CurrentSheet,
                currentPhraseStartBeat,
                currentPositionAvailable ? input.CurrentPhraseBeat : null,
                loadedCue,
                executionProgress.HasValue),
            BuildPhrase(
                input.NextSheet,
                nextPhraseStartBeat,
                null,
                loadedCue,
                false),
            executionProgress);
    }

    /// <summary>Checks the Loaded Cue's existing timing facts without repairing or deriving any of them.</summary>
    private static bool HasConsistentTiming(SwitcherCueStatus cue)
    {
        return cue.CueMarkBeat >= 1 &&
            cue.RunwayBeats >= 0 &&
            cue.TailBeats >= 0 &&
            cue.StartBeat == cue.CueMarkBeat - cue.RunwayBeats &&
            cue.CompleteBeat == cue.CueMarkBeat + cue.TailBeats;
    }

    /// <summary>Splits one valid Phrase into full Grid blocks followed by its honest partial remainder.</summary>
    private static LiveTimelinePhrase BuildPhrase(
        CueSheetView sheet,
        int? phraseStartAbsoluteBeat,
        int? currentPhraseBeat,
        SwitcherCueStatus loadedCue,
        bool isExecutionActive)
    {
        if (!sheet.HasSheet || sheet.PhraseLengthBeats <= 0)
        {
            return LiveTimelinePhrase.Unavailable;
        }

        var blockCount = (sheet.PhraseLengthBeats + CueSheet.GridBeats - 1) / CueSheet.GridBeats;
        var blocks = new LiveTimelineBlock[blockCount];
        var cueMarks = sheet.CueMarkOffsets ?? Array.Empty<int>();

        for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
        {
            var startBeat = blockIndex * CueSheet.GridBeats + 1;
            var cellCount = Math.Min(CueSheet.GridBeats, sheet.PhraseLengthBeats - startBeat + 1);
            var cells = new LiveTimelineCell[cellCount];
            for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
            {
                var phraseBeat = startBeat + cellIndex;
                int? absoluteBeat = phraseStartAbsoluteBeat is { } phraseStart
                    ? phraseStart + phraseBeat - 1
                    : null;
                var isLoadedImpact = loadedCue.HasCue &&
                    absoluteBeat == loadedCue.CueMarkBeat;
                var isRunway = loadedCue.HasCue &&
                    absoluteBeat >= loadedCue.StartBeat &&
                    absoluteBeat < loadedCue.CueMarkBeat;
                var isTail = loadedCue.HasCue &&
                    absoluteBeat > loadedCue.CueMarkBeat &&
                    absoluteBeat <= loadedCue.CompleteBeat;
                var isCurrentBeat = currentPhraseBeat == phraseBeat;
                var isExecuting = isExecutionActive && isCurrentBeat;

                cells[cellIndex] = new LiveTimelineCell(
                    phraseBeat,
                    Array.IndexOf(cueMarks, phraseBeat) >= 0,
                    isLoadedImpact,
                    isLoadedImpact && loadedCue.IsLocked,
                    isLoadedImpact,
                    isRunway,
                    isTail,
                    isCurrentBeat,
                    isExecuting);
            }

            blocks[blockIndex] = new LiveTimelineBlock(startBeat, Array.AsReadOnly(cells));
        }

        return new LiveTimelinePhrase(
            true,
            sheet.PhraseLabel,
            sheet.PhraseLengthBeats,
            Array.AsReadOnly(blocks));
    }
}
