#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

/// <summary>
/// Standing guardrail for the full RaveSystem on-air ingestion path. An explicit bundle containing the
/// complete current <c>/rave/onair/*</c> address set passes through <see cref="RaveOscPacketParser"/> and
/// <see cref="BeatManager"/> before its caller-visible Data Surface values are asserted.
/// </summary>
public sealed class RaveOscIngestionRoundTripTests
{
    /// <summary>Verifies every current on-air OSC address reaches its canonical BeatManager group.</summary>
    [Test]
    public void EveryOnAirAddressFlowsThroughToTheDataSurface()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket());

        Assert.That(beatManager.IsSynced, Is.True);
        Assert.That(beatManager.Timing.Bpm, Is.EqualTo(128.5f).Within(0.0001f));
        Assert.That(beatManager.Timing.BeatAverageMilliseconds, Is.EqualTo(468));
        Assert.That(beatManager.Timing.Beat, Is.EqualTo(64));
        Assert.That(beatManager.Timing.TotalBeats, Is.EqualTo(384));
        Assert.That(beatManager.Timing.Bar, Is.EqualTo(16));
        Assert.That(beatManager.Timing.NextBarMilliseconds, Is.EqualTo(777));
        Assert.That(beatManager.Timing.BeatInBar, Is.EqualTo(3));

        Assert.That(beatManager.Beats.OnBeatMs(1), Is.EqualTo(100));
        Assert.That(beatManager.Beats.OnBeatMs(2), Is.EqualTo(200));
        Assert.That(beatManager.Beats.OnBeatMs(3), Is.EqualTo(300));
        Assert.That(beatManager.Beats.OnBeatMs(4), Is.EqualTo(400));
        Assert.That(beatManager.Beats.OnBeat(1), Is.False);
        Assert.That(beatManager.Beats.OnBeat(2), Is.False);
        Assert.That(beatManager.Beats.OnBeat(3), Is.True);
        Assert.That(beatManager.Beats.OnBeat(4), Is.False);
        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0.625f).Within(0.0001f));

        Assert.That(beatManager.Track.Title, Is.EqualTo("Artist - Track"));
        Assert.That(beatManager.Track.PlayersLive, Is.EqualTo(new[] { 4, 2 }));
        Assert.That(beatManager.Track.Id, Is.EqualTo(777001));

        Assert.That(beatManager.Phrase.Name, Is.EqualTo("Drop"));
        Assert.That(beatManager.Phrase.BeatsRemaining, Is.EqualTo(12));
        Assert.That(beatManager.Phrase.Irregular, Is.True);
        Assert.That(beatManager.Phrase.LengthBeats, Is.EqualTo(32));
        Assert.That(beatManager.NextPhrase.Name, Is.EqualTo("Break"));
        Assert.That(beatManager.NextPhrase.BeatsUntil, Is.EqualTo(8));
        Assert.That(beatManager.NextPhrase.LengthBeats, Is.EqualTo(16));

        Assert.That(beatManager.Drop.Active, Is.True);
        Assert.That(beatManager.Drop.CountBeats, Is.Zero);
        Assert.That(beatManager.Drop.LengthBeats, Is.EqualTo(32));
        Assert.That(beatManager.Drop.Remaining, Is.EqualTo(2));
        Assert.That(beatManager.Drop.BeatsRemaining, Is.Zero);
        Assert.That(beatManager.Fill.Active, Is.False);
        Assert.That(beatManager.Fill.CountBeats, Is.EqualTo(16));
        Assert.That(beatManager.Fill.LengthBeats, Is.EqualTo(8));
        Assert.That(beatManager.Fill.Remaining, Is.EqualTo(1));
        Assert.That(beatManager.Fill.BeatsUntil, Is.EqualTo(16));

        Assert.That(beatManager.Energy.Level, Is.EqualTo(Energy.High));
        Assert.That(beatManager.Energy.BeatsRemaining, Is.EqualTo(4));
        Assert.That(beatManager.Energy.LengthBeats, Is.EqualTo(16));
        Assert.That(beatManager.Energy.Trend, Is.EqualTo(EnergyTrend.Falling));
        Assert.That(beatManager.NextEnergy.Level, Is.EqualTo(Energy.Mid));
        Assert.That(beatManager.NextEnergy.BeatsUntil, Is.EqualTo(20));
        Assert.That(beatManager.NextEnergy.LengthBeats, Is.EqualTo(64));

        Assert.That(beatManager.Loop.Rolling, Is.True);
        Assert.That(beatManager.Loop.RegionSet, Is.True);
        Assert.That(beatManager.Loop.LengthBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Loop.LengthMilliseconds, Is.EqualTo(938));
        Assert.That(beatManager.Loop.SizeNumerator, Is.EqualTo(1));
        Assert.That(beatManager.Loop.SizeDenominator, Is.EqualTo(2));
        Assert.That(beatManager.Loop.NominalSizeBeats, Is.EqualTo(0.5f).Within(0.0001f));

        Assert.That(beatManager.Grid.State, Is.EqualTo(GridState.Locked));
        Assert.That(beatManager.Grid.Beat, Is.EqualTo(5));
        Assert.That(beatManager.Grid.Bar, Is.EqualTo(2));

        Assert.That(beatManager.Levels.Normalized.Low, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(beatManager.Levels.Normalized.Mid, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Levels.Normalized.High, Is.EqualTo(0.75f).Within(0.0001f));
    }

    /// <summary>Verifies an omitted current Phrase lane does not erase populated sibling groups.</summary>
    [Test]
    public void PhraseStateOmittedLeavesPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/phrase_state"));

        Assert.That(beatManager.Phrase.Name, Is.Null);
        Assert.That(beatManager.NextPhrase.Name, Is.Not.Null);
        Assert.That(beatManager.Energy.Level, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.True);
        Assert.That(beatManager.Track.Id, Is.Not.Null);
    }

    /// <summary>Verifies an omitted next Phrase lane nulls only the next announcement.</summary>
    [Test]
    public void NextPhraseStateOmittedLeavesNextPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_phrase_state"));

        Assert.That(beatManager.NextPhrase.Name, Is.Null);
        Assert.That(beatManager.Phrase.Name, Is.Not.Null);
        Assert.That(beatManager.Energy.Level, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.True);
        Assert.That(beatManager.Track.Id, Is.Not.Null);
    }

    /// <summary>Verifies an omitted current Energy lane does not erase populated sibling groups.</summary>
    [Test]
    public void EnergyStateOmittedLeavesEnergyNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/energy_state"));

        Assert.That(beatManager.Energy.Level, Is.Null);
        Assert.That(beatManager.Phrase.Name, Is.Not.Null);
        Assert.That(beatManager.NextPhrase.Name, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.True);
        Assert.That(beatManager.Track.Id, Is.Not.Null);
    }

    /// <summary>Verifies an omitted next Energy lane preserves current Energy and nulls only its successor.</summary>
    [Test]
    public void NextEnergyStateOmittedLeavesNextEnergyNullButCurrentAndSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_energy_state"));

        Assert.That(beatManager.Energy.Level, Is.EqualTo(Energy.High));
        Assert.That(beatManager.NextEnergy.Level, Is.Null);
        Assert.That(beatManager.Phrase.Name, Is.Not.Null);
        Assert.That(beatManager.NextPhrase.Name, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.True);
        Assert.That(beatManager.Track.Id, Is.Not.Null);
    }

    /// <summary>Verifies an omitted Loop lane serves unavailable Loop facts without disturbing siblings.</summary>
    [Test]
    public void LoopStateOmittedLeavesLoopNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/loop_state"));

        Assert.That(beatManager.Loop.Rolling, Is.False);
        Assert.That(beatManager.Loop.LengthBeats, Is.Null);
        Assert.That(beatManager.Phrase.Name, Is.Not.Null);
        Assert.That(beatManager.NextPhrase.Name, Is.Not.Null);
        Assert.That(beatManager.Energy.Level, Is.Not.Null);
        Assert.That(beatManager.Track.Id, Is.Not.Null);
    }

    /// <summary>Verifies an omitted track id translates to null without disturbing sibling groups.</summary>
    [Test]
    public void TrackIdOmittedLeavesTrackIdNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/track_id"));

        Assert.That(beatManager.Track.Id, Is.Null);
        Assert.That(beatManager.Phrase.Name, Is.Not.Null);
        Assert.That(beatManager.NextPhrase.Name, Is.Not.Null);
        Assert.That(beatManager.Energy.Level, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.True);
    }

    /// <summary>Dispatches the packet, takes the snapshot, and feeds it into a live-sourced BeatManager.</summary>
    private static BeatManager BuildLiveBeatManagerFromFullPacket(byte[] packet)
    {
        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet);
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);

        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        return beatManager;
    }

    /// <summary>
    /// Builds one bundle carrying every registered v2 on-air address, optionally omitting exactly one
    /// address so its per-lane "unavailable" behavior can be exercised while every sibling lane stays live.
    /// </summary>
    private static byte[] BuildFullOnAirPacket(string? omitAddress = null)
    {
        var buffer = new byte[4096];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);
        if (omitAddress != "/rave/onair/players_live") OnAirOscWriter.WriteString(ref bundle, "/rave/onair/players_live", "4,2");
        if (omitAddress != "/rave/onair/track") OnAirOscWriter.WriteString(ref bundle, "/rave/onair/track", "Artist - Track");
        if (omitAddress != "/rave/onair/bpm") OnAirOscWriter.WriteFloat(ref bundle, "/rave/onair/bpm", 128.5f);
        if (omitAddress != "/rave/onair/beat") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat", 64);
        if (omitAddress != "/rave/onair/total_beats") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/total_beats", 384);
        if (omitAddress != "/rave/onair/bar") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/bar", 16);
        if (omitAddress != "/rave/onair/next_bar_ms") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/next_bar_ms", 777);
        if (omitAddress != "/rave/onair/beat_in_bar") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 3);
        if (omitAddress != "/rave/onair/beats_count_ms") OnAirOscWriter.WriteFourInts(ref bundle, "/rave/onair/beats_count_ms", 100, 200, 300, 400);
        if (omitAddress != "/rave/onair/on_beats") OnAirOscWriter.WriteFourInts(ref bundle, "/rave/onair/on_beats", 0, 0, 1, 0);
        if (omitAddress != "/rave/onair/beat_avg_ms") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_avg_ms", 468);
        if (omitAddress != "/rave/onair/beat_pulse") OnAirOscWriter.WriteFloat(ref bundle, "/rave/onair/beat_pulse", 0.625f);
        if (omitAddress != "/rave/onair/levels") OnAirOscWriter.WriteThreeFloats(ref bundle, "/rave/onair/levels", 0.25f, 0.5f, 0.75f);
        if (omitAddress != "/rave/onair/phrase_state") OnAirOscWriter.WritePhraseState(ref bundle, "/rave/onair/phrase_state", "Drop", 12, 32, 1);
        if (omitAddress != "/rave/onair/next_phrase_state") OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/next_phrase_state", "Break", 8, 16);
        if (omitAddress != "/rave/onair/drop_state") OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 0, 32, 2);
        if (omitAddress != "/rave/onair/fill_state") OnAirOscWriter.WriteCountdownState(ref bundle, "/rave/onair/fill_state", 0, 16, 8, 1);
        if (omitAddress != "/rave/onair/energy_state") OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/energy_state", "High", 4, 16);
        if (omitAddress != "/rave/onair/next_energy_state") OnAirOscWriter.WriteLabeledCountdown(ref bundle, "/rave/onair/next_energy_state", "Mid", 20, 64);
        if (omitAddress != "/rave/onair/loop_state") OnAirOscWriter.WriteLoopState(ref bundle, "/rave/onair/loop_state", 1, 1, 0.5f, 938, 1, 2);
        if (omitAddress != "/rave/onair/timing_grid") OnAirOscWriter.WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 5, 2, "locked");
        if (omitAddress != "/rave/onair/track_id") OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/track_id", 777001);
        var length = bundle.Finish();
        return buffer.AsSpan(0, length).ToArray();
    }
}
