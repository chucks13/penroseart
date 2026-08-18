using System;
using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Animates exact packed Penrose Circle and Arc Shape List membership as foreground over its
/// complementary background.
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
    private static PaletteConditioning StandaloneForegroundPaletteConditioning => new()
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
    private const float StandaloneForegroundTilePositionStep = 0.01f;

    /// <summary>
    /// Authored palette-position advance per second for each Circle or Arc's stored position.
    /// The 0.6 rate preserves the approved 0.01-per-frame crawl at the 60 fps reference rate.
    /// </summary>
    private const float StandaloneForegroundPositionAdvancePerSecond = 0.6f;

    // Sync Defaults

    /// <summary>Authored Synced Mode counterpart to the background hue advance used in Standalone Mode.</summary>
    private const float SyncBackgroundHueRate = 0.1f;

    /// <summary>
    /// Sync palette-family conditioning, independently authored so ADR-0013 live tuning in one mode
    /// cannot drift the other. It begins at the same working luminance band, hue-spread-aware
    /// equalization, bounded lift, dark-stop repair, duplicate collapse, and full redistribution as
    /// Standalone. Tune on the wall.
    /// </summary>
    private static PaletteConditioning SyncForegroundPaletteConditioning => new()
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
    private const float SyncForegroundTilePositionStep = 0.01f;

    /// <summary>
    /// Authored Synced Mode palette-position advance per second. Raised from the frame-rate
    /// conversion's baseline 0.6 (the approved 0.01-per-frame crawl at 60 fps) to 1 at the wall.
    /// </summary>
    private const float SyncForegroundPositionAdvancePerSecond = 1f;

    /// <summary>
    /// Authored Low Energy multiplier for the foreground Circle and Arc crawl. The ruled 0.75 Low
    /// and 1.25 High endpoints make Mid's midpoint exactly one, preserving the approved baseline speed.
    /// </summary>
    private const float SyncForegroundLowEnergyCrawlSpeedMultiplier = 0.75f;

    /// <summary>
    /// Authored High Energy multiplier for the foreground Circle and Arc crawl. The ruled 0.75 Low
    /// and 1.25 High endpoints make Mid's midpoint exactly one, preserving the approved baseline speed.
    /// </summary>
    private const float SyncForegroundHighEnergyCrawlSpeedMultiplier = 1.25f;

    /// <summary>
    /// Authored foreground Drop ribbon window in beats. Sixteen beats gives each landing one finite
    /// response independent of the wire's Drop length. Tune live at the wall.
    /// </summary>
    private const int SyncForegroundDropRibbonWindowBeats = 16;

    /// <summary>
    /// Authored foreground Drop ribbon flow at the landing, in hue-wheel cycles per beat. The
    /// wall-approved 1.15 is a slight lift from the established one-cycle Angles impact speed.
    /// </summary>
    private const float SyncForegroundDropRibbonFlowCyclesPerBeatAtLanding = 1.15f;

    /// <summary>
    /// Authored Value supplied to the ribbon color's HSV brightness slot. The wall-approved value
    /// is one, the plain full hue wheel; higher values overdrive the ribbons against the Drop background.
    /// </summary>
    private const float SyncForegroundDropRibbonBrightness = 1f;

    /// <summary>
    /// Authored Pool entry name of the one Waveform this effect holds: peaks on counts 2 and 4,
    /// the figure its background brightness response rides. Holding this named figure prevents the
    /// former per-activation Random draw from moving the response to a different figure.
    /// </summary>
    private const string SyncBackgroundWaveformName = "beats 2 and 4";

    /// <summary>
    /// Authored background brightness at a held Waveform trough. The wall-approved half-brightness
    /// floor keeps the response visible without extinguishing the background; peaks reach full brightness.
    /// </summary>
    private const float SyncBackgroundWaveformBrightnessFloor = 0.5f;

    /// <summary>
    /// Current fixed hue step between consecutive Tile indexes in the Drop background. The unfinished
    /// alternative would roll from 0.0004f through 0.003f; keeping the fixed value preserves the approved
    /// look and Random consumption.
    /// </summary>
    private const float SyncBackgroundDropTileHueStep = 0.001f;

    /// <summary>
    /// Current fixed Drop background hue rate in cycles per second. The unfinished alternative would roll
    /// from 0.1f through 1f; keeping the fixed value preserves the approved look and Random consumption.
    /// </summary>
    private const float SyncBackgroundDropHueRate = 0.5f;

    /// <summary>
    /// Authored Value supplied to the Drop background's HSV-to-RGB conversion. One preserves the
    /// smooth full-brightness hue gradient instead of clipping it into a few flat RGB colors.
    /// </summary>
    private const float SyncBackgroundDropValue = 1f;

    /// <summary>Probability that each Circle or Arc becomes black-and-white during an active Fill.</summary>
    private const float SyncForegroundFillBlackAndWhiteProbability = 0.125f;

    /// <summary>
    /// Fraction of the distance from a Fill gray's sampled Value to full brightness that the gray is
    /// lifted, so the flash reads bright even when the conditioned palette sits in a dark stretch.
    /// </summary>
    private const float SyncForegroundFillBrightnessLift = 0.8f;

    /// <summary>
    /// Circle or Arc group reseeds per second, shared by Standalone and Synced Mode. Sixty preserves
    /// the approved one-reseed-per-frame cadence at the 60 fps reference rate, where the per-frame
    /// reseed probability reaches one.
    /// </summary>
    private const float ForegroundGroupReseedsPerSecond = 60f;

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
        ForegroundPaletteConditioning = StandaloneForegroundPaletteConditioning,
        ForegroundTilePositionStep = StandaloneForegroundTilePositionStep,
        ForegroundPositionAdvancePerSecond = StandaloneForegroundPositionAdvancePerSecond,
    };

    /// <summary>Resolves a fresh copy of AnimateShapes' file-local Sync Defaults.</summary>
    public static AnimateShapesSyncSettings SyncDefaults => new()
    {
        BackgroundHueRate = SyncBackgroundHueRate,
        ForegroundPaletteConditioning = SyncForegroundPaletteConditioning,
        ForegroundTilePositionStep = SyncForegroundTilePositionStep,
        ForegroundPositionAdvancePerSecond = SyncForegroundPositionAdvancePerSecond,
        ForegroundEnergyCrawlSpeedMultiplier = new FloatRange(
            SyncForegroundLowEnergyCrawlSpeedMultiplier,
            SyncForegroundHighEnergyCrawlSpeedMultiplier),
        ForegroundDropRibbonWindowBeats = SyncForegroundDropRibbonWindowBeats,
        ForegroundDropRibbonFlowCyclesPerBeatAtLanding =
            SyncForegroundDropRibbonFlowCyclesPerBeatAtLanding,
        ForegroundDropRibbonBrightness = SyncForegroundDropRibbonBrightness,
        BackgroundWaveformName = SyncBackgroundWaveformName,
        BackgroundWaveformBrightnessFloor = SyncBackgroundWaveformBrightnessFloor,
        BackgroundDropTileHueStep = SyncBackgroundDropTileHueStep,
        BackgroundDropHueRate = SyncBackgroundDropHueRate,
        BackgroundDropValue = SyncBackgroundDropValue,
        ForegroundFillBlackAndWhiteProbability = SyncForegroundFillBlackAndWhiteProbability,
        ForegroundFillBrightnessLift = SyncForegroundFillBrightnessLift,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private AnimateShapesStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private AnimateShapesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// AnimateShapes' Effect-local conditioned endpoint cache. It follows shared palette revisions and live
    /// conditioning controls while preserving the animated cross-fade without steady-frame allocation.
    /// </summary>
    private readonly ConditionedPaletteCache foregroundPalette = new();

    /// <summary>Per-group foreground palette positions advanced across the packed Circle and Arc data.</summary>
    private float[] foregroundPositions;

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

    /// <summary>Hue of the complementary background advanced continuously while this effect runs.</summary>
    private float backgroundHue;

    /// <summary>
    /// Allocation-free access to the exact foreground membership: every packed Circle and Arc Tile.
    /// Tiles absent from these groups form the complementary background.
    /// </summary>
    private LayoutData.ShapeList.Reader foregroundShapes;

    /// <summary>The active foreground Shape List name shown in the debug readout.</summary>
    private string foregroundShapeName;

    /// <summary>
    /// Pool entry name of the currently held background Waveform, so a live Play Mode edit of the
    /// BackgroundWaveformName Sync Setting re-acquires while an unchanged setting leaves the held
    /// value — and any owner's replacement of it — alone.
    /// </summary>
    private string acquiredBackgroundWaveformName;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"shape: {foregroundShapeName}\nBackground Waveform: {acquiredBackgroundWaveformName}" +
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
        foregroundPalette.Refresh(APalette, beatManager.IsSynced
            ? SyncSettings.ForegroundPaletteConditioning
            : standaloneSettings.ForegroundPaletteConditioning);
        string requestedBackgroundWaveformName = SyncSettings.BackgroundWaveformName;
        waveform = waveforms.Named(requestedBackgroundWaveformName);
        acquiredBackgroundWaveformName = requestedBackgroundWaveformName;
        foregroundShapes = penrose.Layout.shapes.Circles;
        foregroundShapeName = "circles";
        foregroundPositions = new float[foregroundShapes.GroupCount];
        for (int i = 0; i < foregroundShapes.GroupCount; i++)
        {
            foregroundPositions[i] = Random.value;
        }
        backgroundHue = Random.value;
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
    /// The background pass establishes the complementary Tile colors; the following foreground pass
    /// overwrites exact Circle/Arc membership. Background settings and the background Waveform response
    /// cannot survive the foreground overwrite, while foreground settings never enter the background
    /// calculation. The foreground Drop ribbons read their Stock Envelope and measured beat interval here
    /// every frame, so every ribbon Sync Setting remains live in Play Mode. The authored window is
    /// independent of the wire's Drop length, and Energy scales only the ordinary crawl, never the
    /// ribbon flow phase.
    /// </remarks>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        DrawBackground(isSynced);
        DrawForeground(isSynced);
    }

    /// <summary>
    /// Draws the complementary background base pass before exact Circle/Arc membership overwrites it.
    /// </summary>
    /// <param name="isSynced">Whether the Data Surface selects Synced rather than Standalone Mode.</param>
    /// <remarks>
    /// This method reads only background-owned visual settings and the held Waveform. An active Drop
    /// is the raw Data Surface fact that selects the Drop background; its Value remains one so the
    /// rotating hue gradient reaches full brightness without clipping into flat RGB bands.
    /// </remarks>
    private void DrawBackground(bool isSynced)
    {
        float backgroundHueRate = isSynced
            ? SyncSettings.BackgroundHueRate
            : standaloneSettings.BackgroundHueRate;
        float backgroundDropTileHueStep = SyncSettings.BackgroundDropTileHueStep;
        float backgroundDropHueRate = SyncSettings.BackgroundDropHueRate;
        float backgroundDropValue = SyncSettings.BackgroundDropValue;
        string requestedBackgroundWaveformName = SyncSettings.BackgroundWaveformName;
        if (requestedBackgroundWaveformName != acquiredBackgroundWaveformName)
        {
            waveform = waveforms.Named(requestedBackgroundWaveformName);
            acquiredBackgroundWaveformName = requestedBackgroundWaveformName;
        }
        float backgroundWaveformBrightness = isSynced
            ? waveform.Lerp(SyncSettings.BackgroundWaveformBrightnessFloor, 1f)
            : 1f;

        backgroundHue += effectDelta * backgroundHueRate;
        backgroundHue = Mathf.Repeat(backgroundHue, 1f);
        if (beatManager.Drop.Active)
        {
            float backgroundDropHueOffset = effectTime * backgroundDropHueRate;
            for (int i = 0; i < buffer.Length; i++)
            {
                // The foreground pass overwrites exact Circle/Arc membership, so its Drop colors
                // cannot survive this frame and do not need an HSV conversion here.
                if (foregroundShapes.GetGroupIndex(i) >= 0)
                {
                    continue;
                }

                float phase = Mathf.Repeat(
                    i * backgroundDropTileHueStep + backgroundDropHueOffset,
                    1f);
                Color dropBackgroundColor = Color.HSVToRGB(
                    phase,
                    1f,
                    backgroundDropValue);
                dropBackgroundColor.r *= backgroundWaveformBrightness;
                dropBackgroundColor.g *= backgroundWaveformBrightness;
                dropBackgroundColor.b *= backgroundWaveformBrightness;
                buffer[i] = dropBackgroundColor;
            }
            return;
        }

        Color backgroundColor = Color.HSVToRGB(
            backgroundHue,
            1f,
            1f);
        backgroundColor.r *= backgroundWaveformBrightness;
        backgroundColor.g *= backgroundWaveformBrightness;
        backgroundColor.b *= backgroundWaveformBrightness;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = backgroundColor;
        }
    }

    /// <summary>
    /// Draws exact Circle/Arc Shape List members over the complementary background base pass.
    /// </summary>
    /// <param name="isSynced">Whether the Data Surface selects Synced rather than Standalone Mode.</param>
    /// <remarks>
    /// This method reads only foreground-owned visual settings plus the Energy, Drop, Fill, and Timing
    /// facts that drive those foreground mappings. Its writes are limited to exact
    /// Circle/Arc membership.
    /// </remarks>
    private void DrawForeground(bool isSynced)
    {
        float foregroundTilePositionStep = isSynced
            ? SyncSettings.ForegroundTilePositionStep
            : standaloneSettings.ForegroundTilePositionStep;
        float foregroundPositionAdvancePerSecond = isSynced
            ? SyncSettings.ForegroundPositionAdvancePerSecond
            : standaloneSettings.ForegroundPositionAdvancePerSecond;
        float foregroundEnergyCrawlSpeedMultiplier = GetForegroundEnergyCrawlSpeedMultiplier(
            beatManager.Energy.Level);
        foregroundPositionAdvancePerSecond *= foregroundEnergyCrawlSpeedMultiplier;
        foregroundDropRibbonEnvelope = beatManager.Drop.In.Decay(
            SyncSettings.ForegroundDropRibbonWindowBeats);
        UpdateForegroundDropRibbonFlowPhase(foregroundDropRibbonEnvelope);
        PaletteConditioning foregroundPaletteConditioning = isSynced
            ? SyncSettings.ForegroundPaletteConditioning
            : standaloneSettings.ForegroundPaletteConditioning;
        foregroundPalette.Refresh(APalette, foregroundPaletteConditioning);
        float foregroundDropRibbonBrightness = SyncSettings.ForegroundDropRibbonBrightness;
        float foregroundFillBlackAndWhiteProbability =
            SyncSettings.ForegroundFillBlackAndWhiteProbability;
        float foregroundFillBrightnessLift = SyncSettings.ForegroundFillBrightnessLift;
        int groupCount = foregroundShapes.GroupCount;

        if (Random.value < ForegroundGroupReseedsPerSecond * effectDelta)
        {
            foregroundPositions[Random.Range(0, groupCount)] = Random.value;
        }

        bool fillActive = beatManager.Fill.Active;
        bool ribbonActive = foregroundDropRibbonEnvelope > 0f;
        float foregroundPositionAdvance = foregroundPositionAdvancePerSecond * effectDelta;
        for (int i = 0; i < groupCount; i++)
        {
            LayoutData.ShapeList.Group group = foregroundShapes.GetGroup(i);
            float groupPosition = foregroundPositions[i];
            bool blackAndWhite =
                fillActive &&
                Random.value < foregroundFillBlackAndWhiteProbability;

            for (int j = 0; j < group.TileCount; j++)
            {
                int idx = group[j];
                float palettePosition =
                    (groupPosition +
                    foregroundTilePositionStep * group.PackedIndex(j)) % 1f;
                Color paletteColor = foregroundPalette.ReadCyclic(
                    palettePosition,
                    doblend: true);
                if (blackAndWhite)
                {
                    // Fill desaturates the sampled palette color without overwriting the group's
                    // stored position, so its B&W identity keeps the crawl and ends with the Fill.
                    // The gray's Value is lifted toward full brightness so the flash reads bright
                    // even when the palette sample is dark.
                    Color.RGBToHSV(paletteColor, out _, out _, out float value);
                    value = Mathf.Lerp(value, 1f, foregroundFillBrightnessLift);
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
                        foregroundShapes.GetPosition(idx) + foregroundDropRibbonFlowPhase,
                        1f);
                    paletteColor = Color.Lerp(
                        paletteColor,
                        Color.HSVToRGB(ribbonHue, 1f, foregroundDropRibbonBrightness),
                        foregroundDropRibbonEnvelope);
                }
                buffer[idx] = paletteColor;
            }
            foregroundPositions[i] = (groupPosition + foregroundPositionAdvance) % 1f;
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
    private float GetForegroundEnergyCrawlSpeedMultiplier(Energy? energy)
    {
        if (!beatManager.IsSynced)
        {
            return 1f;
        }

        return (energy ?? Energy.Mid) switch
        {
            Energy.Low => SyncSettings.ForegroundEnergyCrawlSpeedMultiplier.Min,
            Energy.Mid => Mathf.Lerp(
                SyncSettings.ForegroundEnergyCrawlSpeedMultiplier.Min,
                SyncSettings.ForegroundEnergyCrawlSpeedMultiplier.Max,
                0.5f),
            Energy.High => SyncSettings.ForegroundEnergyCrawlSpeedMultiplier.Max,
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
/// <remarks>
/// Foreground settings own exact Circle/Arc Shape List membership. Background settings own its
/// complement, which the foreground pass overwrites after rendering.
/// </remarks>
[Serializable]
public sealed class AnimateShapesStandaloneSettings
{
    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live effect-local palette conditioning for the Standalone foreground.</summary>
    public PaletteConditioning ForegroundPaletteConditioning;

    /// <summary>Foreground cyclic palette-position step between Tiles within each packed Circle or Arc.</summary>
    public float ForegroundTilePositionStep;

    /// <summary>Foreground palette-position advance per second for each packed Circle or Arc.</summary>
    public float ForegroundPositionAdvancePerSecond;

    /// <summary>Copies every AnimateShapes Standalone Setting.</summary>
    public void CopyFrom(AnimateShapesStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        ForegroundPaletteConditioning = source.ForegroundPaletteConditioning;
        ForegroundTilePositionStep = source.ForegroundTilePositionStep;
        ForegroundPositionAdvancePerSecond = source.ForegroundPositionAdvancePerSecond;
    }
}

/// <summary>The saved-or-default musical-response settings used by AnimateShapes in Synced Mode.</summary>
/// <remarks>
/// Foreground settings own exact Circle/Arc Shape List membership. Background settings own its
/// complement, which the foreground pass overwrites after rendering.
/// </remarks>
[Serializable]
public sealed class AnimateShapesSyncSettings
{
    /// <summary>Live Synced Mode background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live effect-local palette conditioning for the Synced foreground.</summary>
    public PaletteConditioning ForegroundPaletteConditioning;

    /// <summary>Live foreground cyclic palette-position step between Tiles in each Circle or Arc.</summary>
    public float ForegroundTilePositionStep;

    /// <summary>Live foreground palette-position advance per second for each packed Circle or Arc.</summary>
    public float ForegroundPositionAdvancePerSecond;

    /// <summary>
    /// Low-to-High Energy range for foreground crawl speed. The ruled endpoints are 0.75 and 1.25;
    /// Mid uses their midpoint of exactly one to preserve the approved baseline speed.
    /// </summary>
    public FloatRange ForegroundEnergyCrawlSpeedMultiplier;

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
    /// Live Value supplied to the foreground Drop ribbon color's HSV brightness slot. Scales only
    /// the ribbon rainbow — never the Drop background, and never the palette crawl the ribbon
    /// dissolves into.
    /// </summary>
    public float ForegroundDropRibbonBrightness;

    /// <summary>
    /// Live Pool entry name of the one Waveform this effect holds — the rhythm that changes background
    /// brightness. A name missing from the Pool is a configuration error and fails visibly.
    /// </summary>
    [WaveformName]
    public string BackgroundWaveformName;

    /// <summary>Live background brightness at a held Waveform trough; peaks always reach one.</summary>
    [Range(0f, 1f)]
    public float BackgroundWaveformBrightnessFloor;

    /// <summary>Hue step between consecutive Tile indexes in the active Drop background.</summary>
    public float BackgroundDropTileHueStep;

    /// <summary>Drop background hue cycles advanced per second.</summary>
    public float BackgroundDropHueRate;

    /// <summary>
    /// Value supplied to the Drop background before final HSV-to-RGB conversion. One keeps the
    /// rotating hue gradient smooth and full-brightness.
    /// </summary>
    public float BackgroundDropValue;

    /// <summary>Probability that each foreground Circle or Arc becomes black-and-white during an active Fill.</summary>
    public float ForegroundFillBlackAndWhiteProbability;

    /// <summary>Foreground Fill gray lift as a fraction of its distance to full brightness.</summary>
    public float ForegroundFillBrightnessLift;

    /// <summary>Copies every AnimateShapes Sync Setting from another value.</summary>
    public void CopyFrom(AnimateShapesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        ForegroundPaletteConditioning = source.ForegroundPaletteConditioning;
        ForegroundTilePositionStep = source.ForegroundTilePositionStep;
        ForegroundPositionAdvancePerSecond = source.ForegroundPositionAdvancePerSecond;
        ForegroundEnergyCrawlSpeedMultiplier = new FloatRange(
            source.ForegroundEnergyCrawlSpeedMultiplier.Min,
            source.ForegroundEnergyCrawlSpeedMultiplier.Max,
            source.ForegroundEnergyCrawlSpeedMultiplier.LowRail,
            source.ForegroundEnergyCrawlSpeedMultiplier.HighRail);
        ForegroundDropRibbonWindowBeats = source.ForegroundDropRibbonWindowBeats;
        ForegroundDropRibbonFlowCyclesPerBeatAtLanding =
            source.ForegroundDropRibbonFlowCyclesPerBeatAtLanding;
        ForegroundDropRibbonBrightness = source.ForegroundDropRibbonBrightness;
        BackgroundWaveformName = source.BackgroundWaveformName;
        BackgroundWaveformBrightnessFloor = source.BackgroundWaveformBrightnessFloor;
        BackgroundDropTileHueStep = source.BackgroundDropTileHueStep;
        BackgroundDropHueRate = source.BackgroundDropHueRate;
        BackgroundDropValue = source.BackgroundDropValue;
        ForegroundFillBlackAndWhiteProbability = source.ForegroundFillBlackAndWhiteProbability;
        ForegroundFillBrightnessLift = source.ForegroundFillBrightnessLift;
    }
}
