using System;
using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Animates packed Penrose Circle and Arc groups over a background color.
/// </summary>
[EffectSyncSettings(typeof(CirclesSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(CirclesStandaloneSettingsAsset))]
public class Circles : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored background hue advance per second for the unchanged Standalone look.</summary>
    private const float StandaloneBackgroundHueRate = 0.1f;

    /// <summary>
    /// Standalone palette-family conditioning. The absolute target and floor put every palette in
    /// the same working band; hue-spread-aware equalization, bounded lift, dark-stop repair, duplicate
    /// collapse, and full redistribution keep the Circle and Arc crawl colorful across palette families.
    /// Tune on the wall.
    /// </summary>
    private static PaletteConditioning StandalonePaletteConditioning => new()
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.5f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>
    /// Authored cyclic palette-position step between Tiles within each packed Circle or Arc,
    /// preserving the approved Standalone crawl spacing as palette sampling replaces HSV.
    /// </summary>
    private const float StandaloneCircleTilePositionStep = 0.01f;

    /// <summary>
    /// Authored per-frame palette-position advance for each Circle or Arc's stored position,
    /// preserving the approved frame-driven Standalone crawl.
    /// </summary>
    private const float StandaloneCirclePositionAdvance = 0.01f;

    /// <summary>Authored inclusive lower bound of the Standalone distortion-mode roll; 1 selects Color.</summary>
    private const int StandaloneDistortionModeMinInclusive = 1;

    /// <summary>Authored exclusive upper bound of the Standalone distortion-mode roll; 1 is Color and 2 is Time.</summary>
    private const int StandaloneDistortionModeMaxExclusive = 3;

    // Sync Defaults

    /// <summary>Authored Synced Mode counterpart to the background hue advance used in Standalone Mode.</summary>
    private const float SyncBackgroundHueRate = 0.1f;

    /// <summary>
    /// Sync palette-family conditioning, independently authored so ADR-0013 live tuning in one mode
    /// cannot drift the other. It begins at the same working luminance band, hue-spread-aware
    /// equalization, bounded lift, dark-stop repair, duplicate collapse, and full redistribution as
    /// Standalone. Tune on the wall.
    /// </summary>
    private static PaletteConditioning SyncPaletteConditioning => new()
    {
        TargetLuminance = 0.4f,
        MinimumLuminance = 0.12f,
        LuminanceEqualization = 0.85f,
        HueSpreadReference = 0.5f,
        MaximumLuminanceScale = 4f,
        DarkLuminanceThreshold = 0.03f,
        DuplicateThreshold = 0.08f,
        HueRedistribution = 1f,
    };

    /// <summary>Authored Synced Mode counterpart to the cyclic palette-position step between Tiles.</summary>
    private const float SyncCircleTilePositionStep = 0.01f;

    /// <summary>Authored Synced Mode counterpart to each Circle or Arc's per-frame palette-position advance.</summary>
    private const float SyncCirclePositionAdvance = 0.01f;

    /// <summary>Inclusive lower bound of the complete distortion roll domain: 1 selects Color.</summary>
    private const int SyncDistortionModeMinInclusive = 1;

    /// <summary>Exclusive upper bound of the complete distortion roll domain: 1 is Color and 2 is Time.</summary>
    private const int SyncDistortionModeMaxExclusive = 3;

    /// <summary>Maximum hue response applied when the rolled distortion mode is Color.</summary>
    private const float SyncHueResponseMagnitude = 0.25f;

    /// <summary>Maximum seconds added to sampled effect time when the rolled distortion mode is Time.</summary>
    private const float SyncTimeWarpSeconds = 0.5f;

    /// <summary>Scale that maps the Time distortion's sampled-time offset into hue.</summary>
    private const float SyncTimeWarpHueScale = 0.1f;

    /// <summary>
    /// Current fixed hue step between consecutive Tile indexes in the Drop background. The unfinished
    /// alternative would roll from 0.0004f through 0.003f; keeping the fixed value preserves the approved
    /// look and Random consumption.
    /// </summary>
    private const float SyncDropTileHueStep = 0.001f;

    /// <summary>
    /// Current fixed Drop background hue rate in cycles per second. The unfinished alternative would roll
    /// from 0.1f through 1f; keeping the fixed value preserves the approved look and Random consumption.
    /// </summary>
    private const float SyncDropHueRate = 0.5f;

    /// <summary>Authored value supplied to the Drop background's HSV brightness slot.</summary>
    private const float SyncDropBrightness = 10f;

    /// <summary>Probability that each Circle or Arc becomes black-and-white during an active Fill.</summary>
    private const float SyncFillBlackAndWhiteProbability = 0.125f;

    /// <summary>Circles' crawling motion suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings cannot mutate Circles' authored
    /// Standalone Defaults.
    /// </summary>
    public static CirclesStandaloneSettings StandaloneDefaults => new()
    {
        BackgroundHueRate = StandaloneBackgroundHueRate,
        PaletteConditioning = StandalonePaletteConditioning,
        CircleTilePositionStep = StandaloneCircleTilePositionStep,
        CirclePositionAdvance = StandaloneCirclePositionAdvance,
        DistortionMode = new IntRange(
            StandaloneDistortionModeMinInclusive,
            StandaloneDistortionModeMaxExclusive),
    };

    /// <summary>Resolves a fresh copy of Circles' file-local Sync Defaults.</summary>
    public static CirclesSyncSettings SyncDefaults => new()
    {
        BackgroundHueRate = SyncBackgroundHueRate,
        PaletteConditioning = SyncPaletteConditioning,
        CircleTilePositionStep = SyncCircleTilePositionStep,
        CirclePositionAdvance = SyncCirclePositionAdvance,
        DistortionMode = new IntRange(
            SyncDistortionModeMinInclusive,
            SyncDistortionModeMaxExclusive),
        HueResponseMagnitude = SyncHueResponseMagnitude,
        TimeWarpSeconds = SyncTimeWarpSeconds,
        TimeWarpHueScale = SyncTimeWarpHueScale,
        DropTileHueStep = SyncDropTileHueStep,
        DropHueRate = SyncDropHueRate,
        DropBrightness = SyncDropBrightness,
        FillBlackAndWhiteProbability = SyncFillBlackAndWhiteProbability,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private CirclesStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private CirclesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// Circles' Effect-local conditioned endpoint cache. It follows shared palette revisions and live
    /// conditioning controls while preserving the animated cross-fade without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>Per-group cyclic palette positions advanced across the packed Circle and Arc data.</summary>
    private float[] positions;

    /// <summary>Background hue advanced continuously while this effect runs.</summary>
    private float background;

    /// <summary>Allocation-free access to the packed Circle and Arc groups supplied by the Penrose layout.</summary>
    private LayoutData.ShapeList.Reader shape;

    /// <summary>The active packed-shape name shown in the debug readout.</summary>
    private string shapeName;

    /// <summary>Which beat response this activation applies: 1 is Color and 2 is Time.</summary>
    private int distortionMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string modeName = distortionMode == 1 ? "Color" : "Time Warp";
        return $"shape: {shapeName}\nBeat Mode: {modeName}";
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Circles),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Circles),
            SyncDefaults);
        conditionedPalette.Refresh(APalette, beatManager.IsSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning);
        waveform = waveforms.Random();
        shape = penrose.Layout.shapes.Circles;
        IntRange distortionModeRange = beatManager.IsSynced
            ? SyncSettings.DistortionMode
            : standaloneSettings.DistortionMode;
        distortionMode = Random.Range(
            distortionModeRange.MinInclusive,
            distortionModeRange.MaxExclusive);
        shapeName = "circles";
        positions = new float[shape.GroupCount];
        for (int i = 0; i < shape.GroupCount; i++)
        {
            positions[i] = Random.value;
        }
        background = Random.value;
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
        float backgroundHueRate = isSynced
            ? SyncSettings.BackgroundHueRate
            : standaloneSettings.BackgroundHueRate;
        float circleTilePositionStep = isSynced
            ? SyncSettings.CircleTilePositionStep
            : standaloneSettings.CircleTilePositionStep;
        float circlePositionAdvance = isSynced
            ? SyncSettings.CirclePositionAdvance
            : standaloneSettings.CirclePositionAdvance;
        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);
        float timeWarpHueScale = SyncSettings.TimeWarpHueScale;
        float dropTileHueStep = SyncSettings.DropTileHueStep;
        float dropHueRate = SyncSettings.DropHueRate;
        float dropBrightness = SyncSettings.DropBrightness;
        float fillBlackAndWhiteProbability = SyncSettings.FillBlackAndWhiteProbability;
        float positionShift = 0f;
        float sampleTime = effectTime;
        int groupCount = shape.GroupCount;

        // This effect owns both response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (distortionMode == 1)
            positionShift = SyncSettings.HueResponseMagnitude * rhythm;
        else if (distortionMode == 2)
            sampleTime = effectTime + (SyncSettings.TimeWarpSeconds * rhythm);

        float beatOffset = sampleTime - effectTime;
        positions[Random.Range(0, groupCount)] = Random.value;
        background += effectDelta * backgroundHueRate;
        background %= 1f;
        bool dropActive = beatManager.Drop.Active;
        if (dropActive)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float phase = (i * dropTileHueStep + (effectTime * dropHueRate)) % 1f;
                buffer[i] = Color.HSVToRGB(phase, 1f, dropBrightness);
            }
        }
        else
        {
            Color backgroundColor = Color.HSVToRGB(
                (background + beatOffset * timeWarpHueScale + positionShift) % 1f,
                1f,
                1f);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = backgroundColor;
            }
        }

        bool fillActive = beatManager.Fill.Active;
        for (int i = 0; i < groupCount; i++)
        {
            LayoutData.ShapeList.Group group = shape.GetGroup(i);
            float groupPosition = positions[i];
            bool blackAndWhite = fillActive && Random.value < fillBlackAndWhiteProbability;

            for (int j = 0; j < group.TileCount; j++)
            {
                int idx = group[j];
                float palettePosition =
                    (groupPosition +
                    circleTilePositionStep * group.PackedIndex(j) +
                    beatOffset * timeWarpHueScale +
                    positionShift) % 1f;
                Color paletteColor = conditionedPalette.ReadCyclic(
                    palettePosition,
                    doblend: true);
                if (blackAndWhite)
                {
                    // Fill desaturates the sampled palette color without overwriting the group's
                    // stored position, so its B&W identity keeps the crawl and ends with the Fill.
                    Color.RGBToHSV(paletteColor, out _, out _, out float value);
                    paletteColor = new Color(value, value, value, paletteColor.a);
                }
                buffer[idx] = paletteColor;
            }
            positions[i] = (groupPosition + circlePositionAdvance) % 1f;
        }
    }

}

/// <summary>
/// The serializable value shape shared by Circles' fully populated Standalone Defaults and
/// saved Standalone Settings; Unity may create an empty instance before serialized values apply.
/// </summary>
[Serializable]
public sealed class CirclesStandaloneSettings
{
    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live effect-local palette conditioning for the Standalone foreground.</summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Cyclic palette-position step between Tiles within each packed Circle or Arc.</summary>
    public float CircleTilePositionStep;

    /// <summary>Per-frame palette-position advance for each packed Circle or Arc's stored position.</summary>
    public float CirclePositionAdvance;

    /// <summary>Per-activation range selecting Color or Time distortion.</summary>
    public IntRange DistortionMode;

    /// <summary>Copies every Circles Standalone Setting, including distortion-mode Rails.</summary>
    public void CopyFrom(CirclesStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        PaletteConditioning = source.PaletteConditioning;
        CircleTilePositionStep = source.CircleTilePositionStep;
        CirclePositionAdvance = source.CirclePositionAdvance;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
    }
}

/// <summary>The saved-or-default musical-response settings used by Circles in Synced Mode.</summary>
[Serializable]
public sealed class CirclesSyncSettings
{
    /// <summary>Live Synced Mode background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live effect-local palette conditioning for the Synced foreground.</summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Live Synced Mode cyclic palette-position step between Tiles within each packed Circle or Arc.</summary>
    public float CircleTilePositionStep;

    /// <summary>Live Synced Mode per-frame palette-position advance for each packed Circle or Arc.</summary>
    public float CirclePositionAdvance;

    /// <summary>Per-activation range selecting Color or Time distortion.</summary>
    public IntRange DistortionMode;

    /// <summary>Maximum hue response applied by Color distortion.</summary>
    [Min(0f)] public float HueResponseMagnitude;

    /// <summary>Maximum seconds added to sampled effect time by Time distortion.</summary>
    [Min(0f)] public float TimeWarpSeconds;

    /// <summary>Scale from the Time distortion's sampled-time offset into hue.</summary>
    [Min(0f)] public float TimeWarpHueScale;

    /// <summary>Hue step between consecutive Tile indexes in the active Drop background.</summary>
    [Min(0f)] public float DropTileHueStep;

    /// <summary>Drop background hue cycles advanced per second.</summary>
    [Min(0f)] public float DropHueRate;

    /// <summary>Value supplied to the Drop background's HSV brightness slot.</summary>
    [Min(0f)] public float DropBrightness;

    /// <summary>Probability that each packed Circle or Arc becomes black-and-white during an active Fill.</summary>
    [Range(0f, 1f)] public float FillBlackAndWhiteProbability;

    /// <summary>Copies every Circles Sync Setting from another value.</summary>
    public void CopyFrom(CirclesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        PaletteConditioning = source.PaletteConditioning;
        CircleTilePositionStep = source.CircleTilePositionStep;
        CirclePositionAdvance = source.CirclePositionAdvance;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
        HueResponseMagnitude = source.HueResponseMagnitude;
        TimeWarpSeconds = source.TimeWarpSeconds;
        TimeWarpHueScale = source.TimeWarpHueScale;
        DropTileHueStep = source.DropTileHueStep;
        DropHueRate = source.DropHueRate;
        DropBrightness = source.DropBrightness;
        FillBlackAndWhiteProbability = source.FillBlackAndWhiteProbability;
    }
}
