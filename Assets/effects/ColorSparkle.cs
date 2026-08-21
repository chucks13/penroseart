using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Maintains a fading palette sparkle field with rare contrasting glints over the previous Buffer.
/// </summary>
[EffectSyncSettings(typeof(ColorSparkleSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(ColorSparkleStandaloneSettingsAsset))]
public class ColorSparkle : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored chance that an activation uses full-wheel HSV confetti in Standalone Mode, tuned at the wall.</summary>
    private const float StandaloneConfettiChance = 0.125f;

    /// <summary>Authored minimum hue for each HSV confetti sparkle in Standalone Mode.</summary>
    private const float StandalonePerSparkleHueMin = 0f;

    /// <summary>Authored maximum hue for each HSV confetti sparkle in Standalone Mode.</summary>
    private const float StandalonePerSparkleHueMax = 1f;

    /// <summary>Authored minimum cyclic palette coordinate for Standalone palette variants.</summary>
    private const float StandaloneCoordinateMin = 0f;

    /// <summary>Authored maximum cyclic palette coordinate for Standalone palette variants.</summary>
    private const float StandaloneCoordinateMax = 1f;

    /// <summary>Authored chance that a Standalone sparkle is born as a contrasting glint.</summary>
    private const float StandaloneGlintChance = 0.003f;

    /// <summary>Authored relative luminance every Standalone glint flashes at.</summary>
    private const float StandaloneGlintLuminance = 0.8f;

    /// <summary>Authored scale applied to the darkest conditioned palette entry for the Standalone field floor.</summary>
    private const float StandaloneFloorLevel = 0f;

    /// <summary>Authored Standalone sparkle births per second.</summary>
    private const float StandaloneSparklesPerSecond = 900f;

    /// <summary>
    /// Authored fraction of each Tile's distance from the field floor retained per Buffer frame,
    /// tuned at the wall. The 0.98 Rail ceiling preserves the original lifetime; tuning can only
    /// shorten it.
    /// </summary>
    private const float StandaloneFadePerFrame = 0.975f;

    /// <summary>
    /// Standalone palette conditioning keeps palette families readable while retaining their
    /// relative luminance character for sparkle birth. It matches the Angles authored preset.
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

    // Sync Defaults

    /// <summary>Authored chance that an activation uses full-wheel HSV confetti in Synced Mode, tuned at the wall.</summary>
    private const float SyncConfettiChance = 0.125f;

    /// <summary>Authored minimum hue for each HSV confetti sparkle in Synced Mode.</summary>
    private const float SyncPerSparkleHueMin = 0f;

    /// <summary>Authored maximum hue for each HSV confetti sparkle in Synced Mode.</summary>
    private const float SyncPerSparkleHueMax = 1f;

    /// <summary>Authored minimum cyclic palette coordinate for Synced palette variants.</summary>
    private const float SyncCoordinateMin = 0f;

    /// <summary>Authored maximum cyclic palette coordinate for Synced palette variants.</summary>
    private const float SyncCoordinateMax = 1f;

    /// <summary>Authored chance that a Synced sparkle is born as a contrasting glint.</summary>
    private const float SyncGlintChance = 0.003f;

    /// <summary>Authored relative luminance every Synced glint flashes at.</summary>
    private const float SyncGlintLuminance = 0.8f;

    /// <summary>Authored scale applied to the darkest conditioned palette entry for the Synced field floor.</summary>
    private const float SyncFloorLevel = 0f;

    /// <summary>Authored Synced sparkle births per second.</summary>
    private const float SyncSparklesPerSecond = 900f;

    /// <summary>
    /// Authored fraction of each Tile's distance from the field floor retained per Buffer frame,
    /// tuned at the wall. The 0.98 Rail ceiling preserves the original lifetime; tuning can only
    /// shorten it.
    /// </summary>
    private const float SyncFadePerFrame = 0.975f;

    /// <summary>
    /// Sync palette conditioning is independently live so tuning cannot drift the Standalone look.
    /// It starts from the same Angles authored preset.
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

    /// <summary>Authored minimum for the activation-wide solid hue used by every Drop sparkle.</summary>
    private const float SyncDropHueMin = 0f;

    /// <summary>Authored maximum for the activation-wide solid hue used by every Drop sparkle.</summary>
    private const float SyncDropHueMax = 1f;

    /// <summary>Authored divisor that halves the number of generated sparkles during a Drop.</summary>
    private const int SyncDropSparkleDivisor = 2;

    /// <summary>Authored chance that a newly generated Fill sparkle is white.</summary>
    private const float SyncFillWhiteChance = 0.5f;

    // Runtime mechanism constants

    /// <summary>Structural equal split between palette-single and palette-scatter activations.</summary>
    private const float PaletteVariantSplit = 0.5f;

    /// <summary>Color source selected by the activation Roll.</summary>
    private enum ColorVariant
    {
        /// <summary>Every sparkle samples one cyclic palette coordinate held for the activation.</summary>
        PaletteSingle,

        /// <summary>Every sparkle rolls its own cyclic palette coordinate.</summary>
        PaletteScatter,

        /// <summary>Every sparkle rolls a full-saturation and full-value HSV hue.</summary>
        Confetti,
    }

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate ColorSparkle's authored
    /// Standalone Defaults.
    /// </summary>
    public static ColorSparkleStandaloneSettings StandaloneDefaults => new()
    {
        ConfettiChance = StandaloneConfettiChance,
        PerSparkleHue = new FloatRange(
            StandalonePerSparkleHueMin,
            StandalonePerSparkleHueMax),
        CoordinateRange = new FloatRange(
            StandaloneCoordinateMin,
            StandaloneCoordinateMax),
        GlintChance = StandaloneGlintChance,
        GlintLuminance = StandaloneGlintLuminance,
        FloorLevel = StandaloneFloorLevel,
        SparklesPerSecond = StandaloneSparklesPerSecond,
        FadePerFrame = StandaloneFadePerFrame,
        PaletteConditioning = StandalonePaletteConditioning,
    };

    /// <summary>Resolves a fresh copy of ColorSparkle's file-local Sync Defaults.</summary>
    public static ColorSparkleSyncSettings SyncDefaults => new()
    {
        ConfettiChance = SyncConfettiChance,
        PerSparkleHue = new FloatRange(
            SyncPerSparkleHueMin,
            SyncPerSparkleHueMax),
        CoordinateRange = new FloatRange(
            SyncCoordinateMin,
            SyncCoordinateMax),
        GlintChance = SyncGlintChance,
        GlintLuminance = SyncGlintLuminance,
        FloorLevel = SyncFloorLevel,
        SparklesPerSecond = SyncSparklesPerSecond,
        FadePerFrame = SyncFadePerFrame,
        PaletteConditioning = SyncPaletteConditioning,
        DropHue = new FloatRange(SyncDropHueMin, SyncDropHueMax),
        DropSparkleDivisor = SyncDropSparkleDivisor,
        FillWhiteChance = SyncFillWhiteChance,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private ColorSparkleStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private ColorSparkleSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>
    /// Effect-local conditioned palette endpoints that follow the shared palette revision,
    /// crossfade, and active mode's live controls.
    /// </summary>
    private readonly ConditionedPaletteCache conditionedPalette = new();

    /// <summary>The activation's selected color source.</summary>
    private ColorVariant colorVariant;

    /// <summary>The cyclic coordinate held when <see cref="ColorVariant.PaletteSingle"/> is selected.</summary>
    private float singlePaletteCoordinate;

    /// <summary>Fractional sparkle births carried between rendered frames for uniform per-second cadence.</summary>
    private float sparkleCarry;

    /// <summary>The activation-wide solid hue used by every sparkle during a Drop.</summary>
    private float dropHue;

    /// <summary>
    /// ColorSparkle's fading sparkle field can accent Fill and Drop moments while its gentle shimmer
    /// suits Low/Mid-energy sections.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |
        Repertoire.HandlesDrop |
        Repertoire.EnergyLow |
        Repertoire.EnergyMid;

    /// <summary>Returns the activation's selected color variant for the Controller debug display.</summary>
    public override string DebugText()
    {
        return colorVariant == ColorVariant.PaletteSingle
            ? $"ColorSparkle\nPalette single {singlePaletteCoordinate:0.00}"
            : $"ColorSparkle\n{colorVariant}";
    }

    /// <summary>
    /// Resolves current Effect Settings without disturbing the roll stream, then performs the
    /// activation Roll and clears all carried field state.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(ColorSparkle),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(ColorSparkle),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        float confettiChance = isSynced
            ? SyncSettings.ConfettiChance
            : standaloneSettings.ConfettiChance;
        FloatRange coordinateRange = isSynced
            ? SyncSettings.CoordinateRange
            : standaloneSettings.CoordinateRange;
        RollColorVariant(confettiChance, coordinateRange);
        dropHue = Random.Range(SyncSettings.DropHue.Min, SyncSettings.DropHue.Max);

        sparkleCarry = 0f;
        buffer.Clear();
        controller.debugText.text = DebugText();
    }

    /// <summary>
    /// Selects the activation color variant and, for palette single, its held cyclic coordinate.
    /// The fixed palette split is structural; all authored probabilities and endpoints come from
    /// the active Effect Settings surface.
    /// </summary>
    /// <param name="confettiChance">Chance that the activation selects HSV confetti.</param>
    /// <param name="coordinateRange">Cyclic palette coordinate endpoints for palette variants.</param>
    private void RollColorVariant(float confettiChance, FloatRange coordinateRange)
    {
        singlePaletteCoordinate = 0f;
        if (Random.value < confettiChance)
        {
            colorVariant = ColorVariant.Confetti;
            return;
        }

        colorVariant = Random.value < PaletteVariantSplit
            ? ColorVariant.PaletteSingle
            : ColorVariant.PaletteScatter;
        if (colorVariant == ColorVariant.PaletteSingle)
        {
            singlePaletteCoordinate = Random.Range(
                coordinateRange.Min,
                coordinateRange.Max);
        }
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color Buffer using the active mode's live palette
    /// conditioning and picture settings. Drop and Fill keep their existing color and count overrides.
    /// </summary>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        FloatRange perSparkleHue = isSynced
            ? SyncSettings.PerSparkleHue
            : standaloneSettings.PerSparkleHue;
        FloatRange coordinateRange = isSynced
            ? SyncSettings.CoordinateRange
            : standaloneSettings.CoordinateRange;
        float glintChance = isSynced
            ? SyncSettings.GlintChance
            : standaloneSettings.GlintChance;
        float glintLuminance = isSynced
            ? SyncSettings.GlintLuminance
            : standaloneSettings.GlintLuminance;
        float floorLevel = isSynced
            ? SyncSettings.FloorLevel
            : standaloneSettings.FloorLevel;
        float sparklesPerSecond = isSynced
            ? SyncSettings.SparklesPerSecond
            : standaloneSettings.SparklesPerSecond;
        float fadePerFrame = isSynced
            ? SyncSettings.FadePerFrame
            : standaloneSettings.FadePerFrame;
        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);

        Color floorColor = FindDarkestConditionedPaletteColor() * floorLevel;
        for (int tileIndex = 0; tileIndex < buffer.Length; tileIndex++)
        {
            buffer[tileIndex] =
                floorColor + ((buffer[tileIndex] - floorColor) * fadePerFrame);
        }

        sparkleCarry += sparklesPerSecond * effectDelta;
        int count = Mathf.FloorToInt(sparkleCarry);
        sparkleCarry -= count;

        bool dropActive = beatManager.Drop.Active;
        if (dropActive)
        {
            count /= SyncSettings.DropSparkleDivisor;
        }

        bool fillActive = beatManager.Fill.Active;
        for (int i = 0; i < count; i++)
        {
            Color sparkleColor = dropActive
                ? Color.HSVToRGB(dropHue, 1f, 1f)
                : ReadSparkleColor(perSparkleHue, coordinateRange);
            if (fillActive && Random.value < SyncSettings.FillWhiteChance)
            {
                sparkleColor = Color.white;
            }

            SpawnSparkle(
                sparkleColor,
                glintChance,
                glintLuminance,
                coordinateRange);
        }
    }

    /// <summary>
    /// Resolves one non-Drop sparkle color from the activation variant. Palette colors are returned
    /// as conditioned and crossfaded, with their authored saturation and luminance unchanged.
    /// </summary>
    /// <param name="perSparkleHue">Active mode's full-value HSV hue endpoints.</param>
    /// <param name="coordinateRange">Active mode's cyclic palette coordinate endpoints.</param>
    /// <returns>The sparkle's birth color.</returns>
    private Color ReadSparkleColor(
        FloatRange perSparkleHue,
        FloatRange coordinateRange)
    {
        return colorVariant switch
        {
            ColorVariant.Confetti => Color.HSVToRGB(
                Random.Range(perSparkleHue.Min, perSparkleHue.Max),
                1f,
                1f),
            ColorVariant.PaletteScatter => conditionedPalette.ReadCyclic(
                Random.Range(coordinateRange.Min, coordinateRange.Max),
                doblend: true),
            _ => conditionedPalette.ReadCyclic(
                singlePaletteCoordinate,
                doblend: true),
        };
    }

    /// <summary>
    /// Finds the darkest color in the active conditioned palette crossfade. Scanning every cyclic
    /// endpoint coordinate from both source tables covers every piecewise-linear segment endpoint,
    /// where relative luminance reaches its minimum.
    /// </summary>
    /// <returns>The darkest live conditioned palette color.</returns>
    private Color FindDarkestConditionedPaletteColor()
    {
        GPalette current = APalette.CurrentPalette;
        Color darkest = conditionedPalette.ReadCyclic(0f, doblend: true);
        float darkestLuminance = darkest.RelativeLuminance();

        for (int i = 1; i < current.length; i++)
        {
            Color candidate = conditionedPalette.ReadCyclic(
                CyclicPaletteEntryCoordinate(i, current.length),
                doblend: true);
            float luminance = candidate.RelativeLuminance();
            if (luminance < darkestLuminance)
            {
                darkest = candidate;
                darkestLuminance = luminance;
            }
        }

        if (APalette.IsTransitioning)
        {
            GPalette next = APalette.NextPalette;
            for (int i = 0; i < next.length; i++)
            {
                Color candidate = conditionedPalette.ReadCyclic(
                    CyclicPaletteEntryCoordinate(i, next.length),
                    doblend: true);
                float luminance = candidate.RelativeLuminance();
                if (luminance < darkestLuminance)
                {
                    darkest = candidate;
                    darkestLuminance = luminance;
                }
            }
        }

        return darkest;
    }

    /// <summary>Returns the cyclic normalized coordinate of one palette entry.</summary>
    /// <param name="index">Zero-based entry index.</param>
    /// <param name="length">Palette entry count.</param>
    /// <returns>The entry's coordinate in the half-open cyclic domain.</returns>
    private static float CyclicPaletteEntryCoordinate(int index, int length) =>
        (float)index / length;

    /// <summary>
    /// Writes one uniformly placed sparkle. A rare glint instead flashes its single Tile with the
    /// conditioned palette at its own rolled coordinate, set to the glint luminance — a glint
    /// contrasts the field by hue and brightness, never by size.
    /// </summary>
    /// <param name="color">The sparkle's birth color.</param>
    /// <param name="glintChance">Chance that this sparkle is born as a glint.</param>
    /// <param name="glintLuminance">Relative luminance every glint flashes at.</param>
    /// <param name="coordinateRange">Cyclic palette coordinate endpoints for the glint's own roll.</param>
    private void SpawnSparkle(
        Color color,
        float glintChance,
        float glintLuminance,
        FloatRange coordinateRange)
    {
        int tileIndex = Random.Range(0, buffer.Length);
        if (Random.value < glintChance)
        {
            color = SetLuminance(
                conditionedPalette.ReadCyclic(
                    Random.Range(coordinateRange.Min, coordinateRange.Max),
                    doblend: true),
                glintLuminance);
        }

        buffer[tileIndex] = color;
    }

    /// <summary>
    /// Sets a color to a target relative luminance at the same hue. Full value is tried first
    /// at the authored saturation; saturation falls only when that vivid color cannot reach the
    /// target, so the flash stays as colorful as the target allows.
    /// </summary>
    /// <param name="color">The color whose hue and alpha are preserved.</param>
    /// <param name="targetLuminance">Relative luminance requested for the result.</param>
    /// <returns>The same-hue color at the requested luminance.</returns>
    private static Color SetLuminance(Color color, float targetLuminance)
    {
        Color.RGBToHSV(color, out float hue, out float saturation, out _);
        Color vivid = Color.HSVToRGB(hue, saturation, 1f);
        float vividLuminance = vivid.RelativeLuminance();
        if (targetLuminance <= vividLuminance)
        {
            float scale = targetLuminance / vividLuminance;
            return new Color(
                vivid.r * scale,
                vivid.g * scale,
                vivid.b * scale,
                color.a);
        }

        float liftedSaturation =
            saturation *
            ((1f - targetLuminance) / (1f - vividLuminance));
        Color lifted = Color.HSVToRGB(hue, liftedSaturation, 1f);
        lifted.a = color.a;
        return lifted;
    }
}

/// <summary>
/// The serializable value shape shared by ColorSparkle's Standalone Defaults and saved Standalone
/// Settings for its authored no-music palette sparkle field.
/// </summary>
[Serializable]
public sealed class ColorSparkleStandaloneSettings
{
    /// <summary>Chance that the activation Roll selects HSV confetti.</summary>
    [Range(0f, 1f)] public float ConfettiChance;

    /// <summary>Per-sparkle hue range used by the HSV confetti variant.</summary>
    public FloatRange PerSparkleHue;

    /// <summary>Cyclic palette coordinate range used by palette-single and palette-scatter variants.</summary>
    public FloatRange CoordinateRange;

    /// <summary>Chance that a spawned sparkle is born as a contrasting glint.</summary>
    [Range(0f, 0.05f)] public float GlintChance;

    /// <summary>Relative luminance every glint flashes at.</summary>
    [Range(0f, 1f)] public float GlintLuminance;

    /// <summary>Scale applied to the darkest conditioned palette entry for the field floor.</summary>
    [Range(0f, 1f)] public float FloorLevel;

    /// <summary>Uniform sparkle birth rate per second.</summary>
    [Range(0f, 3600f)] public float SparklesPerSecond;

    /// <summary>
    /// Fraction of each Tile's distance from the floor retained per Buffer frame. The upper Rail
    /// preserves the original sparkle lifetime, so live tuning can only make the fade faster.
    /// </summary>
    [Range(0.9f, 0.98f)] public float FadePerFrame;

    /// <summary>
    /// Live Effect-local palette conditioning for Standalone Mode, independently saved so tuning it
    /// cannot drift the Synced look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>
    /// Copies every ColorSparkle Standalone Setting, including range endpoints, Rails, and palette
    /// conditioning.
    /// </summary>
    /// <param name="source">The Standalone Settings whose values become this value.</param>
    public void CopyFrom(ColorSparkleStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ConfettiChance = source.ConfettiChance;
        PerSparkleHue = new FloatRange(
            source.PerSparkleHue.Min,
            source.PerSparkleHue.Max,
            source.PerSparkleHue.LowRail,
            source.PerSparkleHue.HighRail);
        CoordinateRange = new FloatRange(
            source.CoordinateRange.Min,
            source.CoordinateRange.Max,
            source.CoordinateRange.LowRail,
            source.CoordinateRange.HighRail);
        GlintChance = source.GlintChance;
        GlintLuminance = source.GlintLuminance;
        FloorLevel = source.FloorLevel;
        SparklesPerSecond = source.SparklesPerSecond;
        FadePerFrame = source.FadePerFrame;
        PaletteConditioning = source.PaletteConditioning;
    }
}

/// <summary>The serializable value shape shared by ColorSparkle's Sync Defaults and Sync Settings, including its retained Drop and Fill controls.</summary>
[Serializable]
public sealed class ColorSparkleSyncSettings
{
    /// <summary>Chance that the activation Roll selects HSV confetti.</summary>
    [Range(0f, 1f)] public float ConfettiChance;

    /// <summary>Per-sparkle hue range used by the HSV confetti variant.</summary>
    public FloatRange PerSparkleHue;

    /// <summary>Cyclic palette coordinate range used by palette-single and palette-scatter variants.</summary>
    public FloatRange CoordinateRange;

    /// <summary>Chance that a spawned sparkle is born as a contrasting glint.</summary>
    [Range(0f, 0.05f)] public float GlintChance;

    /// <summary>Relative luminance every glint flashes at.</summary>
    [Range(0f, 1f)] public float GlintLuminance;

    /// <summary>Scale applied to the darkest conditioned palette entry for the field floor.</summary>
    [Range(0f, 1f)] public float FloorLevel;

    /// <summary>Uniform sparkle birth rate per second.</summary>
    [Range(0f, 3600f)] public float SparklesPerSecond;

    /// <summary>
    /// Fraction of each Tile's distance from the floor retained per Buffer frame. The upper Rail
    /// preserves the original sparkle lifetime, so live tuning can only make the fade faster.
    /// </summary>
    [Range(0.9f, 0.98f)] public float FadePerFrame;

    /// <summary>
    /// Live Effect-local palette conditioning for Synced Mode, independently saved so tuning it
    /// cannot drift the Standalone look.
    /// </summary>
    public PaletteConditioning PaletteConditioning;

    /// <summary>Per-activation range for the solid hue used by every Drop sparkle.</summary>
    public FloatRange DropHue;

    /// <summary>Divisor applied to the generated sparkle count during a Drop.</summary>
    [Min(1)] public int DropSparkleDivisor;

    /// <summary>Chance that a newly generated Fill sparkle is white.</summary>
    [Range(0f, 1f)] public float FillWhiteChance;

    /// <summary>
    /// Copies every ColorSparkle Sync Setting, including range endpoints, Rails, palette
    /// conditioning, and the existing Drop and Fill controls.
    /// </summary>
    /// <param name="source">The Sync Settings whose values become this value.</param>
    public void CopyFrom(ColorSparkleSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ConfettiChance = source.ConfettiChance;
        PerSparkleHue = new FloatRange(
            source.PerSparkleHue.Min,
            source.PerSparkleHue.Max,
            source.PerSparkleHue.LowRail,
            source.PerSparkleHue.HighRail);
        CoordinateRange = new FloatRange(
            source.CoordinateRange.Min,
            source.CoordinateRange.Max,
            source.CoordinateRange.LowRail,
            source.CoordinateRange.HighRail);
        GlintChance = source.GlintChance;
        GlintLuminance = source.GlintLuminance;
        FloorLevel = source.FloorLevel;
        SparklesPerSecond = source.SparklesPerSecond;
        FadePerFrame = source.FadePerFrame;
        PaletteConditioning = source.PaletteConditioning;
        DropHue = new FloatRange(
            source.DropHue.Min,
            source.DropHue.Max,
            source.DropHue.LowRail,
            source.DropHue.HighRail);
        DropSparkleDivisor = source.DropSparkleDivisor;
        FillWhiteChance = source.FillWhiteChance;
    }
}
