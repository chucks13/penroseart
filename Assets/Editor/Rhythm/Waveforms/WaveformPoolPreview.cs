// Honest editor preview state for the runtime-required Waveform Pool.

using System;

/// <summary>An immutable, editor-only view of whether the required runtime Waveform Pool can be previewed honestly.</summary>
/// <remarks>
/// The preview exposes only entries the runtime could use as a complete required configuration. Missing, empty,
/// structurally lossy, or notation-malformed Pools remain explicit failures instead of gaining a synthetic fallback.
/// </remarks>
internal readonly struct WaveformPoolPreview
{
    /// <summary>The usable Pool entries, or an empty array when <see cref="Error"/> explains a failure.</summary>
    public readonly WaveformPool.Entry[] Entries;

    /// <summary>The user-facing required-configuration failure, or empty when the Pool is usable.</summary>
    public readonly string Error;

    /// <summary>Creates one immutable preview result.</summary>
    /// <param name="entries">The complete usable Pool.</param>
    /// <param name="error">The configuration failure, or empty for a usable Pool.</param>
    private WaveformPoolPreview(WaveformPool.Entry[] entries, string error)
    {
        Entries = entries;
        Error = error;
    }

    /// <summary>Whether a complete non-empty Pool is available for runtime-faithful preview.</summary>
    public bool IsUsable => Entries.Length > 0 && string.IsNullOrEmpty(Error);

    /// <summary>Builds preview state from one exact Pool document without logging parser diagnostics.</summary>
    /// <param name="text">The complete Pool file content.</param>
    /// <param name="fileExists">Whether the required Pool file exists.</param>
    public static WaveformPoolPreview FromText(string text, bool fileExists)
    {
        if (!fileExists)
        {
            return Unavailable($"Required Waveform Pool '{WaveformPool.FileName}' is missing.");
        }

        var parsed = WaveformPool.Parse(text, out var diagnostics);
        if (WaveformPoolDocument.HasUnrecoverableRecord(diagnostics) && parsed.Count == 0)
        {
            return Unavailable("The Waveform Pool contains source that could not be represented safely. " +
                               "Fix the Pool before starting or previewing the runtime.");
        }

        if (parsed.Count == 0)
        {
            return Unavailable($"Required Waveform Pool '{WaveformPool.FileName}' contains no Waveforms.");
        }

        var entries = parsed.ToArray();
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i].waveform.IsMalformed)
            {
                return Unavailable($"Waveform Pool entry '{entries[i].name}' is malformed. " +
                                   "Fix the Pool before starting or previewing the runtime.");
            }
        }

        return new WaveformPoolPreview(entries, "");
    }

    /// <summary>Builds an explicit preview failure for file I/O that prevented reading exact content.</summary>
    /// <param name="error">The user-facing read failure.</param>
    public static WaveformPoolPreview Unreadable(string error)
    {
        return Unavailable(string.IsNullOrWhiteSpace(error)
            ? $"Required Waveform Pool '{WaveformPool.FileName}' could not be read."
            : error);
    }

    /// <summary>Creates an unavailable preview with no selectable or synthetic entries.</summary>
    /// <param name="error">The required-configuration failure.</param>
    private static WaveformPoolPreview Unavailable(string error)
    {
        return new WaveformPoolPreview(Array.Empty<WaveformPool.Entry>(), error);
    }
}
