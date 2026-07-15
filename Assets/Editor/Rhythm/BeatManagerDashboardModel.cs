using System;
using System.Collections.Generic;
using System.Reflection;
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
    /// <summary>Whether the staged Effect's Waveform resolves honestly against the required Pool.</summary>
    public readonly bool WaveformAvailable;
    /// <summary>The Waveform selection or configuration message, or empty when available.</summary>
    public readonly string WaveformMessage;
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
        bool waveformAvailable,
        string waveformMessage)
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
        WaveformAvailable = waveformAvailable;
        WaveformMessage = waveformMessage;
        Groups = DisplayGroups;
    }

    /// <summary>
    /// Builds the dashboard display model from the live runtime object and staged Effect selection.
    /// </summary>
    /// <param name="beatManager">Runtime beat manager, or <c>null</c> when the property cannot resolve one.</param>
    /// <param name="selectedWaveform">The staged Effect's Pool-matched Waveform, or null when unavailable.</param>
    /// <param name="waveformMessage">The selection or required-Pool message, or empty when available.</param>
    public static BeatManagerDashboardModel From(
        BeatManager beatManager,
        Waveform? selectedWaveform,
        string waveformMessage)
    {
        var synced = beatManager != null && beatManager.IsSynced;
        var waveformAvailable = selectedWaveform.HasValue && string.IsNullOrEmpty(waveformMessage);
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
                waveformAvailable,
                waveformMessage ?? string.Empty);
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
            offBeatGates[slot] = GateOrUnavailable(offbeats.OffBeatMs(slot + 1), offbeats.OffBeat(slot + 1));
        }

        var count = timing.BeatInBar ?? UnavailableBeat;
        var nextCount = count is >= 1 and <= BeatSlotCount ? (count % BeatSlotCount) + 1 : UnavailableBeat;
        var nextBeatMs = nextCount > 0 ? beats.OnBeatMs(nextCount) : null;
        var nextOffbeatMs = MinimumOffbeatMilliseconds(offbeats);
        bool? onBeat = count > 0 ? GateOrUnavailable(beats.OnBeatMs(count), beats.OnBeat(count)) : null;
        bool? offBeat = count > 0 ? GateOrUnavailable(offbeats.OffBeatMs(count), offbeats.OffBeat(count)) : null;
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
            BuildEnvelopeRow(beatManager, selectedWaveform),
            BuildGridRow(beatManager),
            waveformAvailable,
            waveformMessage ?? string.Empty);
    }

    /// <summary>Selects stacked content below the practical split-layout width.</summary>
    public static RhythmDashboardFlow FlowForWidth(float width)
    {
        return width >= SplitLayoutMinWidth ? RhythmDashboardFlow.Split : RhythmDashboardFlow.Stacked;
    }

    /// <summary>
    /// Presents a resting gate as unavailable (null) when its countdown lane is absent, so the
    /// dashboard keeps distinguishing "lane missing" from a genuinely closed gate.
    /// </summary>
    private static bool? GateOrUnavailable(int? laneMilliseconds, bool gate)
    {
        return laneMilliseconds != null ? gate : null;
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

    /// <summary>Builds the live-clock envelope row for the staged Effect's selected Waveform.</summary>
    /// <param name="beatManager">The live runtime source, or null when unavailable.</param>
    /// <param name="selectedWaveform">The staged Effect's valid Pool-matched Waveform, or null when unavailable.</param>
    private static EnvelopeRowView BuildEnvelopeRow(BeatManager beatManager, Waveform? selectedWaveform)
    {
        if (beatManager == null || selectedWaveform is not { } waveform)
        {
            return EnvelopeRowView.Null;
        }

        var envelope = waveform.Bind(beatManager).Envelope;
        return new EnvelopeRowView(true, envelope, $"{envelope:0.00} · effect");
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
    /// <summary>Staged Effect Waveform label, plot, and emitted envelope.</summary>
    Waveform,
    /// <summary>The staged Effect's four Waveforms arranged as one 16-beat Routine.</summary>
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

/// <summary>Immutable Waveform Pool label resolved for the current Effect.</summary>
internal readonly struct WaveformSelectorView
{
    /// <summary>The zero-based Pool entry matching the staged Effect's Waveform.</summary>
    public readonly int ShownIndex;

    /// <summary>The Pool names used by the read-only selection label.</summary>
    public readonly string[] Options;

    /// <summary>The inspection failure shown when no honest Effect selection is available.</summary>
    public readonly string Error;

    /// <summary>Whether <see cref="Error"/> is a broken configuration rather than a valid Effect omission.</summary>
    public readonly bool IsError;

    /// <summary>Captures one Effect selection and its available Pool names.</summary>
    /// <param name="shownIndex">The zero-based Pool entry matching the Effect selection.</param>
    /// <param name="options">The Pool names used by the read-only label.</param>
    /// <param name="error">The inspection message, or empty when selection is available.</param>
    /// <param name="isError">Whether the message represents broken configuration.</param>
    public WaveformSelectorView(int shownIndex, string[] options, string error, bool isError = false)
    {
        ShownIndex = shownIndex;
        Options = options;
        Error = error;
        IsError = isError;
    }
}

/// <summary>Read-only rhythm configuration resolved from the Effect currently staged by the Switcher.</summary>
internal readonly struct EffectRhythmSelectionView
{
    /// <summary>The private immutable bar storage inside <see cref="Routine"/>, observed only by this Editor adapter.</summary>
    private static readonly FieldInfo RoutineWaveformsField = typeof(Routine).GetField(
        "waveforms",
        BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>The staged Effect's type name, or an unavailable label when no Effect owns the stage.</summary>
    public readonly string EffectName;

    /// <summary>The staged Effect's selected single Waveform, or null when it uses no single Waveform.</summary>
    public readonly Waveform? Waveform;

    /// <summary>The Pool label corresponding to <see cref="Waveform"/>, or its truthful unavailable state.</summary>
    public readonly WaveformSelectorView WaveformSelector;

    /// <summary>The staged Effect's four-bar Routine, or its truthful unavailable state.</summary>
    public readonly RoutineStoryboardView Routine;

    /// <summary>Captures the complete read-only Effect rhythm selection shown by the dashboard.</summary>
    private EffectRhythmSelectionView(
        string effectName,
        Waveform? waveform,
        WaveformSelectorView waveformSelector,
        RoutineStoryboardView routine)
    {
        EffectName = effectName;
        Waveform = waveform;
        WaveformSelector = waveformSelector;
        Routine = routine;
    }

    /// <summary>Resolves the current Switcher Effect and matches its held rhythm values to Pool labels.</summary>
    /// <param name="controller">The live Controller whose Switcher owns the active stage.</param>
    /// <param name="poolEntries">The current parsed Pool entries in document order.</param>
    /// <param name="poolNames">The cached Pool labels in the same document order.</param>
    /// <param name="poolError">The current required-Pool failure, or empty when usable.</param>
    /// <param name="gridBar">The live one-based Grid bar, or null without placement.</param>
    /// <param name="gridProgress">Progress across the complete Grid, or null without placement.</param>
    /// <returns>The staged Effect's exact Waveform and Routine selections without mutating runtime state.</returns>
    public static EffectRhythmSelectionView From(
        Controller controller,
        IReadOnlyList<WaveformPool.Entry> poolEntries,
        string[] poolNames,
        string poolError,
        int? gridBar,
        float? gridProgress)
    {
        if (!string.IsNullOrEmpty(poolError))
        {
            return Unavailable(poolError, poolNames, isError: true);
        }

        var effect = ResolveCurrentEffect(controller);
        if (effect == null)
        {
            const string error = "No Effect currently owns the Switcher stage.";
            return Unavailable(error, poolNames, isError: false);
        }

        var effectName = effect.GetType().Name;
        Waveform? waveform = null;
        WaveformSelectorView selector;
        if (TryFindUniqueWaveform(
            poolEntries,
            effect.waveform,
            out var waveformIndex,
            out var waveformMatchIsAmbiguous))
        {
            waveform = poolEntries[waveformIndex].waveform;
            selector = new WaveformSelectorView(waveformIndex, poolNames, string.Empty);
        }
        else if (waveformMatchIsAmbiguous)
        {
            selector = new WaveformSelectorView(
                -1,
                poolNames,
                $"Effect '{effectName}' has a Waveform matching multiple Pool entries.",
                isError: true);
        }
        else if (string.IsNullOrEmpty(effect.waveform.sequence))
        {
            selector = new WaveformSelectorView(
                -1,
                poolNames,
                $"Effect '{effectName}' has no selected single Waveform.");
        }
        else
        {
            selector = new WaveformSelectorView(
                -1,
                poolNames,
                $"Effect '{effectName}' has a Waveform missing from the current Pool.",
                isError: true);
        }

        var storyboard = BuildRoutineStoryboard(
            effectName,
            ResolveRoutine(effect),
            poolEntries,
            gridBar,
            gridProgress);
        return new EffectRhythmSelectionView(effectName, waveform, selector, storyboard);
    }

    /// <summary>Resolves the top-level Effect currently staged outside a Transition.</summary>
    private static EffectBase ResolveCurrentEffect(Controller controller)
    {
        if (controller == null || controller.effects == null)
        {
            return null;
        }

        var index = controller.SwitcherStatus.CurrentEffectIndex;
        return index >= 0 && index < controller.effects.Length ? controller.effects[index] : null;
    }

    /// <summary>Finds the first concrete Routine held by an Effect without requiring runtime inspection API.</summary>
    private static Routine ResolveRoutine(EffectBase effect)
    {
        for (var type = effect.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            var fields = type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (var index = 0; index < fields.Length; index++)
            {
                if (fields[index].FieldType == typeof(Routine)
                    && fields[index].GetValue(effect) is Routine routine)
                {
                    return routine;
                }
            }
        }

        return null;
    }

    /// <summary>Builds the storyboard from the exact Waveforms retained by the staged Effect's Routine.</summary>
    private static RoutineStoryboardView BuildRoutineStoryboard(
        string effectName,
        Routine routine,
        IReadOnlyList<WaveformPool.Entry> poolEntries,
        int? gridBar,
        float? gridProgress)
    {
        if (routine == null)
        {
            return UnavailableRoutine(
                $"Effect '{effectName}' has no selected Routine.",
                isError: false);
        }

        if (RoutineWaveformsField?.GetValue(routine) is not Waveform[] bars || bars.Length != 4)
        {
            return UnavailableRoutine(
                $"Effect '{effectName}' has an unreadable Routine.",
                isError: true);
        }

        var selection = RoutineStoryboardSelection.Default(poolEntries?.Count ?? 0);
        for (var barIndex = 0; barIndex < bars.Length; barIndex++)
        {
            if (!TryFindUniqueWaveform(
                poolEntries,
                bars[barIndex],
                out var poolIndex,
                out var waveformMatchIsAmbiguous))
            {
                return UnavailableRoutine(
                    waveformMatchIsAmbiguous
                        ? $"Effect '{effectName}' has a Routine Waveform matching multiple Pool entries."
                        : $"Effect '{effectName}' has a Routine Waveform missing from the current Pool.",
                    isError: true);
            }

            selection = selection.Select(barIndex, poolIndex, poolEntries.Count);
        }

        return RoutineStoryboardView.From(poolEntries, selection, string.Empty, gridBar, gridProgress);
    }

    /// <summary>Builds one unavailable Routine view with the exact inspection failure.</summary>
    private static RoutineStoryboardView UnavailableRoutine(
        string error,
        bool isError)
    {
        return RoutineStoryboardView.Unavailable(
            RoutineStoryboardSelection.Default(0),
            error,
            isError);
    }

    /// <summary>Matches one runtime-bound Waveform only when its immutable Pool definition is unique.</summary>
    private static bool TryFindUniqueWaveform(
        IReadOnlyList<WaveformPool.Entry> poolEntries,
        Waveform waveform,
        out int matchingIndex,
        out bool isAmbiguous)
    {
        matchingIndex = -1;
        isAmbiguous = false;
        if (poolEntries != null && !string.IsNullOrEmpty(waveform.sequence))
        {
            for (var index = 0; index < poolEntries.Count; index++)
            {
                var candidate = poolEntries[index].waveform;
                if (candidate.sequence == waveform.sequence
                    && candidate.amplitude == waveform.amplitude
                    && Mathf.Approximately(candidate.rounding, waveform.rounding)
                    && Mathf.Approximately(candidate.offset, waveform.offset))
                {
                    if (matchingIndex >= 0)
                    {
                        matchingIndex = -1;
                        isAmbiguous = true;
                        return false;
                    }

                    matchingIndex = index;
                }
            }
        }

        return matchingIndex >= 0;
    }

    /// <summary>Builds matching unavailable Waveform and Routine views for one shared inspection failure.</summary>
    /// <param name="error">The truthful failure rendered by both views.</param>
    /// <param name="poolNames">The cached Pool labels, when available.</param>
    /// <param name="isError">Whether the failure represents broken configuration.</param>
    private static EffectRhythmSelectionView Unavailable(
        string error,
        string[] poolNames,
        bool isError)
    {
        return new EffectRhythmSelectionView(
            BeatManagerDashboardModel.UnavailableText,
            null,
            new WaveformSelectorView(-1, poolNames, error, isError),
            UnavailableRoutine(error, isError));
    }
}

/// <summary>
/// Four Pool indices matched from one Effect-owned Routine in one-based Grid bar order.
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

    /// <summary>Starts with the first four Pool entries before exact Effect-owned matches are applied.</summary>
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

    /// <summary>Returns a copy with one Effect-owned bar matched to a usable Pool entry.</summary>
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
/// Pure editor display state for four Effect-owned Waveforms arranged as one 16-beat Routine.
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
    /// <summary>The four Pool indices matched from the Effect-owned Routine.</summary>
    private readonly RoutineStoryboardSelection selection;

    /// <summary>Whether all four selected Pool entries are usable.</summary>
    public readonly bool IsUsable;

    /// <summary>The required-Pool or selection failure, or empty when usable.</summary>
    public readonly string Error;

    /// <summary>Whether <see cref="Error"/> is broken configuration rather than a valid Effect omission.</summary>
    public readonly bool IsError;

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
    /// <param name="selection">The Pool indices matched from the Effect-owned Routine.</param>
    /// <param name="isUsable">Whether all four entries resolved.</param>
    /// <param name="error">The required-Pool or selection failure.</param>
    /// <param name="isError">Whether the message represents broken configuration.</param>
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
        bool isError,
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
        IsError = isError;
        ActiveBar = activeBar;
        ActiveBarPhase = activeBarPhase;
        Envelope = envelope;
    }

    /// <summary>Resolves four selected Pool entries and samples the live Grid bar through <see cref="Waveform.Sample(float)"/>.</summary>
    /// <param name="poolEntries">Usable Pool entries in document order.</param>
    /// <param name="selection">Four Pool indices matched from the Effect-owned Routine.</param>
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
            return Unavailable(selection, poolError, isError: true);
        }

        if (!TryResolve(poolEntries, selection.IndexAt(0), out var bar1)
            || !TryResolve(poolEntries, selection.IndexAt(1), out var bar2)
            || !TryResolve(poolEntries, selection.IndexAt(2), out var bar3)
            || !TryResolve(poolEntries, selection.IndexAt(3), out var bar4))
        {
            return Unavailable(selection, "Routine storyboard selection is unavailable.", isError: true);
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
                false,
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
            false,
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
    /// <param name="selection">The matched selections retained for error display.</param>
    /// <param name="error">The truthful required-Pool or selection failure.</param>
    /// <param name="isError">Whether the message represents broken configuration.</param>
    /// <returns>An unavailable, resting storyboard.</returns>
    internal static RoutineStoryboardView Unavailable(
        RoutineStoryboardSelection selection,
        string error,
        bool isError)
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
            isError,
            null,
            null,
            0f);
    }
}

/// <summary>Explicit document action emitted by one dashboard draw.</summary>
internal readonly struct BeatManagerDashboardActions
{
    /// <summary>No document action occurred this IMGUI pass.</summary>
    public static readonly BeatManagerDashboardActions None = new(false);

    /// <summary>Whether the author deliberately requested the Waveform Pool editor.</summary>
    public readonly bool OpenWaveformPoolEditor;

    /// <summary>Captures the explicit Pool-editor action emitted by one dashboard draw.</summary>
    /// <param name="openWaveformPoolEditor">Whether the Pool editor was explicitly requested.</param>
    public BeatManagerDashboardActions(bool openWaveformPoolEditor)
    {
        OpenWaveformPoolEditor = openWaveformPoolEditor;
    }
}
