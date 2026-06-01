// RaveSystem OSC client state for PenroseArt.

#nullable enable

using System;

namespace PenroseArt.RaveOsc {

/// <summary>Focused on-air beat position from <c>/rave/onair/beat</c> and <c>/rave/onair/total_beats</c>.</summary>
[Serializable]
public struct BeatPosition {
    public int current;
    public int total;
}

/// <summary>Focused on-air bar position from <c>/rave/onair/bar</c> and <c>/rave/onair/next_bar_ms</c>.</summary>
[Serializable]
public struct BarPosition {
    public int current;
    public int nextMs;
}

/// <summary>Low/mid/high normalized waveform energy values from <c>/rave/onair/levels</c>.</summary>
[Serializable]
public struct Levels {
    public float low;
    public float mid;
    public float high;
}

/// <summary>
/// Current/next named beat state used by phase and energy projections.
/// Mirrors RaveSystem's track phase state shape: labels, active flag, beat count, length, and remaining count.
/// </summary>
[Serializable]
public struct PhaseState {
    public string? current;
    public string? next;
    public bool active;
    public int countBeats;
    public int lengthBeats;
    public int remaining;
}

/// <summary>
/// Countdown beat state used by drop and fill projections.
/// Mirrors RaveSystem's track countdown state shape: active flag, beat count, length, and remaining count.
/// </summary>
[Serializable]
public struct CountdownState {
    public bool active;
    public int countBeats;
    public int lengthBeats;
    public int remaining;
}

/// <summary>
/// Latest known RaveSystem on-air OSC values decoded from UDP broadcasts.
/// The fields intentionally mirror the compact OSC payload groups.
/// </summary>
[Serializable]
public sealed class RaveOnAirSnapshot {
    public string playersLive = "";
    public string track = "";
    public float bpm = -1f;
    public BeatPosition beat = new BeatPosition { current = -1, total = -1 };
    public BarPosition bar = new BarPosition { current = -1, nextMs = -1 };
    public int beatInBar = -1;
    public int[] beatsCountMs = new[] { -1, -1, -1, -1 };
    public bool[] onBeats = new bool[4];
    public int beatAverageMs = -1;
    public float beatPulse;
    public Levels levels = new Levels { low = -1f, mid = -1f, high = -1f };
    public PhaseState phaseState;
    public CountdownState dropState;
    public CountdownState fillState;
    public PhaseState energyState;

    /// <summary>Creates a deep copy so background OSC updates cannot mutate a returned snapshot.</summary>
    public RaveOnAirSnapshot Clone() {
        return new RaveOnAirSnapshot {
            playersLive = playersLive,
            track = track,
            bpm = bpm,
            beat = beat,
            bar = bar,
            beatInBar = beatInBar,
            beatsCountMs = CopyFour(beatsCountMs),
            onBeats = CopyFour(onBeats),
            beatAverageMs = beatAverageMs,
            beatPulse = beatPulse,
            levels = levels,
            phaseState = phaseState,
            dropState = dropState,
            fillState = fillState,
            energyState = energyState,
        };
    }

    /// <summary>Returns the smallest non-negative beat countdown, or <paramref name="fallback" /> when unavailable.</summary>
    public int NextBeatMs(int fallback = -1) {
        var result = int.MaxValue;
        for (var i = 0; i < beatsCountMs.Length; i++) {
            var value = beatsCountMs[i];
            if (value >= 0 && value < result) {
                result = value;
            }
        }
        return result == int.MaxValue ? fallback : result;
    }

    /// <summary>Returns the on-beat gate for a 1-based beat-in-bar label.</summary>
    public bool IsOnBeat(int beatInBar) {
        var index = beatInBar - 1;
        return index >= 0 && index < onBeats.Length && onBeats[index];
    }

    public override string ToString() {
        var bpmText = bpm > 0f ? bpm.ToString("0.##") : "?";
        var beatText = beat.current >= 0 ? beat.current.ToString() : "?";
        var barText = bar.current >= 0 ? bar.current.ToString() : "?";
        var phaseText = string.IsNullOrEmpty(phaseState.current) ? "?" : phaseState.current;
        return $"Rave OSC bpm={bpmText} beat={beatText} bar={barText} phase={phaseText}";
    }

    private static int[] CopyFour(int[] source) {
        var copy = new int[4];
        if (source != null) {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    private static bool[] CopyFour(bool[] source) {
        var copy = new bool[4];
        if (source != null) {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }
}

}
