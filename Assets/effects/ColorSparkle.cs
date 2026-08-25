using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Maintains a fading Palette sparkle field with rare bright glints over the previous Buffer.
/// A sparkle is one Tile born at its Palette color, fading with the field. A glint is a bright
/// sparkle: the same hue the sparkle would have had on that Tile, lifted to glint luminance.
/// </summary>
[EffectSyncSettings(typeof(ColorSparkleSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(ColorSparkleStandaloneSettingsAsset))]
public class ColorSparkle : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored chance that an activation uses a randomized confetti Palette in Standalone Mode, tuned at the wall.</summary>
    private const float StandaloneConfettiChance = 0.125f;

    /// <summary>Authored number of sorted random hues in a Standalone confetti Palette.</summary>
    private const int StandaloneConfettiPaletteSize = 16;

    /// <summary>Authored minimum cyclic palette coordinate for Standalone palette variants.</summary>
    private const float StandaloneCoordinateMin = 0f;

    /// <summary>Authored maximum cyclic palette coordinate for Standalone palette variants.</summary>
    private const float StandaloneCoordinateMax = 1f;

    /// <summary>Authored chance that a Standalone sparkle is lifted to glint luminance on its Palette hue.</summary>
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

    /// <summary>Authored chance that an activation uses a randomized confetti Palette in Synced Mode, tuned at the wall.</summary>
    private const float SyncConfettiChance = 0.125f;

    /// <summary>Authored number of sorted random hues in a Synced confetti Palette.</summary>
    private const int SyncConfettiPaletteSize = 16;

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

    /// <summary>Authored Pool Waveform held for the Synced 2-and-4 Palette turn.</summary>
    private const string SyncWaveformName = "beats 2 and 4";

    // Runtime mechanism constants

    /// <summary>Structural equal split between single and scatter coordinate policies.</summary>
    private const float CoordinatePolicySplit = 0.5f;

    /// <summary>How an activation chooses the Palette coordinate for a sparkle or glint.</summary>
    private enum CoordinatePolicy
    {
        /// <summary>Every sparkle samples one cyclic palette coordinate held for the activation.</summary>
        Single,

        /// <summary>Every sparkle rolls its own cyclic palette coordinate.</summary>
        Scatter,
    }

    /// <summary>
    /// One Tile's living field sparkle, retaining either its birth Palette coordinate or fixed
    /// color together with the field fade level.
    /// </summary>
    internal struct SparkleState
    {
        /// <summary>Whether a sparkle currently occupies the Tile.</summary>
        private bool active;

        /// <summary>Whether the displayed color follows the activation Palette.</summary>
        private bool followsPalette;

        /// <summary>The Palette coordinate rolled when a turning sparkle was born.</summary>
        private float birthCoordinate;

        /// <summary>The birth color held by a Drop, Fill-white, or Standalone glint sparkle.</summary>
        private Color fixedColor;

        /// <summary>Fraction of the birth color's distance from the field floor still visible.</summary>
        private float fadeLevel;

        /// <summary>Whether a sparkle currently occupies the Tile.</summary>
        internal readonly bool Active => active;

        /// <summary>Whether this sparkle reads the activation Palette on every frame.</summary>
        internal readonly bool FollowsPalette => followsPalette;

        /// <summary>The fixed birth color for a sparkle that skips the Palette turn.</summary>
        internal readonly Color FixedColor => fixedColor;

        /// <summary>Creates a living sparkle at one birth Palette coordinate and full fade level.</summary>
        /// <param name="coordinate">The sparkle's cyclic birth coordinate.</param>
        /// <returns>A sparkle that follows the activation Palette.</returns>
        internal static SparkleState Palette(float coordinate) => new()
        {
            active = true,
            followsPalette = true,
            birthCoordinate = coordinate,
            fadeLevel = 1f,
        };

        /// <summary>Creates a living fixed-color sparkle at full fade level.</summary>
        /// <param name="color">The sparkle's fixed birth color.</param>
        /// <returns>A sparkle that skips the Palette turn.</returns>
        internal static SparkleState Fixed(Color color) => new()
        {
            active = true,
            followsPalette = false,
            fixedColor = color,
            fadeLevel = 1f,
        };

        /// <summary>Returns the birth coordinate advanced and wrapped by one Hump's progress.</summary>
        /// <param name="turnProgress">The current Hump's trough-to-trough progress.</param>
        /// <returns>The displayed cyclic Palette coordinate.</returns>
        internal readonly float TurnedCoordinate(float turnProgress) =>
            Mathf.Repeat(birthCoordinate + turnProgress, 1f);

        /// <summary>Advances the field fade and places the current birth color above the floor.</summary>
        /// <param name="color">The current Palette read or the held fixed birth color.</param>
        /// <param name="floorColor">The field floor for this frame.</param>
        /// <param name="fadePerFrame">The fraction of distance from the floor retained this frame.</param>
        /// <returns>The sparkle's displayed color for this frame.</returns>
        internal Color Advance(Color color, Color floorColor, float fadePerFrame)
        {
            fadeLevel *= fadePerFrame;
            return floorColor + ((color - floorColor) * fadeLevel);
        }

        /// <summary>Releases the Tile when a glint replaces the field sparkle.</summary>
        internal void Clear()
        {
            active = false;
        }
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
        ConfettiPaletteSize = StandaloneConfettiPaletteSize,
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
        ConfettiPaletteSize = SyncConfettiPaletteSize,
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

    /// <summary>Randomized Palette held when the activation Roll selects confetti.</summary>
    private GPalette confettiPalette;

    /// <summary>Whether every activation Palette read comes from the held confetti Palette.</summary>
    private bool usesConfettiPalette;

    /// <summary>The activation's single-or-scatter coordinate policy.</summary>
    private CoordinatePolicy coordinatePolicy;

    /// <summary>The cyclic coordinate held when <see cref="CoordinatePolicy.Single"/> is selected.</summary>
    private float singlePaletteCoordinate;

    /// <summary>Per-Tile field sparkle coordinates, fixed colors, and fade levels.</summary>
    private SparkleState[] sparkles;

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

    /// <summary>Pool entry name held for the 2-and-4 turn, retained for live-edit reacquisition.</summary>
    private string acquiredWaveformName;

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
    /// Returns the activation Palette and coordinate policy, live Energy, Levels drive, and most
    /// recent glint count for the Controller debug display.
    /// </summary>
    public override string DebugText()
    {
        string paletteName = usesConfettiPalette ? "Confetti" : "Shared";
        string coordinate = coordinatePolicy == CoordinatePolicy.Single
            ? $"single {singlePaletteCoordinate:0.00}"
            : "scatter";
        return $"ColorSparkle\n{paletteName} Palette {coordinate}\n" +
            $"ENERGY {liveEnergy?.ToString() ?? "—"} LEVEL {levelsDriveReading:0.00}\n" +
            $"GLINTS {lastGlintCount}";
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
        int confettiPaletteSize = isSynced
            ? SyncSettings.ConfettiPaletteSize
            : standaloneSettings.ConfettiPaletteSize;
        FloatRange coordinateRange = isSynced
            ? SyncSettings.CoordinateRange
            : standaloneSettings.CoordinateRange;
        RollActivationPalette(confettiChance, confettiPaletteSize);
        RollCoordinatePolicy(coordinateRange);
        dropHue = Random.Range(SyncSettings.DropHue.Min, SyncSettings.DropHue.Max);

        string requestedWaveformName = SyncSettings.WaveformName;
        waveform = waveforms.Named(requestedWaveformName);
        acquiredWaveformName = requestedWaveformName;

        sparkleCarry = 0f;
        randomSparklesPerSecond = 0f;
        sparkleRateBeat = null;
        lastGlintBeat = null;
        lastGlintCount = 0;
        wasSynced = isSynced;
        liveEnergy = isSynced ? beatManager.Energy.Level : null;
        levelsDriveReading = isSynced
            ? ReadLevel(SyncSettings.LevelsDriveBand, SyncSettings.LevelsDriveForm)
            : 0f;
        ResetSparklesAndGlints();
        buffer.Clear();
        controller.debugText.text = DebugText();
    }

    /// <summary>
    /// Rolls the activation's Palette source and builds its fixed sorted confetti Palette when
    /// selected. Settings resolution happens before this method and consumes no Random.
    /// </summary>
    /// <param name="confettiChance">Chance that this activation uses a confetti Palette.</param>
    /// <param name="confettiPaletteSize">Number of random hues in the confetti Palette.</param>
    private void RollActivationPalette(float confettiChance, int confettiPaletteSize)
    {
        usesConfettiPalette = Random.value < confettiChance;
        confettiPalette = usesConfettiPalette
            ? CreateConfettiPalette(confettiPaletteSize)
            : null;
    }

    /// <summary>Builds one confetti Palette from sorted full-saturation, full-value random hues.</summary>
    /// <param name="size">Number of random Palette entries.</param>
    /// <returns>The fixed confetti Palette for one activation.</returns>
    internal static GPalette CreateConfettiPalette(int size)
    {
        var hues = new float[size];
        for (int i = 0; i < hues.Length; i++)
        {
            hues[i] = Random.value;
        }

        Array.Sort(hues);
        var colors = new Color[hues.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.HSVToRGB(hues[i], 1f, 1f);
        }

        return new GPalette(colors);
    }

    /// <summary>Rolls the activation's single-or-scatter coordinate policy.</summary>
    /// <param name="coordinateRange">Active mode's cyclic Palette coordinate endpoints.</param>
    private void RollCoordinatePolicy(FloatRange coordinateRange)
    {
        singlePaletteCoordinate = 0f;
        coordinatePolicy = Random.value < CoordinatePolicySplit
            ? CoordinatePolicy.Single
            : CoordinatePolicy.Scatter;
        if (coordinatePolicy == CoordinatePolicy.Single)
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
            wasSynced = isSynced;
        }

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
        float turnProgress = waveform.TroughToTroughProgress;

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
        FadeFieldAndGlints(
            floorColor,
            fadePerFrame,
            effectDelta,
            isSynced,
            turnProgress);

        sparkleCarry += sparklesPerSecond * effectDelta;
        int count = Mathf.FloorToInt(sparkleCarry);
        sparkleCarry -= count;

        bool dropActive = beatManager.Drop.Active;
        if (dropActive)
        {
            count /= SyncSettings.DropSparkleDivisor;
        }

        bool fillActive = beatManager.Fill.Active;
        Color dropColor = dropActive
            ? Color.HSVToRGB(dropHue, 1f, 1f)
            : default;
        for (int i = 0; i < count; i++)
        {
            bool fillWhite = fillActive && Random.value < SyncSettings.FillWhiteChance;
            if (dropActive || fillWhite)
            {
                SpawnFixedSparkle(fillWhite ? Color.white : dropColor, turnProgress);
                continue;
            }

            float coordinate = RollSparkleCoordinate(coordinateRange);
            if (isSynced)
            {
                SpawnSyncedPaletteSparkle(coordinate, turnProgress);
            }
            else
            {
                SpawnStandalonePaletteSparkle(
                    coordinate,
                    standaloneSettings.GlintChance,
                    standaloneSettings.GlintLuminance,
                    turnProgress);
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
    /// Fires every glint for one qualifying beat in the current frame, with independent Palette,
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
            float coordinate = RollSparkleCoordinate(coordinateRange);
            float luminance = Random.Range(
                SyncSettings.GlintLuminance.Min,
                SyncSettings.GlintLuminance.Max);
            float fadeBeats = Random.Range(
                SyncSettings.GlintFadeBeats.Min,
                SyncSettings.GlintFadeBeats.Max);
            Color color = SetLuminance(
                ReadActivationPalette(coordinate),
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

    /// <summary>Clears every field sparkle and glint for a fresh activation.</summary>
    internal void ResetSparklesAndGlints()
    {
        sparkles = new SparkleState[buffer.Length];
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
    /// <param name="turnProgress">Current trough-to-trough progress of the held Waveform.</param>
    internal void FadeFieldAndGlints(
        Color floorColor,
        float fadePerFrame,
        float deltaSeconds,
        bool renderGlints,
        float turnProgress)
    {
        for (int tileIndex = 0; tileIndex < buffer.Length; tileIndex++)
        {
            if (renderGlints && glints[tileIndex].Active)
            {
                buffer[tileIndex] = glints[tileIndex].Advance(floorColor, deltaSeconds);
            }
            else if (sparkles[tileIndex].Active)
            {
                SparkleState sparkle = sparkles[tileIndex];
                Color color = sparkle.FollowsPalette
                    ? ReadActivationPalette(sparkle.TurnedCoordinate(turnProgress))
                    : sparkle.FixedColor;
                buffer[tileIndex] = sparkles[tileIndex].Advance(
                    color,
                    floorColor,
                    fadePerFrame);
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
        sparkles[tileIndex].Clear();
        glints[tileIndex].Start(color, durationSeconds);
        buffer[tileIndex] = color;
    }

    /// <summary>Starts a field sparkle only when no fading glint owns the selected Tile.</summary>
    /// <param name="tileIndex">The random Tile selected for the field sparkle.</param>
    /// <param name="sparkle">The Palette-following or fixed-color sparkle to start.</param>
    /// <param name="turnProgress">Current trough-to-trough progress for its birth frame.</param>
    /// <returns>True when the sparkle was written; false while a glint protects the Tile.</returns>
    internal bool TryStartSparkle(
        int tileIndex,
        SparkleState sparkle,
        float turnProgress)
    {
        if (glints[tileIndex].Active)
        {
            return false;
        }

        sparkles[tileIndex] = sparkle;
        Color color = sparkle.FollowsPalette
            ? ReadActivationPalette(sparkle.TurnedCoordinate(turnProgress))
            : sparkle.FixedColor;
        buffer[tileIndex] = color;
        return true;
    }

    /// <summary>Selects one random Tile for a Synced Palette-following field sparkle.</summary>
    /// <param name="coordinate">The sparkle's cyclic birth coordinate.</param>
    /// <param name="turnProgress">Current trough-to-trough progress for its birth frame.</param>
    private void SpawnSyncedPaletteSparkle(float coordinate, float turnProgress)
    {
        TryStartSparkle(
            Random.Range(0, buffer.Length),
            SparkleState.Palette(coordinate),
            turnProgress);
    }

    /// <summary>Selects one random Tile for a fixed Drop or Fill-white field sparkle.</summary>
    /// <param name="color">The fixed birth color.</param>
    /// <param name="turnProgress">Current trough-to-trough progress, ignored by fixed sparkles.</param>
    private void SpawnFixedSparkle(Color color, float turnProgress)
    {
        TryStartSparkle(
            Random.Range(0, buffer.Length),
            SparkleState.Fixed(color),
            turnProgress);
    }

    /// <summary>Returns the activation's held coordinate or rolls one scatter coordinate.</summary>
    /// <param name="coordinateRange">Active mode's cyclic Palette coordinate endpoints.</param>
    /// <returns>The Palette coordinate for one sparkle or glint.</returns>
    private float RollSparkleCoordinate(FloatRange coordinateRange) =>
        coordinatePolicy == CoordinatePolicy.Single
            ? singlePaletteCoordinate
            : Random.Range(coordinateRange.Min, coordinateRange.Max);

    /// <summary>Reads one cyclic coordinate from the activation's single Palette path.</summary>
    /// <param name="coordinate">The cyclic Palette coordinate.</param>
    /// <returns>The blended activation Palette color at that coordinate.</returns>
    private Color ReadActivationPalette(float coordinate) => usesConfettiPalette
        ? confettiPalette.ReadCyclic(coordinate, doblend: true)
        : conditionedPalette.ReadCyclic(coordinate, doblend: true);

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
    /// Writes one uniformly placed Standalone Palette sparkle. A rare glint reads the same coordinate
    /// at glint luminance and becomes fixed so luminance is its only difference from that sparkle.
    /// </summary>
    /// <param name="coordinate">The sparkle's cyclic birth coordinate.</param>
    /// <param name="glintChance">Chance that this sparkle is born as a glint.</param>
    /// <param name="glintLuminance">Relative luminance every glint flashes at.</param>
    /// <param name="turnProgress">Current trough-to-trough progress, resting at zero in Standalone.</param>
    private void SpawnStandalonePaletteSparkle(
        float coordinate,
        float glintChance,
        float glintLuminance,
        float turnProgress)
    {
        int tileIndex = Random.Range(0, buffer.Length);
        SparkleState sparkle = SparkleState.Palette(coordinate);
        if (Random.value < glintChance)
        {
            sparkle = SparkleState.Fixed(SetLuminance(
                ReadActivationPalette(coordinate),
                glintLuminance));
        }

        TryStartSparkle(tileIndex, sparkle, turnProgress);
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
/// Settings for its authored no-music activation Palette and field rendering.
/// </summary>
[Serializable]
public sealed class ColorSparkleStandaloneSettings
{
    /// <summary>Chance that the activation Roll selects a randomized confetti Palette.</summary>
    [Range(0f, 1f)] public float ConfettiChance;

    /// <summary>Number of sorted random full-saturation, full-value HSV hues in a confetti Palette.</summary>
    public int ConfettiPaletteSize;

    /// <summary>Cyclic Palette coordinate range every coordinate roll draws from: the activation's held single coordinate, or each scatter sparkle's own.</summary>
    public FloatRange CoordinateRange;

    /// <summary>Chance that a spawned sparkle is lifted to glint luminance on its Palette hue.</summary>
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
        ConfettiPaletteSize = source.ConfettiPaletteSize;
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

/// <summary>
/// The serializable value shape shared by ColorSparkle's Sync Defaults and saved Sync Settings for
/// its Palette field, musical response, and retained Drop and Fill controls.
/// </summary>
[Serializable]
public sealed class ColorSparkleSyncSettings
{
    /// <summary>Chance that the activation Roll selects a randomized confetti Palette.</summary>
    [Range(0f, 1f)] public float ConfettiChance;

    /// <summary>Number of sorted random full-saturation, full-value HSV hues in a confetti Palette.</summary>
    public int ConfettiPaletteSize;

    /// <summary>Cyclic Palette coordinate range every coordinate roll draws from: the activation's held single coordinate, or each scatter sparkle's own.</summary>
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
    /// Live Pool entry name held for the 2-and-4 Palette turn. A missing name is a visible configuration
    /// failure rather than a substituted Waveform.
    /// </summary>
    [Header("Hue turn")]
    [WaveformName]
    public string WaveformName;

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
        ConfettiPaletteSize = source.ConfettiPaletteSize;
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
