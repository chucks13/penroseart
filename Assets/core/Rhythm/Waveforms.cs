// Waveform acquisition and evaluation beside BeatManager's musical data surface.

#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides Waveform and Routine acquisition and evaluation beside BeatManager's musical data
/// surface. Consumers hold the values they acquire and decide how those values shape their art.
/// The live Bar Phase and Grid clocks are read internally through BeatManager's doorways.
/// </summary>
/// <remarks>
/// <para>
/// The currency is the Waveform value itself: a consumer acquires one — an Energy-set draw
/// (<see cref="Random"/>), a Preset name (<see cref="ByName"/>), or inline notation
/// (<see cref="Waveform.Parse(string,string)"/>) — holds it, and asks for
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
/// The owner steps the surface once per hub update via <see cref="Update"/>, after BeatManager's
/// own update and ahead of effect Draw — that observation window is what the <see cref="Hit"/>
/// edge reads, so all readers within a frame see the same single-frame truth (ADR-0015 identity
/// rules).
/// </para>
/// </remarks>
public sealed class Waveforms
{
    /// <summary>The hub whose Clock doorway supplies the Bar Phase clock and tempo yardstick.</summary>
    private readonly BeatManager clockSource;

    /// <summary>The Pool entries draws and name lookups read. Never empty — see the constructors.</summary>
    private readonly WaveformPool.Entry[] entries;

    /// <summary>Bar Phase at the previous <see cref="Update"/> — the opening edge of the Hit window.</summary>
    private float? previousPhase;

    /// <summary>Bar Phase at the latest <see cref="Update"/> — the closing edge of the Hit window.</summary>
    private float? currentPhase;

    /// <summary>
    /// The production surface: reads the Pool from the StreamingAssets file through
    /// <see cref="WaveformPool"/>, the single owner of the format.
    /// </summary>
    /// <param name="clockSource">The hub whose Clock doorway supplies the live waveform clock.</param>
    public Waveforms(BeatManager clockSource)
        : this(clockSource, WaveformPool.Parse(WaveformPool.ReadFileOrEmpty()))
    {
    }

    /// <summary>
    /// The dependency-accepting seam: the caller supplies the Pool entries. Tests seed known
    /// entries here; the production constructor routes through it with the parsed file.
    /// </summary>
    /// <param name="clockSource">The hub whose Clock doorway supplies the live waveform clock.</param>
    /// <param name="poolEntries">
    /// The Pool. An empty or null list is logged and degrades to the canonical Beat Pulse
    /// (<c>QQQQ</c> / <c>8888</c>) as the one entry, keeping the "a draw is never null" promise
    /// without duplicating the hub's legacy seed list.
    /// </param>
    public Waveforms(BeatManager clockSource, IReadOnlyList<WaveformPool.Entry> poolEntries)
    {
        this.clockSource = clockSource ?? throw new ArgumentNullException(nameof(clockSource));

        if (poolEntries == null || poolEntries.Count == 0)
        {
            Debug.LogWarning("[Waveforms] the Waveform Pool has no entries — the canonical Beat Pulse stands in.");
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
    /// logged. Never null — the Pool always has entries.
    /// </summary>
    /// <param name="levels">The Energy levels to draw within; empty for the whole Pool.</param>
    public Waveform Random(params Energy[] levels)
    {
        return DrawFromPool(levels);
    }

    /// <summary>
    /// By Preset name. Null when no Pool entry carries the name — the consumer picks its default.
    /// </summary>
    /// <param name="presetName">The Preset's name, matched exactly.</param>
    public Waveform? ByName(string presetName)
    {
        return FindByName(presetName);
    }

    // ── Evaluation — the one primitive ──────────────────────────────────────

    /// <summary>
    /// The envelope of the given Waveform at the current Bar Phase, 0..1. Null with no clock — no
    /// bar position exists, and each consumer chooses its own Standalone response (a fact-read,
    /// not a rest-at-0 envelope). A null Waveform means the consumer holds no rhythm. Brightness
    /// and time seasoning are effect-side.
    /// </summary>
    /// <param name="waveform">The caller-owned Waveform value, or null for "holds no rhythm".</param>
    public float? Evaluate(Waveform? waveform)
    {
        if (waveform is not { } value)
        {
            return null;
        }

        return clockSource.Clock.BarPhase is { } barPhase ? value.Evaluate(barPhase) : (float?)null;
    }

    /// <summary>
    /// Edge: one of this Waveform's humps landed this frame — fires on any shape's actual onsets
    /// (audible hump starts, Phase Offset applied). Never null: with no clock, no prior
    /// observation, or a null Waveform it rests at false. A single-frame truth:
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
    /// <param name="waveform">The caller-owned Waveform value, or null for "holds no rhythm".</param>
    public bool Hit(Waveform? waveform)
    {
        if (waveform is not { } value)
        {
            return false;
        }

        return previousPhase is { } from
            && currentPhase is { } to
            && value.HasAudibleOnsetBetween(from, to);
    }

    /// <summary>
    /// The shortest spacing between the Waveform's audible peaks, in ms at the current tempo —
    /// the yardstick effects scale visuals with, and the measurement the Energy classifier also
    /// consumes (<see cref="Waveform.Energy"/>). Null with no tempo or a null Waveform; 0 for a
    /// silent Waveform, per the kernel's convention.
    /// </summary>
    /// <param name="waveform">The caller-owned Waveform value, or null for "holds no rhythm".</param>
    public float? ShortestPeakSpacingMs(Waveform? waveform)
    {
        if (waveform is not { } value)
        {
            return null;
        }

        return clockSource.Clock.BeatAverageMs is { } beatAverageMs
            ? value.ShortestNonZeroPeakSpacing() * Waveform.BeatsPerBar * beatAverageMs
            : (float?)null;
    }

    // ── Routines (ticket 19) ──────────────────────────────────────────────────

    /// <summary>
    /// Observes the Routine's envelope at the current placed Grid position. The one-based Grid bar
    /// selects one resolved Waveform and the fraction within that bar evaluates it. Null when the
    /// Routine is null or no Grid position exists. Grid State is trust data, never an evaluation
    /// gate.
    /// </summary>
    /// <remarks>
    /// This read never draws, caches, advances a cursor, reconstructs a wrap, or replaces anything.
    /// A caller composes another Routine from resolved Waveforms whenever it wants different bars.
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

        var waveform = routine.WaveformAt(bar - 1);
        var barFraction = Mathf.Repeat(progress * Routine.SlotCount, 1f);
        return waveform.Evaluate(barFraction);
    }

    // ── The frame step ───────────────────────────────────────────────────────

    /// <summary>
    /// Steps this surface's observation of the Bar Phase clock. This owner-only operation is
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

    /// <summary>Draws uniformly across the whole Pool.</summary>
    private Waveform DrawWholePool()
    {
        return entries[UnityEngine.Random.Range(0, entries.Length)].waveform;
    }

    /// <summary>Finds one Pool entry by stable Preset name.</summary>
    private Waveform? FindByName(string? presetName)
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

    /// <summary>
    /// Draws within an Energy set. Empty sets use the whole Pool; unmatched sets log the same
    /// warning as <see cref="Random"/> and fall back likewise.
    /// </summary>
    private Waveform DrawFromPool(Energy[] levels)
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
            Debug.LogWarning($"[Waveforms] no Pool entry matches the requested Energy set " +
                             $"({string.Join(", ", levels)}) — drawing from the whole Pool.");
            return DrawWholePool();
        }

        return entries[matches[UnityEngine.Random.Range(0, matches.Count)]].waveform;
    }
}
