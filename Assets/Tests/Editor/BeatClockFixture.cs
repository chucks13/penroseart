#nullable enable

using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Test-only deterministic beat-clock seam. Writes a known four-beat transport snapshot into a live-sourced
/// <see cref="BeatManager"/> so EditMode tests can pin BarPhase, beat pulse, offbeat derivation, and the
/// envelope/brightness path against exact values without Unity frame timing or a live OSC broadcast.
/// </summary>
/// <remarks>
/// This reproduces the transport a steady metronome at <c>bpm</c> would carry at <c>timeSeconds</c> — the same
/// fields live RaveSystem OSC pushes through <see cref="RaveOscReceiver.ApplyTo"/> — then feeds that snapshot
/// through <see cref="BeatManager.FeedWireSnapshot"/>. It replaces the removed production beat simulator:
/// the clock math was only exercised by tests, so it belongs in this explicit fixture rather than runtime code.
/// </remarks>
internal static class BeatClockFixture
{
    /// <summary>Common-time beat slots, matching the Penrose/Rave beat model.</summary>
    private const int BeatSlotCount = 4;

    /// <summary>
    /// Seeds <paramref name="beatManager"/> with the live transport a metronome at <paramref name="bpm"/> would
    /// carry at <paramref name="timeSeconds"/>, marks it as the live source, and returns it for chaining.
    /// </summary>
    /// <param name="beatManager">The manager to seed through its live transport input.</param>
    /// <param name="bpm">Beats per minute of the seeded clock.</param>
    /// <param name="timeSeconds">Wall-clock position used to roll the bar; <c>0</c> lands the downbeat of beat 1.</param>
    public static BeatManager SeedBeatClock(BeatManager beatManager, float bpm, float timeSeconds)
    {
        beatManager.FeedWireSnapshot(CreateSnapshot(bpm, timeSeconds));
        return beatManager;
    }

    /// <summary>Seeds a live beat clock whose current frame is inside an active Drop.</summary>
    /// <param name="beatManager">The manager to seed through its live transport input.</param>
    /// <param name="bpm">Beats per minute of the seeded clock.</param>
    /// <param name="timeSeconds">Wall-clock position used to roll the bar.</param>
    /// <param name="beatsRemaining">Beats remaining in the active Drop.</param>
    /// <param name="lengthBeats">Total length of the active Drop in beats.</param>
    /// <returns>The seeded manager for chaining.</returns>
    public static BeatManager SeedActiveDrop(
        BeatManager beatManager,
        float bpm,
        float timeSeconds,
        int beatsRemaining,
        int lengthBeats)
    {
        RaveWireSnapshot snapshot = CreateSnapshot(bpm, timeSeconds);
        snapshot.dropState = new CountdownState
        {
            active = 1,
            countBeats = beatsRemaining,
            lengthBeats = lengthBeats,
            remaining = 1,
        };
        beatManager.FeedWireSnapshot(snapshot);
        return beatManager;
    }

    /// <summary>Builds the deterministic live transport snapshot used by <see cref="SeedBeatClock"/>.</summary>
    public static RaveWireSnapshot CreateSnapshot(float bpm, float timeSeconds)
    {
        var beatDurationSeconds = 60f / bpm;
        var measureDurationSeconds = beatDurationSeconds * BeatSlotCount;
        var gateDurationSeconds = beatDurationSeconds * 0.25f;
        var positionSeconds = Mathf.Repeat(timeSeconds, measureDurationSeconds);
        var beatIndex = Mathf.FloorToInt(positionSeconds / beatDurationSeconds);
        var beatStartSeconds = beatIndex * beatDurationSeconds;
        var elapsedSinceBeatSeconds = positionSeconds - beatStartSeconds;
        var onBeat = elapsedSinceBeatSeconds < gateDurationSeconds;

        return new RaveWireSnapshot
        {
            bpm = bpm,
            beatInBar = beatIndex + 1,
            beatAverageMs = Mathf.RoundToInt(beatDurationSeconds * 1000f),
            beatPulse = Pulse(elapsedSinceBeatSeconds, beatDurationSeconds),
            beatsCountMs = BuildCountdowns(positionSeconds, beatDurationSeconds, onBeat, beatIndex),
            onBeats = BuildGates(onBeat, beatIndex),
        };
    }

    /// <summary>Builds a fresh live-sourced manager seeded by <see cref="SeedBeatClock"/>.</summary>
    public static BeatManager CreateSeeded(float bpm, float timeSeconds)
    {
        return SeedBeatClock(new BeatManager(), bpm, timeSeconds);
    }

    /// <summary>Builds label-ordered beat countdowns for the seeded clock.</summary>
    private static int[] BuildCountdowns(float positionSeconds, float beatDurationSeconds, bool currentGate, int currentIndex)
    {
        var measureDurationSeconds = beatDurationSeconds * BeatSlotCount;
        var countdowns = new int[BeatSlotCount];
        for (var i = 0; i < countdowns.Length; i++)
        {
            var slotSeconds = i * beatDurationSeconds;
            var deltaSeconds = slotSeconds - positionSeconds;
            if (i == currentIndex && currentGate)
            {
                deltaSeconds = 0f;
            }
            else if (deltaSeconds <= 0f)
            {
                deltaSeconds += measureDurationSeconds;
            }

            countdowns[i] = Mathf.RoundToInt(deltaSeconds * 1000f);
        }
        return countdowns;
    }

    /// <summary>Builds a four-slot gate array with only the current slot active when its gate is open.</summary>
    private static bool[] BuildGates(bool gateActive, int activeIndex)
    {
        var gates = new bool[BeatSlotCount];
        if (gateActive && activeIndex >= 0 && activeIndex < gates.Length)
        {
            gates[activeIndex] = true;
        }
        return gates;
    }

    /// <summary>Returns a smooth normalized pulse that is 1 at the event and decays toward 0 before the next event.</summary>
    private static float Pulse(float elapsedSeconds, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return 0f;
        }

        var progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
        return 1f - Mathf.SmoothStep(0f, 1f, progress);
    }
}
