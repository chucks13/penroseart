// Correctness and encapsulation tests for captured BeatManager Data Surface facts.

#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Guards the Data Surface's captured-state boundaries: served collections are immutable and
/// invalid Duration values remain unavailable.
/// </summary>
public sealed class DataSurfaceHygieneTests
{
    /// <summary>The captured live-player list cannot be mutated through its runtime collection type.</summary>
    [Test]
    public void PlayersLiveCannotBeMutatedViaDowncast()
    {
        var beatManager = new BeatManager();
        var snapshot = BeatClockFixture.CreateSnapshot(bpm: 120f, timeSeconds: 0.25f);
        snapshot.playersLive = "4,2";
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0.25f);

        var players = beatManager.Track.PlayersLive;
        Assert.That(players, Is.Not.Null);
        Assert.That(players, Is.Not.InstanceOf<List<int>>());

        var mutableView = players as IList<int>;
        Assert.That(mutableView, Is.Not.Null);
        Assert.Throws<NotSupportedException>(() => mutableView!.Add(8));
        Assert.That(beatManager.Track.PlayersLive, Is.EqualTo(new[] { 4, 2 }));
    }

    /// <summary>A captured lane value remains stable after BeatManager captures a later frame.</summary>
    [Test]
    public void CapturedBeatLanesRemainStableAcrossLaterUpdates()
    {
        var beatManager = new BeatManager();
        BeatManagerWireFixture.Feed(beatManager, snapshot =>
        {
            snapshot.beatsCountMs = new[] { 0, 500, 1000, 1500 };
            snapshot.onBeats = new[] { true, false, false, false };
        });
        beatManager.Update(0f);
        var captured = beatManager.Beats;

        BeatManagerWireFixture.Feed(beatManager, snapshot =>
        {
            snapshot.beatsCountMs = new[] { 500, 0, 500, 1000 };
            snapshot.onBeats = new[] { false, true, false, false };
        });
        beatManager.Update(0.5f);

        Assert.That(captured.OnBeatMs(1), Is.Zero);
        Assert.That(captured.OnBeat(1), Is.True);
        Assert.That(beatManager.Beats.OnBeatMs(1), Is.EqualTo(500));
        Assert.That(beatManager.Beats.OnBeat(1), Is.False);
    }

    /// <summary>An out-of-range Duration serves unavailable pulse facts.</summary>
    [Test]
    public void InvalidDurationReadsNullFacts()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        beatManager.Update(0.25f);
        var invalidDuration = (Duration)99;

        Assert.That(beatManager.Pulses.Every(invalidDuration), Is.Null);
        Assert.That(beatManager.Pulses.On(invalidDuration), Is.Null);
        Assert.That(typeof(PulsesValues).GetMethod("GateOpenedEvery"), Is.Null);
    }
}
