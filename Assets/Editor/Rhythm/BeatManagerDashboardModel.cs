using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure display model for the BeatManager Rhythm dashboard.
/// </summary>
/// <remarks>
/// The Unity <see cref="UnityEditor.PropertyDrawer"/> adapter resolves editor-only context, then this module
/// converts a live <see cref="BeatManager"/> into stable dashboard facts: authoritative mode, independent
/// availability, responsive grouping, current/next state, Grid placement, countdowns, and Waveform health.
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
    /// <summary>The practical width at which current and next state can remain readable side by side.</summary>
    private const float SplitLayoutMinWidth = 820f;

    /// <summary>The stable scan order shared by narrow and wide dashboard layouts.</summary>
    private static readonly IReadOnlyList<RhythmDashboardGroup> DisplayGroups = Array.AsReadOnly(new[]
    {
        RhythmDashboardGroup.Timing,
        RhythmDashboardGroup.Waveform,
        RhythmDashboardGroup.Current,
        RhythmDashboardGroup.Next,
        RhythmDashboardGroup.Levels,
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
    public readonly PhraseEventRowView Fill;
    public readonly PhraseEventRowView Drop;
    /// <summary>The current Energy run, kept separate from the announced next run.</summary>
    public readonly EnergyRowView CurrentEnergy;
    /// <summary>The announced next Energy run.</summary>
    public readonly EnergyRowView NextEnergy;
    /// <summary>The current Phrase, kept separate from the announced next Phrase.</summary>
    public readonly PhraseRowView CurrentPhrase;
    /// <summary>The announced next Phrase.</summary>
    public readonly PhraseRowView NextPhrase;
    /// <summary>The sender-provided one-based Grid position.</summary>
    public readonly GridRowView Grid;
    public readonly LevelsRowView Levels;
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
        PhraseEventRowView fill,
        PhraseEventRowView drop,
        EnergyRowView currentEnergy,
        EnergyRowView nextEnergy,
        PhraseRowView currentPhrase,
        PhraseRowView nextPhrase,
        GridRowView grid,
        LevelsRowView levels,
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
        Fill = fill;
        Drop = drop;
        CurrentEnergy = currentEnergy;
        NextEnergy = nextEnergy;
        CurrentPhrase = currentPhrase;
        NextPhrase = nextPhrase;
        Grid = grid;
        Levels = levels;
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
                PhraseEventRowView.Null,
                PhraseEventRowView.Null,
                EnergyRowView.Null,
                EnergyRowView.Null,
                PhraseRowView.Null,
                PhraseRowView.Null,
                GridRowView.Null,
                LevelsRowView.Null,
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
            BuildPhraseEventRow(beatManager != null ? beatManager.Fill : default),
            BuildPhraseEventRow(beatManager != null ? beatManager.Drop : default),
            BuildEnergyRow(beatManager),
            BuildNextEnergyRow(beatManager),
            BuildCurrentPhraseRow(beatManager),
            BuildNextPhraseRow(beatManager),
            BuildGridRow(beatManager),
            BuildLevelsRow(beatManager),
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

    /// <summary>Builds one Fill row from its canonical value group.</summary>
    private static PhraseEventRowView BuildPhraseEventRow(FillValues fill)
    {
        return fill.Active.HasValue || fill.BeatsUntil.HasValue || fill.Remaining.HasValue
            ? new PhraseEventRowView(true, PhraseEventView.Of(fill))
            : PhraseEventRowView.Null;
    }

    /// <summary>Builds one Drop row from its canonical value group.</summary>
    private static PhraseEventRowView BuildPhraseEventRow(DropValues drop)
    {
        return drop.Active.HasValue || drop.BeatsUntil.HasValue || drop.Remaining.HasValue
            ? new PhraseEventRowView(true, PhraseEventView.Of(drop))
            : PhraseEventRowView.Null;
    }

    /// <summary>Builds the Energy row from its canonical run and anticipation facts.</summary>
    private static EnergyRowView BuildEnergyRow(BeatManager beatManager)
    {
        if (beatManager == null || beatManager.Energy.Level is not { } level)
        {
            return EnergyRowView.Null;
        }

        var energy = beatManager.Energy;
        var heading = $"{RhythmText.Beats(energy.BeatsRemaining)} left · len {RhythmText.Count(energy.LengthBeats)}";
        return new EnergyRowView(
            true,
            level.ToString().ToUpperInvariant(),
            level,
            energy.Progress ?? 0f,
            heading);
    }

    /// <summary>Builds the announced next Energy run without merging it into current state.</summary>
    private static EnergyRowView BuildNextEnergyRow(BeatManager beatManager)
    {
        if (beatManager == null || beatManager.NextEnergy.Level is not { } level)
        {
            return EnergyRowView.Null;
        }

        var next = beatManager.NextEnergy;
        return new EnergyRowView(
            true,
            level.ToString().ToUpperInvariant(),
            level,
            0f,
            $"in {RhythmText.Beats(next.BeatsUntil)} · len {RhythmText.Count(next.LengthBeats)}");
    }

    /// <summary>Builds current Phrase display facts without merging the announced next Phrase.</summary>
    private static PhraseRowView BuildCurrentPhraseRow(BeatManager beatManager)
    {
        if (beatManager == null || beatManager.Phrase.Name is not { } name)
        {
            return PhraseRowView.Null;
        }

        var phrase = beatManager.Phrase;
        var heading = $"{RhythmText.Beats(phrase.BeatsRemaining)} left · len {RhythmText.Count(phrase.LengthBeats)}";
        return new PhraseRowView(true, name, phrase.Progress ?? 0f, heading);
    }

    /// <summary>Builds the announced next Phrase as a distinct display row.</summary>
    private static PhraseRowView BuildNextPhraseRow(BeatManager beatManager)
    {
        if (beatManager == null || beatManager.NextPhrase.Name is not { } name)
        {
            return PhraseRowView.Null;
        }

        var next = beatManager.NextPhrase;
        return new PhraseRowView(
            true,
            name,
            0f,
            $"in {RhythmText.Beats(next.BeatsUntil)} · len {RhythmText.Count(next.LengthBeats)}");
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

    /// <summary>Builds the band-meter row from smoothed canonical Levels.</summary>
    private static LevelsRowView BuildLevelsRow(BeatManager beatManager)
    {
        return beatManager != null
            ? new LevelsRowView(true, beatManager.Levels.Smoothed.Low, beatManager.Levels.Smoothed.Mid, beatManager.Levels.Smoothed.High)
            : LevelsRowView.Null;
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
    /// <summary>Current Phrase, Fill, Drop, and Energy.</summary>
    Current,
    /// <summary>Announced next Phrase and Energy.</summary>
    Next,
    /// <summary>Smoothed low, mid, and high bands.</summary>
    Levels,
}

/// <summary>Whether dashboard groups stack vertically or use the available wide workspace.</summary>
internal enum RhythmDashboardFlow
{
    /// <summary>All groups use the full width in scan order.</summary>
    Stacked,
    /// <summary>Current and next musical state share a wide row.</summary>
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

internal readonly struct PhraseEventRowView
{
    public static readonly PhraseEventRowView Null = new(false, default);

    public readonly bool HasValue;
    public readonly PhraseEventView View;

    public PhraseEventRowView(bool hasValue, PhraseEventView view)
    {
        HasValue = hasValue;
        View = view;
    }
}

internal readonly struct EnergyRowView
{
    /// <summary>The unavailable Energy row.</summary>
    public static readonly EnergyRowView Null = new(false, string.Empty, Energy.Low, 0f, string.Empty);

    public readonly bool HasValue;
    public readonly string Chip;
    /// <summary>The current canonical Energy level used to color the status chip.</summary>
    public readonly Energy Level;
    public readonly float Meter;
    public readonly string Readout;

    /// <summary>Captures one Energy row's display facts.</summary>
    public EnergyRowView(bool hasValue, string chip, Energy level, float meter, string readout)
    {
        HasValue = hasValue;
        Chip = chip;
        Level = level;
        Meter = meter;
        Readout = readout;
    }
}

internal readonly struct PhraseRowView
{
    public static readonly PhraseRowView Null = new(false, string.Empty, 0f, string.Empty);

    public readonly bool HasValue;
    public readonly string Label;
    public readonly float Meter;
    public readonly string Readout;

    public PhraseRowView(bool hasValue, string label, float meter, string readout)
    {
        HasValue = hasValue;
        Label = label;
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

internal readonly struct LevelsRowView
{
    public static readonly LevelsRowView Null = new(false, 0f, 0f, 0f);

    public readonly bool HasValue;
    public readonly float Low;
    public readonly float Mid;
    public readonly float High;

    public LevelsRowView(bool hasValue, float low, float mid, float high)
    {
        HasValue = hasValue;
        Low = low;
        Mid = mid;
        High = high;
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

internal readonly struct BeatManagerDashboardActions
{
    public static readonly BeatManagerDashboardActions None = new(-1, false);

    public readonly int SelectedWaveformIndex;
    public readonly bool OpenWaveformPoolEditor;

    public BeatManagerDashboardActions(int selectedWaveformIndex, bool openWaveformPoolEditor)
    {
        SelectedWaveformIndex = selectedWaveformIndex;
        OpenWaveformPoolEditor = openWaveformPoolEditor;
    }

    public bool HasWaveformSelection => SelectedWaveformIndex >= 0;
}
