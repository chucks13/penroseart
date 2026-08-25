using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Maintains a fading palette sparkle field with rare contrasting glints over the previous Buffer.
/// A sparkle is one Tile born at its palette color, fading with the field. A glint is a sparkle
/// born bright.
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

    /// <summary>Authored Standalone hue-scroll speed in wheel revolutions per second.</summary>
    private const float StandaloneHueBaseRate = 0f;

    /// <summary>Authored Standalone extra hue-scroll speed multiplied by the fixed drive.</summary>
    private const float StandaloneHueBeatRate = 0f;

    /// <summary>Authored fixed Standalone drive for the extra hue-scroll rate.</summary>
    private const float StandaloneHueCycleDrive = 0f;

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

    /// <summary>
    /// Authored maximum hue for each HSV confetti sparkle in Synced Mode, tuned at the wall to a
    /// fifth of the wheel.
    /// </summary>
    private const float SyncPerSparkleHueMax = 0.2f;

    /// <summary>Authored minimum cyclic palette coordinate for Synced palette variants.</summary>
    private const float SyncCoordinateMin = 0f;

    /// <summary>
    /// Authored maximum cyclic palette coordinate for Synced palette variants, tuned at the wall to
    /// a fifth of the palette.
    /// </summary>
    private const float SyncCoordinateMax = 0.2f;

    /// <summary>Authored minimum Synced sparkle rate while Energy is Low.</summary>
    private const float SyncLowSparklesPerSecondMin = 500f;

    /// <summary>Authored maximum Synced sparkle rate while Energy is Low.</summary>
    private const float SyncLowSparklesPerSecondMax = 700f;

    /// <summary>Authored minimum Synced sparkle rate while Energy is Mid.</summary>
    private const float SyncMidSparklesPerSecondMin = 300f;

    /// <summary>Authored maximum Synced sparkle rate while Energy is Mid.</summary>
    private const float SyncMidSparklesPerSecondMax = 500f;

    /// <summary>Authored minimum Synced sparkle rate while Energy is High.</summary>
    private const float SyncHighSparklesPerSecondMin = 150f;

    /// <summary>Authored maximum Synced sparkle rate while Energy is High.</summary>
    private const float SyncHighSparklesPerSecondMax = 300f;

    /// <summary>Authored inclusive minimum glint count while Energy is Low.</summary>
    private const int SyncLowGlintsPerBeatMinInclusive = 10;

    /// <summary>Authored exclusive maximum glint count while Energy is Low.</summary>
    private const int SyncLowGlintsPerBeatMaxExclusive = 40;

    /// <summary>Authored inclusive minimum glint count while Energy is Mid.</summary>
    private const int SyncMidGlintsPerBeatMinInclusive = 50;

    /// <summary>Authored exclusive maximum glint count while Energy is Mid.</summary>
    private const int SyncMidGlintsPerBeatMaxExclusive = 150;

    /// <summary>Authored inclusive minimum glint count while Energy is High.</summary>
    private const int SyncHighGlintsPerBeatMinInclusive = 150;

    /// <summary>Authored exclusive maximum glint count while Energy is High.</summary>
    private const int SyncHighGlintsPerBeatMaxExclusive = 300;

    /// <summary>Authored high Rail for the High-Energy glint count, one third of the wall.</summary>
    private const int SyncHighGlintsPerBeatHighRail = 300;

    /// <summary>Authored choice to place Synced rates and counts from a live Levels reading.</summary>
    private const bool SyncLevelsDrive = true;

    /// <summary>Authored Levels band that places Synced sparkle rates and glint counts.</summary>
    private const Band SyncLevelsDriveBand = Band.Low;

    /// <summary>Authored Levels form that places Synced sparkle rates and glint counts.</summary>
    private const LevelsForm SyncLevelsDriveForm = LevelsForm.Normalized;

    /// <summary>Authored Levels band that gates beat-fired glints.</summary>
    private const Band SyncGlintGateBand = Band.Low;

    /// <summary>Authored Levels form that gates beat-fired glints.</summary>
    private const LevelsForm SyncGlintGateForm = LevelsForm.Normalized;

    /// <summary>Authored Levels threshold that a beat must exceed before glints fire.</summary>
    private const float SyncGlintGateThreshold = 0.375f;

    /// <summary>Authored minimum relative luminance rolled independently by each Synced glint.</summary>
    private const float SyncGlintLuminanceMin = 0.6f;

    /// <summary>Authored maximum relative luminance rolled independently by each Synced glint.</summary>
    private const float SyncGlintLuminanceMax = 0.9f;

    /// <summary>Authored minimum fade duration for each Synced glint, in beat fractions.</summary>
    private const float SyncGlintFadeBeatsMin = 0.5f;

    /// <summary>Authored maximum fade duration for each Synced glint, in beat fractions.</summary>
    private const float SyncGlintFadeBeatsMax = 1f;

    /// <summary>Authored scale applied to the darkest conditioned palette entry for the Synced field floor.</summary>
    private const float SyncFloorLevel = 0f;

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

    /// <summary>Authored Pool Waveform held for the Synced hue-scroll response.</summary>
    private const string SyncWaveformName = "beats 2 and 4";

    /// <summary>Authored Synced base hue-scroll speed in wheel revolutions per second.</summary>
    private const float SyncHueBaseRate = 0.05f;

    /// <summary>Authored Synced extra hue-scroll speed at the held Waveform's driven peak.</summary>
    private const float SyncHueBeatRate = 2f;

    /// <summary>Authored minimum hue-scroll drive at the held Waveform's trough.</summary>
    private const float SyncHueCycleDriveMin = 0f;

    /// <summary>Authored maximum hue-scroll drive at the held Waveform's peak.</summary>
    private const float SyncHueCycleDriveMax = 2f;

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

    /// <summary>One Synced glint's independently timed birth color and remaining fade clock.</summary>
    private struct GlintState
    {
        /// <summary>The bright color written on the glint's birth frame.</summary>
        private Color birthColor;

        /// <summary>The glint's complete fade duration in seconds.</summary>
        private float durationSeconds;

        /// <summary>The glint's remaining fade duration in seconds.</summary>
        private float remainingSeconds;

        /// <summary>Whether this glint still owns its Tile.</summary>
        internal readonly bool Active => remainingSeconds > 0f;

        /// <summary>Starts or restarts this glint at its independently rolled color and duration.</summary>
        /// <param name="color">The glint's bright birth color.</param>
        /// <param name="duration">The glint's fade duration in seconds.</param>
        internal void Start(Color color, float duration)
        {
            birthColor = color;
            durationSeconds = duration;
            remainingSeconds = duration;
        }

        /// <summary>Advances only this glint's clock and fades its birth color toward the field floor.</summary>
        /// <param name="floorColor">The current field floor beneath the glint.</param>
        /// <param name="deltaSeconds">Elapsed frame time in seconds.</param>
        /// <returns>The glint color for this frame.</returns>
        internal Color Advance(Color floorColor, float deltaSeconds)
        {
            remainingSeconds -= deltaSeconds;
            return Color.Lerp(floorColor, birthColor, remainingSeconds / durationSeconds);
        }
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
        HueBaseRate = StandaloneHueBaseRate,
        HueBeatRate = StandaloneHueBeatRate,
        HueCycleDrive = StandaloneHueCycleDrive,
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
        LowSparklesPerSecond = new FloatRange(
            SyncLowSparklesPerSecondMin,
            SyncLowSparklesPerSecondMax),
        MidSparklesPerSecond = new FloatRange(
            SyncMidSparklesPerSecondMin,
            SyncMidSparklesPerSecondMax),
        HighSparklesPerSecond = new FloatRange(
            SyncHighSparklesPerSecondMin,
            SyncHighSparklesPerSecondMax),
        LowGlintsPerBeat = new IntRange(
            SyncLowGlintsPerBeatMinInclusive,
            SyncLowGlintsPerBeatMaxExclusive),
        MidGlintsPerBeat = new IntRange(
            SyncMidGlintsPerBeatMinInclusive,
            SyncMidGlintsPerBeatMaxExclusive),
        HighGlintsPerBeat = new IntRange(
            SyncHighGlintsPerBeatMinInclusive,
            SyncHighGlintsPerBeatMaxExclusive,
            SyncHighGlintsPerBeatMinInclusive,
            SyncHighGlintsPerBeatHighRail),
        LevelsDrive = SyncLevelsDrive,
        LevelsDriveBand = SyncLevelsDriveBand,
        LevelsDriveForm = SyncLevelsDriveForm,
        GlintGateBand = SyncGlintGateBand,
        GlintGateForm = SyncGlintGateForm,
        GlintGateThreshold = SyncGlintGateThreshold,
        GlintLuminance = new FloatRange(
            SyncGlintLuminanceMin,
            SyncGlintLuminanceMax),
        GlintFadeBeats = new FloatRange(
            SyncGlintFadeBeatsMin,
            SyncGlintFadeBeatsMax),
        FloorLevel = SyncFloorLevel,
        FadePerFrame = SyncFadePerFrame,
        PaletteConditioning = SyncPaletteConditioning,
        DropHue = new FloatRange(SyncDropHueMin, SyncDropHueMax),
        DropSparkleDivisor = SyncDropSparkleDivisor,
        FillWhiteChance = SyncFillWhiteChance,
        WaveformName = SyncWaveformName,
        HueBaseRate = SyncHueBaseRate,
        HueBeatRate = SyncHueBeatRate,
        HueCycleDrive = new FloatRange(
            SyncHueCycleDriveMin,
            SyncHueCycleDriveMax),
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

    /// <summary>Random Synced sparkle rate retained until the Data Surface advances to another beat.</summary>
    private float randomSparklesPerSecond;

    /// <summary>The absolute beat on which the random Synced sparkle rate was last rolled.</summary>
    private int? sparkleRateBeat;

    /// <summary>The absolute beat on which qualifying glints last fired.</summary>
    private int? lastGlintBeat;

    /// <summary>Independent per-Tile Synced glint colors and fade clocks.</summary>
    private GlintState[] glints;

    /// <summary>Pool entry name currently held for hue scrolling, retained for live-edit reacquisition.</summary>
    private string acquiredWaveformName;

    /// <summary>Cyclic hue offset applied only when a sparkle or glint is born.</summary>
    private float hueScroll;

    /// <summary>Energy read on the latest Synced frame for the debug display.</summary>
    private Energy? liveEnergy;

    /// <summary>Levels-drive band reading on the latest Synced frame for the debug display.</summary>
    private float levelsDriveReading;

    /// <summary>Count fired by the most recent qualifying beat for the debug display.</summary>
    private int lastGlintCount;

    /// <summary>Whether the previous rendered frame used Synced Mode.</summary>
    private bool wasSynced;

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

    /// <summary>
    /// Returns the activation's selected color variant, live Energy, Levels drive, most recent
    /// glint count, and hue scroll for the Controller debug display.
    /// </summary>
    public override string DebugText()
    {
        string variant = colorVariant == ColorVariant.PaletteSingle
            ? $"ColorSparkle\nPalette single {singlePaletteCoordinate:0.00}"
            : $"ColorSparkle\n{colorVariant}";
        return $"{variant}\nENERGY {liveEnergy?.ToString() ?? "—"} " +
            $"LEVEL {levelsDriveReading:0.00}\nGLINTS {lastGlintCount} SCROLL {hueScroll:0.00}";
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

        string requestedWaveformName = SyncSettings.WaveformName;
        waveform = waveforms.Named(requestedWaveformName);
        acquiredWaveformName = requestedWaveformName;

        sparkleCarry = 0f;
        randomSparklesPerSecond = 0f;
        sparkleRateBeat = null;
        lastGlintBeat = null;
        lastGlintCount = 0;
        hueScroll = 0f;
        wasSynced = isSynced;
        liveEnergy = isSynced ? beatManager.Energy.Level : null;
        levelsDriveReading = isSynced
            ? ReadLevel(SyncSettings.LevelsDriveBand, SyncSettings.LevelsDriveForm)
            : 0f;
        ResetGlints();
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
        string requestedWaveformName = SyncSettings.WaveformName;
        if (requestedWaveformName != acquiredWaveformName)
        {
            waveform = waveforms.Named(requestedWaveformName);
            acquiredWaveformName = requestedWaveformName;
        }

        if (isSynced != wasSynced)
        {
            ResetGlints();
            sparkleRateBeat = null;
            lastGlintBeat = null;
            lastGlintCount = 0;
            if (!isSynced)
            {
                hueScroll = 0f;
            }

            wasSynced = isSynced;
        }

        FloatRange perSparkleHue = isSynced
            ? SyncSettings.PerSparkleHue
            : standaloneSettings.PerSparkleHue;
        FloatRange coordinateRange = isSynced
            ? SyncSettings.CoordinateRange
            : standaloneSettings.CoordinateRange;
        float floorLevel = isSynced
            ? SyncSettings.FloorLevel
            : standaloneSettings.FloorLevel;
        float fadePerFrame = isSynced
            ? SyncSettings.FadePerFrame
            : standaloneSettings.FadePerFrame;
        PaletteConditioning paletteConditioning = isSynced
            ? SyncSettings.PaletteConditioning
            : standaloneSettings.PaletteConditioning;
        conditionedPalette.Refresh(APalette, paletteConditioning);

        float hueCycleDrive = isSynced
            ? waveform.Lerp(
                SyncSettings.HueCycleDrive.Min,
                SyncSettings.HueCycleDrive.Max)
            : standaloneSettings.HueCycleDrive;
        float hueBaseRate = isSynced
            ? SyncSettings.HueBaseRate
            : standaloneSettings.HueBaseRate;
        float hueBeatRate = isSynced
            ? SyncSettings.HueBeatRate
            : standaloneSettings.HueBeatRate;
        hueScroll = Mathf.Repeat(
            hueScroll + ((hueBaseRate + (hueCycleDrive * hueBeatRate)) * effectDelta),
            1f);

        float sparklesPerSecond;
        if (isSynced)
        {
            Energy energy = beatManager.Energy.Level.Value;
            liveEnergy = energy;
            levelsDriveReading = ReadLevel(
                SyncSettings.LevelsDriveBand,
                SyncSettings.LevelsDriveForm);
            sparklesPerSecond = ResolveSyncedSparklesPerSecond(
                energy,
                beatManager.Timing.Beat.Value,
                levelsDriveReading);
        }
        else
        {
            liveEnergy = null;
            levelsDriveReading = 0f;
            sparklesPerSecond = standaloneSettings.SparklesPerSecond;
        }

        Color floorColor = FindDarkestConditionedPaletteColor() * floorLevel;
        FadeFieldAndGlints(floorColor, fadePerFrame, effectDelta, isSynced);

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

            if (isSynced)
            {
                SpawnSyncedSparkle(sparkleColor);
            }
            else
            {
                SpawnStandaloneSparkle(
                    sparkleColor,
                    standaloneSettings.GlintChance,
                    standaloneSettings.GlintLuminance,
                    coordinateRange);
            }
        }

        if (isSynced)
        {
            FireSyncedGlints(coordinateRange);
        }
    }

    /// <summary>Reads one band from the selected immutable Levels form.</summary>
    /// <param name="band">The low, mid, or high band to read.</param>
    /// <param name="form">The Normalized, Smoothed, or Peak Levels form to read.</param>
    /// <returns>The selected band reading from zero to one.</returns>
    private float ReadLevel(Band band, LevelsForm form)
    {
        LevelBands levels = beatManager.Levels.Select(form);
        return band switch
        {
            Band.Low => levels.Low,
            Band.Mid => levels.Mid,
            Band.High => levels.High,
            _ => throw new ArgumentOutOfRangeException(nameof(band)),
        };
    }

    /// <summary>Returns the live sparkle-rate range for the current Energy state.</summary>
    /// <param name="energy">The current Energy state.</param>
    /// <returns>The current Energy state's live sparkle-rate range.</returns>
    private FloatRange SparkleRateRange(Energy energy) => energy switch
    {
        Energy.Low => SyncSettings.LowSparklesPerSecond,
        Energy.Mid => SyncSettings.MidSparklesPerSecond,
        Energy.High => SyncSettings.HighSparklesPerSecond,
        _ => throw new ArgumentOutOfRangeException(nameof(energy)),
    };

    /// <summary>Returns the live glint-count range for the current Energy state.</summary>
    /// <param name="energy">The current Energy state.</param>
    /// <returns>The current Energy state's live glint-count range.</returns>
    private IntRange GlintCountRange(Energy energy) => energy switch
    {
        Energy.Low => SyncSettings.LowGlintsPerBeat,
        Energy.Mid => SyncSettings.MidGlintsPerBeat,
        Energy.High => SyncSettings.HighGlintsPerBeat,
        _ => throw new ArgumentOutOfRangeException(nameof(energy)),
    };

    /// <summary>
    /// Places the Synced sparkle rate from Levels or retains one random roll until the absolute
    /// beat changes.
    /// </summary>
    /// <param name="energy">The current Energy state.</param>
    /// <param name="absoluteBeat">The current absolute beat from the Data Surface.</param>
    /// <param name="driveReading">The selected Levels-drive band reading.</param>
    /// <returns>The sparkle birth rate for this frame.</returns>
    private float ResolveSyncedSparklesPerSecond(
        Energy energy,
        int absoluteBeat,
        float driveReading)
    {
        FloatRange range = SparkleRateRange(energy);
        if (SyncSettings.LevelsDrive)
        {
            return Mathf.Lerp(range.Min, range.Max, driveReading);
        }

        if (sparkleRateBeat != absoluteBeat)
        {
            randomSparklesPerSecond = Random.Range(range.Min, range.Max);
            sparkleRateBeat = absoluteBeat;
        }

        return randomSparklesPerSecond;
    }

    /// <summary>Places or rolls the current Energy state's glint count for one qualifying beat.</summary>
    /// <param name="energy">The current Energy state.</param>
    /// <param name="driveReading">The selected Levels-drive band reading.</param>
    /// <returns>The number of glints to fire on the beat.</returns>
    private int ResolveGlintCount(Energy energy, float driveReading)
    {
        IntRange range = GlintCountRange(energy);
        return SyncSettings.LevelsDrive
            ? Mathf.RoundToInt(Mathf.Lerp(
                range.MinInclusive,
                range.MaxExclusive - 1,
                driveReading))
            : Random.Range(range.MinInclusive, range.MaxExclusive);
    }

    /// <summary>
    /// Fires every glint for one qualifying beat in the current frame, with independent palette,
    /// luminance, and beat-fraction fade rolls.
    /// </summary>
    /// <param name="coordinateRange">Live cyclic palette coordinate endpoints for glint rolls.</param>
    private void FireSyncedGlints(FloatRange coordinateRange)
    {
        int absoluteBeat = beatManager.Timing.Beat.Value;
        int beatInBar = beatManager.Timing.BeatInBar.Value;
        bool beatGateOpen = beatManager.Beats.OnBeat(beatInBar);
        float gateReading = ReadLevel(
            SyncSettings.GlintGateBand,
            SyncSettings.GlintGateForm);
        if (!TryBeginGlintBeat(
                absoluteBeat,
                beatGateOpen,
                gateReading,
                SyncSettings.GlintGateThreshold))
        {
            return;
        }

        int count = ResolveGlintCount(beatManager.Energy.Level.Value, levelsDriveReading);
        lastGlintCount = count;
        float beatSeconds = beatManager.Timing.BeatAverageMilliseconds.Value / 1000f;
        for (int i = 0; i < count; i++)
        {
            int tileIndex = Random.Range(0, buffer.Length);
            float coordinate = Mathf.Repeat(
                Random.Range(coordinateRange.Min, coordinateRange.Max) + hueScroll,
                1f);
            float luminance = Random.Range(
                SyncSettings.GlintLuminance.Min,
                SyncSettings.GlintLuminance.Max);
            float fadeBeats = Random.Range(
                SyncSettings.GlintFadeBeats.Min,
                SyncSettings.GlintFadeBeats.Max);
            Color color = SetLuminance(
                conditionedPalette.ReadCyclic(coordinate, doblend: true),
                luminance);
            StartGlint(tileIndex, color, beatSeconds * fadeBeats);
        }
    }

    /// <summary>
    /// Opens a qualifying glint beat once, retaining the last fired absolute beat because the
    /// Data Surface publishes a multi-frame gate rather than a one-frame event.
    /// </summary>
    /// <param name="absoluteBeat">The current absolute beat.</param>
    /// <param name="beatGateOpen">Whether the current beat's On Beat gate is open.</param>
    /// <param name="gateReading">The selected glint-gate Levels reading.</param>
    /// <param name="threshold">The live threshold the reading must exceed.</param>
    /// <returns>True once for a qualifying beat; otherwise false.</returns>
    internal bool TryBeginGlintBeat(
        int absoluteBeat,
        bool beatGateOpen,
        float gateReading,
        float threshold)
    {
        if (!beatGateOpen || gateReading <= threshold || lastGlintBeat == absoluteBeat)
        {
            return false;
        }

        lastGlintBeat = absoluteBeat;
        return true;
    }

    /// <summary>Clears every per-Tile glint clock for activation or a mode change.</summary>
    internal void ResetGlints()
    {
        glints = new GlintState[buffer.Length];
    }

    /// <summary>
    /// Fades the field with its retained-per-frame rule while each active Synced glint instead
    /// advances on its own seconds clock.
    /// </summary>
    /// <param name="floorColor">The current field floor.</param>
    /// <param name="fadePerFrame">Fraction of field distance retained this frame.</param>
    /// <param name="deltaSeconds">Elapsed frame time in seconds.</param>
    /// <param name="renderGlints">Whether Synced glints own their active Tiles this frame.</param>
    internal void FadeFieldAndGlints(
        Color floorColor,
        float fadePerFrame,
        float deltaSeconds,
        bool renderGlints)
    {
        for (int tileIndex = 0; tileIndex < buffer.Length; tileIndex++)
        {
            if (renderGlints && glints[tileIndex].Active)
            {
                buffer[tileIndex] = glints[tileIndex].Advance(floorColor, deltaSeconds);
            }
            else
            {
                buffer[tileIndex] =
                    floorColor + ((buffer[tileIndex] - floorColor) * fadePerFrame);
            }
        }
    }

    /// <summary>Starts one glint and writes its crisp birth color in the firing frame.</summary>
    /// <param name="tileIndex">The random Tile carrying the glint.</param>
    /// <param name="color">The glint's independently rolled bright color.</param>
    /// <param name="durationSeconds">The glint's independently rolled fade duration.</param>
    internal void StartGlint(int tileIndex, Color color, float durationSeconds)
    {
        glints[tileIndex].Start(color, durationSeconds);
        buffer[tileIndex] = color;
    }

    /// <summary>Writes a Synced field sparkle only when no fading glint owns the selected Tile.</summary>
    /// <param name="tileIndex">The random Tile selected for the field sparkle.</param>
    /// <param name="color">The field sparkle's birth color.</param>
    /// <returns>True when the sparkle was written; false while a glint protects the Tile.</returns>
    internal bool TryWriteSyncedSparkle(int tileIndex, Color color)
    {
        if (glints[tileIndex].Active)
        {
            return false;
        }

        buffer[tileIndex] = color;
        return true;
    }

    /// <summary>Selects one random Tile for a Synced field sparkle.</summary>
    /// <param name="color">The sparkle's birth color.</param>
    private void SpawnSyncedSparkle(Color color)
    {
        TryWriteSyncedSparkle(Random.Range(0, buffer.Length), color);
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
                Mathf.Repeat(
                    Random.Range(perSparkleHue.Min, perSparkleHue.Max) + hueScroll,
                    1f),
                1f,
                1f),
            ColorVariant.PaletteScatter => conditionedPalette.ReadCyclic(
                Mathf.Repeat(
                    Random.Range(coordinateRange.Min, coordinateRange.Max) + hueScroll,
                    1f),
                doblend: true),
            _ => conditionedPalette.ReadCyclic(
                Mathf.Repeat(singlePaletteCoordinate + hueScroll, 1f),
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
    /// Writes one uniformly placed Standalone sparkle. A rare glint instead flashes its single Tile
    /// with the conditioned palette at its own rolled coordinate, set to the glint luminance — a
    /// glint contrasts the field by hue and brightness, never by size.
    /// </summary>
    /// <param name="color">The sparkle's birth color.</param>
    /// <param name="glintChance">Chance that this sparkle is born as a glint.</param>
    /// <param name="glintLuminance">Relative luminance every glint flashes at.</param>
    /// <param name="coordinateRange">Cyclic palette coordinate endpoints for the glint's own roll.</param>
    private void SpawnStandaloneSparkle(
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
                    Mathf.Repeat(
                        Random.Range(coordinateRange.Min, coordinateRange.Max) + hueScroll,
                        1f),
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

    /// <summary>Baseline hue-scroll speed in wheel revolutions per second.</summary>
    [Min(0f)] public float HueBaseRate;

    /// <summary>Extra hue-scroll speed multiplied by the fixed Standalone drive.</summary>
    [Min(0f)] public float HueBeatRate;

    /// <summary>Fixed Standalone drive applied to the extra hue-scroll rate.</summary>
    [Min(0f)] public float HueCycleDrive;

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
        HueBaseRate = source.HueBaseRate;
        HueBeatRate = source.HueBeatRate;
        HueCycleDrive = source.HueCycleDrive;
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

    /// <summary>Sparkle birth-rate range while Energy is Low.</summary>
    [Header("Sparkles per second by Energy")]
    public FloatRange LowSparklesPerSecond;

    /// <summary>Sparkle birth-rate range while Energy is Mid.</summary>
    public FloatRange MidSparklesPerSecond;

    /// <summary>Sparkle birth-rate range while Energy is High.</summary>
    public FloatRange HighSparklesPerSecond;

    /// <summary>Inclusive-minimum, exclusive-maximum glint-count range while Energy is Low.</summary>
    [Header("Glints per beat by Energy")]
    public IntRange LowGlintsPerBeat;

    /// <summary>Inclusive-minimum, exclusive-maximum glint-count range while Energy is Mid.</summary>
    public IntRange MidGlintsPerBeat;

    /// <summary>Inclusive-minimum, exclusive-maximum glint-count range while Energy is High.</summary>
    public IntRange HighGlintsPerBeat;

    /// <summary>Whether the selected Levels reading places sparkle rates and glint counts.</summary>
    [Header("Levels drive")]
    public bool LevelsDrive;

    /// <summary>Levels band used to place sparkle rates and glint counts.</summary>
    public Band LevelsDriveBand;

    /// <summary>Levels form used to place sparkle rates and glint counts.</summary>
    public LevelsForm LevelsDriveForm;

    /// <summary>Levels band that gates glints on each On Beat window.</summary>
    [Header("Glint gate and fade")]
    public Band GlintGateBand;

    /// <summary>Levels form that gates glints on each On Beat window.</summary>
    public LevelsForm GlintGateForm;

    /// <summary>Selected glint-gate Levels reading that a beat must exceed.</summary>
    [Range(0f, 1f)] public float GlintGateThreshold;

    /// <summary>Relative luminance range rolled independently by each beat-fired glint.</summary>
    public FloatRange GlintLuminance;

    /// <summary>Fade-duration range rolled independently by each glint, in beat fractions.</summary>
    public FloatRange GlintFadeBeats;

    /// <summary>Scale applied to the darkest conditioned palette entry for the field floor.</summary>
    [Range(0f, 1f)] public float FloorLevel;

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
    /// Live Pool entry name held for hue scrolling. A missing name is a visible configuration
    /// failure rather than a substituted Waveform.
    /// </summary>
    [Header("Hue scroll")]
    [WaveformName]
    public string WaveformName;

    /// <summary>Baseline hue-scroll speed in wheel revolutions per second.</summary>
    [Min(0f)] public float HueBaseRate;

    /// <summary>Extra hue-scroll speed applied through the held Waveform.</summary>
    [Min(0f)] public float HueBeatRate;

    /// <summary>Hue-scroll drive range interpolated by the held Waveform.</summary>
    public FloatRange HueCycleDrive;

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
        LowSparklesPerSecond = Copy(source.LowSparklesPerSecond);
        MidSparklesPerSecond = Copy(source.MidSparklesPerSecond);
        HighSparklesPerSecond = Copy(source.HighSparklesPerSecond);
        LowGlintsPerBeat = Copy(source.LowGlintsPerBeat);
        MidGlintsPerBeat = Copy(source.MidGlintsPerBeat);
        HighGlintsPerBeat = Copy(source.HighGlintsPerBeat);
        LevelsDrive = source.LevelsDrive;
        LevelsDriveBand = source.LevelsDriveBand;
        LevelsDriveForm = source.LevelsDriveForm;
        GlintGateBand = source.GlintGateBand;
        GlintGateForm = source.GlintGateForm;
        GlintGateThreshold = source.GlintGateThreshold;
        GlintLuminance = Copy(source.GlintLuminance);
        GlintFadeBeats = Copy(source.GlintFadeBeats);
        FloorLevel = source.FloorLevel;
        FadePerFrame = source.FadePerFrame;
        PaletteConditioning = source.PaletteConditioning;
        DropHue = new FloatRange(
            source.DropHue.Min,
            source.DropHue.Max,
            source.DropHue.LowRail,
            source.DropHue.HighRail);
        DropSparkleDivisor = source.DropSparkleDivisor;
        FillWhiteChance = source.FillWhiteChance;
        WaveformName = source.WaveformName;
        HueBaseRate = source.HueBaseRate;
        HueBeatRate = source.HueBeatRate;
        HueCycleDrive = Copy(source.HueCycleDrive);
    }

    /// <summary>Copies one Float Range with its endpoints and live-tuned Rails.</summary>
    /// <param name="source">The Float Range to copy.</param>
    /// <returns>An independent copy of the range and its Rails.</returns>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Copies one Int Range with its endpoints and live-tuned Rails.</summary>
    /// <param name="source">The Int Range to copy.</param>
    /// <returns>An independent copy of the range and its Rails.</returns>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}
