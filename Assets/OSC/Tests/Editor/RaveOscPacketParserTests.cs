// Copyright © 2026 Hunter Luisi. All rights reserved.

#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

namespace RaveSystem.Osc.Tests {

public sealed class RaveOscPacketParserTests {
    [Test]
    public void DispatchReadsRaveOnAirBundleIntoSnapshot() {
        var packet = new byte[1024];
        var bundle = new OscBundleWriter(packet, OscTimeTag.Immediately);
        WriteFloat(ref bundle, "/rave/onair/bpm", 128.5f);
        WriteInt(ref bundle, "/rave/onair/beat", 64);
        WriteInt(ref bundle, "/rave/onair/bar", 16);
        WriteInt(ref bundle, "/rave/onair/beat_in_bar", 3);
        WriteInt(ref bundle, "/rave/onair/next_beat_ms", 123);
        WriteInt(ref bundle, "/rave/onair/on_beat", 1);
        WriteFloat(ref bundle, "/rave/onair/low", 0.25f);
        WriteFloat(ref bundle, "/rave/onair/mid", 0.5f);
        WriteFloat(ref bundle, "/rave/onair/high", 0.75f);
        WriteInt(ref bundle, "/rave/onair/drop_in", 1);
        WriteString(ref bundle, "/rave/onair/phase", "Drop");

        using var parser = new RaveOscPacketParser();
        var dispatched = parser.Dispatch(packet.AsSpan(0, bundle.Finish()));

        Assert.That(dispatched, Is.EqualTo(11));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);
        Assert.That(snapshot.Bpm, Is.EqualTo(128.5f));
        Assert.That(snapshot.Beat, Is.EqualTo(64));
        Assert.That(snapshot.Bar, Is.EqualTo(16));
        Assert.That(snapshot.BeatInBar, Is.EqualTo(3));
        Assert.That(snapshot.NextBeatMs, Is.EqualTo(123));
        Assert.That(snapshot.OnBeat, Is.True);
        Assert.That(snapshot.Low, Is.EqualTo(0.25f));
        Assert.That(snapshot.Mid, Is.EqualTo(0.5f));
        Assert.That(snapshot.High, Is.EqualTo(0.75f));
        Assert.That(snapshot.DropIn, Is.True);
        Assert.That(snapshot.Phase, Is.EqualTo("Drop"));
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

    private static void WriteFloat(ref OscBundleWriter bundle, string address, float value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteString(ref OscBundleWriter bundle, string address, string value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(value);
        bundle.EndElement(writer.Finish());
    }
}

}
