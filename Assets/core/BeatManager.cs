using UnityEngine;
using System;
using PenroseArt.RaveOsc;

/// <summary>
/// Mutable Rave on-air beat and phrase state shared with effects through <see cref="BeatManager"/>.
/// </summary>
/// <remarks>
/// Live values come from RaveSystem OSC. Defaults are inert fallback values used before OSC is available.
/// </remarks>
[Serializable]
public class BeatData
{
    /// <summary>
    /// Master switch for beat-reactive behavior. When false, BeatManager helpers
    /// return non-pulsing values and effects should render normally.
    /// </summary>
    public bool active;

    /// <summary>CSV of live on-air player numbers ordered newest on-air first.</summary>
    public string playersLive = "";

    /// <summary>Focused on-air track display text.</summary>
    public string track = "";

    /// <summary>Focused on-air effective tempo in beats per minute.</summary>
    public float bpm;

    /// <summary>Focused on-air beat position from /rave/onair/beat.</summary>
    public RaveBeatPosition beat;

    /// <summary>Focused on-air bar position from /rave/onair/bar.</summary>
    public RaveBarPosition bar;

    /// <summary>Focused on-air 1-based beat label inside the current bar.</summary>
    public int beatInBar;

    /// <summary>Milliseconds until beat labels 1 through 4.</summary>
    public int[] beatsCountMs = new int[4];

    /// <summary>Beat-label gates for beat labels 1 through 4.</summary>
    public bool[] onBeats = new bool[4];

    /// <summary>Milliseconds until offbeat labels 1 through 4, derived from OSC beat countdowns.</summary>
    public int[] offBeatsCountMs = new[] { -1, -1, -1, -1 };

    /// <summary>Offbeat-label gates for offbeats after beat labels 1 through 4.</summary>
    public bool[] offBeats = new bool[4];

    /// <summary>Normalized offbeat pulse: 1 on the offbeat, decaying toward 0 until the next offbeat.</summary>
    public float offBeatPulse;

    /// <summary>Average beat duration in milliseconds across live players with usable timing.</summary>
    public int beatAverageMs;

    /// <summary>Normalized OSC beat pulse: 1 on the beat, decaying toward 0.</summary>
    public float beatPulse;

    /// <summary>Average low/mid/high waveform energy across live players.</summary>
    public RaveLevels levels;

    /// <summary>Grouped phase state for the focused on-air track.</summary>
    public RaveNamedState phaseState;

    /// <summary>Grouped drop countdown state for the focused on-air track.</summary>
    public RaveCountdownState dropState;

    /// <summary>Grouped fill countdown state for the focused on-air track.</summary>
    public RaveCountdownState fillState;

    /// <summary>Grouped energy-run state for the focused on-air track.</summary>
    public RaveNamedState energyState;

    /// <summary>Milliseconds until the nearest upcoming beat label, read from <see cref="beatsCountMs"/>.</summary>
    public int nextBeatMs => ReadIntAt(beatsCountMs, IndexOfSmallestNonNegative(beatsCountMs));

    /// <summary>True while the nearest upcoming beat label gate is active, read from <see cref="onBeats"/>.</summary>
    public bool onBeat => ReadBoolAt(onBeats, IndexOfSmallestNonNegative(beatsCountMs));

    /// <summary>Milliseconds until the nearest upcoming offbeat, read from <see cref="offBeatsCountMs"/>.</summary>
    public int nextOffBeatMs => ReadIntAt(offBeatsCountMs, IndexOfSmallestNonNegative(offBeatsCountMs));

    /// <summary>True while the nearest upcoming offbeat is active, read from <see cref="offBeats"/>.</summary>
    public bool offBeat => ReadBoolAt(offBeats, IndexOfSmallestNonNegative(offBeatsCountMs));

    /// <summary>Number of beats in the repeating measure.</summary>
    public int beatsPerMeasure = 4;

    /// <summary>Zero-based beat index inside the current measure.</summary>
    public int currentBeat;

    /// <summary>Copies the latest OSC-shaped on-air snapshot into this application beat model.</summary>
    public void CopyFrom(RaveOnAirSnapshot snapshot)
    {
        playersLive = snapshot.playersLive ?? "";
        track = snapshot.track ?? "";
        bpm = snapshot.bpm;
        beat = snapshot.beat;
        bar = snapshot.bar;
        beatInBar = snapshot.beatInBar;
        beatsCountMs = CopyFour(snapshot.beatsCountMs);
        onBeats = CopyFour(snapshot.onBeats);
        beatAverageMs = snapshot.beatAverageMs;
        beatPulse = snapshot.beatPulse;
        levels = snapshot.levels;
        phaseState = snapshot.phaseState;
        dropState = snapshot.dropState;
        fillState = snapshot.fillState;
        energyState = snapshot.energyState;
    }

    /// <summary>Returns the smallest non-negative beat countdown, or <paramref name="fallback"/> when unavailable.</summary>
    public int GetNextBeatMs(int fallback = -1)
    {
        var result = int.MaxValue;
        for (var i = 0; i < beatsCountMs.Length; i++)
        {
            var value = beatsCountMs[i];
            if (value >= 0 && value < result)
            {
                result = value;
            }
        }
        return result == int.MaxValue ? fallback : result;
    }

    /// <summary>Returns the on-beat gate for a 1-based beat-in-bar label.</summary>
    public bool IsOnBeat(int beatInBar)
    {
        var index = beatInBar - 1;
        return index >= 0 && index < onBeats.Length && onBeats[index];
    }

    private static int IndexOfSmallestNonNegative(int[] source)
    {
        if (source == null)
        {
            return -1;
        }

        var resultIndex = -1;
        var resultValue = int.MaxValue;
        for (var i = 0; i < source.Length; i++)
        {
            var value = source[i];
            if (value >= 0 && value < resultValue)
            {
                resultIndex = i;
                resultValue = value;
            }
        }
        return resultIndex;
    }

    private static int ReadIntAt(int[] source, int index)
    {
        return source != null && index >= 0 && index < source.Length ? source[index] : -1;
    }

    private static bool ReadBoolAt(bool[] source, int index)
    {
        return source != null && index >= 0 && index < source.Length && source[index];
    }

    private static int[] CopyFour(int[] source)
    {
        var copy = new int[4];
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    private static bool[] CopyFour(bool[] source)
    {
        var copy = new bool[4];
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }
}

/// <summary>
/// Rave OSC beat state container and beat-reactive helper methods used by effects.
/// </summary>
/// <remarks>
/// Controller owns one BeatManager and calls <see cref="Update"/> once per frame.
/// Effects usually receive a random beat variant from <see cref="EffectBase.OnStart"/>
/// and then call <see cref="GetBeatBrightness"/> or <see cref="GetBeatTime"/> while drawing.
/// </remarks>
[Serializable]
public class BeatManager
{
    /// <summary>Current beat state. Defaults are inert until OSC supplies live data.</summary>
    public BeatData beatData = new BeatData();

    /// <summary>Shorthand for whether the shared beat clock is active.</summary>
    public bool IsActive => beatData.active;

    /// <summary>
    /// Reserved update hook. Beat data is populated by OSC, not by a local Unity-time clock.
    /// </summary>
    public void Update()
    {
    }

    /// <summary>
    /// Returns a random full beat-variant index for effect activation.
    /// </summary>
    public int GetRandomVariant()
    {
        return UnityEngine.Random.Range(0, 7);
    }

    /// <summary>
    /// Returns a random lower-intensity beat variant. Current range excludes the
    /// 8th-note and 16th-note variants used by <see cref="GetBeatBrightness"/>.
    /// </summary>
    public int GetRandomVariantChill()
    {
        return UnityEngine.Random.Range(0, 5);
    }

    /// <summary>
    /// Calculates a beat-synced brightness multiplier.
    /// </summary>
    /// <remarks>
    /// The returned pulse is highest on the selected beat and decays toward
    /// <paramref name="minBrightness"/> halfway between beats. The x^4 curve keeps
    /// most of the beat bright while still creating a sharp rhythmic kick.
    ///
    /// Current variant mapping in code:
    /// - 0: every beat
    /// - 1: beats 1 and 3
    /// - 2: beats 2 and 4
    /// - 3: measure start / beat 1
    /// - 4: syncopated beats 1 and 4
    /// - 5: 8th notes
    /// - 6: 16th notes
    /// </remarks>
    /// <param name="variant">Beat variant selector using the mapping above.</param>
    /// <param name="maxBrightness">Brightness value on selected beats.</param>
    /// <param name="minBrightness">Brightness value furthest from selected beats.</param>
    /// <param name="enable">If false, returns <paramref name="maxBrightness"/> with no pulsing.</param>
    /// <returns>A brightness multiplier between <paramref name="minBrightness"/> and <paramref name="maxBrightness"/>.</returns>
    public float GetBeatBrightness(int variant, float maxBrightness = 1.0f, float minBrightness = 0.85f, bool enable = true)
    {
        if (!enable || !beatData.active) return maxBrightness;

        // RaveSystem provides the beat pulse directly over OSC. Do not synthesize a second local envelope here.
        float beatPulse = Mathf.Lerp(minBrightness, maxBrightness, Mathf.Clamp01(beatData.beatPulse));
        float offBeatPulse = Mathf.Lerp(minBrightness, maxBrightness, Mathf.Clamp01(beatData.offBeatPulse));
        float eighthNotePulse = Mathf.Lerp(minBrightness, maxBrightness, Mathf.Max(Mathf.Clamp01(beatData.beatPulse), Mathf.Clamp01(beatData.offBeatPulse)));

        switch (variant)
        {
            case 0: // Every Beat
                return beatPulse;
            case 1: // Beats 1 & 3
                return (beatData.currentBeat == 0 || beatData.currentBeat == 2) ? beatPulse : maxBrightness;
            case 2: // Beats 2 & 4
                return (beatData.currentBeat == 1 || beatData.currentBeat == 3) ? beatPulse : maxBrightness;
            case 3: // Measure Start (Beat 1)
                return (beatData.currentBeat == 0) ? beatPulse : maxBrightness;
            case 4: // Syncopated (1 and 4)
                return (beatData.currentBeat == 0 || beatData.currentBeat == 3) ? beatPulse : maxBrightness;
            case 5: // Offbeat Pulse
                return offBeatPulse;
            case 6: // Eighth Notes
                return eighthNotePulse;
            default:
                return beatPulse;
        }
    }

    /// <summary>
    /// Warps an effect time value forward on the beat.
    /// </summary>
    /// <remarks>
    /// Effects use this to make procedural motion kick or surge rhythmically
    /// without permanently changing their stored <c>effectTime</c>.
    /// </remarks>
    public float GetBeatTime(int variant, float currentTime, float intensity = 0.2f)
    {
        if (!beatData.active)
            return currentTime;

        // Pulse is 1.0 on the beat and approaches 0.0 between beats.
        float pulse = GetBeatBrightness(variant, 1.0f, 0.0f);

        // Surge the local time value forward on the beat.
        return currentTime + (pulse * intensity);
    }

    /// <summary>
    /// Returns true only on the first frame of a beat allowed by the variant.
    /// </summary>
    /// <remarks>
    /// This helper is for one-shot behavior such as palette changes or spawning.
    /// It currently gates variants 1, 2, and 3 explicitly. Variants 4, 5, and 6
    /// fall through as every-beat triggers here even though GetBeatBrightness has
    /// distinct pulse behavior for them.
    /// </remarks>
    public bool IsBeatTriggered(int variant)
    {
        if (!beatData.active || !beatData.onBeat) return false;

        if (variant == 1 && beatData.currentBeat % 2 != 0) return false;
        if (variant == 2 && beatData.currentBeat % 2 == 0) return false;
        if (variant == 3 && beatData.currentBeat != 0) return false;

        return true;
    }
}
