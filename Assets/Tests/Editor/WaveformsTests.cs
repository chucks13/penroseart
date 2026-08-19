// Caller-facing tests for Waveform acquisition and held-value playback.

using System;
using NUnit.Framework;

/// <summary>
/// Proves Waveform Energy classification, Pool invariants, Energy-set acquisition, and the
/// non-null runtime response API against known clock states.
/// </summary>
public sealed class WaveformsTests
{
    private const float Tol = 0.0001f;

    /// <summary>The seed seven classify Low/Mid/High exactly as locked, straight from notation.</summary>
    [Test]
    public void SeedSeven_ClassifyToTheLockedEnergyTiers()
    {
        AssertEnergy("beats 1 and 3", "QQQQ", "8080", 0f, Energy.Low);
        AssertEnergy("beats 2 and 4", "QQQQ", "0808", 0f, Energy.Low);
        AssertEnergy("measure start", "QQQQ", "8000", 0f, Energy.Low);
        AssertEnergy("beat pulse", "QQQQ", "8888", 0f, Energy.Mid);
        AssertEnergy("beats 1 and 4", "QQQQ", "8008", 0f, Energy.Mid);
        AssertEnergy("offbeat", "QQQQ", "8888", 0.5f, Energy.Mid);
        AssertEnergy("every eighth", "EEEEEEEE", "88888888", 0f, Energy.High);
    }

    /// <summary>Amplitude heights never move the Energy tier.</summary>
    [Test]
    public void Energy_ExcludesAmplitudeHeights()
    {
        AssertEnergy("quiet pulse", "QQQQ", "2222", 0f, Energy.Mid);
    }

    /// <summary>A silent Waveform reads Low rather than deriving High from its zero spacing.</summary>
    [Test]
    public void Energy_SilenceReadsLow()
    {
        AssertEnergy("silence", "QQQQ", "0000", 0f, Energy.Low);
    }

    /// <summary>A no-args draw reaches only entries from the supplied Pool.</summary>
    [Test]
    public void Random_NoArgs_DrawsFromTheWholePool()
    {
        var waveforms = CreateSeededWaveforms();

        for (var i = 0; i < 40; i++)
        {
            AssertNotationIn(waveforms.Random(),
                ("QQQQ", "8888", 0f), ("QQQQ", "8080", 0f), ("QQQQ", "0808", 0f),
                ("QQQQ", "8000", 0f), ("QQQQ", "8008", 0f), ("QQQQ", "8888", 0.5f),
                ("EEEEEEEE", "88888888", 0f));
        }
    }

    /// <summary>An Energy-set draw stays inside the requested set.</summary>
    [Test]
    public void Random_AtLow_DrawsOnlyLowEntries()
    {
        var waveforms = CreateSeededWaveforms();

        for (var i = 0; i < 40; i++)
        {
            AssertNotationIn(waveforms.Random(Energy.Low),
                ("QQQQ", "8080", 0f), ("QQQQ", "0808", 0f), ("QQQQ", "8000", 0f));
        }
    }

    /// <summary>A requested Energy absent from the Pool is a visible configuration error.</summary>
    [Test]
    public void Random_WithoutMatchingEnergy_Throws()
    {
        var waveforms = new Waveforms(new BeatManager(), new[]
        {
            Entry("measure start", "QQQQ", "8000"),
        });

        Assert.Throws<InvalidOperationException>(() => waveforms.Random(Energy.High));
    }

    /// <summary>A named draw returns exactly the Pool entry saved under that name.</summary>
    [Test]
    public void Named_ReturnsTheEntrySavedUnderThatName()
    {
        var waveforms = new Waveforms(new BeatManager(), new[]
        {
            Entry("first", "QQQQ", "8888"),
            Entry("second", "QQQQ", "0808"),
        });

        AssertNotationIn(waveforms.Named("first"), ("QQQQ", "8888", 0f));
        AssertNotationIn(waveforms.Named("second"), ("QQQQ", "0808", 0f));
    }

    /// <summary>A selected name missing from the Pool is a visible configuration error.</summary>
    [Test]
    public void Named_WithoutMatchingEntry_Throws()
    {
        var waveforms = CreateSeededWaveforms();

        Assert.Throws<InvalidOperationException>(() => waveforms.Named("no such entry"));
    }

    /// <summary>An empty Pool is a startup defect, never an implicit Beat Pulse.</summary>
    [Test]
    public void EmptyPool_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new Waveforms(new BeatManager(), Array.Empty<WaveformPool.Entry>()));
    }

    /// <summary>Named acquisition returns the first exact match when display names repeat.</summary>
    [Test]
    public void Named_DuplicateNames_ReturnsFirstMatch()
    {
        var waveforms = new Waveforms(new BeatManager(), new[]
        {
            Entry("same name", "QQQQ", "8888"),
            Entry("same name", "QQQQ", "8000"),
        });

        AssertNotationIn(waveforms.Named("same name"), ("QQQQ", "8888", 0f));
    }

    /// <summary>Caller mutation after construction cannot replace the values owned by Waveforms.</summary>
    [Test]
    public void Construction_SnapshotsPoolValuesBeforeCallerMutation()
    {
        var poolEntries = new[]
        {
            Entry("original", "QQQQ", "8888"),
        };
        var waveforms = new Waveforms(new BeatManager(), poolEntries);

        poolEntries[0] = Entry("replacement", "QQQQ", "8000");

        AssertNotationIn(waveforms.Random(), ("QQQQ", "8888", 0f));
    }

    /// <summary>A bound Waveform exposes its envelope and direct Lerp at the current Bar Phase.</summary>
    [Test]
    public void Waveform_PlaysAtCurrentBarPhase()
    {
        var onBeat = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        onBeat.Update(0f);
        var peak = Single(onBeat, "QQQQ", "8888");
        Assert.That(peak.Envelope, Is.EqualTo(1f).Within(Tol));
        Assert.That(peak.Lerp(0.5f, 1f), Is.EqualTo(1f).Within(Tol));

        var betweenBeats = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0.25f);
        betweenBeats.Update(0.25f);
        var trough = Single(betweenBeats, "QQQQ", "8888");
        Assert.That(trough.Envelope, Is.EqualTo(0f).Within(Tol));
        Assert.That(trough.Lerp(0.5f, 1f), Is.EqualTo(0.5f).Within(Tol));
    }

    /// <summary>Standalone playback rests Envelope at 0 while Lerp preserves its steady endpoint.</summary>
    [Test]
    public void Waveform_UsesNonNullStandaloneResponses()
    {
        var standalone = new BeatManager();
        standalone.Update(0f);
        var waveform = Single(standalone, "QQQQ", "8888");

        Assert.That(waveform.Envelope, Is.EqualTo(0f).Within(Tol));
        Assert.That(waveform.Lerp(0.5f, 1f), Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>The explicit suppression value is neutral for both response shapes.</summary>
    [Test]
    public void None_IsAnExplicitNeutralResponse()
    {
        var waveforms = CreateSeededWaveforms();

        Assert.That(waveforms.None.Envelope, Is.EqualTo(0f).Within(Tol));
        Assert.That(waveforms.None.Lerp(0.5f, 1f), Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>Using the CLR default fails at the caller instead of silently reading as a rest.</summary>
    [Test]
    public void DefaultWaveform_Throws()
    {
        var waveform = default(Waveform);

        Assert.Throws<InvalidOperationException>(() => _ = waveform.Envelope);
    }

    /// <summary>A parsed authoring value cannot silently masquerade as runtime playback.</summary>
    [Test]
    public void ParsedWaveform_PlaybackThrowsUntilAcquired()
    {
        var waveform = Waveform.Parse("QQQQ", "8888");

        Assert.Throws<InvalidOperationException>(() => _ = waveform.Envelope);
        Assert.That(waveform.Sample(0f), Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>HQQ/844 has a one-beat shortest gap, 500 ms at 120 BPM.</summary>
    [Test]
    public void ShortestPeakSpacingMs_FromWorkedNotation()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        beatManager.Update(0f);
        var waveform = Single(beatManager, "HQQ", "844");

        Assert.That(waveform.ShortestPeakSpacingMs, Is.EqualTo(500f).Within(Tol));
    }

    /// <summary>Peak spacing rests at 0 when no live tempo exists.</summary>
    [Test]
    public void ShortestPeakSpacingMs_RestsAtZeroWithoutTempo()
    {
        var waveform = Single(new BeatManager(), "QQQQ", "8888");

        Assert.That(waveform.ShortestPeakSpacingMs, Is.EqualTo(0f).Within(Tol));
    }

    /// <summary>Asserts one notation-derived Energy value.</summary>
    private static void AssertEnergy(string label, string sequence, string amplitude, float offset, Energy expected)
    {
        var waveform = Waveform.Parse(sequence, amplitude, Waveform.BeatPulseRounding, offset);
        Assert.That(waveform.Energy, Is.EqualTo(expected), $"{label} ({sequence}/{amplitude} @ {offset})");
    }

    /// <summary>Creates a runtime-bound Waveform from a one-entry Pool.</summary>
    private static Waveform Single(BeatManager beatManager, string sequence, string amplitude)
    {
        return new Waveforms(beatManager, new[] { Entry("test", sequence, amplitude) }).Random();
    }

    /// <summary>Creates one parsed Pool entry.</summary>
    private static WaveformPool.Entry Entry(string name, string sequence, string amplitude)
    {
        return new WaveformPool.Entry(name, Waveform.Parse(sequence, amplitude));
    }

    /// <summary>Creates the seed-seven acquisition surface without a live clock.</summary>
    private static Waveforms CreateSeededWaveforms()
    {
        return new Waveforms(new BeatManager(), SeedEntries());
    }

    /// <summary>Returns the seven canonical Pool entries.</summary>
    private static WaveformPool.Entry[] SeedEntries()
    {
        var rounding = Waveform.BeatPulseRounding;
        return new[]
        {
            new WaveformPool.Entry("beat pulse", Waveform.Parse("QQQQ", "8888", rounding, 0f)),
            new WaveformPool.Entry("beats 1 and 3", Waveform.Parse("QQQQ", "8080", rounding, 0f)),
            new WaveformPool.Entry("beats 2 and 4", Waveform.Parse("QQQQ", "0808", rounding, 0f)),
            new WaveformPool.Entry("measure start", Waveform.Parse("QQQQ", "8000", rounding, 0f)),
            new WaveformPool.Entry("beats 1 and 4", Waveform.Parse("QQQQ", "8008", rounding, 0f)),
            new WaveformPool.Entry("offbeat", Waveform.Parse("QQQQ", "8888", rounding, 0.5f)),
            new WaveformPool.Entry("every eighth", Waveform.Parse("EEEEEEEE", "88888888", rounding, 0f)),
        };
    }

    /// <summary>Asserts that a drawn Waveform belongs to an expected notation set.</summary>
    private static void AssertNotationIn(
        Waveform actual,
        params (string sequence, string amplitude, float offset)[] expected)
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
