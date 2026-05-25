// RaveSystem OSC client state for PenroseArt.

#nullable enable

using System;

namespace PenroseArt.RaveOsc {

/// <summary>Focused on-air beat position from <c>/rave/onair/beat</c>.</summary>
[Serializable]
public struct RaveBeatPosition {
    public int current;
    public int total;
}

/// <summary>Focused on-air bar position from <c>/rave/onair/bar</c>.</summary>
[Serializable]
public struct RaveBarPosition {
    public int current;
    public int nextMs;
}

/// <summary>Low/mid/high normalized waveform energy values from <c>/rave/onair/levels</c>.</summary>
[Serializable]
public struct RaveLevels {
    public float low;
    public float mid;
    public float high;
}

/// <summary>Grouped RaveSystem phase or energy run state.</summary>
[Serializable]
public struct RaveNamedState {
    public string? current;
    public string? next;
    public bool active;
    public int countBeats;
    public int lengthBeats;
    public int remaining;
}

/// <summary>Grouped RaveSystem countdown state for drop and fill regions.</summary>
[Serializable]
public struct RaveCountdownState {
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
    public float bpm;
    public RaveBeatPosition beat;
    public RaveBarPosition bar;
    public int beatInBar;
    public int[] beatsCountMs = new int[4];
    public bool[] onBeats = new bool[4];
    public int beatAverageMs;
    public float beatPulse;
    public RaveLevels levels;
    public RaveNamedState phaseState;
    public RaveCountdownState dropState;
    public RaveCountdownState fillState;
    public RaveNamedState energyState;

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
