#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;

/// <summary>Integration tests at BeatManager's OSC snapshot boundary.</summary>
public sealed class BeatManagerRaveOscIntegrationTests
{
    /// <summary>Verifies BeatManager derives the Off Beat lanes and pulse from live On Beat countdowns.</summary>
    [Test]
    public void UpdateDerivesOffbeatsFromLiveBeatCountdowns()
    {
        var beatManager = BeatClockFixture.CreateSeeded(120f, 0.25f);
        beatManager.Update(0.25f);

        Assert.That(beatManager.Timing.BeatInBar, Is.EqualTo(1));
        Assert.That(beatManager.Offbeats.OffBeatMs(1), Is.Zero);
        Assert.That(beatManager.Offbeats.OffBeat(1), Is.True);
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>Verifies frame capture preserves the live wire values supplied at ingress.</summary>
    [Test]
    public void UpdateDoesNotOverwriteLiveWireValues()
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

        Assert.That(beatManager.Timing.Bpm, Is.EqualTo(128f));
        Assert.That(beatManager.Timing.BeatInBar, Is.EqualTo(3));
        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0.25f));
    }

    /// <summary>Verifies ingress owns the mutable wire arrays before the caller can change them.</summary>
    [Test]
    public void SnapshotIngressOwnsADeepCopy()
    {
        var snapshot = new RaveOnAirSnapshot
        {
            beatInBar = 1,
            beatsCountMs = new[] { 0, 500, 1000, 1500 },
            onBeats = new[] { true, false, false, false },
        };
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        snapshot.beatsCountMs[0] = 999;
        snapshot.onBeats[0] = false;
        beatManager.Update(0f);

        Assert.That(beatManager.Beats.OnBeatMs(1), Is.Zero);
        Assert.That(beatManager.Beats.OnBeat(1), Is.True);
    }

    /// <summary>Verifies the OSC adapter's broadcast-liveness grace window at both sides of its threshold.</summary>
    [Test]
    public void BroadcastLivenessUsesTheDocumentedGraceWindow()
    {
        const float grace = 15f / 60f;
        Assert.That(RaveOscReceiver.IsBroadcastingAt(false, 0f), Is.False);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(true, grace - 0.001f), Is.True);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(true, grace + 0.001f), Is.False);
    }
}
