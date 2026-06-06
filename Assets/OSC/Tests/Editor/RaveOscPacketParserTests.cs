#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

namespace RaveSystem.Osc.Tests {

public sealed class RaveOscPacketParserTests {
    [Test]
    public void DispatchReadsCurrentRaveOnAirLaneBundlesIntoOscShapedSnapshot() {
        var continuousPacket = new byte[2048];
        var continuous = new OscBundleWriter(continuousPacket, OscTimeTag.Immediately);
        WriteFloat(ref continuous, "/rave/onair/bpm", 128.5f);
        WriteInt(ref continuous, "/rave/onair/beat", 64);
        WriteInt(ref continuous, "/rave/onair/bar", 16);
        WriteInt(ref continuous, "/rave/onair/next_bar_ms", 777);
        WriteInt(ref continuous, "/rave/onair/beat_in_bar", 3);
        WriteFourInts(ref continuous, "/rave/onair/beats_count_ms", 100, 200, 300, 400);
        WriteFourInts(ref continuous, "/rave/onair/on_beats", 0, 0, 1, 0);
        WriteInt(ref continuous, "/rave/onair/beat_avg_ms", 468);
        WriteFloat(ref continuous, "/rave/onair/beat_pulse", 0.625f);
        WriteThreeFloats(ref continuous, "/rave/onair/levels", 0.25f, 0.5f, 0.75f);

        using var parser = new RaveOscPacketParser();
        var continuousDispatches = parser.Dispatch(continuousPacket.AsSpan(0, continuous.Finish()));

        Assert.That(continuousDispatches, Is.EqualTo(10));
        Assert.That(parser.TryTakeSnapshot(out var continuousSnapshot), Is.True);
        Assert.That(continuousSnapshot.bpm, Is.EqualTo(128.5f));
        Assert.That(continuousSnapshot.beat.current, Is.EqualTo(64));
        Assert.That(continuousSnapshot.beat.total, Is.EqualTo(-1));
        Assert.That(continuousSnapshot.bar.current, Is.EqualTo(16));
        Assert.That(continuousSnapshot.bar.nextMs, Is.EqualTo(777));
        Assert.That(continuousSnapshot.beatInBar, Is.EqualTo(3));
        Assert.That(continuousSnapshot.beatsCountMs, Is.EqualTo(new[] { 100, 200, 300, 400 }));
        Assert.That(continuousSnapshot.onBeats, Is.EqualTo(new[] { false, false, true, false }));
        Assert.That(continuousSnapshot.beatAverageMs, Is.EqualTo(468));
        Assert.That(continuousSnapshot.beatPulse, Is.EqualTo(0.625f));
        Assert.That(continuousSnapshot.levels.low, Is.EqualTo(0.25f));
        Assert.That(continuousSnapshot.levels.mid, Is.EqualTo(0.5f));
        Assert.That(continuousSnapshot.levels.high, Is.EqualTo(0.75f));

        var discretePacket = new byte[1024];
        var discrete = new OscBundleWriter(discretePacket, OscTimeTag.Immediately);
        WriteString(ref discrete, "/rave/onair/players_live", "4,2");
        WriteString(ref discrete, "/rave/onair/track", "Artist - Track");
        WriteInt(ref discrete, "/rave/onair/total_beats", 384);
        WriteNamedState(ref discrete, "/rave/onair/phase_state", "Drop", "Break", 1, 12, 32, 8);
        WriteCountdownState(ref discrete, "/rave/onair/drop_state", 1, 0, 32, 2);
        WriteCountdownState(ref discrete, "/rave/onair/fill_state", 0, 16, 8, 1);
        WriteNamedState(ref discrete, "/rave/onair/energy_state", "High", "Mid", 1, 4, 16, 2);

        var discreteDispatches = parser.Dispatch(discretePacket.AsSpan(0, discrete.Finish()));

        Assert.That(discreteDispatches, Is.EqualTo(7));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);
        Assert.That(snapshot.playersLive, Is.EqualTo("4,2"));
        Assert.That(snapshot.track, Is.EqualTo("Artist - Track"));
        Assert.That(snapshot.beat.current, Is.EqualTo(64));
        Assert.That(snapshot.beat.total, Is.EqualTo(384));
        Assert.That(snapshot.bar.current, Is.EqualTo(16));
        Assert.That(snapshot.bar.nextMs, Is.EqualTo(777));
        Assert.That(snapshot.phaseState.current, Is.EqualTo("Drop"));
        Assert.That(snapshot.phaseState.next, Is.EqualTo("Break"));
        Assert.That(snapshot.phaseState.active, Is.EqualTo(1));
        Assert.That(snapshot.phaseState.countBeats, Is.EqualTo(12));
        Assert.That(snapshot.phaseState.lengthBeats, Is.EqualTo(32));
        Assert.That(snapshot.phaseState.remaining, Is.EqualTo(8));
        Assert.That(snapshot.dropState.active, Is.EqualTo(1));
        Assert.That(snapshot.dropState.countBeats, Is.EqualTo(0));
        Assert.That(snapshot.dropState.lengthBeats, Is.EqualTo(32));
        Assert.That(snapshot.dropState.remaining, Is.EqualTo(2));
        Assert.That(snapshot.fillState.active, Is.EqualTo(0));
        Assert.That(snapshot.fillState.countBeats, Is.EqualTo(16));
        Assert.That(snapshot.fillState.lengthBeats, Is.EqualTo(8));
        Assert.That(snapshot.fillState.remaining, Is.EqualTo(1));
        Assert.That(snapshot.energyState.current, Is.EqualTo("High"));
        Assert.That(snapshot.energyState.next, Is.EqualTo("Mid"));
        Assert.That(snapshot.energyState.active, Is.EqualTo(1));
        Assert.That(snapshot.energyState.countBeats, Is.EqualTo(4));
        Assert.That(snapshot.energyState.lengthBeats, Is.EqualTo(16));
        Assert.That(snapshot.energyState.remaining, Is.EqualTo(2));
        Assert.That(parser.TryTakeSnapshot(out _), Is.False);
    }

    [Test]
    public void DispatchPreservesUnavailableTriStateInsteadOfCollapsingToActive() {
        // RaveSystem broadcasts active as a tri-state: 1 = active now, 0 = counting to the next
        // occurrence, -1 = unavailable. A boolean collapse (!= 0) would read -1 as "active now".
        var packet = new byte[1024];
        var bundle = new OscBundleWriter(packet, OscTimeTag.Immediately);
        WriteNamedState(ref bundle, "/rave/onair/phase_state", "", "", -1, -1, -1, -1);
        WriteCountdownState(ref bundle, "/rave/onair/drop_state", -1, -1, -1, -1);
        WriteCountdownState(ref bundle, "/rave/onair/fill_state", -1, -1, -1, -1);
        WriteNamedState(ref bundle, "/rave/onair/energy_state", "", "", -1, -1, -1, -1);

        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet.AsSpan(0, bundle.Finish()));

        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);
        Assert.That(snapshot.phaseState.active, Is.EqualTo(-1));
        Assert.That(snapshot.dropState.active, Is.EqualTo(-1));
        Assert.That(snapshot.fillState.active, Is.EqualTo(-1));
        Assert.That(snapshot.energyState.active, Is.EqualTo(-1));
    }

    [Test]
    public void SnapshotDefaultsToUnavailableStatesBeforeAnyStatePacketArrives() {
        var snapshot = new RaveOnAirSnapshot();

        Assert.That(snapshot.phaseState.active, Is.EqualTo(-1));
        Assert.That(snapshot.dropState.active, Is.EqualTo(-1));
        Assert.That(snapshot.fillState.active, Is.EqualTo(-1));
        Assert.That(snapshot.energyState.active, Is.EqualTo(-1));
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

    private static void WriteNamedState(
        ref OscBundleWriter bundle,
        string address,
        string current,
        string next,
        int active,
        int countBeats,
        int lengthBeats,
        int remaining) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(current);
        writer.WriteString(next);
        writer.WriteInt32(active);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        writer.WriteInt32(remaining);
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
