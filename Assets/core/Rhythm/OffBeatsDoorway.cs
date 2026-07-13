// The OffBeats doorway: the "&" cluster, the exact mirror of Beats (beat-data spec).

#nullable enable

using UnityEngine;

/// <summary>
/// The "&amp;" — the moment exactly midway between beats, four per bar, one Data Surface doorway.
/// The exact mirror of <see cref="BeatsView"/>: the wire carries nothing about the "&amp;", so
/// every offering here is contrived from measured beat-time midpoints. Facts are nullable; edges
/// are never null and rest at false.
/// </summary>
public readonly struct OffBeatsView
{
    private readonly GateLanes lanes;

    /// <summary>Next Off Beat: ms until the next "&amp;", whichever slot — the mirror of Next Beat.</summary>
    public float? NextOffBeatMs => lanes.SoonestMs;

    /// <summary>
    /// Off Beat: is the current slot's gate open — the same current-slot semantics as On Beat.
    /// </summary>
    public bool? OffBeat => lanes.CurrentGate;

    /// <summary>Edge: the current slot's gate opened this frame. Never null.</summary>
    public bool OffBeatOpened => lanes.CurrentOpened;

    /// <summary>Per-"&amp;" countdown: ms until the given slot (1..4) next lands; a landed slot holds at 0 while its gate is open.</summary>
    public float? MsUntil(int slot) => lanes.MsUntil(slot);

    /// <summary>Per-"&amp;" gate: is the given slot's window open.</summary>
    public bool? Gate(int slot) => lanes.Gate(slot);

    /// <summary>Edge: the given slot's gate opened this frame. Never null — every gate has an opening edge.</summary>
    public bool GateOpened(int slot) => lanes.GateOpened(slot);

    /// <summary>Built only by the hub's per-update capture over that frame's derived offbeat lanes.</summary>
    internal OffBeatsView(GateLanes lanes)
    {
        this.lanes = lanes;
    }
}

public partial class BeatManager
{
    /// <summary>Milliseconds until offbeat labels 1 through 4, derived from beat countdowns.</summary>
    private int[] offBeatsCountMs = CreateUnavailableCountdowns();

    /// <summary>Derived per-label offbeat gates.</summary>
    private bool[] offBeats = new bool[BeatSlotCount];

    /// <summary>Normalized pulse at the nearest offbeat, used by the Pulses doorway.</summary>
    private float offBeatPulse;

    /// <summary>
    /// The OffBeats doorway, captured once per hub update ahead of effect Draw — identical for
    /// every reader within a frame.
    /// </summary>
    public OffBeatsView OffBeats { get; private set; }

    /// <summary>
    /// Prior served per-slot offbeat gates, retained between hub updates for the opening edges
    /// (<see cref="OffBeatsView.OffBeatOpened"/> is the current slot's lane edge, so this is the
    /// only edge memory the doorway needs).
    /// </summary>
    private readonly bool?[] previousOffBeatGates = new bool?[BeatSlotCount];

    /// <summary>
    /// Captures the OffBeats doorway from the derived offbeat state. Unlike the wire arrays,
    /// <see cref="offBeatsCountMs"/> and <see cref="offBeats"/> are rebuilt fresh by
    /// <see cref="DeriveOffBeats"/> every update — nothing mutates them in place afterwards, so the
    /// capture can hold their references without copying.
    /// </summary>
    private OffBeatsView CaptureOffBeats()
    {
        var synced = IsSynced;
        var lanes = GateLanes.Capture(
            synced,
            CurrentCountOrNone(synced, beatData.snapshot.beatInBar),
            offBeatsCountMs,
            offBeats,
            previousOffBeatGates);
        return new OffBeatsView(lanes);
    }

    /// <summary>
    /// Derives each offbeat at the measured midpoint between adjacent beat labels, including its
    /// quarter-beat gate and normalized pulse.
    /// </summary>
    private void DeriveOffBeats()
    {
        var counts = CreateUnavailableCountdowns();
        var gates = new bool[BeatSlotCount];
        offBeatPulse = 0f;
        var snapshot = beatData.snapshot;
        if (!IsSynced || snapshot.beatAverageMs <= 0 ||
            snapshot.beatsCountMs == null || snapshot.beatsCountMs.Length < BeatSlotCount)
        {
            offBeatsCountMs = counts;
            offBeats = gates;
            return;
        }

        var activeWindowMs = snapshot.beatAverageMs * 0.25f;
        var measureMs = snapshot.beatAverageMs * (float)BeatSlotCount;
        var nearestOffBeatMs = float.MaxValue;
        for (var i = 0; i < counts.Length; i++)
        {
            var nextBeatIndex = (i + 1) % counts.Length;
            var startBeatMs = snapshot.beatsCountMs[i];
            var nextBeatMs = snapshot.beatsCountMs[nextBeatIndex];
            if (startBeatMs < 0 || nextBeatMs < 0)
            {
                continue;
            }

            var beatGapMs = (float)(nextBeatMs - startBeatMs);
            if (beatGapMs <= 0f)
            {
                beatGapMs += measureMs;
            }

            var halfGapMs = beatGapMs * 0.5f;
            var offBeatMs = nextBeatMs - halfGapMs;
            if (offBeatMs < 0f)
            {
                offBeatMs += measureMs;
            }
            nearestOffBeatMs = Mathf.Min(nearestOffBeatMs, offBeatMs);

            if (nextBeatMs > halfGapMs)
            {
                counts[i] = Mathf.RoundToInt(offBeatMs);
                continue;
            }

            var elapsedMs = halfGapMs - nextBeatMs;
            if (elapsedMs <= activeWindowMs)
            {
                counts[i] = 0;
                gates[i] = true;
            }
            else
            {
                counts[i] = Mathf.RoundToInt(measureMs - elapsedMs);
            }
        }

        if (nearestOffBeatMs != float.MaxValue)
        {
            var nextInCycleMs = nearestOffBeatMs % snapshot.beatAverageMs;
            var elapsedMs = nextInCycleMs <= 0f ? 0f : snapshot.beatAverageMs - nextInCycleMs;
            offBeatPulse = OffBeatPulse(elapsedMs, snapshot.beatAverageMs);
        }

        offBeatsCountMs = counts;
        offBeats = gates;
    }

    /// <summary>Returns a smooth pulse that peaks at the offbeat and decays until the next one.</summary>
    private static float OffBeatPulse(float elapsedMs, float durationMs)
    {
        if (durationMs <= 0f)
        {
            return 0f;
        }

        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsedMs / durationMs));
    }
}
