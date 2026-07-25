#nullable enable

using System;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

/// <summary>Integration tests at BeatManager's OSC snapshot boundary, including per-player values.</summary>
public sealed class BeatManagerRaveOscIntegrationTests
{
    /// <summary>Verifies BeatManager derives the Off Beat lanes and pulse from live On Beat countdowns.</summary>
    [Test]
    public void UpdateDerivesOffbeatsFromLiveBeatCountdowns()
    {
        var beatManager = BeatClockFixture.CreateSeeded(120f, 0.25f);
        beatManager.Update(0.25f);

        Assert.That(beatManager.Timing.BeatInBar, Is.EqualTo(1));
        Assert.That(beatManager.Offbeats.OffBeatMs(1), Is.Zero);
        Assert.That(beatManager.Offbeats.OffBeat(1), Is.True);
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(1f).Within(0.001f));
    }

    /// <summary>Verifies frame capture preserves the live wire values supplied at ingress.</summary>
    [Test]
    public void UpdateDoesNotOverwriteLiveWireValues()
    {
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(new RaveWireSnapshot
        {
            bpm = 128f,
            beatInBar = 3,
            beatPulse = 0.25f,
            beatAverageMs = 469,
        });
        beatManager.Update(0f);

        Assert.That(beatManager.Timing.Bpm, Is.EqualTo(128f));
        Assert.That(beatManager.Timing.BeatInBar, Is.EqualTo(3));
        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0.25f));
    }

    /// <summary>Verifies ingress owns the mutable wire arrays before the caller can change them.</summary>
    [Test]
    public void SnapshotIngressOwnsADeepCopy()
    {
        var snapshot = new RaveWireSnapshot
        {
            beatInBar = 1,
            beatsCountMs = new[] { 0, 500, 1000, 1500 },
            onBeats = new[] { true, false, false, false },
        };
        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        snapshot.beatsCountMs[0] = 999;
        snapshot.onBeats[0] = false;
        beatManager.Update(0f);

        Assert.That(beatManager.Beats.OnBeatMs(1), Is.Zero);
        Assert.That(beatManager.Beats.OnBeat(1), Is.True);
    }

    /// <summary>Verifies the OSC adapter's broadcast-liveness grace window at both sides of its threshold.</summary>
    [Test]
    public void BroadcastLivenessUsesTheDocumentedGraceWindow()
    {
        const float grace = 15f / 60f;
        Assert.That(RaveOscReceiver.IsBroadcastingAt(false, 0f), Is.False);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(true, grace - 0.001f), Is.True);
        Assert.That(RaveOscReceiver.IsBroadcastingAt(true, grace + 0.001f), Is.False);
    }

    /// <summary>Verifies per-player OSC packets round-trip through the parser into BeatManager's public surface.</summary>
    [Test]
    public void PlayerValuesFlowThroughToTheDataSurface()
    {
        var beatManager = BuildLiveBeatManager(includeOtherLiveCases: true);

        Assert.That(beatManager.Players.Count, Is.EqualTo(6));
        var player = beatManager.Players[1];
        Assert.That(player.PlayerNumber, Is.EqualTo(2));
        Assert.That(player.Bpm, Is.EqualTo(127.5f).Within(0.0001f));
        Assert.That(player.Beat, Is.EqualTo(129));
        Assert.That(player.Bar, Is.EqualTo(33));
        Assert.That(player.BeatInBar, Is.EqualTo(1));
        Assert.That(player.BeatPulse, Is.EqualTo(0.75f).Within(0.0001f));

        Assert.That(player.Playing, Is.True);
        Assert.That(player.Cued, Is.False);
        Assert.That(player.OnAir, Is.True);
        Assert.That(player.Master, Is.Null);
        Assert.That(player.Synced, Is.True);
        Assert.That(player.Live, Is.True);

        Assert.That(player.Loop.Rolling, Is.True);
        Assert.That(player.Loop.RegionSet, Is.True);
        Assert.That(player.Loop.LengthBeats, Is.EqualTo(8f));
        Assert.That(player.Loop.LengthMilliseconds, Is.EqualTo(3750));
        Assert.That(player.Loop.SizeNumerator, Is.EqualTo(8));
        Assert.That(player.Loop.SizeDenominator, Is.EqualTo(1));
        Assert.That(player.GridState, Is.EqualTo(GridState.Coasting));
        Assert.That(player.GridBeat, Is.EqualTo(13));
        Assert.That(player.GridBar, Is.EqualTo(4));

        Assert.That(player.Structure.Generation, Is.EqualTo(7));
        Assert.That(player.Structure.TrackId, Is.EqualTo("328123"));
        Assert.That(player.Structure.Source, Is.EqualTo(StructureSource.Fused));
        Assert.That(player.Structure.TotalBeats, Is.EqualTo(256));
        Assert.That(player.Structure.PhraseCount, Is.EqualTo(2));
        Assert.That(player.Structure.Phrases.Count, Is.EqualTo(2));

        var firstPhrase = player.Structure.Phrases[0];
        Assert.That(firstPhrase.StartBeat, Is.EqualTo(1));
        Assert.That(firstPhrase.EndBeat, Is.EqualTo(128));
        Assert.That(firstPhrase.Type, Is.EqualTo(PhraseType.Drop));
        Assert.That(firstPhrase.Variant, Is.EqualTo(1));
        Assert.That(firstPhrase.FillStartBeat, Is.EqualTo(120));
        Assert.That(firstPhrase.DropLandingBeat, Is.Null);

        var secondPhrase = player.Structure.Phrases[1];
        Assert.That(secondPhrase.StartBeat, Is.EqualTo(129));
        Assert.That(secondPhrase.EndBeat, Is.EqualTo(256));
        Assert.That(secondPhrase.Type, Is.EqualTo(PhraseType.Unknown));
        Assert.That(secondPhrase.Variant, Is.Zero);
        Assert.That(secondPhrase.FillStartBeat, Is.Null);
        Assert.That(secondPhrase.DropLandingBeat, Is.EqualTo(129));

        Assert.That(player.Cursor.Generation, Is.EqualTo(7));
        Assert.That(player.Cursor.CurrentPhrase, Is.EqualTo(2));
        Assert.That(player.Cursor.BeatInPhrase, Is.EqualTo(8));
        Assert.That(player.Cursor.BeatsToNextPhrase, Is.EqualTo(16));

        Assert.That(beatManager.Players[2].Bpm, Is.Null);
        Assert.That(beatManager.Players[2].Playing, Is.True);
        Assert.That(beatManager.Players[2].Cued, Is.Null);
        Assert.That(beatManager.Players[2].OnAir, Is.False);
        Assert.That(beatManager.Players[2].Master, Is.False);
        Assert.That(beatManager.Players[2].Synced, Is.Null);
        Assert.That(beatManager.Players[2].Live, Is.False);
        Assert.That(beatManager.Players[3].Playing, Is.True);
        Assert.That(beatManager.Players[3].OnAir, Is.Null);
        Assert.That(beatManager.Players[3].Live, Is.False);
    }

    /// <summary>Verifies stopping the broadcast clears every per-player value back to unavailable.</summary>
    [Test]
    public void BroadcastStopClearsPlayerValuesToUnavailable()
    {
        var beatManager = BuildLiveBeatManager(includeOtherLiveCases: false);
        Assert.That(beatManager.Players[1].Structure.Generation, Is.EqualTo(7));

        beatManager.SetLiveBeatSource(false);
        beatManager.Update(1f);

        Assert.That(beatManager.Players.Count, Is.EqualTo(6));
        for (var i = 0; i < beatManager.Players.Count; i++)
        {
            AssertPlayerUnavailable(beatManager.Players[i], i + 1);
        }
    }

    /// <summary>Verifies a packet for player 2 leaves all other physical-player slots unavailable.</summary>
    [Test]
    public void UntouchedPlayersRemainUnavailable()
    {
        var beatManager = BuildLiveBeatManager(includeOtherLiveCases: false);

        Assert.That(beatManager.Players.Count, Is.EqualTo(6));
        Assert.That(beatManager.Players[1].PlayerNumber, Is.EqualTo(2));
        Assert.That(beatManager.Players[1].Bpm, Is.EqualTo(127.5f).Within(0.0001f));
        for (var i = 0; i < beatManager.Players.Count; i++)
        {
            if (i != 1)
            {
                AssertPlayerUnavailable(beatManager.Players[i], i + 1);
            }
        }
    }

    /// <summary>Dispatches per-player wire bytes and captures their live BeatManager representation.</summary>
    private static BeatManager BuildLiveBeatManager(bool includeOtherLiveCases)
    {
        using var parser = new RaveOscPacketParser();
        Assert.That(parser.Dispatch(BuildPlayerBundle(includeOtherLiveCases)), Is.GreaterThan(0));
        Assert.That(parser.Dispatch(BuildPlayer2StructureDatagram()), Is.EqualTo(1));
        Assert.That(parser.Dispatch(BuildPlayer2StructureStateDatagram()), Is.EqualTo(1));
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);

        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);
        return beatManager;
    }

    /// <summary>Builds the continuous per-player bundle used by the round-trip tests.</summary>
    private static byte[] BuildPlayerBundle(bool includeOtherLiveCases)
    {
        var buffer = new byte[2048];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);
        WritePlayerClock(ref bundle, 2, 127.5f, 129, 33, 1, 0.75f);
        WritePlayerTransport(ref bundle, 2, 1, 0, 1, -1, 1);
        WritePlayerLoop(ref bundle, 2, 1, 1, 8f, 3750, 8, 1);
        WritePlayerGrid(ref bundle, 2, 13, 4, "coasting");

        if (includeOtherLiveCases)
        {
            WritePlayerClock(ref bundle, 3, -1f, -1, -1, -1, 0f);
            WritePlayerTransport(ref bundle, 3, 1, -1, 0, 0, -1);
            WritePlayerTransport(ref bundle, 4, 1, -1, -1, -1, -1);
        }

        var length = bundle.Finish();
        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>Builds one bare structure datagram with ordered known and unknown phrase types.</summary>
    private static byte[] BuildPlayer2StructureDatagram()
    {
        var buffer = new byte[1024];
        var writer = new OscWriter(buffer);
        writer.WriteAddress("/rave/player/2/structure");
        writer.WriteString("328123");
        writer.WriteInt32(7);
        writer.WriteString("fused");
        writer.WriteInt32(256);
        writer.WriteInt32(2);
        writer.WriteInt32(0);
        writer.WriteInt32(1);

        writer.WriteInt32(1);
        writer.WriteInt32(128);
        writer.WriteString("drop");
        writer.WriteInt32(1);
        writer.WriteInt32(120);
        writer.WriteInt32(0);

        writer.WriteInt32(129);
        writer.WriteInt32(256);
        writer.WriteString("not-canonical");
        writer.WriteInt32(0);
        writer.WriteInt32(0);
        writer.WriteInt32(129);

        var length = writer.Finish();
        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>Builds the generation-matched cursor datagram for the player 2 structure.</summary>
    private static byte[] BuildPlayer2StructureStateDatagram()
    {
        var buffer = new byte[256];
        var writer = new OscWriter(buffer);
        writer.WriteAddress("/rave/player/2/structure_state");
        writer.WriteInt32(7);
        writer.WriteInt32(2);
        writer.WriteInt32(8);
        writer.WriteInt32(16);
        var length = writer.Finish();
        return buffer.AsSpan(0, length).ToArray();
    }

    /// <summary>Writes one player's clock message into a bundle.</summary>
    private static void WritePlayerClock(ref OscBundleWriter bundle, int playerNumber, float bpm, int beat,
        int bar, int beatInBar, float beatPulse)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress($"/rave/player/{playerNumber}/clock");
        writer.WriteFloat32(bpm);
        writer.WriteInt32(beat);
        writer.WriteInt32(bar);
        writer.WriteInt32(beatInBar);
        writer.WriteFloat32(beatPulse);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes one player's five tri-state transport predicates into a bundle.</summary>
    private static void WritePlayerTransport(ref OscBundleWriter bundle, int playerNumber, int playing,
        int cued, int onAir, int master, int synced)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress($"/rave/player/{playerNumber}/transport");
        writer.WriteInt32(playing);
        writer.WriteInt32(cued);
        writer.WriteInt32(onAir);
        writer.WriteInt32(master);
        writer.WriteInt32(synced);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes one player's loop state into a bundle.</summary>
    private static void WritePlayerLoop(ref OscBundleWriter bundle, int playerNumber, int active, int set,
        float lengthBeats, int lengthMilliseconds, int sizeNumerator, int sizeDenominator)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress($"/rave/player/{playerNumber}/loop_state");
        writer.WriteInt32(active);
        writer.WriteInt32(set);
        writer.WriteFloat32(lengthBeats);
        writer.WriteInt32(lengthMilliseconds);
        writer.WriteInt32(sizeNumerator);
        writer.WriteInt32(sizeDenominator);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes one player's phrase-relative timing grid into a bundle.</summary>
    private static void WritePlayerGrid(ref OscBundleWriter bundle, int playerNumber, int beat, int bar,
        string state)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress($"/rave/player/{playerNumber}/timing_grid");
        writer.WriteInt32(beat);
        writer.WriteInt32(bar);
        writer.WriteString(state);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Asserts the public default contract for an unavailable physical-player slot.</summary>
    private static void AssertPlayerUnavailable(PlayerValues player, int expectedPlayerNumber)
    {
        Assert.That(player.PlayerNumber, Is.EqualTo(expectedPlayerNumber));
        Assert.That(player.Bpm, Is.Null);
        Assert.That(player.Beat, Is.Null);
        Assert.That(player.Bar, Is.Null);
        Assert.That(player.BeatInBar, Is.Null);
        Assert.That(player.BeatPulse, Is.Zero);
        Assert.That(player.Playing, Is.Null);
        Assert.That(player.Cued, Is.Null);
        Assert.That(player.OnAir, Is.Null);
        Assert.That(player.Master, Is.Null);
        Assert.That(player.Synced, Is.Null);
        Assert.That(player.Live, Is.False);
        Assert.That(player.Loop.Rolling, Is.False);
        Assert.That(player.Loop.RegionSet, Is.False);
        Assert.That(player.Loop.LengthBeats, Is.Null);
        Assert.That(player.Loop.LengthMilliseconds, Is.Null);
        Assert.That(player.Loop.SizeNumerator, Is.Null);
        Assert.That(player.Loop.SizeDenominator, Is.Null);
        Assert.That(player.GridState, Is.Null);
        Assert.That(player.GridBeat, Is.Null);
        Assert.That(player.GridBar, Is.Null);
        Assert.That(player.Structure.Generation, Is.Zero);
        Assert.That(player.Structure.TrackId, Is.Null);
        Assert.That(player.Structure.Source, Is.Null);
        Assert.That(player.Structure.TotalBeats, Is.Null);
        Assert.That(player.Structure.PhraseCount, Is.Zero);
        Assert.That(player.Structure.Phrases, Is.Empty);
        Assert.That(player.Cursor.Generation, Is.Zero);
        Assert.That(player.Cursor.CurrentPhrase, Is.Null);
        Assert.That(player.Cursor.BeatInPhrase, Is.Null);
        Assert.That(player.Cursor.BeatsToNextPhrase, Is.Null);
    }
}
