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
        int? currentGridBeat,
        SwitcherCueStatus activeCue,
        SwitcherCueStatus pendingCue,
        int? nextCueBeatsUntil = null,
        int? nextCueGridLengthBeats = null)
    {
        IsSynced = isSynced;
        CurrentGridBeat = currentGridBeat;
        ActiveCue = activeCue;
        PendingCue = pendingCue;
        NextCueBeatsUntil = nextCueBeatsUntil;
        NextCueGridLengthBeats = nextCueGridLengthBeats;
    }

    /// <summary>Whether the runtime has a usable live beat clock.</summary>
    public bool IsSynced { get; }

    /// <summary>The current one-based beat within the Grid, when available.</summary>
    public int? CurrentGridBeat { get; }

    /// <summary>The Switcher's currently executing Cue.</summary>
    public SwitcherCueStatus ActiveCue { get; }

    /// <summary>The Switcher's Loaded Cue waiting to start.</summary>
    public SwitcherCueStatus PendingCue { get; }

    /// <summary>Beats until the next Cue Mark carried by the current Cue Sheet.</summary>
    public int? NextCueBeatsUntil { get; }

    /// <summary>Length of the current Grid at the next Cue boundary, including an irregular short run-out.</summary>
    public int? NextCueGridLengthBeats { get; }
}

/// <summary>Display-ready availability and countdown facts for one Cue lifecycle slot.</summary>
internal readonly struct LiveCueTiming
{
    /// <summary>Creates one Cue timing snapshot without deriving runtime scheduling state.</summary>
    public LiveCueTiming(
        bool hasCue,
        bool isCueLocked,
        bool cueTimingAvailable,
        int? lockBeatsUntil,
        int? startBeatsUntil,
        int? endBeatsUntil)
    {
        HasCue = hasCue;
        IsCueLocked = isCueLocked;
        CueTimingAvailable = cueTimingAvailable;
        LockBeatsUntil = lockBeatsUntil;
        StartBeatsUntil = startBeatsUntil;
        EndBeatsUntil = endBeatsUntil;
    }

    /// <summary>Whether this lifecycle slot contains a Cue.</summary>
    public bool HasCue { get; }

    /// <summary>Whether the Switcher reports that this Cue is locked.</summary>
    public bool IsCueLocked { get; }

    /// <summary>Whether the Cue exposes one self-consistent timing window.</summary>
    public bool CueTimingAvailable { get; }

    /// <summary>Signed beats from now to the Cue's Lock Point.</summary>
    public int? LockBeatsUntil { get; }

    /// <summary>Signed beats from now to the Cue's Transition Start.</summary>
    public int? StartBeatsUntil { get; }

    /// <summary>Signed beats from now to the Cue's Transition End.</summary>
    public int? EndBeatsUntil { get; }
}

/// <summary>Display-ready rolling Grid rows plus separate Active and Pending Cue timing.</summary>
internal sealed class LiveTimelineModel
{
    /// <summary>Creates a model from already-resolved availability, lifecycle slots, and Grid rows.</summary>
    public LiveTimelineModel(
        bool isSynced,
        bool currentPositionAvailable,
        LiveCueTiming active,
        LiveCueTiming pending,
        int? nextCueBeatsUntil,
        IReadOnlyList<LiveTimelineGrid> grids)
    {
        IsSynced = isSynced;
        CurrentPositionAvailable = currentPositionAvailable;
        Active = active;
        Pending = pending;
        NextCueBeatsUntil = nextCueBeatsUntil;
        Grids = grids ?? throw new ArgumentNullException(nameof(grids));
    }

    /// <summary>Whether the runtime is in Synced Mode.</summary>
    public bool IsSynced { get; }

    /// <summary>Whether the live Grid beat identifies one current cell.</summary>
    public bool CurrentPositionAvailable { get; }

    /// <summary>The Cue whose Transition currently owns the stage.</summary>
    public LiveCueTiming Active { get; }

    /// <summary>The Loaded Cue waiting to start.</summary>
    public LiveCueTiming Pending { get; }

    /// <summary>Beats until the current Cue Sheet's next Cue Mark.</summary>
    public int? NextCueBeatsUntil { get; }

    /// <summary>The rolling current and immediately following Grid rows.</summary>
    public IReadOnlyList<LiveTimelineGrid> Grids { get; }
}

/// <summary>One complete 16-beat row in the rolling live window.</summary>
internal sealed class LiveTimelineGrid
{
    /// <summary>Creates one complete Grid row.</summary>
    public LiveTimelineGrid(IReadOnlyList<LiveTimelineCell> cells)
    {
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
    }

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
    /// <summary>Creates one display cell from Grid-relative timing facts.</summary>
    public LiveTimelineCell(
        int gridBeat,
        bool isLockPoint,
        bool isStart,
        bool isImpactPoint,
        bool isEnd,
        bool isRunway,
        bool isTail,
        bool isActiveTail,
        bool isCurrentBeat)
    {
        GridBeat = gridBeat;
        IsLockPoint = isLockPoint;
        IsStart = isStart;
        IsImpactPoint = isImpactPoint;
        IsEnd = isEnd;
        IsRunway = isRunway;
        IsTail = isTail;
        IsActiveTail = isActiveTail;
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

    /// <summary>Whether the currently executing Transition's Tail includes this beat.</summary>
    public bool IsActiveTail { get; }

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
            input.CurrentGridBeat is >= 1 and <= CueSheet.GridBeats;
        var activeCueUpcomingGridLength = FindActiveCueUpcomingGridLength(input);
        var activeCueIsUpcoming = activeCueUpcomingGridLength.HasValue;
        var pendingCueGridLength = PendingCueGridLength(input);
        var active = ProjectCueTiming(
            input.IsSynced,
            input.CurrentGridBeat,
            input.ActiveCue,
            activeCueIsUpcoming,
            activeCueUpcomingGridLength);
        var pending = ProjectCueTiming(
            input.IsSynced,
            input.CurrentGridBeat,
            input.PendingCue,
            isUpcoming: true,
            pendingCueGridLength);
        var activeCue = active.CueTimingAvailable
            ? input.ActiveCue
            : SwitcherCueStatus.Empty;
        var gridCue = pending.HasCue
            ? (pending.CueTimingAvailable ? input.PendingCue : SwitcherCueStatus.Empty)
            : activeCue;
        var gridCueIsUpcoming = pending.HasCue || activeCueIsUpcoming;
        var gridCueGridLength = pending.HasCue
            ? pendingCueGridLength
            : activeCueUpcomingGridLength ?? CueSheet.GridBeats;

        return new LiveTimelineModel(
            input.IsSynced,
            currentPositionAvailable,
            active,
            pending,
            input.IsSynced && input.NextCueBeatsUntil is >= 0
                ? input.NextCueBeatsUntil
                : null,
            BuildRollingGrids(
                input,
                currentPositionAvailable,
                gridCue,
                gridCueIsUpcoming,
                activeCue,
                activeCueIsUpcoming,
                gridCueGridLength));
    }

    /// <summary>Finds the next Cue Sheet mark from the BeatManager's current integer Phrase position.</summary>
    public static int? FindNextCueBeatsUntil(
        CueSheetView currentSheet,
        int? phraseLengthBeats,
        int? phraseBeatsRemaining)
    {
        var markOffset = FindNextCueMarkOffset(
            currentSheet,
            phraseLengthBeats,
            phraseBeatsRemaining,
            out var phrasePosition);
        return markOffset is { } offset ? offset - phrasePosition : null;
    }

    /// <summary>Finds the live Grid length that ends at the current Cue Sheet's next Cue Mark.</summary>
    public static int? FindNextCueGridLengthBeats(
        CueSheetView currentSheet,
        int? phraseLengthBeats,
        int? phraseBeatsRemaining)
    {
        var markOffset = FindNextCueMarkOffset(
            currentSheet,
            phraseLengthBeats,
            phraseBeatsRemaining,
            out _);
        if (markOffset is not { } offset)
        {
            return null;
        }

        var runOutBeats = offset % CueSheet.GridBeats;
        return runOutBeats == 0 ? CueSheet.GridBeats : runOutBeats;
    }

    /// <summary>Projects one Cue slot into honest availability and signed countdown facts.</summary>
    private static LiveCueTiming ProjectCueTiming(
        bool isSynced,
        int? currentGridBeat,
        SwitcherCueStatus cue,
        bool isUpcoming,
        int? nextCueGridLengthBeats)
    {
        var hasCue = isSynced && cue.HasCue;
        var cueTimingAvailable = hasCue && HasConsistentTiming(cue);
        int? lockBeatsUntil = null;
        int? startBeatsUntil = null;
        int? endBeatsUntil = null;
        if (cueTimingAvailable && currentGridBeat is >= 1 and <= CueSheet.GridBeats)
        {
            var impactBeatsUntil = isUpcoming
                ? CueGridLength(nextCueGridLengthBeats) - currentGridBeat.Value + 1
                : 1 - currentGridBeat.Value;
            lockBeatsUntil = impactBeatsUntil - cue.RunwayBeats - 1;
            startBeatsUntil = impactBeatsUntil - cue.RunwayBeats;
            endBeatsUntil = impactBeatsUntil + cue.TailBeats;
        }

        return new LiveCueTiming(
            hasCue,
            hasCue && cue.IsLocked,
            cueTimingAvailable,
            lockBeatsUntil,
            startBeatsUntil,
            endBeatsUntil);
    }

    /// <summary>Checks one Cue's timing facts without repairing or deriving them.</summary>
    private static bool HasConsistentTiming(SwitcherCueStatus cue)
    {
        return cue.CueMarkBeat >= 1 &&
            TransitionSettings.IsValidDuration(cue.RunwayBeats, cue.TailBeats) &&
            cue.LockPointBeat == cue.StartBeat - 1 &&
            cue.StartBeat == cue.CueMarkBeat - cue.RunwayBeats &&
            cue.CompleteBeat == cue.CueMarkBeat + cue.TailBeats;
    }

    /// <summary>Finds the nearest non-past Cue Mark and returns the validated current Phrase position.</summary>
    private static int? FindNextCueMarkOffset(
        CueSheetView currentSheet,
        int? phraseLengthBeats,
        int? phraseBeatsRemaining,
        out int phrasePosition)
    {
        phrasePosition = 0;
        if (!currentSheet.HasSheet ||
            phraseLengthBeats is not { } phraseLength ||
            phraseBeatsRemaining is not { } beatsRemaining ||
            phraseLength != currentSheet.PhraseLengthBeats ||
            phraseLength <= 0 ||
            beatsRemaining < 0 ||
            beatsRemaining > phraseLength)
        {
            return null;
        }

        phrasePosition = phraseLength - beatsRemaining;
        int? nearest = null;
        foreach (var markOffset in currentSheet.CueMarkOffsets)
        {
            if (markOffset >= phrasePosition && (nearest == null || markOffset < nearest.Value))
            {
                nearest = markOffset;
            }
        }

        return nearest;
    }

    /// <summary>Finds the Grid length carrying an active Runway, including an off-sheet starvation Cue.</summary>
    private static int? FindActiveCueUpcomingGridLength(LiveTimelineInput input)
    {
        if (!input.IsSynced ||
            !input.ActiveCue.HasCue ||
            input.PendingCue.HasCue ||
            input.CurrentGridBeat is not { } currentGridBeat ||
            currentGridBeat is < 1 or > CueSheet.GridBeats or 1)
        {
            return null;
        }

        var sheetGridLength = CueGridLength(input.NextCueGridLengthBeats);
        var sheetRunwayStart = sheetGridLength - input.ActiveCue.RunwayBeats + 1;
        if (input.NextCueBeatsUntil is { } nextCueBeatsUntil)
        {
            var followsSheetBoundary = nextCueBeatsUntil >= 0 &&
                nextCueBeatsUntil <= input.ActiveCue.RunwayBeats &&
                currentGridBeat >= sheetRunwayStart &&
                currentGridBeat <= sheetGridLength;
            if (followsSheetBoundary)
            {
                return sheetGridLength;
            }
        }

        var regularRunwayStart = CueSheet.GridBeats - input.ActiveCue.RunwayBeats + 1;
        return currentGridBeat >= regularRunwayStart
            ? CueSheet.GridBeats
            : null;
    }

    /// <summary>Chooses the sheet boundary only when the pending Cue is actually the sheet's next mark.</summary>
    private static int PendingCueGridLength(LiveTimelineInput input)
    {
        if (input.CurrentGridBeat is not { } currentGridBeat ||
            currentGridBeat is < 1 or > CueSheet.GridBeats)
        {
            return CueSheet.GridBeats;
        }

        var beatsUntilRegularBoundary = CueSheet.GridBeats - currentGridBeat + 1;
        if (input.NextCueBeatsUntil is { } beatsUntilSheetMark &&
            beatsUntilSheetMark >= 0 &&
            beatsUntilSheetMark <= beatsUntilRegularBoundary)
        {
            return CueGridLength(input.NextCueGridLengthBeats);
        }

        return CueSheet.GridBeats;
    }

    /// <summary>Uses a validated Cue boundary Grid length, falling back to one full Grid.</summary>
    private static int CueGridLength(int? nextCueGridLengthBeats)
    {
        return nextCueGridLengthBeats is >= 1 and <= CueSheet.GridBeats
            ? nextCueGridLengthBeats.Value
            : CueSheet.GridBeats;
    }

    /// <summary>Builds the current Grid and its immediate successor from the live Grid clock.</summary>
    private static IReadOnlyList<LiveTimelineGrid> BuildRollingGrids(
        LiveTimelineInput input,
        bool currentPositionAvailable,
        SwitcherCueStatus gridCue,
        bool gridCueIsUpcoming,
        SwitcherCueStatus activeCue,
        bool activeCueIsUpcoming,
        int gridCueGridLength)
    {
        if (!currentPositionAvailable ||
            input.CurrentGridBeat is not { } currentGridBeat)
        {
            return Array.Empty<LiveTimelineGrid>();
        }

        var grids = new LiveTimelineGrid[2];
        for (var gridIndex = 0; gridIndex < grids.Length; gridIndex++)
        {
            grids[gridIndex] = BuildGrid(
                gridIndex,
                currentGridBeat,
                gridCue,
                gridCueIsUpcoming,
                activeCue,
                activeCueIsUpcoming,
                gridCueGridLength);
        }

        return Array.AsReadOnly(grids);
    }

    /// <summary>Builds one complete Grid row with Transition geometry anchored only to the live Grid beat.</summary>
    private static LiveTimelineGrid BuildGrid(
        int gridIndex,
        int currentGridBeat,
        SwitcherCueStatus gridCue,
        bool gridCueIsUpcoming,
        SwitcherCueStatus activeCue,
        bool activeCueIsUpcoming,
        int nextCueGridLengthBeats)
    {
        var cueMarkWindowBeat = gridCueIsUpcoming ? CueSheet.GridBeats : 0;
        var startWindowBeat = gridCueIsUpcoming
            ? nextCueGridLengthBeats - gridCue.RunwayBeats
            : -gridCue.RunwayBeats;
        var runwayEndWindowBeat = gridCueIsUpcoming
            ? nextCueGridLengthBeats - 1
            : -1;
        var completeWindowBeat = cueMarkWindowBeat + gridCue.TailBeats;
        var activeCueMarkWindowBeat = activeCueIsUpcoming ? CueSheet.GridBeats : 0;
        var cellCount = gridIndex == 0 && gridCueIsUpcoming
            ? nextCueGridLengthBeats
            : CueSheet.GridBeats;
        var cells = new LiveTimelineCell[cellCount];
        for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
        {
            var gridBeat = cellIndex + 1;
            var windowBeat = gridIndex * CueSheet.GridBeats + cellIndex;
            var hasCue = gridCue.HasCue;
            var isImpactPoint = hasCue && windowBeat == cueMarkWindowBeat;
            var isRunway = hasCue &&
                windowBeat >= startWindowBeat &&
                windowBeat <= runwayEndWindowBeat;
            var isTail = hasCue &&
                windowBeat > cueMarkWindowBeat &&
                windowBeat <= completeWindowBeat;
            var isActiveTail = activeCue.HasCue &&
                windowBeat > activeCueMarkWindowBeat &&
                windowBeat <= activeCueMarkWindowBeat + activeCue.TailBeats;

            cells[cellIndex] = new LiveTimelineCell(
                gridBeat,
                hasCue && windowBeat == startWindowBeat - 1,
                hasCue && windowBeat == startWindowBeat,
                isImpactPoint,
                hasCue && windowBeat == completeWindowBeat,
                isRunway,
                isTail,
                isActiveTail,
                gridIndex == 0 && gridBeat == currentGridBeat);
        }

        return new LiveTimelineGrid(Array.AsReadOnly(cells));
    }
}
