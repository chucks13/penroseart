using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Splits two child effects into rotating yin-yang-like angular regions.
/// </summary>
[EffectSyncSettings(typeof(YinYangMixerSyncSettingsAsset))]
public class YinYangMixer : MixerBase
{
    // Standalone Defaults

    /// <summary>Authored minimum spin endpoint selected by the unchanged Standalone 50/50 roll.</summary>
    private const float StandaloneSpinMin = -6f;

    /// <summary>Authored maximum spin endpoint selected by the unchanged Standalone 50/50 roll.</summary>
    private const float StandaloneSpinMax = 6f;

    /// <summary>Authored minimum radial-drift endpoint selected by the unchanged Standalone 50/50 roll.</summary>
    private const float StandaloneDriftMin = -6f;

    /// <summary>Authored maximum radial-drift endpoint selected by the unchanged Standalone 50/50 roll.</summary>
    private const float StandaloneDriftMax = 6f;

    /// <summary>Authored angular half-width of each separator ribbon for the unchanged Standalone look.</summary>
    private const float StandaloneRibbonHalfWidth = 20f;

    /// <summary>Authored palette position sampled for the separator ribbon in the unchanged Standalone look.</summary>
    private const float StandaloneRibbonPalettePosition = 0.5f;

    /// <summary>Authored fixed separator-ribbon brightness used when no live Waveform sample exists.</summary>
    private const float StandaloneBeatBrightnessPeak = 1f;

    // Sync Defaults

    /// <summary>Authored minimum spin endpoint selected by the Synced 50/50 roll.</summary>
    private const float SyncSpinMin = -6f;

    /// <summary>Authored maximum spin endpoint selected by the Synced 50/50 roll.</summary>
    private const float SyncSpinMax = 6f;

    /// <summary>Authored minimum radial-drift endpoint selected by the Synced 50/50 roll.</summary>
    private const float SyncDriftMin = -6f;

    /// <summary>Authored maximum radial-drift endpoint selected by the Synced 50/50 roll.</summary>
    private const float SyncDriftMax = 6f;

    /// <summary>Authored angular half-width of each separator ribbon in Synced Mode.</summary>
    private const float SyncRibbonHalfWidth = 20f;

    /// <summary>Authored palette position sampled for the separator ribbon in Synced Mode.</summary>
    private const float SyncRibbonPalettePosition = 0.5f;

    /// <summary>Authored floor of the Waveform-driven separator-ribbon brightness.</summary>
    private const float SyncBeatBrightnessFloor = 0f;

    /// <summary>Authored peak of the Waveform-driven separator-ribbon brightness.</summary>
    private const float SyncBeatBrightnessPeak = 1f;

    /// <summary>YinYangMixer's paired blend suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh immutable-by-convention copy of YinYangMixer's Standalone Defaults.</summary>
    public static YinYangMixerStandaloneSettings StandaloneSettings => new YinYangMixerStandaloneSettings(
        new FloatRange(StandaloneSpinMin, StandaloneSpinMax),
        new FloatRange(StandaloneDriftMin, StandaloneDriftMax),
        StandaloneRibbonHalfWidth,
        StandaloneRibbonPalettePosition,
        StandaloneBeatBrightnessPeak);

    /// <summary>Resolves a fresh copy of YinYangMixer's file-local Sync Defaults.</summary>
    public static YinYangMixerSyncSettings SyncDefaults => new YinYangMixerSyncSettings
    {
        SpinMin = SyncSpinMin,
        SpinMax = SyncSpinMax,
        DriftMin = SyncDriftMin,
        DriftMax = SyncDriftMax,
        RibbonHalfWidth = SyncRibbonHalfWidth,
        RibbonPalettePosition = SyncRibbonPalettePosition,
        BeatBrightnessFloor = SyncBeatBrightnessFloor,
        BeatBrightnessPeak = SyncBeatBrightnessPeak,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private YinYangMixerStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private YinYangMixerSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The two child Effects owned and combined by this Mixer.</summary>
    private EffectBase[] effects;

    /// <summary>The accumulated angular offset of the yin-yang split.</summary>
    private float yina;

    /// <summary>Whether the current activation selected the minimum spin endpoint.</summary>
    private bool spinUsesMinimum;

    /// <summary>Whether the current activation selected the minimum radial-drift endpoint.</summary>
    private bool driftUsesMinimum;

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
        standaloneSettings = StandaloneSettings;
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
            // The parent applies one shared beat pulse after splitting child buffers.
            effects[i].waveform = waveforms.None;
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }
        controller.debugText.text = debugText;
        spinUsesMinimum = Random.value < 0.5;
        driftUsesMinimum = Random.value < 0.5;
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
        float spin = spinUsesMinimum
            ? (isSynced ? SyncSettings.SpinMin : standaloneSettings.Spin.Min)
            : (isSynced ? SyncSettings.SpinMax : standaloneSettings.Spin.Max);
        yina += spin * effectDelta * 60f;
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            // Reassert suppression after UpdateTime because the child may acquire a new Waveform on a Grid wrap.
            effects[i].waveform = waveforms.None;
            effects[i].Draw();
        }
        // Parent beat pulse scales the final split/masked child-effect output.
        float beatBrightnessPeak = isSynced
            ? SyncSettings.BeatBrightnessPeak
            : standaloneSettings.BeatBrightnessPeak;
        float beatBrightness = waveform.Lerp(SyncSettings.BeatBrightnessFloor, beatBrightnessPeak);
        float ribbonPalettePosition = isSynced
            ? SyncSettings.RibbonPalettePosition
            : standaloneSettings.RibbonPalettePosition;
        Color ribbon = APalette.read(ribbonPalettePosition, true) * beatBrightness;
        float ribbonHalfWidth = isSynced
            ? SyncSettings.RibbonHalfWidth
            : standaloneSettings.RibbonHalfWidth;
        float drift = driftUsesMinimum
            ? (isSynced ? SyncSettings.DriftMin : standaloneSettings.Drift.Min)
            : (isSynced ? SyncSettings.DriftMax : standaloneSettings.Drift.Max);

        for (int i = 0; i < buffer.Length; i++)
        {
            float a = tiles[i].angle;
            float r = tiles[i].radius * drift;
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


/// <summary>The non-editable Standalone Settings that reproduce YinYangMixer's authored no-music look.</summary>
public sealed class YinYangMixerStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from YinYangMixer's file-local defaults.</summary>
    public YinYangMixerStandaloneSettings(
        FloatRange spin,
        FloatRange drift,
        float ribbonHalfWidth,
        float ribbonPalettePosition,
        float beatBrightnessPeak)
    {
        Spin = spin;
        Drift = drift;
        RibbonHalfWidth = ribbonHalfWidth;
        RibbonPalettePosition = ribbonPalettePosition;
        BeatBrightnessPeak = beatBrightnessPeak;
    }

    /// <summary>The pair of spin endpoints selected by the activation's 50/50 roll.</summary>
    public FloatRange Spin;

    /// <summary>The pair of radial-drift endpoints selected by the activation's 50/50 roll.</summary>
    public FloatRange Drift;

    /// <summary>Angular half-width of each separator ribbon.</summary>
    public float RibbonHalfWidth;

    /// <summary>Palette position sampled for the separator ribbon.</summary>
    public float RibbonPalettePosition;

    /// <summary>Fixed separator-ribbon brightness used without a live Waveform sample.</summary>
    public float BeatBrightnessPeak;
}

/// <summary>Serializable saved values used by YinYangMixer in Synced Mode.</summary>
[Serializable]
public sealed class YinYangMixerSyncSettings
{
    /// <summary>Minimum spin endpoint selected by the activation's 50/50 roll.</summary>
    public float SpinMin;

    /// <summary>Maximum spin endpoint selected by the activation's 50/50 roll.</summary>
    public float SpinMax;

    /// <summary>Minimum radial-drift endpoint selected by the activation's 50/50 roll.</summary>
    public float DriftMin;

    /// <summary>Maximum radial-drift endpoint selected by the activation's 50/50 roll.</summary>
    public float DriftMax;

    /// <summary>Angular half-width of each separator ribbon.</summary>
    [Min(0f)] public float RibbonHalfWidth;

    /// <summary>Palette position sampled for the separator ribbon.</summary>
    [Range(0f, 1f)] public float RibbonPalettePosition;

    /// <summary>Floor of the Waveform-driven separator-ribbon brightness.</summary>
    [Range(0f, 1f)] public float BeatBrightnessFloor;

    /// <summary>Peak of the Waveform-driven separator-ribbon brightness.</summary>
    [Range(0f, 1f)] public float BeatBrightnessPeak;

    /// <summary>Copies every YinYangMixer Sync Setting from another value.</summary>
    public void CopyFrom(YinYangMixerSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        SpinMin = source.SpinMin;
        SpinMax = source.SpinMax;
        DriftMin = source.DriftMin;
        DriftMax = source.DriftMax;
        RibbonHalfWidth = source.RibbonHalfWidth;
        RibbonPalettePosition = source.RibbonPalettePosition;
        BeatBrightnessFloor = source.BeatBrightnessFloor;
        BeatBrightnessPeak = source.BeatBrightnessPeak;
    }
}
