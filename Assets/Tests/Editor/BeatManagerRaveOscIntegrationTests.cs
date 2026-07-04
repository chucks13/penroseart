#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

public sealed class BeatManagerRaveOscIntegrationTests
{
    [Test]
    public void BeatManagerUpdateDerivesOffBeatHalfwayBetweenBeats()
    {
        // Seed beat 1 a half-beat in (0.25 s at 120 BPM): the nearest offbeat sits exactly on the gate.
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);

        beatManager.Update(0.25f);

        Assert.That(beatManager.beatData.snapshot.beatInBar, Is.EqualTo(1));
        Assert.That(beatManager.beatData.snapshot.beatsCountMs, Is.EqualTo(new[] { 1750, 250, 750, 1250 }));
        Assert.That(beatManager.beatData.snapshot.onBeats, Is.EqualTo(new[] { false, false, false, false }));
        Assert.That(beatManager.OffBeatsCountMs, Is.EqualTo(new[] { 0, 500, 1000, 1500 }));
        Assert.That(beatManager.OffBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatManager.OffBeatPulse, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(beatManager.NextOffBeatMs, Is.EqualTo(0));
        Assert.That(beatManager.OffBeat, Is.True);
    }

    [Test]
    public void BeatManagerUpdateClearsToNoBeatInStandalone()
    {
        // No live OSC source: Update must clear to the no-beat state regardless of any stale transport data.
        var beatManager = new BeatManager();
        beatManager.beatData.snapshot.bpm = 128f;
        beatManager.beatData.snapshot.beatPulse = 1f;

        beatManager.Update(0f);

        Assert.That(beatManager.IsActive, Is.False);
        Assert.That(beatManager.beatData.nextBeatMs, Is.EqualTo(-1));
        Assert.That(beatManager.beatData.snapshot.beatPulse, Is.EqualTo(0f));
        Assert.That(beatManager.Pulse, Is.Null);
        Assert.That(beatManager.GetBeatBrightness(0, 1f, 0.85f), Is.EqualTo(1f));
        Assert.That(beatManager.IsBeatTriggered(0), Is.False);
    }

    [Test]
    public void BeatManagerUpdateDoesNotOverwriteLiveBeatData()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.snapshot.bpm = 128f;
        beatManager.beatData.snapshot.beatInBar = 3;
        beatManager.beatData.snapshot.beatPulse = 0.25f;
        beatManager.SetLiveBeatSource(true);

        beatManager.Update(0f);

        Assert.That(beatManager.beatData.snapshot.bpm, Is.EqualTo(128f));
        Assert.That(beatManager.beatData.snapshot.beatInBar, Is.EqualTo(3));
        Assert.That(beatManager.beatData.snapshot.beatPulse, Is.EqualTo(0.25f));
    }

    [Test]
    public void RaveOscBroadcastLivenessFollowsRecognizedUdp7000Packets()
    {
        // The grace window is 15 broadcast intervals (0.25 s) so UDP burst loss and frame hitches
        // cannot flap the beat source; each flap wipes the contrived phrase/Levels state downstream.
        const float fifteenSixtyHzIntervals = 15f / 60f;

        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: false, 0f), Is.False);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, 0f), Is.True);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, fifteenSixtyHzIntervals - 0.001f), Is.True);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, fifteenSixtyHzIntervals + 0.001f), Is.False);

        // Before any recognized packet, elapsed time reads as infinity and liveness must stay off.
        Assert.That(RaveOscReceiver.IsBroadcastingAt(hasSnapshot: true, float.PositiveInfinity), Is.False);
    }

    [Test]
    public void BeatManagerLiveSourceStaysInactiveWhenOscSnapshotHasSentinelBpm()
    {
        var beatManager = new BeatManager();
        var snapshot = new RaveOnAirSnapshot
        {
            bpm = -1f,
            beatInBar = -1,
            beatPulse = 0f,
            beatAverageMs = -1,
            beatsCountMs = new[] { -1, -1, -1, -1 },
            onBeats = new[] { false, false, false, false },
        };

        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.CopyFrom(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.IsLiveSource, Is.True);
        Assert.That(beatManager.IsActive, Is.False);
        Assert.That(beatManager.beatData.snapshot.bpm, Is.EqualTo(-1f));
        Assert.That(beatManager.beatData.snapshot.playersLive, Is.EqualTo(""));
        Assert.That(beatManager.beatData.snapshot.track, Is.EqualTo(""));
        Assert.That(beatManager.Bpm, Is.Null);
        Assert.That(beatManager.Track, Is.Null);
        Assert.That(beatManager.PlayersLive, Is.Null);
    }

    [Test]
    public void BeatDataNearestBeatPropertiesReadActiveOnBeatSlotFromCountdownArrays()
    {
        var beatData = new BeatData
        {
            snapshot = new RaveOnAirSnapshot
            {
                beatInBar = 3,
                beatsCountMs = new[] { 1900, 2900, 0, 900 },
                onBeats = new[] { false, false, true, false },
            },
        };

        Assert.That(beatData.nextBeatMs, Is.EqualTo(0));
        Assert.That(beatData.onBeat, Is.True);
        Assert.That(beatData.currentOnBeat, Is.True);
    }

    [Test]
    public void BeatDataNearestBeatPropertiesUseNextCountdownSlotAfterOnBeatGate()
    {
        var beatData = new BeatData
        {
            snapshot = new RaveOnAirSnapshot
            {
                beatInBar = 3,
                beatsCountMs = new[] { 1700, 2700, 3700, 700 },
                onBeats = new[] { false, false, false, false },
            },
        };

        Assert.That(beatData.nextBeatMs, Is.EqualTo(700));
        Assert.That(beatData.onBeat, Is.False);
    }

    [Test]
    public void BeatDataNearestBeatPropertiesAreInertWhenCountdownsAreUnavailable()
    {
        var beatData = new BeatData
        {
            snapshot = new RaveOnAirSnapshot
            {
                beatInBar = -1,
                beatsCountMs = new[] { -1, -1, -1, -1 },
                onBeats = new[] { true, true, true, true },
            },
        };

        Assert.That(beatData.nextBeatMs, Is.EqualTo(-1));
        Assert.That(beatData.onBeat, Is.False);
    }

    [Test]
    public void BeatDataDefaultsToUnavailableCountdowns()
    {
        var beatData = new BeatData();

        Assert.That(beatData.nextBeatMs, Is.EqualTo(-1));
        Assert.That(beatData.onBeat, Is.False);
    }

    [Test]
    public void CopyFromStoresRawRaveOnAirDataAsBeatData()
    {
        var beatData = new BeatData();
        var snapshot = BuildSnapshot(onBeatForBeat3: true);

        beatData.CopyFrom(snapshot);

        Assert.That(beatData.snapshot.playersLive, Is.EqualTo("4,2"));
        Assert.That(beatData.snapshot.track, Is.EqualTo("Artist - Track"));
        Assert.That(beatData.snapshot.bpm, Is.EqualTo(128.5f));
        Assert.That(beatData.snapshot.beat.current, Is.EqualTo(64));
        Assert.That(beatData.snapshot.beat.total, Is.EqualTo(384));
        Assert.That(beatData.snapshot.bar.current, Is.EqualTo(16));
        Assert.That(beatData.snapshot.bar.nextMs, Is.EqualTo(777));
        Assert.That(beatData.snapshot.beatInBar, Is.EqualTo(3));
        Assert.That(beatData.snapshot.beatsCountMs, Is.EqualTo(new[] { 100, 200, 300, 400 }));
        Assert.That(beatData.snapshot.onBeats, Is.EqualTo(new[] { false, false, true, false }));
        Assert.That(beatData.snapshot.beatAverageMs, Is.EqualTo(468));
        Assert.That(beatData.snapshot.beatPulse, Is.EqualTo(0.625f));
        Assert.That(beatData.snapshot.levels.low, Is.EqualTo(0.25f));
        Assert.That(beatData.snapshot.levels.mid, Is.EqualTo(0.5f));
        Assert.That(beatData.snapshot.levels.high, Is.EqualTo(0.75f));
        Assert.That(beatData.snapshot.phraseState.label, Is.EqualTo("Drop"));
        Assert.That(beatData.snapshot.phraseState.countBeats, Is.EqualTo(12));
        Assert.That(beatData.snapshot.phraseState.lengthBeats, Is.EqualTo(32));
        Assert.That(beatData.snapshot.phraseState.irregular, Is.EqualTo(0));
        Assert.That(beatData.snapshot.dropState.active, Is.EqualTo(1));
        Assert.That(beatData.snapshot.dropState.countBeats, Is.EqualTo(0));
        Assert.That(beatData.snapshot.dropState.lengthBeats, Is.EqualTo(32));
        Assert.That(beatData.snapshot.dropState.remaining, Is.EqualTo(2));
        Assert.That(beatData.snapshot.fillState.active, Is.EqualTo(0));
        Assert.That(beatData.snapshot.fillState.countBeats, Is.EqualTo(16));
        Assert.That(beatData.snapshot.fillState.lengthBeats, Is.EqualTo(8));
        Assert.That(beatData.snapshot.fillState.remaining, Is.EqualTo(1));
        Assert.That(beatData.snapshot.energyState.label, Is.EqualTo("High"));
        Assert.That(beatData.snapshot.energyState.countBeats, Is.EqualTo(4));
        Assert.That(beatData.snapshot.energyState.lengthBeats, Is.EqualTo(16));
        Assert.That(beatData.snapshot.nextEnergyState.label, Is.EqualTo("Mid"));
    }

    [Test]
    public void CopyFromExposesNearestBeatCountdownHelpers()
    {
        var beatData = new BeatData();
        var snapshot = BuildSnapshot(onBeatForBeat3: true);

        beatData.CopyFrom(snapshot);

        Assert.That(beatData.nextBeatMs, Is.EqualTo(100));
        Assert.That(beatData.GetNextBeatMs(), Is.EqualTo(100));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.snapshot.beatPulse, Is.EqualTo(0.625f));
    }

    [Test]
    public void CopyFromUsesBeatCountdownWhenCurrentBeatGateIsOff()
    {
        var beatData = new BeatData();
        var snapshot = BuildSnapshot(onBeatForBeat3: false);

        beatData.CopyFrom(snapshot);

        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.nextBeatMs, Is.EqualTo(100));
        Assert.That(beatData.GetNextBeatMs(), Is.EqualTo(100));
    }

    [Test]
    public void TrackIdChangesWhenTheOnAirTrackIdChanges()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);

        beatManager.beatData.snapshot.trackId = 11;
        beatManager.Update(0f);
        Assert.That(beatManager.TrackId, Is.EqualTo(11));

        beatManager.beatData.snapshot.trackId = 22;
        beatManager.Update(0f);
        Assert.That(beatManager.TrackId, Is.EqualTo(22));
    }

    [Test]
    public void TrackIdIsNullWhenTrackIdIsSentinel()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.snapshot.trackId = -1;

        beatManager.Update(0f);

        Assert.That(beatManager.TrackId, Is.Null);
    }

    [Test]
    public void TrackIdIgnoresATitleChangeWithoutATrackIdChange()
    {
        // TrackId only tracks snapshot.trackId, so a title-only rewrite of the same track must leave it
        // untouched (a bare title change is not a new track).
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.snapshot.trackId = 7;
        beatManager.beatData.snapshot.track = "Artist - One";
        beatManager.Update(0f);
        var first = beatManager.TrackId;
        Assert.That(first, Is.EqualTo(7));

        beatManager.beatData.snapshot.track = "Artist - Two";
        beatManager.Update(0f);

        Assert.That(beatManager.TrackId, Is.EqualTo(first));
    }

    [Test]
    public void LiveSentinelSnapshotDisablesBeatAndOffBeatDerivation()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.snapshot.bpm = 128f;
        beatManager.beatData.snapshot.beatInBar = 3;
        beatManager.beatData.snapshot.onBeats = new[] { false, false, true, false };
        beatManager.beatData.snapshot.beatPulse = 1f;
        beatManager.beatData.snapshot.playersLive = "4";
        beatManager.beatData.snapshot.track = "Track";

        var snapshot = new RaveOnAirSnapshot
        {
            bpm = -1f,
            beatInBar = -1,
            beatPulse = 0f,
            beatAverageMs = -1,
            beatsCountMs = new[] { -1, -1, -1, -1 },
            onBeats = new[] { false, false, false, false },
        };

        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.CopyFrom(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.IsActive, Is.False);
        Assert.That(beatManager.beatData.snapshot.bpm, Is.EqualTo(-1f));
        Assert.That(beatManager.beatData.GetNextBeatMs(), Is.EqualTo(-1));
        Assert.That(beatManager.beatData.onBeat, Is.False);
        Assert.That(beatManager.OffBeatsCountMs, Is.EqualTo(new[] { -1, -1, -1, -1 }));
        Assert.That(beatManager.OffBeats, Is.EqualTo(new[] { false, false, false, false }));
        Assert.That(beatManager.beatData.snapshot.beatPulse, Is.EqualTo(0f));
        Assert.That(beatManager.beatData.snapshot.playersLive, Is.EqualTo(""));
        Assert.That(beatManager.beatData.snapshot.track, Is.EqualTo(""));
        Assert.That(beatManager.NextOffBeatMs, Is.Null);
        Assert.That(beatManager.OffBeat, Is.Null);
        Assert.That(beatManager.OffBeatPulse, Is.Null);
    }

    [Test]
    public void UpdateDerivesOffBeatArraysFromOscBeatCountdowns()
    {
        var beatManager = CreateLiveBeatManager(beatsCountMs: new[] { 1750, 250, 750, 1250 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeatsCountMs, Is.EqualTo(new[] { 0, 500, 1000, 1500 }));
        Assert.That(beatManager.OffBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatManager.OffBeatPulse, Is.EqualTo(1f));
        Assert.That(beatManager.NextOffBeatMs, Is.EqualTo(0));
        Assert.That(beatManager.OffBeat, Is.True);
    }

    [Test]
    public void UpdateUsesActualCountdownGapForOffBeatMidpoint()
    {
        var beatManager = CreateLiveBeatManager(beatsCountMs: new[] { 1800, 200, 800, 1300 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeatsCountMs[0], Is.EqualTo(0));
        Assert.That(beatManager.OffBeats[0], Is.True);
    }

    [Test]
    public void UpdateKeepsOffBeatGateOpenForQuarterOfAverageBeat()
    {
        var beatManager = CreateLiveBeatManager(beatsCountMs: new[] { 1700, 200, 700, 1200 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeatsCountMs, Is.EqualTo(new[] { 0, 450, 950, 1450 }));
        Assert.That(beatManager.OffBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatManager.OffBeatPulse, Is.EqualTo(0.972f).Within(0.001f));
    }

    [Test]
    public void UpdateTurnsOffBeatGateOffAfterQuarterOfAverageBeat()
    {
        var beatManager = CreateLiveBeatManager(beatsCountMs: new[] { 1570, 70, 570, 1070 });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeatsCountMs, Is.EqualTo(new[] { 1820, 320, 820, 1320 }));
        Assert.That(beatManager.OffBeats, Is.EqualTo(new[] { false, false, false, false }));
        Assert.That(beatManager.OffBeatPulse, Is.EqualTo(0.705f).Within(0.001f));
    }

    [Test]
    public void GetBeatBrightnessUsesBarPhaseWaveformsForMusicalVariants()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.snapshot.bpm = 128f;
        beatManager.beatData.snapshot.beatInBar = 1;
        beatManager.beatData.snapshot.beatAverageMs = 500;
        beatManager.beatData.snapshot.beatsCountMs = new[] { 0, 250, 750, 1250 };
        beatManager.beatData.snapshot.beatPulse = 0.25f;

        Assert.That(beatManager.BarPhase, Is.EqualTo(0.125f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(0, 1f, 0.5f), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(5, 1f, 0.5f), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(6, 1f, 0.5f), Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void IsBeatTriggeredUsesRaveOnBeatValue()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.snapshot.bpm = 128f;
        beatManager.beatData.snapshot.beatInBar = 1;
        beatManager.beatData.snapshot.onBeats = new[] { true, false, false, false };

        Assert.That(beatManager.IsBeatTriggered(0), Is.True);
    }

    /// <summary>Builds a live-sourced BeatManager whose transport carries the supplied beat countdowns.</summary>
    private static BeatManager CreateLiveBeatManager(int[] beatsCountMs)
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.snapshot.bpm = 120f;
        // A running clock always carries a 1..4 beat label on the wire; the 4-count is now the mode
        // authority (IsActive => IsSynced), so a live fixture must supply it (ADR-0007).
        beatManager.beatData.snapshot.beatInBar = 1;
        beatManager.beatData.snapshot.beatAverageMs = 500;
        beatManager.beatData.snapshot.beatsCountMs = beatsCountMs;
        beatManager.beatData.snapshot.onBeats = new[] { false, false, false, false };
        return beatManager;
    }

    private static RaveOnAirSnapshot BuildSnapshot(bool onBeatForBeat3)
    {
        return new RaveOnAirSnapshot
        {
            playersLive = "4,2",
            track = "Artist - Track",
            bpm = 128.5f,
            beat = new BeatPosition { current = 64, total = 384 },
            bar = new BarPosition { current = 16, nextMs = 777 },
            beatInBar = 3,
            beatsCountMs = new[] { 100, 200, 300, 400 },
            onBeats = new[] { false, false, onBeatForBeat3, false },
            beatAverageMs = 468,
            beatPulse = 0.625f,
            levels = new Levels { low = 0.25f, mid = 0.5f, high = 0.75f },
            phraseState = new PhraseState { label = "Drop", countBeats = 12, lengthBeats = 32, irregular = 0 },
            dropState = new CountdownState { active = 1, countBeats = 0, lengthBeats = 32, remaining = 2 },
            fillState = new CountdownState { active = 0, countBeats = 16, lengthBeats = 8, remaining = 1 },
            energyState = new LabeledCountdown { label = "High", countBeats = 4, lengthBeats = 16 },
            nextEnergyState = new LabeledCountdown { label = "Mid", countBeats = 4, lengthBeats = 16 },
        };
    }
}
