// Copyright © 2026 Hunter Luisi. All rights reserved.
// Tests for PenroseArt's Rave OSC Unity adapter.

#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

namespace RaveSystem.Osc.Tests {

public sealed class RaveOscPacketParserTests {
    [Test]
    public void DispatchReadsEveryV2RaveOnAirLaneIntoOscShapedSnapshot() {
        var packet = new byte[4096];
        var bundle = new OscBundleWriter(packet, OscTimeTag.Immediately);
        WriteString(ref bundle, "/rave/onair/players_live", "4,2");
        WriteString(ref bundle, "/rave/onair/track", "Artist - Track");
        WriteFloat(ref bundle, "/rave/onair/bpm", 128.5f);
        WriteInt(ref bundle, "/rave/onair/beat", 64);
        WriteInt(ref bundle, "/rave/onair/total_beats", 384);
        WriteInt(ref bundle, "/rave/onair/bar", 16);
        WriteInt(ref bundle, "/rave/onair/next_bar_ms", 777);
        WriteInt(ref bundle, "/rave/onair/beat_in_bar", 3);
        WriteFourInts(ref bundle, "/rave/onair/beats_count_ms", 100, 200, 300, 400);
        WriteFourInts(ref bundle, "/rave/onair/on_beats", 0, 0, 1, 0);
        WriteInt(ref bundle, "/rave/onair/beat_avg_ms", 468);
        WriteFloat(ref bundle, "/rave/onair/beat_pulse", 0.625f);
        WriteThreeFloats(ref bundle, "/rave/onair/levels", 0.25f, 0.5f, 0.75f);
        WritePhraseState(ref bundle, "/rave/onair/phrase_state", "Drop", 12, 32, 1);
        WriteLabeledCountdown(ref bundle, "/rave/onair/next_phrase_state", "Break", 8, 16);
        WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 0, 32, 2);
        WriteCountdownState(ref bundle, "/rave/onair/fill_state", 0, 16, 8, 1);
        WriteLabeledCountdown(ref bundle, "/rave/onair/energy_state", "High", 4, 16);
        WriteLabeledCountdown(ref bundle, "/rave/onair/next_energy_state", "Mid", 20, 64);
        WriteLoopState(ref bundle, "/rave/onair/loop_state", 1, 1, 0.5f, 938, 1, 2);
        WriteTimingGrid(ref bundle, "/rave/onair/timing_grid", 5, 2, "locked");
        WriteInt(ref bundle, "/rave/onair/track_id", 777001);

        using var parser = new RaveOscPacketParser();
        var dispatched = parser.Dispatch(packet.AsSpan(0, bundle.Finish()));

        // 22 registered addresses: the constructor list in RaveOscPacketParser, counted by hand.
        Assert.That(dispatched, Is.EqualTo(22));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);

        Assert.That(snapshot.playersLive, Is.EqualTo("4,2"));
        Assert.That(snapshot.track, Is.EqualTo("Artist - Track"));
        Assert.That(snapshot.bpm, Is.EqualTo(128.5f));
        Assert.That(snapshot.beat.current, Is.EqualTo(64));
        Assert.That(snapshot.beat.total, Is.EqualTo(384));
        Assert.That(snapshot.bar.current, Is.EqualTo(16));
        Assert.That(snapshot.bar.nextMs, Is.EqualTo(777));
        Assert.That(snapshot.beatInBar, Is.EqualTo(3));
        Assert.That(snapshot.beatsCountMs, Is.EqualTo(new[] { 100, 200, 300, 400 }));
        Assert.That(snapshot.onBeats, Is.EqualTo(new[] { false, false, true, false }));
        Assert.That(snapshot.beatAverageMs, Is.EqualTo(468));
        Assert.That(snapshot.beatPulse, Is.EqualTo(0.625f));
        Assert.That(snapshot.levels.low, Is.EqualTo(0.25f));
        Assert.That(snapshot.levels.mid, Is.EqualTo(0.5f));
        Assert.That(snapshot.levels.high, Is.EqualTo(0.75f));

        Assert.That(snapshot.phraseState.label, Is.EqualTo("Drop"));
        Assert.That(snapshot.phraseState.countBeats, Is.EqualTo(12));
        Assert.That(snapshot.phraseState.lengthBeats, Is.EqualTo(32));
        Assert.That(snapshot.phraseState.irregular, Is.EqualTo(1));

        Assert.That(snapshot.nextPhraseState.label, Is.EqualTo("Break"));
        Assert.That(snapshot.nextPhraseState.countBeats, Is.EqualTo(8));
        Assert.That(snapshot.nextPhraseState.lengthBeats, Is.EqualTo(16));

        Assert.That(snapshot.dropState.active, Is.EqualTo(1));
        Assert.That(snapshot.dropState.countBeats, Is.EqualTo(0));
        Assert.That(snapshot.dropState.lengthBeats, Is.EqualTo(32));
        Assert.That(snapshot.dropState.remaining, Is.EqualTo(2));

        Assert.That(snapshot.fillState.active, Is.EqualTo(0));
        Assert.That(snapshot.fillState.countBeats, Is.EqualTo(16));
        Assert.That(snapshot.fillState.lengthBeats, Is.EqualTo(8));
        Assert.That(snapshot.fillState.remaining, Is.EqualTo(1));

        Assert.That(snapshot.energyState.label, Is.EqualTo("High"));
        Assert.That(snapshot.energyState.countBeats, Is.EqualTo(4));
        Assert.That(snapshot.energyState.lengthBeats, Is.EqualTo(16));

        Assert.That(snapshot.nextEnergyState.label, Is.EqualTo("Mid"));
        Assert.That(snapshot.nextEnergyState.countBeats, Is.EqualTo(20));
        Assert.That(snapshot.nextEnergyState.lengthBeats, Is.EqualTo(64));

        Assert.That(snapshot.loopState.active, Is.EqualTo(1));
        Assert.That(snapshot.loopState.set, Is.EqualTo(1));
        Assert.That(snapshot.loopState.lengthBeats, Is.EqualTo(0.5f));
        Assert.That(snapshot.loopState.lengthMs, Is.EqualTo(938));
        Assert.That(snapshot.loopState.sizeNumerator, Is.EqualTo(1));
        Assert.That(snapshot.loopState.sizeDenominator, Is.EqualTo(2));

        Assert.That(snapshot.timingGrid.beat, Is.EqualTo(5));
        Assert.That(snapshot.timingGrid.bar, Is.EqualTo(2));
        Assert.That(snapshot.timingGrid.state, Is.EqualTo("locked"));

        Assert.That(snapshot.trackId, Is.EqualTo(777001));

        Assert.That(parser.TryTakeSnapshot(out _), Is.False);
    }

    [Test]
    public void DispatchIgnoresFutureBundleTimeTagsForLiveOnAirStream() {
        var packet = new byte[512];
        var futureTimeTag = OscTimeTag.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(30));
        var bundle = new OscBundleWriter(packet, futureTimeTag);
        WriteFloat(ref bundle, "/rave/onair/bpm", 128.5f);
        WriteInt(ref bundle, "/rave/onair/beat", 64);

        using var parser = new RaveOscPacketParser();
        var dispatches = parser.Dispatch(packet.AsSpan(0, bundle.Finish()));

        Assert.That(dispatches, Is.EqualTo(2));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);
        Assert.That(snapshot.bpm, Is.EqualTo(128.5f));
        Assert.That(snapshot.beat.current, Is.EqualTo(64));
    }

    [Test]
    public void DispatchPreservesUnavailableTriStatesInsteadOfCollapsingToActiveOrRegular() {
        // RaveSystem broadcasts several fields as tri-states: 1 = yes/active, 0 = no/counting,
        // -1 = unavailable. A boolean collapse (!= 0) would read -1 as "yes"/"active now".
        var packet = new byte[1024];
        var bundle = new OscBundleWriter(packet, OscTimeTag.Immediately);
        WritePhraseState(ref bundle, "/rave/onair/phrase_state", "", -1, -1, -1);
        WriteCountdownState(ref bundle, "/rave/onair/drop_state", -1, -1, -1, -1);
        WriteCountdownState(ref bundle, "/rave/onair/fill_state", -1, -1, -1, -1);
        WriteLabeledCountdown(ref bundle, "/rave/onair/energy_state", "", -1, -1);
        WriteLoopState(ref bundle, "/rave/onair/loop_state", -1, -1, -1f, -1, -1, -1);

        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet.AsSpan(0, bundle.Finish()));

        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);
        Assert.That(snapshot.phraseState.irregular, Is.EqualTo(-1));
        Assert.That(snapshot.dropState.active, Is.EqualTo(-1));
        Assert.That(snapshot.fillState.active, Is.EqualTo(-1));
        Assert.That(snapshot.loopState.active, Is.EqualTo(-1));
        Assert.That(snapshot.loopState.set, Is.EqualTo(-1));
    }

    [Test]
    public void SnapshotDefaultsToUnavailableStatesBeforeAnyStatePacketArrives() {
        var snapshot = new RaveOnAirSnapshot();

        Assert.That(snapshot.phraseState.label, Is.Null);
        Assert.That(snapshot.phraseState.countBeats, Is.EqualTo(-1));
        Assert.That(snapshot.phraseState.lengthBeats, Is.EqualTo(-1));
        Assert.That(snapshot.phraseState.irregular, Is.EqualTo(-1));

        Assert.That(snapshot.nextPhraseState.label, Is.Null);
        Assert.That(snapshot.nextPhraseState.countBeats, Is.EqualTo(-1));
        Assert.That(snapshot.nextPhraseState.lengthBeats, Is.EqualTo(-1));

        Assert.That(snapshot.energyState.label, Is.Null);
        Assert.That(snapshot.nextEnergyState.label, Is.Null);

        Assert.That(snapshot.dropState.active, Is.EqualTo(-1));
        Assert.That(snapshot.fillState.active, Is.EqualTo(-1));

        Assert.That(snapshot.loopState.active, Is.EqualTo(-1));
        Assert.That(snapshot.loopState.set, Is.EqualTo(-1));
        Assert.That(snapshot.loopState.lengthBeats, Is.EqualTo(-1f));
        Assert.That(snapshot.loopState.lengthMs, Is.EqualTo(-1));
        Assert.That(snapshot.loopState.sizeNumerator, Is.EqualTo(-1));
        Assert.That(snapshot.loopState.sizeDenominator, Is.EqualTo(-1));

        Assert.That(snapshot.timingGrid.beat, Is.EqualTo(-1));
        Assert.That(snapshot.timingGrid.bar, Is.EqualTo(-1));
        Assert.That(snapshot.timingGrid.state, Is.Null);

        Assert.That(snapshot.trackId, Is.EqualTo(-1));
        Assert.That(snapshot.levels.low, Is.EqualTo(-1f));
    }

    [Test]
    public void DispatchRejectsWrongTypeForKnownRaveAddress() {
        var packet = new byte[256];
        var writer = new OscWriter(packet);
        writer.WriteAddress("/rave/onair/bpm");
        writer.WriteString("fast");

        var length = writer.Finish();
        using var parser = new RaveOscPacketParser();

        Assert.Throws<OscFormatException>(() => parser.Dispatch(packet.AsSpan(0, length)));
    }

    [Test]
    public void DispatchRejectsWrongTypeForLoopStateLengthBeatsSlot() {
        // loop_state is iifiii; the third argument (lengthBeats) must be a float32, not a string.
        var packet = new byte[256];
        var writer = new OscWriter(packet);
        writer.WriteAddress("/rave/onair/loop_state");
        writer.WriteInt32(1);
        writer.WriteInt32(1);
        writer.WriteString("fast");
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(0);

        var length = writer.Finish();
        using var parser = new RaveOscPacketParser();

        Assert.Throws<OscFormatException>(() => parser.Dispatch(packet.AsSpan(0, length)));
    }

    [Test]
    public void DispatchIgnoresUnrecognizedLegacyPhaseStateAddress() {
        // v2 dropped the legacy misspelled "/rave/onair/phase_state" address; only the correctly
        // spelled "/rave/onair/phrase_state" is registered now, so this must dispatch as unrecognized.
        var packet = new byte[256];
        var writer = new OscWriter(packet);
        writer.WriteAddress("/rave/onair/phase_state");
        writer.WriteString("Drop");
        writer.WriteInt32(12);
        writer.WriteInt32(32);
        writer.WriteInt32(1);

        var length = writer.Finish();
        using var parser = new RaveOscPacketParser();

        var dispatched = parser.Dispatch(packet.AsSpan(0, length));

        Assert.That(dispatched, Is.EqualTo(0));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.False);
        Assert.That(snapshot.phraseState.label, Is.Null);
    }

    private static void WriteInt(ref OscBundleWriter bundle, string address, int value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteFourInts(ref OscBundleWriter bundle, string address, int first, int second, int third, int fourth) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(first);
        writer.WriteInt32(second);
        writer.WriteInt32(third);
        writer.WriteInt32(fourth);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteFloat(ref OscBundleWriter bundle, string address, float value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteThreeFloats(ref OscBundleWriter bundle, string address, float low, float mid, float high) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(low);
        writer.WriteFloat32(mid);
        writer.WriteFloat32(high);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteString(ref OscBundleWriter bundle, string address, string value) {
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
        int irregular) {
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
        int lengthBeats) {
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
        int sizeDenominator) {
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
    private static void WriteTimingGrid(ref OscBundleWriter bundle, string address, int beat, int bar, string state) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(beat);
        writer.WriteInt32(bar);
        writer.WriteString(state);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteCountdownState(ref OscBundleWriter bundle, string address, int active, int countBeats, int lengthBeats, int remaining) {
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

}
