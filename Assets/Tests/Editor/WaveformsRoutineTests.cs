// Seam-2 tests for Routine choreography through the Waveforms public surface.

#nullable enable

using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Pins the Routine contract at the Waveforms seam: direct four-Waveform composition,
/// observational Grid reads, and hub-owned wrap identity.
/// </summary>
public sealed class WaveformsRoutineTests
{
    /// <summary>Worked-value tolerance for Waveform envelope assertions.</summary>
    private const float Tol = 0.0001f;

    /// <summary>The Grid bar selects one of four directly composed resolved Waveforms.</summary>
    [Test]
    public void Evaluate_UsesKnownGridPositions()
    {
        var (beatManager, waveforms) = CreateWaveforms(EnergyEntries());
        var routine = PinnedRoutine(0.25f, 0.5f, 0.75f, 1f);

        Assert.That(EvaluateAt(beatManager, waveforms, routine, beat: 1, bar: 1, timeSeconds: 0f),
            Is.EqualTo(0.25f).Within(Tol), "bar 1 downbeat selects bar 1");
        Assert.That(EvaluateAt(beatManager, waveforms, routine, beat: 5, bar: 2, timeSeconds: 0.25f),
            Is.EqualTo(0f).Within(Tol), "halfway between beats is the selected Waveform's trough");
        Assert.That(EvaluateAt(beatManager, waveforms, routine, beat: 9, bar: 3, timeSeconds: 0f),
            Is.EqualTo(0.75f).Within(Tol), "bar 3 downbeat selects bar 3");
        Assert.That(EvaluateAt(beatManager, waveforms, routine, beat: 13, bar: 4, timeSeconds: 0f),
            Is.EqualTo(1f).Within(Tol), "bar 4 downbeat selects bar 4");
    }

    /// <summary>Repeated reads are observational and never infer a private Grid wrap or replacement.</summary>
    [Test]
    public void Evaluate_RepeatedReadsArePureAndGridWrapIdentityStaysHubOwned()
    {
        var (beatManager, waveforms) = CreateWaveforms(EnergyEntries());
        var routine = PinnedRoutine(0.25f, 0.5f, 0.75f, 1f);
        PlaceGrid(beatManager, waveforms, beat: 16, bar: 4, state: "locked", timeSeconds: 0f);
        PlaceGrid(beatManager, waveforms, beat: 12, bar: 3, state: "locked", timeSeconds: 0f);

        var first = waveforms.Evaluate(routine);
        var second = waveforms.Evaluate(routine);

        Assert.That(beatManager.Grid.Wrapped, Is.False, "a backward non-One position is not a wrap");
        Assert.That(first, Is.EqualTo(0.75f).Within(Tol));
        Assert.That(second, Is.EqualTo(first));
    }

    /// <summary>A live Routine read requires Grid placement but never gates on Grid confidence.</summary>
    [Test]
    public void Evaluate_UsesGridPlacementButNotGridConfidence()
    {
        var routine = PinnedRoutine(1f, 1f, 1f, 1f);
        var noClock = new Waveforms(new BeatManager(), EnergyEntries());
        Assert.That(noClock.Evaluate(routine), Is.Null, "no clock");

        var (beatManager, waveforms) = CreateWaveforms(EnergyEntries());
        PlaceGrid(beatManager, waveforms, beat: -1, bar: -1, state: "coasting", timeSeconds: 0f);
        Assert.That(waveforms.Evaluate(routine), Is.Null, "partial unplaced Grid");

        Assert.That(EvaluateAt(beatManager, waveforms, routine,
            beat: 1, bar: 1, timeSeconds: 0f, state: "disputed"), Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>Builds a live BeatManager and Waveforms instance over caller-provided Pool entries.</summary>
    private static (BeatManager, Waveforms) CreateWaveforms(WaveformPool.Entry[] entries)
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        beatManager.Update(0f);
        return (beatManager, new Waveforms(beatManager, entries));
    }

    /// <summary>Places one Grid observation and reads the Routine envelope through Waveforms.</summary>
    private static float EvaluateAt(
        BeatManager beatManager,
        Waveforms waveforms,
        Routine routine,
        int beat,
        int bar,
        float timeSeconds,
        string state = "locked")
    {
        PlaceGrid(beatManager, waveforms, beat, bar, state, timeSeconds);
        var value = waveforms.Evaluate(routine);
        Assert.That(value, Is.Not.Null, "the placed Grid has a Routine value");
        return value!.Value;
    }

    /// <summary>Seeds the shared clock, places timing-grid facts, then steps the hub and Waveforms.</summary>
    private static void PlaceGrid(
        BeatManager beatManager,
        Waveforms waveforms,
        int beat,
        int bar,
        string state,
        float timeSeconds)
    {
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: timeSeconds);
        beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = beat, bar = bar, state = state };
        beatManager.Update(timeSeconds);
        waveforms.Update();
    }

    /// <summary>Composes four resolved quarter-note Waveforms with known normalized peak heights.</summary>
    private static Routine PinnedRoutine(float first, float second, float third, float fourth)
    {
        return Routine.Of(
            ConstantPulse(first),
            ConstantPulse(second),
            ConstantPulse(third),
            ConstantPulse(fourth));
    }

    /// <summary>Builds a quarter-note Waveform whose four peaks share one eighth-step amplitude.</summary>
    private static Waveform ConstantPulse(float amplitude)
    {
        var digit = Mathf.RoundToInt(amplitude * 8f).ToString();
        return Waveform.Parse("QQQQ", digit + digit + digit + digit);
    }

    /// <summary>Pool with one verifiable entry in each Energy tier.</summary>
    private static WaveformPool.Entry[] EnergyEntries()
    {
        return new[]
        {
            Entry("low", "QQQQ", "2000"),
            Entry("mid", "QQQQ", "4444"),
            Entry("high", "EEEEEEEE", "88888888"),
        };
    }

    /// <summary>Creates one named Pool entry from worked notation.</summary>
    private static WaveformPool.Entry Entry(string name, string sequence, string amplitude)
    {
        return new WaveformPool.Entry(name, Waveform.Parse(sequence, amplitude));
    }
}
