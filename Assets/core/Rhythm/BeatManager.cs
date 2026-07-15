using System;
using PenroseArt.RaveOsc;
using UnityEngine;

/// <summary>
/// The read-only musical-data hub. It exposes wire facts and derived musical values in shallow,
/// immutable groups without directing consumers how to use them.
/// </summary>
/// <remarks>
/// Controller owns one BeatManager, the RaveSystem adapter applies snapshots through
/// <see cref="FeedWireSnapshot"/>, and <see cref="Update"/> captures every group once ahead of
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
        return value >= 0 ? value : null;
    }

    /// <summary>Translates a wire tri-state integer: one, zero, or unavailable.</summary>
    private static bool? TriStateOrNull(int value)
    {
        return value switch
        {
            1 => true,
            0 => false,
            _ => null,
        };
    }

    /// <summary>Translates a wire tri-state integer to a resting bool: true only when reported active.</summary>
    private static bool TriStateTrue(int value) => value == 1;

    /// <summary>The private OSC-shaped transport state serialized for the raw Inspector debug foldout.</summary>
    [SerializeField]
    private RaveOnAirSnapshot wireSnapshot = new();

    /// <summary>
    /// Applies the latest live transport snapshot and owns a deep copy before public values are
    /// captured. This is the one wire-in seam used by the OSC adapter and transport-level tests.
    /// </summary>
    internal void FeedWireSnapshot(RaveOnAirSnapshot snapshot)
    {
        SetLiveBeatSource(true);
        wireSnapshot = snapshot.Clone();
    }

    /// <summary>
    /// Whether live RaveSystem OSC currently owns <see cref="wireSnapshot"/>: <c>true</c> = live OSC (pushed in by
    /// <see cref="RaveOscReceiver.ApplyTo"/> every frame), <c>false</c> = Standalone (no beat).
    /// </summary>
    /// <remarks>
    /// The source is controlled by <see cref="RaveOscReceiver.ApplyTo"/> every frame from UDP 7000 transport
    /// liveness. Any recognized RaveSystem on-air OSC packet makes this live immediately; when packets stop,
    /// the receiver switches this back to Standalone and the next <see cref="Update"/> clears to no beat.
    /// </remarks>
    private bool liveBeatActive;

    /// <summary>
    /// Whether the captured frame contains the wire's running one-through-four beat count.
    /// </summary>
    /// <remarks>
    /// Captured once per <see cref="Update"/> so it remains coherent with every public value group.
    /// </remarks>
    public bool IsSynced { get; private set; }

    /// <summary>
    /// Captures the next frame of wire facts and derived musical values using Unity time.
    /// </summary>
    /// <remarks>
    /// Live OSC is the only beat source. In Standalone (no live OSC) <see cref="wireSnapshot"/> is cleared to the
    /// standard no-beat state exposed through <see cref="IsSynced"/>.
    /// </remarks>
    public void Update()
    {
        Update(Time.time);
    }

    /// <summary>
    /// Captures the next frame of wire facts and derived musical values using a supplied clock value.
    /// </summary>
    /// <remarks>
    /// The explicit time overload keeps the Levels shaping (the attack/release follower and the
    /// Peak drain) testable without relying on Unity frame timing.
    /// </remarks>
    public void Update(float timeSeconds)
    {
        wireSnapshot ??= new();

        if (!liveBeatActive)
        {
            ClearToNoBeat();
        }

        IsSynced = wireSnapshot.beatInBar is >= 1 and <= BeatSlotCount;

        // Derivation and shaping run after wireSnapshot has settled for this frame so contrived values
        // never lag the transport by a frame or shape from stale data across a source switch.
        DeriveBeatState();
        UpdateLevelsShaping(timeSeconds);

        // Capture last so every reader sees one frame-coherent musical snapshot.
        CaptureDataSurface();
    }

    /// <summary>
    /// Captures every public value group after wire and derived state have settled.
    /// </summary>
    private void CaptureDataSurface()
    {
        Timing = CaptureTiming();
        Track = CaptureTrack();
        Beats = CaptureBeats();
        Offbeats = CaptureOffbeats();
        Pulses = CapturePulses();
        CapturePhrases();
        Drop = CaptureDrop();
        Fill = CaptureFill();
        CaptureEnergyValues();
        Loop = CaptureLoop();
        Grid = CaptureGrid();
        Levels = CaptureLevels();
    }

    // ---- Shared duration math -----------------------------------------------------------------

    /// <summary>
    /// Beats elapsed from a wire count that includes the current beat, smoothed within the beat.
    /// </summary>
    private float? ElapsedInDuration(int countBeats, int lengthBeats)
    {
        if (lengthBeats <= 0 || countBeats < 0 || countBeats > lengthBeats)
        {
            return null;
        }

        return lengthBeats - countBeats + IntraBeatFraction();
    }

    /// <summary>0..1 position through a duration of the given length.</summary>
    private static float? ProgressOverLength(float? elapsedBeats, int lengthBeats)
    {
        if (elapsedBeats is not { } elapsed || lengthBeats <= 0)
        {
            return null;
        }

        return Mathf.Clamp01(elapsed / lengthBeats);
    }

    /// <summary>
    /// Chooses the beat source: <paramref name="live"/> = live RaveSystem OSC, otherwise Standalone (no beat).
    /// </summary>
    /// <remarks>
    /// Logged on every change of source (never silent). When this turns live off, the next <see cref="Update"/>
    /// clears <see cref="wireSnapshot"/> to the no-beat state; when it turns live on, <see cref="Update"/> stands
    /// aside and lets <see cref="RaveOscReceiver.ApplyTo"/> keep <see cref="wireSnapshot"/> current.
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
    /// <see cref="Update"/> after the live source has written <see cref="wireSnapshot"/>.
    /// </summary>
    private void DeriveBeatState()
    {
        DeriveOffBeats();
    }

    /// <summary>
    /// Resets Levels and the Phrase, Drop, Fill, Energy, Loop, Grid, and Track ID facts to unavailable.
    /// </summary>
    /// <remarks>
    /// Standalone is a no-beat state, not a musical analysis, so it must never present phrase data. Running
    /// this also flushes stale live values left in <see cref="wireSnapshot"/> (including scene-serialized ones)
    /// after a RaveSystem broadcast stops, so the captured groups return null values instead of
    /// replaying the last live Fill/Drop/Energy forever.
    /// </remarks>
    private void ClearPhraseAndLevelState()
    {
        wireSnapshot.levels = PenroseArt.RaveOsc.Levels.Unavailable;
        wireSnapshot.phraseState = PhraseState.Unavailable;
        wireSnapshot.nextPhraseState = LabeledCountdown.Unavailable;
        wireSnapshot.dropState = CountdownState.Unavailable;
        wireSnapshot.fillState = CountdownState.Unavailable;
        wireSnapshot.energyState = LabeledCountdown.Unavailable;
        wireSnapshot.nextEnergyState = LabeledCountdown.Unavailable;
        wireSnapshot.loopState = LoopState.Unavailable;
        wireSnapshot.timingGrid = TimingGrid.Unavailable;
        wireSnapshot.trackId = -1;
    }

    /// <summary>
    /// Clears the wire snapshot to the standard no-beat state. Reached only in Standalone (no live OSC source), so there
    /// is no live OSC source to protect here — live mode is handled before this in <see cref="Update"/>.
    /// </summary>
    private void ClearToNoBeat()
    {
        wireSnapshot.playersLive = "";
        wireSnapshot.track = "";
        wireSnapshot.bpm = UnavailableMs; // wire sentinel: no usable tempo
        wireSnapshot.beat = new BeatPosition { current = -1, total = -1 };
        wireSnapshot.bar = new BarPosition { current = -1, nextMs = -1 };
        wireSnapshot.beatInBar = -1; // real 4-count sentinel (musically 1..4 or -1, never 0); clears IsSynced
        wireSnapshot.beatAverageMs = 0;
        wireSnapshot.beatPulse = 0f;
        wireSnapshot.beatsCountMs = CreateUnavailableCountdowns();
        wireSnapshot.onBeats = new bool[BeatSlotCount];
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
