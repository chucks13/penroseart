using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Splits two child effects into rotating yin-yang-like angular regions.
/// </summary>
[EffectSyncSettings(typeof(YinYangMixerSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(YinYangMixerStandaloneSettingsAsset))]
public class YinYangMixer : MixerBase
{
    // Standalone Defaults

    /// <summary>Authored minimum spin-rate endpoint selected by the unchanged Standalone 50/50 Roll.</summary>
    private const float StandaloneSpinRateMin = -6f;

    /// <summary>Authored maximum spin-rate endpoint selected by the unchanged Standalone 50/50 Roll.</summary>
    private const float StandaloneSpinRateMax = 6f;

    /// <summary>Authored minimum radius-to-angle scale selected by the unchanged Standalone 50/50 Roll.</summary>
    private const float StandaloneRadialAngleScaleMin = -6f;

    /// <summary>Authored maximum radius-to-angle scale selected by the unchanged Standalone 50/50 Roll.</summary>
    private const float StandaloneRadialAngleScaleMax = 6f;

    /// <summary>Authored angular half-width of each separator ribbon for the unchanged Standalone look.</summary>
    private const float StandaloneRibbonHalfWidth = 20f;

    /// <summary>Authored palette position sampled for the separator ribbon in the unchanged Standalone look.</summary>
    private const float StandaloneRibbonPalettePosition = 0.5f;

    /// <summary>Authored fixed separator-ribbon brightness used when no live Waveform sample exists.</summary>
    private const float StandaloneWaveformBrightnessPeak = 1f;

    // Sync Defaults

    /// <summary>Authored minimum spin-rate endpoint selected by the Synced 50/50 Roll.</summary>
    private const float SyncSpinRateMin = -6f;

    /// <summary>Authored maximum spin-rate endpoint selected by the Synced 50/50 Roll.</summary>
    private const float SyncSpinRateMax = 6f;

    /// <summary>Authored minimum radius-to-angle scale selected by the Synced 50/50 Roll.</summary>
    private const float SyncRadialAngleScaleMin = -6f;

    /// <summary>Authored maximum radius-to-angle scale selected by the Synced 50/50 Roll.</summary>
    private const float SyncRadialAngleScaleMax = 6f;

    /// <summary>Authored angular half-width of each separator ribbon in Synced Mode.</summary>
    private const float SyncRibbonHalfWidth = 20f;

    /// <summary>Authored palette position sampled for the separator ribbon in Synced Mode.</summary>
    private const float SyncRibbonPalettePosition = 0.5f;

    /// <summary>Authored floor of the Waveform-driven separator-ribbon brightness.</summary>
    private const float SyncWaveformBrightnessFloor = 0f;

    /// <summary>Authored peak of the Waveform-driven separator-ribbon brightness.</summary>
    private const float SyncWaveformBrightnessPeak = 1f;

    /// <summary>YinYangMixer's paired blend suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate YinYangMixer's authored
    /// Standalone Defaults.
    /// </summary>
    public static YinYangMixerStandaloneSettings StandaloneDefaults => new()
    {
        SpinRate = new FloatRange(StandaloneSpinRateMin, StandaloneSpinRateMax),
        RadialAngleScale = new FloatRange(
            StandaloneRadialAngleScaleMin,
            StandaloneRadialAngleScaleMax),
        RibbonHalfWidth = StandaloneRibbonHalfWidth,
        RibbonPalettePosition = StandaloneRibbonPalettePosition,
        WaveformBrightnessPeak = StandaloneWaveformBrightnessPeak,
    };

    /// <summary>Resolves a fresh copy of YinYangMixer's file-local Sync Defaults.</summary>
    public static YinYangMixerSyncSettings SyncDefaults => new()
    {
        SpinRate = new FloatRange(SyncSpinRateMin, SyncSpinRateMax),
        RadialAngleScale = new FloatRange(SyncRadialAngleScaleMin, SyncRadialAngleScaleMax),
        RibbonHalfWidth = SyncRibbonHalfWidth,
        RibbonPalettePosition = SyncRibbonPalettePosition,
        WaveformBrightnessFloor = SyncWaveformBrightnessFloor,
        WaveformBrightnessPeak = SyncWaveformBrightnessPeak,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private YinYangMixerStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private YinYangMixerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The two child Effects owned and combined by this Mixer.</summary>
    private EffectBase[] effects;

    /// <summary>The accumulated angular offset of the yin-yang split.</summary>
    private float yina;

    /// <summary>Whether the current activation selected the minimum spin-rate endpoint.</summary>
    private bool spinUsesMinimum;

    /// <summary>Whether the current activation selected the minimum radius-to-angle scale.</summary>
    private bool radialAngleScaleUsesMinimum;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(YinYangMixer),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(YinYangMixer),
            SyncDefaults);
        waveform = waveforms.Random();
        effects = new EffectBase[2];
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            // The parent applies one shared Waveform after splitting child buffers.
            effects[i].waveform = waveforms.None;
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }
        controller.debugText.text = debugText;
        spinUsesMinimum = Random.value < 0.5;
        radialAngleScaleUsesMinimum = Random.value < 0.5;
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
        bool isSynced = beatManager.IsSynced;
        FloatRange spinRateRange = isSynced ? SyncSettings.SpinRate : standaloneSettings.SpinRate;
        float spinRate = spinUsesMinimum ? spinRateRange.Min : spinRateRange.Max;
        yina += spinRate * effectDelta * 60f;
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            // Reassert suppression after UpdateTime because the child may acquire a new Waveform on a Grid wrap.
            effects[i].waveform = waveforms.None;
            effects[i].Draw();
        }
        // The parent's Waveform scales the final split/masked child-effect output.
        float waveformBrightnessPeak = isSynced
            ? SyncSettings.WaveformBrightnessPeak
            : standaloneSettings.WaveformBrightnessPeak;
        float waveformBrightness = waveform.Lerp(
            SyncSettings.WaveformBrightnessFloor,
            waveformBrightnessPeak);
        float ribbonPalettePosition = isSynced
            ? SyncSettings.RibbonPalettePosition
            : standaloneSettings.RibbonPalettePosition;
        Color ribbon = APalette.read(ribbonPalettePosition, true) * waveformBrightness;
        float ribbonHalfWidth = isSynced
            ? SyncSettings.RibbonHalfWidth
            : standaloneSettings.RibbonHalfWidth;
        FloatRange radialAngleScaleRange = isSynced
            ? SyncSettings.RadialAngleScale
            : standaloneSettings.RadialAngleScale;
        float radialAngleScale = radialAngleScaleUsesMinimum
            ? radialAngleScaleRange.Min
            : radialAngleScaleRange.Max;

        for (int i = 0; i < buffer.Length; i++)
        {
            float a = tiles[i].angle;
            float r = tiles[i].radius * radialAngleScale;
            a += yina;
            a += r;
            a += 360000f;
            a %= 360f;

            if (a < ribbonHalfWidth)
            {
                buffer[i] = ribbon;
                continue;
            }
            if (a > 360 - ribbonHalfWidth)
            {
                buffer[i] = ribbon;
                continue;
            }
            if (a < (180f - ribbonHalfWidth))
            {
                buffer[i] = effects[0].buffer[i];
                continue;
            }
            if (a > (180f + ribbonHalfWidth))
            {
                buffer[i] = effects[1].buffer[i];
                continue;
            }
            buffer[i] = ribbon;
        }
    }
}


/// <summary>
/// The serializable value shape shared by YinYangMixer's fully populated Standalone Defaults and
/// saved Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class YinYangMixerStandaloneSettings
{
    /// <summary>The pair of spin-rate endpoints selected by the activation's 50/50 Roll.</summary>
    public FloatRange SpinRate;

    /// <summary>The pair of radius-to-angle scale endpoints selected by the activation's 50/50 Roll.</summary>
    public FloatRange RadialAngleScale;

    /// <summary>Angular half-width of each separator ribbon.</summary>
    [Min(0f)] public float RibbonHalfWidth;

    /// <summary>Palette position sampled for the separator ribbon.</summary>
    [Range(0f, 1f)] public float RibbonPalettePosition;

    /// <summary>Fixed separator-ribbon brightness used without a live Waveform sample.</summary>
    [Range(0f, 1f)] public float WaveformBrightnessPeak;

    /// <summary>Copies every YinYangMixer Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(YinYangMixerStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpinRate = new FloatRange(
            source.SpinRate.Min,
            source.SpinRate.Max,
            source.SpinRate.LowRail,
            source.SpinRate.HighRail);
        RadialAngleScale = new FloatRange(
            source.RadialAngleScale.Min,
            source.RadialAngleScale.Max,
            source.RadialAngleScale.LowRail,
            source.RadialAngleScale.HighRail);
        RibbonHalfWidth = source.RibbonHalfWidth;
        RibbonPalettePosition = source.RibbonPalettePosition;
        WaveformBrightnessPeak = source.WaveformBrightnessPeak;
    }
}

/// <summary>The serializable value shape shared by YinYangMixer's Sync Defaults and Sync Settings.</summary>
[Serializable]
public sealed class YinYangMixerSyncSettings
{
    /// <summary>The pair of spin-rate endpoints selected by the activation's 50/50 Roll.</summary>
    public FloatRange SpinRate;

    /// <summary>The pair of radius-to-angle scale endpoints selected by the activation's 50/50 Roll.</summary>
    public FloatRange RadialAngleScale;

    /// <summary>Angular half-width of each separator ribbon.</summary>
    [Min(0f)] public float RibbonHalfWidth;

    /// <summary>Palette position sampled for the separator ribbon.</summary>
    [Range(0f, 1f)] public float RibbonPalettePosition;

    /// <summary>Floor of the Waveform-driven separator-ribbon brightness.</summary>
    [Range(0f, 1f)] public float WaveformBrightnessFloor;

    /// <summary>Peak of the Waveform-driven separator-ribbon brightness.</summary>
    [Range(0f, 1f)] public float WaveformBrightnessPeak;

    /// <summary>Copies every YinYangMixer Sync Setting from another value.</summary>
    public void CopyFrom(YinYangMixerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpinRate = new FloatRange(
            source.SpinRate.Min,
            source.SpinRate.Max,
            source.SpinRate.LowRail,
            source.SpinRate.HighRail);
        RadialAngleScale = new FloatRange(
            source.RadialAngleScale.Min,
            source.RadialAngleScale.Max,
            source.RadialAngleScale.LowRail,
            source.RadialAngleScale.HighRail);
        RibbonHalfWidth = source.RibbonHalfWidth;
        RibbonPalettePosition = source.RibbonPalettePosition;
        WaveformBrightnessFloor = source.WaveformBrightnessFloor;
        WaveformBrightnessPeak = source.WaveformBrightnessPeak;
    }
}
