using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Paints fading trails from random walkers moving through tile neighbor links.
/// </summary>
[Serializable]
[EffectSyncSettings(typeof(NibblerSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(NibblerStandaloneSettingsAsset))]
public class Nibbler : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored chance that an activation keeps one fixed hue in Standalone Mode.</summary>
    private const float StandaloneFixedColorChance = 0.5f;

    /// <summary>Authored minimum trail retention for the unchanged Standalone look.</summary>
    private const float StandaloneTrailRetentionMin = 0.97f;

    /// <summary>Authored maximum trail retention for the unchanged Standalone look.</summary>
    private const float StandaloneTrailRetentionMax = 0.999f;

    /// <summary>Authored minimum activation hue; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneFixedColorHueMin = 0f;

    /// <summary>Authored maximum activation hue; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneFixedColorHueMax = 1f;

    /// <summary>Authored minimum hue rolled per walker step by a random-color activation; deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneStepHueMin = 0f;

    /// <summary>Authored maximum hue rolled per walker step by a random-color activation; deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneStepHueMax = 1f;

    /// <summary>Authored inclusive minimum for the Standalone Waveform hue-mode roll.</summary>
    private const int StandaloneWaveformHueModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Standalone Waveform hue-mode roll.</summary>
    private const int StandaloneWaveformHueModeMaxExclusive = 2;

    /// <summary>Authored brightness used when no live Waveform placement exists.</summary>
    private const float StandaloneBeatBrightnessAtPeak = 0.75f;

    /// <summary>Authored random-walker step rate for the unchanged Standalone look.</summary>
    private const float StandaloneWalkerStepsPerSecond = 300f;

    // Sync Defaults

    /// <summary>Authored chance that an activation keeps one fixed hue in Synced Mode.</summary>
    private const float SyncFixedColorChance = 0.5f;

    /// <summary>Authored minimum trail retention in Synced Mode.</summary>
    private const float SyncTrailRetentionMin = 0.97f;

    /// <summary>Authored maximum trail retention in Synced Mode.</summary>
    private const float SyncTrailRetentionMax = 0.999f;

    /// <summary>Authored minimum activation hue in Synced Mode; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float SyncFixedColorHueMin = 0f;

    /// <summary>Authored maximum activation hue in Synced Mode; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float SyncFixedColorHueMax = 1f;

    /// <summary>Authored minimum hue rolled per walker step in Synced Mode; deliberately spans the complete HSV hue domain.</summary>
    private const float SyncStepHueMin = 0f;

    /// <summary>Authored maximum hue rolled per walker step in Synced Mode; deliberately spans the complete HSV hue domain.</summary>
    private const float SyncStepHueMax = 1f;

    /// <summary>Authored inclusive minimum for the Synced Waveform hue-mode roll.</summary>
    private const int SyncWaveformHueModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Synced Waveform hue-mode roll.</summary>
    private const int SyncWaveformHueModeMaxExclusive = 2;

    /// <summary>Authored brightness reached at a Waveform trough in Synced Mode.</summary>
    private const float SyncBeatBrightnessAtTrough = 1f;

    /// <summary>Authored brightness reached at a Waveform peak in Synced Mode; the peak slot doubles as the Standalone fallback, so its Standalone twin lives above.</summary>
    private const float SyncBeatBrightnessAtPeak = 0.75f;

    /// <summary>Authored maximum hue shift contributed by the Waveform in Synced Mode.</summary>
    private const float SyncBeatHueShift = 0.5f;

    /// <summary>Authored random-walker step rate in Synced Mode.</summary>
    private const float SyncWalkerStepsPerSecond = 300f;

    /// <summary>Nibbler's roaming eaters suit Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh copy so saved Standalone Settings can never mutate Nibbler's authored Standalone Defaults.</summary>
    public static NibblerStandaloneSettings StandaloneDefaults => new()
    {
        FixedColorChance = StandaloneFixedColorChance,
        TrailRetention = new FloatRange(
            StandaloneTrailRetentionMin,
            StandaloneTrailRetentionMax),
        FixedColorHue = new FloatRange(StandaloneFixedColorHueMin, StandaloneFixedColorHueMax),
        StepHue = new FloatRange(StandaloneStepHueMin, StandaloneStepHueMax),
        WaveformHueMode = new IntRange(
            StandaloneWaveformHueModeMinInclusive,
            StandaloneWaveformHueModeMaxExclusive),
        BeatBrightnessAtPeak = StandaloneBeatBrightnessAtPeak,
        WalkerStepsPerSecond = StandaloneWalkerStepsPerSecond,
    };

    /// <summary>Resolves a fresh copy of Nibbler's file-local Sync Defaults.</summary>
    public static NibblerSyncSettings SyncDefaults => new()
    {
        FixedColorChance = SyncFixedColorChance,
        TrailRetention = new FloatRange(SyncTrailRetentionMin, SyncTrailRetentionMax),
        FixedColorHue = new FloatRange(SyncFixedColorHueMin, SyncFixedColorHueMax),
        StepHue = new FloatRange(SyncStepHueMin, SyncStepHueMax),
        WaveformHueMode = new IntRange(
            SyncWaveformHueModeMinInclusive,
            SyncWaveformHueModeMaxExclusive),
        BeatBrightnessAtPeak = SyncBeatBrightnessAtPeak,
        BeatBrightnessAtTrough = SyncBeatBrightnessAtTrough,
        BeatHueShift = SyncBeatHueShift,
        WalkerStepsPerSecond = SyncWalkerStepsPerSecond,
    };

    /// <summary>Number of random walkers maintained for the effect's fixed simulation shape.</summary>
    private const int Count = 10;

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private NibblerStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private NibblerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Current tile index occupied by each random walker.</summary>
    private int[] current;

    /// <summary>Whether this activation rolls a fresh hue for every walker step.</summary>
    private bool randomColor;

    /// <summary>The fixed hue color rolled for this activation.</summary>
    private Color color;

    /// <summary>The trail-retention value rolled for this activation.</summary>
    private float fade;

    /// <summary>The beat-hue mode rolled for this activation.</summary>
    private int beatMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var colorText = (randomColor) ? "random" : color.ToString();
        return $"Color: {colorText}\nFade: {fade}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        current = new int[Count];
        for (int i = 0; i < Count; i++) current[i] = Random.Range(0, Penrose.Total);
    }

    /// <summary>
    /// Resolves Effect Settings, then initializes per-activation random state in the original order.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Nibbler),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Nibbler),
            SyncDefaults);

        waveform = waveforms.Random();
        float fixedColorChance = beatManager.IsSynced
            ? SyncSettings.FixedColorChance
            : standaloneSettings.FixedColorChance;
        if (Random.value > fixedColorChance)
        {
            randomColor = true;
            color = Color.clear;
        }
        else
        {
            randomColor = false;
        }
        FloatRange fixedColorHue = beatManager.IsSynced
            ? SyncSettings.FixedColorHue
            : standaloneSettings.FixedColorHue;
        color = Color.HSVToRGB(
            Mathf.Lerp(fixedColorHue.Min, fixedColorHue.Max, Random.value),
            1f,
            1f);

        FloatRange trailRetention = beatManager.IsSynced
            ? SyncSettings.TrailRetention
            : standaloneSettings.TrailRetention;
        fade = Random.Range(trailRetention.Min, trailRetention.Max);
        buffer.Clear();
        IntRange waveformHueMode = beatManager.IsSynced
            ? SyncSettings.WaveformHueMode
            : standaloneSettings.WaveformHueMode;
        beatMode = Random.Range(waveformHueMode.MinInclusive, waveformHueMode.MaxExclusive);
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // This Effect owns its brightness, hue, and clockless fallback mappings.
        float rhythm = waveform.Envelope;
        float beatBrightness = waveform.Lerp(
            SyncSettings.BeatBrightnessAtTrough,
            beatManager.IsSynced
                ? SyncSettings.BeatBrightnessAtPeak
                : standaloneSettings.BeatBrightnessAtPeak);
        float beatHue = SyncSettings.BeatHueShift * rhythm;
        buffer.Fade(fade);

        float localDelta = DropSlowdown(effectDelta);

        float walkerStepsPerSecond = beatManager.IsSynced
            ? SyncSettings.WalkerStepsPerSecond
            : standaloneSettings.WalkerStepsPerSecond;
        int count = (int)(localDelta * walkerStepsPerSecond);

        // Hoisted out of the walker loops; the per-step Random.value roll itself stays per step.
        FloatRange stepHue = beatManager.IsSynced ? SyncSettings.StepHue : standaloneSettings.StepHue;

        for (int y = 0; y < Count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                current[y] = tiles[current[y]].GetRandomNeighbor();
                Color c = randomColor
                    ? Color.HSVToRGB(Mathf.Lerp(stepHue.Min, stepHue.Max, Random.value), 1f, 1f)
                    : color;

                if (beatMode > 0)
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    h += beatHue;
                    c = Color.HSVToRGB(h % 1f, s, v);
                }

                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    float h, s, v_col;
                    Color.RGBToHSV(c, out h, out s, out v_col);
                    v_col = (h + s + v_col) % 1f;                   // assure there is brightness variation
                    s = 0f;
                    c = Color.HSVToRGB(h, s, v_col);
                }

                buffer[current[y]] = c * beatBrightness;
            }
        }
    }
}

/// <summary>The serializable value shape shared by Nibbler's Standalone Defaults and Settings.</summary>
[Serializable]
public sealed class NibblerStandaloneSettings
{
    /// <summary>Probability that an activation keeps one fixed hue instead of rolling per step.</summary>
    [Range(0f, 1f)] public float FixedColorChance;

    /// <summary>Per-activation trail-retention range.</summary>
    public FloatRange TrailRetention;

    /// <summary>Per-activation base-hue range for the fixed walker color.</summary>
    public FloatRange FixedColorHue;

    /// <summary>Per-step hue range rolled by a random-color activation.</summary>
    public FloatRange StepHue;

    /// <summary>Per-activation mode controlling whether the Waveform shifts hue.</summary>
    public IntRange WaveformHueMode;

    /// <summary>Brightness used when no live Waveform placement exists.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtPeak;

    /// <summary>Random-walker steps performed per second.</summary>
    [Min(0f)] public float WalkerStepsPerSecond;

    /// <summary>Copies every Nibbler Standalone Setting and range Rail from another value.</summary>
    public void CopyFrom(NibblerStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        FixedColorChance = source.FixedColorChance;
        TrailRetention = Copy(source.TrailRetention);
        FixedColorHue = Copy(source.FixedColorHue);
        StepHue = Copy(source.StepHue);
        WaveformHueMode = Copy(source.WaveformHueMode);
        BeatBrightnessAtPeak = source.BeatBrightnessAtPeak;
        WalkerStepsPerSecond = source.WalkerStepsPerSecond;
    }

    /// <summary>Creates an independent copy of a floating-point range and its Rails.</summary>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Creates an independent copy of an integer range and its Rails.</summary>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}

/// <summary>The serializable value shape shared by Nibbler's Sync Defaults and Settings.</summary>
[Serializable]
public sealed class NibblerSyncSettings
{
    /// <summary>Probability that an activation keeps one fixed hue instead of rolling per step.</summary>
    [Range(0f, 1f)] public float FixedColorChance;

    /// <summary>Per-activation trail-retention range.</summary>
    public FloatRange TrailRetention;

    /// <summary>Per-activation base-hue range for the fixed walker color.</summary>
    public FloatRange FixedColorHue;

    /// <summary>Per-step hue range rolled by a random-color activation.</summary>
    public FloatRange StepHue;

    /// <summary>Per-activation mode controlling whether the Waveform shifts hue.</summary>
    public IntRange WaveformHueMode;

    /// <summary>Brightness reached at a Waveform peak.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtPeak;

    /// <summary>Brightness reached at a Waveform trough.</summary>
    [Range(0f, 1f)] public float BeatBrightnessAtTrough;

    /// <summary>Maximum hue shift contributed by the Waveform.</summary>
    [Range(0f, 1f)] public float BeatHueShift;

    /// <summary>Random-walker steps performed per second.</summary>
    [Min(0f)] public float WalkerStepsPerSecond;

    /// <summary>Copies every Nibbler Sync Setting from another value.</summary>
    public void CopyFrom(NibblerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        FixedColorChance = source.FixedColorChance;
        TrailRetention = Copy(source.TrailRetention);
        FixedColorHue = Copy(source.FixedColorHue);
        StepHue = Copy(source.StepHue);
        WaveformHueMode = Copy(source.WaveformHueMode);
        BeatBrightnessAtPeak = source.BeatBrightnessAtPeak;
        BeatBrightnessAtTrough = source.BeatBrightnessAtTrough;
        BeatHueShift = source.BeatHueShift;
        WalkerStepsPerSecond = source.WalkerStepsPerSecond;
    }

    /// <summary>Creates an independent copy of a floating-point range and its Rails.</summary>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Creates an independent copy of an integer range and its Rails.</summary>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}
