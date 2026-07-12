#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

/// <summary>
/// Standing guardrail for the full RaveSystem on-air ingestion path: a packet that writes EVERY registered
/// <c>/rave/onair/*</c> address is dispatched through <see cref="RaveOscPacketParser"/>, the taken snapshot is
/// fed into a <see cref="BeatManager"/>, and the nullable query surface (ADR-0002) is asserted. The point is
/// that adding a wire field but forgetting to wire it through to the queries makes this fail, not pass silently.
/// </summary>
public sealed class RaveOscIngestionRoundTripTests
{
    /// <summary>Verifies every on-air OSC address reaches the nullable BeatManager query surface.</summary>
    [Test]
    public void EveryOnAirAddressFlowsThroughToTheNullableQuerySurface()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket());

        Assert.That(beatManager.IsActive, Is.True);
        Assert.That(beatManager.Bpm, Is.EqualTo(128.5f).Within(0.0001f));
        Assert.That(beatManager.Beat, Is.EqualTo(64));
        Assert.That(beatManager.TotalBeats, Is.EqualTo(384));
        Assert.That(beatManager.Bar, Is.EqualTo(16));
        Assert.That(beatManager.BeatInBar, Is.EqualTo(3));
        Assert.That(beatManager.TrackText, Is.EqualTo("Artist - Track"));
        Assert.That(beatManager.PlayersLive, Is.EqualTo("4,2"));

        var drop = beatManager.DropQuery;
        Assert.That(drop, Is.Not.Null);
        Assert.That(drop!.Value.inProgress, Is.True);
        Assert.That(drop.Value.remaining, Is.EqualTo(2));

        var fill = beatManager.FillQuery;
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.False);
        Assert.That(fill.Value.beatsUntilStart, Is.EqualTo(16));

        var phrase = beatManager.PhraseQuery;
        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.Value.label, Is.EqualTo("Drop"));
        Assert.That(phrase.Value.irregular, Is.True);
        Assert.That(phrase.Value.lengthBeats, Is.EqualTo(32));

        var nextPhrase = beatManager.NextPhrase;
        Assert.That(nextPhrase, Is.Not.Null);
        Assert.That(nextPhrase!.Value.label, Is.EqualTo("Break"));
        Assert.That(nextPhrase.Value.beatsUntilChange, Is.EqualTo(8));
        Assert.That(nextPhrase.Value.lengthBeats, Is.EqualTo(16));

        var energy = beatManager.EnergyQuery;
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.Mid));
        Assert.That(energy.Value.nextRunLengthBeats, Is.EqualTo(64));

        var loop = beatManager.LoopQuery;
        Assert.That(loop, Is.Not.Null);
        Assert.That(loop!.Value.looping, Is.True);
        Assert.That(loop.Value.regionSet, Is.True);
        Assert.That(loop.Value.lengthBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(loop.Value.lengthMs, Is.EqualTo(938));
        Assert.That(loop.Value.sizeNumerator, Is.EqualTo(1));
        Assert.That(loop.Value.sizeDenominator, Is.EqualTo(2));

        Assert.That(beatManager.TrackId, Is.EqualTo(777001));

        var levels = beatManager.Levels;
        Assert.That(levels, Is.Not.Null);
        Assert.That(levels!.Value.low, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(levels.Value.mid, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(levels.Value.high, Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void PhraseStateOmittedLeavesPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/phrase_state"));

        Assert.That(beatManager.PhraseQuery, Is.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.EnergyQuery, Is.Not.Null);
        Assert.That(beatManager.LoopQuery, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void NextPhraseStateOmittedLeavesNextPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_phrase_state"));

        Assert.That(beatManager.NextPhrase, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Not.Null);
        Assert.That(beatManager.EnergyQuery, Is.Not.Null);
        Assert.That(beatManager.LoopQuery, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void EnergyStateOmittedLeavesEnergyNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/energy_state"));

        Assert.That(beatManager.EnergyQuery, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.LoopQuery, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void NextEnergyStateOmittedLeavesEnergyNextNullButEnergyAndSiblingsPopulated()
    {
        // next_energy_state has no dedicated query of its own; it folds into Energy.next, so omitting
        // it should null out only that field, not the whole Energy result.
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_energy_state"));

        var energy = beatManager.EnergyQuery;
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.LoopQuery, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void LoopStateOmittedLeavesLoopNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/loop_state"));

        Assert.That(beatManager.LoopQuery, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.EnergyQuery, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void TrackIdOmittedLeavesTrackIdNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/track_id"));

        Assert.That(beatManager.TrackId, Is.Null);
        Assert.That(beatManager.PhraseQuery, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.EnergyQuery, Is.Not.Null);
        Assert.That(beatManager.LoopQuery, Is.Not.Null);
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
