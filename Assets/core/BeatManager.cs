using UnityEngine;
using System;

[Serializable]
public class BeatData
{
    public bool active = true;
    public float bpm = 120.0f;
    public int timeEvent; // ms to next beat (neg) or ms since last beat (pos)
    public int beatsPerMeasure = 4;
    public int currentBeat; // 0-indexed
}

[Serializable]
public class BeatManager
{
    public BeatData beatData = new BeatData();
    private int _lastTotalBeats = -1;
    private bool _isBeatTriggered = false;

    public void Update()
    {
        if (!beatData.active || beatData.bpm <= 0) return;

        float secondsPerBeat = 60f / beatData.bpm;
        float totalTime = Time.time;

        // Detect if this frame is the start of a new beat
        int totalBeats = Mathf.FloorToInt(totalTime / secondsPerBeat);
        _isBeatTriggered = (totalBeats != _lastTotalBeats);
        _lastTotalBeats = totalBeats;

        // Calculate current beat in the measure
        beatData.currentBeat = totalBeats % beatData.beatsPerMeasure;

        // Calculate time within the current beat cycle
        float cycleTime = totalTime % secondsPerBeat;
        float msSince = cycleTime * 1000f;
        float msUntil = (secondsPerBeat - cycleTime) * 1000f;

        // Positive = since last beat, Negative = until next beat
        if (msSince <= msUntil)
            beatData.timeEvent = (int)msSince;
        else
            beatData.timeEvent = -(int)msUntil;
    }

    /// <summary>
    /// Returns a random beat variant index.
    /// </summary>
    public int GetRandomVariant()
    {
        return UnityEngine.Random.Range(0, 4);
    }

    /// <summary>
    /// Calculates a beat-synced brightness multiplier.
    /// Max brightness (e.g., 1.0) on the beat, decaying to min brightness (e.g., 0.5) halfway between beats.
    /// </summary>
    /// <param name="variant">The rhythm variant: 0=All, 1=Even, 2=Odd, 3=Measure Start</param>
    /// <param name="maxBrightness">The brightness value on the beat.</param>
    /// <param name="minBrightness">The brightness value furthest from the beat.</param>
    /// <param name="enable">If false, returns maxBrightness (no pulsing).</param>
    /// <returns>A float between minBrightness and maxBrightness.</returns>
    public float GetBeatBrightness(int variant, float maxBrightness = 1.0f, float minBrightness = 0.5f, bool enable = true)
    {
        if (!enable || !beatData.active) return maxBrightness;

        float msPerBeat = 60000f / Mathf.Max(beatData.bpm, 1f);
        float normalizedDist = Mathf.Abs(beatData.timeEvent) / (msPerBeat * 0.5f);
        float pulse = Mathf.Lerp(maxBrightness, minBrightness, Mathf.Clamp01(normalizedDist));

        if (variant == 1 && beatData.currentBeat % 2 != 0) return minBrightness; // Even
        if (variant == 2 && beatData.currentBeat % 2 == 0) return minBrightness; // Odd
        if (variant == 3 && beatData.currentBeat != 0) return minBrightness;     // Measure Start

        return pulse;
    }

    /// <summary>
    /// Warps a time value (like effectTime) to "kick" or surge on the beat.
    /// Useful for making noise or fluid motion jump rhythmically.
    /// </summary>
    public float GetBeatTime(int variant, float currentTime, float intensity = 0.2f)
    {
        if (!beatData.active)
            return currentTime;

        // We get the pulse (1.0 on beat, 0.0 off beat)
        float pulse = GetBeatBrightness(variant, 1.0f, 0.0f);
        // This causes the "clock" to surge forward on the beat
        return currentTime + (pulse * intensity);
    }

    /// <summary>
    /// Returns true only on the first frame of a beat allowed by the variant.
    /// Ideal for triggering APalette.Change() or one-shot logic.
    /// </summary>
    public bool IsBeatTriggered(int variant)
    {
        if (!beatData.active || !_isBeatTriggered) return false;

        if (variant == 1 && beatData.currentBeat % 2 != 0) return false;
        if (variant == 2 && beatData.currentBeat % 2 == 0) return false;
        if (variant == 3 && beatData.currentBeat != 0) return false;

        return true;
    }
}