// Derives four Off Beat lanes from the measured On Beat timing.

#nullable enable

using UnityEngine;

/// <summary>Immutable tempo-derived Off Beat values captured for one BeatManager update.</summary>
public readonly struct OffbeatsValues
{
    /// <summary>The captured four-lane storage.</summary>
    private readonly OnBeatLanes lanes;

    /// <summary>Creates one public Off Beat reading from captured lane storage.</summary>
    internal OffbeatsValues(OnBeatLanes lanes)
    {
        this.lanes = lanes;
    }

    /// <summary>Milliseconds until the selected 1..4 Off Beat next lands.</summary>
    public int? OffBeatMs(int count) => lanes.Milliseconds(count);

    /// <summary>Whether the selected 1..4 Off Beat is inside its quarter-interval window.</summary>
    public bool? OffBeat(int count) => lanes.Active(count);
}

public partial class BeatManager
{
    /// <summary>Latest derived milliseconds-until values in count order.</summary>
    private int[] offBeatsCountMs = CreateUnavailableCountdowns();
    /// <summary>Latest derived active-window values in count order.</summary>
    private bool[] offBeats = new bool[BeatSlotCount];
    /// <summary>Latest derived continuous Off Beat pulse.</summary>
    private float offBeatPulse;

    /// <summary>The four tempo-derived Off Beat countdowns and trigger windows.</summary>
    public OffbeatsValues Offbeats { get; private set; }

    /// <summary>Captures the settled derived Off Beat lanes for public reading.</summary>
    private OffbeatsValues CaptureOffbeats()
    {
        return new OffbeatsValues(new OnBeatLanes(offBeatsCountMs, offBeats));
    }

    /// <summary>Derives the four Off Beat countdowns, windows, and pulse from the measured On Beats.</summary>
    private void DeriveOffBeats()
    {
        var counts = CreateUnavailableCountdowns();
        var active = new bool[BeatSlotCount];
        offBeatPulse = 0f;
        var snapshot = wireSnapshot;
        if (!IsSynced || snapshot.beatAverageMs <= 0 || snapshot.beatsCountMs == null ||
            snapshot.beatsCountMs.Length < BeatSlotCount)
        {
            offBeatsCountMs = counts;
            offBeats = active;
            return;
        }

        var activeWindowMs = snapshot.beatAverageMs * 0.25f;
        var measureMs = snapshot.beatAverageMs * (float)BeatSlotCount;
        var nearestOffBeatMs = float.MaxValue;
        for (var slot = 0; slot < counts.Length; slot++)
        {
            var nextBeatSlot = (slot + 1) % counts.Length;
            float startBeatMs = snapshot.beatsCountMs[slot];
            float nextBeatMs = snapshot.beatsCountMs[nextBeatSlot];
            if (startBeatMs < 0 || nextBeatMs < 0)
            {
                continue;
            }

            var beatGapMs = nextBeatMs - startBeatMs;
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
                counts[slot] = Mathf.RoundToInt(offBeatMs);
                continue;
            }

            var elapsedMs = halfGapMs - nextBeatMs;
            if (elapsedMs <= activeWindowMs)
            {
                counts[slot] = 0;
                active[slot] = true;
            }
            else
            {
                counts[slot] = Mathf.RoundToInt(measureMs - elapsedMs);
            }
        }

        if (nearestOffBeatMs != float.MaxValue)
        {
            var nextInCycleMs = nearestOffBeatMs % snapshot.beatAverageMs;
            var elapsedMs = nextInCycleMs <= 0f ? 0f : snapshot.beatAverageMs - nextInCycleMs;
            offBeatPulse = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsedMs / snapshot.beatAverageMs));
        }

        offBeatsCountMs = counts;
        offBeats = active;
    }
}
