// The Pulses doorway: the pulse family under one roof, plus the Duration clock math (beat-data spec).

#nullable enable

using UnityEngine;

/// <summary>
/// The pulse family, one Data Surface doorway — the rhythmic heartbeat signals effects animate
/// against, gathered under one roof. Three of the four offerings live here: the wire's beat
/// triangle, the contrived offbeat ramp, and the idealized-clock Duration pulses/gates. The
/// fourth — Waveforms, the authored dance moves — is the synthesizer's. Four offerings, four
/// purposes; none is a duplicate of another. Facts are nullable; edges are never null.
/// </summary>
public readonly struct PulsesView
{
    private readonly bool synced;
    private readonly float beatTriangle;
    private readonly float offBeatRamp;
    private readonly float barPhase;
    private readonly bool[]? durationOpened;

    /// <summary>
    /// The wire's own beat-position signal: a triangle, 1.0 on the hit, 0.0 midway between beats —
    /// a free 0..1 animation ramp needing no local timing math. Wire fact, distinct from the
    /// glossary's Beat Pulse. Availability derives from the clock, never from the value: 0.0 is
    /// both a real trough and the wire's resting default, so the value alone cannot say "unavailable".
    /// </summary>
    public float? Beat => synced ? beatTriangle : (float?)null;

    /// <summary>
    /// The Offbeat pulse: peaks on each "&amp;" and decays — contrived from the measured midpoints
    /// between real beat times.
    /// </summary>
    public float? OffBeat => synced ? offBeatRamp : (float?)null;

    /// <summary>
    /// Duration Pulse: peaks on each onset of the given Duration and decays to 0 across its
    /// cycle — "pulse me every eighth." Idealized clock, instant and parametric.
    /// </summary>
    public float? Every(Duration duration)
    {
        return synced ? DurationClock.Pulse(DurationClock.Phase(barPhase, duration)) : (float?)null;
    }

    /// <summary>
    /// Duration Gate: the square sibling — open for the first <paramref name="duty"/> of each
    /// cycle (strobes, ratchets).
    /// </summary>
    public bool? GateEvery(Duration duration, float duty = 0.25f)
    {
        return synced ? DurationClock.Phase(barPhase, duration) < duty : (bool?)null;
    }

    /// <summary>
    /// Edge: the given Duration's gate opened this frame — its cycle wrapped during continuous
    /// sync. Never null.
    /// </summary>
    public bool GateOpenedEvery(Duration duration)
    {
        return durationOpened != null && (int)duration >= 0 && (int)duration < durationOpened.Length &&
               durationOpened[(int)duration];
    }

    /// <summary>Built only by the hub's per-update capture, with the Duration wrap edges already evaluated.</summary>
    internal PulsesView(bool synced, float beatTriangle, float offBeatRamp, float barPhase, bool[] durationOpened)
    {
        this.synced = synced;
        this.beatTriangle = beatTriangle;
        this.offBeatRamp = offBeatRamp;
        this.barPhase = barPhase;
        this.durationOpened = durationOpened;
    }
}

/// <summary>
/// The idealized clock behind the Duration pulses and gates: each <see cref="Duration"/> as a
/// cycle read against the Bar Phase clock, so every rung locks to the beat at any tempo. Internal
/// machinery — consumers read <see cref="PulsesView"/>.
/// </summary>
internal static class DurationClock
{
    /// <summary>Rungs on the <see cref="Duration"/> ladder, for sizing per-rung capture state.</summary>
    internal const int LadderLength = 5;

    /// <summary>Beats per bar in the wall's common-time model — the whole note spans the measure.</summary>
    private const float BeatsPerBar = 4f;

    /// <summary>A rung's cycle period in beats.</summary>
    internal static float PeriodBeats(Duration duration)
    {
        return duration switch
        {
            Duration.Whole => BeatsPerBar,
            Duration.Half => BeatsPerBar / 2f,
            Duration.Quarter => 1f,
            Duration.Eighth => 0.5f,
            Duration.Sixteenth => 0.25f,
            _ => 1f,
        };
    }

    /// <summary>
    /// Phase within one Duration cycle in [0..1): 0 at each onset, approaching 1 just before the
    /// next — every rung reads the same bar-locked clock.
    /// </summary>
    internal static float Phase(float barPhase, Duration duration)
    {
        var beatsIntoBar = barPhase * BeatsPerBar;
        return Mathf.Repeat(beatsIntoBar / PeriodBeats(duration), 1f);
    }

    /// <summary>
    /// The pulse family's shared decay shape: 1 at the onset, smoothstep-decaying to 0 across the
    /// cycle — the same envelope the offbeat ramp wears, so the family reads as one voice.
    /// </summary>
    internal static float Pulse(float phase)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
    }
}

public partial class BeatManager
{
    /// <summary>
    /// The Pulses doorway, captured once per hub update ahead of effect Draw — identical for every
    /// reader within a frame.
    /// </summary>
    public PulsesView Pulses { get; private set; }

    /// <summary>
    /// Prior observed Duration-cycle phases (one per ladder rung), retained between hub updates so
    /// <see cref="PulsesView.GateOpenedEvery"/> can witness each cycle wrap (ADR-0015).
    /// </summary>
    private readonly float?[] previousDurationPhases = new float?[DurationClock.LadderLength];

    /// <summary>
    /// Captures the Pulses doorway from the settled transport and derived state. The Duration gate
    /// edges are cycle wraps during continuous sync: a phase that stepped backward since the prior
    /// update. Becoming synced is a mode change, not a wrap, so it never fires them.
    /// </summary>
    private PulsesView CapturePulses()
    {
        var synced = IsSynced;
        var barPhase = synced ? BarPhase : (float?)null;

        var opened = new bool[previousDurationPhases.Length];
        for (var rung = 0; rung < opened.Length; rung++)
        {
            var phase = barPhase is { } current ? DurationClock.Phase(current, (Duration)rung) : (float?)null;
            opened[rung] = Edges.Wrapped(previousDurationPhases[rung], phase);
            previousDurationPhases[rung] = phase;
        }

        return new PulsesView(synced, beatData.snapshot.beatPulse, offBeatPulse, barPhase ?? 0f, opened);
    }
}
