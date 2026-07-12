// Seam-2 tests for Routine choreography through the Waveform Synthesizer public surface.

#nullable enable

using System.Text.RegularExpressions;
using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Pins ticket 19's Routine contract at the Waveform Synthesizer seam: explicit caller-owned
/// acquisition, immutable resolved values, observational Grid reads, hub-owned wrap identity,
/// name-pinned settings, and Hold reach-through without rewriting the Routine.
/// </summary>
public sealed class WaveformSynthRoutineTests
{
    /// <summary>Worked-value tolerance for Waveform envelope assertions.</summary>
    private const float Tol = 0.0001f;

    /// <summary>A random acquisition resolves every Routine bar within the requested Energy set.</summary>
    [Test]
    public void RandomRoutine_ResolvesEverySlotWithinTheRequestedEnergySet()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());

        var values = EvaluateBars(beatManager, synth, synth.RandomRoutine(Energy.Low, Energy.High));

        Assert.That(values.Item1, Is.EqualTo(0.25f).Or.EqualTo(1f));
        Assert.That(values.Item2, Is.EqualTo(0.25f).Or.EqualTo(1f));
        Assert.That(values.Item3, Is.EqualTo(0.25f).Or.EqualTo(1f));
        Assert.That(values.Item4, Is.EqualTo(0.25f).Or.EqualTo(1f));
    }

    /// <summary>An empty Energy match logs and resolves every bar from the whole-Pool fallback.</summary>
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

    /// <summary>Explicit authored acquisition resolves draw, Preset-name, inline, and silence settings.</summary>
    [Test]
    public void CreateRoutine_ResolvesEveryAuthoredAcquisitionPath()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());

        var routine = RequireRoutine(synth.CreateRoutine(
            RoutineSlot.Pin("mid"),
            RoutineSlot.Draw(Energy.High),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "0000")),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "8888"))));

        Assert.That(EvaluateBars(beatManager, synth, routine), Is.EqualTo((0.5f, 1f, 0f, 1f)));
    }

    /// <summary>A missing pinned Preset fails acquisition before an unresolved Routine can be held.</summary>
    [Test]
    public void CreateRoutine_MissingPresetReadsNull()
    {
        var (_, synth) = CreateSynth(EnergyEntries());

        var routine = synth.CreateRoutine(
            RoutineSlot.Pin("missing"),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "8888")),
            RoutineSlot.Draw(Energy.Low),
            RoutineSlot.Draw(Energy.High));

        Assert.That(routine, Is.Null);
    }

    /// <summary>The Grid bar selects one of four directly composed resolved Waveforms.</summary>
    [Test]
    public void Evaluate_UsesKnownGridPositions()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        var routine = PinnedRoutine(0.25f, 0.5f, 0.75f, 1f);

        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 1, bar: 1, timeSeconds: 0f),
            Is.EqualTo(0.25f).Within(Tol), "bar 1 downbeat selects bar 1");
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 5, bar: 2, timeSeconds: 0.25f),
            Is.EqualTo(0f).Within(Tol), "halfway between beats is the selected Waveform's trough");
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 9, bar: 3, timeSeconds: 0f),
            Is.EqualTo(0.75f).Within(Tol), "bar 3 downbeat selects bar 3");
        Assert.That(EvaluateAt(beatManager, synth, routine, beat: 13, bar: 4, timeSeconds: 0f),
            Is.EqualTo(1f).Within(Tol), "bar 4 downbeat selects bar 4");
    }

    /// <summary>Repeated reads are observational and never infer a private Grid wrap or replacement.</summary>
    [Test]
    public void Evaluate_RepeatedReadsArePureAndGridWrapIdentityStaysHubOwned()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        var routine = PinnedRoutine(0.25f, 0.5f, 0.75f, 1f);
        PlaceGrid(beatManager, synth, beat: 16, bar: 4, state: "locked", timeSeconds: 0f);
        PlaceGrid(beatManager, synth, beat: 12, bar: 3, state: "locked", timeSeconds: 0f);

        var first = synth.Evaluate(routine);
        var second = synth.Evaluate(routine);

        Assert.That(beatManager.Grid.Wrapped, Is.False, "a backward non-One position is not a wrap");
        Assert.That(first, Is.EqualTo(0.75f).Within(Tol));
        Assert.That(second, Is.EqualTo(first));
    }

    /// <summary>A live Routine read requires Grid placement but never gates on Grid confidence.</summary>
    [Test]
    public void Evaluate_UsesGridPlacementButNotGridConfidence()
    {
        var routine = PinnedRoutine(1f, 1f, 1f, 1f);
        var noClock = new WaveformSynth(new BeatManager(), EnergyEntries());
        Assert.That(noClock.Evaluate(routine), Is.Null, "no clock");

        var (beatManager, synth) = CreateSynth(EnergyEntries());
        PlaceGrid(beatManager, synth, beat: -1, bar: -1, state: "coasting", timeSeconds: 0f);
        Assert.That(synth.Evaluate(routine), Is.Null, "partial unplaced Grid");

        Assert.That(EvaluateAt(beatManager, synth, routine,
            beat: 1, bar: 1, timeSeconds: 0f, state: "disputed"), Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>The caller alone chooses whether and when to repeat acquisition with its settings.</summary>
    [Test]
    public void CallerChoosesWhenToAcquireAReplacementRoutine()
    {
        Random.InitState(19);
        var (beatManager, synth) = CreateSynth(TwoMidEntries());
        var bar1 = RoutineSlot.Draw(Energy.Mid);
        var bar2 = RoutineSlot.Pin("quiet mid");
        var bar3 = RoutineSlot.Draw(Energy.Mid);
        var bar4 = RoutineSlot.Pin(Waveform.Parse("QQQQ", "8888"));
        var original = RequireRoutine(synth.CreateRoutine(bar1, bar2, bar3, bar4));

        PlaceGrid(beatManager, synth, beat: 16, bar: 4, state: "locked", timeSeconds: 0f);
        PlaceGrid(beatManager, synth, beat: 1, bar: 1, state: "locked", timeSeconds: 0f);
        Assert.That(beatManager.Grid.Wrapped, Is.True);
        var originalAtWrap = synth.Evaluate(original);
        Assert.That(synth.Evaluate(original), Is.EqualTo(originalAtWrap),
            "the provider reports the wrap but never replaces the held value");

        var replacement = RequireRoutine(synth.CreateRoutine(bar1, bar2, bar3, bar4));

        Assert.That(replacement, Is.Not.SameAs(original));
        Assert.That(synth.Evaluate(original), Is.EqualTo(originalAtWrap),
            "caller acquisition cannot mutate the previously held Routine");
    }

    /// <summary>Hold substitutes at evaluation while acquisition still preserves genuine resolved values.</summary>
    [Test]
    public void Hold_ReachesThroughRoutineAndReleaseRestoresItsValues()
    {
        var (beatManager, synth) = CreateSynth(EnergyEntries());
        synth.Hold(Waveform.Parse("QQQQ", "8888"));

        var routine = RequireRoutine(synth.CreateRoutine(
            RoutineSlot.Draw(Energy.Low),
            RoutineSlot.Pin("mid"),
            RoutineSlot.Draw(Energy.Low),
            RoutineSlot.Pin(Waveform.Parse("QQQQ", "4444"))));
        Assert.That(EvaluateBars(beatManager, synth, routine),
            Is.EqualTo((1f, 1f, 1f, 1f)), "Hold substitutes during every Routine read");

        synth.ReleaseToAuto();
        Assert.That(EvaluateBars(beatManager, synth, routine),
            Is.EqualTo((0.25f, 0.5f, 0.25f, 0.5f)),
            "acquisition under Hold preserved the Routine's genuine resolved values");
    }

    /// <summary>Requires a successful authored acquisition and returns its resolved value.</summary>
    private static Routine RequireRoutine(Routine? routine)
    {
        Assert.That(routine, Is.Not.Null, "the authored settings resolve");
        return routine!;
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

    /// <summary>Seeds the shared clock, places timing-grid facts, then steps the hub and synth.</summary>
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

    /// <summary>Pool with two observable choices in the same Mid tier.</summary>
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
