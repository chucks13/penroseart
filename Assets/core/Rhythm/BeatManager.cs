using System;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Transport-only beat, phrase, and level state: exactly what live RaveSystem OSC last said, with wire
/// sentinels intact.
/// </summary>
/// <remarks>
/// This is the thin Unity-serializable transport wrapper around the OSC <see cref="RaveOnAirSnapshot"/>:
/// it holds one snapshot (so adding a wire field never needs a mirrored field here) and adds only the
/// few derived convenience members the runtime reads. Locally derived state (offbeats, beat availability)
/// is contrived by <see cref="BeatManager"/>; effects never read this class directly — raw values flow
/// through the nullable queries on <see cref="BeatManager"/> instead (ADR-0002).
/// </remarks>
[Serializable]
public class BeatData
{
    /// <summary>The Penrose runtime currently models one common-time measure as four named beat slots.</summary>
    private const int BeatSlotCount = 4;

    /// <summary>Sentinel used when OSC has not supplied a usable beat countdown.</summary>
    private const int UnavailableMs = -1;

    /// <summary>
    /// The latest OSC-shaped on-air snapshot this beat model holds. Non-null so Unity can serialize and
    /// display it directly and so readers never need a null check before reaching a transport field.
    /// </summary>
    public RaveOnAirSnapshot snapshot = new RaveOnAirSnapshot();

    /// <summary>Milliseconds until the nearest upcoming beat label, read from the held snapshot.</summary>
    public int nextBeatMs => ReadIntAt(snapshot.beatsCountMs, IndexOfNearestCountdown(snapshot.beatsCountMs));

    /// <summary>True while the nearest upcoming beat label gate is active, read from the held snapshot.</summary>
    public bool onBeat => ReadBoolAt(snapshot.onBeats, IndexOfNearestCountdown(snapshot.beatsCountMs));

    /// <summary>True while the current musical beat label gate is active.</summary>
    public bool currentOnBeat => IsOnBeat(snapshot.beatInBar);

    /// <summary>Replaces the held snapshot with a deep copy of the latest OSC-shaped on-air snapshot.</summary>
    public void CopyFrom(RaveOnAirSnapshot source)
    {
        snapshot = source.Clone();
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
        return ReadBoolAt(snapshot.onBeats, BeatLabelToIndex(beatInBar));
    }

    /// <summary>Converts a musical 1-based beat label into the matching zero-based array slot.</summary>
    private static int BeatLabelToIndex(int beatLabel)
    {
        return beatLabel >= 1 && beatLabel <= BeatSlotCount ? beatLabel - 1 : -1;
    }

    /// <summary>Finds the array slot with the smallest available non-negative countdown.</summary>
    internal static int IndexOfNearestCountdown(int[] source)
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
    internal static int ReadIntAt(int[] source, int index)
    {
        return source != null && index >= 0 && index < source.Length ? source[index] : UnavailableMs;
    }

    /// <summary>Reads a beat/offbeat gate if the requested slot exists; otherwise returns false.</summary>
    internal static bool ReadBoolAt(bool[] source, int index)
    {
        return source != null && index >= 0 && index < source.Length && source[index];
    }
}

/// <summary>
/// The musical-data hub and its read-only Data Surface — the single gateway for asking what the
/// music is doing. Raw wire facts and contrived values are captured into concept doorways with
/// provenance hidden; consumers can read but cannot write through the surface.
/// </summary>
/// <remarks>
/// Controller owns one BeatManager, the RaveSystem adapter applies snapshots through
/// <see cref="SetLiveBeatSource(RaveOnAirSnapshot)"/>, and <see cref="Update"/> captures every
/// doorway once ahead of effect drawing. Legacy rhythm helpers coexist during the expand phase
/// and retire in the beat-data contract cut.
/// </remarks>
[Serializable]
public partial class BeatManager
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

    /// <summary>Sentinel used when a beat countdown is not available.</summary>
    private const int UnavailableMs = -1;

    /// <summary>Current beat state. Defaults are inert until live OSC supplies data.</summary>
    public BeatData beatData = new BeatData();

    /// <summary>
    /// Test seam: the live wire snapshot this manager holds, so edit-mode tests can feed and inspect
    /// wire state by name instead of reaching through <see cref="beatData"/> (which the beat-data
    /// effort will make private). Internal machinery, never a consumer offering — consumers read the
    /// Data Surface doorways.
    /// </summary>
    internal RaveOnAirSnapshot WireSnapshot => beatData.snapshot;

    /// <summary>
    /// Test seam: applies a wire snapshot exactly as the live transport does — the same move as
    /// <see cref="RaveOscReceiver.ApplyTo"/>: mark the source live, then deep-copy the snapshot in.
    /// Internal machinery, never a consumer offering.
    /// </summary>
    internal void FeedWireSnapshot(RaveOnAirSnapshot snapshot)
    {
        SetLiveBeatSource(snapshot);
    }

    /// <summary>
    /// Whether live RaveSystem OSC currently owns <see cref="beatData"/>: <c>true</c> = live OSC (pushed in by
    /// <see cref="RaveOscReceiver.ApplyTo"/> every frame), <c>false</c> = Standalone (no beat).
    /// </summary>
    /// <remarks>
    /// The source is controlled by <see cref="RaveOscReceiver.ApplyTo"/> every frame from UDP 7000 transport
    /// liveness. Any recognized RaveSystem on-air OSC packet makes this live immediately; when packets stop,
    /// the receiver switches this back to Standalone and the next <see cref="Update"/> clears to no beat.
    /// </remarks>
    private bool liveBeatActive;

    /// <summary>True while live RaveSystem OSC owns the beat; false in Standalone (no beat).</summary>
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
    /// Driven two-way by the Waveform Pool selector in the BeatManager dashboard: writing it locks/releases the
    /// wall live, and the selector reads it back to show the current state. <see cref="NonSerializedAttribute"/>
    /// on purpose — a lock is a live performance choice, not a saved scene default, so every session starts in
    /// Auto. Effects already re-read their <c>beatVariant</c> each frame, so a lock that only future effects
    /// pick up would lag; the selector additionally pokes the on-screen effect's variant for an instant change.
    /// </remarks>
    [NonSerialized]
    public int activeVariant = -1;

    // ---- Derived beat state -------------------------------------------------------------------
    // Contrived locally from the transport fields once per frame from the live OSC source
    // (ADR-0002: BeatData is transport-only; locally derived state lives here).

    /// <summary>Milliseconds until offbeat labels 1 through 4, derived from the beat countdowns.</summary>
    private int[] offBeatsCountMs = CreateUnavailableCountdowns();

    /// <summary>Offbeat-label gates for offbeats after beat labels 1 through 4.</summary>
    private bool[] offBeats = new bool[BeatSlotCount];

    /// <summary>Normalized offbeat pulse: 1 on the offbeat, decaying toward 0 until the next offbeat.</summary>
    private float offBeatPulse;

    /// <summary>Derived offbeat countdowns in label order. Dashboard/test view — effects use the nullable queries.</summary>
    public int[] OffBeatsCountMs => offBeatsCountMs;

    /// <summary>Derived offbeat gates in label order. Dashboard/test view — effects use the nullable queries.</summary>
    public bool[] OffBeatGates => offBeats;

    /// <summary>
    /// The single Standalone/Synced mode authority: true while a usable beat clock is running. The running
    /// 4-count (<c>beat_in_bar &gt;= 1</c>) is the truest "is a clock running" signal — it is bedrock,
    /// always-on and given by the wire, never derived from the beat — so every consumer reads mode from it
    /// rather than re-deriving from tempo or transport liveness (ADR-0007).
    /// </summary>
    /// <remarks>
    /// Reading the 4-count means trusting that a running 4-count implies a usable <c>bpm</c>: the tempo-derived
    /// queries below still gate on this authority. That coupling holds because the wire clears <c>bpm</c> and
    /// <c>beat_in_bar</c> together as a set (<see cref="ClearToNoBeat"/>), so a running 4-count never coexists
    /// with an absent tempo on the rig. <see cref="IsLiveSource"/> is OSC-connectivity only, never mode.
    /// </remarks>
    public bool IsSynced => beatData != null && beatData.snapshot.beatInBar >= 1;

    /// <summary>
    /// True while a usable beat clock is present. Redefined onto the single mode authority
    /// (<see cref="IsSynced"/>); the old <c>bpm &gt; 0</c> proxy is retired as a mode signal (ADR-0007).
    /// </summary>
    public bool IsActive => IsSynced;

    /// <summary>
    /// Advances the per-frame derived beat state from Unity time.
    /// </summary>
    /// <remarks>
    /// Live OSC is the only beat source. In Standalone (no live OSC) <see cref="beatData"/> is cleared to the
    /// standard no-beat state exposed through <see cref="IsActive"/>.
    /// </remarks>
    public void Update()
    {
        Update(Time.time);
    }

    /// <summary>
    /// Advances the per-frame derived beat state from a supplied clock value.
    /// </summary>
    /// <remarks>
    /// The explicit time overload keeps the Levels shaping (the attack/release follower and the
    /// Peak drain) testable without relying on Unity frame timing.
    /// </remarks>
    public void Update(float timeSeconds)
    {
        if (beatData == null)
        {
            beatData = new BeatData();
        }

        if (liveBeatActive)
        {
            // Live RaveSystem OSC owns beatData; RaveOscReceiver.ApplyTo wrote it earlier this frame
            // (Controller calls ApplyTo immediately before this Update).
        }
        else
        {
            ClearToNoBeat();
        }

        // Derivation and shaping run after beatData has settled for this frame so the contrived
        // queries never lag the transport by a frame or shape from stale data across a source switch.
        DeriveBeatState();
        UpdateLevelsShaping(timeSeconds);

        // The Data Surface doorway views are captured last, once the transport and every derived
        // value have settled, so all readers this frame — effects, transitions, the Director,
        // dashboards — see one frame-coherent musical truth and edges are evaluated exactly once
        // per hub update, ahead of effect Draw (ADR-0015).
        CaptureDataSurface();
    }

    /// <summary>
    /// Captures every Data Surface doorway view for this frame, in gateway order. Each doorway's
    /// capture translates wire sentinels to null, copies any snapshot-owned arrays it serves, and
    /// evaluates its edges against the prior observed state the hub retains between updates.
    /// </summary>
    private void CaptureDataSurface()
    {
        Clock = CaptureClock();
        Position = CapturePosition();
        Track = CaptureTrack();
        Beats = CaptureBeats();
        OffBeats = CaptureOffBeats();
        Pulses = CapturePulses();
        Phrase = CapturePhrase();
        Drop = CaptureDrop();
        Fill = CaptureFill();
        Energy = CaptureEnergy();
        Loop = CaptureLoop();
        Grid = CaptureGrid();
        Levels = CaptureLevels();
    }

    // ---- Shared span capture math -------------------------------------------------------------
    // One spelling of the elapsed-position rules every Span doorway anchors on (Progress and the
    // Stock Envelopes), so the doorways can never disagree about where "inside a span" sits.

    /// <summary>
    /// Beats elapsed since a span's start, from the wire's beats-remaining count (which includes
    /// the current beat: a length-N span counts N on its first beat) and total length, smoothed
    /// by the shared intra-beat clock so span-anchored motion sweeps instead of stepping once per
    /// beat. Null when the wire's shape cannot anchor a position (either side unavailable, or an
    /// incoherent count) — sentinels never become math.
    /// </summary>
    private float? ElapsedInSpan(int countBeats, int lengthBeats)
    {
        if (lengthBeats <= 0 || countBeats < 0 || countBeats > lengthBeats)
        {
            return null;
        }

        return (lengthBeats - countBeats) + IntraBeatFraction();
    }

    /// <summary>0..1 position through a span of the given length, from the shared elapsed anchor.</summary>
    private static float? ProgressOverLength(float? elapsedBeats, int lengthBeats)
    {
        if (elapsedBeats is not { } elapsed || lengthBeats <= 0)
        {
            return null;
        }

        return Mathf.Clamp01(elapsed / lengthBeats);
    }

    /// <summary>A wire length in beats as an envelope window: positive lengths pass, sentinels and degenerate zero read null.</summary>
    private static float? LengthOrNull(int lengthBeats)
    {
        return lengthBeats > 0 ? lengthBeats : (float?)null;
    }

    /// <summary>
    /// Chooses the beat source: <paramref name="live"/> = live RaveSystem OSC, otherwise Standalone (no beat).
    /// </summary>
    /// <remarks>
    /// Logged on every change of source (never silent). When this turns live off, the next <see cref="Update"/>
    /// clears <see cref="beatData"/> to the no-beat state; when it turns live on, <see cref="Update"/> stands
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
            : "[BeatManager] Beat source -> NONE; RaveSystem OSC is not broadcasting (Standalone, no beat).");
    }

    /// <summary>
    /// Applies one live wire snapshot through the Data Surface ingress. The manager owns a deep
    /// copy, so later transport writes cannot change the frame captured by <see cref="Update"/>.
    /// </summary>
    /// <param name="snapshot">The latest complete RaveSystem on-air snapshot.</param>
    public void SetLiveBeatSource(RaveOnAirSnapshot snapshot)
    {
        SetLiveBeatSource(true);
        beatData.CopyFrom(snapshot);
    }

    /// <summary>
    /// Contrives the locally derived beat state from the transport fields. Runs once per frame from
    /// <see cref="Update"/> after the live source has written <see cref="beatData"/>.
    /// </summary>
    private void DeriveBeatState()
    {
        DeriveOffBeats();
    }

    /// <summary>
    /// Derives offbeat countdowns, gates, and the offbeat pulse from the beat countdowns: each offbeat is
    /// the exact midpoint between adjacent beat labels. A reached offbeat holds its countdown at 0 and its
    /// gate open for a quarter of the average beat, then counts down to the same offbeat in the next bar.
    /// </summary>
    private void DeriveOffBeats()
    {
        var offBeatCounts = CreateUnavailableCountdowns();
        var offBeatGates = new bool[BeatSlotCount];
        offBeatPulse = 0f;
        if (!IsActive || beatData.snapshot.beatAverageMs <= 0 ||
            beatData.snapshot.beatsCountMs == null || beatData.snapshot.beatsCountMs.Length < BeatSlotCount)
        {
            offBeatsCountMs = offBeatCounts;
            offBeats = offBeatGates;
            return;
        }

        var activeWindowMs = beatData.snapshot.beatAverageMs * 0.25f;
        var measureMs = beatData.snapshot.beatAverageMs * (float)BeatSlotCount;
        var nearestOffBeatMs = float.MaxValue;

        for (var i = 0; i < offBeatCounts.Length; i++)
        {
            var nextBeatIndex = (i + 1) % offBeatCounts.Length;
            var startBeatMs = beatData.snapshot.beatsCountMs[i];
            var nextBeatMs = beatData.snapshot.beatsCountMs[nextBeatIndex];
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
                offBeatCounts[i] = Mathf.RoundToInt(offBeatMs);
                continue;
            }

            var elapsedSinceOffBeatMs = halfGapMs - nextBeatMs;
            if (elapsedSinceOffBeatMs <= activeWindowMs)
            {
                offBeatCounts[i] = 0;
                offBeatGates[i] = true;
                continue;
            }

            offBeatCounts[i] = Mathf.RoundToInt(measureMs - elapsedSinceOffBeatMs);
        }

        if (nearestOffBeatMs != float.MaxValue)
        {
            var nextOffBeatInCycleMs = nearestOffBeatMs % beatData.snapshot.beatAverageMs;
            var elapsedSinceNearestOffBeatMs = nextOffBeatInCycleMs <= 0f ? 0f : beatData.snapshot.beatAverageMs - nextOffBeatInCycleMs;
            offBeatPulse = GetPulse(elapsedSinceNearestOffBeatMs, beatData.snapshot.beatAverageMs);
        }

        offBeatsCountMs = offBeatCounts;
        offBeats = offBeatGates;
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
    /// Locks the whole wall to a single Waveform Pool variant — the engaged state of the Wall Variant Lock.
    /// </summary>
    /// <remarks>
    /// Ensures the Pool is loaded, then clamps <paramref name="poolIndex"/> into it, so a lock request always
    /// pins a real Preset and never resolves to the <c>-1</c> Auto sentinel (use <see cref="ReleaseToAuto"/> for
    /// that). The change is logged: a silent change to what the whole wall is playing is exactly the invisible
    /// state the project forbids. This does NOT retarget the effect already on screen — that one step needs the
    /// effects array and stays a Controller concern (see <c>Controller.CurrentBeatVariant</c>).
    /// </remarks>
    /// <param name="poolIndex">The Pool index to lock to; clamped to <c>[0, pool-1]</c>.</param>
    public void LockVariant(int poolIndex)
    {
        EnsurePool();
        var count = waveformPool?.Length ?? 0;
        activeVariant = count > 0 ? Mathf.Clamp(poolIndex, 0, count - 1) : Mathf.Max(0, poolIndex);

        var name = (waveformPoolNames != null && activeVariant < waveformPoolNames.Length)
            ? waveformPoolNames[activeVariant]
            : activeVariant.ToString();
        Debug.Log($"[Waveform] Wall locked to '{name}' (variant {activeVariant}) — every effect uses this until set to Auto.");
    }

    /// <summary>
    /// Releases the Wall Variant Lock back to <b>Auto</b>: every effect rolls its own variant in <c>OnStart()</c>
    /// again. Logged for the same reason <see cref="LockVariant"/> is.
    /// </summary>
    public void ReleaseToAuto()
    {
        activeVariant = -1;
        Debug.Log("[Waveform] Wall released to Auto — effects roll their own beat variant again.");
    }

    /// <summary>
    /// Resolves which variant a read-back display should show: the wall lock when one is held, otherwise the
    /// caller-supplied on-screen effect variant, otherwise Pool index 0.
    /// </summary>
    /// <remarks>
    /// Pure — the on-screen variant is passed in, not reached for via Controller — so it is testable without a
    /// live Controller and cannot drift from the lock semantics above.
    /// </remarks>
    /// <param name="onScreenVariant">The variant the on-screen effect is using, or <c>-1</c> when none
    /// (startup, mid-transition, or Edit Mode).</param>
    public int ResolveDisplayVariant(int onScreenVariant)
    {
        if (activeVariant >= 0)
        {
            return activeVariant;
        }

        return onScreenVariant >= 0 ? onScreenVariant : 0;
    }

    /// <summary>
    /// Normalized position within the current bar in [0..1): 0 on the downbeat, approaching 1 at the next.
    /// </summary>
    /// <remarks>
    /// This is the always-running clock the Waveforms surface evaluates against. It is derived from live
    /// OSC <see cref="beatData"/>: the current 1-based beat label plus the fraction elapsed into that beat.
    /// The intra-beat fraction is read from the *next* beat label's countdown — not the nearest, which reads 0
    /// during the on-beat gate window and would jump. It is as fresh as the incoming snapshots (the same
    /// staleness profile as <see cref="BeatData.beatPulse"/>).
    /// </remarks>
    public float BarPhase
    {
        get
        {
            if (!IsActive)
            {
                return 0f;
            }

            var label = beatData.snapshot.beatInBar;
            if (label < 1 || label > BeatSlotCount)
            {
                return 0f; // beat label not yet known / inert
            }

            return ((label - 1) + IntraBeatFraction()) / (float)BeatSlotCount;
        }
    }

    /// <summary>
    /// Fraction elapsed into the current beat in [0..1], or 0 when the beat clock cannot supply it.
    /// </summary>
    /// <remarks>
    /// This is the sub-beat half of <see cref="BarPhase"/>, shared with the contrived phrase-progress
    /// queries (Fill/Drop/Phrase) so all beat-smoothed motion uses one clock. The fraction is read from
    /// the *next* beat label's countdown — not the nearest, which reads 0 during the on-beat gate window
    /// and would jump.
    /// </remarks>
    private float IntraBeatFraction()
    {
        if (!IsActive)
        {
            return 0f;
        }

        var label = beatData.snapshot.beatInBar;
        if (label < 1 || label > BeatSlotCount)
        {
            return 0f;
        }

        var nextSlot = label % BeatSlotCount; // 0-based slot of the next label (wraps last -> 0)
        var countdowns = beatData.snapshot.beatsCountMs;
        var msToNext = countdowns != null && nextSlot >= 0 && nextSlot < countdowns.Length
            ? countdowns[nextSlot]
            : UnavailableMs;

        if (beatData.snapshot.beatAverageMs > 0 && msToNext >= 0)
        {
            return Mathf.Clamp01(1f - ((float)msToNext / beatData.snapshot.beatAverageMs));
        }

        return 0f;
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
    /// Returns the shortest time, in milliseconds, between audible peaks in the selected Waveform.
    /// </summary>
    /// <remarks>
    /// <see cref="Waveform.ShortestNonZeroPeakSpacing"/> returns a normalized fraction of the whole bar, while
    /// <c>beatAverageMs</c> is one beat. Multiplying by <see cref="Waveform.BeatsPerBar"/> converts the spacing
    /// to beat units before converting to milliseconds. Returns 0 when the beat clock is inactive/unavailable,
    /// the average beat duration is not known, or the Waveform has no audible peaks.
    /// </remarks>
    public float GetShortestNonZeroPeakSpacingMs(int variant)
    {
        if (!IsActive || beatData.snapshot.beatAverageMs <= 0)
        {
            return 0f;
        }

        return GetWaveform(variant).ShortestNonZeroPeakSpacing()
            * Waveform.BeatsPerBar
            * beatData.snapshot.beatAverageMs;
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

    /// <summary>
    /// Resets levels and the phrase states (phase/drop/fill/energy) to their unavailable sentinels.
    /// </summary>
    /// <remarks>
    /// Standalone is a no-beat state, not a musical analysis, so it must never present phrase data. Running
    /// this also flushes stale live values left in <see cref="beatData"/> (including scene-serialized ones)
    /// after a RaveSystem broadcast stops, so the contrived queries on this class return null instead of
    /// replaying the last live Fill/Drop/Energy forever.
    /// </remarks>
    private void ClearPhraseAndLevelState()
    {
        beatData.snapshot.levels = PenroseArt.RaveOsc.Levels.Unavailable;
        beatData.snapshot.phraseState = PhraseState.Unavailable;
        beatData.snapshot.nextPhraseState = LabeledCountdown.Unavailable;
        beatData.snapshot.dropState = CountdownState.Unavailable;
        beatData.snapshot.fillState = CountdownState.Unavailable;
        beatData.snapshot.energyState = LabeledCountdown.Unavailable;
        beatData.snapshot.nextEnergyState = LabeledCountdown.Unavailable;
        beatData.snapshot.loopState = LoopState.Unavailable;
        beatData.snapshot.timingGrid = TimingGrid.Unavailable;
        beatData.snapshot.trackId = -1;
    }

    /// <summary>
    /// Clears beatData to the standard no-beat state. Reached only in Standalone (no live OSC source), so there
    /// is no live OSC source to protect here — live mode is handled before this in <see cref="Update"/>.
    /// </summary>
    private void ClearToNoBeat()
    {
        beatData.snapshot.playersLive = "";
        beatData.snapshot.track = "";
        beatData.snapshot.bpm = UnavailableMs; // wire sentinel: no usable tempo
        beatData.snapshot.beatInBar = -1; // real 4-count sentinel (musically 1..4 or -1, never 0); clears IsSynced/IsActive
        beatData.snapshot.beatAverageMs = 0;
        beatData.snapshot.beatPulse = 0f;
        beatData.snapshot.beatsCountMs = CreateUnavailableCountdowns();
        beatData.snapshot.onBeats = new bool[BeatSlotCount];
        ClearPhraseAndLevelState();
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
    /// Returns the current musical 1-based beat label, treating an unknown label as the downbeat.
    /// </summary>
    private int GetCurrentBeatLabel()
    {
        if (beatData.snapshot.beatInBar >= 1 && beatData.snapshot.beatInBar <= BeatSlotCount)
        {
            return beatData.snapshot.beatInBar;
        }

        return 1;
    }
}
