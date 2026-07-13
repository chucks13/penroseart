using System;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// Unity-serializable holder for the private RaveSystem transport snapshot.
/// </summary>
/// <remarks>
/// The holder has no consumer-facing conveniences. <see cref="BeatManager"/> owns it privately, and
/// every read leaves through a captured concept doorway.
/// </remarks>
[Serializable]
internal sealed class BeatData
{
    /// <summary>
    /// The latest OSC-shaped on-air snapshot this beat model holds. Non-null so Unity can serialize and
    /// display it directly and so readers never need a null check before reaching a transport field.
    /// </summary>
    [SerializeField]
    internal RaveOnAirSnapshot snapshot = new RaveOnAirSnapshot();

    /// <summary>Replaces the held snapshot with a deep copy of the latest OSC-shaped on-air snapshot.</summary>
    internal void CopyFrom(RaveOnAirSnapshot source)
    {
        snapshot = source.Clone();
    }
}

/// <summary>
/// The musical-data hub and its read-only Data Surface — the single gateway for asking what the
/// music is doing. Raw wire facts and contrived values are captured into concept doorways with
/// provenance hidden; consumers can read but cannot write through the surface.
/// </summary>
/// <remarks>
/// Controller owns one BeatManager, the RaveSystem adapter applies snapshots through
/// <see cref="FeedWireSnapshot"/>, and <see cref="Update"/> captures every doorway once ahead of
/// effect drawing.
/// </remarks>
[Serializable]
public partial class BeatManager
{
    /// <summary>Common-time beat slots used by the current Penrose/Rave beat model.</summary>
    private const int BeatSlotCount = 4;

    /// <summary>Sentinel used when a beat countdown is not available.</summary>
    private const int UnavailableMs = -1;

    /// <summary>Translates a non-negative wire integer while mapping its unavailable sentinel to null.</summary>
    private static int? NonNegativeOrNull(int value)
    {
        return value >= 0 ? value : (int?)null;
    }

    /// <summary>Translates a wire tri-state integer: one, zero, or unavailable.</summary>
    private static bool? TriStateOrNull(int value)
    {
        return value switch
        {
            1 => true,
            0 => false,
            _ => (bool?)null,
        };
    }

    /// <summary>Private transport state. Unity serializes it for the raw Inspector debug foldout.</summary>
    [SerializeField]
    private BeatData beatData = new BeatData();

    /// <summary>
    /// Applies the latest live transport snapshot and owns a deep copy before the Data Surface is
    /// captured. This is the one wire-in seam used by the OSC adapter and transport-level tests.
    /// </summary>
    internal void FeedWireSnapshot(RaveOnAirSnapshot snapshot)
    {
        SetLiveBeatSource(true);
        beatData.CopyFrom(snapshot);
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

    /// <summary>
    /// The single Standalone/Synced mode authority: true while a usable beat clock is running. The running
    /// 4-count (<c>beat_in_bar &gt;= 1</c>) is the truest "is a clock running" signal — it is bedrock,
    /// always-on and given by the wire, never derived from the beat — so every consumer reads mode from it
    /// rather than re-deriving from tempo or transport liveness (ADR-0007).
    /// </summary>
    /// <remarks>
    /// Reading the 4-count means trusting that a running 4-count implies a usable <c>bpm</c>: the tempo-derived
    /// doorway facts still gate on this authority. That coupling holds because the wire clears <c>bpm</c> and
    /// <c>beat_in_bar</c> together as a set (<see cref="ClearToNoBeat"/>), so a running 4-count never coexists
    /// with an absent tempo on the rig. Transport liveness remains private plumbing, never mode.
    /// </remarks>
    public bool IsSynced => beatData != null && beatData.snapshot.beatInBar >= 1;

    /// <summary>
    /// Advances the per-frame derived beat state from Unity time.
    /// </summary>
    /// <remarks>
    /// Live OSC is the only beat source. In Standalone (no live OSC) <see cref="beatData"/> is cleared to the
    /// standard no-beat state exposed through <see cref="IsSynced"/>.
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
    internal void SetLiveBeatSource(bool live)
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
    /// Contrives the locally derived beat state from the transport fields. Runs once per frame from
    /// <see cref="Update"/> after the live source has written <see cref="beatData"/>.
    /// </summary>
    private void DeriveBeatState()
    {
        DeriveOffBeats();
    }

    /// <summary>
    /// Resets levels and the phrase states (phase/drop/fill/energy) to their unavailable sentinels.
    /// </summary>
    /// <remarks>
    /// Standalone is a no-beat state, not a musical analysis, so it must never present phrase data. Running
    /// this also flushes stale live values left in <see cref="beatData"/> (including scene-serialized ones)
    /// after a RaveSystem broadcast stops, so the captured concept doorways return null instead of
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
        beatData.snapshot.beatInBar = -1; // real 4-count sentinel (musically 1..4 or -1, never 0); clears IsSynced
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

}
