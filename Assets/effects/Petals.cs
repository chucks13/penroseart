using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Colors packed Ring, Arc, Starball, and Star groups with layered petal-style palettes.
/// </summary>
[EffectSyncSettings(typeof(PetalsSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(PetalsStandaloneSettingsAsset))]
public class Petals : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored minimum hue rolled for every layer color in the unchanged Standalone look.</summary>
    private const float StandaloneLayerHueMin = 0f;

    /// <summary>Authored maximum hue rolled for every layer color in the unchanged Standalone look.</summary>
    private const float StandaloneLayerHueMax = 1f;

    /// <summary>Authored minimum saturation rolled for every layer color in the unchanged Standalone look.</summary>
    private const float StandaloneLayerSaturationMin = 0f;

    /// <summary>Authored maximum saturation rolled for every layer color in the unchanged Standalone look.</summary>
    private const float StandaloneLayerSaturationMax = 1f;

    /// <summary>Authored minimum starting background hue in the unchanged Standalone look.</summary>
    private const float StandaloneBackgroundHueMin = 0f;

    /// <summary>Authored maximum starting background hue in the unchanged Standalone look.</summary>
    private const float StandaloneBackgroundHueMax = 1f;

    /// <summary>Authored background hue advance per second in the unchanged Standalone look.</summary>
    private const float StandaloneBackgroundHueAdvance = 0.1f;

    /// <summary>
    /// Authored Waveform to-slot brightness for the unchanged Standalone look; the to-slot is both
    /// the Waveform peak and the fallback returned without a live Bar Phase.
    /// </summary>
    private const float StandaloneWaveformBrightnessAtPeak = 1f;

    /// <summary>Authored hue spread between packed-list tile positions in the unchanged Standalone look.</summary>
    private const float StandaloneTileHueSpread = 0.002f;

    /// <summary>Authored hue advance applied to a layer color after each packed shape list in the unchanged Standalone look.</summary>
    private const float StandaloneLayerHueAdvance = 0.00004f;

    /// <summary>Authored inclusive minimum for the Standalone per-frame shape-layer mask draw.</summary>
    private const int StandaloneLayerMaskMinInclusive = 0;

    /// <summary>Authored exclusive maximum for the Standalone per-frame shape-layer mask draw.</summary>
    private const int StandaloneLayerMaskMaxExclusive = 256;

    // Sync Defaults

    /// <summary>Authored minimum hue rolled for every layer color in Synced Mode.</summary>
    private const float SyncLayerHueMin = 0f;

    /// <summary>Authored maximum hue rolled for every layer color in Synced Mode.</summary>
    private const float SyncLayerHueMax = 1f;

    /// <summary>Authored minimum saturation rolled for every layer color in Synced Mode.</summary>
    private const float SyncLayerSaturationMin = 0f;

    /// <summary>Authored maximum saturation rolled for every layer color in Synced Mode.</summary>
    private const float SyncLayerSaturationMax = 1f;

    /// <summary>Authored minimum starting background hue in Synced Mode.</summary>
    private const float SyncBackgroundHueMin = 0f;

    /// <summary>Authored maximum starting background hue in Synced Mode.</summary>
    private const float SyncBackgroundHueMax = 1f;

    /// <summary>Authored background hue advance per second in Synced Mode.</summary>
    private const float SyncBackgroundHueAdvance = 0.1f;

    /// <summary>Authored brightness at the Waveform trough in Synced Mode.</summary>
    private const float SyncWaveformBrightnessAtTrough = 0.85f;

    /// <summary>Authored brightness at the Waveform peak in Synced Mode.</summary>
    private const float SyncWaveformBrightnessAtPeak = 1f;

    /// <summary>Authored layer hue shift at the Waveform peak in Synced Mode.</summary>
    private const float SyncWaveformHueShiftAtPeak = 0.001f;

    /// <summary>Authored inclusive minimum for the per-frame shape-layer mask draw.</summary>
    private const int SyncLayerMaskMinInclusive = 0;

    /// <summary>
    /// Authored exclusive maximum for the per-frame shape-layer mask draw. The consuming code tests
    /// only <c>1 &lt;&lt; 1</c>, so this preserves the existing eight-bit draw without claiming that all bits select layers.
    /// </summary>
    private const int SyncLayerMaskMaxExclusive = 256;

    /// <summary>Authored probability that each tile sparkles during an active Drop or Fill.</summary>
    private const float SyncSparkleChance = 0.25f;

    /// <summary>
    /// Authored fixed per-Tile index step used by the Drop and Fill sparkle phase. The unfinished alternative
    /// would roll from 0.0004f through 0.003f; keeping the fixed value preserves the approved look and Random
    /// consumption.
    /// </summary>
    private const float SyncSparklePhaseTileIndexStep = 0.001f;

    /// <summary>
    /// Authored fixed speed used by the Drop and Fill sparkle phase. The unfinished alternative would roll
    /// from 0.1f through 1f; keeping the fixed value preserves the approved look and Random consumption.
    /// </summary>
    private const float SyncSparklePhaseSpeed = 0.5f;

    /// <summary>Authored HDR value of tiles replaced by a Drop or Fill sparkle.</summary>
    private const float SyncSparkleValue = 10f;

    /// <summary>Authored hue spread between packed-list tile positions in Synced Mode.</summary>
    private const float SyncTileHueSpread = 0.002f;

    /// <summary>Authored hue advance applied to a layer color after each packed shape list in Synced Mode.</summary>
    private const float SyncLayerHueAdvance = 0.00004f;

    /// <summary>Petals' gentle bloom suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh copy of Petals' file-local Standalone Defaults.</summary>
    public static PetalsStandaloneSettings StandaloneDefaults => new()
    {
        LayerHue = new FloatRange(StandaloneLayerHueMin, StandaloneLayerHueMax),
        LayerSaturation = new FloatRange(StandaloneLayerSaturationMin, StandaloneLayerSaturationMax),
        BackgroundHue = new FloatRange(StandaloneBackgroundHueMin, StandaloneBackgroundHueMax),
        BackgroundHueAdvance = StandaloneBackgroundHueAdvance,
        WaveformBrightnessAtPeak = StandaloneWaveformBrightnessAtPeak,
        TileHueSpread = StandaloneTileHueSpread,
        LayerHueAdvance = StandaloneLayerHueAdvance,
        LayerMask = new IntRange(StandaloneLayerMaskMinInclusive, StandaloneLayerMaskMaxExclusive),
    };

    /// <summary>Resolves a fresh copy of Petals' file-local Sync Defaults.</summary>
    public static PetalsSyncSettings SyncDefaults => new()
    {
        LayerHue = new FloatRange(SyncLayerHueMin, SyncLayerHueMax),
        LayerSaturation = new FloatRange(SyncLayerSaturationMin, SyncLayerSaturationMax),
        BackgroundHue = new FloatRange(SyncBackgroundHueMin, SyncBackgroundHueMax),
        BackgroundHueAdvance = SyncBackgroundHueAdvance,
        WaveformBrightnessAtTrough = SyncWaveformBrightnessAtTrough,
        WaveformBrightnessAtPeak = SyncWaveformBrightnessAtPeak,
        WaveformHueShiftAtPeak = SyncWaveformHueShiftAtPeak,
        LayerMask = new IntRange(SyncLayerMaskMinInclusive, SyncLayerMaskMaxExclusive),
        SparkleChance = SyncSparkleChance,
        SparklePhaseTileIndexStep = SyncSparklePhaseTileIndexStep,
        SparklePhaseSpeed = SyncSparklePhaseSpeed,
        SparkleValue = SyncSparkleValue,
        TileHueSpread = SyncTileHueSpread,
        LayerHueAdvance = SyncLayerHueAdvance,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private PetalsStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private PetalsSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The four per-activation HSV layer-color rolls; Draw currently consumes the first three.</summary>
    private Color[] colors;

    /// <summary>The current background hue, rolled at activation and advanced every frame.</summary>
    private float background;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => string.Empty;

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Petals),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Petals),
            SyncDefaults);
        waveform = waveforms.Random();

        bool isSynced = beatManager.IsSynced;
        FloatRange layerHue = isSynced ? SyncSettings.LayerHue : standaloneSettings.LayerHue;
        FloatRange layerSaturation = isSynced
            ? SyncSettings.LayerSaturation
            : standaloneSettings.LayerSaturation;
        colors = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            colors[i] = Color.HSVToRGB(
                Mathf.Lerp(layerHue.Min, layerHue.Max, Random.value),
                Mathf.Lerp(layerSaturation.Min, layerSaturation.Max, Random.value),
                1f);
        }

        FloatRange backgroundHue = isSynced ? SyncSettings.BackgroundHue : standaloneSettings.BackgroundHue;
        background = Mathf.Lerp(backgroundHue.Min, backgroundHue.Max, Random.value);
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
        bool isSynced = beatManager.IsSynced;
        float rhythm = waveform.Envelope;
        float waveformBrightnessAtPeak = isSynced
            ? SyncSettings.WaveformBrightnessAtPeak
            : standaloneSettings.WaveformBrightnessAtPeak;
        float beatBrightness = waveform.Lerp(
            SyncSettings.WaveformBrightnessAtTrough,
            waveformBrightnessAtPeak);
        float hueShift = SyncSettings.WaveformHueShiftAtPeak * rhythm;
        float backgroundHueAdvance = isSynced
            ? SyncSettings.BackgroundHueAdvance
            : standaloneSettings.BackgroundHueAdvance;
        float tileHueSpread = isSynced ? SyncSettings.TileHueSpread : standaloneSettings.TileHueSpread;
        float layerHueAdvance = isSynced ? SyncSettings.LayerHueAdvance : standaloneSettings.LayerHueAdvance;
        float sparkleChance = SyncSettings.SparkleChance;
        float sparklePhaseTileIndexStep = SyncSettings.SparklePhaseTileIndexStep;
        float sparklePhaseSpeed = SyncSettings.SparklePhaseSpeed;
        float sparkleValue = SyncSettings.SparkleValue;
        IntRange layerMask = isSynced ? SyncSettings.LayerMask : standaloneSettings.LayerMask;
        int bitMask = Random.Range(layerMask.MinInclusive, layerMask.MaxExclusive);       // randomly select shape layers with bit masks
        background += effectDelta * backgroundHueAdvance;
        background %= 1f;
        Color backgroundColor = Color.HSVToRGB(background, 1f, 1f) * beatBrightness;
        bool dropActive = beatManager.Drop.Active;
        for (int i = 0; i < buffer.Length; i++)
        {
            Color color = backgroundColor;
            if (dropActive && Random.value < sparkleChance)
            {
                float phase = (i * sparklePhaseTileIndexStep + (effectTime * sparklePhaseSpeed)) % 1f;
                color = Color.HSVToRGB(phase, 1f, sparkleValue);
            }
            buffer[i] = color;
        }

        bool shiftLayerHue = ((1 << 1) & bitMask) == 0;
        bool fillActive = beatManager.Fill.Active;
        for (int shapeIdx = 0; shapeIdx < 3; shapeIdx++)
        {
            LayoutData.ShapeList.Reader shape = shapeIdx switch
            {
                0 => penrose.Layout.shapes.Rings,
                1 => penrose.Layout.shapes.Starballs,
                2 => penrose.Layout.shapes.Stars,
                _ => default
            };
            for (int i = 0; i < shape.GroupCount; i++)
            {
                LayoutData.ShapeList.Group group = shape.GetGroup(i);
                Color.RGBToHSV(colors[shapeIdx], out float hue, out float sat, out float bri);
                if (shiftLayerHue)
                {
                    hue += hueShift;
                }

                for (int j = 0; j < group.TileCount; j++)
                {
                    int idx = group[j];
                    Color color = Color.HSVToRGB(
                        (hue + tileHueSpread * group.PackedIndex(j)) % 1f,
                        sat,
                        bri) * beatBrightness;
                    if (fillActive && Random.value < sparkleChance)
                    {
                        float phase = (idx * sparklePhaseTileIndexStep + (effectTime * sparklePhaseSpeed)) % 1f;
                        color = Color.HSVToRGB(phase, 1f, sparkleValue);
                    }
                    buffer[idx] = color;
                }
                colors[shapeIdx] = Color.HSVToRGB((hue + layerHueAdvance) % 1f, sat, bri);
            }
        }
    }

}

/// <summary>
/// The serializable value shape shared by Petals' Standalone Defaults and saved Standalone Settings.
/// </summary>
[Serializable]
public sealed class PetalsStandaloneSettings
{
    /// <summary>Per-activation hue range for each randomly rolled layer color.</summary>
    public FloatRange LayerHue;

    /// <summary>Per-activation saturation range for each randomly rolled layer color.</summary>
    public FloatRange LayerSaturation;

    /// <summary>Per-activation starting background-hue range.</summary>
    public FloatRange BackgroundHue;

    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueAdvance;

    /// <summary>Waveform peak and no-Bar-Phase fallback brightness.</summary>
    public float WaveformBrightnessAtPeak;

    /// <summary>Hue spread between packed-list tile positions.</summary>
    public float TileHueSpread;

    /// <summary>Hue advance applied to a layer color after each packed shape list.</summary>
    public float LayerHueAdvance;

    /// <summary>Per-frame range for the random shape-layer mask draw.</summary>
    public IntRange LayerMask;

    /// <summary>Copies every Petals Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(PetalsStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        LayerHue = Copy(source.LayerHue);
        LayerSaturation = Copy(source.LayerSaturation);
        BackgroundHue = Copy(source.BackgroundHue);
        BackgroundHueAdvance = source.BackgroundHueAdvance;
        WaveformBrightnessAtPeak = source.WaveformBrightnessAtPeak;
        TileHueSpread = source.TileHueSpread;
        LayerHueAdvance = source.LayerHueAdvance;
        LayerMask = Copy(source.LayerMask);
    }

    /// <summary>Copies a floating-point range without sharing its mutable serialized instance.</summary>
    private static FloatRange Copy(FloatRange source) =>
        new(source.Min, source.Max, source.LowRail, source.HighRail);

    /// <summary>Copies an integer range without sharing its mutable serialized instance.</summary>
    private static IntRange Copy(IntRange source) =>
        new(source.MinInclusive, source.MaxExclusive, source.LowRail, source.HighRail);
}

/// <summary>The saved-or-default musical-response settings used by Petals in Synced Mode.</summary>
[Serializable]
public sealed class PetalsSyncSettings
{
    /// <summary>Range rolled for each layer color's hue on the next Synced activation.</summary>
    public FloatRange LayerHue;

    /// <summary>Range rolled for each layer color's saturation on the next Synced activation.</summary>
    public FloatRange LayerSaturation;

    /// <summary>Starting background-hue range rolled on the next Synced activation.</summary>
    public FloatRange BackgroundHue;

    /// <summary>Background hue advance per second in Synced Mode.</summary>
    public float BackgroundHueAdvance;

    /// <summary>Brightness at the Waveform trough in Synced Mode.</summary>
    [Min(0f)] public float WaveformBrightnessAtTrough;

    /// <summary>Brightness at the Waveform peak in Synced Mode.</summary>
    [Min(0f)] public float WaveformBrightnessAtPeak;

    /// <summary>Layer hue shift at the Waveform peak in Synced Mode.</summary>
    public float WaveformHueShiftAtPeak;

    /// <summary>
    /// Range for the per-frame shape-layer mask draw; the current consumer tests only
    /// <c>1 &lt;&lt; 1</c>, so the full numeric domain is not a layer-selector domain.
    /// </summary>
    public IntRange LayerMask;

    /// <summary>Probability that each tile sparkles during an active Drop or Fill.</summary>
    [Range(0f, 1f)] public float SparkleChance;

    /// <summary>Per-Tile index step used by the Drop and Fill sparkle phase.</summary>
    [Min(0f)] public float SparklePhaseTileIndexStep;

    /// <summary>Speed used by the Drop and Fill sparkle phase.</summary>
    [Min(0f)] public float SparklePhaseSpeed;

    /// <summary>HDR value of a Drop or Fill sparkle.</summary>
    [Min(0f)] public float SparkleValue;

    /// <summary>Hue spread between packed-list tile positions in Synced Mode.</summary>
    public float TileHueSpread;

    /// <summary>Hue advance applied to a layer color after each packed shape list in Synced Mode.</summary>
    public float LayerHueAdvance;

    /// <summary>Copies every Petals Sync Setting from another value.</summary>
    public void CopyFrom(PetalsSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        LayerHue = Copy(source.LayerHue);
        LayerSaturation = Copy(source.LayerSaturation);
        BackgroundHue = Copy(source.BackgroundHue);
        BackgroundHueAdvance = source.BackgroundHueAdvance;
        WaveformBrightnessAtTrough = source.WaveformBrightnessAtTrough;
        WaveformBrightnessAtPeak = source.WaveformBrightnessAtPeak;
        WaveformHueShiftAtPeak = source.WaveformHueShiftAtPeak;
        LayerMask = Copy(source.LayerMask);
        SparkleChance = source.SparkleChance;
        SparklePhaseTileIndexStep = source.SparklePhaseTileIndexStep;
        SparklePhaseSpeed = source.SparklePhaseSpeed;
        SparkleValue = source.SparkleValue;
        TileHueSpread = source.TileHueSpread;
        LayerHueAdvance = source.LayerHueAdvance;
    }

    /// <summary>Copies a floating-point range without sharing its mutable serialized instance.</summary>
    private static FloatRange Copy(FloatRange source) =>
        new(source.Min, source.Max, source.LowRail, source.HighRail);

    /// <summary>Copies an integer range without sharing its mutable serialized instance.</summary>
    private static IntRange Copy(IntRange source) =>
        new(source.MinInclusive, source.MaxExclusive, source.LowRail, source.HighRail);
}
