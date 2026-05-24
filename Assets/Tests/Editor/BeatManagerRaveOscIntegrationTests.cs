// Copyright © 2026 Hunter Luisi. All rights reserved.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

public sealed class BeatManagerRaveOscIntegrationTests
{
    [Test]
    public void BeatManagerUpdateDoesNotSynthesizeLocalBeatState()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.active = false;
        beatManager.beatData.bpm = 111f;
        beatManager.beatData.currentBeat = 2;
        beatManager.beatData.timeEvent = -123;
        beatManager.beatData.onBeat = false;
        beatManager.beatData.beatPulse = 0.25f;

        beatManager.Update();

        Assert.That(beatManager.beatData.bpm, Is.EqualTo(111f));
        Assert.That(beatManager.beatData.currentBeat, Is.EqualTo(2));
        Assert.That(beatManager.beatData.timeEvent, Is.EqualTo(-123));
        Assert.That(beatManager.beatData.onBeat, Is.False);
        Assert.That(beatManager.beatData.beatPulse, Is.EqualTo(0.25f));
    }

    [TestCase(true, 0)]
    [TestCase(false, -321)]
    public void ApplySnapshotCopiesRaveBeatFieldsIntoBeatData(bool onBeat, int expectedTimeEvent)
    {
        var beatData = new BeatData
        {
            beatsPerMeasure = 4,
            onBeat = !onBeat,
            beatPulse = 0f,
            timeEvent = 99,
        };
        var snapshot = new RaveOscSnapshot
        {
            Bpm = 128.5f,
            BeatInBar = 3,
            NextBeatMs = 321,
            OnBeat = onBeat,
            BeatPulse = 0.625f,
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.active, Is.True);
        Assert.That(beatData.bpm, Is.EqualTo(128.5f));
        Assert.That(beatData.currentBeat, Is.EqualTo(2));
        Assert.That(beatData.onBeat, Is.EqualTo(onBeat));
        Assert.That(beatData.beatPulse, Is.EqualTo(0.625f));
        Assert.That(beatData.timeEvent, Is.EqualTo(expectedTimeEvent));
    }

    [Test]
    public void ApplySnapshotDisablesBeatDataWhenRaveHasNoUsableBeat()
    {
        var beatData = new BeatData
        {
            active = true,
            bpm = 128f,
            currentBeat = 2,
            timeEvent = -50,
            onBeat = true,
            beatPulse = 1f,
        };
        var snapshot = new RaveOscSnapshot
        {
            Bpm = -1f,
            BeatInBar = -1,
            NextBeatMs = -1,
            OnBeat = false,
            BeatPulse = 0f,
        };

        RaveOscReceiver.ApplySnapshotToBeatData(snapshot, beatData);

        Assert.That(beatData.active, Is.False);
        Assert.That(beatData.bpm, Is.EqualTo(120f));
        Assert.That(beatData.currentBeat, Is.EqualTo(0));
        Assert.That(beatData.timeEvent, Is.EqualTo(0));
        Assert.That(beatData.onBeat, Is.False);
        Assert.That(beatData.beatPulse, Is.EqualTo(0f));
    }

    [Test]
    public void GetBeatBrightnessUsesRaveBeatPulseForEveryVariant()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.active = true;
        beatManager.beatData.beatPulse = 0.25f;
        beatManager.beatData.currentBeat = 0;
        beatManager.beatData.timeEvent = 0;

        Assert.That(beatManager.GetBeatBrightness(0, 1f, 0.5f), Is.EqualTo(0.625f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(5, 1f, 0.5f), Is.EqualTo(0.625f).Within(0.0001f));
        Assert.That(beatManager.GetBeatBrightness(6, 1f, 0.5f), Is.EqualTo(0.625f).Within(0.0001f));
    }

    [Test]
    public void IsBeatTriggeredUsesRaveOnBeatValue()
    {
        var beatManager = new BeatManager();
        beatManager.beatData.active = true;
        beatManager.beatData.onBeat = true;
        beatManager.beatData.currentBeat = 0;

        Assert.That(beatManager.IsBeatTriggered(0), Is.True);
    }
}
