// Caller-facing tests for held-value Routine playback.

using NUnit.Framework;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Proves direct four-Waveform composition and the Routine response API against known Grid states.
/// </summary>
public sealed class WaveformsRoutineTests
{
    private const float Tol = 0.0001f;

    /// <summary>The Grid bar selects one of four directly composed resolved Waveforms.</summary>
    [Test]
    public void Routine_PlaysTheWaveformAtTheCurrentGridBar()
    {
        var beatManager = CreateBeatManager();
        var routine = PinnedRoutine(beatManager, 0.25f, 0.5f, 0.75f, 1f);

        Assert.That(EnvelopeAt(beatManager, routine, beat: 1, bar: 1, timeSeconds: 0f),
            Is.EqualTo(0.25f).Within(Tol), "bar 1 downbeat selects bar 1");
        Assert.That(EnvelopeAt(beatManager, routine, beat: 5, bar: 2, timeSeconds: 0.25f),
            Is.EqualTo(0f).Within(Tol), "halfway between beats is the selected Waveform's trough");
        Assert.That(EnvelopeAt(beatManager, routine, beat: 9, bar: 3, timeSeconds: 0f),
            Is.EqualTo(0.75f).Within(Tol), "bar 3 downbeat selects bar 3");
        Assert.That(EnvelopeAt(beatManager, routine, beat: 13, bar: 4, timeSeconds: 0f),
            Is.EqualTo(1f).Within(Tol), "bar 4 downbeat selects bar 4");
    }

    /// <summary>Routine reads are observational and do not manufacture Grid events.</summary>
    [Test]
    public void Routine_RepeatedReadsArePureAndDoNotManufactureGridEvents()
    {
        var beatManager = CreateBeatManager();
        var routine = PinnedRoutine(beatManager, 0.25f, 0.5f, 0.75f, 1f);
        PlaceGrid(beatManager, beat: 16, bar: 4, state: "locked", timeSeconds: 0f);
        PlaceGrid(beatManager, beat: 12, bar: 3, state: "locked", timeSeconds: 0f);

        var first = routine.Envelope;
        var second = routine.Envelope;

        Assert.That(typeof(GridValues).GetProperty("Wrapped"), Is.Null);
        Assert.That(first, Is.EqualTo(0.75f).Within(Tol));
        Assert.That(second, Is.EqualTo(first));
    }

    /// <summary>Missing Grid placement has total responses, while confidence never gates placement.</summary>
    [Test]
    public void Routine_UsesNonNullStandaloneResponsesAndIgnoresGridConfidence()
    {
        var standalone = new BeatManager();
        var standaloneRoutine = PinnedRoutine(standalone, 1f, 1f, 1f, 1f);
        Assert.That(standaloneRoutine.Envelope, Is.EqualTo(0f).Within(Tol));
        Assert.That(standaloneRoutine.Lerp(0.5f, 1f), Is.EqualTo(1f).Within(Tol));

        var beatManager = CreateBeatManager();
        var routine = PinnedRoutine(beatManager, 1f, 1f, 1f, 1f);
        PlaceGrid(beatManager, beat: -1, bar: -1, state: "coasting", timeSeconds: 0f);
        Assert.That(routine.Envelope, Is.EqualTo(0f).Within(Tol), "partial unplaced Grid");
        Assert.That(routine.Lerp(0.5f, 1f), Is.EqualTo(1f).Within(Tol));

        Assert.That(EnvelopeAt(beatManager, routine,
            beat: 1, bar: 1, timeSeconds: 0f, state: "disputed"), Is.EqualTo(1f).Within(Tol));
    }

    /// <summary>Creates one seeded live BeatManager.</summary>
    private static BeatManager CreateBeatManager()
    {
        var beatManager = BeatClockFixture.CreateSeeded(bpm: 120f, timeSeconds: 0f);
        beatManager.Update(0f);
        return beatManager;
    }

    /// <summary>Places one Grid observation and reads the Routine envelope.</summary>
    private static float EnvelopeAt(
        BeatManager beatManager,
        Routine routine,
        int beat,
        int bar,
        float timeSeconds,
        string state = "locked")
    {
        PlaceGrid(beatManager, beat, bar, state, timeSeconds);
        return routine.Envelope;
    }

    /// <summary>Seeds the shared clock and places timing-grid facts.</summary>
    private static void PlaceGrid(
        BeatManager beatManager,
        int beat,
        int bar,
        string state,
        float timeSeconds)
    {
        var snapshot = BeatClockFixture.CreateSnapshot(bpm: 120f, timeSeconds: timeSeconds);
        snapshot.timingGrid = new TimingGrid { beat = beat, bar = bar, state = state };
        beatManager.FeedWireSnapshot(snapshot);
        beatManager.Update(timeSeconds);
    }

    /// <summary>Composes four runtime-bound quarter-note Waveforms with known peak heights.</summary>
    private static Routine PinnedRoutine(
        BeatManager beatManager,
        float first,
        float second,
        float third,
        float fourth)
    {
        return Routine.Of(
            BoundPulse(beatManager, first),
            BoundPulse(beatManager, second),
            BoundPulse(beatManager, third),
            BoundPulse(beatManager, fourth));
    }

    /// <summary>Builds one bound quarter-note Waveform with a constant normalized peak height.</summary>
    private static Waveform BoundPulse(BeatManager beatManager, float amplitude)
    {
        var digit = Mathf.RoundToInt(amplitude * 8f).ToString();
        var entry = new WaveformPool.Entry(
            "test",
            Waveform.Parse("QQQQ", digit + digit + digit + digit));
        return new Waveforms(beatManager, new[] { entry }).Random();
    }
}
