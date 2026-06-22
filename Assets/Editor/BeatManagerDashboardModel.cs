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

    public readonly bool Active;
    public readonly bool Live;
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
    public readonly PhaseRowView Phase;
    public readonly LevelsRowView Levels;
    public readonly ColorBankRowView ColorBank;

    private BeatManagerDashboardModel(
        bool active,
        bool live,
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
        PhaseRowView phase,
        LevelsRowView levels,
        ColorBankRowView colorBank)
    {
        Active = active;
        Live = live;
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
        Phase = phase;
        Levels = levels;
        ColorBank = colorBank;
    }

    /// <summary>
    /// Builds the dashboard display model from the live runtime object and the on-screen effect's variant.
    /// </summary>
    /// <param name="beatManager">Runtime beat manager, or <c>null</c> when the property cannot resolve one.</param>
    /// <param name="onScreenVariant">The effect variant currently visible on the wall, or <c>-1</c> outside Play Mode.</param>
    public static BeatManagerDashboardModel From(BeatManager beatManager, int onScreenVariant)
    {
        var active = beatManager != null && beatManager.IsActive;
        var live = beatManager != null && beatManager.IsLiveSource;
        var bpmText = beatManager?.Bpm is { } bpm ? $"{bpm:0.##} BPM" : "-- BPM";
        var rightText = beatManager?.PlayersLive is { } players && live ? $"{players} · {bpmText}" : bpmText;
        var beatPulse = Mathf.Clamp01(beatManager?.Pulse ?? 0f);
        var offBeatPulse = Mathf.Clamp01(beatManager?.OffBeatPulse ?? 0f);

        return new BeatManagerDashboardModel(
            active,
            live,
            !active ? "OFFLINE" : live ? "LIVE" : "SIM",
            beatManager?.Track ?? "—",
            rightText,
            beatManager?.BeatInBar ?? UnavailableBeat,
            beatManager?.OnBeat ?? false,
            beatPulse,
            beatManager?.OffBeats,
            offBeatPulse,
            GetClampedEighthPulseValue(beatPulse, offBeatPulse),
            new CountdownChipView("NEXT BEAT", FormatMs(beatManager?.NextBeatMs), alignValueRight: true),
            new CountdownChipView("ON BEAT", beatManager?.OnBeat == true ? "YES" : "NO"),
            new CountdownChipView("NEXT OFF BEAT", FormatMs(beatManager?.NextOffBeatMs), alignValueRight: true),
            new CountdownChipView("OFF BEAT", beatManager?.OffBeat == true ? "YES" : "NO"),
            active ? beatManager.BarPhase : 0f,
            BuildEnvelopeRow(beatManager, onScreenVariant),
            BuildPhraseEventRow(beatManager?.Fill),
            BuildPhraseEventRow(beatManager?.Drop),
            BuildEnergyRow(beatManager?.Energy),
            BuildPhaseRow(beatManager?.Phase),
            BuildLevelsRow(beatManager?.Levels),
            new ColorBankRowView(beatManager?.LevelsRgb, beatManager?.LevelsHue, beatManager?.LevelsPalette));
    }

    public BeatMarkerState GetBeatMarkerState(int beatLabel)
    {
        if (!Active || BeatInBar < 1 || BeatInBar > BeatSlotCount)
        {
            return BeatMarkerState.Disabled;
        }

        if (beatLabel < BeatInBar)
        {
            return BeatMarkerState.Past;
        }

        return beatLabel == BeatInBar ? BeatMarkerState.Current : BeatMarkerState.Future;
    }

    public string GetBeatGlyph(int beatLabel)
    {
        return BuildBeatDotGlyph(Active, BeatInBar, beatLabel);
    }

    public bool IsOffBeatEnabled(int index)
    {
        return Active && OffBeatGates != null && index >= 0 && index < OffBeatGates.Length && OffBeatGates[index];
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

    private static string FormatMs(int? value)
    {
        return value is { } ms ? $"{ms}ms" : "--";
    }

    private static EnvelopeRowView BuildEnvelopeRow(BeatManager beatManager, int onScreenVariant)
    {
        if (beatManager == null)
        {
            return EnvelopeRowView.Null;
        }

        var variant = beatManager.ResolveDisplayVariant(onScreenVariant);
        return beatManager.Envelope(variant) is { } envelope
            ? new EnvelopeRowView(true, envelope, $"{envelope:0.00} · var {variant}")
            : EnvelopeRowView.Null;
    }

    private static PhraseEventRowView BuildPhraseEventRow(PhraseEventInfo? info)
    {
        return info is { } value ? new PhraseEventRowView(true, PhraseEventView.Of(value)) : PhraseEventRowView.Null;
    }

    private static EnergyRowView BuildEnergyRow(EnergyInfo? info)
    {
        if (!(info is { } energy))
        {
            return EnergyRowView.Null;
        }

        var arrow = energy.direction > 0 ? "↗" : energy.direction < 0 ? "↘" : "→";
        var heading = energy.next is { } next
            ? $"{arrow} {next.ToString().ToUpperInvariant()} in {RhythmText.Beats(energy.beatsUntilChange)}"
            : "steady";
        return new EnergyRowView(
            true,
            energy.level.ToString().ToUpperInvariant(),
            energy.level,
            energy.runProgress ?? 0f,
            $"{heading} · ×{RhythmText.Count(energy.changesRemaining)}");
    }

    private static PhaseRowView BuildPhaseRow(PhaseInfo? info)
    {
        if (!(info is { } phase))
        {
            return PhaseRowView.Null;
        }

        var heading = phase.next != null
            ? $"→ {phase.next} in {RhythmText.Beats(phase.beatsUntilNext)}"
            : $"len {RhythmText.Count(phase.lengthBeats)}";
        return new PhaseRowView(true, phase.label, phase.progress ?? 0f, heading);
    }

    private static LevelsRowView BuildLevelsRow(LevelsInfo? info)
    {
        return info is { } levels ? new LevelsRowView(true, levels.low, levels.mid, levels.high) : LevelsRowView.Null;
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
    public static readonly EnergyRowView Null = new EnergyRowView(false, string.Empty, EnergyLevel.Low, 0f, string.Empty);

    public readonly bool HasValue;
    public readonly string Chip;
    public readonly EnergyLevel Level;
    public readonly float Meter;
    public readonly string Readout;

    public EnergyRowView(bool hasValue, string chip, EnergyLevel level, float meter, string readout)
    {
        HasValue = hasValue;
        Chip = chip;
        Level = level;
        Meter = meter;
        Readout = readout;
    }
}

internal readonly struct PhaseRowView
{
    public static readonly PhaseRowView Null = new PhaseRowView(false, string.Empty, 0f, string.Empty);

    public readonly bool HasValue;
    public readonly string Label;
    public readonly float Meter;
    public readonly string Readout;

    public PhaseRowView(bool hasValue, string label, float meter, string readout)
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

internal readonly struct WaveformSelectorView
{
    public readonly bool Live;
    public readonly int ShownIndex;
    public readonly string[] Options;

    public WaveformSelectorView(bool live, int shownIndex, string[] options)
    {
        Live = live;
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
