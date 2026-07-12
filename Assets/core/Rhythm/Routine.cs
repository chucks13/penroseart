// Routine values: four one-bar Waveform acquisition slots composing exactly one Grid.

#nullable enable

using System;

/// <summary>
/// A 16-beat choreography of exactly four one-bar slots. Each slot either draws a Waveform from
/// an Energy set or pins an authored Waveform; the synthesizer owns draw resolution and live Grid
/// evaluation. Draws persist until the holder explicitly rerolls or opts into refresh on wrap.
/// </summary>
public sealed class Routine
{
    /// <summary>A Routine is exactly four one-bar slots, one 16-beat Grid.</summary>
    public const int SlotCount = 4;

    /// <summary>The four author-provided slot acquisition instructions, in Grid bar order.</summary>
    private readonly RoutineSlot[] slots;

    /// <summary>Resolved Waveforms for draw slots; pinned slots never use this cache.</summary>
    private readonly Waveform?[] resolvedDraws = new Waveform?[SlotCount];

    /// <summary>The last placed Grid progress this Routine observed; null before its first placed read.</summary>
    internal float? LastObservedGridProgress { get; set; }

    /// <summary>
    /// Whether the synthesizer redraws this Routine's draw slots when two placed observations
    /// witness the Grid position stepping backward. False by default; refresh is holder-chosen.
    /// </summary>
    public bool RefreshOnWrap { get; set; }

    /// <summary>Builds one Routine from exactly four authorable one-bar slots.</summary>
    /// <param name="first">How Grid bar 1 gets its Waveform.</param>
    /// <param name="second">How Grid bar 2 gets its Waveform.</param>
    /// <param name="third">How Grid bar 3 gets its Waveform.</param>
    /// <param name="fourth">How Grid bar 4 gets its Waveform.</param>
    public Routine(RoutineSlot first, RoutineSlot second, RoutineSlot third, RoutineSlot fourth)
    {
        slots = new[] { first, second, third, fourth };
    }

    /// <summary>Returns the author-provided acquisition instruction for one zero-based Grid bar.</summary>
    internal RoutineSlot SlotAt(int index)
    {
        return slots[index];
    }

    /// <summary>Reads a draw slot's resolved Waveform, or null while that slot is unresolved.</summary>
    internal Waveform? ResolvedDrawAt(int index)
    {
        return resolvedDraws[index];
    }

    /// <summary>Caches one genuine Pool draw for a draw slot.</summary>
    internal void SetResolvedDraw(int index, Waveform waveform)
    {
        resolvedDraws[index] = waveform;
    }
}

/// <summary>
/// One Routine bar's Waveform acquisition instruction: draw within an Energy set, or pin one
/// Waveform value. Silence is expressed by pinning an all-zero Waveform, never by another kind.
/// </summary>
public readonly struct RoutineSlot
{
    /// <summary>Whether this slot asks the synthesizer for a Pool draw instead of a pinned value.</summary>
    internal bool IsDraw { get; }

    /// <summary>The Energy set a draw uses; empty means the whole Pool.</summary>
    internal Energy[] Levels { get; }

    /// <summary>The author-pinned value; meaningful only when <see cref="IsDraw"/> is false.</summary>
    internal Waveform PinnedWaveform { get; }

    /// <summary>Builds the immutable acquisition instruction used by the public factories.</summary>
    private RoutineSlot(bool isDraw, Energy[] levels, Waveform pinnedWaveform)
    {
        IsDraw = isDraw;
        Levels = levels;
        PinnedWaveform = pinnedWaveform;
    }

    /// <summary>
    /// Makes a slot that draws from Pool entries at the requested Energy levels; no levels means
    /// the whole Pool. The set is copied so later caller mutation cannot rewrite the Routine.
    /// </summary>
    /// <param name="levels">The Energy levels to draw within; empty for the whole Pool.</param>
    public static RoutineSlot Draw(params Energy[] levels)
    {
        var copiedLevels = levels == null || levels.Length == 0
            ? Array.Empty<Energy>()
            : (Energy[])levels.Clone();
        return new RoutineSlot(true, copiedLevels, default);
    }

    /// <summary>Makes a slot that always carries the given authored Waveform value.</summary>
    /// <param name="waveform">The Waveform to pin, including an all-zero Waveform for silence.</param>
    public static RoutineSlot Pin(Waveform waveform)
    {
        return new RoutineSlot(false, Array.Empty<Energy>(), waveform);
    }
}
