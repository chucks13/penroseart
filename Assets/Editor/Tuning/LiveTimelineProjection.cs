// Projects live Transition timing into a rolling two-Grid editor model.
#nullable enable

using System;
using System.Collections.Generic;

/// <summary>Frame-coherent runtime facts required by the live Transition display.</summary>
internal readonly struct LiveTimelineInput
{
    /// <summary>Creates one input snapshot without deriving or repairing runtime timing.</summary>
    public LiveTimelineInput(
        bool isSynced,
        int? currentAbsoluteBeat,
        int? currentGridBeat,
        SwitcherCueStatus cue)
    {
        IsSynced = isSynced;
        CurrentAbsoluteBeat = currentAbsoluteBeat;
        CurrentGridBeat = currentGridBeat;
        Cue = cue;
    }

    /// <summary>Whether the runtime has a usable live beat clock.</summary>
    public bool IsSynced { get; }

    /// <summary>The current absolute beat reported by the wire, when available.</summary>
    public int? CurrentAbsoluteBeat { get; }

    /// <summary>The current one-based beat within the Grid, when available.</summary>
    public int? CurrentGridBeat { get; }

    /// <summary>The Switcher's pending or currently executing Cue.</summary>
    public SwitcherCueStatus Cue { get; }
}

/// <summary>Display-ready rolling Grid rows and Transition countdowns for one editor frame.</summary>
internal sealed class LiveTimelineModel
{
    /// <summary>Creates a model from already-resolved availability, rows, and countdown facts.</summary>
    public LiveTimelineModel(
        bool isSynced,
        bool currentPositionAvailable,
        bool hasCue,
        bool isCueLocked,
        bool cueTimingAvailable,
        IReadOnlyList<LiveTimelineGrid> grids,
        int? lockBeatsUntil,
        int? startBeatsUntil,
        int? endBeatsUntil)
    {
        IsSynced = isSynced;
        CurrentPositionAvailable = currentPositionAvailable;
        HasCue = hasCue;
        IsCueLocked = isCueLocked;
        CueTimingAvailable = cueTimingAvailable;
        Grids = grids ?? throw new ArgumentNullException(nameof(grids));
        LockBeatsUntil = lockBeatsUntil;
        StartBeatsUntil = startBeatsUntil;
        EndBeatsUntil = endBeatsUntil;
    }

    /// <summary>Whether the runtime is in Synced Mode.</summary>
    public bool IsSynced { get; }

    /// <summary>Whether live absolute and Grid beats identify one current cell.</summary>
    public bool CurrentPositionAvailable { get; }

    /// <summary>Whether the Switcher reports a pending or active Cue.</summary>
    public bool HasCue { get; }

    /// <summary>Whether the Switcher reports that the Loaded Cue is locked.</summary>
    public bool IsCueLocked { get; }

    /// <summary>Whether the Cue exposes one self-consistent timing window.</summary>
    public bool CueTimingAvailable { get; }

    /// <summary>The rolling current and immediately following Grid rows.</summary>
    public IReadOnlyList<LiveTimelineGrid> Grids { get; }

    /// <summary>Signed beats from now to the Cue's Lock Point.</summary>
    public int? LockBeatsUntil { get; }

    /// <summary>Signed beats from now to the Cue's Transition Start.</summary>
    public int? StartBeatsUntil { get; }

    /// <summary>Signed beats from now to the Cue's Transition End.</summary>
    public int? EndBeatsUntil { get; }

    /// <summary>Whether the live beat lies from Transition Start through End.</summary>
    public bool IsActive =>
        CueTimingAvailable &&
        StartBeatsUntil is <= 0 &&
        EndBeatsUntil is >= 0;
}

/// <summary>One complete 16-beat row in the rolling live window.</summary>
internal sealed class LiveTimelineGrid
{
    /// <summary>Creates one Grid row anchored to its first absolute beat.</summary>
    public LiveTimelineGrid(int startAbsoluteBeat, IReadOnlyList<LiveTimelineCell> cells)
    {
        StartAbsoluteBeat = startAbsoluteBeat;
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
    }

    /// <summary>The absolute beat represented by the first cell.</summary>
    public int StartAbsoluteBeat { get; }

    /// <summary>The Grid's ordered beat cells.</summary>
    public IReadOnlyList<LiveTimelineCell> Cells { get; }
}

/// <summary>Semantic background fill for one live Grid cell.</summary>
internal enum LiveTimelineFill
{
    /// <summary>Neutral Grid background.</summary>
    Base,

    /// <summary>Transition Runway from Start until the Impact Point.</summary>
    Runway,

    /// <summary>Transition Tail after the Impact Point through End.</summary>
    Tail,

    /// <summary>The last mutable beat before Transition Start.</summary>
    LockPoint,

    /// <summary>The Transition Impact Point between Runway and Tail.</summary>
    ImpactPoint,

    /// <summary>Live current-beat fill, which overrides every timing color.</summary>
    CurrentBeat,
}

/// <summary>One beat cell with independent Transition markers and resolved color precedence.</summary>
internal readonly struct LiveTimelineCell
{
    /// <summary>Creates one display cell from absolute timing facts.</summary>
    public LiveTimelineCell(
        int gridBeat,
        int absoluteBeat,
        bool isLockPoint,
        bool isStart,
        bool isImpactPoint,
        bool isEnd,
        bool isRunway,
        bool isTail,
        bool isCurrentBeat)
    {
        GridBeat = gridBeat;
        AbsoluteBeat = absoluteBeat;
        IsLockPoint = isLockPoint;
        IsStart = isStart;
        IsImpactPoint = isImpactPoint;
        IsEnd = isEnd;
        IsRunway = isRunway;
        IsTail = isTail;
        IsCurrentBeat = isCurrentBeat;
        Fill = isCurrentBeat
            ? LiveTimelineFill.CurrentBeat
            : isLockPoint
                ? LiveTimelineFill.LockPoint
                : isImpactPoint
                    ? LiveTimelineFill.ImpactPoint
                    : isRunway
                        ? LiveTimelineFill.Runway
                        : isTail
                            ? LiveTimelineFill.Tail
                            : LiveTimelineFill.Base;
    }

    /// <summary>The one-based beat within this Grid row.</summary>
    public int GridBeat { get; }

    /// <summary>The absolute wire beat represented by this cell.</summary>
    public int AbsoluteBeat { get; }

    /// <summary>Whether this beat is the loaded Cue's Lock Point.</summary>
    public bool IsLockPoint { get; }

    /// <summary>Whether this beat starts the Transition Runway.</summary>
    public bool IsStart { get; }

    /// <summary>Whether this beat is the Transition Impact Point.</summary>
    public bool IsImpactPoint { get; }

    /// <summary>Whether this beat ends the Transition Tail.</summary>
    public bool IsEnd { get; }

    /// <summary>Whether this beat lies in the Transition Runway.</summary>
    public bool IsRunway { get; }

    /// <summary>Whether this beat lies in the Transition Tail.</summary>
    public bool IsTail { get; }

    /// <summary>Whether this is the live current beat.</summary>
    public bool IsCurrentBeat { get; }

    /// <summary>The resolved background fill after current-beat and boundary precedence.</summary>
    public LiveTimelineFill Fill { get; }
}

/// <summary>Builds a rolling two-Grid view without owning runtime scheduling policy.</summary>
internal static class LiveTimelineProjection
{
    /// <summary>Projects one runtime snapshot into two adjacent Grid rows and signed timing deltas.</summary>
    public static LiveTimelineModel Build(LiveTimelineInput input)
    {
        var currentPositionAvailable =
            input.IsSynced &&
            input.CurrentAbsoluteBeat is >= 1 &&
            input.CurrentGridBeat is >= 1 and <= CueSheet.GridBeats;
        var hasCue = input.IsSynced && input.Cue.HasCue;
        var cueTimingAvailable = hasCue && HasConsistentTiming(input.Cue);
        var cue = cueTimingAvailable
            ? input.Cue
            : SwitcherCueStatus.Empty;

        int? lockBeatsUntil = null;
        int? startBeatsUntil = null;
        int? endBeatsUntil = null;
        if (cueTimingAvailable && input.CurrentAbsoluteBeat is { } currentBeat)
        {
            lockBeatsUntil = cue.LockPointBeat - currentBeat;
            startBeatsUntil = cue.StartBeat - currentBeat;
            endBeatsUntil = cue.CompleteBeat - currentBeat;
        }

        return new LiveTimelineModel(
            input.IsSynced,
            currentPositionAvailable,
            hasCue,
            hasCue && input.Cue.IsLocked,
            cueTimingAvailable,
            BuildRollingGrids(input, currentPositionAvailable, cue),
            lockBeatsUntil,
            startBeatsUntil,
            endBeatsUntil);
    }

    /// <summary>Checks the Loaded Cue's existing timing facts without repairing or deriving them.</summary>
    private static bool HasConsistentTiming(SwitcherCueStatus cue)
    {
        return cue.CueMarkBeat >= 1 &&
            cue.RunwayBeats >= 0 &&
            cue.TailBeats >= 0 &&
            cue.LockPointBeat == cue.StartBeat - 1 &&
            cue.StartBeat == cue.CueMarkBeat - cue.RunwayBeats &&
            cue.CompleteBeat == cue.CueMarkBeat + cue.TailBeats;
    }

    /// <summary>Builds the current Grid and its immediate successor from the live Grid clock.</summary>
    private static IReadOnlyList<LiveTimelineGrid> BuildRollingGrids(
        LiveTimelineInput input,
        bool currentPositionAvailable,
        SwitcherCueStatus loadedCue)
    {
        if (!currentPositionAvailable ||
            input.CurrentAbsoluteBeat is not { } currentBeat ||
            input.CurrentGridBeat is not { } currentGridBeat)
        {
            return Array.Empty<LiveTimelineGrid>();
        }

        var currentGridStart = currentBeat - currentGridBeat + 1;
        var grids = new LiveTimelineGrid[2];
        for (var gridIndex = 0; gridIndex < grids.Length; gridIndex++)
        {
            var gridStart = currentGridStart + gridIndex * CueSheet.GridBeats;
            grids[gridIndex] = BuildGrid(gridStart, currentBeat, loadedCue);
        }

        return Array.AsReadOnly(grids);
    }

    /// <summary>Builds one complete Grid row with absolute Transition timing projected onto its cells.</summary>
    private static LiveTimelineGrid BuildGrid(
        int gridStart,
        int currentBeat,
        SwitcherCueStatus loadedCue)
    {
        var cells = new LiveTimelineCell[CueSheet.GridBeats];
        for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            var gridBeat = cellIndex + 1;
            var absoluteBeat = gridStart + cellIndex;
            var hasCue = loadedCue.HasCue;
            var isImpactPoint = hasCue && absoluteBeat == loadedCue.CueMarkBeat;
            var isRunway = hasCue &&
                absoluteBeat >= loadedCue.StartBeat &&
                absoluteBeat < loadedCue.CueMarkBeat;
            var isTail = hasCue &&
                absoluteBeat > loadedCue.CueMarkBeat &&
                absoluteBeat <= loadedCue.CompleteBeat;

            cells[cellIndex] = new LiveTimelineCell(
                gridBeat,
                absoluteBeat,
                hasCue && absoluteBeat == loadedCue.LockPointBeat,
                hasCue && absoluteBeat == loadedCue.StartBeat,
                isImpactPoint,
                hasCue && absoluteBeat == loadedCue.CompleteBeat,
                isRunway,
                isTail,
                absoluteBeat == currentBeat);
        }

        return new LiveTimelineGrid(gridStart, Array.AsReadOnly(cells));
    }
}
