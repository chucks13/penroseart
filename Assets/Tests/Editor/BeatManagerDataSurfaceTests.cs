// Seam-1 tests for the BeatManager Data Surface: wire snapshot state in, doorway reads out.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;
using RaveSystem.Osc;

/// <summary>
/// Seam-1 tests for the Data Surface core (beat-data ticket 15): wire snapshot state in, doorway
/// reads out. Covers sentinel → null translation per doorway, the recorded on-beats wire regression,
/// frame coherence of captured views, and edge single-frame truth. Expected values come from the
/// wire contract and the repo's worked clock examples (the 120 BPM seeded metronome), never from
/// re-running the implementation's math.
/// </summary>
public sealed class BeatManagerDataSurfaceTests
{
    // ---- Clock and Position -------------------------------------------------------------------

    /// <summary>A fresh manager — before any update and in Standalone Mode — serves null facts and false edges.</summary>
    [Test]
    public void DoorwaysAreInertBeforeAnyUpdateAndInStandalone()
    {
        var beatManager = new BeatManager();

        // Before the first hub update the doorways are default views: all facts null, all edges false.
        Assert.That(beatManager.Clock.Bpm, Is.Null);
        Assert.That(beatManager.Beats.OnBeat, Is.Null);
        Assert.That(beatManager.Beats.GateOpened(1), Is.False);
        Assert.That(beatManager.Pulses.Every(Duration.Quarter), Is.Null);
        Assert.That(beatManager.Track.Changed, Is.False);

        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.False);
        Assert.That(beatManager.Clock.Bpm, Is.Null);
        Assert.That(beatManager.Clock.BeatAverageMs, Is.Null);
        Assert.That(beatManager.Clock.BarPhase, Is.Null);
        Assert.That(beatManager.Clock.BeatFraction, Is.Null);
        Assert.That(beatManager.Position.Beat, Is.Null);
        Assert.That(beatManager.Position.BeatInBar, Is.Null);
        Assert.That(beatManager.Track.PlayersLive, Is.Null);
        Assert.That(beatManager.Track.TrackTitle, Is.Null);
        Assert.That(beatManager.Beats.NextBeatMs, Is.Null);
        Assert.That(beatManager.Beats.OnBeat, Is.Null);
        Assert.That(beatManager.Beats.OnBeatOpened, Is.False);
        Assert.That(beatManager.OffBeats.OffBeat, Is.Null);
        Assert.That(beatManager.OffBeats.NextOffBeatMs, Is.Null);
        Assert.That(beatManager.Pulses.Beat, Is.Null);
        Assert.That(beatManager.Pulses.OffBeat, Is.Null);
        Assert.That(beatManager.Pulses.GateEvery(Duration.Eighth), Is.Null);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Sixteenth), Is.False);
    }

    /// <summary>The seeded 120 BPM metronome half a beat into beat 1 serves the worked clock values.</summary>
    [Test]
    public void ClockServesTempoAndOfferedClockWhenSynced()
    {
        // Worked example: 120 BPM at t = 0.25 s carries beatInBar 1, avg 500 ms, next-beat 250 ms,
        // so the beat is half elapsed and the bar an eighth elapsed.
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);

        beatManager.Update(0.25f);

        Assert.That(beatManager.IsSynced, Is.True);
        Assert.That(beatManager.Clock.Bpm, Is.EqualTo(120f).Within(0.0001f));
        Assert.That(beatManager.Clock.BeatAverageMs, Is.EqualTo(500f).Within(0.0001f));
        Assert.That(beatManager.Clock.BeatFraction, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Clock.BarPhase, Is.EqualTo(0.125f).Within(0.0001f));
    }

    /// <summary>Tempo sentinels translate to null per lane while the 4-count clock stays served.</summary>
    [Test]
    public void ClockTranslatesTempoSentinelsToNullWhileSynced()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.beatInBar = 2;
        beatManager.WireSnapshot.bpm = -1f;
        beatManager.WireSnapshot.beatAverageMs = -1;

        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.True);
        Assert.That(beatManager.Clock.Bpm, Is.Null);
        Assert.That(beatManager.Clock.BeatAverageMs, Is.Null);
        // A running 4-count is what Synced Mode means, so the offered clock is present: on the 2
        // with no sub-beat data, the bar is a quarter elapsed.
        Assert.That(beatManager.Clock.BarPhase, Is.EqualTo(0.25f).Within(0.0001f));
    }

    /// <summary>Position lanes serve wire facts and translate each lane's -1 sentinel on its own.</summary>
    [Test]
    public void PositionServesWireFactsAndTranslatesSentinels()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.beatInBar = 3;
        beatManager.WireSnapshot.beat = new BeatPosition { current = 64, total = 384 };
        beatManager.WireSnapshot.bar = new BarPosition { current = 16, nextMs = 777 };

        beatManager.Update(0f);

        Assert.That(beatManager.Position.Beat, Is.EqualTo(64));
        Assert.That(beatManager.Position.TotalBeats, Is.EqualTo(384));
        Assert.That(beatManager.Position.Bar, Is.EqualTo(16));
        Assert.That(beatManager.Position.BeatInBar, Is.EqualTo(3));

        beatManager.WireSnapshot.beat = new BeatPosition { current = -1, total = -1 };
        beatManager.WireSnapshot.bar = new BarPosition { current = -1, nextMs = -1 };
        beatManager.Update(0f);

        Assert.That(beatManager.Position.Beat, Is.Null);
        Assert.That(beatManager.Position.TotalBeats, Is.Null);
        Assert.That(beatManager.Position.Bar, Is.Null);
        Assert.That(beatManager.Position.BeatInBar, Is.EqualTo(3));
    }

    // ---- Track --------------------------------------------------------------------------------

    /// <summary>Track identity facts and the live player list serve straight off the wire.</summary>
    [Test]
    public void TrackServesIdentityAndPlayersLive()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.track = "Artist - Track";
        beatManager.WireSnapshot.trackId = 777001;
        beatManager.WireSnapshot.playersLive = "4,2";

        beatManager.Update(0f);

        Assert.That(beatManager.Track.TrackTitle, Is.EqualTo("Artist - Track"));
        Assert.That(beatManager.Track.TrackId, Is.EqualTo(777001));
        Assert.That(beatManager.Track.PlayersLive, Is.EqualTo(new[] { 4, 2 }));
    }

    /// <summary>Nobody live (wire "") is an empty list; no live source at all is null.</summary>
    [Test]
    public void TrackPlayersLiveDistinguishesNobodyLiveFromUnavailable()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.playersLive = "";
        beatManager.WireSnapshot.track = "";
        beatManager.WireSnapshot.trackId = -1;
        beatManager.Update(0f);

        Assert.That(beatManager.Track.PlayersLive, Is.Not.Null);
        Assert.That(beatManager.Track.PlayersLive, Is.Empty);
        Assert.That(beatManager.Track.TrackTitle, Is.Null);
        Assert.That(beatManager.Track.TrackId, Is.Null);

        var standalone = new BeatManager();
        standalone.Update(0f);
        Assert.That(standalone.Track.PlayersLive, Is.Null);
    }

    /// <summary>Track.Changed keys on the (trackId, title) identity pair, including to/from null, one frame each.</summary>
    [Test]
    public void TrackChangedFiresOncePerIdentityChangeIncludingToAndFromNull()
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.False, "nothing on air yet — no change");

        beatManager.WireSnapshot.track = "Artist - One";
        beatManager.WireSnapshot.trackId = 7;
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.True, "a track appeared from nothing");
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.False, "an edge is true for exactly one frame");

        beatManager.WireSnapshot.trackId = 8;
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.True, "an id change with the same title is a new identity");
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.False);

        beatManager.WireSnapshot.track = "Artist - Two";
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.True, "a title change with the same id is a new identity");

        beatManager.SetLiveBeatSource(false);
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.True, "the track vanishing to nothing on air is a change");
        beatManager.Update(0f);
        Assert.That(beatManager.Track.Changed, Is.False);
    }

    // ---- Beats --------------------------------------------------------------------------------

    /// <summary>The on-beat cluster serves the wire countdowns, gates, next-bar, and current-count On Beat.</summary>
    [Test]
    public void BeatsServeWireCountdownsGatesAndNextBar()
    {
        var beatManager = CreateLiveClock(beatInBar: 3,
            beatsCountMs: new[] { 1900, 2900, 0, 900 },
            onBeats: new[] { false, false, true, false });
        beatManager.WireSnapshot.bar = new BarPosition { current = 16, nextMs = 777 };

        beatManager.Update(0f);

        Assert.That(beatManager.Beats.NextBeatMs, Is.EqualTo(0f));
        Assert.That(beatManager.Beats.NextBarMs, Is.EqualTo(777f));
        Assert.That(beatManager.Beats.MsUntil(3), Is.EqualTo(0f));
        Assert.That(beatManager.Beats.MsUntil(1), Is.EqualTo(1900f));
        Assert.That(beatManager.Beats.Gate(3), Is.True);
        Assert.That(beatManager.Beats.Gate(1), Is.False);
        Assert.That(beatManager.Beats.OnBeat, Is.True, "the current count (3) has its gate open");

        // Out-of-range counts are not lanes: null facts, false edges.
        Assert.That(beatManager.Beats.MsUntil(5), Is.Null);
        Assert.That(beatManager.Beats.Gate(0), Is.Null);
        Assert.That(beatManager.Beats.GateOpened(9), Is.False);
    }

    /// <summary>
    /// The recorded wire bug (beat-data spec "Known wire bug"): the on-beats contract's
    /// -1 -1 -1 -1 unavailable shape, fed as real bytes through the parser, reads as null gates at
    /// the doorway — never four open gates.
    /// </summary>
    [Test]
    public void OnBeatsUnavailableWireShapeReadsAsNullGatesNeverOpenOnes()
    {
        var buffer = new byte[512];
        var bundle = new OscBundleWriter(buffer, OscTimeTag.Immediately);
        OnAirOscWriter.WriteFloat(ref bundle, "/rave/onair/bpm", 128.5f);
        OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_in_bar", 3);
        OnAirOscWriter.WriteInt(ref bundle, "/rave/onair/beat_avg_ms", 500);
        OnAirOscWriter.WriteFourInts(ref bundle, "/rave/onair/beats_count_ms", -1, -1, -1, -1);
        OnAirOscWriter.WriteFourInts(ref bundle, "/rave/onair/on_beats", -1, -1, -1, -1);
        var packet = System.MemoryExtensions.AsSpan(buffer, 0, bundle.Finish()).ToArray();

        using var parser = new RaveOscPacketParser();
        parser.Dispatch(packet);
        Assert.That(parser.TryTakeSnapshot(out var snapshot), Is.True);

        var beatManager = new BeatManager();
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.IsSynced, Is.True);
        for (var count = 1; count <= 4; count++)
        {
            Assert.That(beatManager.Beats.Gate(count), Is.Null, $"count {count} must read unavailable, not open");
        }
        Assert.That(beatManager.Beats.OnBeat, Is.Null);
        Assert.That(beatManager.Beats.OnBeatOpened, Is.False);
        Assert.That(beatManager.Beats.NextBeatMs, Is.Null);
    }

    /// <summary>A single lane's countdown sentinel nulls that lane's gate while its siblings stay served.</summary>
    [Test]
    public void BeatsGateNullnessFollowsEachCountdownLaneSentinel()
    {
        var beatManager = CreateLiveClock(beatInBar: 1,
            beatsCountMs: new[] { 100, -1, 300, 400 },
            onBeats: new[] { true, true, false, false });

        beatManager.Update(0f);

        Assert.That(beatManager.Beats.Gate(1), Is.True);
        Assert.That(beatManager.Beats.Gate(2), Is.Null, "an unavailable lane never reads as a gate value");
        Assert.That(beatManager.Beats.Gate(3), Is.False);
        Assert.That(beatManager.Beats.MsUntil(2), Is.Null);
        Assert.That(beatManager.Beats.NextBeatMs, Is.EqualTo(100f));
    }

    /// <summary>A gate-opening edge is true exactly the frame the served gate opens, false the next update.</summary>
    [Test]
    public void BeatsGateOpenedFiresExactlyTheFrameTheGateOpens()
    {
        var beatManager = CreateLiveClock(beatInBar: 3,
            beatsCountMs: new[] { 1900, 2900, 500, 900 },
            onBeats: new[] { false, false, false, false });
        beatManager.Update(0f);
        Assert.That(beatManager.Beats.GateOpened(3), Is.False);

        beatManager.WireSnapshot.beatsCountMs[2] = 0;
        beatManager.WireSnapshot.onBeats[2] = true;
        beatManager.Update(0f);
        Assert.That(beatManager.Beats.GateOpened(3), Is.True);
        Assert.That(beatManager.Beats.OnBeatOpened, Is.True, "the current count is 3, so On Beat opened too");

        beatManager.Update(0f);
        Assert.That(beatManager.Beats.Gate(3), Is.True, "the gate window is still open");
        Assert.That(beatManager.Beats.GateOpened(3), Is.False, "but the opening was last frame");
        Assert.That(beatManager.Beats.OnBeatOpened, Is.False);
    }

    /// <summary>A gate first observed open (unavailable → open) still counts as an opening.</summary>
    [Test]
    public void BeatsGateOpenedTreatsUnavailableToOpenAsAnOpening()
    {
        var beatManager = new BeatManager();
        beatManager.Update(0f); // Standalone frame: every gate reads null.

        var snapshot = new RaveOnAirSnapshot
        {
            bpm = 120f,
            beatInBar = 1,
            beatAverageMs = 500,
            beatsCountMs = new[] { 0, 500, 1000, 1500 },
            onBeats = new[] { true, false, false, false },
        };
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(0f);

        Assert.That(beatManager.Beats.Gate(1), Is.True);
        Assert.That(beatManager.Beats.GateOpened(1), Is.True);
        Assert.That(beatManager.Beats.OnBeatOpened, Is.True);
    }

    /// <summary>A frame hitch spanning two counts' open windows still reads On Beat's opening — it is the current count's own lane edge.</summary>
    [Test]
    public void OnBeatOpenedFiresWhenConsecutiveFramesSitInDifferentCountsOpenWindows()
    {
        // Frame A lands inside count 2's open window...
        var beatManager = CreateLiveClock(beatInBar: 2,
            beatsCountMs: new[] { 1500, 0, 500, 1000 },
            onBeats: new[] { false, true, false, false });
        beatManager.Update(0f);
        Assert.That(beatManager.Beats.OnBeat, Is.True);

        // ...and after a hitch, frame B lands inside count 3's open window. The served On Beat
        // value stayed true across both frames, but a beat landed: the current count's lane opened.
        beatManager.WireSnapshot.beatInBar = 3;
        beatManager.WireSnapshot.beatsCountMs = new[] { 1000, 1500, 0, 500 };
        beatManager.WireSnapshot.onBeats = new[] { false, false, true, false };
        beatManager.Update(0f);

        Assert.That(beatManager.Beats.OnBeat, Is.True);
        Assert.That(beatManager.Beats.GateOpened(3), Is.True);
        Assert.That(beatManager.Beats.OnBeatOpened, Is.True, "count 3's window opened this frame — the landed beat must not be missed");
    }

    /// <summary>Captured views stay identical for every reader until the next hub update, even when the snapshot mutates.</summary>
    [Test]
    public void ViewsAreFrameCoherentAcrossInPlaceSnapshotMutation()
    {
        var beatManager = CreateLiveClock(beatInBar: 3,
            beatsCountMs: new[] { 1900, 2900, 0, 900 },
            onBeats: new[] { false, false, true, false });
        beatManager.WireSnapshot.track = "Artist - One";
        beatManager.Update(0f);

        // Mutate the held snapshot in place, as the transport does mid-frame.
        beatManager.WireSnapshot.beatsCountMs[2] = 400;
        beatManager.WireSnapshot.onBeats[2] = false;
        beatManager.WireSnapshot.bpm = 90f;
        beatManager.WireSnapshot.track = "Artist - Two";

        Assert.That(beatManager.Beats.MsUntil(3), Is.EqualTo(0f), "the captured view serves capture-time values");
        Assert.That(beatManager.Beats.Gate(3), Is.True);
        Assert.That(beatManager.Clock.Bpm, Is.EqualTo(120f).Within(0.0001f));
        Assert.That(beatManager.Track.TrackTitle, Is.EqualTo("Artist - One"));

        beatManager.Update(0f);

        Assert.That(beatManager.Beats.MsUntil(3), Is.EqualTo(400f), "the next update serves the new truth");
        Assert.That(beatManager.Beats.Gate(3), Is.False);
        Assert.That(beatManager.Clock.Bpm, Is.EqualTo(90f).Within(0.0001f));
        Assert.That(beatManager.Track.TrackTitle, Is.EqualTo("Artist - Two"));
    }

    // ---- OffBeats -----------------------------------------------------------------------------

    /// <summary>The OffBeats doorway mirrors Beats from the derived midpoints (the repo's worked offbeat example).</summary>
    [Test]
    public void OffBeatsMirrorTheBeatsClusterFromDerivedMidpoints()
    {
        // Worked example (existing offbeat derivation tests): beat countdowns {1750,250,750,1250}
        // at 500 ms/beat derive offbeat countdowns {0,500,1000,1500} with slot 1's gate open.
        var beatManager = CreateLiveClock(beatInBar: 1,
            beatsCountMs: new[] { 1750, 250, 750, 1250 },
            onBeats: new[] { false, false, false, false });

        beatManager.Update(0f);

        Assert.That(beatManager.OffBeats.MsUntil(1), Is.EqualTo(0f));
        Assert.That(beatManager.OffBeats.MsUntil(2), Is.EqualTo(500f));
        Assert.That(beatManager.OffBeats.Gate(1), Is.True);
        Assert.That(beatManager.OffBeats.Gate(2), Is.False);
        Assert.That(beatManager.OffBeats.NextOffBeatMs, Is.EqualTo(0f));
        Assert.That(beatManager.OffBeats.OffBeat, Is.True, "the current slot (the & of 1) has its gate open");
    }

    /// <summary>The offbeat opening edges are single-frame, mirroring the on-beat edge rule.</summary>
    [Test]
    public void OffBeatOpenedFiresTheFrameTheDerivedGateOpens()
    {
        // First frame: countdowns whose derived offbeat gates are all closed; second frame:
        // countdowns whose derived slot-1 gate is open (both from the existing derivation tests).
        var beatManager = CreateLiveClock(beatInBar: 1,
            beatsCountMs: new[] { 1570, 70, 570, 1070 },
            onBeats: new[] { false, false, false, false });
        beatManager.Update(0f);
        Assert.That(beatManager.OffBeats.Gate(1), Is.False);
        Assert.That(beatManager.OffBeats.OffBeatOpened, Is.False);

        beatManager.WireSnapshot.beatsCountMs = new[] { 1700, 200, 700, 1200 };
        beatManager.Update(0f);
        Assert.That(beatManager.OffBeats.Gate(1), Is.True);
        Assert.That(beatManager.OffBeats.GateOpened(1), Is.True);
        Assert.That(beatManager.OffBeats.OffBeatOpened, Is.True);

        beatManager.Update(0f);
        Assert.That(beatManager.OffBeats.Gate(1), Is.True);
        Assert.That(beatManager.OffBeats.GateOpened(1), Is.False);
        Assert.That(beatManager.OffBeats.OffBeatOpened, Is.False);
    }

    /// <summary>The mirror of the hitch shape: consecutive frames inside different slots' open windows still read Off Beat's opening.</summary>
    [Test]
    public void OffBeatOpenedFiresWhenConsecutiveFramesSitInDifferentSlotsOpenWindows()
    {
        // Frame A: the & of 2 is landing (rotating the worked-example countdowns opens slot 2's derived gate)...
        var beatManager = CreateLiveClock(beatInBar: 2,
            beatsCountMs: new[] { 1250, 1750, 250, 750 },
            onBeats: new[] { false, false, false, false });
        beatManager.Update(0f);
        Assert.That(beatManager.OffBeats.OffBeat, Is.True);

        // ...frame B: the & of 3 is landing while the count advanced to 3 — Off Beat stayed true,
        // but a new "&" landed.
        beatManager.WireSnapshot.beatInBar = 3;
        beatManager.WireSnapshot.beatsCountMs = new[] { 750, 1250, 1750, 250 };
        beatManager.Update(0f);

        Assert.That(beatManager.OffBeats.OffBeat, Is.True);
        Assert.That(beatManager.OffBeats.GateOpened(3), Is.True);
        Assert.That(beatManager.OffBeats.OffBeatOpened, Is.True, "slot 3's window opened this frame — the landed & must not be missed");
    }

    // ---- Pulses -------------------------------------------------------------------------------

    /// <summary>The wire triangle and offbeat ramp serve gated by the clock — 0.0 is a real value, not "unavailable".</summary>
    [Test]
    public void PulsesServeTheWireTriangleAndOffbeatRampGatedByTheClock()
    {
        // The seeded metronome at t = 0.25 s: the beat pulse is half decayed (smoothstep midpoint)
        // and the offbeat ramp peaks at 1 exactly on the &.
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        beatManager.Update(0.25f);

        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Pulses.OffBeat, Is.EqualTo(1f).Within(0.0001f));

        // The lane where null cannot be manufactured from the value: 0.0 is both trough and
        // "no timing", so availability derives from the clock, never the value.
        beatManager.WireSnapshot.beatPulse = 0f;
        beatManager.Update(0.25f);
        Assert.That(beatManager.Pulses.Beat, Is.EqualTo(0f), "a served 0.0 while synced is a real trough");

        beatManager.SetLiveBeatSource(false);
        beatManager.Update(0.3f);
        Assert.That(beatManager.Pulses.Beat, Is.Null);
        Assert.That(beatManager.Pulses.OffBeat, Is.Null);
    }

    /// <summary>Duration pulses and gates run the ladder off the Bar Phase clock (hand-worked smoothstep values).</summary>
    [Test]
    public void PulsesEveryAndGateEveryRunTheDurationLadder()
    {
        // Seeded clock at Bar Phase 0.125 = half a beat into the bar. Cycle phases by rung:
        // Whole 0.125, Half 0.25, Quarter 0.5, Eighth 0, Sixteenth 0. A Duration Pulse is
        // 1 - smoothstep(phase), with smoothstep(t) = t²(3 - 2t) — worked by hand below.
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        beatManager.Update(0.25f);

        Assert.That(beatManager.Pulses.Every(Duration.Whole), Is.EqualTo(0.95703125f).Within(0.0001f));
        Assert.That(beatManager.Pulses.Every(Duration.Half), Is.EqualTo(0.84375f).Within(0.0001f));
        Assert.That(beatManager.Pulses.Every(Duration.Quarter), Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(beatManager.Pulses.Every(Duration.Eighth), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(beatManager.Pulses.Every(Duration.Sixteenth), Is.EqualTo(1f).Within(0.0001f));

        Assert.That(beatManager.Pulses.GateEvery(Duration.Quarter), Is.False, "phase 0.5 is past the default 0.25 duty");
        Assert.That(beatManager.Pulses.GateEvery(Duration.Quarter, duty: 0.6f), Is.True);
        Assert.That(beatManager.Pulses.GateEvery(Duration.Eighth), Is.True, "phase 0 is inside any duty window");
    }

    /// <summary>A Duration gate's opening edge is its cycle wrap during continuous sync — one frame, never on sync onset.</summary>
    [Test]
    public void GateOpenedEveryFiresOnCycleWrapDuringContinuousSyncOnly()
    {
        var beatManager = new BeatManager();

        // Quarter phase 0.8 (0.4 s into a 120 BPM bar is 0.8 beats in)...
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: 0.4f);
        beatManager.Update(0.4f);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Quarter), Is.False);

        // ...then quarter phase 0.1 (1.1 beats in): the beat cycle wrapped between observations.
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: 0.55f);
        beatManager.Update(0.55f);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Quarter), Is.True);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Whole), Is.False, "the bar cycle did not wrap");

        // Quarter phase 0.4: no wrap, the edge was last frame only.
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: 0.7f);
        beatManager.Update(0.7f);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Quarter), Is.False);
    }

    /// <summary>Becoming synced is a mode change, not a cycle boundary — no Duration gate edge fires.</summary>
    [Test]
    public void GateOpenedEveryStaysFalseOnSyncOnset()
    {
        var beatManager = new BeatManager();
        beatManager.Update(0f); // Standalone: no phases observed.

        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: 0.1f);
        beatManager.Update(0.1f);

        Assert.That(beatManager.IsSynced, Is.True);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Whole), Is.False);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Quarter), Is.False);
        Assert.That(beatManager.Pulses.GateOpenedEvery(Duration.Sixteenth), Is.False);
    }

    // ---- Fixtures -----------------------------------------------------------------------------

    /// <summary>Builds a live-sourced manager carrying a usable 120 BPM clock and the supplied beat lanes.</summary>
    private static BeatManager CreateLiveClock(int beatInBar, int[] beatsCountMs, bool[] onBeats)
    {
        var beatManager = new BeatManager();
        beatManager.SetLiveBeatSource(true);
        beatManager.WireSnapshot.bpm = 120f;
        beatManager.WireSnapshot.beatInBar = beatInBar;
        beatManager.WireSnapshot.beatAverageMs = 500;
        beatManager.WireSnapshot.beatsCountMs = beatsCountMs;
        beatManager.WireSnapshot.onBeats = onBeats;
        return beatManager;
    }
}
