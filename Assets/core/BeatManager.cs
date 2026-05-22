using UnityEngine;
using System;

/// <summary>
/// Mutable beat-clock state shared with effects through <see cref="BeatManager"/>.
/// </summary>
/// <remarks>
/// This is not currently driven by external OSC or MIDI. <see cref="BeatManager.Update"/>
/// derives the beat position from Unity's <see cref="Time.time"/> and the configured BPM.
/// </remarks>
[Serializable]
public class BeatData
{
    /// <summary>
    /// Master switch for beat-reactive behavior. When false, BeatManager helpers
    /// return non-pulsing values and effects should render normally.
    /// </summary>
    public bool active = true;

    /// <summary>Tempo in beats per minute for the simulated beat clock.</summary>
    public float bpm = 120.0f;

    /// <summary>
    /// Milliseconds to the nearest beat event. Positive means milliseconds since
    /// the previous beat; negative means milliseconds until the next beat.
    /// </summary>
    public int timeEvent;

    /// <summary>Number of beats in the repeating measure.</summary>
    public int beatsPerMeasure = 4;

    /// <summary>Zero-based beat index inside the current measure.</summary>
    public int currentBeat;
}

/// <summary>
/// Simulated beat clock and beat-reactive helper methods used by effects.
/// </summary>
/// <remarks>
/// Controller owns one BeatManager and calls <see cref="Update"/> once per frame.
/// Effects usually receive a random beat variant from <see cref="EffectBase.OnStart"/>
/// and then call <see cref="GetBeatBrightness"/> or <see cref="GetBeatTime"/> while drawing.
/// </remarks>
[Serializable]
public class BeatManager
{
    /// <summary>Current simulated beat state.</summary>
    public BeatData beatData = new BeatData();

    /// <summary>Last absolute beat number seen by <see cref="Update"/>.</summary>
    private int _lastTotalBeats = -1;

    /// <summary>True for only the first frame after the simulated beat index changes.</summary>
    private bool _isBeatTriggered = false;

    /// <summary>Shorthand for whether the shared beat clock is active.</summary>
    public bool IsActive => beatData.active;

    /// <summary>
    /// Advances the simulated beat clock from Unity time.
    /// </summary>
    /// <remarks>
    /// The beat clock is deterministic from <see cref="Time.time"/> and BPM. It does
    /// not accumulate delta time, so changing BPM immediately changes the computed
    /// phase. <see cref="BeatData.timeEvent"/> is signed so effects can know whether
    /// the closest beat is behind or ahead.
    /// </remarks>
    public void Update()
    {
        if (!beatData.active || beatData.bpm <= 0) return;

        float secondsPerBeat = 60f / beatData.bpm;
        float totalTime = Time.time;

        // Detect if this frame is the start of a new beat.
        int totalBeats = Mathf.FloorToInt(totalTime / secondsPerBeat);
        _isBeatTriggered = (totalBeats != _lastTotalBeats);
        _lastTotalBeats = totalBeats;

        // Calculate current beat in the measure.
        beatData.currentBeat = totalBeats % beatData.beatsPerMeasure;

        // Calculate time within the current beat cycle.
        float cycleTime = totalTime % secondsPerBeat;
        float msSince = cycleTime * 1000f;
        float msUntil = (secondsPerBeat - cycleTime) * 1000f;

        // Positive = since last beat, negative = until next beat.
        if (msSince <= msUntil)
            beatData.timeEvent = (int)msSince;
        else
            beatData.timeEvent = -(int)msUntil;
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

        float msPerBeat = 60000f / Mathf.Max(beatData.bpm, 1f);

        // Standard normalized distance: 0 at the beat, 1 at the midpoint between beats.
        float dist = Mathf.Abs(beatData.timeEvent);
        float normDist = Mathf.Clamp01(dist / (msPerBeat * 0.5f));

        // Higher-frequency variants reuse the same nearest-beat distance inside
        // shorter subdivisions.
        float msPer8th = msPerBeat * 0.5f;
        float norm8thDist = Mathf.Clamp01((dist % msPer8th) / (msPer8th * 0.5f));
        float msPer16th = msPerBeat * 0.25f;
        float norm16thDist = Mathf.Clamp01((dist % msPer16th) / (msPer16th * 0.5f));

        // Use a high power (x^4) to keep brightness high for most of the beat.
        // This prevents the "too dark" feeling while still providing a sharp rhythmic kick.
        float standardPulse = Mathf.Lerp(maxBrightness, minBrightness, Mathf.Pow(normDist, 4.0f));
        float doublePulse = Mathf.Lerp(maxBrightness, minBrightness, Mathf.Pow(norm8thDist, 4.0f));
        float quadPulse = Mathf.Lerp(maxBrightness, minBrightness, Mathf.Pow(norm16thDist, 4.0f));

        switch (variant)
        {
            case 1: // Beats 1 & 3
                return (beatData.currentBeat == 0 || beatData.currentBeat == 2) ? standardPulse : maxBrightness;
            case 2: // Beats 2 & 4
                return (beatData.currentBeat == 1 || beatData.currentBeat == 3) ? standardPulse : maxBrightness;
            case 3: // Measure Start (Beat 1)
                return (beatData.currentBeat == 0) ? standardPulse : maxBrightness;
            case 5: // 8th Notes
                return doublePulse;
            case 6: // 16th Notes
                return quadPulse;
            case 4: // Syncopated (1 and 4)
                return (beatData.currentBeat == 0 || beatData.currentBeat == 3) ? standardPulse : maxBrightness;
            case 0: // Every Beat
            default:
                return standardPulse;
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
        if (!beatData.active || !_isBeatTriggered) return false;

        if (variant == 1 && beatData.currentBeat % 2 != 0) return false;
        if (variant == 2 && beatData.currentBeat % 2 == 0) return false;
        if (variant == 3 && beatData.currentBeat != 0) return false;

        return true;
    }
}
