#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>Exercises live OSC ingestion through BeatManager's caller-facing Data Surface.</summary>
public sealed class BeatManagerRaveOscIntegrationTests
{
    /// <summary>Verifies BeatManager derives the off-beat lane halfway between live beats.</summary>
    [Test]
    public void BeatManagerUpdateDerivesOffBeatHalfwayBetweenBeats()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);

        beatManager.Update(0.25f);

        Assert.That(beatManager.Position.BeatInBar, Is.EqualTo(1));
        Assert.That(beatManager.OffBeats.MsUntil(1), Is.EqualTo(0));
        Assert.That(beatManager.OffBeats.Gate(1), Is.True);
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(beatManager.OffBeats.NextOffBeatMs, Is.EqualTo(0));
        Assert.That(beatManager.OffBeats.OffBeat, Is.True);
    }

    /// <summary>Verifies standalone updates expose an inert Data Surface.</summary>
    [Test]
    public void BeatManagerUpdateClearsToNoBeatInStandalone()
    {
        var beatManager = new BeatManager();

        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Beats.NextBeatMs, Is.Null);
        Assert.That(beatManager.Pulses.Beat, Is.Null);
        Assert.That(beatManager.OffBeats.NextOffBeatMs, Is.Null);
        Assert.That(beatManager.OffBeats.OffBeat, Is.Null);
    }

    /// <summary>Verifies live wire values survive the hub update and reach their canonical doorways.</summary>
    [Test]
    public void BeatManagerUpdateDoesNotOverwriteLiveBeatData()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot
        {
            bpm = 128f,
            beatInBar = 3,
            beatPulse = 0.25f,
            beatAverageMs = 469,
        });

        beatManager.Update(0f);

        Assert.That(beatManager.Clock.Bpm, Is.EqualTo(128f));
        Assert.That(beatManager.Position.BeatInBar, Is.EqualTo(3));
        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0.25f));
    }

    /// <summary>Verifies receiver liveness uses the documented fifteen-frame grace window.</summary>
    [Test]
    public void RaveOscBroadcastLivenessFollowsRecognizedUdp7000Packets()
    {
        const float fifteenSixtyHzIntervals = 15f / 60f;

        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: false, 0f), Is.False);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, 0f), Is.True);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, fifteenSixtyHzIntervals - 0.001f), Is.True);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, fifteenSixtyHzIntervals + 0.001f), Is.False);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, float.PositiveInfinity), Is.False);
    }

    /// <summary>Verifies an unavailable live BPM sentinel leaves clock-derived doorways inactive.</summary>
    [Test]
    public void BeatManagerLiveSourceStaysInactiveWhenOscSnapshotHasSentinelBpm()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot
        {
            bpm = -1f,
            beatInBar = -1,
            beatPulse = 0f,
            beatAverageMs = -1,
            beatsCountMs = new[] { -1, -1, -1, -1 },
            onBeats = new[] { false, false, false, false },
        });

        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Clock.Bpm, Is.Null);
        Assert.That(beatManager.Track.TrackTitle, Is.Null);
        Assert.That(beatManager.Track.PlayersLive, Is.Empty);
    }

    /// <summary>Verifies Track.Id follows each live wire identity change.</summary>
    [Test]
    public void TrackIdChangesWhenTheOnAirTrackIdChanges()
    {
        var beatManager = new BeatManager();
        var snapshot = new RaveOnAirSnapshot { trackId = 11 };

        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        Assert.That(beatManager.Track.TrackId, Is.EqualTo(11));

        snapshot.trackId = 22;
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        Assert.That(beatManager.Track.TrackId, Is.EqualTo(22));
    }

    /// <summary>Verifies the wire's unavailable track id sentinel is translated to null.</summary>
    [Test]
    public void TrackIdIsNullWhenTrackIdIsSentinel()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot { trackId = -1 });

        beatManager.Update(0f);

        Assert.That(beatManager.Track.TrackId, Is.Null);
    }

    /// <summary>Verifies a title-only rewrite leaves the deprecated numeric track id unchanged.</summary>
    [Test]
    public void TrackIdIgnoresATitleChangeWithoutATrackIdChange()
    {
        var beatManager = new BeatManager();
        var snapshot = new RaveOnAirSnapshot { trackId = 7, track = "Artist - One" };
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        var first = beatManager.Track.TrackId;

        snapshot.track = "Artist - Two";
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.Track.TrackId, Is.EqualTo(first));
    }

    /// <summary>Verifies unavailable live timing disables beat and off-beat derivation.</summary>
    [Test]
    public void LiveSentinelSnapshotDisablesBeatAndOffBeatDerivation()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot
        {
            bpm = -1f,
            beatInBar = -1,
            beatPulse = 0f,
            beatAverageMs = -1,
            beatsCountMs = new[] { -1, -1, -1, -1 },
            onBeats = new[] { false, false, false, false },
        });

        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Beats.NextBeatMs, Is.Null);
        Assert.That(beatManager.OffBeats.NextOffBeatMs, Is.Null);
        Assert.That(beatManager.OffBeats.OffBeat, Is.Null);
        Assert.That(beatManager.Pulses.Beat, Is.Null);
        Assert.That(beatManager.Pulses.OffBeat, Is.Null);
        Assert.That(beatManager.Track.TrackTitle, Is.Null);
    }

    /// <summary>Verifies OSC beat countdowns drive the derived off-beat lanes.</summary>
    [Test]
    public void UpdateDerivesOffBeatLanesFromOscBeatCountdowns()
    {
        var beatManager = CreateLiveBeatManager(new[] { 1750, 250, 750, 1250 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeats.MsUntil(1), Is.EqualTo(0));
        Assert.That(beatManager.OffBeats.MsUntil(2), Is.EqualTo(500));
        Assert.That(beatManager.OffBeats.MsUntil(3), Is.EqualTo(1000));
        Assert.That(beatManager.OffBeats.MsUntil(4), Is.EqualTo(1500));
        Assert.That(beatManager.OffBeats.Gate(1), Is.True);
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(1f));
    }

    /// <summary>Verifies the off-beat midpoint uses the actual gap between countdown lanes.</summary>
    [Test]
    public void UpdateUsesActualCountdownGapForOffBeatMidpoint()
    {
        var beatManager = CreateLiveBeatManager(new[] { 1800, 200, 800, 1300 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeats.MsUntil(1), Is.EqualTo(0));
        Assert.That(beatManager.OffBeats.Gate(1), Is.True);
    }

    /// <summary>Verifies the off-beat gate remains open for one quarter of the average beat.</summary>
    [Test]
    public void UpdateKeepsOffBeatGateOpenForQuarterOfAverageBeat()
    {
        var beatManager = CreateLiveBeatManager(new[] { 1700, 200, 700, 1200 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeats.MsUntil(1), Is.EqualTo(0));
        Assert.That(beatManager.OffBeats.MsUntil(2), Is.EqualTo(450));
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(0.972f).Within(0.001f));
    }

    /// <summary>Verifies the off-beat gate closes after one quarter of the average beat.</summary>
    [Test]
    public void UpdateTurnsOffBeatGateOffAfterQuarterOfAverageBeat()
    {
        var beatManager = CreateLiveBeatManager(new[] { 1570, 70, 570, 1070 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeats.MsUntil(1), Is.EqualTo(1820));
        Assert.That(beatManager.OffBeats.MsUntil(2), Is.EqualTo(320));
        Assert.That(beatManager.OffBeats.Gate(1), Is.False);
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(0.705f).Within(0.001f));
    }

    /// <summary>Builds a live-sourced BeatManager carrying the supplied beat countdowns.</summary>
    private static BeatManager CreateLiveBeatManager(int[] beatsCountMs)
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveOnAirSnapshot
        {
            bpm = 120f,
            beatInBar = 1,
            beatAverageMs = 500,
            beatsCountMs = beatsCountMs,
            onBeats = new[] { false, false, false, false },
        });
        return beatManager;
    }
}
