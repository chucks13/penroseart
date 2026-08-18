using System;
using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Animates packed Penrose Circle and Arc groups over a background color.
/// </summary>
[EffectSyncSettings(typeof(AnimateShapesSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(AnimateShapesStandaloneSettingsAsset))]
public class AnimateShapes : EffectBase
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
    /// Authored palette-position advance per second for each Circle or Arc's stored position.
    /// The 0.6 rate preserves the approved 0.01-per-frame crawl at the 60 fps reference rate.
    /// </summary>
    private const float StandaloneCirclePositionAdvancePerSecond = 0.6f;

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

    /// <summary>
    /// Authored Synced Mode palette-position advance per second. Raised from the frame-rate
    /// conversion's baseline 0.6 (the approved 0.01-per-frame crawl at 60 fps) to 1 at the wall.
    /// </summary>
    private const float SyncCirclePositionAdvancePerSecond = 1f;

    /// <summary>
    /// Authored Low Energy multiplier for the foreground Circle and Arc crawl. The ruled 0.75 Low
    /// and 1.25 High endpoints make Mid's midpoint exactly one, preserving the approved baseline speed.
    /// </summary>
    private const float SyncLowEnergyCrawlSpeedMultiplier = 0.75f;

    /// <summary>
    /// Authored High Energy multiplier for the foreground Circle and Arc crawl. The ruled 0.75 Low
    /// and 1.25 High endpoints make Mid's midpoint exactly one, preserving the approved baseline speed.
    /// </summary>
    private const float SyncHighEnergyCrawlSpeedMultiplier = 1.25f;

    /// <summary>
    /// Authored foreground Drop ribbon window in beats. Sixteen beats gives each landing one finite
    /// response independent of the wire's Drop length. Tune live at the wall.
    /// </summary>
    private const int SyncForegroundDropRibbonWindowBeats = 16;

    /// <summary>
    /// Authored foreground Drop ribbon flow at the landing, in hue-wheel cycles per beat. One cycle
    /// per beat matches the established Angles impact speed. Tune live at the wall.
    /// </summary>
    private const float SyncForegroundDropRibbonFlowCyclesPerBeatAtLanding = 1f;

    /// <summary>
    /// Authored Pool entry name of the one Waveform this effect holds: peaks on counts 2 and 4,
    /// the figure its distortion response rides. A random draw put the response on a different
    /// figure every activation.
    /// </summary>
    private const string SyncWaveformName = "beats 2 and 4";

    /// <summary>Inclusive lower bound of the complete distortion roll domain: 1 selects Color.</summary>
    private const int SyncDistortionModeMinInclusive = 1;

    /// <summary>Exclusive upper bound of the complete distortion roll domain: 1 is Color and 2 is Time.</summary>
    private const int SyncDistortionModeMaxExclusive = 3;

    /// <summary>Maximum hue response applied when the rolled distortion mode is Color.</summary>
    private const float SyncHueResponseMagnitude = 0.25f;

    /// <summary>
    /// Maximum hue response applied when the rolled distortion mode is Time. The 0.05 magnitude
    /// preserves the former 0.5-second sampled-time offset multiplied by its 0.1 hue scale; keeping
    /// their effective product here removes a second tuning knob that could not change the look
    /// independently.
    /// </summary>
    private const float SyncTimeWarpHueResponseMagnitude = 0.05f;

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

    /// <summary>
    /// Fraction of the distance from a Fill gray's sampled Value to full brightness that the gray is
    /// lifted, so the flash reads bright even when the conditioned palette sits in a dark stretch.
    /// </summary>
    private const float SyncFillBrightnessLift = 0.5f;

    /// <summary>
    /// Circle or Arc group reseeds per second, shared by Standalone and Synced Mode. Sixty preserves
    /// the approved one-reseed-per-frame cadence at the 60 fps reference rate, where the per-frame
    /// reseed probability reaches one.
    /// </summary>
    private const float GroupReseedsPerSecond = 60f;

    /// <summary>AnimateShapes' crawling motion suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings cannot mutate AnimateShapes' authored
    /// Standalone Defaults.
    /// </summary>
    public static AnimateShapesStandaloneSettings StandaloneDefaults => new()
    {
        BackgroundHueRate = StandaloneBackgroundHueRate,
        PaletteConditioning = StandalonePaletteConditioning,
        CircleTilePositionStep = StandaloneCircleTilePositionStep,
        CirclePositionAdvancePerSecond = StandaloneCirclePositionAdvancePerSecond,
        DistortionMode = new IntRange(
            StandaloneDistortionModeMinInclusive,
            StandaloneDistortionModeMaxExclusive),
    };

    /// <summary>Resolves a fresh copy of AnimateShapes' file-local Sync Defaults.</summary>
    public static AnimateShapesSyncSettings SyncDefaults => new()
    {
        BackgroundHueRate = SyncBackgroundHueRate,
        PaletteConditioning = SyncPaletteConditioning,
        CircleTilePositionStep = SyncCircleTilePositionStep,
        CirclePositionAdvancePerSecond = SyncCirclePositionAdvancePerSecond,
        EnergyCrawlSpeedMultiplier = new FloatRange(
            SyncLowEnergyCrawlSpeedMultiplier,
            SyncHighEnergyCrawlSpeedMultiplier),
        ForegroundDropRibbonWindowBeats = SyncForegroundDropRibbonWindowBeats,
        ForegroundDropRibbonFlowCyclesPerBeatAtLanding =
            SyncForegroundDropRibbonFlowCyclesPerBeatAtLanding,
        WaveformName = SyncWaveformName,
        DistortionMode = new IntRange(
            SyncDistortionModeMinInclusive,
            SyncDistortionModeMaxExclusive),
        HueResponseMagnitude = SyncHueResponseMagnitude,
        TimeWarpHueResponseMagnitude = SyncTimeWarpHueResponseMagnitude,
        DropTileHueStep = SyncDropTileHueStep,
        DropHueRate = SyncDropHueRate,
        DropBrightness = SyncDropBrightness,
        FillBlackAndWhiteProbability = SyncFillBlackAndWhiteProbability,
        FillBrightnessLift = SyncFillBrightnessLift,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private AnimateShapesStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private AnimateShapesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// AnimateShapes' Effect-local conditioned endpoint cache. It follows shared palette revisions and live
    /// conditioning controls while preserving the animated cross-fade without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>Per-group cyclic palette positions advanced across the packed Circle and Arc data.</summary>
    private float[] positions;

    /// <summary>
    /// Bounded hue-wheel phase shared by every foreground Circle and Arc during the Drop response.
    /// It advances only while the response is visible, holds after the window, and resets at activation.
    /// </summary>
    private float foregroundDropRibbonFlowPhase;

    /// <summary>
    /// The frame's single <see cref="InSpan.Decay(int)"/> read, retained after <see cref="Draw"/>
    /// uses it for ribbon flow and rainbow mixing so <see cref="DebugText"/> can expose it.
    /// </summary>
    private float foregroundDropRibbonEnvelope;

    /// <summary>Background hue advanced continuously while this effect runs.</summary>
    private float background;

    /// <summary>Allocation-free access to the packed Circle and Arc groups supplied by the Penrose layout.</summary>
    private LayoutData.ShapeList.Reader shape;

    /// <summary>The active packed-shape name shown in the debug readout.</summary>
    private string shapeName;

    /// <summary>Which beat response this activation applies: 1 is Color and 2 is Time.</summary>
    private int distortionMode;

    /// <summary>
    /// Pool entry name of the currently held Waveform, so a live Play Mode edit of the
    /// WaveformName Sync Setting re-acquires while an unchanged setting leaves the held value —
    /// and any owner's replacement of it — alone.
    /// </summary>
    private string acquiredWaveformName;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string modeName = distortionMode switch
        {
            1 => "Color",
            2 => "Time Warp",
            _ => $"None ({distortionMode})",
        };
        return $"shape: {shapeName}\nBeat Mode: {modeName}" +
            (foregroundDropRibbonEnvelope > 0f
                ? $"\nDROP {foregroundDropRibbonEnvelope:0.00}  {SyncSettings.ForegroundDropRibbonFlowCyclesPerBeatAtLanding:0.00} cpb"
                : "");
    }

    /// <summary>
    /// Resolves settings, acquires the selected Waveform, and initializes per-activation random
    /// and Drop-ribbon state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(AnimateShapes),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(AnimateShapes),
            SyncDefaults);
        conditionedPalette.Refresh(APalette, beatManager.IsSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning);
        acquiredWaveformName = SyncSettings.WaveformName;
        waveform = waveforms.Named(acquiredWaveformName);
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
        foregroundDropRibbonFlowPhase = 0f;
        foregroundDropRibbonEnvelope = 0f;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    /// <remarks>
    /// The foreground Drop ribbons read their Stock Envelope and measured beat interval here every
    /// frame, so every ribbon Sync Setting remains live in Play Mode. The authored window is
    /// independent of the wire's Drop length, and Energy scales only the ordinary crawl, never the
    /// ribbon flow phase.
    /// </remarks>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        float backgroundHueRate = isSynced
            ? SyncSettings.BackgroundHueRate
            : standaloneSettings.BackgroundHueRate;
        float circleTilePositionStep = isSynced
            ? SyncSettings.CircleTilePositionStep
            : standaloneSettings.CircleTilePositionStep;
        float circlePositionAdvancePerSecond = isSynced
            ? SyncSettings.CirclePositionAdvancePerSecond
            : standaloneSettings.CirclePositionAdvancePerSecond;
        float energyCrawlSpeedMultiplier = GetEnergyCrawlSpeedMultiplier(
            beatManager.Energy.Level);
        float foregroundPositionAdvancePerSecond =
            circlePositionAdvancePerSecond * energyCrawlSpeedMultiplier;
        foregroundDropRibbonEnvelope = beatManager.Drop.In.Decay(
            SyncSettings.ForegroundDropRibbonWindowBeats);
        UpdateForegroundDropRibbonFlowPhase(foregroundDropRibbonEnvelope);
        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);
        float dropTileHueStep = SyncSettings.DropTileHueStep;
        float dropHueRate = SyncSettings.DropHueRate;
        float dropBrightness = SyncSettings.DropBrightness;
        float fillBlackAndWhiteProbability = SyncSettings.FillBlackAndWhiteProbability;
        float fillBrightnessLift = SyncSettings.FillBrightnessLift;
        float positionShift = 0f;
        int groupCount = shape.GroupCount;

        if (SyncSettings.WaveformName != acquiredWaveformName)
        {
            acquiredWaveformName = SyncSettings.WaveformName;
            waveform = waveforms.Named(acquiredWaveformName);
        }

        // This effect owns both response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (distortionMode == 1)
            positionShift = SyncSettings.HueResponseMagnitude * rhythm;
        else if (distortionMode == 2)
            positionShift = SyncSettings.TimeWarpHueResponseMagnitude * rhythm;

        if (Random.value < GroupReseedsPerSecond * effectDelta)
        {
            positions[Random.Range(0, groupCount)] = Random.value;
        }
        background += effectDelta * backgroundHueRate;
        background = Mathf.Repeat(background, 1f);
        bool dropActive = beatManager.Drop.Active;
        if (dropActive)
        {
            float dropHueOffset = effectTime * dropHueRate;
            for (int i = 0; i < buffer.Length; i++)
            {
                float phase = Mathf.Repeat(i * dropTileHueStep + dropHueOffset, 1f);
                buffer[i] = Color.HSVToRGB(phase, 1f, dropBrightness);
            }
        }
        else
        {
            Color backgroundColor = Color.HSVToRGB(
                Mathf.Repeat(background + positionShift, 1f),
                1f,
                1f);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = backgroundColor;
            }
        }

        bool fillActive = beatManager.Fill.Active;
        bool ribbonActive = foregroundDropRibbonEnvelope > 0f;
        float foregroundPositionAdvance = foregroundPositionAdvancePerSecond * effectDelta;
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
                    positionShift) % 1f;
                Color paletteColor = conditionedPalette.ReadCyclic(
                    palettePosition,
                    doblend: true);
                if (blackAndWhite)
                {
                    // Fill desaturates the sampled palette color without overwriting the group's
                    // stored position, so its B&W identity keeps the crawl and ends with the Fill.
                    // The gray's Value is lifted toward full brightness so the flash reads bright
                    // even when the palette sample is dark.
                    Color.RGBToHSV(paletteColor, out _, out _, out float value);
                    value = Mathf.Lerp(value, 1f, fillBrightnessLift);
                    paletteColor = new Color(value, value, value, paletteColor.a);
                }
                if (ribbonActive)
                {
                    // The Drop response paints the full hue wheel once along each Circle and Arc
                    // and flows it at the authored cycles per beat. The shared palette plays no
                    // part in the ribbon color, so no palette pick can mute the landing; the
                    // envelope dissolves the rainbow back into the palette crawl as the window
                    // closes. The Drop background is untouched by this response.
                    float ribbonHue = Mathf.Repeat(
                        shape.GetPosition(idx) + foregroundDropRibbonFlowPhase,
                        1f);
                    paletteColor = Color.Lerp(
                        paletteColor,
                        Color.HSVToRGB(ribbonHue, 1f, 1f),
                        foregroundDropRibbonEnvelope);
                }
                buffer[idx] = paletteColor;
            }
            positions[i] = (groupPosition + foregroundPositionAdvance) % 1f;
        }
    }

    /// <summary>
    /// Advances the shared foreground Drop ribbon current at the live authored cycles-per-beat rate.
    /// </summary>
    /// <param name="envelope">Current foreground Drop ribbon response envelope.</param>
    private void UpdateForegroundDropRibbonFlowPhase(float envelope)
    {
        if (envelope <= 0f)
        {
            return;
        }

        float cyclesPerSecond =
            SyncSettings.ForegroundDropRibbonFlowCyclesPerBeatAtLanding *
            1000f /
            beatManager.Timing.BeatAverageMilliseconds.Value;
        foregroundDropRibbonFlowPhase = Mathf.Repeat(
            foregroundDropRibbonFlowPhase + (cyclesPerSecond * envelope * effectDelta),
            1f);
    }

    /// <summary>Maps the current Energy level to the authored foreground crawl-speed multiplier.</summary>
    /// <param name="energy">Current track-relative Energy, or null while that classification rests.</param>
    /// <returns>
    /// The Low endpoint, the midpoint for Mid or unavailable Energy, or the High endpoint; exactly one
    /// in Standalone Mode so its authored foreground crawl remains unchanged.
    /// </returns>
    private float GetEnergyCrawlSpeedMultiplier(Energy? energy)
    {
        if (!beatManager.IsSynced)
        {
            return 1f;
        }

        return (energy ?? Energy.Mid) switch
        {
            Energy.Low => SyncSettings.EnergyCrawlSpeedMultiplier.Min,
            Energy.Mid => Mathf.Lerp(
                SyncSettings.EnergyCrawlSpeedMultiplier.Min,
                SyncSettings.EnergyCrawlSpeedMultiplier.Max,
                0.5f),
            Energy.High => SyncSettings.EnergyCrawlSpeedMultiplier.Max,
            _ => throw new ArgumentOutOfRangeException(
                nameof(energy),
                energy,
                "Unsupported Energy level."),
        };
    }

}

/// <summary>
/// The serializable value shape shared by AnimateShapes' fully populated Standalone Defaults and
/// saved Standalone Settings; Unity may create an empty instance before serialized values apply.
/// </summary>
[Serializable]
public sealed class AnimateShapesStandaloneSettings
{
    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live effect-local palette conditioning for the Standalone foreground.</summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Cyclic palette-position step between Tiles within each packed Circle or Arc.</summary>
    public float CircleTilePositionStep;

    /// <summary>Palette-position advance per second for each packed Circle or Arc's stored position.</summary>
    public float CirclePositionAdvancePerSecond;

    /// <summary>Per-activation range selecting Color or Time distortion.</summary>
    public IntRange DistortionMode;

    /// <summary>Copies every AnimateShapes Standalone Setting, including distortion-mode Rails.</summary>
    public void CopyFrom(AnimateShapesStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        PaletteConditioning = source.PaletteConditioning;
        CircleTilePositionStep = source.CircleTilePositionStep;
        CirclePositionAdvancePerSecond = source.CirclePositionAdvancePerSecond;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
    }
}

/// <summary>The saved-or-default musical-response settings used by AnimateShapes in Synced Mode.</summary>
[Serializable]
public sealed class AnimateShapesSyncSettings
{
    /// <summary>Live Synced Mode background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live effect-local palette conditioning for the Synced foreground.</summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Live Synced Mode cyclic palette-position step between Tiles within each packed Circle or Arc.</summary>
    public float CircleTilePositionStep;

    /// <summary>Live Synced Mode palette-position advance per second for each packed Circle or Arc.</summary>
    public float CirclePositionAdvancePerSecond;

    /// <summary>
    /// Low-to-High Energy range for foreground crawl speed. The ruled endpoints are 0.75 and 1.25;
    /// Mid uses their midpoint of exactly one to preserve the approved baseline speed.
    /// </summary>
    public FloatRange EnergyCrawlSpeedMultiplier;

    /// <summary>
    /// Live foreground Drop ribbon window in beats. Sixteen beats gives each landing one finite
    /// response even when the wire's Drop Phrase continues longer.
    /// </summary>
    public int ForegroundDropRibbonWindowBeats;

    /// <summary>
    /// Live foreground Drop ribbon flow at the landing, in hue-wheel cycles per beat. One cycle per
    /// beat is the authored impact speed; Energy does not rescale it. Each shape carries the full
    /// rainbow once along its path; the shared palette plays no part in the ribbon color.
    /// </summary>
    public float ForegroundDropRibbonFlowCyclesPerBeatAtLanding;

    /// <summary>
    /// Live Pool entry name of the one Waveform this effect holds — the rhythm its distortion
    /// response rides. A name missing from the Pool is a configuration error and fails visibly.
    /// </summary>
    [WaveformName]
    public string WaveformName;

    /// <summary>Per-activation range selecting Color or Time distortion.</summary>
    public IntRange DistortionMode;

    /// <summary>Maximum hue response applied by Color distortion.</summary>
    public float HueResponseMagnitude;

    /// <summary>
    /// Maximum hue response applied by Time distortion. This single live magnitude replaces the
    /// former sampled-time seconds and hue-scale controls whose product was the only rendered value.
    /// </summary>
    public float TimeWarpHueResponseMagnitude;

    /// <summary>Hue step between consecutive Tile indexes in the active Drop background.</summary>
    public float DropTileHueStep;

    /// <summary>Drop background hue cycles advanced per second.</summary>
    public float DropHueRate;

    /// <summary>Value supplied to the Drop background's HSV brightness slot.</summary>
    public float DropBrightness;

    /// <summary>Probability that each packed Circle or Arc becomes black-and-white during an active Fill.</summary>
    public float FillBlackAndWhiteProbability;

    /// <summary>Fraction of the distance from a Fill gray's sampled Value to full brightness that it is lifted.</summary>
    public float FillBrightnessLift;

    /// <summary>Copies every AnimateShapes Sync Setting from another value.</summary>
    public void CopyFrom(AnimateShapesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        PaletteConditioning = source.PaletteConditioning;
        CircleTilePositionStep = source.CircleTilePositionStep;
        CirclePositionAdvancePerSecond = source.CirclePositionAdvancePerSecond;
        EnergyCrawlSpeedMultiplier = new FloatRange(
            source.EnergyCrawlSpeedMultiplier.Min,
            source.EnergyCrawlSpeedMultiplier.Max,
            source.EnergyCrawlSpeedMultiplier.LowRail,
            source.EnergyCrawlSpeedMultiplier.HighRail);
        ForegroundDropRibbonWindowBeats = source.ForegroundDropRibbonWindowBeats;
        ForegroundDropRibbonFlowCyclesPerBeatAtLanding =
            source.ForegroundDropRibbonFlowCyclesPerBeatAtLanding;
        WaveformName = source.WaveformName;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
        HueResponseMagnitude = source.HueResponseMagnitude;
        TimeWarpHueResponseMagnitude = source.TimeWarpHueResponseMagnitude;
        DropTileHueStep = source.DropTileHueStep;
        DropHueRate = source.DropHueRate;
        DropBrightness = source.DropBrightness;
        FillBlackAndWhiteProbability = source.FillBlackAndWhiteProbability;
        FillBrightnessLift = source.FillBrightnessLift;
    }
}
