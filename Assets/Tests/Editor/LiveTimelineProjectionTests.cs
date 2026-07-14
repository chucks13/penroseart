// Tests the deterministic rolling live Transition projection.
#nullable enable

using NUnit.Framework;

/// <summary>Behavior tests for the two-Grid live Transition projection seam.</summary>
public sealed class LiveTimelineProjectionTests
{
    /// <summary>The live projection exposes only the current Grid and its immediate successor.</summary>
    [Test]
    public void RollingWindowProjectsCurrentAndFollowingGrid()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 114,
            currentGridBeat: 14,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));

        Assert.That(model.Grids, Has.Count.EqualTo(2));
        Assert.That(model.Grids[0].StartAbsoluteBeat, Is.EqualTo(101));
        Assert.That(model.Grids[1].StartAbsoluteBeat, Is.EqualTo(117));
        Assert.That(model.Grids[0].Cells[13].IsCurrentBeat, Is.True);
    }

    /// <summary>Transition timing keeps Lock, Start, Impact, and End visible across the Grid wrap.</summary>
    [Test]
    public void RollingWindowProjectsTransitionBoundariesAcrossWrap()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 112,
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
            116,
            2,
            1,
            111,
            112,
            116,
            4,
            0);
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 101,
            currentGridBeat: 1,
            activeCue: activeCue,
            pendingCue: pendingCue));

        Assert.That(model.Active.HasCue, Is.True);
        Assert.That(model.Pending.HasCue, Is.True);
        Assert.That(model.Grids[0].Cells[1].IsActiveTail, Is.True, "The executing Tail remains on beat 102.");
        Assert.That(model.Grids[0].Cells[10].IsLockPoint, Is.True, "The pending Lock remains on beat 111.");
        Assert.That(model.Grids[0].Cells[11].IsRunway, Is.True, "The pending Runway begins on beat 112.");
        Assert.That(model.Grids[0].Cells[15].IsImpactPoint, Is.True, "The pending Impact remains on beat 116.");
    }

    /// <summary>The lower Grid becomes current when the live clock wraps from beat 16 to beat 1.</summary>
    [Test]
    public void GridBoundaryPromotesFollowingRow()
    {
        var before = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 116,
            currentGridBeat: 16,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));
        var after = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 117,
            currentGridBeat: 1,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: SwitcherCueStatus.Empty));

        Assert.That(before.Grids[1].StartAbsoluteBeat, Is.EqualTo(117));
        Assert.That(after.Grids[0].StartAbsoluteBeat, Is.EqualTo(117));
        Assert.That(after.Grids[1].StartAbsoluteBeat, Is.EqualTo(133));
        Assert.That(after.Grids[0].Cells[0].IsCurrentBeat, Is.True);
    }

    /// <summary>An active Runway keeps its marker while the current beat wins the fill and End remains countable.</summary>
    [Test]
    public void ActiveTransitionCountsToEndWithCurrentBeatPrecedence()
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 115,
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
            currentAbsoluteBeat: 117,
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
            currentAbsoluteBeat: 116,
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
            currentAbsoluteBeat: 112,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: WrappedCue(isLocked: true)));

        Assert.That(model.Grids, Is.Empty);
        Assert.That(model.Pending.HasCue, Is.False);
        Assert.That(model.Pending.LockBeatsUntil, Is.Null);
        Assert.That(model.Pending.StartBeatsUntil, Is.Null);
        Assert.That(model.Pending.EndBeatsUntil, Is.Null);
    }

    /// <summary>An invalid Grid position makes the rolling rows unavailable.</summary>
    [TestCase(null)]
    [TestCase(0)]
    [TestCase(17)]
    public void InvalidGridPositionSuppressesRollingRows(int? gridBeat)
    {
        var model = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 112,
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
            currentAbsoluteBeat: 112,
            currentGridBeat: 12,
            activeCue: SwitcherCueStatus.Empty,
            pendingCue: cue));

        Assert.That(model.Pending.HasCue, Is.True);
        Assert.That(model.Pending.CueTimingAvailable, Is.False);
        Assert.That(model.Pending.StartBeatsUntil, Is.Null);
        Assert.That(model.Grids[0].Cells[13].IsStart, Is.False);
        Assert.That(model.Grids[1].Cells[0].IsImpactPoint, Is.False);
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
