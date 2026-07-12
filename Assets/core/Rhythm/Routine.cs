// Routine values: four resolved one-bar Waveforms composing exactly one Grid.

#nullable enable

using System;

/// <summary>
/// A 16-beat choreography of exactly four resolved one-bar Waveforms. The value stores no
/// acquisition settings or lifecycle policy; callers acquire another value explicitly when they
/// choose. The synthesizer only evaluates this value against the hub's captured Grid.
/// </summary>
public sealed class Routine
{
    /// <summary>One Routine is exactly four resolved one-bar Waveforms, one 16-beat Grid.</summary>
    internal const int SlotCount = 4;

    /// <summary>The four resolved Waveforms in Grid bar order.</summary>
    private readonly Waveform[] waveforms;

    /// <summary>Builds one immutable Routine from copied resolved values.</summary>
    private Routine(Waveform[] waveforms)
    {
        this.waveforms = (Waveform[])waveforms.Clone();
    }

    /// <summary>Composes exactly four already-resolved one-bar Waveforms into one immutable value.</summary>
    /// <param name="bar1">The Waveform for Grid bar 1.</param>
    /// <param name="bar2">The Waveform for Grid bar 2.</param>
    /// <param name="bar3">The Waveform for Grid bar 3.</param>
    /// <param name="bar4">The Waveform for Grid bar 4.</param>
    public static Routine Of(Waveform bar1, Waveform bar2, Waveform bar3, Waveform bar4)
    {
        return new Routine(new[] { bar1, bar2, bar3, bar4 });
    }

    /// <summary>Returns one zero-based Grid bar's resolved Waveform.</summary>
    internal Waveform WaveformAt(int index)
    {
        return waveforms[index];
    }
}

/// <summary>
/// One Routine bar's acquisition setting: draw within an Energy set, pin an inline Waveform,
/// or pin a stable Preset name. Silence is an all-zero inline Waveform, never another kind.
/// </summary>
public readonly struct RoutineSlot
{
    /// <summary>The three sanctioned ways a Routine bar acquires its Waveform.</summary>
    internal enum Kind
    {
        /// <summary>Draw from the Waveform Pool within an Energy set.</summary>
        Draw,

        /// <summary>Pin one inline Waveform value.</summary>
        Inline,

        /// <summary>Pin one stable Preset name.</summary>
        Preset,
    }

    /// <summary>How this slot acquires its Waveform.</summary>
    internal Kind Acquisition { get; }

    /// <summary>The Energy set for a draw; empty means the whole Pool.</summary>
    internal Energy[] Levels { get; }

    /// <summary>The inline-pinned value; meaningful only for <see cref="Kind.Inline"/>.</summary>
    internal Waveform PinnedWaveform { get; }

    /// <summary>The stable Preset handle; meaningful only for <see cref="Kind.Preset"/>.</summary>
    internal string? PresetName { get; }

    /// <summary>Builds one immutable acquisition instruction.</summary>
    private RoutineSlot(Kind acquisition, Energy[] levels, Waveform pinnedWaveform, string? presetName)
    {
        Acquisition = acquisition;
        Levels = levels;
        PinnedWaveform = pinnedWaveform;
        PresetName = presetName;
    }

    /// <summary>Requests a Pool draw at this Energy level during explicit Routine acquisition.</summary>
    /// <param name="level">The Energy level to draw within.</param>
    public static RoutineSlot Draw(Energy level)
    {
        return DrawFrom(new[] { level });
    }

    /// <summary>A pinned inline Waveform; this bar always plays the captured value.</summary>
    /// <param name="waveform">The Waveform to pin, including an all-zero Waveform for silence.</param>
    public static RoutineSlot Pin(Waveform waveform)
    {
        return new RoutineSlot(Kind.Inline, Array.Empty<Energy>(), waveform, null);
    }

    /// <summary>Requests a stable Preset name during explicit Routine acquisition.</summary>
    /// <param name="presetName">The exact Preset name.</param>
    public static RoutineSlot Pin(string presetName)
    {
        return new RoutineSlot(Kind.Preset, Array.Empty<Energy>(), default, presetName);
    }

    /// <summary>Builds the set-based draw used by <see cref="WaveformSynth.RandomRoutine"/>.</summary>
    internal static RoutineSlot DrawFrom(Energy[] levels)
    {
        var copiedLevels = levels == null || levels.Length == 0
            ? Array.Empty<Energy>()
            : (Energy[])levels.Clone();
        return new RoutineSlot(Kind.Draw, copiedLevels, default, null);
    }
}
