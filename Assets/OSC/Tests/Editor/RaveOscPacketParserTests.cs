#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

namespace RaveSystem.Osc.Tests {

public sealed class RaveOscPacketParserTests {
    [Test]
    public void DispatchReadsRaveOnAirBundlesIntoOscShapedSnapshot() {
        var packet = new byte[2048];
        var core = new OscBundleWriter(packet, OscTimeTag.Immediately);
        WriteString(ref core, "/rave/onair/players_live", "4,2");
        WriteString(ref core, "/rave/onair/track", "Artist - Track");
        WriteFloat(ref core, "/rave/onair/bpm", 128.5f);
        WriteTwoInts(ref core, "/rave/onair/beat", 64, 384);
        WriteTwoInts(ref core, "/rave/onair/bar", 16, 777);
        WriteInt(ref core, "/rave/onair/beat_in_bar", 3);
        WriteFourInts(ref core, "/rave/onair/beats_count_ms", 100, 200, 300, 400);
        WriteFourInts(ref core, "/rave/onair/on_beats", 0, 0, 1, 0);
        WriteInt(ref core, "/rave/onair/beat_avg_ms", 468);
        WriteFloat(ref core, "/rave/onair/beat_pulse", 0.625f);
        WriteThreeFloats(ref core, "/rave/onair/levels", 0.25f, 0.5f, 0.75f);

        using var parser = new RaveOscPacketParser();
        var coreDispatches = parser.Dispatch(packet.AsSpan(0, core.Finish()));

        Assert.That(coreDispatches, Is.EqualTo(11));
        Assert.That(parser.TryTakeSnapshot(out var coreSnapshot), Is.True);
        Assert.That(coreSnapshot.playersLive, Is.EqualTo("4,2"));
        Assert.That(coreSnapshot.track, Is.EqualTo("Artist - Track"));
        Assert.That(coreSnapshot.bpm, Is.EqualTo(128.5f));
        Assert.That(coreSnapshot.beat.current, Is.EqualTo(64));
        Assert.That(coreSnapshot.beat.total, Is.EqualTo(384));
        Assert.That(coreSnapshot.bar.current, Is.EqualTo(16));
        Assert.That(coreSnapshot.bar.nextMs, Is.EqualTo(777));
        Assert.That(coreSnapshot.beatInBar, Is.EqualTo(3));
        Assert.That(coreSnapshot.beatsCountMs, Is.EqualTo(new[] { 100, 200, 300, 400 }));
        Assert.That(coreSnapshot.onBeats, Is.EqualTo(new[] { false, false, true, false }));
        Assert.That(coreSnapshot.beatAverageMs, Is.EqualTo(468));
        Assert.That(coreSnapshot.beatPulse, Is.EqualTo(0.625f));
        Assert.That(coreSnapshot.levels.low, Is.EqualTo(0.25f));
        Assert.That(coreSnapshot.levels.mid, Is.EqualTo(0.5f));
        Assert.That(coreSnapshot.levels.high, Is.EqualTo(0.75f));

        var phrasePacket = new byte[1024];
        var phrase = new OscBundleWriter(phrasePacket, OscTimeTag.Immediately);
        WriteNamedState(ref phrase, "/rave/onair/phase_state", "Drop", "Break", 1, 12, 32, 8);
        WriteCountdownState(ref phrase, "/rave/onair/drop_state", 1, 0, 32, 2);
        WriteCountdownState(ref phrase, "/rave/onair/fill_state", 0, 16, 8, 1);
        WriteNamedState(ref phrase, "/rave/onair/energy_state", "High", "Mid", 1, 4, 16, 2);

        var phraseDispatches = parser.Dispatch(phrasePacket.AsSpan(0, phrase.Finish()));

        Assert.That(phraseDispatches, Is.EqualTo(4));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);
        Assert.That(snapshot.phaseState.current, Is.EqualTo("Drop"));
        Assert.That(snapshot.phaseState.next, Is.EqualTo("Break"));
        Assert.That(snapshot.phaseState.active, Is.True);
        Assert.That(snapshot.phaseState.countBeats, Is.EqualTo(12));
        Assert.That(snapshot.phaseState.lengthBeats, Is.EqualTo(32));
        Assert.That(snapshot.phaseState.remaining, Is.EqualTo(8));
        Assert.That(snapshot.dropState.active, Is.True);
        Assert.That(snapshot.dropState.countBeats, Is.EqualTo(0));
        Assert.That(snapshot.dropState.lengthBeats, Is.EqualTo(32));
        Assert.That(snapshot.dropState.remaining, Is.EqualTo(2));
        Assert.That(snapshot.fillState.active, Is.False);
        Assert.That(snapshot.fillState.countBeats, Is.EqualTo(16));
        Assert.That(snapshot.fillState.lengthBeats, Is.EqualTo(8));
        Assert.That(snapshot.fillState.remaining, Is.EqualTo(1));
        Assert.That(snapshot.energyState.current, Is.EqualTo("High"));
        Assert.That(snapshot.energyState.next, Is.EqualTo("Mid"));
        Assert.That(snapshot.energyState.active, Is.True);
        Assert.That(snapshot.energyState.countBeats, Is.EqualTo(4));
        Assert.That(snapshot.energyState.lengthBeats, Is.EqualTo(16));
        Assert.That(snapshot.energyState.remaining, Is.EqualTo(2));
        Assert.That(parser.TryTakeSnapshot(out _), Is.False);
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

    private static void WriteTwoInts(ref OscBundleWriter bundle, string address, int first, int second) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(first);
        writer.WriteInt32(second);
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
