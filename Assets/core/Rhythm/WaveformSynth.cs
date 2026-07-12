// The Waveform Synthesizer: the sibling readable surface beside BeatManager (beat-data spec).

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The wall's own instrument — the always-running Waveform Synthesizer, its own surface root
/// beside BeatManager's Data Surface: BeatManager answers "what is the music doing", this surface
/// answers "play my rhythm". It acquires and evaluates both one-bar Waveforms and four-bar
/// Routines. Readable by any holder of the reference; it consumes the Bar Phase and Grid clocks
/// internally through the hub's doorways.
/// </summary>
/// <remarks>
/// <para>
/// The currency is the Waveform value itself: a consumer acquires one — an Energy-set draw
/// (<see cref="Random"/>), a Preset name (<see cref="ByName"/>), or inline notation
/// (<see cref="Waveform.Parse(string,string)"/>, no synthesizer involved) — holds it, and asks for
/// its value now with the one primitive, <see cref="Evaluate"/>. Index addressing does not exist
/// in any form: a Pool position may change at any time; names and values are the handles.
/// </para>
/// <para>
/// Draws come from the Pool, the hand-vetted Preset set read once from the StreamingAssets file
/// (see <see cref="WaveformPool"/>) — so a random pick is always musically sensible. A pool with
/// no entries degrades to the canonical Beat Pulse, logged, so a draw is never null. Tests seed
/// known entries through the dependency-accepting constructor.
/// </para>
/// <para>
/// The Waveform Hold (<see cref="Hold"/> / <see cref="ReleaseToAuto"/>) is the synthesizer's one
/// inbound knob — control sitting apart from the data reads, actuated by editor surfaces
/// (ADR-0016). While engaged it pins every draw and every evaluation of a non-null Waveform
/// argument to the held value; null still means the consumer holds no rhythm.
/// </para>
/// <para>
/// The owner steps the surface once per hub update via <see cref="Update"/>, after BeatManager's
/// own update and ahead of effect Draw — that observation window is what the <see cref="Hit"/>
/// edge reads, so all readers within a frame see the same single-frame truth (ADR-0015 identity
/// rules).
/// </para>
/// </remarks>
public sealed class WaveformSynth
{
    /// <summary>The hub whose Clock doorway supplies the Bar Phase clock and tempo yardstick.</summary>
    private readonly BeatManager clockSource;

    /// <summary>The Pool entries draws and name lookups read. Never empty — see the constructors.</summary>
    private readonly WaveformPool.Entry[] entries;

    /// <summary>The Waveform Hold's held value; null is the released state (Auto).</summary>
    private Waveform? held;

    /// <summary>Bar Phase at the previous <see cref="Update"/> — the opening edge of the Hit window.</summary>
    private float? previousPhase;

    /// <summary>Bar Phase at the latest <see cref="Update"/> — the closing edge of the Hit window.</summary>
    private float? currentPhase;

    /// <summary>
    /// The production surface: reads the Pool from the StreamingAssets file through
    /// <see cref="WaveformPool"/>, the single owner of the format.
    /// </summary>
    /// <param name="clockSource">The hub whose Clock doorway the synthesizer consumes internally.</param>
    public WaveformSynth(BeatManager clockSource)
        : this(clockSource, WaveformPool.Parse(WaveformPool.ReadFileOrEmpty()))
    {
    }

    /// <summary>
    /// The dependency-accepting seam: the caller supplies the Pool entries. Tests seed known
    /// entries here; the production constructor routes through it with the parsed file.
    /// </summary>
    /// <param name="clockSource">The hub whose Clock doorway the synthesizer consumes internally.</param>
    /// <param name="poolEntries">
    /// The Pool. An empty or null list is logged and degrades to the canonical Beat Pulse
    /// (<c>QQQQ</c> / <c>8888</c>) as the one entry, keeping the "a draw is never null" promise
    /// without duplicating the hub's legacy seed list.
    /// </param>
    public WaveformSynth(BeatManager clockSource, IReadOnlyList<WaveformPool.Entry> poolEntries)
    {
        this.clockSource = clockSource ?? throw new ArgumentNullException(nameof(clockSource));

        if (poolEntries == null || poolEntries.Count == 0)
        {
            Debug.LogWarning("[WaveformSynth] the Waveform Pool has no entries — the canonical Beat Pulse stands in.");
            entries = new[] { new WaveformPool.Entry("beat pulse", Waveform.Parse("QQQQ", "8888")) };
            return;
        }

        entries = new WaveformPool.Entry[poolEntries.Count];
        for (var i = 0; i < entries.Length; i++)
        {
            entries[i] = poolEntries[i];
        }
    }

    // ── Acquisition ─────────────────────────────────────────────────────────

    /// <summary>
    /// Random draw from Pool entries at the given Energy levels; no args = the whole Pool. Every
    /// subset is expressible; an Energy set no entry matches falls back to a whole-Pool draw,
    /// logged. Never null — the Pool always has entries. The Waveform Hold overrides the draw
    /// while engaged.
    /// </summary>
    /// <param name="levels">The Energy levels to draw within; empty for the whole Pool.</param>
    public Waveform Random(params Energy[] levels)
    {
        if (held is { } heldValue)
        {
            return heldValue;
        }

        return DrawUnheld(levels);
    }

    /// <summary>
    /// By Preset name. Null when no Pool entry carries the name — the consumer picks its default.
    /// While the Waveform Hold is engaged a known name reads the held value (the Hold pins the
    /// whole wall); a miss still reads null — the Hold never invents a Pool entry.
    /// </summary>
    /// <param name="presetName">The Preset's name, matched exactly.</param>
    public Waveform? ByName(string presetName)
    {
        var waveform = FindUnheldByName(presetName);
        if (waveform == null)
        {
            return null;
        }

        return held ?? waveform;
    }

    // ── Evaluation — the one primitive ──────────────────────────────────────

    /// <summary>
    /// The envelope of the given Waveform at the current Bar Phase, 0..1. While the Waveform Hold
    /// is engaged, a non-null argument evaluates as the held value. Null with no clock — no bar
    /// position exists, and each consumer chooses its own Standalone response (a fact-read, not a
    /// rest-at-0 envelope). A null Waveform — a consumer holding no rhythm — still reads null; the
    /// Hold never invents one. Brightness and time seasoning are effect-side.
    /// </summary>
    /// <param name="waveform">The held Waveform value, or null for "holds no rhythm".</param>
    public float? Evaluate(Waveform? waveform)
    {
        if (waveform is not { } value)
        {
            return null;
        }

        if (held is { } heldValue)
        {
            value = heldValue;
        }

        return clockSource.Clock.BarPhase is { } barPhase ? value.Evaluate(barPhase) : (float?)null;
    }

    /// <summary>
    /// Edge: one of this Waveform's humps landed this frame — fires on any shape's actual onsets
    /// (audible hump starts, Phase Offset applied). While the Waveform Hold is engaged, a non-null
    /// argument reads the held value's onsets. Never null: with no clock, no prior observation, or
    /// a null Waveform it rests at false; the Hold never invents a rhythm. A single-frame truth:
    /// true during exactly the frame whose observation window crossed an onset (per-count wire
    /// gates remain for count-specific wants).
    /// </summary>
    /// <remarks>
    /// The window is the Bar Phase step between the last two <see cref="Update"/> calls —
    /// identical for every reader within a frame. A backward step is read as the bar wrapping
    /// (the hub's edge identity rule for cycles), so the window covers the downbeat; the first
    /// observation of a running clock opens no window — the music crossed that boundary
    /// unobserved.
    /// </remarks>
    /// <param name="waveform">The held Waveform value, or null for "holds no rhythm".</param>
    public bool Hit(Waveform? waveform)
    {
        if (waveform is not { } value)
        {
            return false;
        }

        if (held is { } heldValue)
        {
            value = heldValue;
        }

        return previousPhase is { } from
            && currentPhase is { } to
            && value.HasAudibleOnsetBetween(from, to);
    }

    /// <summary>
    /// The shortest spacing between the Waveform's audible peaks, in ms at the current tempo —
    /// the yardstick effects scale visuals with, and the measurement the Energy classifier also
    /// consumes (<see cref="Waveform.Energy"/>). While the Waveform Hold is engaged, a non-null
    /// argument measures the held value. Null with no tempo or a null Waveform — the Hold never
    /// invents a rhythm; 0 for a silent Waveform, per the kernel's convention.
    /// </summary>
    /// <param name="waveform">The held Waveform value, or null for "holds no rhythm".</param>
    public float? ShortestPeakSpacingMs(Waveform? waveform)
    {
        if (waveform is not { } value)
        {
            return null;
        }

        if (held is { } heldValue)
        {
            value = heldValue;
        }

        return clockSource.Clock.BeatAverageMs is { } beatAverageMs
            ? value.ShortestNonZeroPeakSpacing() * Waveform.BeatsPerBar * beatAverageMs
            : (float?)null;
    }

    // ── Routines (ticket 19) ──────────────────────────────────────────────────

    /// <summary>
    /// Explicitly acquires a four-bar Routine whose bars each draw within the requested Energy
    /// set; no levels means the whole Pool. All draws resolve before return and bypass the
    /// Waveform Hold, so release reveals the Routine's genuine values.
    /// </summary>
    /// <param name="levels">The Energy levels each slot draws within; empty for the whole Pool.</param>
    public Routine RandomRoutine(params Energy[] levels)
    {
        return Routine.Of(
            DrawUnheld(levels),
            DrawUnheld(levels),
            DrawUnheld(levels),
            DrawUnheld(levels));
    }

    /// <summary>
    /// Explicitly acquires one immutable Routine from four caller-owned settings. Energy slots
    /// draw from the Pool, Preset-name pins resolve by stable name, and inline pins keep their
    /// values. Null when any Preset name is missing. Resolution bypasses the Waveform Hold so
    /// release reveals the Routine's genuine values. This method carries no replacement policy.
    /// </summary>
    /// <param name="bar1">How Grid bar 1 acquires its Waveform.</param>
    /// <param name="bar2">How Grid bar 2 acquires its Waveform.</param>
    /// <param name="bar3">How Grid bar 3 acquires its Waveform.</param>
    /// <param name="bar4">How Grid bar 4 acquires its Waveform.</param>
    public Routine? CreateRoutine(
        RoutineSlot bar1,
        RoutineSlot bar2,
        RoutineSlot bar3,
        RoutineSlot bar4)
    {
        if (!TryResolveRoutineSlot(bar1, out var waveform1)
            || !TryResolveRoutineSlot(bar2, out var waveform2)
            || !TryResolveRoutineSlot(bar3, out var waveform3)
            || !TryResolveRoutineSlot(bar4, out var waveform4))
        {
            return null;
        }

        return Routine.Of(waveform1, waveform2, waveform3, waveform4);
    }

    /// <summary>
    /// Observes the Routine's envelope at the current placed Grid position. The one-based Grid bar
    /// selects one resolved Waveform and the fraction within that bar evaluates it. Null when the
    /// Routine is null or no Grid position exists. Grid State is trust data, never an evaluation
    /// gate.
    /// </summary>
    /// <remarks>
    /// This read never draws, caches, advances a cursor, reconstructs a wrap, or replaces anything.
    /// A caller that wants another value invokes an acquisition method with its chosen settings
    /// under any condition it owns. The Waveform Hold substitutes at evaluation without rewriting
    /// the Routine.
    /// </remarks>
    /// <param name="routine">The holder-owned Routine, or null when the consumer holds none.</param>
    public float? Evaluate(Routine? routine)
    {
        if (routine == null
            || clockSource.Grid.Current is not { } facts
            || facts.Bar is not { } bar
            || facts.Progress is not { } progress
            || bar < 1
            || bar > Routine.SlotCount)
        {
            return null;
        }

        var waveform = held ?? routine.WaveformAt(bar - 1);
        var barFraction = Mathf.Repeat(progress * Routine.SlotCount, 1f);
        return waveform.Evaluate(barFraction);
    }

    // ── The frame step ───────────────────────────────────────────────────────

    /// <summary>
    /// Steps the synthesizer's observation of the Bar Phase clock. This owner-only operation is
    /// internal so consumers cannot advance the window; the runtime owner calls it once per hub
    /// update, after BeatManager's own update and ahead of effect Draw. The step from the previous
    /// observation to this one is the window the <see cref="Hit"/> edge reads, so the edge is
    /// frame-coherent across every reader.
    /// </summary>
    internal void Update()
    {
        previousPhase = currentPhase;
        currentPhase = clockSource.Clock.BarPhase;
    }

    // ── Inbound knob — control, not data (ADR-0016) ─────────────────────────

    /// <summary>
    /// The Waveform Hold: pin the whole wall to one Waveform value to examine it. Every draw and
    /// every evaluation of a non-null Waveform argument uses the held value while engaged;
    /// retargeting is immediate. Null evaluation arguments still mean the consumer holds no
    /// rhythm. An inspection affordance the runtime owns and editor surfaces actuate.
    /// </summary>
    /// <param name="waveform">The Waveform value to hold — a value, never a Pool position.</param>
    public void Hold(Waveform waveform)
    {
        held = waveform;
    }

    /// <summary>
    /// Clears the Waveform Hold (Auto). Subsequent single-Waveform acquisitions and all
    /// evaluations pass through caller values again; no consumer is instructed to reacquire.
    /// </summary>
    public void ReleaseToAuto()
    {
        held = null;
    }

    /// <summary>Draws uniformly across the whole Pool.</summary>
    private Waveform DrawWholePool()
    {
        return entries[UnityEngine.Random.Range(0, entries.Length)].waveform;
    }

    /// <summary>Finds one Pool entry by stable Preset name without applying the Waveform Hold.</summary>
    private Waveform? FindUnheldByName(string? presetName)
    {
        if (string.IsNullOrEmpty(presetName))
        {
            return null;
        }

        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].name == presetName)
            {
                return entries[i].waveform;
            }
        }

        return null;
    }

    /// <summary>Resolves one caller-authored Routine slot without applying the Waveform Hold.</summary>
    private bool TryResolveRoutineSlot(RoutineSlot slot, out Waveform waveform)
    {
        switch (slot.Acquisition)
        {
            case RoutineSlot.Kind.Draw:
                waveform = DrawUnheld(slot.Levels);
                return true;
            case RoutineSlot.Kind.Inline:
                waveform = slot.PinnedWaveform;
                return true;
            case RoutineSlot.Kind.Preset:
                if (FindUnheldByName(slot.PresetName) is { } namedWaveform)
                {
                    waveform = namedWaveform;
                    return true;
                }

                waveform = default;
                return false;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Draws within an Energy set without consulting the Waveform Hold. Empty sets use the whole
    /// Pool; unmatched sets log the same warning as <see cref="Random"/> and fall back likewise.
    /// </summary>
    private Waveform DrawUnheld(Energy[] levels)
    {
        if (levels == null || levels.Length == 0)
        {
            return DrawWholePool();
        }

        var matches = new List<int>(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            if (Array.IndexOf(levels, entries[i].waveform.Energy) >= 0)
            {
                matches.Add(i);
            }
        }

        if (matches.Count == 0)
        {
            Debug.LogWarning($"[WaveformSynth] no Pool entry matches the requested Energy set " +
                             $"({string.Join(", ", levels)}) — drawing from the whole Pool.");
            return DrawWholePool();
        }

        return entries[matches[UnityEngine.Random.Range(0, matches.Count)]].waveform;
    }
}
