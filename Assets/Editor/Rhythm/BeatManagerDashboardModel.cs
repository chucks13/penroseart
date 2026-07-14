using UnityEngine;

/// <summary>
/// Pure display model for the BeatManager Inspector dashboard.
/// </summary>
/// <remarks>
/// The Unity <see cref="UnityEditor.PropertyDrawer"/> adapter resolves editor-only context, then this module
/// converts a live <see cref="BeatManager"/> into stable dashboard facts: status text, marker states, query
/// rows, countdown readouts, and waveform playhead data. Keeping these display decisions out of IMGUI gives
/// tests a small seam that exercises the same behavior the Inspector renders without driving Unity layout.
/// </remarks>
internal readonly struct BeatManagerDashboardModel
{
    public const int BeatSlotCount = 4;

    private const string DotFilled = "●";
    private const string DotEmpty = "○";
    private const int UnavailableBeat = -1;

    /// <summary>Whether the canonical rhythm hub currently serves a live four-count.</summary>
    public readonly bool Synced;
    public readonly string BadgeText;
    public readonly string TrackText;
    public readonly string HeaderRightText;
    public readonly int BeatInBar;
    public readonly bool OnBeat;
    public readonly float BeatPulse;
    public readonly bool[] OffBeatGates;
    public readonly float OffBeatPulse;
    public readonly float EighthPulse;
    public readonly CountdownChipView NextBeat;
    public readonly CountdownChipView OnBeatGate;
    public readonly CountdownChipView NextOffBeat;
    public readonly CountdownChipView OffBeatGate;
    public readonly float BarPhase;
    public readonly EnvelopeRowView Envelope;
    public readonly PhraseEventRowView Fill;
    public readonly PhraseEventRowView Drop;
    public readonly EnergyRowView Energy;
    public readonly PhraseRowView Phrase;
    public readonly LevelsRowView Levels;

    /// <summary>Captures one immutable set of downstream Inspector display facts.</summary>
    private BeatManagerDashboardModel(
        bool synced,
        string badgeText,
        string trackText,
        string headerRightText,
        int beatInBar,
        bool onBeat,
        float beatPulse,
        bool[] offBeatGates,
        float offBeatPulse,
        float eighthPulse,
        CountdownChipView nextBeat,
        CountdownChipView onBeatGate,
        CountdownChipView nextOffBeat,
        CountdownChipView offBeatGate,
        float barPhase,
        EnvelopeRowView envelope,
        PhraseEventRowView fill,
        PhraseEventRowView drop,
        EnergyRowView energy,
        PhraseRowView phrase,
        LevelsRowView levels)
    {
        Synced = synced;
        BadgeText = badgeText;
        TrackText = trackText;
        HeaderRightText = headerRightText;
        BeatInBar = beatInBar;
        OnBeat = onBeat;
        BeatPulse = beatPulse;
        OffBeatGates = offBeatGates;
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
        Energy = energy;
        Phrase = phrase;
        Levels = levels;
    }

    /// <summary>
    /// Builds the dashboard display model from the live runtime object and the previewed Pool entry.
    /// </summary>
    /// <param name="beatManager">Runtime beat manager, or <c>null</c> when the property cannot resolve one.</param>
    /// <param name="previewWaveform">The parsed Pool entry selected for editor-only preview, or null when unusable.</param>
    public static BeatManagerDashboardModel From(BeatManager beatManager, Waveform? previewWaveform)
    {
        var synced = beatManager != null && beatManager.IsSynced;
        var timing = beatManager != null ? beatManager.Timing : default;
        var beats = beatManager != null ? beatManager.Beats : default;
        var offbeats = beatManager != null ? beatManager.Offbeats : default;
        var pulses = beatManager != null ? beatManager.Pulses : default;
        var track = beatManager != null ? beatManager.Track : default;
        var bpmText = timing.Bpm is { } bpm ? $"{bpm:0.##} BPM" : "-- BPM";
        var rightText = track.PlayersLive is { Count: > 0 } players ? $"{string.Join(",", players)} · {bpmText}" : bpmText;
        var beatPulse = Mathf.Clamp01(pulses.Beat ?? 0f);
        var offBeatPulse = Mathf.Clamp01(pulses.OffBeat ?? 0f);
        var offBeatGates = new bool[BeatSlotCount];
        for (var slot = 0; slot < offBeatGates.Length; slot++)
        {
            offBeatGates[slot] = offbeats.OffBeat(slot + 1) == true;
        }

        var count = timing.BeatInBar ?? UnavailableBeat;
        var nextCount = count is >= 1 and <= BeatSlotCount ? (count % BeatSlotCount) + 1 : UnavailableBeat;
        var nextBeatMs = nextCount > 0 ? beats.OnBeatMs(nextCount) : null;
        var nextOffbeatMs = MinimumOffbeatMilliseconds(offbeats);

        return new BeatManagerDashboardModel(
            synced,
            synced ? "SYNCED" : "STANDALONE",
            track.Title ?? "—",
            rightText,
            count,
            count > 0 && beats.OnBeat(count) == true,
            beatPulse,
            offBeatGates,
            offBeatPulse,
            GetClampedEighthPulseValue(beatPulse, offBeatPulse),
            new CountdownChipView("NEXT BEAT", FormatMs(nextBeatMs), alignValueRight: true),
            new CountdownChipView("ON BEAT", count > 0 && beats.OnBeat(count) == true ? "YES" : "NO"),
            new CountdownChipView("NEXT OFF BEAT", FormatMs(nextOffbeatMs), alignValueRight: true),
            new CountdownChipView("OFF BEAT", count > 0 && offbeats.OffBeat(count) == true ? "YES" : "NO"),
            synced ? timing.BarProgress ?? 0f : 0f,
            BuildEnvelopeRow(beatManager, previewWaveform),
            BuildPhraseEventRow(beatManager != null ? beatManager.Fill : default),
            BuildPhraseEventRow(beatManager != null ? beatManager.Drop : default),
            BuildEnergyRow(beatManager),
            BuildPhraseRow(beatManager),
            BuildLevelsRow(beatManager));
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

    /// <summary>Whether the zero-based offbeat marker has a served open gate.</summary>
    public bool IsOffBeatEnabled(int index)
    {
        return Synced && OffBeatGates != null && index >= 0 && index < OffBeatGates.Length && OffBeatGates[index];
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

    private static string BuildBeatDotGlyph(bool active, int beatInBar, int beatLabel)
    {
        return active && beatLabel >= 1 && beatLabel <= beatInBar && beatInBar <= BeatSlotCount ? DotFilled : DotEmpty;
    }

    /// <summary>Formats a nullable millisecond countdown for a compact dashboard chip.</summary>
    private static string FormatMs(float? value)
    {
        return value is { } ms ? $"{ms:0}ms" : "--";
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
        var arrow = energy.Trend == EnergyTrend.Rising ? "↗" : energy.Trend == EnergyTrend.Falling ? "↘" : "→";
        var heading = beatManager.NextEnergy.Level is { } next
            ? $"{arrow} {next.ToString().ToUpperInvariant()} in {RhythmText.Beats(beatManager.NextEnergy.BeatsUntil)}"
            : "steady";
        return new EnergyRowView(
            true,
            level.ToString().ToUpperInvariant(),
            level,
            energy.Progress ?? 0f,
            heading);
    }

    /// <summary>Builds the current and upcoming Phrase row from the canonical value groups.</summary>
    private static PhraseRowView BuildPhraseRow(BeatManager beatManager)
    {
        if (beatManager == null || beatManager.Phrase.Name is not { } name)
        {
            return PhraseRowView.Null;
        }

        var phrase = beatManager.Phrase;
        var heading = beatManager.NextPhrase.Name is { } nextName
            ? $"→ {nextName} in {RhythmText.Beats(beatManager.NextPhrase.BeatsUntil)}"
            : $"len {RhythmText.Count(phrase.LengthBeats)}";
        return new PhraseRowView(true, name, phrase.Progress ?? 0f, heading);
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
