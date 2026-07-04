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
        Assert.That(beatManager.Track, Is.EqualTo("Artist - Track"));
        Assert.That(beatManager.PlayersLive, Is.EqualTo("4,2"));

        var drop = beatManager.Drop;
        Assert.That(drop, Is.Not.Null);
        Assert.That(drop!.Value.inProgress, Is.True);
        Assert.That(drop.Value.remaining, Is.EqualTo(2));

        var fill = beatManager.Fill;
        Assert.That(fill, Is.Not.Null);
        Assert.That(fill!.Value.inProgress, Is.False);
        Assert.That(fill.Value.beatsUntilStart, Is.EqualTo(16));

        var phrase = beatManager.Phrase;
        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.Value.label, Is.EqualTo("Drop"));
        Assert.That(phrase.Value.irregular, Is.True);
        Assert.That(phrase.Value.lengthBeats, Is.EqualTo(32));

        var nextPhrase = beatManager.NextPhrase;
        Assert.That(nextPhrase, Is.Not.Null);
        Assert.That(nextPhrase!.Value.label, Is.EqualTo("Break"));
        Assert.That(nextPhrase.Value.beatsUntilChange, Is.EqualTo(8));
        Assert.That(nextPhrase.Value.lengthBeats, Is.EqualTo(16));

        var energy = beatManager.Energy;
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.Mid));
        Assert.That(energy.Value.nextRunLengthBeats, Is.EqualTo(64));

        var loop = beatManager.Loop;
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

        Assert.That(beatManager.Phrase, Is.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.Energy, Is.Not.Null);
        Assert.That(beatManager.Loop, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void NextPhraseStateOmittedLeavesNextPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_phrase_state"));

        Assert.That(beatManager.NextPhrase, Is.Null);
        Assert.That(beatManager.Phrase, Is.Not.Null);
        Assert.That(beatManager.Energy, Is.Not.Null);
        Assert.That(beatManager.Loop, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void EnergyStateOmittedLeavesEnergyNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/energy_state"));

        Assert.That(beatManager.Energy, Is.Null);
        Assert.That(beatManager.Phrase, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.Loop, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void NextEnergyStateOmittedLeavesEnergyNextNullButEnergyAndSiblingsPopulated()
    {
        // next_energy_state has no dedicated query of its own; it folds into Energy.next, so omitting
        // it should null out only that field, not the whole Energy result.
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_energy_state"));

        var energy = beatManager.Energy;
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.Null);
        Assert.That(beatManager.Phrase, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.Loop, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void LoopStateOmittedLeavesLoopNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/loop_state"));

        Assert.That(beatManager.Loop, Is.Null);
        Assert.That(beatManager.Phrase, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.Energy, Is.Not.Null);
        Assert.That(beatManager.TrackId, Is.Not.Null);
    }

    [Test]
    public void TrackIdOmittedLeavesTrackIdNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/track_id"));

        Assert.That(beatManager.TrackId, Is.Null);
        Assert.That(beatManager.Phrase, Is.Not.Null);
        Assert.That(beatManager.NextPhrase, Is.Not.Null);
        Assert.That(beatManager.Energy, Is.Not.Null);
        Assert.That(beatManager.Loop, Is.Not.Null);
    }

    /// <summary>Dispatches the packet, takes the snapshot, and feeds it into a live-sourced BeatManager.</summary>
    private static BeatManager BuildLiveBeatManagerFromFullPacket(byte[] packet)
    {
        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet);
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);

        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.beatData.CopyFrom(snapshot);
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
        if (omitAddress != "/rave/onair/players_live") WriteString(ref bundle, "/rave/onair/players_live", "4,2");
        if (omitAddress != "/rave/onair/track") WriteString(ref bundle, "/rave/onair/track", "Artist - Track");
        if (omitAddress != "/rave/onair/bpm") WriteFloat(ref bundle, "/rave/onair/bpm", 128.5f);
        if (omitAddress != "/rave/onair/beat") WriteInt(ref bundle, "/rave/onair/beat", 64);
        if (omitAddress != "/rave/onair/total_beats") WriteInt(ref bundle, "/rave/onair/total_beats", 384);
        if (omitAddress != "/rave/onair/bar") WriteInt(ref bundle, "/rave/onair/bar", 16);
        if (omitAddress != "/rave/onair/next_bar_ms") WriteInt(ref bundle, "/rave/onair/next_bar_ms", 777);
        if (omitAddress != "/rave/onair/beat_in_bar") WriteInt(ref bundle, "/rave/onair/beat_in_bar", 3);
        if (omitAddress != "/rave/onair/beats_count_ms") WriteFourInts(ref bundle, "/rave/onair/beats_count_ms", 100, 200, 300, 400);
        if (omitAddress != "/rave/onair/on_beats") WriteFourInts(ref bundle, "/rave/onair/on_beats", 0, 0, 1, 0);
        if (omitAddress != "/rave/onair/beat_avg_ms") WriteInt(ref bundle, "/rave/onair/beat_avg_ms", 468);
        if (omitAddress != "/rave/onair/beat_pulse") WriteFloat(ref bundle, "/rave/onair/beat_pulse", 0.625f);
        if (omitAddress != "/rave/onair/levels") WriteThreeFloats(ref bundle, "/rave/onair/levels", 0.25f, 0.5f, 0.75f);
        if (omitAddress != "/rave/onair/phrase_state") WritePhraseState(ref bundle, "/rave/onair/phrase_state", "Drop", 12, 32, 1);
        if (omitAddress != "/rave/onair/next_phrase_state") WriteLabeledCountdown(ref bundle, "/rave/onair/next_phrase_state", "Break", 8, 16);
        if (omitAddress != "/rave/onair/drop_state") WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 0, 32, 2);
        if (omitAddress != "/rave/onair/fill_state") WriteCountdownState(ref bundle, "/rave/onair/fill_state", 0, 16, 8, 1);
        if (omitAddress != "/rave/onair/energy_state") WriteLabeledCountdown(ref bundle, "/rave/onair/energy_state", "High", 4, 16);
        if (omitAddress != "/rave/onair/next_energy_state") WriteLabeledCountdown(ref bundle, "/rave/onair/next_energy_state", "Mid", 20, 64);
        if (omitAddress != "/rave/onair/loop_state") WriteLoopState(ref bundle, "/rave/onair/loop_state", 1, 1, 0.5f, 938, 1, 2);
        if (omitAddress != "/rave/onair/timing_grid") WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 5, 2, "locked");
        if (omitAddress != "/rave/onair/track_id") WriteInt(ref bundle, "/rave/onair/track_id", 777001);
        var length = bundle.Finish();
        return buffer.AsSpan(0, length).ToArray();
    }

    private static void WriteInt(ref OscBundleWriter bundle, string address, int value)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteFourInts(ref OscBundleWriter bundle, string address, int first, int second, int third, int fourth)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(first);
        writer.WriteInt32(second);
        writer.WriteInt32(third);
        writer.WriteInt32(fourth);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteFloat(ref OscBundleWriter bundle, string address, float value)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteThreeFloats(ref OscBundleWriter bundle, string address, float low, float mid, float high)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(low);
        writer.WriteFloat32(mid);
        writer.WriteFloat32(high);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteString(ref OscBundleWriter bundle, string address, string value)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(value);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes a <c>siii</c> phrase_state lane: label, countBeats, lengthBeats, irregular tri-state.</summary>
    private static void WritePhraseState(
        ref OscBundleWriter bundle,
        string address,
        string label,
        int countBeats,
        int lengthBeats,
        int irregular)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(label);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        writer.WriteInt32(irregular);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes a <c>sii</c> labeled-countdown lane shared by next_phrase_state/energy_state/next_energy_state.</summary>
    private static void WriteLabeledCountdown(
        ref OscBundleWriter bundle,
        string address,
        string label,
        int countBeats,
        int lengthBeats)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(label);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes an <c>iifiii</c> loop_state lane: active/set tri-states, lengthBeats (float), lengthMs, size fraction.</summary>
    private static void WriteLoopState(
        ref OscBundleWriter bundle,
        string address,
        int active,
        int set,
        float lengthBeats,
        int lengthMs,
        int sizeNumerator,
        int sizeDenominator)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(active);
        writer.WriteInt32(set);
        writer.WriteFloat32(lengthBeats);
        writer.WriteInt32(lengthMs);
        writer.WriteInt32(sizeNumerator);
        writer.WriteInt32(sizeDenominator);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes an <c>iis</c> timing_grid lane: beat, bar, grid-confidence state string.</summary>
    private static void WriteTimingGrid(ref OscBundleWriter bundle, string address, int beat, int bar, string state)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(beat);
        writer.WriteInt32(bar);
        writer.WriteString(state);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteCountdownState(ref OscBundleWriter bundle, string address, int active, int countBeats, int lengthBeats, int remaining)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(active);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        writer.WriteInt32(remaining);
        bundle.EndElement(writer.Finish());
    }
}
