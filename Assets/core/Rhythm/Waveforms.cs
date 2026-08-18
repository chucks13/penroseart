// Waveform acquisition beside BeatManager's musical data surface.

using System;
using System.Collections.Generic;

/// <summary>
/// Acquires immutable, runtime-bound Waveforms from the hand-vetted Pool. A held Waveform or
/// Routine reads its own live envelope and applies caller-chosen response endpoints directly.
/// </summary>
/// <remarks>
/// The Pool is required runtime configuration. Construction fails when it is absent or empty so
/// configuration defects cannot be hidden behind a synthesized rhythm.
/// </remarks>
public sealed class Waveforms
{
    /// <summary>The runtime-bound Waveforms available for acquisition.</summary>
    private readonly Waveform[] waveforms;

    /// <summary>Unique persisted identity of each Waveform, aligned by index, for named acquisition.</summary>
    private readonly string[] names;

    /// <summary>
    /// Explicit non-null Waveform value Mixers assign when they intentionally suppress a child's
    /// rhythmic response.
    /// </summary>
    public Waveform None { get; }

    /// <summary>Reads and validates the production Waveform Pool.</summary>
    /// <param name="clockSource">The shared musical source runtime Waveforms read.</param>
    public Waveforms(BeatManager clockSource)
        : this(clockSource, WaveformPool.Parse(WaveformPool.ReadFileOrEmpty()))
    {
    }

    /// <summary>Creates the acquisition surface from caller-supplied Pool entries with valid, unique persisted names.</summary>
    /// <param name="clockSource">The shared musical source runtime Waveforms read.</param>
    /// <param name="poolEntries">The required, non-empty Pool.</param>
    public Waveforms(BeatManager clockSource, IReadOnlyList<WaveformPool.Entry> poolEntries)
    {
        if (clockSource == null)
        {
            throw new ArgumentNullException(nameof(clockSource));
        }

        if (poolEntries == null)
        {
            throw new ArgumentNullException(nameof(poolEntries));
        }

        if (poolEntries.Count == 0)
        {
            throw new InvalidOperationException(
                $"Waveform Pool '{WaveformPool.FilePath}' contains no Waveforms.");
        }

        for (var i = 0; i < poolEntries.Count; i++)
        {
            var entryName = poolEntries[i].name;
            if (!WaveformPool.IsValidName(entryName))
            {
                throw new InvalidOperationException(
                    $"Waveform Pool '{WaveformPool.FilePath}' contains invalid entry name '{entryName}'. " +
                    "Pool entry names must be non-empty and cannot contain macro delimiters.");
            }
        }

        var duplicateName = WaveformPool.FindDuplicateName(poolEntries);
        if (duplicateName != null)
        {
            throw new InvalidOperationException(
                $"Waveform Pool '{WaveformPool.FilePath}' contains duplicate entry name '{duplicateName}'. " +
                "Pool entry names are persisted identities and must be unique.");
        }

        waveforms = new Waveform[poolEntries.Count];
        names = new string[poolEntries.Count];
        for (var i = 0; i < waveforms.Length; i++)
        {
            var entry = poolEntries[i];
            if (entry.waveform.IsMalformed)
            {
                throw new InvalidOperationException(
                    $"Waveform Pool entry '{entry.name}' is malformed. Fix the Pool before starting the runtime.");
            }

            waveforms[i] = entry.waveform.Bind(clockSource);
            names[i] = entry.name;
        }

        None = Waveform.Disabled(clockSource);
    }

    /// <summary>
    /// Draws one Waveform from the requested Energy levels; no levels means the whole Pool.
    /// </summary>
    /// <param name="levels">The Energy levels eligible for this draw.</param>
    /// <returns>A uniformly selected, runtime-bound Waveform.</returns>
    public Waveform Random(params Energy[] levels)
    {
        if (levels == null)
        {
            throw new ArgumentNullException(nameof(levels));
        }

        if (levels.Length == 0)
        {
            return DrawWholePool();
        }

        var matches = new List<Waveform>(waveforms.Length);
        for (var i = 0; i < waveforms.Length; i++)
        {
            var waveform = waveforms[i];
            if (Array.IndexOf(levels, waveform.Energy) >= 0)
            {
                matches.Add(waveform);
            }
        }

        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Waveform Pool contains no entry at the requested Energy levels ({string.Join(", ", levels)}).");
        }

        return matches[UnityEngine.Random.Range(0, matches.Count)];
    }

    /// <summary>
    /// Draws the one Pool Preset identified by the given unique persisted name, for a performer that holds one selected
    /// Waveform instead of drawing randomly. A name missing from the Pool — including a Pool-side
    /// rename of a selected entry — is a configuration error and fails visibly rather than
    /// widening to a fallback.
    /// </summary>
    /// <param name="name">The exact Pool entry name.</param>
    /// <returns>The runtime-bound Waveform saved under that name.</returns>
    public Waveform Named(string name)
    {
        for (var i = 0; i < names.Length; i++)
        {
            if (names[i] == name)
            {
                return waveforms[i];
            }
        }

        throw new InvalidOperationException(
            $"Waveform Pool '{WaveformPool.FilePath}' contains no entry named '{name}'.");
    }

    /// <summary>Draws uniformly across the whole Pool.</summary>
    /// <returns>A runtime-bound Waveform from the Pool.</returns>
    private Waveform DrawWholePool()
    {
        return waveforms[UnityEngine.Random.Range(0, waveforms.Length)];
    }
}
