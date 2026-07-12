// Seam-2 tests for Routine choreography through the Waveform Synthesizer public surface.

#nullable enable

using System.Text.RegularExpressions;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins ticket 19's Routine behavior at the Waveform Synthesizer seam: four authored or drawn
/// bars, Grid-position evaluation, holder-chosen rerolls, per-Routine wrap refresh, and the
/// Waveform Hold reaching through without replacing the Routine's own values.
/// </summary>
public sealed class WaveformSynthRoutineTests
{
    /// <summary>Worked-value tolerance for Waveform envelope assertions.</summary>
    private const float Tol = 0.0001f;

    /// <summary>A rolled Routine draws each of its four slots only from the requested Energy set.</summary>
    [Test]
    public void RandomRoutine_DrawsEverySlotWithinTheRequestedEnergySet()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());

        var values = EvaluateBars(beatManager, synth, synth.RandomRoutine(Energy.Low, Energy.High));

        Assert.That(values.Item1, Is.EqualTo(0.25f).Or.EqualTo(1f));
        Assert.That(values.Item2, Is.EqualTo(0.25f).Or.EqualTo(1f));
        Assert.That(values.Item3, Is.EqualTo(0.25f).Or.EqualTo(1f));
        Assert.That(values.Item4, Is.EqualTo(0.25f).Or.EqualTo(1f));
    }

    /// <summary>An empty Energy match logs and falls back to draws from the whole Pool for every slot.</summary>
    [Test]
    public void RandomRoutine_EmptyEnergyMatchFallsBackToTheWholePool()
    {
        var entries = new[]
        {
            Entry("low", "QQQQ", "2000"),
            Entry("mid", "QQQQ", "4444"),
        };
        var (beatManager, synth) = CreateSynth(entries);
        for (var i = 0; i < 4; i++)
        {
            LogAssert.Expect(LogType.Warning, new Regex(
                @"^\[WaveformSynth\] no Pool entry matches the requested Energy set \(High\) — drawing from the whole Pool\.$"));
        }

        var values = EvaluateBars(beatManager, synth, synth.RandomRoutine(Energy.High));

        Assert.That(values.Item1, Is.EqualTo(0.25f).Or.EqualTo(0.5f));
        Assert.That(values.Item2, Is.EqualTo(0.25f).Or.EqualTo(0.5f));
        Assert.That(values.Item3, Is.EqualTo(0.25f).Or.EqualTo(0.5f));
        Assert.That(values.Item4, Is.EqualTo(0.25f).Or.EqualTo(0.5f));
    }

    /// <summary>Authors can compose pinned and Energy-drawn bars slot by slot, including plain silence.</summary>
    [Test]
    public void AuthoredRoutine_MixesPinnedDrawnAndSilentSlots()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        var routine = new Routine(
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "4444")),
            RoutineSlot.Draw(Energy.High),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "0000")),
            RoutineSlot.Draw(Energy.Low));

        Assert.That(EvaluateBars(beatManager, synth, routine),
            Is.EqualTo((0.5f, 1f, 0f, 0.25f)));
    }

    /// <summary>The Grid's bar selects the slot and its within-bar fraction evaluates that Waveform.</summary>
    [Test]
    public void Evaluate_UsesTheGridBarAndBarFraction()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        var routine = PinnedRoutine(0.25f, 0.5f, 0.75f, 1f);

        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 1, bar: 1, timeSeconds: 0f),
            Is.EqualTo(0.25f).Within(Tol), "bar 1 downbeat selects slot 1");
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 5, bar: 2, timeSeconds: 0.25f),
            Is.EqualTo(0f).Within(Tol), "halfway between beats is the selected slot's trough");
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 9, bar: 3, timeSeconds: 0f),
            Is.EqualTo(0.75f).Within(Tol), "bar 3 downbeat selects slot 3");
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 13, bar: 4, timeSeconds: 0f),
            Is.EqualTo(1f).Within(Tol), "bar 4 downbeat selects slot 4");
    }

    /// <summary>A Routine live read is null both without a clock and with a clock that places no Grid.</summary>
    [Test]
    public void Evaluate_ReadsNullWithoutAClockOrPlacedGrid()
    {
        var routine = PinnedRoutine(1f, 1f, 1f, 1f);
        var noClock = new WaveformSynth(new BeatManager(), EnergyEntries());
        Assert.That(noClock.Evaluate(routine), Is.Null, "no clock");

        var clockOnly = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        clockOnly.Update(0f);
        Assert.That(new WaveformSynth(clockOnly, EnergyEntries()).Evaluate(routine), Is.Null, "no Grid");
    }

    /// <summary>The partial Coasting shape has trust data but no position, so Routine evaluation is null.</summary>
    [Test]
    public void Evaluate_PartialCoastingGridReadsNull()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        PlaceGrid(beatManager, synth, beat: -1, bar: -1, state: "coasting", timeSeconds: 0f);

        Assert.That(synth.Evaluate(PinnedRoutine(1f, 1f, 1f, 1f)), Is.Null);
    }

    /// <summary>Grid State is served trust data, never a gate: a placed Disputed Grid still evaluates.</summary>
    [Test]
    public void Evaluate_DisputedGridStillEvaluates()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());

        var value = EvaluateAt(beatManager, synth, PinnedRoutine(1f, 1f, 1f, 1f),
            beat: 1, bar: 1, timeSeconds: 0f, state: "disputed");

        Assert.That(value, Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>A backward Grid position wraps the Routine from its fourth slot to its first.</summary>
    [Test]
    public void Evaluate_BackwardGridStepWrapsToSlotOne()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        var routine = PinnedRoutine(0.25f, 0.5f, 0.75f, 1f);
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 16, bar: 4, timeSeconds: 0f),
            Is.EqualTo(1f).Within(Tol));

        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 1, bar: 1, timeSeconds: 0f),
            Is.EqualTo(0.25f).Within(Tol));
    }

    /// <summary>Explicit reroll replaces draw slots while an author-pinned slot stays unchanged.</summary>
    [Test]
    public void Reroll_RedrawsDrawSlotsAndLeavesPinnedSlotsUntouched()
    {
        var seed = FindObservableAuthoredRerollSeed();
        Random.InitState(seed);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        var routine = new Routine(
            RoutineSlot.Draw(Energy.Mid),
            RoutineSlot.Draw(Energy.Mid),
            RoutineSlot.Draw(Energy.Mid),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "8888")));
        var before = EvaluateBars(beatManager, synth, routine);

        synth.Reroll(routine);
        var after = EvaluateBars(beatManager, synth, routine);

        Assert.That((after.Item1, after.Item2, after.Item3),
            Is.Not.EqualTo((before.Item1, before.Item2, before.Item3)), "the seeded redraw changes a draw slot");
        Assert.That(after.Item4, Is.EqualTo(1f).Within(Tol), "the pinned slot survives");
    }

    /// <summary>With refresh disabled, draw caches survive a witnessed Grid wrap unchanged.</summary>
    [Test]
    public void RefreshOnWrap_FalseKeepsDrawsAcrossTheWrap()
    {
        Random.InitState(3232);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        var routine = synth.RandomRoutine(Energy.Mid);
        var before = EvaluateBars(beatManager, synth, routine);
        EvaluateAt(beatManager, synth, routine, beat: 16, bar: 4, timeSeconds: 0f);
        EvaluateAt(beatManager, synth, routine, beat: 1, bar: 1, timeSeconds: 0f);

        Assert.That(EvaluateBars(beatManager, synth, routine), Is.EqualTo(before));
    }

    /// <summary>
    /// One witnessed wrap with refresh enabled is observationally identical to exactly one public
    /// reroll, including when Evaluate is called twice at the wrapped position.
    /// </summary>
    [Test]
    public void RefreshOnWrap_TrueRerollsExactlyOncePerWitnessedWrap()
    {
        var seed = FindObservableRandomRoutineRerollSeed();
        var automatic = RunAutomaticRefresh(seed);
        var manual = RunSingleManualRerollAtSecondBeat(seed);

        Assert.That(automatic.After, Is.Not.EqualTo(automatic.Before),
            "the deterministic seed makes the refresh observable");
        Assert.That(automatic.After, Is.EqualTo(manual),
            "the wrap performs exactly one reroll despite two reads at the wrapped position");
    }

    /// <summary>
    /// The Hold overrides drawn and pinned slots at evaluation, then release restores genuine
    /// caches; even a Routine acquired under Hold resolves its draws independently of the Hold.
    /// </summary>
    [Test]
    public void Hold_ReachesThroughEverySlotWithoutPoisoningRoutineValues()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        var routine = new Routine(
            RoutineSlot.Draw(Energy.Low),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "4444")),
            RoutineSlot.Draw(Energy.Low),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "4444")));
        Assert.That(EvaluateBars(beatManager, synth, routine),
            Is.EqualTo((0.25f, 0.5f, 0.25f, 0.5f)), "genuine values before Hold");

        synth.Hold(Waveform.Parse("QQQQ", "8888"));
        var acquiredUnderHold = synth.RandomRoutine(Energy.Low);
        Assert.That(EvaluateBars(beatManager, synth, routine),
            Is.EqualTo((1f, 1f, 1f, 1f)), "Hold reaches drawn and pinned slots alike");

        synth.ReleaseToAuto();
        Assert.That(EvaluateBars(beatManager, synth, routine),
            Is.EqualTo((0.25f, 0.5f, 0.25f, 0.5f)), "pre-Hold caches return");
        Assert.That(EvaluateBars(beatManager, synth, acquiredUnderHold),
            Is.EqualTo((0.25f, 0.25f, 0.25f, 0.25f)), "draws acquired under Hold were genuine");
    }

    /// <summary>
    /// Runs one opt-in automatic refresh after another Routine observes the wrap first, proving
    /// the draw holder's own sparse observations witness the backward step.
    /// </summary>
    private static ((float, float, float, float) Before, (float, float, float, float) After)
        RunAutomaticRefresh(int seed)
    {
        Random.InitState(seed);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        var routine = synth.RandomRoutine(Energy.Mid);
        routine.RefreshOnWrap = true;
        var before = EvaluateBars(beatManager, synth, routine);
        EvaluateAt(beatManager, synth, routine, beat: 16, bar: 4, timeSeconds: 0f);
        EvaluateAt(beatManager, synth, PinnedRoutine(1f, 1f, 1f, 1f),
            beat: 1, bar: 1, timeSeconds: 0f);
        EvaluateAt(beatManager, synth, routine, beat: 2, bar: 1, timeSeconds: 0f);
        EvaluateAt(beatManager, synth, routine, beat: 2, bar: 1, timeSeconds: 0f);
        return (before, EvaluateBarsAtSecondBeat(beatManager, synth, routine));
    }

    /// <summary>Replays the same seeded acquisition followed by exactly one explicit reroll.</summary>
    private static (float, float, float, float) RunSingleManualReroll(int seed)
    {
        Random.InitState(seed);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        var routine = synth.RandomRoutine(Energy.Mid);
        synth.Reroll(routine);
        return EvaluateBars(beatManager, synth, routine);
    }

    /// <summary>Replays the same seeded acquisition, one reroll, and second-beat observations.</summary>
    private static (float, float, float, float) RunSingleManualRerollAtSecondBeat(int seed)
    {
        Random.InitState(seed);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        var routine = synth.RandomRoutine(Energy.Mid);
        synth.Reroll(routine);
        return EvaluateBarsAtSecondBeat(beatManager, synth, routine);
    }

    /// <summary>Finds a deterministic Unity random seed whose authored draw slots visibly change on reroll.</summary>
    private static int FindObservableAuthoredRerollSeed()
    {
        for (var seed = 0; seed < 128; seed++)
        {
            Random.InitState(seed);
            var (beatManager, synth) = CreateSynth(TwoMidEntries());
            var routine = new Routine(
                RoutineSlot.Draw(Energy.Mid),
                RoutineSlot.Draw(Energy.Mid),
                RoutineSlot.Draw(Energy.Mid),
                RoutineSlot.Pin(Waveform.Parse("QQQQ", "8888")));
            var before = EvaluateBars(beatManager, synth, routine);
            synth.Reroll(routine);
            var after = EvaluateBars(beatManager, synth, routine);
            if ((before.Item1, before.Item2, before.Item3) != (after.Item1, after.Item2, after.Item3))
            {
                return seed;
            }
        }

        throw new AssertionException("No seed in the deterministic search made an authored reroll observable.");
    }

    /// <summary>Finds a deterministic Unity random seed whose four-slot RandomRoutine visibly changes on reroll.</summary>
    private static int FindObservableRandomRoutineRerollSeed()
    {
        for (var seed = 0; seed < 128; seed++)
        {
            var before = RunWithoutReroll(seed);
            var after = RunSingleManualReroll(seed);
            if (before != after)
            {
                return seed;
            }
        }

        throw new AssertionException("No seed in the deterministic search made a RandomRoutine reroll observable.");
    }

    /// <summary>Replays one seeded RandomRoutine acquisition without any subsequent reroll.</summary>
    private static (float, float, float, float) RunWithoutReroll(int seed)
    {
        Random.InitState(seed);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        return EvaluateBars(beatManager, synth, synth.RandomRoutine(Energy.Mid));
    }

    /// <summary>Builds a live BeatManager and synth over caller-provided Pool entries.</summary>
    private static (BeatManager, WaveformSynth) CreateSynth(WaveformPool.Entry[] entries)
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        beatManager.Update(0f);
        return (beatManager, new WaveformSynth(beatManager, entries));
    }

    /// <summary>Evaluates the four bars on their downbeats and returns their public envelope values.</summary>
    private static (float, float, float, float) EvaluateBars(
        BeatManager beatManager,
        WaveformSynth synth,
        Routine routine)
    {
        return (
            EvaluateAt(beatManager, synth, routine, beat: 1, bar: 1, timeSeconds: 0f),
            EvaluateAt(beatManager, synth, routine, beat: 5, bar: 2, timeSeconds: 0f),
            EvaluateAt(beatManager, synth, routine, beat: 9, bar: 3, timeSeconds: 0f),
            EvaluateAt(beatManager, synth, routine, beat: 13, bar: 4, timeSeconds: 0f));
    }

    /// <summary>Evaluates each bar on its second beat, preserving the same peak-height observations.</summary>
    private static (float, float, float, float) EvaluateBarsAtSecondBeat(
        BeatManager beatManager,
        WaveformSynth synth,
        Routine routine)
    {
        return (
            EvaluateAt(beatManager, synth, routine, beat: 2, bar: 1, timeSeconds: 0f),
            EvaluateAt(beatManager, synth, routine, beat: 6, bar: 2, timeSeconds: 0f),
            EvaluateAt(beatManager, synth, routine, beat: 10, bar: 3, timeSeconds: 0f),
            EvaluateAt(beatManager, synth, routine, beat: 14, bar: 4, timeSeconds: 0f));
    }

    /// <summary>Places one Grid observation and reads the Routine envelope through the synth.</summary>
    private static float EvaluateAt(
        BeatManager beatManager,
        WaveformSynth synth,
        Routine routine,
        int beat,
        int bar,
        float timeSeconds,
        string state = "locked")
    {
        PlaceGrid(beatManager, synth, beat, bar, state, timeSeconds);
        var value = synth.Evaluate(routine);
        Assert.That(value, Is.Not.Null, "the placed Grid has a Routine value");
        return value!.Value;
    }

    /// <summary>Seeds the shared clock, places timing_grid facts, then steps the hub and synth.</summary>
    private static void PlaceGrid(
        BeatManager beatManager,
        WaveformSynth synth,
        int beat,
        int bar,
        string state,
        float timeSeconds)
    {
        BeatClockFixture.SeedBeatClock(beatManager, bpm: 120f, timeSeconds: timeSeconds);
        beatManager.WireSnapshot.timingGrid = new TimingGrid { beat = beat, bar = bar, state = state };
        beatManager.Update(timeSeconds);
        synth.Update();
    }

    /// <summary>Creates four pinned quarter-note Waveforms with the requested normalized peak heights.</summary>
    private static Routine PinnedRoutine(float first, float second, float third, float fourth)
    {
        return new Routine(
            RoutineSlot.Pin(ConstantPulse(first)),
            RoutineSlot.Pin(ConstantPulse(second)),
            RoutineSlot.Pin(ConstantPulse(third)),
            RoutineSlot.Pin(ConstantPulse(fourth)));
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

    /// <summary>Pool with two observable choices in the same Mid tier for deterministic reroll tests.</summary>
    private static WaveformPool.Entry[] TwoMidEntries()
    {
        return new[]
        {
            Entry("quiet mid", "QQQQ", "2222"),
            Entry("loud mid", "QQQQ", "4444"),
        };
    }

    /// <summary>Creates one named Pool entry from worked notation.</summary>
    private static WaveformPool.Entry Entry(string name, string sequence, string amplitude)
    {
        return new WaveformPool.Entry(name, Waveform.Parse(sequence, amplitude));
    }
}
