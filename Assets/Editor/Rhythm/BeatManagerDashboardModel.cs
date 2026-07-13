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
    public readonly ColorBankRowView ColorBank;

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
        LevelsRowView levels,
        ColorBankRowView colorBank)
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
        ColorBank = colorBank;
    }

    /// <summary>
    /// Builds the dashboard display model from the live runtime object and the previewed Pool entry.
    /// </summary>
    /// <param name="beatManager">Runtime beat manager, or <c>null</c> when the property cannot resolve one.</param>
    /// <param name="previewWaveform">The parsed Pool entry selected for editor-only preview.</param>
    public static BeatManagerDashboardModel From(BeatManager beatManager, Waveform previewWaveform)
    {
        var synced = beatManager != null && beatManager.IsSynced;
        var clock = beatManager != null ? beatManager.Clock : default;
        var position = beatManager != null ? beatManager.Position : default;
        var beats = beatManager != null ? beatManager.Beats : default;
        var offBeats = beatManager != null ? beatManager.OffBeats : default;
        var pulses = beatManager != null ? beatManager.Pulses : default;
        var track = beatManager != null ? beatManager.Track : default;
        var bpmText = clock.Bpm is { } bpm ? $"{bpm:0.##} BPM" : "-- BPM";
        var rightText = track.PlayersLive is { Count: > 0 } players ? $"{string.Join(",", players)} · {bpmText}" : bpmText;
        var beatPulse = Mathf.Clamp01(pulses.Beat ?? 0f);
        var offBeatPulse = Mathf.Clamp01(pulses.OffBeat ?? 0f);
        var offBeatGates = new bool[BeatSlotCount];
        for (var slot = 0; slot < offBeatGates.Length; slot++)
        {
            offBeatGates[slot] = offBeats.Gate(slot + 1) == true;
        }

        return new BeatManagerDashboardModel(
            synced,
            synced ? "SYNCED" : "STANDALONE",
            track.TrackTitle ?? "—",
            rightText,
            position.BeatInBar ?? UnavailableBeat,
            beats.OnBeat ?? false,
            beatPulse,
            offBeatGates,
            offBeatPulse,
            GetClampedEighthPulseValue(beatPulse, offBeatPulse),
            new CountdownChipView("NEXT BEAT", FormatMs(beats.NextBeatMs), alignValueRight: true),
            new CountdownChipView("ON BEAT", beats.OnBeat == true ? "YES" : "NO"),
            new CountdownChipView("NEXT OFF BEAT", FormatMs(offBeats.NextOffBeatMs), alignValueRight: true),
            new CountdownChipView("OFF BEAT", offBeats.OffBeat == true ? "YES" : "NO"),
            synced ? clock.BarPhase ?? 0f : 0f,
            BuildEnvelopeRow(beatManager, previewWaveform),
            BuildPhraseEventRow(beatManager != null ? beatManager.Fill : default),
            BuildPhraseEventRow(beatManager != null ? beatManager.Drop : default),
            BuildEnergyRow(beatManager != null ? beatManager.Energy : default),
            BuildPhraseRow(beatManager != null ? beatManager.Phrase : default),
            BuildLevelsRow(beatManager?.Levels),
            BuildColorBankRow(beatManager?.Levels));
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

    /// <summary>Builds the live-clock envelope row for the editor-previewed Pool entry.</summary>
    private static EnvelopeRowView BuildEnvelopeRow(BeatManager beatManager, Waveform previewWaveform)
    {
        if (beatManager == null)
        {
            return EnvelopeRowView.Null;
        }

        var envelope = previewWaveform.Bind(beatManager).Envelope;
        return new EnvelopeRowView(true, envelope, $"{envelope:0.00} · preview");
    }

    /// <summary>Builds one Fill row from its canonical doorway.</summary>
    private static PhraseEventRowView BuildPhraseEventRow(FillView fill)
    {
        return fill.Span.Current.HasValue || fill.NextInBeats.HasValue || fill.RemainingOnTrack.HasValue
            ? new PhraseEventRowView(true, PhraseEventView.Of(fill))
            : PhraseEventRowView.Null;
    }

    /// <summary>Builds one Drop row from its canonical doorway.</summary>
    private static PhraseEventRowView BuildPhraseEventRow(DropView drop)
    {
        return drop.Span.Current.HasValue || drop.NextInBeats.HasValue || drop.RemainingOnTrack.HasValue
            ? new PhraseEventRowView(true, PhraseEventView.Of(drop))
            : PhraseEventRowView.Null;
    }

    /// <summary>Builds the Energy row from its canonical run and anticipation facts.</summary>
    private static EnergyRowView BuildEnergyRow(EnergyView energy)
    {
        if (!(energy.Run.Current is { } current))
        {
            return EnergyRowView.Null;
        }

        var arrow = energy.Trend == EnergyTrend.Rising ? "↗" : energy.Trend == EnergyTrend.Falling ? "↘" : "→";
        var heading = energy.NextLevel is { } next
            ? $"{arrow} {next.ToString().ToUpperInvariant()} in {RhythmText.Beats(energy.NextChangeInBeats)}"
            : "steady";
        return new EnergyRowView(
            true,
            current.Level.ToString().ToUpperInvariant(),
            current.Level,
            energy.Run.Progress ?? 0f,
            heading);
    }

    /// <summary>Builds the current and upcoming Phrase row from the canonical doorway.</summary>
    private static PhraseRowView BuildPhraseRow(PhraseView phrase)
    {
        if (!(phrase.Span.Current is { } current))
        {
            return PhraseRowView.Null;
        }

        var heading = phrase.NextName is { } nextName
            ? $"→ {nextName} in {RhythmText.Beats(phrase.NextInBeats)}"
            : $"len {RhythmText.Count(current.LengthBeats)}";
        return new PhraseRowView(true, current.Name, phrase.Span.Progress ?? 0f, heading);
    }

    /// <summary>Builds the band-meter row from smoothed canonical Levels.</summary>
    private static LevelsRowView BuildLevelsRow(LevelsView? info)
    {
        return info is { } levels
            ? new LevelsRowView(true, levels.Smoothed.Low, levels.Smoothed.Mid, levels.Smoothed.High)
            : LevelsRowView.Null;
    }

    /// <summary>Builds RGB and HSV swatches from the same smoothed Levels triple.</summary>
    private static ColorBankRowView BuildColorBankRow(LevelsView? info)
    {
        return info is { } levels
            ? new ColorBankRowView(levels.Smoothed.Rgb(), levels.Smoothed.Hsv(), null)
            : new ColorBankRowView(null, null, null);
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
    public static readonly EnvelopeRowView Null = new EnvelopeRowView(false, 0f, string.Empty);

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
    public static readonly PhraseEventRowView Null = new PhraseEventRowView(false, default);

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
    public static readonly EnergyRowView Null = new EnergyRowView(false, string.Empty, global::Energy.Low, 0f, string.Empty);

    public readonly bool HasValue;
    public readonly string Chip;
    /// <summary>The current canonical Energy level used to color the status chip.</summary>
    public readonly global::Energy Level;
    public readonly float Meter;
    public readonly string Readout;

    /// <summary>Captures one Energy row's display facts.</summary>
    public EnergyRowView(bool hasValue, string chip, global::Energy level, float meter, string readout)
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
    public static readonly PhraseRowView Null = new PhraseRowView(false, string.Empty, 0f, string.Empty);

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
    public static readonly LevelsRowView Null = new LevelsRowView(false, 0f, 0f, 0f);

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

internal readonly struct ColorBankRowView
{
    public readonly Color? Rgb;
    public readonly Color? Hue;
    public readonly Color? Palette;

    public ColorBankRowView(Color? rgb, Color? hue, Color? palette)
    {
        Rgb = rgb;
        Hue = hue;
        Palette = palette;
    }
}

/// <summary>Immutable editor-only Waveform Pool preview selection.</summary>
internal readonly struct WaveformSelectorView
{
    /// <summary>The zero-based Pool entry shown in the preview.</summary>
    public readonly int ShownIndex;

    /// <summary>The Pool names offered by the preview popup.</summary>
    public readonly string[] Options;

    /// <summary>Captures one preview selection and its available Pool names.</summary>
    /// <param name="shownIndex">The zero-based Pool entry shown in the preview.</param>
    /// <param name="options">The Pool names offered by the preview popup.</param>
    public WaveformSelectorView(int shownIndex, string[] options)
    {
        ShownIndex = shownIndex;
        Options = options;
    }
}

internal readonly struct BeatManagerDashboardActions
{
    public static readonly BeatManagerDashboardActions None = new BeatManagerDashboardActions(-1, false);

    public readonly int SelectedWaveformIndex;
    public readonly bool OpenWaveformPoolEditor;

    public BeatManagerDashboardActions(int selectedWaveformIndex, bool openWaveformPoolEditor)
    {
        SelectedWaveformIndex = selectedWaveformIndex;
        OpenWaveformPoolEditor = openWaveformPoolEditor;
    }

    public bool HasWaveformSelection => SelectedWaveformIndex >= 0;
}
