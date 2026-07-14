using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure display model for the BeatManager Rhythm dashboard.
/// </summary>
/// <remarks>
/// The Unity <see cref="UnityEditor.PropertyDrawer"/> adapter resolves editor-only context, then this module
/// converts a live <see cref="BeatManager"/> into stable dashboard facts: authoritative mode, independent
/// availability, responsive grouping, Grid placement, countdowns, and Waveform health.
/// Keeping these decisions out of IMGUI gives tests the same seam rendered by the Tuning Window and drawer.
/// </remarks>
internal readonly struct BeatManagerDashboardModel
{
    public const int BeatSlotCount = 4;

    private const string DotFilled = "●";
    private const string DotEmpty = "○";
    /// <summary>The canonical label for one unavailable display fact.</summary>
    internal const string UnavailableText = "Unavailable";
    private const int UnavailableBeat = -1;
    /// <summary>The practical width at which four Routine bars can remain readable side by side.</summary>
    private const float SplitLayoutMinWidth = 820f;

    /// <summary>The stable scan order shared by narrow and wide dashboard layouts.</summary>
    private static readonly IReadOnlyList<RhythmDashboardGroup> DisplayGroups = Array.AsReadOnly(new[]
    {
        RhythmDashboardGroup.Timing,
        RhythmDashboardGroup.Waveform,
        RhythmDashboardGroup.Routine,
    });

    /// <summary>Whether the canonical rhythm hub currently serves a live four-count.</summary>
    public readonly bool Synced;
    public readonly string BadgeText;
    public readonly string TrackText;
    public readonly string HeaderRightText;
    public readonly int BeatInBar;
    /// <summary>The current count's On Beat gate, or null when that lane is unavailable.</summary>
    public readonly bool? OnBeat;
    /// <summary>The wire Beat Pulse, or null when the sender did not provide it.</summary>
    public readonly float? BeatPulse;
    /// <summary>The four nullable Off Beat gates in one-based musical order.</summary>
    private readonly bool?[] offBeatGates;
    /// <summary>The derived Off Beat pulse, or null when its timing inputs are unavailable.</summary>
    public readonly float? OffBeatPulse;
    /// <summary>The strongest available Beat or Off Beat pulse, or null when neither exists.</summary>
    public readonly float? EighthPulse;
    public readonly CountdownChipView NextBeat;
    public readonly CountdownChipView OnBeatGate;
    public readonly CountdownChipView NextOffBeat;
    public readonly CountdownChipView OffBeatGate;
    /// <summary>The live bar phase, or null when its timing inputs are unavailable.</summary>
    public readonly float? BarPhase;
    public readonly EnvelopeRowView Envelope;
    /// <summary>The sender-provided one-based Grid position.</summary>
    public readonly GridRowView Grid;
    /// <summary>Whether the required Waveform Pool can provide an honest preview.</summary>
    public readonly bool PoolHealthy;
    /// <summary>The required-Pool configuration failure, or empty when healthy.</summary>
    public readonly string PoolError;
    /// <summary>The semantic section order rendered by both responsive flows.</summary>
    public readonly IReadOnlyList<RhythmDashboardGroup> Groups;

    /// <summary>Captures one immutable set of downstream Inspector display facts.</summary>
    private BeatManagerDashboardModel(
        bool synced,
        string badgeText,
        string trackText,
        string headerRightText,
        int beatInBar,
        bool? onBeat,
        float? beatPulse,
        bool?[] offBeatGates,
        float? offBeatPulse,
        float? eighthPulse,
        CountdownChipView nextBeat,
        CountdownChipView onBeatGate,
        CountdownChipView nextOffBeat,
        CountdownChipView offBeatGate,
        float? barPhase,
        EnvelopeRowView envelope,
        GridRowView grid,
        bool poolHealthy,
        string poolError)
    {
        Synced = synced;
        BadgeText = badgeText;
        TrackText = trackText;
        HeaderRightText = headerRightText;
        BeatInBar = beatInBar;
        OnBeat = onBeat;
        BeatPulse = beatPulse;
        this.offBeatGates = offBeatGates;
        OffBeatPulse = offBeatPulse;
        EighthPulse = eighthPulse;
        NextBeat = nextBeat;
        OnBeatGate = onBeatGate;
        NextOffBeat = nextOffBeat;
        OffBeatGate = offBeatGate;
        BarPhase = barPhase;
        Envelope = envelope;
        Grid = grid;
        PoolHealthy = poolHealthy;
        PoolError = poolError;
        Groups = DisplayGroups;
    }

    /// <summary>
    /// Builds the dashboard display model from the live runtime object and the previewed Pool entry.
    /// </summary>
    /// <param name="beatManager">Runtime beat manager, or <c>null</c> when the property cannot resolve one.</param>
    /// <param name="previewWaveform">The parsed Pool entry selected for editor-only preview, or null when unusable.</param>
    /// <param name="poolError">The required-Pool failure, or empty when the Pool is usable.</param>
    public static BeatManagerDashboardModel From(BeatManager beatManager, Waveform? previewWaveform, string poolError)
    {
        var synced = beatManager != null && beatManager.IsSynced;
        var poolHealthy = previewWaveform.HasValue && string.IsNullOrEmpty(poolError);
        if (!synced)
        {
            return new BeatManagerDashboardModel(
                false,
                "STANDALONE MODE",
                UnavailableText,
                UnavailableText,
                UnavailableBeat,
                null,
                null,
                new bool?[BeatSlotCount],
                null,
                null,
                UnavailableChip("NEXT BEAT", alignValueRight: true),
                UnavailableChip("ON BEAT"),
                UnavailableChip("NEXT OFF BEAT", alignValueRight: true),
                UnavailableChip("OFF BEAT"),
                null,
                EnvelopeRowView.Null,
                GridRowView.Null,
                poolHealthy,
                poolError ?? string.Empty);
        }

        var timing = beatManager != null ? beatManager.Timing : default;
        var beats = beatManager != null ? beatManager.Beats : default;
        var offbeats = beatManager != null ? beatManager.Offbeats : default;
        var pulses = beatManager != null ? beatManager.Pulses : default;
        var track = beatManager != null ? beatManager.Track : default;
        var bpmText = timing.Bpm is { } bpm ? $"{bpm:0.##} BPM" : UnavailableText;
        var rightText = track.PlayersLive is { Count: > 0 } players ? $"{string.Join(",", players)} · {bpmText}" : bpmText;
        var beatPulse = ClampPulse(pulses.Beat);
        var offBeatPulse = ClampPulse(pulses.OffBeat);
        var offBeatGates = new bool?[BeatSlotCount];
        for (var slot = 0; slot < offBeatGates.Length; slot++)
        {
            offBeatGates[slot] = offbeats.OffBeat(slot + 1);
        }

        var count = timing.BeatInBar ?? UnavailableBeat;
        var nextCount = count is >= 1 and <= BeatSlotCount ? (count % BeatSlotCount) + 1 : UnavailableBeat;
        var nextBeatMs = nextCount > 0 ? beats.OnBeatMs(nextCount) : null;
        var nextOffbeatMs = MinimumOffbeatMilliseconds(offbeats);
        var onBeat = count > 0 ? beats.OnBeat(count) : null;
        var offBeat = count > 0 ? offbeats.OffBeat(count) : null;
        var barPhase = timing.BarProgress;

        return new BeatManagerDashboardModel(
            synced,
            "SYNCED MODE",
            track.Title ?? UnavailableText,
            rightText,
            count,
            onBeat,
            beatPulse,
            offBeatGates,
            offBeatPulse,
            GetAvailableEighthPulse(beatPulse, offBeatPulse),
            new CountdownChipView("NEXT BEAT", FormatMs(nextBeatMs), alignValueRight: true),
            new CountdownChipView("ON BEAT", FormatGate(onBeat)),
            new CountdownChipView("NEXT OFF BEAT", FormatMs(nextOffbeatMs), alignValueRight: true),
            new CountdownChipView("OFF BEAT", FormatGate(offBeat)),
            barPhase,
            BuildEnvelopeRow(beatManager, previewWaveform),
            BuildGridRow(beatManager),
            poolHealthy,
            poolError ?? string.Empty);
    }

    /// <summary>Selects stacked content below the practical split-layout width.</summary>
    public static RhythmDashboardFlow FlowForWidth(float width)
    {
        return width >= SplitLayoutMinWidth ? RhythmDashboardFlow.Split : RhythmDashboardFlow.Stacked;
    }

    /// <summary>Creates an unavailable timing chip without implying a false negative gate.</summary>
    private static CountdownChipView UnavailableChip(string label, bool alignValueRight = false)
    {
        return new CountdownChipView(label, UnavailableText, alignValueRight);
    }

    /// <summary>Classifies one beat label against the current synchronized bar position.</summary>
    public BeatMarkerState GetBeatMarkerState(int beatLabel)
    {
        if (!Synced || BeatInBar < 1 || BeatInBar > BeatSlotCount)
        {
            return BeatMarkerState.Disabled;
        }

        if (beatLabel < BeatInBar)
        {
            return BeatMarkerState.Past;
        }

        return beatLabel == BeatInBar ? BeatMarkerState.Current : BeatMarkerState.Future;
    }

    /// <summary>Returns the filled or empty glyph for one beat label.</summary>
    public string GetBeatGlyph(int beatLabel)
    {
        return BuildBeatDotGlyph(Synced, BeatInBar, beatLabel);
    }

    /// <summary>Returns the zero-based Off Beat gate, or null when that lane is unavailable.</summary>
    public bool? OffBeatGateAt(int index)
    {
        return Synced && offBeatGates != null && index >= 0 && index < offBeatGates.Length
            ? offBeatGates[index]
            : null;
    }

    /// <summary>
    /// Builds the four-dot beat row as one glyph string: RaveSystem-style filled dots up to the current
    /// musical beat, empty-dot placeholders when the clock is inactive or the beat label is unknown.
    /// </summary>
    internal static string BuildBeatDotGlyphs(bool active, int beatInBar)
    {
        var glyphs = string.Empty;
        for (var beatLabel = 1; beatLabel <= BeatSlotCount; beatLabel++)
        {
            glyphs += BuildBeatDotGlyph(active, beatInBar, beatLabel);
        }

        return glyphs;
    }

    /// <summary>Returns the stronger beat/offbeat pulse after clamping both inputs to the 0..1 Inspector meter range.</summary>
    internal static float GetClampedEighthPulseValue(float beatPulse, float offBeatPulse)
    {
        return Mathf.Max(Mathf.Clamp01(beatPulse), Mathf.Clamp01(offBeatPulse));
    }

    /// <summary>Returns the stronger available pulse, preserving absence when both inputs are missing.</summary>
    private static float? GetAvailableEighthPulse(float? beatPulse, float? offBeatPulse)
    {
        return beatPulse.HasValue || offBeatPulse.HasValue
            ? GetClampedEighthPulseValue(beatPulse ?? 0f, offBeatPulse ?? 0f)
            : null;
    }

    /// <summary>Clamps one available pulse while preserving an unavailable lane.</summary>
    private static float? ClampPulse(float? pulse)
    {
        return pulse is { } value ? Mathf.Clamp01(value) : null;
    }

    /// <summary>Formats one nullable timing gate without turning absence into a false negative.</summary>
    private static string FormatGate(bool? active)
    {
        return active is { } value ? value ? "YES" : "NO" : UnavailableText;
    }

    private static string BuildBeatDotGlyph(bool active, int beatInBar, int beatLabel)
    {
        return active && beatLabel >= 1 && beatLabel <= beatInBar && beatInBar <= BeatSlotCount ? DotFilled : DotEmpty;
    }

    /// <summary>Formats a nullable millisecond countdown for a compact dashboard chip.</summary>
    private static string FormatMs(float? value)
    {
        return value is { } ms ? $"{ms:0}ms" : UnavailableText;
    }

    /// <summary>Returns the nearest available offbeat countdown across musical labels one through four.</summary>
    private static int? MinimumOffbeatMilliseconds(OffbeatsValues offbeats)
    {
        int? minimum = null;
        for (var count = 1; count <= BeatSlotCount; count++)
        {
            if (offbeats.OffBeatMs(count) is { } value && (minimum == null || value < minimum))
            {
                minimum = value;
            }
        }
        return minimum;
    }

    /// <summary>Builds the live-clock envelope row for the editor-previewed Pool entry.</summary>
    /// <param name="beatManager">The live runtime source, or null when unavailable.</param>
    /// <param name="previewWaveform">The valid editor preview, or null for a required-Pool failure.</param>
    private static EnvelopeRowView BuildEnvelopeRow(BeatManager beatManager, Waveform? previewWaveform)
    {
        if (beatManager == null || previewWaveform is not { } waveform)
        {
            return EnvelopeRowView.Null;
        }

        var envelope = waveform.Bind(beatManager).Envelope;
        return new EnvelopeRowView(true, envelope, $"{envelope:0.00} · preview");
    }

    /// <summary>Builds the sender-provided one-based Grid position without synthesizing placement.</summary>
    private static GridRowView BuildGridRow(BeatManager beatManager)
    {
        if (beatManager == null || beatManager.Grid.State is not { } state ||
            beatManager.Grid.Bar is not { } bar || beatManager.Grid.Beat is not { } beat)
        {
            return GridRowView.Null;
        }

        return new GridRowView(
            true,
            state.ToString().ToUpperInvariant(),
            beatManager.Grid.Progress ?? 0f,
            $"Bar {bar} · Beat {beat}");
    }

}

internal enum BeatMarkerState
{
    Disabled,
    Past,
    Current,
    Future,
}

/// <summary>The semantic scan groups shared by the responsive Rhythm dashboard layouts.</summary>
internal enum RhythmDashboardGroup
{
    /// <summary>Track, tempo, beat/offbeat timing, and Grid placement.</summary>
    Timing,
    /// <summary>Required-Pool selection, plot, and emitted envelope.</summary>
    Waveform,
    /// <summary>Four selected Waveforms arranged and previewed as one 16-beat Routine.</summary>
    Routine,
}

/// <summary>Whether dashboard groups stack vertically or use the available wide workspace.</summary>
internal enum RhythmDashboardFlow
{
    /// <summary>All groups use the full width in scan order.</summary>
    Stacked,
    /// <summary>The Routine storyboard uses four columns instead of a two-by-two card grid.</summary>
    Split,
}

internal readonly struct CountdownChipView
{
    public readonly string Label;
    public readonly string Value;
    public readonly bool AlignValueRight;

    public CountdownChipView(string label, string value, bool alignValueRight = false)
    {
        Label = label;
        Value = value;
        AlignValueRight = alignValueRight;
    }
}

internal readonly struct EnvelopeRowView
{
    public static readonly EnvelopeRowView Null = new(false, 0f, string.Empty);

    public readonly bool HasValue;
    public readonly float Meter;
    public readonly string Readout;

    public EnvelopeRowView(bool hasValue, float meter, string readout)
    {
        HasValue = hasValue;
        Meter = meter;
        Readout = readout;
    }
}

/// <summary>Immutable one-based Grid placement prepared for editor display.</summary>
internal readonly struct GridRowView
{
    /// <summary>The unavailable Grid row.</summary>
    public static readonly GridRowView Null = new(false, string.Empty, 0f, string.Empty);

    /// <summary>Whether the sender provided complete Grid state and placement.</summary>
    public readonly bool HasValue;

    /// <summary>The sender's Grid trust state.</summary>
    public readonly string State;

    /// <summary>The 0..1 Grid progress used by the renderer.</summary>
    public readonly float Meter;

    /// <summary>The one-based Bar and Beat display text.</summary>
    public readonly string Readout;

    /// <summary>Captures one complete Grid row's display facts.</summary>
    public GridRowView(bool hasValue, string state, float meter, string readout)
    {
        HasValue = hasValue;
        State = state;
        Meter = meter;
        Readout = readout;
    }
}

/// <summary>Immutable editor-only Waveform Pool preview selection.</summary>
internal readonly struct WaveformSelectorView
{
    /// <summary>The zero-based Pool entry shown in the preview.</summary>
    public readonly int ShownIndex;

    /// <summary>The Pool names offered by the preview popup.</summary>
    public readonly string[] Options;

    /// <summary>The required-Pool failure shown when no honest preview is available.</summary>
    public readonly string Error;

    /// <summary>Captures one preview selection and its available Pool names.</summary>
    /// <param name="shownIndex">The zero-based Pool entry shown in the preview.</param>
    /// <param name="options">The Pool names offered by the preview popup.</param>
    /// <param name="error">The required-Pool failure, or empty when selection is available.</param>
    public WaveformSelectorView(int shownIndex, string[] options, string error)
    {
        ShownIndex = shownIndex;
        Options = options;
        Error = error;
    }
}

/// <summary>
/// Four editor-only Pool choices in one-based Grid bar order.
/// </summary>
internal readonly struct RoutineStoryboardSelection
{
    /// <summary>One Routine spans exactly four bars.</summary>
    public const int BarCount = 4;

    /// <summary>Zero-based Pool index selected for Routine bar one.</summary>
    private readonly int bar1;
    /// <summary>Zero-based Pool index selected for Routine bar two.</summary>
    private readonly int bar2;
    /// <summary>Zero-based Pool index selected for Routine bar three.</summary>
    private readonly int bar3;
    /// <summary>Zero-based Pool index selected for Routine bar four.</summary>
    private readonly int bar4;

    /// <summary>Captures the four selected zero-based Pool indices.</summary>
    /// <param name="bar1">Pool index for Routine bar one.</param>
    /// <param name="bar2">Pool index for Routine bar two.</param>
    /// <param name="bar3">Pool index for Routine bar three.</param>
    /// <param name="bar4">Pool index for Routine bar four.</param>
    private RoutineStoryboardSelection(int bar1, int bar2, int bar3, int bar4)
    {
        this.bar1 = bar1;
        this.bar2 = bar2;
        this.bar3 = bar3;
        this.bar4 = bar4;
    }

    /// <summary>Starts with the first four Pool entries, repeating the last entry only when the Pool is smaller.</summary>
    /// <param name="poolCount">Number of usable entries in the required Pool.</param>
    /// <returns>Four valid selections, or four unavailable indices when the Pool is empty.</returns>
    public static RoutineStoryboardSelection Default(int poolCount)
    {
        return new RoutineStoryboardSelection(
            InitialIndex(0, poolCount),
            InitialIndex(1, poolCount),
            InitialIndex(2, poolCount),
            InitialIndex(3, poolCount));
    }

    /// <summary>Clamps existing preview choices after the required Pool changes.</summary>
    /// <param name="poolCount">Number of usable entries in the required Pool.</param>
    /// <returns>Selections valid for the current Pool.</returns>
    public RoutineStoryboardSelection WithPoolCount(int poolCount)
    {
        if (poolCount <= 0)
        {
            return Default(0);
        }

        if (bar1 < 0 || bar2 < 0 || bar3 < 0 || bar4 < 0)
        {
            return Default(poolCount);
        }

        return new RoutineStoryboardSelection(
            Mathf.Clamp(bar1, 0, poolCount - 1),
            Mathf.Clamp(bar2, 0, poolCount - 1),
            Mathf.Clamp(bar3, 0, poolCount - 1),
            Mathf.Clamp(bar4, 0, poolCount - 1));
    }

    /// <summary>Returns a copy with one bar assigned to a usable Pool entry.</summary>
    /// <param name="barIndex">Zero-based Routine bar index.</param>
    /// <param name="waveformIndex">Zero-based Pool entry index.</param>
    /// <param name="poolCount">Number of usable entries in the required Pool.</param>
    /// <returns>A copy with the requested bar selection changed.</returns>
    public RoutineStoryboardSelection Select(int barIndex, int waveformIndex, int poolCount)
    {
        if (barIndex < 0 || barIndex >= BarCount)
        {
            throw new ArgumentOutOfRangeException(nameof(barIndex));
        }

        if (waveformIndex < 0 || waveformIndex >= poolCount)
        {
            throw new ArgumentOutOfRangeException(nameof(waveformIndex));
        }

        return barIndex switch
        {
            0 => new RoutineStoryboardSelection(waveformIndex, bar2, bar3, bar4),
            1 => new RoutineStoryboardSelection(bar1, waveformIndex, bar3, bar4),
            2 => new RoutineStoryboardSelection(bar1, bar2, waveformIndex, bar4),
            _ => new RoutineStoryboardSelection(bar1, bar2, bar3, waveformIndex),
        };
    }

    /// <summary>Returns the zero-based Pool entry assigned to one zero-based Routine bar.</summary>
    /// <param name="barIndex">Zero-based Routine bar index.</param>
    /// <returns>The selected zero-based Pool index.</returns>
    public int IndexAt(int barIndex)
    {
        return barIndex switch
        {
            0 => bar1,
            1 => bar2,
            2 => bar3,
            3 => bar4,
            _ => throw new ArgumentOutOfRangeException(nameof(barIndex)),
        };
    }

    /// <summary>Chooses one initial Pool entry without inventing content for an empty Pool.</summary>
    /// <param name="preferredIndex">The desired document-order Pool index.</param>
    /// <param name="poolCount">Number of usable entries in the required Pool.</param>
    /// <returns>A usable Pool index, or -1 for an empty Pool.</returns>
    private static int InitialIndex(int preferredIndex, int poolCount)
    {
        return poolCount > 0 ? Mathf.Min(preferredIndex, poolCount - 1) : -1;
    }
}

/// <summary>
/// Pure editor display state for four selected Waveforms arranged as one 16-beat Routine.
/// </summary>
internal readonly struct RoutineStoryboardView
{
    /// <summary>Resolved Pool entry for Routine bar one.</summary>
    private readonly WaveformPool.Entry bar1;
    /// <summary>Resolved Pool entry for Routine bar two.</summary>
    private readonly WaveformPool.Entry bar2;
    /// <summary>Resolved Pool entry for Routine bar three.</summary>
    private readonly WaveformPool.Entry bar3;
    /// <summary>Resolved Pool entry for Routine bar four.</summary>
    private readonly WaveformPool.Entry bar4;
    /// <summary>The four editor-only Pool indices used to resolve the entries.</summary>
    private readonly RoutineStoryboardSelection selection;

    /// <summary>Whether all four selected Pool entries are usable.</summary>
    public readonly bool IsUsable;

    /// <summary>The required-Pool or selection failure, or empty when usable.</summary>
    public readonly string Error;

    /// <summary>The active one-based Grid bar, or null when placement is unavailable.</summary>
    public readonly int? ActiveBar;

    /// <summary>The active bar phase passed to <see cref="Waveform.Sample(float)"/>, or null without placement.</summary>
    public readonly float? ActiveBarPhase;

    /// <summary>The current Routine envelope; rests at zero without usable Grid placement.</summary>
    public readonly float Envelope;

    /// <summary>Captures four resolved selections and their optional live Grid placement.</summary>
    /// <param name="bar1">Resolved entry for Routine bar one.</param>
    /// <param name="bar2">Resolved entry for Routine bar two.</param>
    /// <param name="bar3">Resolved entry for Routine bar three.</param>
    /// <param name="bar4">Resolved entry for Routine bar four.</param>
    /// <param name="selection">The editor-only Pool indices used to resolve the entries.</param>
    /// <param name="isUsable">Whether all four entries resolved.</param>
    /// <param name="error">The required-Pool or selection failure.</param>
    /// <param name="activeBar">The active one-based Grid bar, if placed.</param>
    /// <param name="activeBarPhase">The active bar's phase, if placed.</param>
    /// <param name="envelope">The sampled Routine envelope.</param>
    private RoutineStoryboardView(
        WaveformPool.Entry bar1,
        WaveformPool.Entry bar2,
        WaveformPool.Entry bar3,
        WaveformPool.Entry bar4,
        RoutineStoryboardSelection selection,
        bool isUsable,
        string error,
        int? activeBar,
        float? activeBarPhase,
        float envelope)
    {
        this.bar1 = bar1;
        this.bar2 = bar2;
        this.bar3 = bar3;
        this.bar4 = bar4;
        this.selection = selection;
        IsUsable = isUsable;
        Error = error;
        ActiveBar = activeBar;
        ActiveBarPhase = activeBarPhase;
        Envelope = envelope;
    }

    /// <summary>Resolves four selected Pool entries and samples the live Grid bar through <see cref="Waveform.Sample(float)"/>.</summary>
    /// <param name="poolEntries">Usable Pool entries in document order.</param>
    /// <param name="selection">Four editor-only Pool selections.</param>
    /// <param name="poolError">The truthful required-Pool failure, or empty when usable.</param>
    /// <param name="gridBar">The sender-provided one-based Grid bar.</param>
    /// <param name="gridProgress">The sender-provided progress across the complete 16-beat Grid.</param>
    /// <returns>A usable storyboard or the exact unavailable state without substitute content.</returns>
    public static RoutineStoryboardView From(
        IReadOnlyList<WaveformPool.Entry> poolEntries,
        RoutineStoryboardSelection selection,
        string poolError,
        int? gridBar,
        float? gridProgress)
    {
        if (!string.IsNullOrEmpty(poolError) || poolEntries == null || poolEntries.Count == 0)
        {
            return Unavailable(selection, poolError);
        }

        if (!TryResolve(poolEntries, selection.IndexAt(0), out var bar1)
            || !TryResolve(poolEntries, selection.IndexAt(1), out var bar2)
            || !TryResolve(poolEntries, selection.IndexAt(2), out var bar3)
            || !TryResolve(poolEntries, selection.IndexAt(3), out var bar4))
        {
            return Unavailable(selection, "Routine storyboard selection is unavailable.");
        }

        if (gridBar is >= 1 and <= RoutineStoryboardSelection.BarCount && gridProgress.HasValue)
        {
            var barPhase = Mathf.Repeat(gridProgress.Value * RoutineStoryboardSelection.BarCount, 1f);
            var activeWaveform = gridBar.Value switch
            {
                1 => bar1.waveform,
                2 => bar2.waveform,
                3 => bar3.waveform,
                _ => bar4.waveform,
            };
            return new RoutineStoryboardView(
                bar1,
                bar2,
                bar3,
                bar4,
                selection,
                true,
                string.Empty,
                gridBar,
                barPhase,
                activeWaveform.Sample(barPhase));
        }

        return new RoutineStoryboardView(
            bar1,
            bar2,
            bar3,
            bar4,
            selection,
            true,
            string.Empty,
            null,
            null,
            0f);
    }

    /// <summary>Returns one resolved selected entry by zero-based Routine bar, or null when unavailable.</summary>
    /// <param name="barIndex">Zero-based Routine bar index.</param>
    /// <returns>The resolved Pool entry, or null when the storyboard is unavailable.</returns>
    public WaveformPool.Entry? EntryAt(int barIndex)
    {
        if (!IsUsable)
        {
            return null;
        }

        return barIndex switch
        {
            0 => bar1,
            1 => bar2,
            2 => bar3,
            3 => bar4,
            _ => throw new ArgumentOutOfRangeException(nameof(barIndex)),
        };
    }

    /// <summary>Returns the selected zero-based Pool index for one zero-based Routine bar.</summary>
    /// <param name="barIndex">Zero-based Routine bar index.</param>
    /// <returns>The selected zero-based Pool index.</returns>
    public int SelectedIndexAt(int barIndex)
    {
        return selection.IndexAt(barIndex);
    }

    /// <summary>Resolves one Pool selection without widening or substituting the requested entry.</summary>
    /// <param name="poolEntries">Usable Pool entries in document order.</param>
    /// <param name="poolIndex">The exact zero-based Pool index to resolve.</param>
    /// <param name="entry">The resolved entry when successful.</param>
    /// <returns>True only when the exact requested entry exists.</returns>
    private static bool TryResolve(
        IReadOnlyList<WaveformPool.Entry> poolEntries,
        int poolIndex,
        out WaveformPool.Entry entry)
    {
        if (poolIndex >= 0 && poolIndex < poolEntries.Count)
        {
            entry = poolEntries[poolIndex];
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>Builds an unavailable view without substituting storyboard content.</summary>
    /// <param name="selection">The editor-only selections retained for error display.</param>
    /// <param name="error">The truthful required-Pool or selection failure.</param>
    /// <returns>An unavailable, resting storyboard.</returns>
    private static RoutineStoryboardView Unavailable(RoutineStoryboardSelection selection, string error)
    {
        var message = string.IsNullOrEmpty(error)
            ? "Required Waveform Pool contains no Waveforms."
            : error;
        return new RoutineStoryboardView(
            default,
            default,
            default,
            default,
            selection,
            false,
            message,
            null,
            null,
            0f);
    }
}

/// <summary>Editor-only selections and explicit document actions emitted by one dashboard draw.</summary>
internal readonly struct BeatManagerDashboardActions
{
    /// <summary>No waveform, Routine, or document action occurred this IMGUI pass.</summary>
    public static readonly BeatManagerDashboardActions None = new(-1, false, -1, -1);

    /// <summary>The selected single-Waveform preview index, or -1 when unchanged.</summary>
    public readonly int SelectedWaveformIndex;

    /// <summary>Whether the author deliberately requested the Waveform Pool editor.</summary>
    public readonly bool OpenWaveformPoolEditor;

    /// <summary>The zero-based Routine bar whose editor-only selection changed, or -1.</summary>
    public readonly int RoutineBarIndex;

    /// <summary>The selected Pool entry for <see cref="RoutineBarIndex"/>, or -1.</summary>
    public readonly int RoutineWaveformIndex;

    /// <summary>Captures all editor-only actions emitted by one dashboard draw.</summary>
    /// <param name="selectedWaveformIndex">The changed single-preview Pool index, or -1.</param>
    /// <param name="openWaveformPoolEditor">Whether the Pool editor was explicitly requested.</param>
    /// <param name="routineBarIndex">The changed zero-based Routine bar, or -1.</param>
    /// <param name="routineWaveformIndex">The changed Routine Pool index, or -1.</param>
    public BeatManagerDashboardActions(
        int selectedWaveformIndex,
        bool openWaveformPoolEditor,
        int routineBarIndex = -1,
        int routineWaveformIndex = -1)
    {
        SelectedWaveformIndex = selectedWaveformIndex;
        OpenWaveformPoolEditor = openWaveformPoolEditor;
        RoutineBarIndex = routineBarIndex;
        RoutineWaveformIndex = routineWaveformIndex;
    }

    /// <summary>Whether the single-Waveform preview selection changed.</summary>
    public bool HasWaveformSelection => SelectedWaveformIndex >= 0;

    /// <summary>Whether one Routine bar's editor-only selection changed.</summary>
    public bool HasRoutineSelection => RoutineBarIndex >= 0 && RoutineWaveformIndex >= 0;

    /// <summary>Returns a copy carrying one changed Routine selector.</summary>
    /// <param name="barIndex">The changed zero-based Routine bar.</param>
    /// <param name="waveformIndex">The selected zero-based Pool entry.</param>
    /// <returns>A combined action value retaining any other action from this draw.</returns>
    public BeatManagerDashboardActions WithRoutineSelection(int barIndex, int waveformIndex)
    {
        return new BeatManagerDashboardActions(
            SelectedWaveformIndex,
            OpenWaveformPoolEditor,
            barIndex,
            waveformIndex);
    }
}
