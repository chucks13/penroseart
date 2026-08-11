using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Colors packed loop, starball, and star shape groups with layered petal-style palettes.
/// </summary>
[EffectSyncSettings(typeof(PetalsSyncSettingsAsset))]
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
    private const float StandaloneBeatBrightnessAtPeak = 1f;

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
    private const float SyncBeatBrightnessAtTrough = 0.85f;

    /// <summary>Authored brightness at the Waveform peak in Synced Mode.</summary>
    private const float SyncBeatBrightnessAtPeak = 1f;

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
    /// Authored fixed density used by the Drop and Fill sparkle phase. The intended alternative roll
    /// remains unfinished: <c>Random.Range(SyncDropDensityMin, SyncDropDensityMax)</c>.
    /// </summary>
    private const float SyncDropDensity = 0.001f;

    /// <summary>Authored minimum for the unfinished per-activation Drop/Fill sparkle-density roll.</summary>
    private const float SyncDropDensityMin = 0.0004f;

    /// <summary>Authored maximum for the unfinished per-activation Drop/Fill sparkle-density roll.</summary>
    private const float SyncDropDensityMax = 0.003f;

    /// <summary>
    /// Authored fixed speed used by the Drop and Fill sparkle phase. The intended alternative roll
    /// remains unfinished: <c>Random.Range(SyncDropSpeedMin, SyncDropSpeedMax)</c>.
    /// </summary>
    private const float SyncDropSpeed = 0.5f;

    /// <summary>Authored minimum for the unfinished per-activation Drop/Fill sparkle-speed roll.</summary>
    private const float SyncDropSpeedMin = 0.1f;

    /// <summary>Authored maximum for the unfinished per-activation Drop/Fill sparkle-speed roll.</summary>
    private const float SyncDropSpeedMax = 1f;

    /// <summary>Authored HDR value of tiles replaced by a Drop or Fill sparkle.</summary>
    private const float SyncSparkleValue = 10f;

    /// <summary>Authored hue spread between packed-list tile positions in Synced Mode.</summary>
    private const float SyncTileHueSpread = 0.002f;

    /// <summary>Authored hue advance applied to a layer color after each packed shape list in Synced Mode.</summary>
    private const float SyncLayerHueAdvance = 0.00004f;

    /// <summary>Petals' gentle bloom suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh immutable-by-convention copy of Petals' Standalone Defaults.</summary>
    public static PetalsStandaloneSettings StandaloneSettings => new PetalsStandaloneSettings(
        new FloatRange(StandaloneLayerHueMin, StandaloneLayerHueMax),
        new FloatRange(StandaloneLayerSaturationMin, StandaloneLayerSaturationMax),
        new FloatRange(StandaloneBackgroundHueMin, StandaloneBackgroundHueMax),
        StandaloneBackgroundHueAdvance,
        StandaloneBeatBrightnessAtPeak,
        StandaloneTileHueSpread,
        StandaloneLayerHueAdvance,
        StandaloneLayerMaskMinInclusive,
        StandaloneLayerMaskMaxExclusive);

    /// <summary>Resolves a fresh copy of Petals' file-local Sync Defaults.</summary>
    public static PetalsSyncSettings SyncDefaults => new PetalsSyncSettings
    {
        LayerHueMin = SyncLayerHueMin,
        LayerHueMax = SyncLayerHueMax,
        LayerSaturationMin = SyncLayerSaturationMin,
        LayerSaturationMax = SyncLayerSaturationMax,
        BackgroundHueMin = SyncBackgroundHueMin,
        BackgroundHueMax = SyncBackgroundHueMax,
        BackgroundHueAdvance = SyncBackgroundHueAdvance,
        BeatBrightnessAtTrough = SyncBeatBrightnessAtTrough,
        BeatBrightnessAtPeak = SyncBeatBrightnessAtPeak,
        WaveformHueShiftAtPeak = SyncWaveformHueShiftAtPeak,
        LayerMaskMinInclusive = SyncLayerMaskMinInclusive,
        LayerMaskMaxExclusive = SyncLayerMaskMaxExclusive,
        SparkleChance = SyncSparkleChance,
        DropDensity = SyncDropDensity,
        DropSpeed = SyncDropSpeed,
        SparkleValue = SyncSparkleValue,
        TileHueSpread = SyncTileHueSpread,
        LayerHueAdvance = SyncLayerHueAdvance,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private PetalsStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private PetalsSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The four per-activation HSV layer-color rolls; Draw currently consumes the first three.</summary>
    private Color[] colors;

    /// <summary>The current background hue, rolled at activation and advanced every frame.</summary>
    private float background;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() { return $""; }

    /// <summary>
    /// Resolves Effect Settings and initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Petals),
            SyncDefaults);
        waveform = waveforms.Random();

        bool isSynced = beatManager.IsSynced;
        float layerHueMin = isSynced ? SyncSettings.LayerHueMin : standaloneSettings.LayerHue.Min;
        float layerHueMax = isSynced ? SyncSettings.LayerHueMax : standaloneSettings.LayerHue.Max;
        float layerSaturationMin = isSynced
            ? SyncSettings.LayerSaturationMin
            : standaloneSettings.LayerSaturation.Min;
        float layerSaturationMax = isSynced
            ? SyncSettings.LayerSaturationMax
            : standaloneSettings.LayerSaturation.Max;
        colors = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            colors[i] = Color.HSVToRGB(
                Mathf.Lerp(layerHueMin, layerHueMax, Random.value),
                Mathf.Lerp(layerSaturationMin, layerSaturationMax, Random.value),
                1f);
        }

        float backgroundHueMin = isSynced
            ? SyncSettings.BackgroundHueMin
            : standaloneSettings.BackgroundHue.Min;
        float backgroundHueMax = isSynced
            ? SyncSettings.BackgroundHueMax
            : standaloneSettings.BackgroundHue.Max;
        background = Mathf.Lerp(backgroundHueMin, backgroundHueMax, Random.value);
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
        float beatBrightnessAtPeak = isSynced
            ? SyncSettings.BeatBrightnessAtPeak
            : standaloneSettings.BeatBrightnessAtPeak;
        float beatBrightness = waveform.Lerp(SyncSettings.BeatBrightnessAtTrough, beatBrightnessAtPeak);
        float hueShift = SyncSettings.WaveformHueShiftAtPeak * rhythm;
        float backgroundHueAdvance = isSynced
            ? SyncSettings.BackgroundHueAdvance
            : standaloneSettings.BackgroundHueAdvance;
        float tileHueSpread = isSynced ? SyncSettings.TileHueSpread : standaloneSettings.TileHueSpread;
        float layerHueAdvance = isSynced ? SyncSettings.LayerHueAdvance : standaloneSettings.LayerHueAdvance;
        float sparkleChance = SyncSettings.SparkleChance;
        float dropDensity = SyncSettings.DropDensity;
        float dropSpeed = SyncSettings.DropSpeed;
        float sparkleValue = SyncSettings.SparkleValue;
        int layerMaskMin = isSynced
            ? SyncSettings.LayerMaskMinInclusive
            : standaloneSettings.LayerMaskMinInclusive;
        int layerMaskMax = isSynced
            ? SyncSettings.LayerMaskMaxExclusive
            : standaloneSettings.LayerMaskMaxExclusive;
        int bitMask = Random.Range(layerMaskMin, layerMaskMax);       // randomly select shape layers with bit masks
        background += effectDelta * backgroundHueAdvance;
        background %= 1f;
        for (int i = 0; i < buffer.Length; i++)
        {
            Color color = Color.HSVToRGB(background, 1f, 1f) * beatBrightness;
            // drop mode stole part of tunnel for the backbround color
            if (beatManager.Drop.Active)
            {
                if (Random.value < sparkleChance)
                {
                    float phase = (i * dropDensity + (effectTime * dropSpeed)) % 1f;
                    color = Color.HSVToRGB(phase, 1f, sparkleValue);
                }
            }
            buffer[i] = color;
        }

        for (int shapeIdx = 0; shapeIdx < 3; shapeIdx++)
        {
            LayoutData.ShapeList.Reader shape = penrose.Layout.shapes.Read(penrose.Layout.shapes.loops);
            switch (shapeIdx)
            {
                case 0:
                    shape = penrose.Layout.shapes.Read(penrose.Layout.shapes.loops);
                    break;
                case 1:
                    shape = penrose.Layout.shapes.Read(penrose.Layout.shapes.starballs);
                    break;
                case 2:
                    shape = penrose.Layout.shapes.Read(penrose.Layout.shapes.stars);
                    break;
            }
            for (int i = 0; i < shape.GroupCount; i++)
            {
                int thisBit = 1 << 1;
                LayoutData.ShapeList.Group group = shape.GetGroup(i);
                Color.RGBToHSV(colors[shapeIdx], out float hue, out float sat, out float bri);
                if ((thisBit & bitMask) == 0)
                    hue += hueShift;
                for (int j = 0; j < group.TileCount; j++)
                {
                    int idx = group[j];
                    Color color = Color.HSVToRGB(
                        (hue + tileHueSpread * group.PackedIndex(j)) % 1f,
                        sat,
                        bri) * beatBrightness;
                    if (beatManager.Fill.Active)
                    {
                        if (Random.value < sparkleChance)
                        {
                            float phase = (idx * dropDensity + (effectTime * dropSpeed)) % 1f;
                            color = Color.HSVToRGB(phase, 1f, sparkleValue);

                        }
                    }
                    buffer[idx] = color;
                }
                colors[shapeIdx] = Color.HSVToRGB((hue + layerHueAdvance) % 1f, sat, bri);
            }
        }
    }

}

/// <summary>The resolved Standalone Settings that preserve Petals' authored no-music look.</summary>
public sealed class PetalsStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Petals' file-local defaults.</summary>
    public PetalsStandaloneSettings(
        FloatRange layerHue,
        FloatRange layerSaturation,
        FloatRange backgroundHue,
        float backgroundHueAdvance,
        float beatBrightnessAtPeak,
        float tileHueSpread,
        float layerHueAdvance,
        int layerMaskMinInclusive,
        int layerMaskMaxExclusive)
    {
        LayerHue = layerHue;
        LayerSaturation = layerSaturation;
        BackgroundHue = backgroundHue;
        BackgroundHueAdvance = backgroundHueAdvance;
        BeatBrightnessAtPeak = beatBrightnessAtPeak;
        TileHueSpread = tileHueSpread;
        LayerHueAdvance = layerHueAdvance;
        LayerMaskMinInclusive = layerMaskMinInclusive;
        LayerMaskMaxExclusive = layerMaskMaxExclusive;
    }

    /// <summary>Per-activation hue range for each randomly rolled layer color.</summary>
    public FloatRange LayerHue;

    /// <summary>Per-activation saturation range for each randomly rolled layer color.</summary>
    public FloatRange LayerSaturation;

    /// <summary>Per-activation starting background-hue range.</summary>
    public FloatRange BackgroundHue;

    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueAdvance;

    /// <summary>Waveform peak and no-Bar-Phase fallback brightness.</summary>
    public float BeatBrightnessAtPeak;

    /// <summary>Hue spread between packed-list tile positions.</summary>
    public float TileHueSpread;

    /// <summary>Hue advance applied to a layer color after each packed shape list.</summary>
    public float LayerHueAdvance;

    /// <summary>Inclusive minimum for the per-frame shape-layer mask draw.</summary>
    public int LayerMaskMinInclusive;

    /// <summary>Exclusive maximum for the per-frame shape-layer mask draw.</summary>
    public int LayerMaskMaxExclusive;
}

/// <summary>The saved-or-default musical-response settings used by Petals in Synced Mode.</summary>
[Serializable]
public sealed class PetalsSyncSettings
{
    /// <summary>Minimum hue rolled for each layer color on the next Synced activation.</summary>
    [Range(0f, 1f)] public float LayerHueMin;

    /// <summary>Maximum hue rolled for each layer color on the next Synced activation.</summary>
    [Range(0f, 1f)] public float LayerHueMax;

    /// <summary>Minimum saturation rolled for each layer color on the next Synced activation.</summary>
    [Range(0f, 1f)] public float LayerSaturationMin;

    /// <summary>Maximum saturation rolled for each layer color on the next Synced activation.</summary>
    [Range(0f, 1f)] public float LayerSaturationMax;

    /// <summary>Minimum starting background hue rolled on the next Synced activation.</summary>
    [Range(0f, 1f)] public float BackgroundHueMin;

    /// <summary>Maximum starting background hue rolled on the next Synced activation.</summary>
    [Range(0f, 1f)] public float BackgroundHueMax;

    /// <summary>Background hue advance per second in Synced Mode.</summary>
    public float BackgroundHueAdvance;

    /// <summary>Brightness at the Waveform trough in Synced Mode.</summary>
    [Min(0f)] public float BeatBrightnessAtTrough;

    /// <summary>Brightness at the Waveform peak in Synced Mode.</summary>
    [Min(0f)] public float BeatBrightnessAtPeak;

    /// <summary>Layer hue shift at the Waveform peak in Synced Mode.</summary>
    public float WaveformHueShiftAtPeak;

    /// <summary>Inclusive minimum for the per-frame shape-layer mask draw.</summary>
    [Min(0)] public int LayerMaskMinInclusive;

    /// <summary>
    /// Exclusive maximum for the per-frame shape-layer mask draw; the current consumer tests only
    /// <c>1 &lt;&lt; 1</c>, so the full numeric domain is not a layer-selector domain.
    /// </summary>
    [Min(0)] public int LayerMaskMaxExclusive;

    /// <summary>Probability that each tile sparkles during an active Drop or Fill.</summary>
    [Range(0f, 1f)] public float SparkleChance;

    /// <summary>Fixed density currently used by the Drop and Fill sparkle phase.</summary>
    [Min(0f)] public float DropDensity;

    /// <summary>Fixed speed currently used by the Drop and Fill sparkle phase.</summary>
    [Min(0f)] public float DropSpeed;

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

        LayerHueMin = source.LayerHueMin;
        LayerHueMax = source.LayerHueMax;
        LayerSaturationMin = source.LayerSaturationMin;
        LayerSaturationMax = source.LayerSaturationMax;
        BackgroundHueMin = source.BackgroundHueMin;
        BackgroundHueMax = source.BackgroundHueMax;
        BackgroundHueAdvance = source.BackgroundHueAdvance;
        BeatBrightnessAtTrough = source.BeatBrightnessAtTrough;
        BeatBrightnessAtPeak = source.BeatBrightnessAtPeak;
        WaveformHueShiftAtPeak = source.WaveformHueShiftAtPeak;
        LayerMaskMinInclusive = source.LayerMaskMinInclusive;
        LayerMaskMaxExclusive = source.LayerMaskMaxExclusive;
        SparkleChance = source.SparkleChance;
        DropDensity = source.DropDensity;
        DropSpeed = source.DropSpeed;
        SparkleValue = source.SparkleValue;
        TileHueSpread = source.TileHueSpread;
        LayerHueAdvance = source.LayerHueAdvance;
    }
}
