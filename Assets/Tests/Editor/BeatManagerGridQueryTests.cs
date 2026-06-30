#nullable enable

using NUnit.Framework;

/// <summary>
/// Pins the effect-facing 16-beat Grid query (<see cref="BeatManager.Grid"/> => <see cref="GridInfo"/>?).
/// The Director is the single writer via <see cref="BeatManager.PublishGrid"/>; these tests drive that
/// seam directly to fix the facade contract: the null rules (off the grid), the 1..16 Count, the 0..1
/// Progress enrichment, Confidence passthrough, and that the facade never latches — it mirrors whatever was
/// last published, so a beat rewind within a phrase self-corrects.
/// </summary>
public sealed class BeatManagerGridQueryTests
{
    /// <summary>
    /// A live-sourced BeatManager whose intra-beat fraction is exactly 0.5 (250 ms into a 500 ms beat),
    /// matching the BarPhase fixture the other rhythm-query tests pin. The Progress derivation reads it.
    /// </summary>
    private static BeatManager CreateLiveBeatManager()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.snapshot.beatInBar = 1;
        beatManager.beatData.snapshot.beatAverageMs = 500;
        beatManager.beatData.snapshot.beatsCountMs = new[] { 0, 250, 750, 1250 };
        return beatManager;
    }

    [Test]
    public void GridIsNullBeforeAnyPublish()
    {
        var beatManager = new BeatManager();

        Assert.That(beatManager.Grid, Is.Null);
    }

    [Test]
    public void GridIsNullWhenTheDirectorPublishesNone()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.PublishGrid(GridReading.None);

        Assert.That(beatManager.Grid, Is.Null);
    }

    [Test]
    public void GridIsNullOnTheStandaloneFloorEvenWithAGridPosition()
    {
        // StandAloneFloor is a mode exit: the clock is gone, so there is no Grid even when a stale grid
        // position still rides along in the reading.
        var beatManager = CreateLiveBeatManager();
        beatManager.PublishGrid(new GridReading(0, 5, GridSyncState.Coasting, standAloneFloor: true));

        Assert.That(beatManager.Grid, Is.Null);
    }

    [Test]
    public void GridSurfacesCountConfidenceAndProgress()
    {
        var beatManager = CreateLiveBeatManager();
        beatManager.PublishGrid(new GridReading(0, 5, GridSyncState.Locked, standAloneFloor: false));

        var grid = beatManager.Grid;

        Assert.That(grid, Is.Not.Null);
        Assert.That(grid!.Value.Confidence, Is.EqualTo(GridSyncState.Locked));
        Assert.That(grid.Value.Count, Is.EqualTo(5));
        // (5 - 1 + 0.5 intra-beat) / 16
        Assert.That(grid.Value.Progress, Is.EqualTo(0.28125f).Within(0.0001f));
    }

    [Test]
    public void CoastingAndContradictedAreInGridReadings()
    {
        // All three GridSyncState values are on-grid readings with a valid Count; they differ only in trust.
        var beatManager = CreateLiveBeatManager();

        beatManager.PublishGrid(new GridReading(0, 8, GridSyncState.Coasting, false));
        Assert.That(beatManager.Grid?.Confidence, Is.EqualTo(GridSyncState.Coasting));

        beatManager.PublishGrid(new GridReading(0, 8, GridSyncState.Contradicted, false));
        Assert.That(beatManager.Grid?.Confidence, Is.EqualTo(GridSyncState.Contradicted));
    }

    [Test]
    public void CountStaysWithinTheSixteenBeatGridAndProgressWithinUnit()
    {
        var beatManager = CreateLiveBeatManager();

        beatManager.PublishGrid(new GridReading(0, 1, GridSyncState.Locked, false));
        Assert.That(beatManager.Grid!.Value.Count, Is.EqualTo(1));
        Assert.That(beatManager.Grid!.Value.Progress, Is.EqualTo(0.03125f).Within(0.0001f)); // (0 + 0.5) / 16
        Assert.That(beatManager.Grid!.Value.Progress, Is.InRange(0f, 1f));

        beatManager.PublishGrid(new GridReading(0, 16, GridSyncState.Locked, false));
        Assert.That(beatManager.Grid!.Value.Count, Is.EqualTo(16));
        Assert.That(beatManager.Grid!.Value.Progress, Is.EqualTo(0.96875f).Within(0.0001f)); // (15 + 0.5) / 16
        Assert.That(beatManager.Grid!.Value.Progress, Is.InRange(0f, 1f));
    }

    [Test]
    public void FacadeMirrorsTheLatestPublishSoABeatRewindSelfCorrects()
    {
        // No latching: a loop (a beat rewind within a phrase) re-publishes a lower position, and the facade
        // reflects it immediately rather than holding the higher count.
        var beatManager = CreateLiveBeatManager();

        beatManager.PublishGrid(new GridReading(0, 14, GridSyncState.Locked, false));
        Assert.That(beatManager.Grid!.Value.Count, Is.EqualTo(14));

        beatManager.PublishGrid(new GridReading(0, 2, GridSyncState.Locked, false));
        Assert.That(beatManager.Grid!.Value.Count, Is.EqualTo(2));
    }
}
