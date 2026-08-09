using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Paints fading trails from random walkers moving through tile neighbor links.
/// </summary>
[Serializable]
[EffectSyncSettings(typeof(NibblerSyncSettingsAsset))]
public class Nibbler : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored threshold above which an activation uses per-step random hues in Standalone Mode.</summary>
    private const float StandaloneRandomColorThreshold = 0.5f;

    /// <summary>Authored minimum trail retention for the unchanged Standalone look.</summary>
    private const float StandaloneFadeMin = 0.97f;

    /// <summary>Authored maximum trail retention for the unchanged Standalone look.</summary>
    private const float StandaloneFadeMax = 0.999f;

    /// <summary>Authored minimum activation hue; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneColorHueMin = 0f;

    /// <summary>Authored maximum activation hue; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneColorHueMax = 1f;

    /// <summary>Authored minimum hue rolled per walker step by a random-color activation; deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneStepHueMin = 0f;

    /// <summary>Authored maximum hue rolled per walker step by a random-color activation; deliberately spans the complete HSV hue domain.</summary>
    private const float StandaloneStepHueMax = 1f;

    /// <summary>Authored inclusive minimum for the Standalone beat-mode roll.</summary>
    private const int StandaloneBeatModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Standalone beat-mode roll.</summary>
    private const int StandaloneBeatModeMaxExclusive = 2;

    /// <summary>Authored brightness used when no live Waveform placement exists.</summary>
    private const float StandaloneBeatBrightness = 0.75f;

    /// <summary>Authored random-walker step rate for the unchanged Standalone look.</summary>
    private const float StandaloneWalkerStepsPerSecond = 300f;

    // Sync Defaults

    /// <summary>Authored threshold above which an activation uses per-step random hues in Synced Mode.</summary>
    private const float SyncRandomColorThreshold = 0.5f;

    /// <summary>Authored minimum trail retention in Synced Mode.</summary>
    private const float SyncFadeMin = 0.97f;

    /// <summary>Authored maximum trail retention in Synced Mode.</summary>
    private const float SyncFadeMax = 0.999f;

    /// <summary>Authored minimum activation hue in Synced Mode; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float SyncColorHueMin = 0f;

    /// <summary>Authored maximum activation hue in Synced Mode; the authored range deliberately spans the complete HSV hue domain.</summary>
    private const float SyncColorHueMax = 1f;

    /// <summary>Authored minimum hue rolled per walker step in Synced Mode; deliberately spans the complete HSV hue domain.</summary>
    private const float SyncStepHueMin = 0f;

    /// <summary>Authored maximum hue rolled per walker step in Synced Mode; deliberately spans the complete HSV hue domain.</summary>
    private const float SyncStepHueMax = 1f;

    /// <summary>Authored inclusive minimum for the Synced beat-mode roll.</summary>
    private const int SyncBeatModeMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Synced beat-mode roll.</summary>
    private const int SyncBeatModeMaxExclusive = 2;

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

    /// <summary>Resolves a fresh immutable-by-convention copy of Nibbler's Standalone Defaults.</summary>
    public static NibblerStandaloneSettings StandaloneSettings => new NibblerStandaloneSettings(
        StandaloneRandomColorThreshold,
        new FloatRange(StandaloneFadeMin, StandaloneFadeMax),
        new FloatRange(StandaloneColorHueMin, StandaloneColorHueMax),
        new FloatRange(StandaloneStepHueMin, StandaloneStepHueMax),
        StandaloneBeatModeMinInclusive,
        StandaloneBeatModeMaxExclusive,
        StandaloneBeatBrightness,
        StandaloneWalkerStepsPerSecond);

    /// <summary>Resolves a fresh copy of Nibbler's file-local Sync Defaults.</summary>
    public static NibblerSyncSettings SyncDefaults => new NibblerSyncSettings
    {
        RandomColorThreshold = SyncRandomColorThreshold,
        FadeMin = SyncFadeMin,
        FadeMax = SyncFadeMax,
        ColorHueMin = SyncColorHueMin,
        ColorHueMax = SyncColorHueMax,
        StepHueMin = SyncStepHueMin,
        StepHueMax = SyncStepHueMax,
        BeatModeMinInclusive = SyncBeatModeMinInclusive,
        BeatModeMaxExclusive = SyncBeatModeMaxExclusive,
        BeatBrightnessAtPeak = SyncBeatBrightnessAtPeak,
        BeatBrightnessAtTrough = SyncBeatBrightnessAtTrough,
        BeatHueShift = SyncBeatHueShift,
        WalkerStepsPerSecond = SyncWalkerStepsPerSecond,
    };

    /// <summary>Number of random walkers maintained for the effect's fixed simulation shape.</summary>
    private const int Count = 10;

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private NibblerStandaloneSettings standaloneSettings = StandaloneSettings;

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
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Nibbler),
            SyncDefaults);

        waveform = waveforms.Random();
        float randomColorThreshold = beatManager.IsSynced
            ? SyncSettings.RandomColorThreshold
            : standaloneSettings.RandomColorThreshold;
        if (Random.value > randomColorThreshold)
        {
            randomColor = true;
            color = Color.clear;
        }
        else
        {
            randomColor = false;
        }
        float colorHueMin = beatManager.IsSynced ? SyncSettings.ColorHueMin : standaloneSettings.ColorHue.Min;
        float colorHueMax = beatManager.IsSynced ? SyncSettings.ColorHueMax : standaloneSettings.ColorHue.Max;
        color = Color.HSVToRGB(Mathf.Lerp(colorHueMin, colorHueMax, Random.value), 1f, 1f);

        float fadeMin = beatManager.IsSynced ? SyncSettings.FadeMin : standaloneSettings.Fade.Min;
        float fadeMax = beatManager.IsSynced ? SyncSettings.FadeMax : standaloneSettings.Fade.Max;
        fade = Random.Range(fadeMin, fadeMax);
        buffer.Clear();
        int beatModeMin = beatManager.IsSynced
            ? SyncSettings.BeatModeMinInclusive
            : standaloneSettings.BeatModeMinInclusive;
        int beatModeMax = beatManager.IsSynced
            ? SyncSettings.BeatModeMaxExclusive
            : standaloneSettings.BeatModeMaxExclusive;
        beatMode = Random.Range(beatModeMin, beatModeMax);
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
            beatManager.IsSynced ? SyncSettings.BeatBrightnessAtPeak : standaloneSettings.BeatBrightness);
        float beatHue = SyncSettings.BeatHueShift * rhythm;
        buffer.Fade(fade);

        float localDelta = DropSlowdown(effectDelta);

        float walkerStepsPerSecond = beatManager.IsSynced
            ? SyncSettings.WalkerStepsPerSecond
            : standaloneSettings.WalkerStepsPerSecond;
        int count = (int)(localDelta * walkerStepsPerSecond);

        // Hoisted out of the walker loops; the per-step Random.value roll itself stays per step.
        float stepHueMin = beatManager.IsSynced ? SyncSettings.StepHueMin : standaloneSettings.StepHue.Min;
        float stepHueMax = beatManager.IsSynced ? SyncSettings.StepHueMax : standaloneSettings.StepHue.Max;

        for (int y = 0; y < Count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                current[y] = tiles[current[y]].GetRandomNeighbor();
                Color c = randomColor
                    ? Color.HSVToRGB(Mathf.Lerp(stepHueMin, stepHueMax, Random.value), 1f, 1f)
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

/// <summary>The non-editable Standalone Settings that reproduce Nibbler's authored no-music look.</summary>
public sealed class NibblerStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Nibbler's file-local defaults.</summary>
    public NibblerStandaloneSettings(
        float randomColorThreshold,
        FloatRange fade,
        FloatRange colorHue,
        FloatRange stepHue,
        int beatModeMinInclusive,
        int beatModeMaxExclusive,
        float beatBrightness,
        float walkerStepsPerSecond)
    {
        RandomColorThreshold = randomColorThreshold;
        Fade = fade;
        ColorHue = colorHue;
        StepHue = stepHue;
        BeatModeMinInclusive = beatModeMinInclusive;
        BeatModeMaxExclusive = beatModeMaxExclusive;
        BeatBrightness = beatBrightness;
        WalkerStepsPerSecond = walkerStepsPerSecond;
    }

    /// <summary>Threshold above which an activation uses per-step random hues.</summary>
    public float RandomColorThreshold;

    /// <summary>Per-activation trail-retention range.</summary>
    public FloatRange Fade;

    /// <summary>Per-activation base-hue range for the fixed walker color.</summary>
    public FloatRange ColorHue;

    /// <summary>Per-step hue range rolled by a random-color activation.</summary>
    public FloatRange StepHue;

    /// <summary>Inclusive minimum for the per-activation beat-mode roll.</summary>
    public int BeatModeMinInclusive;

    /// <summary>Exclusive maximum for the per-activation beat-mode roll.</summary>
    public int BeatModeMaxExclusive;

    /// <summary>Brightness used when no live Waveform placement exists.</summary>
    public float BeatBrightness;

    /// <summary>Random-walker steps performed per second.</summary>
    public float WalkerStepsPerSecond;
}

/// <summary>Editable music-response values saved as Nibbler's Sync Settings.</summary>
[Serializable]
public sealed class NibblerSyncSettings
{
    /// <summary>Threshold above which an activation uses per-step random hues.</summary>
    [Range(0f, 1f)] public float RandomColorThreshold;

    /// <summary>Minimum trail retention rolled per activation.</summary>
    [Range(0f, 1f)] public float FadeMin;

    /// <summary>Maximum trail retention rolled per activation.</summary>
    [Range(0f, 1f)] public float FadeMax;

    /// <summary>Minimum activation hue for the fixed walker color.</summary>
    [Range(0f, 1f)] public float ColorHueMin;

    /// <summary>Maximum activation hue for the fixed walker color.</summary>
    [Range(0f, 1f)] public float ColorHueMax;

    /// <summary>Minimum hue rolled per walker step by a random-color activation.</summary>
    [Range(0f, 1f)] public float StepHueMin;

    /// <summary>Maximum hue rolled per walker step by a random-color activation.</summary>
    [Range(0f, 1f)] public float StepHueMax;

    /// <summary>Inclusive minimum for the per-activation beat-mode roll.</summary>
    [Min(0)] public int BeatModeMinInclusive;

    /// <summary>Exclusive maximum for the per-activation beat-mode roll.</summary>
    [Min(1)] public int BeatModeMaxExclusive;

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

        RandomColorThreshold = source.RandomColorThreshold;
        FadeMin = source.FadeMin;
        FadeMax = source.FadeMax;
        ColorHueMin = source.ColorHueMin;
        ColorHueMax = source.ColorHueMax;
        StepHueMin = source.StepHueMin;
        StepHueMax = source.StepHueMax;
        BeatModeMinInclusive = source.BeatModeMinInclusive;
        BeatModeMaxExclusive = source.BeatModeMaxExclusive;
        BeatBrightnessAtPeak = source.BeatBrightnessAtPeak;
        BeatBrightnessAtTrough = source.BeatBrightnessAtTrough;
        BeatHueShift = source.BeatHueShift;
        WalkerStepsPerSecond = source.WalkerStepsPerSecond;
    }
}
