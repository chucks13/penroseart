using System;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Mutable Rave on-air beat, phrase, level, and offbeat state shared with effects through <see cref="BeatManager"/>.
/// </summary>
/// <remarks>
/// Live values come from RaveSystem OSC. Defaults are inert fallback values used before OSC is available.
/// The field names intentionally mirror the incoming OSC snapshot shape so Unity can serialize and display them directly.
/// </remarks>
[Serializable]
public class BeatData
{
    /// <summary>The Penrose runtime currently models one common-time measure as four named beat slots.</summary>
    private const int BeatSlotCount = 4;

    /// <summary>Sentinel used when OSC has not supplied a usable beat countdown.</summary>
    private const int UnavailableMs = -1;

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
    public BeatPosition beat;

    /// <summary>Focused on-air bar position from /rave/onair/bar.</summary>
    public BarPosition bar;

    /// <summary>Focused on-air 1-based beat label inside the current bar.</summary>
    public int beatInBar;

    /// <summary>Milliseconds until beat labels 1 through 4.</summary>
    public int[] beatsCountMs = CreateUnavailableCountdowns();

    /// <summary>Beat-label gates for beat labels 1 through 4.</summary>
    public bool[] onBeats = new bool[BeatSlotCount];

    /// <summary>Milliseconds until offbeat labels 1 through 4, derived from OSC beat countdowns.</summary>
    public int[] offBeatsCountMs = CreateUnavailableCountdowns();

    /// <summary>Offbeat-label gates for offbeats after beat labels 1 through 4.</summary>
    public bool[] offBeats = new bool[BeatSlotCount];

    /// <summary>Normalized offbeat pulse: 1 on the offbeat, decaying toward 0 until the next offbeat.</summary>
    public float offBeatPulse;

    /// <summary>Average beat duration in milliseconds across live players with usable timing.</summary>
    public int beatAverageMs;

    /// <summary>Normalized OSC beat pulse: 1 on the beat, decaying toward 0.</summary>
    public float beatPulse;

    /// <summary>Average low/mid/high waveform energy across live players.</summary>
    public Levels levels;

    /// <summary>Grouped phase state for the focused on-air track.</summary>
    public PhaseState phaseState;

    /// <summary>Grouped drop countdown state for the focused on-air track.</summary>
    public CountdownState dropState;

    /// <summary>Grouped fill countdown state for the focused on-air track.</summary>
    public CountdownState fillState;

    /// <summary>Grouped energy-run state for the focused on-air track.</summary>
    public PhaseState energyState;

    /// <summary>Milliseconds until the nearest upcoming beat label, read from <see cref="beatsCountMs"/>.</summary>
    public int nextBeatMs => ReadIntAt(beatsCountMs, IndexOfNearestCountdown(beatsCountMs));

    /// <summary>True while the nearest upcoming beat label gate is active, read from <see cref="onBeats"/>.</summary>
    public bool onBeat => ReadBoolAt(onBeats, IndexOfNearestCountdown(beatsCountMs));

    /// <summary>True while the current musical beat label gate is active.</summary>
    public bool currentOnBeat => IsOnBeat(beatInBar);

    /// <summary>Milliseconds until the nearest upcoming offbeat, read from <see cref="offBeatsCountMs"/>.</summary>
    public int nextOffBeatMs => ReadIntAt(offBeatsCountMs, IndexOfNearestCountdown(offBeatsCountMs));

    /// <summary>True while the nearest upcoming offbeat is active, read from <see cref="offBeats"/>.</summary>
    public bool offBeat => ReadBoolAt(offBeats, IndexOfNearestCountdown(offBeatsCountMs));

    /// <summary>Number of beats in the repeating measure.</summary>
    public int beatsPerMeasure = BeatSlotCount;

    /// <summary>
    /// Zero-based compatibility beat index inside the current measure.
    /// Prefer <see cref="beatInBar"/> for new code because Rave OSC uses musical 1-based labels.
    /// </summary>
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
        beatsCountMs = CopyFour(snapshot.beatsCountMs, UnavailableMs);
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
    public int GetNextBeatMs(int fallback = UnavailableMs)
    {
        var value = nextBeatMs;
        return value >= 0 ? value : fallback;
    }

    /// <summary>Returns the on-beat gate for a 1-based beat-in-bar label.</summary>
    public bool IsOnBeat(int beatInBar)
    {
        return ReadBoolAt(onBeats, BeatLabelToIndex(beatInBar));
    }

    /// <summary>Converts a musical 1-based beat label into the matching zero-based array slot.</summary>
    private static int BeatLabelToIndex(int beatLabel)
    {
        return beatLabel >= 1 && beatLabel <= BeatSlotCount ? beatLabel - 1 : -1;
    }

    /// <summary>Finds the array slot with the smallest available non-negative countdown.</summary>
    private static int IndexOfNearestCountdown(int[] source)
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

    /// <summary>Reads a countdown value if the requested slot exists; otherwise returns the unavailable sentinel.</summary>
    private static int ReadIntAt(int[] source, int index)
    {
        return source != null && index >= 0 && index < source.Length ? source[index] : UnavailableMs;
    }

    /// <summary>Reads a beat/offbeat gate if the requested slot exists; otherwise returns false.</summary>
    private static bool ReadBoolAt(bool[] source, int index)
    {
        return source != null && index >= 0 && index < source.Length && source[index];
    }

    /// <summary>Copies OSC countdown data into a fixed four-slot Unity-serialized array.</summary>
    private static int[] CopyFour(int[] source, int missingValue)
    {
        var copy = CreateFilledIntArray(missingValue);
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    /// <summary>Copies OSC gate data into a fixed four-slot Unity-serialized array.</summary>
    private static bool[] CopyFour(bool[] source)
    {
        var copy = new bool[BeatSlotCount];
        if (source != null)
        {
            Array.Copy(source, copy, Math.Min(source.Length, copy.Length));
        }
        return copy;
    }

    /// <summary>Creates a four-slot countdown array where every value is unavailable.</summary>
    private static int[] CreateUnavailableCountdowns()
    {
        return CreateFilledIntArray(UnavailableMs);
    }

    /// <summary>Creates a four-slot countdown array initialized to the same value in every slot.</summary>
    private static int[] CreateFilledIntArray(int value)
    {
        var values = new int[BeatSlotCount];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = value;
        }
        return values;
    }
}

/// <summary>
/// Shared beat helper for effects that want to pulse brightness, time, or one-shot events from Rave OSC beat data.
/// </summary>
/// <remarks>
/// Controller owns one BeatManager and calls <see cref="Update"/> once per frame.
/// Effects usually receive a random beat variant from <see cref="EffectBase.OnStart"/>
/// and pass that variant into the helper methods below.
/// </remarks>
[Serializable]
public class BeatManager
{
    /// <summary>Variant 0: every quarter-note beat uses the main beat pulse.</summary>
    private const int VariantEveryBeat = 0;

    /// <summary>Variant 1: quarter-note beats 1 and 3 use the main beat pulse.</summary>
    private const int VariantBeatsOneAndThree = 1;

    /// <summary>Variant 2: quarter-note beats 2 and 4 use the main beat pulse.</summary>
    private const int VariantBeatsTwoAndFour = 2;

    /// <summary>Variant 3: only the first beat of the measure uses the main beat pulse.</summary>
    private const int VariantMeasureStart = 3;

    /// <summary>Variant 4: quarter-note beats 1 and 4 use the main beat pulse.</summary>
    private const int VariantBeatsOneAndFour = 4;

    /// <summary>Variant 5: offbeat eighth-note positions use the derived offbeat pulse.</summary>
    private const int VariantOffbeat = 5;

    /// <summary>Variant 6: every eighth note uses whichever pulse, beat or offbeat, is currently stronger.</summary>
    private const int VariantEighthNotes = 6;

    /// <summary>Exclusive upper bound for full random variant selection.</summary>
    private const int VariantCount = 7;

    /// <summary>Exclusive upper bound for lower-intensity random variants that avoid offbeat/eighth-note motion.</summary>
    private const int ChillVariantCount = 5;

    /// <summary>Common-time beat slots used by the current Penrose/Rave beat model.</summary>
    private const int BeatSlotCount = 4;

    /// <summary>Sentinel used when a simulated countdown is not available.</summary>
    private const int UnavailableMs = -1;

    /// <summary>Current beat state. Defaults are inert until OSC or the local simulator supplies data.</summary>
    public BeatData beatData = new BeatData();

    /// <summary>
    /// Local fallback tempo used when no live Rave OSC beat data is active.
    /// Set this to 0 or below to disable local beat simulation.
    /// </summary>
    public float simulatedBpm = 120f;

    /// <summary>Tracks whether the currently active beatData values were generated locally rather than received from OSC.</summary>
    private bool usingSimulatedBeatData;

    /// <summary>Shorthand for whether the shared beat state is active.</summary>
    public bool IsActive => beatData != null && beatData.active;

    /// <summary>
    /// Updates the fallback beat simulator from Unity time.
    /// </summary>
    /// <remarks>
    /// Live OSC remains the source of truth. Simulation only runs when live data is inactive
    /// or when the previous frame was already simulated.
    /// </remarks>
    public void Update()
    {
        Update(Time.time);
    }

    /// <summary>
    /// Updates the fallback beat simulator from a supplied clock value.
    /// </summary>
    /// <remarks>
    /// The explicit time overload keeps the simple BPM simulator testable without relying on Unity frame timing.
    /// </remarks>
    public void Update(float timeSeconds)
    {
        if (beatData == null)
        {
            beatData = new BeatData();
        }

        if (beatData.active && !usingSimulatedBeatData)
        {
            return;
        }

        if (simulatedBpm <= 0f)
        {
            ClearSimulatedBeatData();
            return;
        }

        ApplySimulatedBeat(timeSeconds);
    }

    /// <summary>Marks the current beatData as externally supplied so the local simulator will not overwrite active live data.</summary>
    public void MarkExternalBeatDataApplied()
    {
        usingSimulatedBeatData = false;
    }

    /// <summary>Returns a random beat-variant index for effect activation.</summary>
    public int GetRandomVariant()
    {
        return UnityEngine.Random.Range(0, VariantCount);
    }

    /// <summary>Returns a random lower-intensity beat variant, excluding offbeat and eighth-note variants.</summary>
    public int GetRandomVariantChill()
    {
        return UnityEngine.Random.Range(0, ChillVariantCount);
    }

    /// <summary>
    /// Calculates a beat-synced brightness multiplier from Rave OSC beat and offbeat pulses.
    /// </summary>
    /// <remarks>
    /// Variant mapping:
    /// - 0: every beat
    /// - 1: beats 1 and 3
    /// - 2: beats 2 and 4
    /// - 3: measure start / beat 1
    /// - 4: syncopated beats 1 and 4
    /// - 5: offbeat pulse
    /// - 6: eighth notes, using the stronger of the beat and offbeat pulses
    /// Unknown variants intentionally fall back to the every-beat brightness so effect code degrades visibly but safely.
    /// </remarks>
    /// <param name="variant">Beat variant selector using the mapping above.</param>
    /// <param name="maxBrightness">Brightness value at full pulse or when the variant is not currently gated.</param>
    /// <param name="minBrightness">Brightness value at zero pulse.</param>
    /// <param name="enable">If false, returns <paramref name="maxBrightness"/> with no pulsing.</param>
    /// <returns>A brightness multiplier between <paramref name="minBrightness"/> and <paramref name="maxBrightness"/>.</returns>
    public float GetBeatBrightness(int variant, float maxBrightness = 1.0f, float minBrightness = 0.85f, bool enable = true)
    {
        if (!enable || !IsActive)
        {
            return maxBrightness;
        }

        switch (variant)
        {
            case VariantBeatsOneAndThree:
                return BeatLabelMatchesCurrent(1, 3) ? BeatPulseBrightness(minBrightness, maxBrightness) : maxBrightness;
            case VariantBeatsTwoAndFour:
                return BeatLabelMatchesCurrent(2, 4) ? BeatPulseBrightness(minBrightness, maxBrightness) : maxBrightness;
            case VariantMeasureStart:
                return BeatLabelMatchesCurrent(1) ? BeatPulseBrightness(minBrightness, maxBrightness) : maxBrightness;
            case VariantBeatsOneAndFour:
                return BeatLabelMatchesCurrent(1, 4) ? BeatPulseBrightness(minBrightness, maxBrightness) : maxBrightness;
            case VariantOffbeat:
                return PulseToBrightness(beatData.offBeatPulse, minBrightness, maxBrightness);
            case VariantEighthNotes:
                return PulseToBrightness(GetEighthNotePulse(), minBrightness, maxBrightness);
            case VariantEveryBeat:
            default:
                return BeatPulseBrightness(minBrightness, maxBrightness);
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
        if (!IsActive)
        {
            return currentTime;
        }

        var pulse = GetBeatBrightness(variant, 1.0f, 0.0f);
        return currentTime + (pulse * intensity);
    }

    /// <summary>
    /// Returns true while the current beat gate is active and allowed by one-shot beat variants.
    /// </summary>
    /// <remarks>
    /// This helper is used for event-like work such as spawning or palette changes.
    /// To preserve existing effect behavior, only variants 1, 2, and 3 add extra beat-label gates here;
    /// variants 0, 4, 5, and 6 trigger on every current on-beat gate.
    /// </remarks>
    public bool IsBeatTriggered(int variant)
    {
        if (!IsActive || !beatData.currentOnBeat)
        {
            return false;
        }

        var currentBeatLabel = GetCurrentBeatLabel();
        switch (variant)
        {
            case VariantBeatsOneAndThree:
                return BeatLabelMatches(currentBeatLabel, 1, 3);
            case VariantBeatsTwoAndFour:
                return BeatLabelMatches(currentBeatLabel, 2, 4);
            case VariantMeasureStart:
                return BeatLabelMatches(currentBeatLabel, 1);
            default:
                return true;
        }
    }

    /// <summary>Writes a complete four-beat simulated snapshot into BeatData for the supplied clock time.</summary>
    private void ApplySimulatedBeat(float timeSeconds)
    {
        var beatDurationSeconds = 60f / simulatedBpm;
        var measureDurationSeconds = beatDurationSeconds * BeatSlotCount;
        var beatAverageMs = Mathf.RoundToInt(beatDurationSeconds * 1000f);
        var gateDurationSeconds = beatDurationSeconds * 0.25f;
        var positionSeconds = Mathf.Repeat(timeSeconds, measureDurationSeconds);
        var beatIndex = Mathf.FloorToInt(positionSeconds / beatDurationSeconds);
        var beatStartSeconds = beatIndex * beatDurationSeconds;
        var elapsedSinceBeatSeconds = positionSeconds - beatStartSeconds;
        var onBeat = elapsedSinceBeatSeconds < gateDurationSeconds;

        beatData.active = true;
        beatData.playersLive = "SIM";
        beatData.track = "Simulated Beat";
        beatData.bpm = simulatedBpm;
        beatData.beatInBar = beatIndex + 1;
        beatData.currentBeat = beatIndex;
        beatData.beatsPerMeasure = BeatSlotCount;
        beatData.beatAverageMs = beatAverageMs;
        beatData.beatPulse = GetPulse(elapsedSinceBeatSeconds, beatDurationSeconds);
        beatData.beatsCountMs = BuildCountdowns(positionSeconds, beatDurationSeconds, 0f, onBeat, beatIndex);
        beatData.onBeats = BuildGates(onBeat, beatIndex);

        var offBeatIndex = GetOffBeatIndex(positionSeconds, beatDurationSeconds);
        var elapsedSinceOffBeatSeconds = GetElapsedSinceOffBeat(positionSeconds, beatDurationSeconds, offBeatIndex);
        var offBeat = elapsedSinceOffBeatSeconds < gateDurationSeconds;
        beatData.offBeatPulse = GetPulse(elapsedSinceOffBeatSeconds, beatDurationSeconds);
        beatData.offBeatsCountMs = BuildCountdowns(positionSeconds, beatDurationSeconds, beatDurationSeconds * 0.5f, offBeat, offBeatIndex);
        beatData.offBeats = BuildGates(offBeat, offBeatIndex);
        usingSimulatedBeatData = true;
    }

    /// <summary>Clears locally simulated values when fallback BPM is disabled.</summary>
    private void ClearSimulatedBeatData()
    {
        if (!usingSimulatedBeatData && beatData.active)
        {
            return;
        }

        beatData.active = false;
        beatData.playersLive = "";
        beatData.track = "";
        beatData.bpm = simulatedBpm;
        beatData.beatInBar = 0;
        beatData.currentBeat = 0;
        beatData.beatAverageMs = 0;
        beatData.beatPulse = 0f;
        beatData.offBeatPulse = 0f;
        beatData.beatsCountMs = CreateUnavailableCountdowns();
        beatData.onBeats = new bool[BeatSlotCount];
        beatData.offBeatsCountMs = CreateUnavailableCountdowns();
        beatData.offBeats = new bool[BeatSlotCount];
        usingSimulatedBeatData = false;
    }

    /// <summary>Builds label-ordered countdowns for either beat or offbeat slots.</summary>
    private static int[] BuildCountdowns(float positionSeconds, float beatDurationSeconds, float slotOffsetSeconds, bool currentGate, int currentIndex)
    {
        var measureDurationSeconds = beatDurationSeconds * BeatSlotCount;
        var countdowns = new int[BeatSlotCount];
        for (var i = 0; i < countdowns.Length; i++)
        {
            var slotSeconds = (i * beatDurationSeconds) + slotOffsetSeconds;
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

    /// <summary>Creates a four-slot countdown array where every value is unavailable.</summary>
    private static int[] CreateUnavailableCountdowns()
    {
        var countdowns = new int[BeatSlotCount];
        for (var i = 0; i < countdowns.Length; i++)
        {
            countdowns[i] = UnavailableMs;
        }
        return countdowns;
    }

    /// <summary>Returns the currently active offbeat slot for the supplied position inside the measure.</summary>
    private static int GetOffBeatIndex(float positionSeconds, float beatDurationSeconds)
    {
        return Mod(Mathf.FloorToInt((positionSeconds - (beatDurationSeconds * 0.5f)) / beatDurationSeconds), BeatSlotCount);
    }

    /// <summary>Returns seconds elapsed since the most recent offbeat midpoint.</summary>
    private static float GetElapsedSinceOffBeat(float positionSeconds, float beatDurationSeconds, int offBeatIndex)
    {
        var offBeatSeconds = (offBeatIndex * beatDurationSeconds) + (beatDurationSeconds * 0.5f);
        var elapsed = positionSeconds - offBeatSeconds;
        return elapsed >= 0f ? elapsed : elapsed + (beatDurationSeconds * BeatSlotCount);
    }

    /// <summary>Returns a smooth normalized pulse that is 1 at the event and decays toward 0 before the next event.</summary>
    private static float GetPulse(float elapsedSeconds, float durationSeconds)
    {
        if (durationSeconds <= 0f)
        {
            return 0f;
        }

        var progress = Mathf.Clamp01(elapsedSeconds / durationSeconds);
        return 1f - Mathf.SmoothStep(0f, 1f, progress);
    }

    /// <summary>Positive modulo helper for wrapping negative offbeat indexes back into four beat slots.</summary>
    private static int Mod(int value, int modulus)
    {
        return ((value % modulus) + modulus) % modulus;
    }

    /// <summary>Returns the beat pulse converted into the requested brightness range.</summary>
    private float BeatPulseBrightness(float minBrightness, float maxBrightness)
    {
        return PulseToBrightness(beatData.beatPulse, minBrightness, maxBrightness);
    }

    /// <summary>Returns true when the current musical beat label matches either supplied label.</summary>
    private bool BeatLabelMatchesCurrent(int firstBeatLabel, int secondBeatLabel)
    {
        return BeatLabelMatches(GetCurrentBeatLabel(), firstBeatLabel, secondBeatLabel);
    }

    /// <summary>Returns true when the current musical beat label matches the supplied label.</summary>
    private bool BeatLabelMatchesCurrent(int beatLabel)
    {
        return BeatLabelMatches(GetCurrentBeatLabel(), beatLabel);
    }

    /// <summary>Returns true when a musical beat label equals either accepted label.</summary>
    private static bool BeatLabelMatches(int beatLabel, int firstBeatLabel, int secondBeatLabel)
    {
        return beatLabel == firstBeatLabel || beatLabel == secondBeatLabel;
    }

    /// <summary>Returns true when a musical beat label equals the accepted label.</summary>
    private static bool BeatLabelMatches(int beatLabel, int acceptedBeatLabel)
    {
        return beatLabel == acceptedBeatLabel;
    }

    /// <summary>Returns the stronger normalized pulse across quarter-note and offbeat positions.</summary>
    private float GetEighthNotePulse()
    {
        return Mathf.Max(Mathf.Clamp01(beatData.beatPulse), Mathf.Clamp01(beatData.offBeatPulse));
    }

    /// <summary>
    /// Returns the current musical 1-based beat label, falling back to the legacy zero-based field when needed.
    /// </summary>
    private int GetCurrentBeatLabel()
    {
        if (beatData.beatInBar >= 1 && beatData.beatInBar <= beatData.beatsPerMeasure)
        {
            return beatData.beatInBar;
        }

        // Legacy fallback for tests or older callers that only populated currentBeat.
        return beatData.currentBeat + 1;
    }

    /// <summary>Clamps a normalized pulse and maps it into the requested brightness range.</summary>
    private static float PulseToBrightness(float pulse, float minBrightness, float maxBrightness)
    {
        return Mathf.Lerp(minBrightness, maxBrightness, Mathf.Clamp01(pulse));
    }
}
