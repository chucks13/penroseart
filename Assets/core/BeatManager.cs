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
    // The constants below name the beat-label gates used by IsBeatTriggered, the legacy discrete-event path
    // (spawns, palette changes) that deliberately survived the Waveform migration. They are NOT a variant
    // enumeration: continuous brightness now flows through GetWaveform + Waveform.Evaluate, and random
    // selection bounds on the live Pool length rather than a fixed variant count. Their values intentionally
    // match the seed Pool order in SeedDefaultPool (index 1 = "beats 1 & 3", 2 = "beats 2 & 4",
    // 3 = "measure start"), so a variant gates the same beats it brightens as long as that seed order holds.

    /// <summary>Beat-label gate for the "beats 1 and 3" variant (seed Pool index 1).</summary>
    private const int VariantBeatsOneAndThree = 1;

    /// <summary>Beat-label gate for the "beats 2 and 4" variant (seed Pool index 2).</summary>
    private const int VariantBeatsTwoAndFour = 2;

    /// <summary>Beat-label gate for the "measure start" variant (seed Pool index 3).</summary>
    private const int VariantMeasureStart = 3;

    /// <summary>Exclusive upper bound for lower-intensity random variants that avoid offbeat/eighth-note motion.</summary>
    private const int ChillVariantCount = 5;

    /// <summary>Common-time beat slots used by the current Penrose/Rave beat model.</summary>
    private const int BeatSlotCount = 4;

    /// <summary>Sentinel used when a simulated countdown is not available.</summary>
    private const int UnavailableMs = -1;

    /// <summary>
    /// Local fallback tempo used when no live Rave OSC beat data is active and
    /// <see cref="simulatedBeatEnabled"/> allows the simulator to run.
    /// </summary>
    public float simulatedBpm = 120f;

    /// <summary>
    /// Whether the local fallback simulator may synthesize beat data when no live Rave OSC beat is active.
    /// Turn this off to make "no live beat" a real no-beat state for effects: <see cref="BeatData.active"/>
    /// and <see cref="IsActive"/> become false.
    /// </summary>
    public bool simulatedBeatEnabled = true;

    /// <summary>Current beat state. Defaults are inert until OSC or the local simulator supplies data.</summary>
    public BeatData beatData = new BeatData();

    /// <summary>
    /// Which source currently owns <see cref="beatData"/>: <c>true</c> = live RaveSystem OSC (pushed in by
    /// <see cref="RaveOscReceiver.ApplyTo"/> every frame), <c>false</c> = the local simulator.
    /// </summary>
    /// <remarks>
    /// The source is controlled by <see cref="RaveOscReceiver.ApplyTo"/> every frame from UDP 7000 transport
    /// liveness. Any recognized RaveSystem on-air OSC packet makes this live immediately; when packets stop,
    /// the receiver switches this back to the simulator/no-beat fallback.
    /// </remarks>
    private bool liveBeatActive;

    /// <summary>True while live RaveSystem OSC owns the beat; false while the local simulator drives it.</summary>
    public bool IsLiveSource => liveBeatActive;

    /// <summary>
    /// The Waveform Pool: the Presets random selection draws from and that <c>int</c> variants index into.
    /// Loaded lazily via <see cref="EnsurePool"/> — file-first from <c>penrose_waveforms.txt</c> in
    /// StreamingAssets, falling back to the built-in seed Pool only when that file is missing or unparseable.
    /// </summary>
    private Waveform[] waveformPool;

    /// <summary>
    /// Preset names parallel to <see cref="waveformPool"/> (same index), in the GPalette <c>names</c>/<c>palettes</c>
    /// style. Kept for meaningful load logging and future by-name Preset lookup; the runtime brightness path
    /// indexes by <c>int</c> and does not require them.
    /// </summary>
    private string[] waveformPoolNames;

    /// <summary>
    /// Wall-wide Waveform override. <c>-1</c> means "Auto": every effect rolls its own random variant in
    /// <c>OnStart()</c> exactly as it always has. Any value &gt;= 0 locks the whole wall to that single
    /// Waveform Pool index — <see cref="GetRandomVariant"/> and <see cref="GetRandomVariantChill"/> stop
    /// rolling and return it, so each newly started effect inherits the lock and the wall keeps one rhythm.
    /// </summary>
    /// <remarks>
    /// Driven two-way by the Waveform Pool selector in the BeatData inspector: writing it locks/releases the
    /// wall live, and the selector reads it back to show the current state. <see cref="NonSerializedAttribute"/>
    /// on purpose — a lock is a live performance choice, not a saved scene default, so every session starts in
    /// Auto. Effects already re-read their <c>beatVariant</c> each frame, so a lock that only future effects
    /// pick up would lag; the selector additionally pokes the on-screen effect's variant for an instant change.
    /// </remarks>
    [NonSerialized]
    public int activeVariant = -1;

    /// <summary>Shorthand for whether the shared beat state is active.</summary>
    public bool IsActive => beatData != null && beatData.active;

    /// <summary>
    /// Updates the fallback beat simulator from Unity time.
    /// </summary>
    /// <remarks>
    /// Live OSC remains the source of truth. Simulation only runs when live data is inactive, the simulator is
    /// enabled, and <see cref="simulatedBpm"/> is positive. Otherwise <see cref="beatData"/> is cleared to the
    /// standard no-beat state exposed through <see cref="IsActive"/>.
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

        if (liveBeatActive)
        {
            // Live RaveSystem OSC owns beatData this effect; RaveOscReceiver.ApplyTo writes it each frame.
            return;
        }

        if (!simulatedBeatEnabled || simulatedBpm <= 0f)
        {
            ClearSimulatedBeatData();
            return;
        }

        ApplySimulatedBeat(timeSeconds);
    }

    /// <summary>
    /// Chooses the beat source: <paramref name="live"/> = live RaveSystem OSC, otherwise the local simulator.
    /// </summary>
    /// <remarks>
    /// Logged on every change of source (never silent). When this turns live off, the next <see cref="Update"/>
    /// either resumes the simulator on <see cref="simulatedBpm"/> or clears <see cref="beatData"/> to the no-beat
    /// state when <see cref="simulatedBeatEnabled"/> is false; when it turns live on, <see cref="Update"/> stands
    /// aside and lets <see cref="RaveOscReceiver.ApplyTo"/> keep <see cref="beatData"/> current.
    /// </remarks>
    public void SetLiveBeatSource(bool live)
    {
        if (live == liveBeatActive)
        {
            return;
        }

        liveBeatActive = live;
        Debug.Log(live
            ? "[BeatManager] Beat source -> LIVE RaveSystem OSC (UDP 7000 broadcasting)."
            : simulatedBeatEnabled && simulatedBpm > 0f
                ? $"[BeatManager] Beat source -> SIMULATED ({simulatedBpm:0.#} BPM); RaveSystem OSC is not broadcasting."
                : "[BeatManager] Beat source -> NONE; RaveSystem OSC is not broadcasting and simulated beat is disabled.");
    }

    /// <summary>Returns a random Waveform index drawn from the full Pool, for effect activation.</summary>
    public int GetRandomVariant()
    {
        EnsurePool();
        // Honor a wall-wide lock so every effect that starts uses the chosen Waveform. Clamp defensively: the
        // Pool can shrink when penrose_waveforms.txt is re-saved with fewer entries while a higher lock is held.
        if (activeVariant >= 0)
        {
            return Mathf.Clamp(activeVariant, 0, waveformPool.Length - 1);
        }
        return UnityEngine.Random.Range(0, waveformPool.Length);
    }

    /// <summary>
    /// Returns a random lower-intensity Waveform index, excluding the busier offbeat/eighth-note Presets.
    /// </summary>
    /// <remarks>
    /// The "chill" subset is the lower-index range of the Pool (the seed Pool lists the calmer Presets
    /// first). This index split is a placeholder: energy/mood-filtered selection driven by OSC
    /// <c>energy_state</c> is deferred until that incoming data is finalized.
    /// </remarks>
    public int GetRandomVariantChill()
    {
        EnsurePool();
        // A wall-wide lock wins here too, so the chill path can never bypass the chosen Waveform.
        if (activeVariant >= 0)
        {
            return Mathf.Clamp(activeVariant, 0, waveformPool.Length - 1);
        }
        var chillCount = Mathf.Clamp(ChillVariantCount, 1, waveformPool.Length);
        return UnityEngine.Random.Range(0, chillCount);
    }

    /// <summary>
    /// Normalized position within the current bar in [0..1): 0 on the downbeat, approaching 1 at the next.
    /// </summary>
    /// <remarks>
    /// This is the always-running clock the Waveform Synthesizer evaluates against. It is derived uniformly
    /// for both the local simulator and live OSC from <see cref="beatData"/>: the current 1-based beat label
    /// plus the fraction elapsed into that beat. The intra-beat fraction is read from the *next* beat label's
    /// countdown — not the nearest, which reads 0 during the on-beat gate window and would jump. In the
    /// simulator this equals positionSeconds / measureDuration exactly; in live OSC it is as fresh as the
    /// incoming snapshots (the same staleness profile as <see cref="BeatData.beatPulse"/>) and should be
    /// validated against real Rave timing once OSC becomes the active beat source.
    /// </remarks>
    public float BarPhase
    {
        get
        {
            if (!IsActive)
            {
                return 0f;
            }

            var beatsPerMeasure = beatData.beatsPerMeasure > 0 ? beatData.beatsPerMeasure : BeatSlotCount;
            var label = beatData.beatInBar;
            if (label < 1 || label > beatsPerMeasure)
            {
                return 0f; // beat label not yet known / inert
            }

            // Read the countdown to the *next* beat label so the fraction grows 0 -> 1 across this beat.
            var nextSlot = label % beatsPerMeasure; // 0-based slot of the next label (wraps last -> 0)
            var countdowns = beatData.beatsCountMs;
            var msToNext = countdowns != null && nextSlot >= 0 && nextSlot < countdowns.Length
                ? countdowns[nextSlot]
                : UnavailableMs;

            var intraBeatPhase = 0f;
            if (beatData.beatAverageMs > 0 && msToNext >= 0)
            {
                intraBeatPhase = Mathf.Clamp01(1f - ((float)msToNext / beatData.beatAverageMs));
            }

            return ((label - 1) + intraBeatPhase) / beatsPerMeasure;
        }
    }

    /// <summary>Returns the Waveform for a Pool index, clamping an unknown index to the Beat Pulse (index 0).</summary>
    /// <remarks>
    /// Out-of-range maps to index 0 (the Beat Pulse / every-beat), matching the old "unknown variant
    /// degrades to every-beat" behavior. We deliberately do not log here — this runs every frame and
    /// random selection only ever produces valid indices.
    /// </remarks>
    public Waveform GetWaveform(int variant)
    {
        EnsurePool();
        var index = variant >= 0 && variant < waveformPool.Length ? variant : 0;
        return waveformPool[index];
    }

    /// <summary>Loads the Pool on first use if it has not been populated yet.</summary>
    /// <remarks>
    /// Guarded so the file is read exactly once per session (this runs every frame via <see cref="GetWaveform"/>):
    /// once the Pool is non-empty — whether from the file or the seed fallback — it is never re-read.
    /// </remarks>
    private void EnsurePool()
    {
        if (waveformPool != null && waveformPool.Length > 0)
        {
            return;
        }

        LoadWaveformPool();
    }

    /// <summary>
    /// Loads the Pool file-first, falling back to the built-in seed Pool only when the file is missing or
    /// yields no parseable Presets. The fallback is always logged — there is no silent substitution.
    /// </summary>
    private void LoadWaveformPool()
    {
        // The Pool format lives in one place — WaveformPool — so this read path can never disagree with the
        // editor's write path. ReadFileOrEmpty already logs a present-but-unreadable file as an error.
        var entries = WaveformPool.Parse(WaveformPool.ReadFileOrEmpty());
        if (entries.Count > 0)
        {
            waveformPoolNames = new string[entries.Count];
            waveformPool = new Waveform[entries.Count];
            for (var i = 0; i < entries.Count; i++)
            {
                waveformPoolNames[i] = entries[i].name;
                waveformPool[i] = entries[i].waveform;
            }

            Debug.Log($"[Waveform] Loaded {waveformPool.Length} Preset(s) from {WaveformPool.FileName}: " +
                      $"{string.Join(", ", waveformPoolNames)}");
            return;
        }

        Debug.LogWarning($"[Waveform] {WaveformPool.FileName} is missing, empty, or had no parseable " +
                         "DEFINE_WAVEFORM entries — falling back to the built-in seed Pool.");
        SeedDefaultPool();
    }

    /// <summary>
    /// Seeds the Pool in-memory with the seven legacy beat variants as Waveforms, in their original index order.
    /// </summary>
    /// <remarks>
    /// This is the fallback when <see cref="WaveformPoolFileName"/> is absent or unparseable, and the bootstrap
    /// content the file ships with. The index order is load-bearing: it keeps the existing <c>int beatVariant</c>
    /// currency resolving to the same rhythmic intent, and <see cref="IsBeatTriggered"/>'s gate labels (1/2/3) are
    /// coupled to it. Each entry uses the canonical Beat Pulse rounding; gated beats are amplitude 0 (the gate),
    /// and the offbeat is a half-beat phase offset.
    /// </remarks>
    private void SeedDefaultPool()
    {
        var r = Waveform.BeatPulseRounding;
        waveformPoolNames = new[]
        {
            "beat pulse", "beats 1 and 3", "beats 2 and 4", "measure start",
            "beats 1 and 4", "offbeat", "every eighth",
        };
        waveformPool = new[]
        {
            Waveform.Parse("QQQQ", "8888", r, 0f),         // 0 every beat — the Beat Pulse
            Waveform.Parse("QQQQ", "8080", r, 0f),         // 1 beats 1 & 3
            Waveform.Parse("QQQQ", "0808", r, 0f),         // 2 beats 2 & 4
            Waveform.Parse("QQQQ", "8000", r, 0f),         // 3 measure start
            Waveform.Parse("QQQQ", "8008", r, 0f),         // 4 beats 1 & 4
            Waveform.Parse("QQQQ", "8888", r, 0.5f),       // 5 offbeat (half-beat phase offset)
            Waveform.Parse("EEEEEEEE", "88888888", r, 0f), // 6 every eighth note
        };
    }

    /// <summary>
    /// Calculates a beat-synced brightness multiplier by evaluating the variant's Waveform against the live Bar Phase.
    /// </summary>
    /// <remarks>
    /// This is the single migration seam from the old seven-way rhythm switch to the Waveform model. The
    /// <paramref name="variant"/> selector no longer indexes a hardcoded <c>switch</c>; it indexes the Waveform Pool.
    /// <see cref="GetWaveform"/> resolves it to a <see cref="Waveform"/>, which <see cref="Waveform.Evaluate"/>
    /// turns into a unipolar [0..1] envelope at the current <see cref="BarPhase"/>. That envelope is then mapped
    /// into the requested brightness range.
    ///
    /// The envelope is symmetric around every enabled beat: it peaks (1) on the beat and falls to 0 at the midpoint
    /// between beats, so <paramref name="maxBrightness"/> lands on the beat and <paramref name="minBrightness"/> in
    /// the trough. A skipped beat (Amplitude 0) sits flat at <paramref name="minBrightness"/>.
    ///
    /// Note the behavior delta from the legacy switch: gated variants (e.g. measure-start) used to snap their
    /// off-beats to <paramref name="maxBrightness"/>; under the Waveform model a gated (Amplitude-0) beat instead
    /// rests at the <paramref name="minBrightness"/> floor, because 0 amplitude means a silent Hump, not "full on."
    ///
    /// An out-of-range or unknown variant resolves to Waveform 0 (the Beat Pulse) inside <see cref="GetWaveform"/>,
    /// so effect code degrades visibly to the canonical pulse rather than throwing. The ~18 effect call sites are
    /// unchanged by this swap.
    /// </remarks>
    /// <param name="variant">Waveform Pool selector; resolved to a <see cref="Waveform"/> by <see cref="GetWaveform"/>.</param>
    /// <param name="maxBrightness">Brightness emitted at the envelope peak (on the beat), or when disabled/inactive.</param>
    /// <param name="minBrightness">Brightness emitted at the envelope trough (the midpoint between beats).</param>
    /// <param name="enable">If false, returns <paramref name="maxBrightness"/> with no pulsing.</param>
    /// <returns>A brightness multiplier between <paramref name="minBrightness"/> and <paramref name="maxBrightness"/>.</returns>
    public float GetBeatBrightness(int variant, float maxBrightness = 1.0f, float minBrightness = 0.85f, bool enable = true)
    {
        if (!enable || !IsActive)
        {
            return maxBrightness;
        }

        var envelope = GetWaveform(variant).Evaluate(BarPhase);
        return Mathf.Lerp(minBrightness, maxBrightness, envelope);
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
    }

    /// <summary>
    /// Clears beatData to the standard no-beat state. Reached only in simulator mode when the simulator is off
    /// or <see cref="simulatedBpm"/> is disabled (&lt;= 0), so there is no live data to protect here — live mode
    /// returns earlier in <see cref="Update"/>.
    /// </summary>
    private void ClearSimulatedBeatData()
    {
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
}
