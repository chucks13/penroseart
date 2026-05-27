#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

public sealed class BeatManagerRaveOscIntegrationTests
{
    [Test]
    public void BeatManagerUpdateSynthesizesOnBeatWhenNoLiveBeatDataExists()
    {
        var beatManager = new BeatManager
        {
            simulatedBpm = 120f,
        };
        beatManager.beatData.active = false;

        beatManager.Update(0f);

        Assert.That(beatManager.beatData.active, Is.True);
        Assert.That(beatManager.beatData.playersLive, Is.EqualTo("SIM"));
        Assert.That(beatManager.beatData.track, Is.EqualTo("Simulated Beat"));
        Assert.That(beatManager.beatData.bpm, Is.EqualTo(120f));
        Assert.That(beatManager.beatData.beatAverageMs, Is.EqualTo(500));
        Assert.That(beatManager.beatData.beatInBar, Is.EqualTo(1));
        Assert.That(beatManager.beatData.currentBeat, Is.EqualTo(0));
        Assert.That(beatManager.beatData.beatsCountMs, Is.EqualTo(new[] { 0, 500, 1000, 1500 }));
        Assert.That(beatManager.beatData.onBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatManager.beatData.beatPulse, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(beatManager.beatData.offBeats, Is.EqualTo(new[] { false, false, false, false }));
    }

    [Test]
    public void BeatManagerUpdateSynthesizesOffBeatHalfwayBetweenSimulatedBeats()
    {
        var beatManager = new BeatManager
        {
            simulatedBpm = 120f,
        };
        beatManager.beatData.active = false;

        beatManager.Update(0.25f);

        Assert.That(beatManager.beatData.beatInBar, Is.EqualTo(1));
        Assert.That(beatManager.beatData.beatsCountMs, Is.EqualTo(new[] { 1750, 250, 750, 1250 }));
        Assert.That(beatManager.beatData.onBeats, Is.EqualTo(new[] { false, false, false, false }));
        Assert.That(beatManager.beatData.offBeatsCountMs, Is.EqualTo(new[] { 0, 500, 1000, 1500 }));
        Assert.That(beatManager.beatData.offBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatManager.beatData.offBeatPulse, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void BeatManagerUpdateDoesNotSimulateWhenSimulatedBpmIsUnavailable()
    {
        var beatManager = new BeatManager
        {
            simulatedBpm = 0f,
        };
        beatManager.beatData.active = false;

        beatManager.Update(0f);

        Assert.That(beatManager.beatData.active, Is.False);
        Assert.That(beatManager.beatData.nextBeatMs, Is.EqualTo(-1));
        Assert.That(beatManager.beatData.beatPulse, Is.EqualTo(0f));
    }

    [Test]
    public void BeatManagerUpdateDoesNotOverwriteLiveBeatData()
    {
        var beatManager = new BeatManager
        {
            simulatedBpm = 120f,
        };
        beatManager.beatData.active = true;
        beatManager.beatData.bpm = 128f;
        beatManager.beatData.beatInBar = 3;
        beatManager.beatData.currentBeat = 2;
        beatManager.beatData.beatPulse = 0.25f;
        beatManager.SetLiveBeatSource(true);

        beatManager.Update(0f);

        Assert.That(beatManager.beatData.bpm, Is.EqualTo(128f));
        Assert.That(beatManager.beatData.beatInBar, Is.EqualTo(3));
        Assert.That(beatManager.beatData.currentBeat, Is.EqualTo(2));
        Assert.That(beatManager.beatData.beatPulse, Is.EqualTo(0.25f));
    }

    [Test]
    public void BeatDataCurrentBeatPropertiesReadActiveOnBeatSlotFromCountdownArrays()
    {
        var beatData = new BeatData
        {
            beatInBar = 3,
            beatsCountMs = new[] { 1900, 2900, 0, 900 },
            onBeats = new[] { false, false, true, false },
            offBeatsCountMs = new[] { 250, 1250, 2250, 3250 },
            offBeats = new[] { true, false, false, false },
        };

        Assert.That(beatData.nextBeatMs, Is.EqualTo(0));
        Assert.That(beatData.onBeat, Is.True);
        Assert.That(beatData.nextOffBeatMs, Is.EqualTo(250));
        Assert.That(beatData.offBeat, Is.True);
    }

    [Test]
    public void BeatDataCurrentBeatPropertiesUseNextCountdownSlotAfterOnBeatGate()
    {
        var beatData = new BeatData
        {
            beatInBar = 3,
            beatsCountMs = new[] { 1700, 2700, 3700, 700 },
            onBeats = new[] { false, false, false, false },
            offBeatsCountMs = new[] { 1950, 450, 950, 1450 },
            offBeats = new[] { false, true, false, false },
        };

        Assert.That(beatData.nextBeatMs, Is.EqualTo(700));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.nextOffBeatMs, Is.EqualTo(450));
        Assert.That(beatData.offBeat, Is.True);
    }

    [Test]
    public void BeatDataCurrentBeatPropertiesAreInertWhenCountdownsAreUnavailable()
    {
        var beatData = new BeatData
        {
            beatInBar = -1,
            beatsCountMs = new[] { -1, -1, -1, -1 },
            onBeats = new[] { true, true, true, true },
            offBeatsCountMs = new[] { -1, -1, -1, -1 },
            offBeats = new[] { true, true, true, true },
        };

        Assert.That(beatData.nextBeatMs, Is.EqualTo(-1));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.nextOffBeatMs, Is.EqualTo(-1));
        Assert.That(beatData.offBeat, Is.False);
    }

    [Test]
    public void BeatDataDefaultsToUnavailableCountdowns()
    {
        var beatData = new BeatData();

        Assert.That(beatData.nextBeatMs, Is.EqualTo(-1));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.nextOffBeatMs, Is.EqualTo(-1));
        Assert.That(beatData.offBeat, Is.False);
    }

    [Test]
    public void ApplySnapshotStoresRawRaveOnAirDataAsBeatData()
    {
        var beatData = new BeatData
        {
            beatsPerMeasure = 4,
        };
        var snapshot = BuildSnapshot(onBeatForBeat3: true);

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.active, Is.True);
        Assert.That(beatData.playersLive, Is.EqualTo("4,2"));
        Assert.That(beatData.track, Is.EqualTo("Artist - Track"));
        Assert.That(beatData.bpm, Is.EqualTo(128.5f));
        Assert.That(beatData.beat.current, Is.EqualTo(64));
        Assert.That(beatData.beat.total, Is.EqualTo(384));
        Assert.That(beatData.bar.current, Is.EqualTo(16));
        Assert.That(beatData.bar.nextMs, Is.EqualTo(777));
        Assert.That(beatData.beatInBar, Is.EqualTo(3));
        Assert.That(beatData.beatsCountMs, Is.EqualTo(new[] { 100, 200, 300, 400 }));
        Assert.That(beatData.onBeats, Is.EqualTo(new[] { false, false, true, false }));
        Assert.That(beatData.beatAverageMs, Is.EqualTo(468));
        Assert.That(beatData.beatPulse, Is.EqualTo(0.625f));
        Assert.That(beatData.levels.low, Is.EqualTo(0.25f));
        Assert.That(beatData.levels.mid, Is.EqualTo(0.5f));
        Assert.That(beatData.levels.high, Is.EqualTo(0.75f));
        Assert.That(beatData.phaseState.current, Is.EqualTo("Drop"));
        Assert.That(beatData.phaseState.next, Is.EqualTo("Break"));
        Assert.That(beatData.phaseState.active, Is.True);
        Assert.That(beatData.phaseState.countBeats, Is.EqualTo(12));
        Assert.That(beatData.phaseState.lengthBeats, Is.EqualTo(32));
        Assert.That(beatData.phaseState.remaining, Is.EqualTo(8));
        Assert.That(beatData.dropState.active, Is.True);
        Assert.That(beatData.dropState.countBeats, Is.EqualTo(0));
        Assert.That(beatData.dropState.lengthBeats, Is.EqualTo(32));
        Assert.That(beatData.dropState.remaining, Is.EqualTo(2));
        Assert.That(beatData.fillState.active, Is.False);
        Assert.That(beatData.fillState.countBeats, Is.EqualTo(16));
        Assert.That(beatData.fillState.lengthBeats, Is.EqualTo(8));
        Assert.That(beatData.fillState.remaining, Is.EqualTo(1));
        Assert.That(beatData.energyState.current, Is.EqualTo("High"));
        Assert.That(beatData.energyState.next, Is.EqualTo("Mid"));
        Assert.That(beatData.energyState.active, Is.True);
        Assert.That(beatData.energyState.countBeats, Is.EqualTo(4));
        Assert.That(beatData.energyState.lengthBeats, Is.EqualTo(16));
        Assert.That(beatData.energyState.remaining, Is.EqualTo(2));
    }

    [Test]
    public void ApplySnapshotDerivesCompatibilityBeatFieldsFromStructuredOnAirData()
    {
        var beatData = new BeatData { beatsPerMeasure = 4 };
        var snapshot = BuildSnapshot(onBeatForBeat3: true);

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.currentBeat, Is.EqualTo(2));
        Assert.That(beatData.nextBeatMs, Is.EqualTo(100));
        Assert.That(beatData.GetNextBeatMs(), Is.EqualTo(100));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.beatPulse, Is.EqualTo(0.625f));
    }

    [Test]
    public void ApplySnapshotUsesBeatCountdownWhenCurrentBeatGateIsOff()
    {
        var beatData = new BeatData { beatsPerMeasure = 4 };
        var snapshot = BuildSnapshot(onBeatForBeat3: false);

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.nextBeatMs, Is.EqualTo(100));
        Assert.That(beatData.GetNextBeatMs(), Is.EqualTo(100));
    }

    [Test]
    public void ApplySnapshotDisablesBeatDataWhenRaveHasNoUsableBeat()
    {
        var beatData = new BeatData
        {
            active = true,
            currentBeat = 2,
            beatInBar = 3,
            onBeats = new[] { false, false, true, false },
            beatPulse = 1f,
        };
        beatData.bpm = 128f;
        beatData.playersLive = "4";
        beatData.track = "Track";

        var snapshot = new RaveOnAirSnapshot
        {
            bpm = -1f,
            beatInBar = -1,
            beatPulse = 0f,
            beatAverageMs = -1,
            beatsCountMs = new[] { -1, -1, -1, -1 },
            onBeats = new[] { false, false, false, false },
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.active, Is.False);
        Assert.That(beatData.bpm, Is.EqualTo(-1f));
        Assert.That(beatData.currentBeat, Is.EqualTo(0));
        Assert.That(beatData.GetNextBeatMs(), Is.EqualTo(-1));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.offBeatsCountMs, Is.EqualTo(new[] { -1, -1, -1, -1 }));
        Assert.That(beatData.offBeats, Is.EqualTo(new[] { false, false, false, false }));
        Assert.That(beatData.beatPulse, Is.EqualTo(0f));
        Assert.That(beatData.playersLive, Is.EqualTo(""));
        Assert.That(beatData.track, Is.EqualTo(""));
    }

    [Test]
    public void ApplySnapshotDerivesOffBeatArraysFromOscBeatCountdowns()
    {
        var beatData = new BeatData();
        var snapshot = new RaveOnAirSnapshot
        {
            bpm = 120f,
            beatAverageMs = 500,
            beatsCountMs = new[] { 1750, 250, 750, 1250 },
            onBeats = new[] { false, false, false, false },
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.offBeatsCountMs, Is.EqualTo(new[] { 0, 500, 1000, 1500 }));
        Assert.That(beatData.offBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatData.offBeatPulse, Is.EqualTo(1f));
    }

    [Test]
    public void ApplySnapshotUsesActualCountdownGapForOffBeatMidpoint()
    {
        var beatData = new BeatData();
        var snapshot = new RaveOnAirSnapshot
        {
            bpm = 120f,
            beatAverageMs = 500,
            beatsCountMs = new[] { 1800, 200, 800, 1300 },
            onBeats = new[] { false, false, false, false },
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.offBeatsCountMs[0], Is.EqualTo(0));
        Assert.That(beatData.offBeats[0], Is.True);
    }

    [Test]
    public void ApplySnapshotKeepsOffBeatGateOpenForQuarterOfAverageBeat()
    {
        var beatData = new BeatData();
        var snapshot = new RaveOnAirSnapshot
        {
            bpm = 120f,
            beatAverageMs = 500,
            beatsCountMs = new[] { 1700, 200, 700, 1200 },
            onBeats = new[] { false, false, false, false },
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.offBeatsCountMs, Is.EqualTo(new[] { 0, 450, 950, 1450 }));
        Assert.That(beatData.offBeats, Is.EqualTo(new[] { true, false, false, false }));
        Assert.That(beatData.offBeatPulse, Is.EqualTo(0.972f).Within(0.001f));
    }

    [Test]
    public void ApplySnapshotTurnsOffBeatGateOffAfterQuarterOfAverageBeat()
    {
        var beatData = new BeatData();
        var snapshot = new RaveOnAirSnapshot
        {
            bpm = 120f,
            beatAverageMs = 500,
            beatsCountMs = new[] { 1570, 70, 570, 1070 },
            onBeats = new[] { false, false, false, false },
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.offBeatsCountMs, Is.EqualTo(new[] { 1820, 320, 820, 1320 }));
        Assert.That(beatData.offBeats, Is.EqualTo(new[] { false, false, false, false }));
        Assert.That(beatData.offBeatPulse, Is.EqualTo(0.705f).Within(0.001f));
    }

    [Test]
    public void GetBeatBrightnessUsesBeatAndOffBeatPulsesForMusicalVariants()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.active = true;
        beatManager.beatData.beatPulse = 0.25f;
        beatManager.beatData.offBeatPulse = 0.75f;
        beatManager.beatData.currentBeat = 0;

        Assert.That(beatManager.GetBeatBrightness(0, 1f, 0.5f), Is.EqualTo(0.625f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(5, 1f, 0.5f), Is.EqualTo(0.875f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(6, 1f, 0.5f), Is.EqualTo(0.875f).Within(0.0001f));
    }

    [Test]
    public void IsBeatTriggeredUsesRaveOnBeatValue()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.active = true;
        beatManager.beatData.beatInBar = 1;
        beatManager.beatData.onBeats = new[] { true, false, false, false };
        beatManager.beatData.currentBeat = 0;

        Assert.That(beatManager.IsBeatTriggered(0), Is.True);
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
            phaseState = new PhaseState { current = "Drop", next = "Break", active = true, countBeats = 12, lengthBeats = 32, remaining = 8 },
            dropState = new CountdownState { active = true, countBeats = 0, lengthBeats = 32, remaining = 2 },
            fillState = new CountdownState { active = false, countBeats = 16, lengthBeats = 8, remaining = 1 },
            energyState = new PhaseState { current = "High", next = "Mid", active = true, countBeats = 4, lengthBeats = 16, remaining = 2 },
        };
    }
}
