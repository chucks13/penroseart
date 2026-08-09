using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Flashes randomly selected packed Penrose shape lists.
/// </summary>
[EffectSyncSettings(typeof(TileShapesSyncSettingsAsset))]
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

    /// <summary>Authored exclusive upper bound covering all nine packed-shape selector arms, zero through eight.</summary>
    private const int StandaloneShapeSelectorMaxExclusive = 9;

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

    /// <summary>Authored exclusive upper bound covering all nine packed-shape selector arms, zero through eight, in Synced Mode.</summary>
    private const int SyncShapeSelectorMaxExclusive = 9;

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
    public static TileShapesStandaloneSettings StandaloneSettings => new TileShapesStandaloneSettings
    {
        RandomColorThreshold = StandaloneRandomColorThreshold,
        FixedHue = new FloatRange(StandaloneFixedHueMin, StandaloneFixedHueMax),
        ShapeSelectorMin = StandaloneShapeSelectorMin,
        ShapeSelectorMaxExclusive = StandaloneShapeSelectorMaxExclusive,
        RandomColorBrightness = StandaloneRandomColorBrightness,
        FixedHueShift = StandaloneFixedHueShift,
        FlashCountDivisor = StandaloneFlashCountDivisor,
        RandomHue = new FloatRange(StandaloneRandomHueMin, StandaloneRandomHueMax),
    };

    /// <summary>Resolves a fresh copy of TileShapes' file-local Sync Defaults.</summary>
    public static TileShapesSyncSettings SyncDefaults => new TileShapesSyncSettings
    {
        RandomColorThreshold = SyncRandomColorThreshold,
        FixedHueMin = SyncFixedHueMin,
        FixedHueMax = SyncFixedHueMax,
        ShapeSelectorMin = SyncShapeSelectorMin,
        ShapeSelectorMaxExclusive = SyncShapeSelectorMaxExclusive,
        RandomColorBrightnessAtTrough = SyncRandomColorBrightnessAtTrough,
        RandomColorBrightnessAtPeak = SyncRandomColorBrightnessAtPeak,
        FixedHueShiftAtTrough = SyncFixedHueShiftAtTrough,
        FixedHueShiftAtPeak = SyncFixedHueShiftAtPeak,
        FlashCountDivisor = SyncFlashCountDivisor,
        RandomHueMin = SyncRandomHueMin,
        RandomHueMax = SyncRandomHueMax,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private TileShapesStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private TileShapesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Whether this activation rolls a fresh hue for every flash.</summary>
    private bool randomColor;

    /// <summary>The fixed hue rolled for this activation when random-color mode is off.</summary>
    private float hue;

    /// <summary>The packed Penrose shape list rolled for this activation.</summary>
    private int[] shape;

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
        standaloneSettings = StandaloneSettings;
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
            float fixedHueMin = beatManager.IsSynced
                ? SyncSettings.FixedHueMin
                : standaloneSettings.FixedHue.Min;
            float fixedHueMax = beatManager.IsSynced
                ? SyncSettings.FixedHueMax
                : standaloneSettings.FixedHue.Max;
            hue = Mathf.Lerp(fixedHueMin, fixedHueMax, Random.value);
        }

        int shapeSelectorMin = beatManager.IsSynced
            ? SyncSettings.ShapeSelectorMin
            : standaloneSettings.ShapeSelectorMin;
        int shapeSelectorMaxExclusive = beatManager.IsSynced
            ? SyncSettings.ShapeSelectorMaxExclusive
            : standaloneSettings.ShapeSelectorMaxExclusive;
        switch (Random.Range(shapeSelectorMin, shapeSelectorMaxExclusive))
        {
            case 0:
                shape = penrose.Layout.shapes.lines0;
                break;
            case 1:
                shape = penrose.Layout.shapes.lines1;
                break;
            case 2:
                shape = penrose.Layout.shapes.lines2;
                break;
            case 3:
                shape = penrose.Layout.shapes.lines3;
                break;
            case 4:
                shape = penrose.Layout.shapes.lines4;
                break;
            case 5:
                shape = penrose.Layout.shapes.loops;
                break;
            case 6:
                shape = penrose.Layout.shapes.lotusballs;
                break;
            case 7:
                shape = penrose.Layout.shapes.starballs;
                break;
            case 8:
                shape = penrose.Layout.shapes.stars;
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
            ? SyncSettings.RandomColorBrightnessAtPeak
            : standaloneSettings.RandomColorBrightness;
        float beatBrightness = waveform.Lerp(
            SyncSettings.RandomColorBrightnessAtTrough,
            randomColorBrightnessAtPeak);
        float fixedHueShiftAtPeak = beatManager.IsSynced
            ? SyncSettings.FixedHueShiftAtPeak
            : standaloneSettings.FixedHueShift;
        float hueShift = waveform.Lerp(
            SyncSettings.FixedHueShiftAtTrough,
            fixedHueShiftAtPeak);
        int flashCountDivisor = beatManager.IsSynced
            ? SyncSettings.FlashCountDivisor
            : standaloneSettings.FlashCountDivisor;
        float randomHueMin = beatManager.IsSynced
            ? SyncSettings.RandomHueMin
            : standaloneSettings.RandomHue.Min;
        float randomHueMax = beatManager.IsSynced
            ? SyncSettings.RandomHueMax
            : standaloneSettings.RandomHue.Max;
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        count = count / flashCountDivisor;
        for (int i = 0; i < count; i++)
        {
            Color color = Color.HSVToRGB(hue+hueShift, 1f, 1f);

            if (randomColor)
                color = Color.HSVToRGB(Mathf.Lerp(randomHueMin, randomHueMax, Random.value), 1f, 1f)* beatBrightness;


            int loop = Random.Range(0, shape[0]);
            int list = shape[1 + loop];
            int start = list + 1;
            int end = start + shape[list];
            for (int j = start; j < end; j++)
            {
                int idx = shape[j];
                if (idx >= 0)
                    buffer[idx] = color;
            }
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce TileShapes' authored no-music look.</summary>
public sealed class TileShapesStandaloneSettings
{
    /// <summary>Threshold above which the activation Roll selects random-color mode.</summary>
    public float RandomColorThreshold;

    /// <summary>Per-activation fixed-hue Roll range used when random-color mode is off.</summary>
    public FloatRange FixedHue;

    /// <summary>Inclusive lower bound of the packed-shape selector Roll.</summary>
    public int ShapeSelectorMin;

    /// <summary>Exclusive upper bound of the packed-shape selector Roll.</summary>
    public int ShapeSelectorMaxExclusive;

    /// <summary>Random-color brightness returned when no live Bar Phase is available.</summary>
    public float RandomColorBrightness;

    /// <summary>Fixed-color hue shift returned when no live Bar Phase is available.</summary>
    public float FixedHueShift;

    /// <summary>Divisor that turns delta-scaled buffer length into the per-frame flash count.</summary>
    public int FlashCountDivisor;

    /// <summary>Per-flash hue Roll range used in random-color mode.</summary>
    public FloatRange RandomHue;
}

/// <summary>Editable music-response values saved as TileShapes' Sync Settings.</summary>
[Serializable]
public sealed class TileShapesSyncSettings
{
    /// <summary>Threshold above which the activation Roll selects random-color mode.</summary>
    [Range(0f, 1f)] public float RandomColorThreshold;

    /// <summary>Minimum fixed hue rolled when random-color mode is off.</summary>
    [Range(0f, 1f)] public float FixedHueMin;

    /// <summary>Maximum fixed hue rolled when random-color mode is off.</summary>
    [Range(0f, 1f)] public float FixedHueMax;

    /// <summary>Inclusive lower bound of the packed-shape selector Roll.</summary>
    [Range(0, 8)] public int ShapeSelectorMin;

    /// <summary>Exclusive upper bound of the packed-shape selector Roll.</summary>
    [Range(1, 9)] public int ShapeSelectorMaxExclusive;

    /// <summary>Random-color brightness at the Waveform trough.</summary>
    [Range(0f, 1f)] public float RandomColorBrightnessAtTrough;

    /// <summary>Random-color brightness at the Waveform peak.</summary>
    [Range(0f, 1f)] public float RandomColorBrightnessAtPeak;

    /// <summary>Fixed-color hue shift at the Waveform trough.</summary>
    [Range(0f, 1f)] public float FixedHueShiftAtTrough;

    /// <summary>Fixed-color hue shift at the Waveform peak.</summary>
    [Range(0f, 1f)] public float FixedHueShiftAtPeak;

    /// <summary>Divisor that turns delta-scaled buffer length into the per-frame flash count.</summary>
    [Min(1)] public int FlashCountDivisor;

    /// <summary>Minimum per-flash hue rolled in random-color mode.</summary>
    [Range(0f, 1f)] public float RandomHueMin;

    /// <summary>Maximum per-flash hue rolled in random-color mode.</summary>
    [Range(0f, 1f)] public float RandomHueMax;

    /// <summary>Copies every TileShapes Sync Setting from another value.</summary>
    public void CopyFrom(TileShapesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        RandomColorThreshold = source.RandomColorThreshold;
        FixedHueMin = source.FixedHueMin;
        FixedHueMax = source.FixedHueMax;
        ShapeSelectorMin = source.ShapeSelectorMin;
        ShapeSelectorMaxExclusive = source.ShapeSelectorMaxExclusive;
        RandomColorBrightnessAtTrough = source.RandomColorBrightnessAtTrough;
        RandomColorBrightnessAtPeak = source.RandomColorBrightnessAtPeak;
        FixedHueShiftAtTrough = source.FixedHueShiftAtTrough;
        FixedHueShiftAtPeak = source.FixedHueShiftAtPeak;
        FlashCountDivisor = source.FlashCountDivisor;
        RandomHueMin = source.RandomHueMin;
        RandomHueMax = source.RandomHueMax;
    }
}
