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
            loadedCue: SwitcherCueStatus.Empty));

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
            loadedCue: WrappedCue(isLocked: false)));
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
        Assert.That(model.LockBeatsUntil, Is.EqualTo(1));
        Assert.That(model.StartBeatsUntil, Is.EqualTo(2));
        Assert.That(model.EndBeatsUntil, Is.EqualTo(7));
    }

    /// <summary>The lower Grid becomes current when the live clock wraps from beat 16 to beat 1.</summary>
    [Test]
    public void GridBoundaryPromotesFollowingRow()
    {
        var before = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 116,
            currentGridBeat: 16,
            loadedCue: SwitcherCueStatus.Empty));
        var after = LiveTimelineProjection.Build(new LiveTimelineInput(
            isSynced: true,
            currentAbsoluteBeat: 117,
            currentGridBeat: 1,
            loadedCue: SwitcherCueStatus.Empty));

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
            loadedCue: WrappedCue(isLocked: true)));
        var current = model.Grids[0].Cells[14];

        Assert.That(model.IsActive, Is.True);
        Assert.That(model.StartBeatsUntil, Is.EqualTo(-1));
        Assert.That(model.EndBeatsUntil, Is.EqualTo(4));
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
            loadedCue: WrappedCue(isLocked: true)));
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
            loadedCue: cue));
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
            loadedCue: WrappedCue(isLocked: true)));

        Assert.That(model.Grids, Is.Empty);
        Assert.That(model.HasLoadedCue, Is.False);
        Assert.That(model.LockBeatsUntil, Is.Null);
        Assert.That(model.StartBeatsUntil, Is.Null);
        Assert.That(model.EndBeatsUntil, Is.Null);
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
            loadedCue: SwitcherCueStatus.Empty));

        Assert.That(model.CurrentPositionAvailable, Is.False);
        Assert.That(model.Grids, Is.Empty);
    }

    /// <summary>Inconsistent loaded timing is reported but never repaired into colored cells or countdowns.</summary>
    [Test]
    public void InconsistentLoadedCueTimingIsUnavailable()
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
            loadedCue: cue));

        Assert.That(model.HasLoadedCue, Is.True);
        Assert.That(model.LoadedCueTimingAvailable, Is.False);
        Assert.That(model.StartBeatsUntil, Is.Null);
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
