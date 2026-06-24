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
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/phrase_state"));

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

        var energy = beatManager.Energy;
        Assert.That(energy, Is.Not.Null);
        Assert.That(energy!.Value.level, Is.EqualTo(EnergyLevel.High));
        Assert.That(energy.Value.next, Is.EqualTo(EnergyLevel.Mid));

        var phrase = beatManager.Phrase;
        Assert.That(phrase, Is.Not.Null);
        Assert.That(phrase!.Value.label, Is.EqualTo("Drop"));
        Assert.That(phrase.Value.next, Is.EqualTo("Break"));
        Assert.That(phrase.Value.inPhrase, Is.True);

        var levels = beatManager.Levels;
        Assert.That(levels, Is.Not.Null);
        Assert.That(levels!.Value.low, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(levels.Value.mid, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(levels.Value.high, Is.EqualTo(0.75f).Within(0.0001f));
    }

    [Test]
    public void NewAndLegacyPhraseStateAddressesBothPopulateThePhraseQuery()
    {
        var fromNew = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/phrase_state"));
        Assert.That(fromNew.Phrase, Is.Not.Null);
        Assert.That(fromNew.Phrase!.Value.label, Is.EqualTo("Drop"));

        // TRANSITIONAL: the parser also accepts the legacy misspelled address. Delete this case (and the
        // const + registration in RaveOscPacketParser) once RaveSystem ships /rave/onair/phrase_state and
        // pre-rename recordings are retired.
        var fromLegacy = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/phase_state"));
        Assert.That(fromLegacy.Phrase, Is.Not.Null);
        Assert.That(fromLegacy.Phrase!.Value.label, Is.EqualTo("Drop"));
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
    /// Builds one bundle carrying every registered on-air address. <paramref name="phraseStateAddress"/>
    /// selects which spelling of the phrase-state address to write so both the new and legacy wire forms
    /// can be exercised.
    /// </summary>
    private static byte[] BuildFullOnAirPacket(string phraseStateAddress)
    {
        var buffer = new byte[2048];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);
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
        WriteNamedState(ref bundle, phraseStateAddress, "Drop", "Break", 1, 12, 32, 8);
        WriteCountdownState(ref bundle, "/rave/onair/drop_state", 1, 0, 32, 2);
        WriteCountdownState(ref bundle, "/rave/onair/fill_state", 0, 16, 8, 1);
        WriteNamedState(ref bundle, "/rave/onair/energy_state", "High", "Mid", 1, 4, 16, 2);
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

    private static void WriteNamedState(
        ref OscBundleWriter bundle,
        string address,
        string current,
        string next,
        int active,
        int countBeats,
        int lengthBeats,
        int remaining)
    {
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
