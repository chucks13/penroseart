using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Flashes randomly selected packed Penrose shape lists.
/// </summary>
[EffectSyncSettings(typeof(TileShapesSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(TileShapesStandaloneSettingsAsset))]
public class TileShapes : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored random-color Roll threshold for the unchanged Standalone look; values above it select random-color mode.</summary>
    private const float StandaloneRandomColorThreshold = 0.5f;

    /// <summary>Authored minimum fixed hue rolled for the unchanged Standalone look.</summary>
    private const float StandaloneFixedHueMin = 0f;

    /// <summary>Authored maximum fixed hue rolled for the unchanged Standalone look.</summary>
    private const float StandaloneFixedHueMax = 1f;

    /// <summary>Authored inclusive lower bound of the packed-shape selector Roll for the unchanged Standalone look.</summary>
    private const int StandaloneShapeSelectorMin = 0;

    /// <summary>Authored exclusive upper bound covering all eight packed-shape selector arms, zero through seven.</summary>
    private const int StandaloneShapeSelectorMaxExclusive = 8;

    /// <summary>Authored random-color brightness returned without a live Bar Phase, preserving the unchanged Standalone look.</summary>
    private const float StandaloneRandomColorBrightness = 1f;

    /// <summary>Authored fixed-color hue shift returned without a live Bar Phase, preserving the unchanged Standalone look.</summary>
    private const float StandaloneFixedHueShift = 0.25f;

    /// <summary>Authored divisor converting delta-scaled buffer length into the unchanged Standalone flash count.</summary>
    private const int StandaloneFlashCountDivisor = 5;

    /// <summary>Authored minimum per-flash random hue for the unchanged Standalone look.</summary>
    private const float StandaloneRandomHueMin = 0f;

    /// <summary>Authored maximum per-flash random hue for the unchanged Standalone look.</summary>
    private const float StandaloneRandomHueMax = 1f;

    // Sync Defaults

    /// <summary>Authored random-color Roll threshold in Synced Mode; values above it select random-color mode.</summary>
    private const float SyncRandomColorThreshold = 0.5f;

    /// <summary>Authored minimum fixed hue rolled in Synced Mode.</summary>
    private const float SyncFixedHueMin = 0f;

    /// <summary>Authored maximum fixed hue rolled in Synced Mode.</summary>
    private const float SyncFixedHueMax = 1f;

    /// <summary>Authored inclusive lower bound of the packed-shape selector Roll in Synced Mode.</summary>
    private const int SyncShapeSelectorMin = 0;

    /// <summary>Authored exclusive upper bound covering all eight packed-shape selector arms, zero through seven, in Synced Mode.</summary>
    private const int SyncShapeSelectorMaxExclusive = 8;

    /// <summary>Authored random-color brightness at the Waveform trough in Synced Mode.</summary>
    private const float SyncRandomColorBrightnessAtTrough = 0.75f;

    /// <summary>Authored random-color brightness at the Waveform peak in Synced Mode.</summary>
    private const float SyncRandomColorBrightnessAtPeak = 1f;

    /// <summary>Authored fixed-color hue shift at the Waveform trough in Synced Mode.</summary>
    private const float SyncFixedHueShiftAtTrough = 0f;

    /// <summary>Authored fixed-color hue shift at the Waveform peak in Synced Mode.</summary>
    private const float SyncFixedHueShiftAtPeak = 0.25f;

    /// <summary>Authored divisor converting delta-scaled buffer length into the Synced Mode flash count.</summary>
    private const int SyncFlashCountDivisor = 5;

    /// <summary>Authored minimum per-flash random hue in Synced Mode.</summary>
    private const float SyncRandomHueMin = 0f;

    /// <summary>Authored maximum per-flash random hue in Synced Mode.</summary>
    private const float SyncRandomHueMax = 1f;

    /// <summary>TileShapes' snapping shapes accent Fills and suit Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of TileShapes' Standalone Defaults.</summary>
    public static TileShapesStandaloneSettings StandaloneDefaults => new TileShapesStandaloneSettings
    {
        RandomColorThreshold = StandaloneRandomColorThreshold,
        FixedHue = new FloatRange(StandaloneFixedHueMin, StandaloneFixedHueMax),
        ShapeSelector = new IntRange(StandaloneShapeSelectorMin, StandaloneShapeSelectorMaxExclusive),
        RandomColorBrightnessAtPeak = StandaloneRandomColorBrightness,
        FixedHueShiftAtPeak = StandaloneFixedHueShift,
        FlashCountDivisor = StandaloneFlashCountDivisor,
        RandomHue = new FloatRange(StandaloneRandomHueMin, StandaloneRandomHueMax),
    };

    /// <summary>Resolves a fresh copy of TileShapes' file-local Sync Defaults.</summary>
    public static TileShapesSyncSettings SyncDefaults => new TileShapesSyncSettings
    {
        RandomColorThreshold = SyncRandomColorThreshold,
        FixedHue = new FloatRange(SyncFixedHueMin, SyncFixedHueMax),
        ShapeSelector = new IntRange(SyncShapeSelectorMin, SyncShapeSelectorMaxExclusive),
        RandomColorBrightness = new FloatRange(
            SyncRandomColorBrightnessAtTrough,
            SyncRandomColorBrightnessAtPeak),
        FixedHueShift = new FloatRange(SyncFixedHueShiftAtTrough, SyncFixedHueShiftAtPeak),
        FlashCountDivisor = SyncFlashCountDivisor,
        RandomHue = new FloatRange(SyncRandomHueMin, SyncRandomHueMax),
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private TileShapesStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private TileShapesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Whether this activation rolls a fresh hue for every flash.</summary>
    private bool randomColor;

    /// <summary>The fixed hue rolled for this activation when random-color mode is off.</summary>
    private float hue;

    /// <summary>The allocation-free Penrose Shape List reader rolled for this activation.</summary>
    private LayoutData.ShapeList.Reader shape;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => randomColor ? "Color: random" : $"hue: {hue}";

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    // Should be called every time an effect is turned on
    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(TileShapes),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(TileShapes),
            SyncDefaults);
        waveform = waveforms.Random();

        float randomColorThreshold = beatManager.IsSynced
            ? SyncSettings.RandomColorThreshold
            : standaloneSettings.RandomColorThreshold;
        if (Random.value > randomColorThreshold)
        {
            randomColor = true;
        }
        else
        {
            randomColor = false;
            FloatRange fixedHue = beatManager.IsSynced
                ? SyncSettings.FixedHue
                : standaloneSettings.FixedHue;
            hue = Mathf.Lerp(fixedHue.Min, fixedHue.Max, Random.value);
        }

        IntRange shapeSelector = beatManager.IsSynced
            ? SyncSettings.ShapeSelector
            : standaloneSettings.ShapeSelector;
        switch (Random.Range(shapeSelector.MinInclusive, shapeSelector.MaxExclusive))
        {
            case 0:
                shape = penrose.Layout.shapes.Lines0;
                break;
            case 1:
                shape = penrose.Layout.shapes.Lines1;
                break;
            case 2:
                shape = penrose.Layout.shapes.Lines2;
                break;
            case 3:
                shape = penrose.Layout.shapes.Lines3;
                break;
            case 4:
                shape = penrose.Layout.shapes.Rings;
                break;
            case 5:
                shape = penrose.Layout.shapes.Lotusballs;
                break;
            case 6:
                shape = penrose.Layout.shapes.Starballs;
                break;
            case 7:
                shape = penrose.Layout.shapes.Stars;
                break;
        }

        var text = (randomColor) ? "random" : hue.ToString();
        controller.debugText.text = $"Color: {text}";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales randomly selected shape flashes.
        float randomColorBrightnessAtPeak = beatManager.IsSynced
            ? SyncSettings.RandomColorBrightness.Max
            : standaloneSettings.RandomColorBrightnessAtPeak;
        float beatBrightness = waveform.Lerp(
            SyncSettings.RandomColorBrightness.Min,
            randomColorBrightnessAtPeak);
        float fixedHueShiftAtPeak = beatManager.IsSynced
            ? SyncSettings.FixedHueShift.Max
            : standaloneSettings.FixedHueShiftAtPeak;
        float hueShift = waveform.Lerp(
            SyncSettings.FixedHueShift.Min,
            fixedHueShiftAtPeak);
        int flashCountDivisor = beatManager.IsSynced
            ? SyncSettings.FlashCountDivisor
            : standaloneSettings.FlashCountDivisor;
        FloatRange randomHue = beatManager.IsSynced
            ? SyncSettings.RandomHue
            : standaloneSettings.RandomHue;
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        count = count / flashCountDivisor;
        for (int i = 0; i < count; i++)
        {
            Color color = Color.HSVToRGB(hue+hueShift, 1f, 1f);

            if (randomColor)
                color = Color.HSVToRGB(Mathf.Lerp(randomHue.Min, randomHue.Max, Random.value), 1f, 1f)* beatBrightness;


            int groupIndex = Random.Range(0, shape.GroupCount);
            LayoutData.ShapeList.Group group = shape.GetGroup(groupIndex);
            for (int j = 0; j < group.TileCount; j++)
            {
                int idx = group[j];
                if (idx >= 0)
                    buffer[idx] = color;
            }
        }
    }
}

/// <summary>The serializable value shape shared by TileShapes' Standalone Defaults and Settings.</summary>
[Serializable]
public sealed class TileShapesStandaloneSettings
{
    /// <summary>Threshold above which the activation Roll selects random-color mode.</summary>
    public float RandomColorThreshold;

    /// <summary>Per-activation fixed-hue Roll range used when random-color mode is off.</summary>
    public FloatRange FixedHue;

    /// <summary>Roll range selecting one packed Shape List switch arm.</summary>
    public IntRange ShapeSelector;

    /// <summary>Random-color brightness used as the Waveform peak endpoint in Standalone Mode.</summary>
    public float RandomColorBrightnessAtPeak;

    /// <summary>Fixed-color hue shift used as the Waveform peak endpoint in Standalone Mode.</summary>
    public float FixedHueShiftAtPeak;

    /// <summary>Divisor that turns delta-scaled buffer length into the per-frame flash count.</summary>
    [Min(1)] public int FlashCountDivisor;

    /// <summary>Per-flash hue Roll range used in random-color mode.</summary>
    public FloatRange RandomHue;

    /// <summary>Copies every TileShapes Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(TileShapesStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        RandomColorThreshold = source.RandomColorThreshold;
        FixedHue = CopyRange(source.FixedHue);
        ShapeSelector = CopyRange(source.ShapeSelector);
        RandomColorBrightnessAtPeak = source.RandomColorBrightnessAtPeak;
        FixedHueShiftAtPeak = source.FixedHueShiftAtPeak;
        FlashCountDivisor = source.FlashCountDivisor;
        RandomHue = CopyRange(source.RandomHue);
    }

    /// <summary>Creates an asset-owned copy of a floating-point range and its tuning Rails.</summary>
    private static FloatRange CopyRange(FloatRange source) => new FloatRange(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Creates an asset-owned copy of an integer range and its tuning Rails.</summary>
    private static IntRange CopyRange(IntRange source) => new IntRange(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}

/// <summary>Editable music-response values saved as TileShapes' Sync Settings.</summary>
[Serializable]
public sealed class TileShapesSyncSettings
{
    /// <summary>Threshold above which the activation Roll selects random-color mode.</summary>
    [Range(0f, 1f)] public float RandomColorThreshold;

    /// <summary>Per-activation fixed-hue Roll range used when random-color mode is off.</summary>
    public FloatRange FixedHue;

    /// <summary>Roll range selecting one packed Shape List switch arm.</summary>
    public IntRange ShapeSelector;

    /// <summary>Random-color brightness range mapped from Waveform trough to peak.</summary>
    public FloatRange RandomColorBrightness;

    /// <summary>Fixed-color hue-shift range mapped from Waveform trough to peak.</summary>
    public FloatRange FixedHueShift;

    /// <summary>Divisor that turns delta-scaled buffer length into the per-frame flash count.</summary>
    [Min(1)] public int FlashCountDivisor;

    /// <summary>Per-flash hue Roll range used in random-color mode.</summary>
    public FloatRange RandomHue;

    /// <summary>Copies every TileShapes Sync Setting from another value.</summary>
    public void CopyFrom(TileShapesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        RandomColorThreshold = source.RandomColorThreshold;
        FixedHue = CopyRange(source.FixedHue);
        ShapeSelector = CopyRange(source.ShapeSelector);
        RandomColorBrightness = CopyRange(source.RandomColorBrightness);
        FixedHueShift = CopyRange(source.FixedHueShift);
        FlashCountDivisor = source.FlashCountDivisor;
        RandomHue = CopyRange(source.RandomHue);
    }

    /// <summary>Creates an asset-owned copy of a floating-point range and its tuning Rails.</summary>
    private static FloatRange CopyRange(FloatRange source) => new FloatRange(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Creates an asset-owned copy of an integer range and its tuning Rails.</summary>
    private static IntRange CopyRange(IntRange source) => new IntRange(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}
