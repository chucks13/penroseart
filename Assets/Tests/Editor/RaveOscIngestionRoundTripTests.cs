#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

/// <summary>
/// Standing guardrail for the full RaveSystem on-air ingestion path: a packet that writes EVERY registered
/// <c>/rave/onair/*</c> address is dispatched through <see cref="RaveOscPacketParser"/>, the taken snapshot is
/// fed into a <see cref="BeatManager"/>, and the canonical Data Surface is asserted. Adding a wire
/// field without carrying it to its concept doorway therefore fails at the production ingress seam.
/// </summary>
public sealed class RaveOscIngestionRoundTripTests
{
    /// <summary>Verifies every on-air OSC address reaches its canonical BeatManager doorway.</summary>
    [Test]
    public void EveryOnAirAddressFlowsThroughToTheDataSurface()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket());

        Assert.That(beatManager.IsSynced, Is.True);
        Assert.That(beatManager.Clock.Bpm, Is.EqualTo(128.5f).Within(0.0001f));
        Assert.That(beatManager.Position.Beat, Is.EqualTo(64));
        Assert.That(beatManager.Position.TotalBeats, Is.EqualTo(384));
        Assert.That(beatManager.Position.Bar, Is.EqualTo(16));
        Assert.That(beatManager.Position.BeatInBar, Is.EqualTo(3));
        Assert.That(beatManager.Track.TrackTitle, Is.EqualTo("Artist - Track"));
        Assert.That(beatManager.Track.PlayersLive, Is.EqualTo(new[] { 4, 2 }));

        Assert.That(beatManager.Drop.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Drop.RemainingOnTrack, Is.EqualTo(2));
        Assert.That(beatManager.Fill.Span.Current, Is.Null);
        Assert.That(beatManager.Fill.NextInBeats, Is.EqualTo(16));

        Assert.That(beatManager.Phrase.Span.Current?.Name, Is.EqualTo("Drop"));
        Assert.That(beatManager.Phrase.Span.Current?.Irregular, Is.True);
        Assert.That(beatManager.Phrase.Span.Current?.LengthBeats, Is.EqualTo(32));
        Assert.That(beatManager.Phrase.NextName, Is.EqualTo("Break"));
        Assert.That(beatManager.Phrase.NextInBeats, Is.EqualTo(8));
        Assert.That(beatManager.Phrase.NextLengthBeats, Is.EqualTo(16));

        Assert.That(beatManager.Energy.Run.Current?.Level, Is.EqualTo(Energy.High));
        Assert.That(beatManager.Energy.NextLevel, Is.EqualTo(Energy.Mid));
        Assert.That(beatManager.Energy.NextRunLengthBeats, Is.EqualTo(64));

        Assert.That(beatManager.Loop.Rolling, Is.True);
        Assert.That(beatManager.Loop.RegionSet, Is.True);
        Assert.That(beatManager.Loop.LengthBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Loop.LengthMs, Is.EqualTo(938));
        Assert.That(beatManager.Loop.NominalSizeBeats, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Track.TrackId, Is.EqualTo(777001));

        Assert.That(beatManager.Levels?.Normalized.Low, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(beatManager.Levels?.Normalized.Mid, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Levels?.Normalized.High, Is.EqualTo(0.75f).Within(0.0001f));
    }

    /// <summary>Verifies an omitted current Phrase lane does not erase populated sibling doorways.</summary>
    [Test]
    public void PhraseStateOmittedLeavesPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/phrase_state"));

        Assert.That(beatManager.Phrase.Span.Current, Is.Null);
        Assert.That(beatManager.Phrase.NextName, Is.Not.Null);
        Assert.That(beatManager.Energy.Run.Current, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.Not.Null);
        Assert.That(beatManager.Track.TrackId, Is.Not.Null);
    }

    /// <summary>Verifies an omitted next Phrase lane nulls only the next announcement.</summary>
    [Test]
    public void NextPhraseStateOmittedLeavesNextPhraseNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_phrase_state"));

        Assert.That(beatManager.Phrase.NextName, Is.Null);
        Assert.That(beatManager.Phrase.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Energy.Run.Current, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.Not.Null);
        Assert.That(beatManager.Track.TrackId, Is.Not.Null);
    }

    /// <summary>Verifies an omitted current Energy lane does not erase populated sibling doorways.</summary>
    [Test]
    public void EnergyStateOmittedLeavesEnergyNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/energy_state"));

        Assert.That(beatManager.Energy.Run.Current, Is.Null);
        Assert.That(beatManager.Phrase.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Phrase.NextName, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.Not.Null);
        Assert.That(beatManager.Track.TrackId, Is.Not.Null);
    }

    /// <summary>Verifies an omitted next Energy lane preserves the current run and nulls only its successor.</summary>
    [Test]
    public void NextEnergyStateOmittedLeavesEnergyNextNullButEnergyAndSiblingsPopulated()
    {
        // next_energy_state has no dedicated query of its own; it folds into Energy.next, so omitting
        // it should null out only that field, not the whole Energy result.
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/next_energy_state"));

        Assert.That(beatManager.Energy.Run.Current?.Level, Is.EqualTo(Energy.High));
        Assert.That(beatManager.Energy.NextLevel, Is.Null);
        Assert.That(beatManager.Phrase.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Phrase.NextName, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.Not.Null);
        Assert.That(beatManager.Track.TrackId, Is.Not.Null);
    }

    /// <summary>Verifies an omitted Loop lane serves unavailable Loop facts without disturbing siblings.</summary>
    [Test]
    public void LoopStateOmittedLeavesLoopNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/loop_state"));

        Assert.That(beatManager.Loop.Rolling, Is.Null);
        Assert.That(beatManager.Phrase.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Phrase.NextName, Is.Not.Null);
        Assert.That(beatManager.Energy.Run.Current, Is.Not.Null);
        Assert.That(beatManager.Track.TrackId, Is.Not.Null);
    }

    /// <summary>Verifies an omitted track id translates to null without disturbing sibling doorways.</summary>
    [Test]
    public void TrackIdOmittedLeavesTrackIdNullButSiblingsPopulated()
    {
        var beatManager = BuildLiveBeatManagerFromFullPacket(BuildFullOnAirPacket("/rave/onair/track_id"));

        Assert.That(beatManager.Track.TrackId, Is.Null);
        Assert.That(beatManager.Phrase.Span.Current, Is.Not.Null);
        Assert.That(beatManager.Phrase.NextName, Is.Not.Null);
        Assert.That(beatManager.Energy.Run.Current, Is.Not.Null);
        Assert.That(beatManager.Loop.Rolling, Is.Not.Null);
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
