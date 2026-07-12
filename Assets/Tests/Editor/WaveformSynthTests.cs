// Seam-2 tests for the Waveform Synthesizer surface (beat-data ticket 18): the synth against
// seeded clock states and notation worked examples.

#nullable enable

using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Seam-2 tests for the Waveform Synthesizer surface (beat-data ticket 18): the synth against
/// seeded clock states and notation worked examples. Covers the derived Energy classification
/// (the seed seven, locked from notation), draws (Energy-set membership, by-name misses,
/// empty-match whole-Pool fallback), Evaluate against known clock states, the Hit edge on actual
/// onsets, peak-spacing worked examples, and the Waveform Hold reaching through draws. Expected
/// values come from the notation and the locked classifications, never from re-running the
/// implementation's math.
/// </summary>
public sealed class WaveformSynthTests
{
    private const float Tol = 0.0001f;

    // ---- Energy classification (derived from notation, never stored) ----------------------------

    /// <summary>The seed seven classify Low/Mid/High exactly as locked, straight from their notation.</summary>
    [Test]
    public void SeedSeven_ClassifyToTheLockedEnergyTiers()
    {
        // (name, sequence, amplitude, offset, locked tier) — the seven entries seeded in
        // penrose_waveforms.txt, classifications locked in the ticket.
        AssertEnergy("beats 1 and 3", "QQQQ", "8080", 0f, Energy.Low);
        AssertEnergy("beats 2 and 4", "QQQQ", "0808", 0f, Energy.Low);
        AssertEnergy("measure start", "QQQQ", "8000", 0f, Energy.Low);
        AssertEnergy("beat pulse", "QQQQ", "8888", 0f, Energy.Mid);
        AssertEnergy("beats 1 and 4", "QQQQ", "8008", 0f, Energy.Mid);
        AssertEnergy("offbeat", "QQQQ", "8888", 0.5f, Energy.Mid);
        AssertEnergy("every eighth", "EEEEEEEE", "88888888", 0f, Energy.High);
    }

    /// <summary>Amplitude heights never move the tier — a quiet pulse ranks with the full one.</summary>
    [Test]
    public void Energy_ExcludesAmplitudeHeights()
    {
        // Same rhythm as the beat pulse at quarter height: density 4 (Mid), gap 1 beat (Mid).
        AssertEnergy("quiet pulse", "QQQQ", "2222", 0f, Energy.Mid);
    }

    /// <summary>An inline Waveform classifies by the same tiers as a Pool entry — a half-beat gap reads High.</summary>
    [Test]
    public void Energy_CoversInlineWaveforms()
    {
        // "heartbeat": audible onsets at bar fractions 0, 0.125, 0.5, 0.625 → density 4 (Mid),
        // shortest gap 0.125 bar = half a beat (High) → High.
        AssertEnergy("heartbeat", "EEQEEQ", "860860", 0f, Energy.High);
    }

    /// <summary>
    /// A silent Waveform (no audible peak) reads Low: zero peaks sit below Low's density floor, and
    /// the spacing measurement's 0-for-silence convention must not leak High through the gap tier.
    /// </summary>
    [Test]
    public void Energy_SilenceReadsLow()
    {
        AssertEnergy("silence", "QQQQ", "0000", 0f, Energy.Low);
    }

    private static void AssertEnergy(string label, string sequence, string amplitude, float offset, Energy expected)
    {
        var wf = Waveform.Parse(sequence, amplitude, Waveform.BeatPulseRounding, offset);
        Assert.That(wf.Energy, Is.EqualTo(expected), $"{label} ({sequence}/{amplitude} @ {offset})");
    }

    // ---- Draws (acquisition: Energy-set draw, Preset name) --------------------------------------

    /// <summary>A no-args draw reaches the whole Pool — every draw is one of the seeded entries.</summary>
    [Test]
    public void Random_NoArgs_DrawsFromTheWholePool()
    {
        var synth = CreateSeededSynth();

        for (var i = 0; i < 40; i++)
        {
            AssertNotationIn(synth.Random(),
                ("QQQQ", "8888", 0f), ("QQQQ", "8080", 0f), ("QQQQ", "0808", 0f), ("QQQQ", "8000", 0f),
                ("QQQQ", "8008", 0f), ("QQQQ", "8888", 0.5f), ("EEEEEEEE", "88888888", 0f));
        }
    }

    /// <summary>An Energy-set draw stays inside the set — Low draws only the three Low entries.</summary>
    [Test]
    public void Random_AtLow_DrawsOnlyLowEntries()
    {
        var synth = CreateSeededSynth();

        for (var i = 0; i < 40; i++)
        {
            AssertNotationIn(synth.Random(Energy.Low),
                ("QQQQ", "8080", 0f), ("QQQQ", "0808", 0f), ("QQQQ", "8000", 0f));
        }
    }

    /// <summary>With one High entry in the Pool, a High draw is deterministic.</summary>
    [Test]
    public void Random_AtHigh_DrawsTheOnlyHighEntry()
    {
        var synth = CreateSeededSynth();

        AssertNotation(synth.Random(Energy.High), "EEEEEEEE", "88888888", 0f, "the only High entry");
    }

    /// <summary>An Energy set no entry matches falls back to a whole-Pool draw and logs the miss.</summary>
    [Test]
    public void Random_EmptyEnergyMatch_FallsBackWholePoolAndLogs()
    {
        // A Pool of only Low entries has nothing at High.
        var lowOnly = new[]
        {
            new WaveformPool.Entry("beats 1 and 3", Waveform.Parse("QQQQ", "8080")),
            new WaveformPool.Entry("measure start", Waveform.Parse("QQQQ", "8000")),
        };
        var synth = new WaveformSynth(new BeatManager(), lowOnly);

        LogAssert.Expect(LogType.Warning, new Regex("no Pool entry matches"));
        AssertNotationIn(synth.Random(Energy.High), ("QQQQ", "8080", 0f), ("QQQQ", "8000", 0f));
    }

    /// <summary>An empty Pool degrades to the canonical Beat Pulse, logged — a draw is never null.</summary>
    [Test]
    public void Random_EmptyPool_FallsBackToTheBeatPulseAndLogs()
    {
        LogAssert.Expect(LogType.Warning, new Regex("no entries"));
        var synth = new WaveformSynth(new BeatManager(), new WaveformPool.Entry[0]);

        AssertNotation(synth.Random(), "QQQQ", "8888", 0f, "the canonical Beat Pulse stand-in");
    }

    /// <summary>ByName serves the named Preset's Waveform value.</summary>
    [Test]
    public void ByName_ServesTheNamedPresetsWaveform()
    {
        var synth = CreateSeededSynth();

        var wf = synth.ByName("measure start");

        Assert.That(wf, Is.Not.Null);
        AssertNotation(wf!.Value, "QQQQ", "8000", 0f, "measure start");
    }

    /// <summary>ByName reads null when no Pool entry carries the name — the consumer picks its default.</summary>
    [Test]
    public void ByName_MissReadsNull()
    {
        var synth = CreateSeededSynth();

        Assert.That(synth.ByName("no such preset"), Is.Null);
    }

    // ---- Evaluate (the one primitive) ------------------------------------------------------------

    /// <summary>Evaluate serves the envelope at the current Bar Phase — peak on the downbeat, trough between beats.</summary>
    [Test]
    public void Evaluate_ServesTheEnvelopeAtTheCurrentBarPhase()
    {
        var pulse = Waveform.Parse("QQQQ", "8888");

        // Worked clock: 120 BPM at t = 0 is the downbeat (Bar Phase 0) — the Beat Pulse peaks at 1.
        var onBeat = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        onBeat.Update(0f);
        Assert.That(new WaveformSynth(onBeat, SeedEntries()).Evaluate(pulse), Is.EqualTo(1f).Within(Tol));

        // 120 BPM at t = 0.25 s is Bar Phase 0.125 — the midpoint trough between beats 1 and 2, 0.
        var offBeat = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        offBeat.Update(0.25f);
        Assert.That(new WaveformSynth(offBeat, SeedEntries()).Evaluate(pulse), Is.EqualTo(0f).Within(Tol));
    }

    /// <summary>With no clock there is no bar position — Evaluate reads null, a fact-read, not rest-at-0.</summary>
    [Test]
    public void Evaluate_ReadsNullWithNoClock()
    {
        var standalone = new BeatManager();
        standalone.Update(0f);
        var synth = new WaveformSynth(standalone, SeedEntries());

        Assert.That(synth.Evaluate(Waveform.Parse("QQQQ", "8888")), Is.Null);
    }

    /// <summary>A null Waveform — a consumer holding no rhythm — reads null even with a clock running.</summary>
    [Test]
    public void Evaluate_NullWaveformReadsNull()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        beatManager.Update(0f);
        var synth = new WaveformSynth(beatManager, SeedEntries());

        Assert.That(synth.Evaluate(null), Is.Null);
    }

    // ---- Peak spacing (feature parity — effects scale visuals with it) --------------------------

    /// <summary>Worked notation example: HQQ/844 has audible peaks at 0, 0.5, 0.75 — the wrap gap of a
    /// quarter bar is shortest, one beat = 500 ms at 120 BPM.</summary>
    [Test]
    public void ShortestPeakSpacingMs_FromWorkedNotation()
    {
        var synth = CreateSeededSynth(bpm: 120f, timeSeconds: 0f);

        Assert.That(synth.ShortestPeakSpacingMs(Waveform.Parse("HQQ", "844")), Is.EqualTo(500f).Within(Tol));
    }

    /// <summary>A single audible peak spaces one full bar from itself — 2000 ms at 120 BPM.</summary>
    [Test]
    public void ShortestPeakSpacingMs_SingleAudiblePeakIsTheFullBar()
    {
        var synth = CreateSeededSynth(bpm: 120f, timeSeconds: 0f);

        Assert.That(synth.ShortestPeakSpacingMs(Waveform.Parse("QQQQ", "8000")), Is.EqualTo(2000f).Within(Tol));
    }

    /// <summary>With no tempo the spacing has no ms expression — null, per lane, like every fact-read.</summary>
    [Test]
    public void ShortestPeakSpacingMs_ReadsNullWithNoTempo()
    {
        var standalone = new BeatManager();
        standalone.Update(0f);
        var synth = new WaveformSynth(standalone, SeedEntries());

        Assert.That(synth.ShortestPeakSpacingMs(Waveform.Parse("QQQQ", "8888")), Is.Null);
        Assert.That(synth.ShortestPeakSpacingMs(null), Is.Null);
    }

    // ---- The Hit edge (fires on any shape's actual onsets) --------------------------------------

    /// <summary>
    /// Hit fires exactly when the observation window crosses one of the shape's audible onsets —
    /// beat 3 lands for shapes that sound it, not for shapes that skip it.
    /// </summary>
    [Test]
    public void Hit_FiresOnActualOnsetsOnly()
    {
        // 120 BPM: bar = 2 s, so Bar Phase = t/2. Step the window from 0.45 to 0.55 — across the
        // beat-3 onset at phase 0.5.
        var (_, synth) = CreateSteppedSynth(0.9f, 1.1f);

        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8888")), Is.True, "the Beat Pulse sounds beat 3");
        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8080")), Is.True, "beats 1 and 3 sounds beat 3");
        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "0808")), Is.False, "beats 2 and 4 skips beat 3");
        Assert.That(synth.Hit(null), Is.False, "no rhythm held, no onset");
    }

    /// <summary>The edge is a single-frame truth: the next observation without a crossing reads false.</summary>
    [Test]
    public void Hit_IsASingleFrameTruth()
    {
        var (beatManager, synth) = CreateSteppedSynth(0.9f, 1.1f);

        Step(beatManager, synth, 1.15f); // phase 0.575 — no onset in (0.55, 0.575]

        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8888")), Is.False);
    }

    /// <summary>A Phase Offset moves where the humps land — the edge follows the shifted onsets.</summary>
    [Test]
    public void Hit_FollowsThePhaseOffset()
    {
        // The offbeat entry's onsets sit at phases 0.125/0.375/0.625/0.875. Window (0.6, 0.65]
        // crosses 0.625 — the offbeat fires where the plain pulse (onsets 0.5/0.75) is silent.
        var (_, synth) = CreateSteppedSynth(1.2f, 1.3f);
        var offbeat = Waveform.Parse("QQQQ", "8888", Waveform.BeatPulseRounding, 0.5f);

        Assert.That(synth.Hit(offbeat), Is.True, "the offbeat lands inside the window");
        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8888")), Is.False, "the plain pulse does not");
    }

    /// <summary>The window wraps with the bar: stepping across the downbeat catches the onset at 0.</summary>
    [Test]
    public void Hit_FiresAcrossTheBarWrap()
    {
        // Phase steps 0.95 → 0.05: the wrapped window covers the downbeat onset at phase 0.
        var (_, synth) = CreateSteppedSynth(1.9f, 2.1f);

        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8000")), Is.True, "measure start sounds the downbeat");
        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "0808")), Is.False, "no onset of 2-and-4 in the wrap window");
    }

    /// <summary>With no clock there is no window — the edge rests at false, never null.</summary>
    [Test]
    public void Hit_ReadsFalseWithNoClock()
    {
        var standalone = new BeatManager();
        standalone.Update(0f);
        var synth = new WaveformSynth(standalone, SeedEntries());
        synth.Update();
        synth.Update();

        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8888")), Is.False);
    }

    /// <summary>
    /// The first observation of a running clock opens no window — the music crossed that boundary
    /// unobserved, so nothing fires (the hub's edge identity rule), even sitting on the downbeat.
    /// </summary>
    [Test]
    public void Hit_ReadsFalseOnFirstObservation()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        beatManager.Update(0f);
        var synth = new WaveformSynth(beatManager, SeedEntries());

        synth.Update();

        Assert.That(synth.Hit(Waveform.Parse("QQQQ", "8888")), Is.False);
    }

    // ---- The Waveform Hold (released state: Auto) ------------------------------------------------

    /// <summary>While engaged, the Hold pins every draw — random and by-name alike — to the held value.</summary>
    [Test]
    public void Hold_PinsEveryDrawToTheHeldValue()
    {
        var synth = CreateSeededSynth();
        var heartbeat = Waveform.Parse("EEQEEQ", "860860");

        synth.Hold(heartbeat);

        AssertNotation(synth.Random(), "EEQEEQ", "860860", 0f, "whole-Pool draw under the Hold");
        AssertNotation(synth.Random(Energy.Low), "EEQEEQ", "860860", 0f, "Energy-set draw under the Hold");
        var byName = synth.ByName("beat pulse");
        Assert.That(byName, Is.Not.Null);
        AssertNotation(byName!.Value, "EEQEEQ", "860860", 0f, "by-name acquisition under the Hold");
        // A miss is still a fact about the Pool — the Hold never invents an entry for an unknown name.
        Assert.That(synth.ByName("no such preset"), Is.Null);
    }

    /// <summary>Releasing to Auto restores each consumer's own acquisition immediately.</summary>
    [Test]
    public void ReleaseToAuto_RestoresAcquisition()
    {
        var synth = CreateSeededSynth();
        synth.Hold(Waveform.Parse("EEQEEQ", "860860"));

        synth.ReleaseToAuto();

        var byName = synth.ByName("beat pulse");
        Assert.That(byName, Is.Not.Null);
        AssertNotation(byName!.Value, "QQQQ", "8888", 0f, "by-name after release");
        AssertNotation(synth.Random(Energy.High), "EEEEEEEE", "88888888", 0f, "High draw after release");
    }

    // ---- Fixture helpers -------------------------------------------------------------------------

    /// <summary>
    /// The seed seven as known Pool entries, built from notation — tests accept dependencies rather
    /// than reading StreamingAssets.
    /// </summary>
    private static WaveformPool.Entry[] SeedEntries()
    {
        var r = Waveform.BeatPulseRounding;
        return new[]
        {
            new WaveformPool.Entry("beat pulse", Waveform.Parse("QQQQ", "8888", r, 0f)),
            new WaveformPool.Entry("beats 1 and 3", Waveform.Parse("QQQQ", "8080", r, 0f)),
            new WaveformPool.Entry("beats 2 and 4", Waveform.Parse("QQQQ", "0808", r, 0f)),
            new WaveformPool.Entry("measure start", Waveform.Parse("QQQQ", "8000", r, 0f)),
            new WaveformPool.Entry("beats 1 and 4", Waveform.Parse("QQQQ", "8008", r, 0f)),
            new WaveformPool.Entry("offbeat", Waveform.Parse("QQQQ", "8888", r, 0.5f)),
            new WaveformPool.Entry("every eighth", Waveform.Parse("EEEEEEEE", "88888888", r, 0f)),
        };
    }

    /// <summary>A synth over the seed seven with no clock — draws and the Hold need no bar position.</summary>
    private static WaveformSynth CreateSeededSynth()
    {
        return new WaveformSynth(new BeatManager(), SeedEntries());
    }

    /// <summary>A synth over the seed seven against the seeded metronome clock.</summary>
    private static WaveformSynth CreateSeededSynth(float bpm, float timeSeconds)
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm, timeSeconds);
        beatManager.Update(timeSeconds);
        return new WaveformSynth(beatManager, SeedEntries());
    }

    /// <summary>
    /// A synth stepped across two observations of the 120 BPM metronome — the Hit window runs from
    /// the first time's Bar Phase (exclusive) to the second's (inclusive).
    /// </summary>
    private static (BeatManager, WaveformSynth) CreateSteppedSynth(float firstSeconds, float secondSeconds)
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: firstSeconds);
        beatManager.Update(firstSeconds);
        var synth = new WaveformSynth(beatManager, SeedEntries());
        synth.Update();
        Step(beatManager, synth, secondSeconds);
        return (beatManager, synth);
    }

    /// <summary>Advances the seeded metronome to <paramref name="timeSeconds"/> and steps the synth after the hub.</summary>
    private static void Step(BeatManager beatManager, WaveformSynth synth, float timeSeconds)
    {
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: timeSeconds);
        beatManager.Update(timeSeconds);
        synth.Update();
    }

    /// <summary>Asserts a Waveform value carries the expected notation (its identity: sequence, amplitude, offset).</summary>
    private static void AssertNotation(Waveform actual, string sequence, string amplitude, float offset, string message)
    {
        Assert.That((actual.sequence, actual.amplitude, actual.offset),
            Is.EqualTo((sequence, amplitude, offset)), message);
    }

    /// <summary>Asserts a drawn Waveform's notation is one of the expected set.</summary>
    private static void AssertNotationIn(Waveform actual, params (string sequence, string amplitude, float offset)[] expected)
    {
        var actualNotation = (actual.sequence, actual.amplitude, actual.offset);
        foreach (var candidate in expected)
        {
            if (actualNotation.Equals(candidate))
            {
                return;
            }
        }

        Assert.Fail($"drawn Waveform {actualNotation} is outside the expected entries");
    }
}
