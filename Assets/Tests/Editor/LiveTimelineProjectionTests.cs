// Tests the deterministic rolling live Transition projection.
#nullable enable

using NUnit.Framework;

/// <summary>Behavior tests for the two-Grid live Transition projection seam.</summary>
public sealed class LiveTimelineProjectionTests
{
    /// <summary>The next Cue countdown follows the current Cue Sheet's phrase-relative mark positions.</summary>
    [TestCase(96, 80, 48, 32)]
    [TestCase(128, 128, 64, 64)]
    [TestCase(96, 48, 48, 0)]
    public void NextCueCountdownComesFromCurrentCueSheetPosition(
        int phraseLength,
        int phraseBeatsRemaining,
        int nextMarkOffset,
        int expectedBeatsUntil)
    {
        var sheet = new CueSheetView(true, phraseLength, "phrase", new[] { nextMarkOffset, phraseLength });

        var beatsUntil = LiveTimelineProjection.FindNextCueBeatsUntil(
            sheet,
            phraseLength,
            phraseBeatsRemaining);

        Assert.That(beatsUntil, Is.EqualTo(expectedBeatsUntil));
    }

    /// <summary>A stale Cue Sheet cannot produce a plausible countdown for a different Phrase.</summary>
    [Test]
    public void NextCueCountdownRejectsMismatchedPhraseIdentity()
    {
        var sheet = new CueSheetView(true, 96, "old phrase", new[] { 48, 96 });

        Assert.That(
            LiveTimelineProjection.FindNextCueBeatsUntil(sheet, 80, 64),
            Is.Null);
    }

    /// <summary>An irregular Phrase end shortens the current row before Impact wraps to the next row.</summary>
    [Test]
    public void IrregularPhraseBoundaryPlacesRunwayAtTheActualShortGridEnd()
    {
        var sheet = new CueSheetView(true, 24, "short ending", new[] { 24 });
        var nextCueGridLength = LiveTimelineProjection.FindNextCueGridLengthBeats(sheet, 24, 4);
        var cue = new SwitcherCueStatus(
            true,
            false,
            109,
            2,
            1,
            105,
            106,
            111,
            3,
            2);

        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 5,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: cue,
            nextCueBeatsUntil: 4,
            nextCueGridLengthBeats: nextCueGridLength));

        Assert.That(nextCueGridLength, Is.EqualTo(8));
        Assert.That(model.Grids[0].Cells[4].IsLockPoint, Is.True);
        Assert.That(model.Grids[0].Cells[5].IsStart, Is.True);
        Assert.That(model.Grids[0].Cells[5].IsRunway, Is.True);
        Assert.That(model.Grids[0].Cells[7].IsRunway, Is.True);
        Assert.That(model.Grids[0].Cells, Has.Count.EqualTo(8));
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(model.Grids[1].Cells[1].IsTail, Is.True);
        Assert.That(model.Grids[1].Cells[2].IsEnd, Is.True);
        Assert.That(model.Pending.LockBeatsUntil, Is.Zero);
        Assert.That(model.Pending.StartBeatsUntil, Is.EqualTo(1));
        Assert.That(model.Pending.EndBeatsUntil, Is.EqualTo(6));
    }

    /// <summary>An irregular Runway keeps the same shortened Grid geometry after Loaded becomes Active.</summary>
    [Test]
    public void IrregularRunwayRemainsAnchoredAfterCueBecomesActive()
    {
        var cue = new SwitcherCueStatus(
            true,
            true,
            109,
            2,
            1,
            105,
            106,
            111,
            3,
            2);

        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 6,
            activeCue: cue,
            pendingCue: SwitcherCueStatus.Empty,
            nextCueBeatsUntil: 3,
            nextCueGridLengthBeats: 8));

        Assert.That(model.Grids[0].Cells[4].IsLockPoint, Is.True);
        Assert.That(model.Grids[0].Cells[5].IsStart, Is.True);
        Assert.That(model.Grids[0].Cells[7].IsRunway, Is.True);
        Assert.That(model.Grids[0].Cells, Has.Count.EqualTo(8));
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(model.Grids[1].Cells[1].IsTail, Is.True);
        Assert.That(model.Active.StartBeatsUntil, Is.Zero);
        Assert.That(model.Active.EndBeatsUntil, Is.EqualTo(5));
    }

    /// <summary>An off-sheet starvation Cue uses the next regular Grid, not a later irregular sheet mark.</summary>
    [Test]
    public void StarvationPendingCueIgnoresLaterSheetBoundaryGeometry()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: false),
            nextCueBeatsUntil: 20,
            nextCueGridLengthBeats: 8));

        Assert.That(model.Grids[0].Cells, Has.Count.EqualTo(CueSheet.GridBeats));
        Assert.That(model.Grids[0].Cells[12].IsLockPoint, Is.True);
        Assert.That(model.Grids[0].Cells[13].IsStart, Is.True);
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(model.Pending.StartBeatsUntil, Is.EqualTo(2));
    }

    /// <summary>The same off-sheet starvation Cue stays on that regular Grid after Loaded becomes Active.</summary>
    [Test]
    public void StarvationRunwayRemainsAnchoredAfterCueBecomesActive()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 14,
            activeCue: WrappedCue(isLocked: true),
            pendingCue: SwitcherCueStatus.Empty,
            nextCueBeatsUntil: 18,
            nextCueGridLengthBeats: 8));

        Assert.That(model.Grids[0].Cells, Has.Count.EqualTo(CueSheet.GridBeats));
        Assert.That(model.Grids[0].Cells[13].IsStart, Is.True);
        Assert.That(model.Grids[0].Cells[13].IsCurrentBeat, Is.True);
        Assert.That(model.Grids[0].Cells[15].IsRunway, Is.True);
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(model.Active.StartBeatsUntil, Is.Zero);
        Assert.That(model.Active.EndBeatsUntil, Is.EqualTo(5));
    }

    /// <summary>The live projection exposes only the current Grid and its immediate successor.</summary>
    [Test]
    public void RollingWindowProjectsCurrentAndFollowingGrid()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 14,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));

        Assert.That(model.Grids, Has.Count.EqualTo(2));
        Assert.That(model.Grids[0].Cells, Has.Count.EqualTo(CueSheet.GridBeats));
        Assert.That(model.Grids[1].Cells, Has.Count.EqualTo(CueSheet.GridBeats));
        Assert.That(model.Grids[0].Cells[13].IsCurrentBeat, Is.True);
    }

    /// <summary>Transition timing keeps Lock, Start, Impact, and End visible across the Grid wrap.</summary>
    [Test]
    public void RollingWindowProjectsTransitionBoundariesAcrossWrap()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: false)));
        var lockPoint = model.Grids[0].Cells[12];
        var start = model.Grids[0].Cells[13];
        var impact = model.Grids[1].Cells[0];
        var tail = model.Grids[1].Cells[1];
        var end = model.Grids[1].Cells[2];

        Assert.That(lockPoint.IsLockPoint, Is.True);
        Assert.That(lockPoint.Fill, Is.EqualTo(LiveTimelineFill.LockPoint));
        Assert.That(start.IsStart, Is.True);
        Assert.That(start.IsRunway, Is.True);
        Assert.That(start.Fill, Is.EqualTo(LiveTimelineFill.Runway));
        Assert.That(impact.IsImpactPoint, Is.True);
        Assert.That(impact.Fill, Is.EqualTo(LiveTimelineFill.ImpactPoint));
        Assert.That(tail.IsTail, Is.True);
        Assert.That(end.IsEnd, Is.True);
        Assert.That(end.IsTail, Is.True);
        Assert.That(model.Pending.LockBeatsUntil, Is.EqualTo(1));
        Assert.That(model.Pending.StartBeatsUntil, Is.EqualTo(2));
        Assert.That(model.Pending.EndBeatsUntil, Is.EqualTo(7));
    }

    /// <summary>Advancing the Grid moves only Current while Cue geometry remains fixed around the wrap.</summary>
    [Test]
    public void GridBeatChangesCurrentCellWithoutMovingCueGeometry()
    {
        var gridBeat12 = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: false)));
        var gridBeat13 = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 13,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: false)));

        Assert.That(gridBeat12.Grids[0].Cells[11].IsCurrentBeat, Is.True);
        Assert.That(gridBeat13.Grids[0].Cells[12].IsCurrentBeat, Is.True);
        Assert.That(gridBeat12.Grids[0].Cells[12].IsLockPoint, Is.True);
        Assert.That(gridBeat13.Grids[0].Cells[12].IsLockPoint, Is.True);
        Assert.That(gridBeat12.Grids[0].Cells[13].IsStart, Is.True);
        Assert.That(gridBeat13.Grids[0].Cells[13].IsStart, Is.True);
        Assert.That(gridBeat12.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(gridBeat13.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(gridBeat12.Pending.LockBeatsUntil, Is.EqualTo(1));
        Assert.That(gridBeat13.Pending.LockBeatsUntil, Is.Zero);
    }

    /// <summary>An executing Tail and the next Loaded Cue remain visible together without a mid-Grid handoff.</summary>
    [Test]
    public void ActiveTailAndPendingCueProjectIntoTheSameRollingWindow()
    {
        var activeCue = new SwitcherCueStatus(
            true,
            true,
            101,
            1,
            0,
            99,
            100,
            105,
            1,
            4);
        var pendingCue = new SwitcherCueStatus(
            true,
            false,
            117,
            2,
            1,
            112,
            113,
            117,
            4,
            0);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 1,
            activeCue: activeCue,
            pendingCue: pendingCue));

        Assert.That(model.Active.HasCue, Is.True);
        Assert.That(model.Pending.HasCue, Is.True);
        Assert.That(model.Grids[0].Cells[1].IsActiveTail, Is.True, "The executing Tail remains on beat 102.");
        Assert.That(model.Grids[0].Cells[11].IsLockPoint, Is.True, "The pending Lock remains on Grid beat 12.");
        Assert.That(model.Grids[0].Cells[12].IsRunway, Is.True, "The pending Runway begins on Grid beat 13.");
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.True, "The pending Impact remains at the Grid wrap.");
    }

    /// <summary>The lower Grid becomes current when the live clock wraps from beat 16 to beat 1.</summary>
    [Test]
    public void GridBoundaryPromotesFollowingRow()
    {
        var before = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 16,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));
        var after = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 1,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));

        Assert.That(before.Grids[0].Cells[15].IsCurrentBeat, Is.True);
        Assert.That(after.Grids[0].Cells[0].IsCurrentBeat, Is.True);
    }

    /// <summary>An active Runway keeps its marker while the current beat wins the fill and End remains countable.</summary>
    [Test]
    public void ActiveTransitionCountsToEndWithCurrentBeatPrecedence()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 15,
            activeCue: WrappedCue(isLocked: true),
            pendingCue: SwitcherCueStatus.Empty));
        var current = model.Grids[0].Cells[14];

        Assert.That(model.Active.HasCue, Is.True);
        Assert.That(model.Active.StartBeatsUntil, Is.EqualTo(-1));
        Assert.That(model.Active.EndBeatsUntil, Is.EqualTo(4));
        Assert.That(current.IsRunway, Is.True);
        Assert.That(current.IsCurrentBeat, Is.True);
        Assert.That(current.Fill, Is.EqualTo(LiveTimelineFill.CurrentBeat));
    }

    /// <summary>The yellow current beat wins at Impact without erasing the Impact marker.</summary>
    [Test]
    public void CurrentBeatOverridesImpactColorWithoutRemovingMarker()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 1,
            activeCue: WrappedCue(isLocked: true),
            pendingCue: SwitcherCueStatus.Empty));
        var current = model.Grids[0].Cells[0];

        Assert.That(current.IsImpactPoint, Is.True);
        Assert.That(current.IsCurrentBeat, Is.True);
        Assert.That(current.Fill, Is.EqualTo(LiveTimelineFill.CurrentBeat));
    }

    /// <summary>A hard cut keeps coincident Start, Impact, and End markers without inventing Runway or Tail beats.</summary>
    [Test]
    public void ZeroDurationTransitionKeepsCoincidentBoundaryMarkers()
    {
        var cue = new SwitcherCueStatus(
            true,
            true,
            117,
            2,
            1,
            116,
            117,
            117,
            0,
            0);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 16,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: cue));
        var boundary = model.Grids[1].Cells[0];

        Assert.That(boundary.IsStart, Is.True);
        Assert.That(boundary.IsImpactPoint, Is.True);
        Assert.That(boundary.IsEnd, Is.True);
        Assert.That(boundary.IsRunway, Is.False);
        Assert.That(boundary.IsTail, Is.False);
        Assert.That(boundary.Fill, Is.EqualTo(LiveTimelineFill.ImpactPoint));
    }

    /// <summary>Standalone Mode suppresses stale clock and Cue facts instead of displaying plausible timing.</summary>
    [Test]
    public void StandaloneSuppressesRollingWindowAndCountdowns()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: false,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: true)));

        Assert.That(model.Grids, Is.Empty);
        Assert.That(model.Pending.HasCue, Is.False);
        Assert.That(model.Pending.LockBeatsUntil, Is.Null);
        Assert.That(model.Pending.StartBeatsUntil, Is.Null);
        Assert.That(model.Pending.EndBeatsUntil, Is.Null);
        Assert.That(model.NextCueBeatsUntil, Is.Null);
    }

    /// <summary>An invalid Grid position makes the rolling rows unavailable.</summary>
    [TestCase(null)]
    [TestCase(0)]
    [TestCase(17)]
    public void InvalidGridPositionSuppressesRollingRows(int? gridBeat)
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: gridBeat,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));

        Assert.That(model.CurrentPositionAvailable, Is.False);
        Assert.That(model.Grids, Is.Empty);
    }

    /// <summary>Inconsistent Cue timing is reported but never repaired into colored cells or countdowns.</summary>
    [Test]
    public void InconsistentCueTimingIsUnavailable()
    {
        var cue = new SwitcherCueStatus(
            true,
            false,
            117,
            2,
            1,
            113,
            115,
            119,
            3,
            2);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: cue));

        Assert.That(model.Pending.HasCue, Is.True);
        Assert.That(model.Pending.CueTimingAvailable, Is.False);
        Assert.That(model.Pending.StartBeatsUntil, Is.Null);
        Assert.That(model.Grids[0].Cells[13].IsStart, Is.False);
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.False);
    }

    /// <summary>The authoring preview reuses live placement while applying the saved Runway and Tail.</summary>
    [Test]
    public void TimingPreviewProjectsSavedTimingOntoTheLoadedCue()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: false));

        var available = LiveTimelineProjection.TryBuildTimingPreview(
            input,
            runwayBeats: 1,
            tailBeats: 4,
            out var preview);

        Assert.That(available, Is.True);
        Assert.That(preview.Grids[0].Cells[15].IsStart, Is.True);
        Assert.That(preview.Grids[0].Cells[15].IsRunway, Is.True);
        Assert.That(preview.Grids[1].Cells[0].IsImpactPoint, Is.True);
        Assert.That(preview.Grids[1].Cells[4].IsTail, Is.True);
        Assert.That(preview.Grids[1].Cells[4].IsEnd, Is.True);
    }

    /// <summary>A hard-cut authoring preview keeps Start, Impact Point, and End on one real Cue Mark.</summary>
    [Test]
    public void TimingPreviewRepresentsHardCutsWithoutInventingDuration()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 16,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: true));

        var available = LiveTimelineProjection.TryBuildTimingPreview(
            input,
            runwayBeats: 0,
            tailBeats: 0,
            out var preview);
        var boundary = preview.Grids[1].Cells[0];

        Assert.That(available, Is.True);
        Assert.That(boundary.IsStart, Is.True);
        Assert.That(boundary.IsImpactPoint, Is.True);
        Assert.That(boundary.IsEnd, Is.True);
        Assert.That(boundary.IsRunway, Is.False);
        Assert.That(boundary.IsTail, Is.False);
    }

    /// <summary>The authoring preview preserves the live model's current-beat visual precedence.</summary>
    [Test]
    public void TimingPreviewKeepsCurrentBeatAboveRunwayFill()
    {
        var input = new LiveTimelineInput(
            isSynced: true,
            currentGridBeat: 16,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: false));

        var available = LiveTimelineProjection.TryBuildTimingPreview(
            input,
            runwayBeats: 3,
            tailBeats: 2,
            out var preview);
        var currentBeat = preview.Grids[0].Cells[15];

        Assert.That(available, Is.True);
        Assert.That(currentBeat.IsRunway, Is.True);
        Assert.That(currentBeat.IsCurrentBeat, Is.True);
        Assert.That(currentBeat.Fill, Is.EqualTo(LiveTimelineFill.CurrentBeat));
    }

    /// <summary>Missing sync, Grid, or Cue placement leaves the timing preview unavailable.</summary>
    [Test]
    public void TimingPreviewDoesNotInventMissingRuntimePlacement()
    {
        Assert.That(
            LiveTimelineProjection.TryBuildTimingPreview(
                new LiveTimelineInput(false, 8, SwitcherCueStatus.Empty, WrappedCue(false)),
                3,
                2,
                out _),
            Is.False);
        Assert.That(
            LiveTimelineProjection.TryBuildTimingPreview(
                new LiveTimelineInput(true, null, SwitcherCueStatus.Empty, WrappedCue(false)),
                3,
                2,
                out _),
            Is.False);
        Assert.That(
            LiveTimelineProjection.TryBuildTimingPreview(
                new LiveTimelineInput(true, 8, SwitcherCueStatus.Empty, SwitcherCueStatus.Empty),
                3,
                2,
                out _),
            Is.False);
    }

    /// <summary>Returns one valid Cue whose Runway ends and Tail begins across a Grid boundary.</summary>
    private static SwitcherCueStatus WrappedCue(bool isLocked)
    {
        return new SwitcherCueStatus(
            true,
            isLocked,
            117,
            2,
            1,
            113,
            114,
            119,
            3,
            2);
    }
}
