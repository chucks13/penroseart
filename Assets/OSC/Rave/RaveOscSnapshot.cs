// Copyright © 2026 Hunter Luisi. All rights reserved.
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

    /// <summary>Levels value whose bands are all unavailable (-1 sentinels).</summary>
    public static Levels Unavailable => new Levels { low = -1f, mid = -1f, high = -1f };
}

/// <summary>
/// Current phrase of the on-air focus from <c>/rave/onair/phrase_state</c> (<c>siii</c>).
/// <c>countBeats</c> is beats remaining in the phrase; <c>lengthBeats</c> its total length.
/// </summary>
[Serializable]
public struct PhraseState {
    /// <summary>Phrase name; empty/null = unavailable.</summary>
    public string? label;

    public int countBeats;
    public int lengthBeats;

    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = phrase length not divisible by 16, <c>0</c> = regular,
    /// <c>-1</c> = unavailable. Do not collapse to a bool.
    /// </summary>
    public int irregular;

    /// <summary>Phrase state whose fields are all unavailable (-1 sentinels, empty label).</summary>
    public static PhraseState Unavailable =>
        new PhraseState { label = null, countBeats = -1, lengthBeats = -1, irregular = -1 };
}

/// <summary>
/// Labeled beat countdown shared by <c>/rave/onair/next_phrase_state</c>, <c>energy_state</c>, and
/// <c>next_energy_state</c> (<c>sii</c>). For "next" lanes <c>countBeats</c> counts down to the change and
/// <c>lengthBeats</c> is the upcoming run's own total length; for the current energy lane they describe the
/// current run.
/// </summary>
[Serializable]
public struct LabeledCountdown {
    /// <summary>Phrase name or energy level; empty/null = unavailable.</summary>
    public string? label;

    public int countBeats;
    public int lengthBeats;

    /// <summary>Labeled countdown whose fields are all unavailable (-1 sentinels, empty label).</summary>
    public static LabeledCountdown Unavailable =>
        new LabeledCountdown { label = null, countBeats = -1, lengthBeats = -1 };
}

/// <summary>
/// Loop state of the on-air focus from <c>/rave/onair/loop_state</c> (<c>iifiii</c>).
/// </summary>
[Serializable]
public struct LoopState {
    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = looping audio is rolling (looping and playing), <c>0</c> = not rolling,
    /// <c>-1</c> = unavailable. Do not collapse to a bool.
    /// </summary>
    public int active;

    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = a loop region exists on the deck (persists while paused),
    /// <c>0</c> = no region, <c>-1</c> = unavailable. Do not collapse to a bool.
    /// </summary>
    public int set;

    /// <summary>Loop region length in beats; fractional loops are real (a 1/2-beat loop is 0.5).</summary>
    public float lengthBeats;

    public int lengthMs;
    public int sizeNumerator;
    public int sizeDenominator;

    /// <summary>Loop state whose fields are all unavailable (-1 sentinels).</summary>
    public static LoopState Unavailable => new LoopState {
        active = -1, set = -1, lengthBeats = -1f, lengthMs = -1, sizeNumerator = -1, sizeDenominator = -1,
    };
}

/// <summary>
/// Source-computed cyclic timing grid of the on-air focus from <c>/rave/onair/timing_grid</c> (<c>iis</c>).
/// <c>beat</c> counts 1..16 with 1 as the One; <c>bar</c> is the 1..4 four-beat subdivision. Beat/bar can be
/// -1 with a live <c>state</c> when no beat is placeable yet; the full unavailable shape is -1 -1 "".
/// </summary>
[Serializable]
public struct TimingGrid {
    public int beat;
    public int bar;

    /// <summary>Grid-confidence vocabulary: "locked" / "coasting" / "disputed"; empty/null = unavailable.</summary>
    public string? state;

    /// <summary>Timing grid whose fields are all unavailable (-1 sentinels, empty state).</summary>
    public static TimingGrid Unavailable => new TimingGrid { beat = -1, bar = -1, state = null };
}

/// <summary>
/// Countdown beat state used by drop and fill projections.
/// Mirrors RaveSystem's track countdown state shape: tri-state active value, beat count, length, and remaining count.
/// </summary>
[Serializable]
public struct CountdownState {
    /// <summary>
    /// RaveSystem tri-state: <c>1</c> = active now, <c>0</c> = counting to the next occurrence,
    /// <c>-1</c> = unavailable. Mirrors <c>TrackCountdownState.Active</c>; do not collapse to a bool.
    /// </summary>
    public int active;

    public int countBeats;
    public int lengthBeats;
    public int remaining;

    /// <summary>Countdown state whose fields are all unavailable (-1 sentinels).</summary>
    public static CountdownState Unavailable =>
        new CountdownState { active = -1, countBeats = -1, lengthBeats = -1, remaining = -1 };
}

/// <summary>
/// Latest known RaveSystem OSC wire values decoded from UDP broadcasts.
/// The fields intentionally mirror the compact OSC payload groups.
/// </summary>
[Serializable]
public sealed class RaveWireSnapshot {
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
    public Levels levels = Levels.Unavailable;
    public PhraseState phraseState = PhraseState.Unavailable;
    public LabeledCountdown nextPhraseState = LabeledCountdown.Unavailable;
    public CountdownState dropState = CountdownState.Unavailable;
    public CountdownState fillState = CountdownState.Unavailable;
    public LabeledCountdown energyState = LabeledCountdown.Unavailable;
    public LabeledCountdown nextEnergyState = LabeledCountdown.Unavailable;
    public LoopState loopState = LoopState.Unavailable;
    public TimingGrid timingGrid = TimingGrid.Unavailable;
    public int trackId = -1;

    /// <summary>Creates a deep copy so background OSC updates cannot mutate a returned snapshot.</summary>
    /// <remarks>
    /// All scalar/struct fields copy via <see cref="object.MemberwiseClone"/>; only the two array fields are
    /// then re-copied so the clone is independent for thread-safety. Keeping the per-field list out of here
    /// is deliberate — it stops Clone from silently dropping a newly added scalar field.
    /// </remarks>
    public RaveWireSnapshot Clone() {
        var copy = (RaveWireSnapshot)MemberwiseClone();
        copy.beatsCountMs = CopyFour(beatsCountMs);
        copy.onBeats = CopyFour(onBeats);
        return copy;
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
        var phraseText = string.IsNullOrEmpty(phraseState.label) ? "?" : phraseState.label;
        return $"Rave OSC bpm={bpmText} beat={beatText} bar={barText} phrase={phraseText}";
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
