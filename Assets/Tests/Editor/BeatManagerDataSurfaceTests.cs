// Contract tests for BeatManager timing, beat, pulse, track, and surface-shape values.

#nullable enable

using System.Linq;
using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>Caller-facing tests for BeatManager's shallow immutable musical-data surface.</summary>
public sealed class BeatManagerDataSurfaceTests
{
    /// <summary>Standalone serves unavailable optional facts while Levels rests at silence.</summary>
    [Test]
    public void StandaloneExposesUnavailableFactsAndZeroLevels()
    {
        var beatManager = new BeatManager();
        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Timing.Bpm, Is.Null);
        Assert.That(beatManager.Beats.OnBeatMs(1), Is.Null);
        Assert.That(beatManager.Offbeats.OffBeat(1), Is.False);
        Assert.That(beatManager.Pulses.Every(Duration.Quarter), Is.Zero);
        Assert.That(beatManager.Levels.Normalized.Average, Is.Zero);
    }

    /// <summary>Valid wire facts remain readable even before the running beat count makes the frame synced.</summary>
    [Test]
    public void RawWireFactsDoNotDependOnTheDerivedSyncState()
    {
        var snapshot = new RaveOnAirSnapshot
        {
            bpm = 128f,
            beat = new BeatPosition { current = 32, total = 256 },
            bar = new BarPosition { current = 8, nextMs = 300 },
            beatInBar = -1,
            beatAverageMs = 469,
            beatsCountMs = new[] { 0, 469, 938, 1407 },
            onBeats = new[] { true, false, false, false },
            beatPulse = 0.75f,
            timingGrid = new TimingGrid { state = "locked", beat = 9, bar = 3 },
        };
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);

        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Timing.Bpm, Is.EqualTo(128f));
        Assert.That(beatManager.Timing.Beat, Is.EqualTo(32));
        Assert.That(beatManager.Timing.BeatProgress, Is.Null);
        Assert.That(beatManager.Beats.OnBeatMs(1), Is.Zero);
        Assert.That(beatManager.Beats.OnBeat(1), Is.True);
        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0.75f));
        Assert.That(beatManager.Pulses.OffBeat, Is.Zero);
        Assert.That(beatManager.Pulses.Every(Duration.Quarter), Is.Zero);
        Assert.That(beatManager.Grid.State, Is.EqualTo(GridState.Locked));
        Assert.That(beatManager.Grid.Beat, Is.EqualTo(9));
        Assert.That(beatManager.Grid.Progress, Is.Null);

        beatManager.SetLiveBeatSource(false);
        beatManager.Update(1f);

        Assert.That(beatManager.Timing.Bpm, Is.Null);
        Assert.That(beatManager.Timing.Beat, Is.Null);
        Assert.That(beatManager.Timing.Bar, Is.Null);
        Assert.That(beatManager.Beats.OnBeatMs(1), Is.Null);
        Assert.That(beatManager.Pulses.Beat, Is.Zero);
        Assert.That(beatManager.Grid.State, Is.Null);
    }

    /// <summary>Timing groups direct wire position with beat- and bar-progress calculations.</summary>
    [Test]
    public void TimingGroupsWirePositionAndDerivedProgress()
    {
        var snapshot = BeatClockFixture.CreateSnapshot(120f, 0.25f);
        snapshot.beat = new BeatPosition { current = 64, total = 384 };
        snapshot.bar = new BarPosition { current = 16, nextMs = 250 };
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);

        beatManager.Update(0.25f);

        Assert.That(beatManager.Timing.Bpm, Is.EqualTo(120f));
        Assert.That(beatManager.Timing.BeatAverageMilliseconds, Is.EqualTo(500));
        Assert.That(beatManager.Timing.Beat, Is.EqualTo(64));
        Assert.That(beatManager.Timing.TotalBeats, Is.EqualTo(384));
        Assert.That(beatManager.Timing.Bar, Is.EqualTo(16));
        Assert.That(beatManager.Timing.BeatProgress, Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(beatManager.Timing.BarProgress, Is.EqualTo(0.125f).Within(0.001f));
    }

    /// <summary>The four On Beat wire lanes are addressed directly by one-based musical count.</summary>
    [Test]
    public void BeatsExposeTheFourNamedWireLanesDirectly()
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
        {
            snapshot.beatInBar = 3;
            snapshot.beatsCountMs = new[] { 800, 1200, 0, 400 };
            snapshot.onBeats = new[] { false, false, true, false };
        });
        beatManager.Update(0f);

        Assert.That(beatManager.Beats.OnBeatMs(1), Is.EqualTo(800));
        Assert.That(beatManager.Beats.OnBeatMs(3), Is.Zero);
        Assert.That(beatManager.Beats.OnBeat(3), Is.True);
        Assert.That(beatManager.Beats.OnBeat(0), Is.False);
        Assert.That(beatManager.Beats.OnBeat(5), Is.False);
    }

    /// <summary>Offbeats provide the same countdown and active-window reads at derived midpoints.</summary>
    [Test]
    public void OffbeatsMirrorTheBeatQueriesWithTempoDerivedValues()
    {
        var beatManager = BeatClockFixture.CreateSeeded(120f, 0.25f);
        beatManager.Update(0.25f);

        Assert.That(beatManager.Offbeats.OffBeatMs(1), Is.Zero);
        Assert.That(beatManager.Offbeats.OffBeatMs(2), Is.EqualTo(500));
        Assert.That(beatManager.Offbeats.OffBeat(1), Is.True);
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>Duration pulses follow tempo and accept a caller-selected active fraction.</summary>
    [Test]
    public void PulsesExposeTempoBasedValuesAndConfigurableActiveWindows()
    {
        var beatManager = BeatClockFixture.CreateSeeded(120f, 0.25f);
        beatManager.Update(0.25f);

        Assert.That(beatManager.Pulses.Every(Duration.Quarter), Is.EqualTo(0.5f).Within(0.001f));
        Assert.That(beatManager.Pulses.On(Duration.Quarter), Is.False);
        Assert.That(beatManager.Pulses.On(Duration.Quarter, activeFor: 0.75f), Is.True);
    }

    /// <summary>Track values preserve parsed player order alongside the focused track identity.</summary>
    [Test]
    public void TrackValuesPreservePlayerOrderAndIdentity()
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
        {
            snapshot.playersLive = "2,1";
            snapshot.track = "Artist - Track";
            snapshot.trackId = 7661;
        });
        beatManager.Update(0f);

        Assert.That(beatManager.Track.PlayersLive, Is.EqualTo(new[] { 2, 1 }));
        Assert.That(beatManager.Track.Title, Is.EqualTo("Artist - Track"));
        Assert.That(beatManager.Track.Id, Is.EqualTo(7661));
    }

    /// <summary>The public surface omits the deleted navigation and one-frame edge members.</summary>
    [Test]
    public void PublicSurfaceDoesNotRestoreDeletedNavigationOrEdgeTypes()
    {
        var names = typeof(BeatManager).GetProperties().Select(property => property.Name).ToArray();
        Assert.That(names, Does.Not.Contain("Clock"));
        Assert.That(names, Does.Not.Contain("Position"));
        Assert.That(names, Does.Not.Contain("OffBeats"));
        Assert.That(typeof(DropValues).GetProperty("Span"), Is.Null);
        Assert.That(typeof(GridValues).GetProperty("Wrapped"), Is.Null);
        Assert.That(typeof(TrackValues).GetProperty("Changed"), Is.Null);
    }
}
