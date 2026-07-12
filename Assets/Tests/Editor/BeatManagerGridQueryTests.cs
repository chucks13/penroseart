#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>
/// Pins the effect-facing 16-beat Grid query (<see cref="BeatManager.GridQuery"/> => <see cref="GridInfo"/>?).
/// Wire-fed from RaveSystem's source-computed <c>timing_grid</c> lane: the null rules (off the grid /
/// no usable wire grid), the 1..16 Count, the 1..4 Bar, the 0..1 Progress enrichment, and the
/// locked/coasting/disputed Confidence parse.
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
        beatManager.WireSnapshot.beatInBar = 1;
        beatManager.WireSnapshot.beatAverageMs = 500;
        beatManager.WireSnapshot.beatsCountMs = new[] { 0, 250, 750, 1250 };
        return beatManager;
    }

    /// <summary>Writes a <c>timing_grid</c> wire state onto the manager's held snapshot.</summary>
    private static void SetGrid(BeatManager beatManager, int beat, int bar, string? state)
    {
        beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = beat, bar = bar, state = state };
    }

    [Test]
    public void GridIsNullWithNoTimingGridOnTheWire()
    {
        // Default Unavailable timing_grid (beat -1, state null): no usable grid.
        var beatManager = CreateLiveBeatManager();

        Assert.That(beatManager.GridQuery, Is.Null);
    }

    /// <summary>With no 4-count clock, Grid reads null even when a stale wire grid still rides on the snapshot.</summary>
    [Test]
    public void GridIsNullOnTheStandaloneFloorEvenWithAWireGrid()
    {
        // No 4-count clock (beatInBar < 1) is a mode exit: the wall is off the grid even when a stale
        // timing_grid still rides along on the snapshot.
        var beatManager = CreateLiveBeatManager();
        beatManager.WireSnapshot.beatInBar = -1;
        SetGrid(beatManager, beat: 5, bar: 2, state: "locked");

        Assert.That(beatManager.GridQuery, Is.Null);
    }

    [Test]
    public void GridSurfacesBeatBarStateAndProgress()
    {
        var beatManager = CreateLiveBeatManager();
        SetGrid(beatManager, beat: 5, bar: 2, state: "locked");

        var grid = beatManager.GridQuery;

        Assert.That(grid, Is.Not.Null);
        Assert.That(grid!.Value.State, Is.EqualTo(GridState.Locked));
        Assert.That(grid.Value.Beat, Is.EqualTo(5));
        Assert.That(grid.Value.Bar, Is.EqualTo(2));
        // (5 - 1 + 0.5 intra-beat) / 16
        Assert.That(grid.Value.Progress, Is.EqualTo(0.28125f).Within(0.0001f));
    }

    [Test]
    public void CoastingAndDisputedParseCaseInsensitively()
    {
        // All three states are on-grid readings with a valid grid beat; they differ only in trust.
        var beatManager = CreateLiveBeatManager();

        SetGrid(beatManager, 8, 2, "coasting");
        Assert.That(beatManager.GridQuery?.State, Is.EqualTo(GridState.Coasting));

        SetGrid(beatManager, 8, 2, "DISPUTED");
        Assert.That(beatManager.GridQuery?.State, Is.EqualTo(GridState.Disputed));
    }

    [Test]
    public void GridIsNullWhenStateIsEmptyOrUnrecognized()
    {
        var beatManager = CreateLiveBeatManager();

        SetGrid(beatManager, 5, 2, string.Empty);
        Assert.That(beatManager.GridQuery, Is.Null, "An empty state is no usable grid.");

        SetGrid(beatManager, 5, 2, "wobbling");
        Assert.That(beatManager.GridQuery, Is.Null, "An unrecognized state never becomes a wrong state.");
    }

    [Test]
    public void GridIsNullWhenBeatIsBelowOne()
    {
        // "-1 -1 coasting": a valid state string but no placed beat reads as no grid.
        var beatManager = CreateLiveBeatManager();
        SetGrid(beatManager, beat: -1, bar: -1, state: "coasting");

        Assert.That(beatManager.GridQuery, Is.Null);
    }

    [Test]
    public void BeatStaysWithinTheSixteenBeatGridAndProgressWithinUnit()
    {
        var beatManager = CreateLiveBeatManager();

        SetGrid(beatManager, 1, 1, "locked");
        Assert.That(beatManager.GridQuery!.Value.Beat, Is.EqualTo(1));
        Assert.That(beatManager.GridQuery!.Value.Progress, Is.EqualTo(0.03125f).Within(0.0001f)); // (0 + 0.5) / 16
        Assert.That(beatManager.GridQuery!.Value.Progress, Is.InRange(0f, 1f));

        SetGrid(beatManager, 16, 4, "locked");
        Assert.That(beatManager.GridQuery!.Value.Beat, Is.EqualTo(16));
        Assert.That(beatManager.GridQuery!.Value.Progress, Is.EqualTo(0.96875f).Within(0.0001f)); // (15 + 0.5) / 16
        Assert.That(beatManager.GridQuery!.Value.Progress, Is.InRange(0f, 1f));
    }

    [Test]
    public void GridReflectsTheLatestWireBeatSoABeatRewindSelfCorrects()
    {
        // No latching: a loop (a beat rewind within a phrase) lowers the wire beat and the query reflects
        // it immediately rather than holding the higher beat.
        var beatManager = CreateLiveBeatManager();

        SetGrid(beatManager, 14, 4, "locked");
        Assert.That(beatManager.GridQuery!.Value.Beat, Is.EqualTo(14));

        SetGrid(beatManager, 2, 1, "locked");
        Assert.That(beatManager.GridQuery!.Value.Beat, Is.EqualTo(2));
    }
}
