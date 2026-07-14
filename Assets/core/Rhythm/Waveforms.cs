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
    /// <summary>The runtime-bound Waveforms available for random acquisition.</summary>
    private readonly Waveform[] waveforms;

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

    /// <summary>Creates the acquisition surface from caller-supplied Pool entries.</summary>
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

        waveforms = new Waveform[poolEntries.Count];
        for (var i = 0; i < waveforms.Length; i++)
        {
            var entry = poolEntries[i];
            if (entry.waveform.IsMalformed)
            {
                throw new InvalidOperationException(
                    $"Waveform Pool entry '{entry.name}' is malformed. Fix the Pool before starting the runtime.");
            }

            waveforms[i] = entry.waveform.Bind(clockSource);
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

    /// <summary>Draws uniformly across the whole Pool.</summary>
    /// <returns>A runtime-bound Waveform from the Pool.</returns>
    private Waveform DrawWholePool()
    {
        return waveforms[UnityEngine.Random.Range(0, waveforms.Length)];
    }
}
