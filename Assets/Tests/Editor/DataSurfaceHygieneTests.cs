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
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        beatManager.WireSnapshot.playersLive = "4,2";
        beatManager.Update(0.25f);

        var players = beatManager.Track.PlayersLive;
        Assert.That(players, Is.Not.Null);
        Assert.That(players, Is.Not.InstanceOf<List<int>>());

        var mutableView = players as IList<int>;
        Assert.That(mutableView, Is.Not.Null);
        Assert.Throws<NotSupportedException>(() => mutableView!.Add(8));
        Assert.That(beatManager.Track.PlayersLive, Is.EqualTo(new[] { 4, 2 }));
    }

    /// <summary>An out-of-range Duration serves unavailable facts and never reports an opening edge.</summary>
    [Test]
    public void InvalidDurationReadsNullFactsAndFalseEdge()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        beatManager.Update(0.25f);
        var invalidDuration = (Duration)99;

        Assert.That(beatManager.Pulses.Every(invalidDuration), Is.Null);
        Assert.That(beatManager.Pulses.GateEvery(invalidDuration), Is.Null);
        Assert.That(beatManager.Pulses.GateOpenedEvery(invalidDuration), Is.False);
    }
}
