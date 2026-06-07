// Copyright © 2026 Hunter Luisi. All rights reserved.
// Tests for PenroseArt's Unity-adapted RaveSystem.Osc code.

#nullable enable
using System;
using System.Text;
using NUnit.Framework;

namespace RaveSystem.Osc.Tests {

[TestFixture]
[Category("OSC")]
public sealed class OscPacketTests {
    [Test]
    public void WriterAndReaderRoundTripIntFloatAndStringMessage() {
        var buffer = new byte[256];
        var writer = new OscWriter(buffer);

        writer.WriteAddress("/rave/onair/sample");
        writer.WriteInt32(17);
        writer.WriteFloat32(128.5f);
        writer.WriteString("breakdown");

        var length = writer.Finish();
        Assert.That(OscPacket.Classify(buffer.AsSpan(0, length)), Is.EqualTo(OscPacketKind.Message));

        var reader = new OscReader(buffer.AsSpan(0, length));
        Assert.That(ReadAscii(reader.ReadAddress()), Is.EqualTo("/rave/onair/sample"));

        Assert.That(reader.MoveNext(), Is.True);
        Assert.That(reader.CurrentTag, Is.EqualTo(OscToken.I32));
        Assert.That(reader.ReadInt32(), Is.EqualTo(17));

        Assert.That(reader.MoveNext(), Is.True);
        Assert.That(reader.CurrentTag, Is.EqualTo(OscToken.F32));
        Assert.That(reader.ReadFloat32(), Is.EqualTo(128.5f).Within(0.001f));

        Assert.That(reader.MoveNext(), Is.True);
        Assert.That(reader.CurrentTag, Is.EqualTo(OscToken.Str));
        Assert.That(reader.ReadStringAlloc(), Is.EqualTo("breakdown"));

        Assert.That(reader.MoveNext(), Is.False);
    }

    [Test]
    public void BundleReaderReadsRaveStyleOnAirMessagesInOrder() {
        var buffer = new byte[512];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);

        WriteFloatElement(ref bundle, "/rave/onair/bpm", 128.5f);
        WriteIntElement(ref bundle, "/rave/onair/beat", 17);
        WriteStringElement(ref bundle, "/rave/onair/phase", "drop");

        var length = bundle.Finish();
        Assert.That(OscPacket.Classify(buffer.AsSpan(0, length)), Is.EqualTo(OscPacketKind.Bundle));

        var reader = new OscBundleReader(buffer.AsSpan(0, length));
        AssertFloatElement(reader.ReadNextElement(), "/rave/onair/bpm", 128.5f);
        AssertIntElement(reader.ReadNextElement(), "/rave/onair/beat", 17);
        AssertStringElement(reader.ReadNextElement(), "/rave/onair/phase", "drop");
        Assert.That(reader.HasMoreElements, Is.False);
    }

    [Test]
    public void PacketClassifyRejectsMalformedPackets() {
        Assert.Throws<OscFormatException>(() => OscPacket.Classify(Array.Empty<byte>()));
        Assert.Throws<OscFormatException>(() => OscPacket.Classify(new byte[] { (byte)'x', 0, 0, 0 }));
        Assert.Throws<OscFormatException>(() => OscPacket.Classify(Encoding.ASCII.GetBytes("#bad\0\0\0\0")));
    }

    [Test]
    public void AddressPatternSupportsOsc11DoubleSlashTraversal() {
        Assert.That(OscAddressPattern.Matches("//spherical", "/position/spherical"), Is.True);
        Assert.That(OscAddressPattern.Matches("//spherical", "/rig/deck/1/spherical"), Is.True);
        Assert.That(OscAddressPattern.Matches("/position//frequency", "/position/cartesian/player/1/frequency"), Is.True);
        Assert.That(OscAddressPattern.Matches("/position//frequency", "/rotation/frequency"), Is.False);
    }

    [Test]
    public void SlipFramingRoundTripsEscapedOsc11StreamPacket() {
        var packet = new byte[] { 0x01, OscSlipFraming.End, OscSlipFraming.Esc, 0x02 };
        var frame = new byte[8];
        var decoded = new byte[4];

        var frameLength = OscSlipFraming.EncodePacket(packet, frame);
        Assert.That(frame.AsSpan(0, frameLength).ToArray(), Is.EqualTo(new byte[] {
            OscSlipFraming.End,
            0x01,
            OscSlipFraming.Esc,
            OscSlipFraming.EscEnd,
            OscSlipFraming.Esc,
            OscSlipFraming.EscEsc,
            0x02,
            OscSlipFraming.End,
        }));

        var decodedLength = OscSlipFraming.DecodePacket(frame.AsSpan(0, frameLength), decoded);
        Assert.That(decoded.AsSpan(0, decodedLength).ToArray(), Is.EqualTo(packet));
    }

    [Test]
    public void SlipFramingReportsRequiredLengthWhenDestinationIsTooSmall() {
        var destination = new byte[3];

        var success = OscSlipFraming.TryEncodePacket(new byte[] { OscSlipFraming.End }, destination, out var written, out var requiredLength);

        Assert.That(success, Is.False);
        Assert.That(written, Is.EqualTo(0));
        Assert.That(requiredLength, Is.EqualTo(4));
    }

    private static void WriteIntElement(ref OscBundleWriter bundle, string address, int value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteFloatElement(ref OscBundleWriter bundle, string address, float value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(value);
        bundle.EndElement(writer.Finish());
    }

    private static void WriteStringElement(ref OscBundleWriter bundle, string address, string value) {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(value);
        bundle.EndElement(writer.Finish());
    }

    private static void AssertIntElement(ReadOnlySpan<byte> packet, string address, int value) {
        var reader = new OscReader(packet);
        Assert.That(ReadAscii(reader.ReadAddress()), Is.EqualTo(address));
        Assert.That(reader.MoveNext(), Is.True);
        Assert.That(reader.CurrentTag, Is.EqualTo(OscToken.I32));
        Assert.That(reader.ReadInt32(), Is.EqualTo(value));
        Assert.That(reader.MoveNext(), Is.False);
    }

    private static void AssertFloatElement(ReadOnlySpan<byte> packet, string address, float value) {
        var reader = new OscReader(packet);
        Assert.That(ReadAscii(reader.ReadAddress()), Is.EqualTo(address));
        Assert.That(reader.MoveNext(), Is.True);
        Assert.That(reader.CurrentTag, Is.EqualTo(OscToken.F32));
        Assert.That(reader.ReadFloat32(), Is.EqualTo(value).Within(0.001f));
        Assert.That(reader.MoveNext(), Is.False);
    }

    private static void AssertStringElement(ReadOnlySpan<byte> packet, string address, string value) {
        var reader = new OscReader(packet);
        Assert.That(ReadAscii(reader.ReadAddress()), Is.EqualTo(address));
        Assert.That(reader.MoveNext(), Is.True);
        Assert.That(reader.CurrentTag, Is.EqualTo(OscToken.Str));
        Assert.That(reader.ReadStringAlloc(), Is.EqualTo(value));
        Assert.That(reader.MoveNext(), Is.False);
    }

    private static string ReadAscii(ReadOnlySpan<byte> bytes) => Encoding.ASCII.GetString(bytes.ToArray());
}

}
