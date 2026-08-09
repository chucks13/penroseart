using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders falling screen-space droplets over an animated palette background.
/// </summary>
[EffectSyncSettings(typeof(WaterfallSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(WaterfallStandaloneSettingsAsset))]
public class Waterfall : ScreenEffect
{
    // Standalone Defaults

    /// <summary>
    /// Authored Waveform peak height and no-clock fallback for the unchanged Standalone look. The
    /// pulse returns to zero at each peak and in Standalone Mode.
    /// </summary>
    private const float StandaloneWaveformPeakHeight = 0f;

    /// <summary>
    /// Authored inclusive minimum beat-mode Roll bound for the unchanged Standalone look. This Roll
    /// is rendering-invisible in Standalone Mode because pulse history rests at zero without a clock;
    /// this Standalone Default prevents a saved or live-tweaked Sync Setting from feeding a Standalone Roll.
    /// </summary>
    private const int StandaloneBeatModeMin = 0;

    /// <summary>
    /// Authored exclusive maximum beat-mode Roll bound for the unchanged Standalone look. This Roll
    /// is rendering-invisible in Standalone Mode because pulse history rests at zero without a clock;
    /// this Standalone Default prevents a saved or live-tweaked Sync Setting from feeding a Standalone Roll.
    /// </summary>
    private const int StandaloneBeatModeMaxExclusive = 2;

    /// <summary>
    /// Authored inclusive minimum pulse-direction Roll bound for the unchanged Standalone look. This
    /// Roll is rendering-invisible in Standalone Mode because pulse history rests at zero without a clock;
    /// this Standalone Default prevents a saved or live-tweaked Sync Setting from feeding a Standalone Roll.
    /// </summary>
    private const int StandalonePulseDirectionMin = 0;

    /// <summary>
    /// Authored exclusive maximum pulse-direction Roll bound for the unchanged Standalone look. This
    /// Roll is rendering-invisible in Standalone Mode because pulse history rests at zero without a clock;
    /// this Standalone Default prevents a saved or live-tweaked Sync Setting from feeding a Standalone Roll.
    /// </summary>
    private const int StandalonePulseDirectionMaxExclusive = 2;

    /// <summary>
    /// Authored minimum pulse-multiplier Roll bound for the unchanged Standalone look. This Roll is
    /// rendering-invisible in Standalone Mode because pulse history rests at zero without a clock;
    /// this Standalone Default prevents a saved or live-tweaked Sync Setting from feeding a Standalone Roll.
    /// </summary>
    private const float StandalonePulseMultiplierMin = 0.125f;

    /// <summary>
    /// Authored maximum pulse-multiplier Roll bound for the unchanged Standalone look. This Roll is
    /// rendering-invisible in Standalone Mode because pulse history rests at zero without a clock;
    /// this Standalone Default prevents a saved or live-tweaked Sync Setting from feeding a Standalone Roll.
    /// </summary>
    private const float StandalonePulseMultiplierMax = 0.25f;

    /// <summary>
    /// Authored Waveform trough height carried as an inert identity: Standalone Mode never samples
    /// the trough endpoint, and this Standalone Default exists so a saved or live-tweaked Sync
    /// Setting never feeds a Standalone-evaluated term.
    /// </summary>
    private const float StandaloneWaveformTroughHeight = 1f;

    /// <summary>
    /// Authored pulse-spacing divisor carried as an inert identity: the pulse history rests at zero
    /// in Standalone Mode, so the divisor cannot reach Standalone rendering; this Standalone Default
    /// exists so a saved or live-tweaked Sync Setting never feeds a Standalone-evaluated term.
    /// </summary>
    private const float StandalonePulseScaleDivisor = 200f;

    /// <summary>
    /// Authored saturation-response multiple carried as an inert identity: it multiplies the resting
    /// pulse history, so it cannot reach Standalone rendering; this Standalone Default exists so a
    /// saved or live-tweaked Sync Setting never feeds a Standalone-evaluated term.
    /// </summary>
    private const float StandaloneSaturationPulseMultiplier = 2f;

    /// <summary>Authored inclusive minimum number of drops rolled for the unchanged Standalone look.</summary>
    private const int StandaloneDropCountMin = 70;

    /// <summary>Authored exclusive maximum number of drops rolled for the unchanged Standalone look.</summary>
    private const int StandaloneDropCountMaxExclusive = 100;

    /// <summary>Authored minimum palette stretch for the unchanged Standalone background.</summary>
    private const float StandaloneBackgroundStretchMin = 0.001f;

    /// <summary>Authored maximum palette stretch for the unchanged Standalone background.</summary>
    private const float StandaloneBackgroundStretchMax = 0.025f;

    /// <summary>Authored minimum palette speed for the unchanged Standalone background.</summary>
    private const float StandaloneBackgroundSpeedMin = 0.01f;

    /// <summary>Authored maximum palette speed for the unchanged Standalone background.</summary>
    private const float StandaloneBackgroundSpeedMax = 0.3f;

    /// <summary>Authored inclusive minimum screen-height multiplier for Standalone drop spawns.</summary>
    private const int StandaloneDropSpawnHeightMinMultiplier = 1;

    /// <summary>Authored exclusive maximum screen-height multiplier for Standalone drop spawns.</summary>
    private const int StandaloneDropSpawnHeightMaxMultiplier = 10;

    /// <summary>Authored minimum radius for Standalone drop rolls.</summary>
    private const float StandaloneDropRadiusMin = 0.2f;

    /// <summary>Authored maximum radius for Standalone drop rolls.</summary>
    private const float StandaloneDropRadiusMax = 2f;

    /// <summary>Authored minimum falling speed for Standalone drop rolls.</summary>
    private const float StandaloneDropSpeedMin = 0.005f;

    /// <summary>Authored maximum falling speed for Standalone drop rolls.</summary>
    private const float StandaloneDropSpeedMax = 0.05f;

    /// <summary>Authored minimum palette intensity for Standalone drop rolls.</summary>
    private const float StandaloneDropIntensityMin = 0.1f;

    /// <summary>Authored maximum palette intensity for Standalone drop rolls.</summary>
    private const float StandaloneDropIntensityMax = 0.5f;

    /// <summary>Authored trail falloff distance for the unchanged Standalone look.</summary>
    private const float StandaloneDropTrailFalloff = 25f;

    /// <summary>Authored screen-height multiplier where Standalone drops respawn above the wall.</summary>
    private const int StandaloneDropRespawnHeightMultiplier = -15;

    // Sync Defaults

    /// <summary>
    /// Authored Waveform trough height for Waterfall in Synced Mode. The authored value makes the
    /// pulse full between rhythmic peaks.
    /// </summary>
    private const float SyncWaveformTroughHeight = 1f;

    /// <summary>
    /// Authored Waveform peak height for Waterfall in Synced Mode. The authored value returns the
    /// pulse to zero at each rhythmic peak.
    /// </summary>
    private const float SyncWaveformPeakHeight = 0f;

    /// <summary>Authored inclusive minimum hue/saturation/value response mode.</summary>
    private const int SyncBeatModeMin = 0;

    /// <summary>Authored exclusive maximum hue/saturation/value response mode.</summary>
    private const int SyncBeatModeMaxExclusive = 2;

    /// <summary>Authored inclusive minimum pulse direction.</summary>
    private const int SyncPulseDirectionMin = 0;

    /// <summary>Authored exclusive maximum pulse direction.</summary>
    private const int SyncPulseDirectionMaxExclusive = 2;

    /// <summary>Authored minimum color-response multiplier rolled in Synced Mode.</summary>
    private const float SyncPulseMultiplierMin = 0.125f;

    /// <summary>Authored maximum color-response multiplier rolled in Synced Mode.</summary>
    private const float SyncPulseMultiplierMax = 0.25f;

    /// <summary>Authored divisor mapping the Waveform's shortest peak spacing onto screen rows.</summary>
    private const float SyncPulseScaleDivisor = 200f;

    /// <summary>Authored saturation-response multiple applied by saturation mode.</summary>
    private const float SyncSaturationPulseMultiplier = 2f;

    /// <summary>Authored inclusive minimum number of drops rolled in Synced Mode.</summary>
    private const int SyncDropCountMin = 70;

    /// <summary>Authored exclusive maximum number of drops rolled in Synced Mode.</summary>
    private const int SyncDropCountMaxExclusive = 100;

    /// <summary>Authored minimum palette stretch for the Synced background.</summary>
    private const float SyncBackgroundStretchMin = 0.001f;

    /// <summary>Authored maximum palette stretch for the Synced background.</summary>
    private const float SyncBackgroundStretchMax = 0.025f;

    /// <summary>Authored minimum palette speed for the Synced background.</summary>
    private const float SyncBackgroundSpeedMin = 0.01f;

    /// <summary>Authored maximum palette speed for the Synced background.</summary>
    private const float SyncBackgroundSpeedMax = 0.3f;

    /// <summary>Authored inclusive minimum screen-height multiplier for Synced drop spawns.</summary>
    private const int SyncDropSpawnHeightMinMultiplier = 1;

    /// <summary>Authored exclusive maximum screen-height multiplier for Synced drop spawns.</summary>
    private const int SyncDropSpawnHeightMaxMultiplier = 10;

    /// <summary>Authored minimum radius for Synced drop rolls.</summary>
    private const float SyncDropRadiusMin = 0.2f;

    /// <summary>Authored maximum radius for Synced drop rolls.</summary>
    private const float SyncDropRadiusMax = 2f;

    /// <summary>Authored minimum falling speed for Synced drop rolls.</summary>
    private const float SyncDropSpeedMin = 0.005f;

    /// <summary>Authored maximum falling speed for Synced drop rolls.</summary>
    private const float SyncDropSpeedMax = 0.05f;

    /// <summary>Authored minimum palette intensity for Synced drop rolls.</summary>
    private const float SyncDropIntensityMin = 0.1f;

    /// <summary>Authored maximum palette intensity for Synced drop rolls.</summary>
    private const float SyncDropIntensityMax = 0.5f;

    /// <summary>Authored trail falloff distance in Synced Mode.</summary>
    private const float SyncDropTrailFalloff = 25f;

    /// <summary>Authored screen-height multiplier where Synced drops respawn above the wall.</summary>
    private const int SyncDropRespawnHeightMultiplier = -15;

    /// <summary>Waterfall's falling drops suit Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow |Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Waterfall's file-local Standalone Defaults.</summary>
    public static WaterfallStandaloneSettings StandaloneDefaults => new WaterfallStandaloneSettings
    {
        WaveformPeakHeight = StandaloneWaveformPeakHeight,
        BeatMode = new IntRange(StandaloneBeatModeMin, StandaloneBeatModeMaxExclusive),
        PulseDirection = new IntRange(
            StandalonePulseDirectionMin,
            StandalonePulseDirectionMaxExclusive),
        PulseMultiplier = new FloatRange(
            StandalonePulseMultiplierMin,
            StandalonePulseMultiplierMax),
        WaveformTroughHeight = StandaloneWaveformTroughHeight,
        PulseScaleDivisor = StandalonePulseScaleDivisor,
        SaturationPulseMultiplier = StandaloneSaturationPulseMultiplier,
        DropCount = new IntRange(StandaloneDropCountMin, StandaloneDropCountMaxExclusive),
        BackgroundStretch = new FloatRange(StandaloneBackgroundStretchMin, StandaloneBackgroundStretchMax),
        BackgroundSpeed = new FloatRange(StandaloneBackgroundSpeedMin, StandaloneBackgroundSpeedMax),
        DropSpawnHeightMultiplier = new IntRange(
            StandaloneDropSpawnHeightMinMultiplier,
            StandaloneDropSpawnHeightMaxMultiplier),
        DropRadius = new FloatRange(StandaloneDropRadiusMin, StandaloneDropRadiusMax),
        DropSpeed = new FloatRange(StandaloneDropSpeedMin, StandaloneDropSpeedMax),
        DropIntensity = new FloatRange(StandaloneDropIntensityMin, StandaloneDropIntensityMax),
        DropTrailFalloff = StandaloneDropTrailFalloff,
        DropRespawnHeightMultiplier = StandaloneDropRespawnHeightMultiplier,
    };

    /// <summary>Resolves a fresh copy of Waterfall's file-local Sync Defaults.</summary>
    public static WaterfallSyncSettings SyncDefaults => new WaterfallSyncSettings
    {
        WaveformTroughHeight = SyncWaveformTroughHeight,
        WaveformPeakHeight = SyncWaveformPeakHeight,
        BeatMode = new IntRange(SyncBeatModeMin, SyncBeatModeMaxExclusive),
        PulseDirection = new IntRange(SyncPulseDirectionMin, SyncPulseDirectionMaxExclusive),
        PulseMultiplier = new FloatRange(SyncPulseMultiplierMin, SyncPulseMultiplierMax),
        PulseScaleDivisor = SyncPulseScaleDivisor,
        SaturationPulseMultiplier = SyncSaturationPulseMultiplier,
        DropCount = new IntRange(SyncDropCountMin, SyncDropCountMaxExclusive),
        BackgroundStretch = new FloatRange(SyncBackgroundStretchMin, SyncBackgroundStretchMax),
        BackgroundSpeed = new FloatRange(SyncBackgroundSpeedMin, SyncBackgroundSpeedMax),
        DropSpawnHeightMultiplier = new IntRange(
            SyncDropSpawnHeightMinMultiplier,
            SyncDropSpawnHeightMaxMultiplier),
        DropRadius = new FloatRange(SyncDropRadiusMin, SyncDropRadiusMax),
        DropSpeed = new FloatRange(SyncDropSpeedMin, SyncDropSpeedMax),
        DropIntensity = new FloatRange(SyncDropIntensityMin, SyncDropIntensityMax),
        DropTrailFalloff = SyncDropTrailFalloff,
        DropRespawnHeightMultiplier = SyncDropRespawnHeightMultiplier,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private WaterfallStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private WaterfallSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The drops currently falling through screen space.</summary>
    private Drop[] drops;

    /// <summary>The number of drops rolled for the current activation.</summary>
    private int numDrops;

    /// <summary>The palette stretch rolled for the current background.</summary>
    private float backgrounStretch;

    /// <summary>The palette animation speed rolled for the current background.</summary>
    private float backgroundSpeed;

    /// <summary>The color-response multiplier rolled for the current activation.</summary>
    float pulseMultipler;

    /// <summary>The fixed-length pulse history propagated across screen rows.</summary>
    private float[] wave = new float[400];

    /// <summary>The shortest peak spacing sampled from the Waveform acquired during the current Roll.</summary>
    private float pulsePeakSpacingMs;

    /// <summary>The current mapping from screen rows into the pulse history.</summary>
    float pulseScale;

    /// <summary>The rolled hue, saturation, or value response mode.</summary>
    int beatMode;

    /// <summary>The rolled direction used to traverse the pulse history.</summary>
    int pulseDirection;

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    public override string DebugText()
    {
        return $"Drops: {numDrops}\n" +
            $"Background stretch: {backgrounStretch}\n" +
            $"Background speed: {backgroundSpeed}\n";
    }

    /// <summary>
    /// Called once when effect is created
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Waterfall),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Waterfall),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        IntRange beatModeRange = isSynced ? SyncSettings.BeatMode : standaloneSettings.BeatMode;
        IntRange pulseDirectionRange = isSynced
            ? SyncSettings.PulseDirection
            : standaloneSettings.PulseDirection;
        FloatRange pulseMultiplierRange = isSynced
            ? SyncSettings.PulseMultiplier
            : standaloneSettings.PulseMultiplier;

        waveform = waveforms.Random();
        beatMode = Random.Range(beatModeRange.MinInclusive, beatModeRange.MaxExclusive);
        pulseDirection = Random.Range(
            pulseDirectionRange.MinInclusive,
            pulseDirectionRange.MaxExclusive);
        pulseMultipler = Random.value * (pulseMultiplierRange.Max - pulseMultiplierRange.Min) +
            pulseMultiplierRange.Min;
        pulsePeakSpacingMs = waveform.ShortestPeakSpacingMs;
        wave = new float[400];      // clear array

        IntRange dropCountRange = isSynced ? SyncSettings.DropCount : standaloneSettings.DropCount;
        FloatRange backgroundStretchRange = isSynced
            ? SyncSettings.BackgroundStretch
            : standaloneSettings.BackgroundStretch;
        FloatRange backgroundSpeedRange = isSynced
            ? SyncSettings.BackgroundSpeed
            : standaloneSettings.BackgroundSpeed;

        numDrops = Random.Range(dropCountRange.MinInclusive, dropCountRange.MaxExclusive);
        backgrounStretch = Random.Range(backgroundStretchRange.Min, backgroundStretchRange.Max);
        backgroundSpeed = Random.Range(backgroundSpeedRange.Min, backgroundSpeedRange.Max);
        buffer.Clear();

        IntRange dropSpawnHeightMultiplierRange = isSynced
            ? SyncSettings.DropSpawnHeightMultiplier
            : standaloneSettings.DropSpawnHeightMultiplier;
        FloatRange dropRadiusRange = isSynced ? SyncSettings.DropRadius : standaloneSettings.DropRadius;
        FloatRange dropSpeedRange = isSynced ? SyncSettings.DropSpeed : standaloneSettings.DropSpeed;
        FloatRange dropIntensityRange = isSynced
            ? SyncSettings.DropIntensity
            : standaloneSettings.DropIntensity;

        drops = new Drop[numDrops];
        for (int i = 0; i < drops.Length; i++)
        {
            drops[i] = new Drop(
                dropSpawnHeightMultiplierRange,
                dropRadiusRange,
                dropSpeedRange,
                dropIntensityRange);
        }
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales the waterfall colors after droplet/background sampling.
        bool isSynced = beatManager.IsSynced;
        float waveformTroughHeight = isSynced
            ? SyncSettings.WaveformTroughHeight
            : standaloneSettings.WaveformTroughHeight;
        float waveformPeakHeight = isSynced
            ? SyncSettings.WaveformPeakHeight
            : standaloneSettings.WaveformPeakHeight;
        float pulseScaleDivisor = isSynced
            ? SyncSettings.PulseScaleDivisor
            : standaloneSettings.PulseScaleDivisor;
        float saturationPulseMultiplier = isSynced
            ? SyncSettings.SaturationPulseMultiplier
            : standaloneSettings.SaturationPulseMultiplier;
        IntRange dropSpawnHeightMultiplierRange = isSynced
            ? SyncSettings.DropSpawnHeightMultiplier
            : standaloneSettings.DropSpawnHeightMultiplier;
        float dropTrailFalloff = isSynced
            ? SyncSettings.DropTrailFalloff
            : standaloneSettings.DropTrailFalloff;
        int dropRespawnHeightMultiplier = isSynced
            ? SyncSettings.DropRespawnHeightMultiplier
            : standaloneSettings.DropRespawnHeightMultiplier;

        float waveHeight = waveform.Lerp(waveformTroughHeight, waveformPeakHeight);
        pulseScale = pulsePeakSpacingMs / pulseScaleDivisor;
        for (int i = wave.Length - 1; i > 0; i--)
            wave[i] = wave[i - 1];
        wave[0] = waveHeight;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float pulseY=(pulseDirection==0)?y:height-y;
                float waveidxf = pulseY * pulseScale;
                int waveidx = (int)waveidxf;
                if (waveidx > (wave.Length - 1))
                    waveidx = wave.Length - 1;

                var screen = new Vector2();
                screen.x = x;
                screen.y = y;
                // background
                var color = y * backgrounStretch + effectTime * backgroundSpeed;
                for (int i = 0; i < drops.Length; i++)
                {
                    Drop drop = drops[i];
                    drop.Update(
                        effectDelta,
                        dropSpawnHeightMultiplierRange,
                        dropRespawnHeightMultiplier);
                    var distance = Vector2.Distance(screen, drop.position);
                    //drop
                    if (distance < drop.radius)
                    {
                        color += drop.intensity;
                    }
                    //trail
                    else if (drop.position.y < screen.y && drop.position.x > screen.x - drop.radius && drop.position.x < screen.x + drop.radius)
                    {
                        color += dropTrailFalloff * drop.intensity / (dropTrailFalloff + (y - drop.position.y));
                    }
                }
                Color color2 = APalette.read(color % 1f, true);
                Color.RGBToHSV(color2, out float h, out float s, out float v);

                switch (beatMode)
                {
                    case 0:
                        h += wave[waveidx] * pulseMultipler;
                        break;
                    case 1:
                        s += wave[waveidx] * pulseMultipler * saturationPulseMultiplier;
                        break;
                    case 2:
                        v += wave[waveidx] * (1f - pulseMultipler);
                        break;
                }

                screenBuffer[x + (y * width)] = Color.HSVToRGB(h % 1f, s, v); ;
            }
        }
        // convert the 2D Matrix buffer to a tile buffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
    }

    /// <summary>
    /// Falling screen-space drop used by the Waterfall effect.
    /// </summary>
    public class Drop
    {
        /// <summary>The current screen-space position of this drop.</summary>
        public Vector2 position;

        /// <summary>The rolled radius of this drop.</summary>
        public float radius;

        /// <summary>The rolled falling speed of this drop.</summary>
        public float speed;

        /// <summary>The rolled palette intensity contributed by this drop.</summary>
        public float intensity;

        /// <summary>
        /// Creates a falling drop with random position, speed, and size.
        /// </summary>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for the position roll.</param>
        /// <param name="radiusRange">Endpoints supplied to the radius roll.</param>
        /// <param name="speedRange">Endpoints supplied to the speed roll.</param>
        /// <param name="intensityRange">Endpoints supplied to the intensity roll.</param>
        public Drop(
            IntRange spawnHeightMultiplierRange,
            FloatRange radiusRange,
            FloatRange speedRange,
            FloatRange intensityRange)
        {
            position = new Vector2(
                Random.Range(0, width),
                Random.Range(
                    height * spawnHeightMultiplierRange.MinInclusive,
                    height * spawnHeightMultiplierRange.MaxExclusive));
            radius = Random.Range(radiusRange.Min, radiusRange.Max);
            speed = Random.Range(speedRange.Min, speedRange.Max);
            intensity = Random.Range(intensityRange.Min, intensityRange.Max);
        }

        /// <summary>
        /// Moves the drop downward and respawns it above the screen after it exits.
        /// </summary>
        /// <param name="deltaTime">Frame delta applied to this update call.</param>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for a respawn roll.</param>
        /// <param name="respawnHeightMultiplier">Screen-height multiplier that triggers a respawn.</param>
        public void Update(
            float deltaTime,
            IntRange spawnHeightMultiplierRange,
            int respawnHeightMultiplier)
        {
            var velocity = new Vector2();
            velocity.x = 0f;
            velocity.y = -speed;
            position += deltaTime * velocity;
            if (position.y < height * respawnHeightMultiplier) position = new Vector2(
                Random.Range(0, width),
                Random.Range(
                    height * spawnHeightMultiplierRange.MinInclusive,
                    height * spawnHeightMultiplierRange.MaxExclusive));
        }
    }
}

/// <summary>
/// Editable no-music values saved as Waterfall's Standalone Settings and restored from its authored
/// Standalone Defaults.
/// </summary>
[Serializable]
public sealed class WaterfallStandaloneSettings
{
    /// <summary>
    /// Waveform peak endpoint and no-clock fallback; zero keeps the pulse at rest at each peak and in
    /// Standalone Mode.
    /// </summary>
    [Range(0f, 1f)]
    public float WaveformPeakHeight;

    /// <summary>
    /// Inclusive-minimum/exclusive-maximum beat-mode Roll range fixed by the Standalone Defaults.
    /// </summary>
    public IntRange BeatMode;

    /// <summary>
    /// Inclusive-minimum/exclusive-maximum pulse-direction Roll range fixed by the Standalone Defaults.
    /// </summary>
    public IntRange PulseDirection;

    /// <summary>Pulse-multiplier Roll range fixed by the Standalone Defaults.</summary>
    public FloatRange PulseMultiplier;

    /// <summary>Waveform trough endpoint carried as an inert identity: Standalone Mode never samples it.</summary>
    [Range(0f, 1f)]
    public float WaveformTroughHeight;

    /// <summary>Pulse-spacing divisor carried as an inert identity: the pulse history rests at zero in Standalone Mode.</summary>
    [Min(0.0001f)]
    public float PulseScaleDivisor;

    /// <summary>Saturation-response multiple carried as an inert identity: it multiplies the resting pulse history.</summary>
    [Min(0f)]
    public float SaturationPulseMultiplier;

    /// <summary>Inclusive-minimum/exclusive-maximum number of drops rolled per activation.</summary>
    public IntRange DropCount;

    /// <summary>Per-activation background palette-stretch range.</summary>
    public FloatRange BackgroundStretch;

    /// <summary>Per-activation background palette-speed range.</summary>
    public FloatRange BackgroundSpeed;

    /// <summary>Inclusive-lower/exclusive-upper screen-height multiplier range for drop spawns.</summary>
    public IntRange DropSpawnHeightMultiplier;

    /// <summary>Per-drop radius range.</summary>
    public FloatRange DropRadius;

    /// <summary>Per-drop falling-speed range.</summary>
    public FloatRange DropSpeed;

    /// <summary>Per-drop palette-intensity range.</summary>
    public FloatRange DropIntensity;

    /// <summary>Distance controlling the brightness falloff along each drop trail.</summary>
    [Min(0.0001f)]
    public float DropTrailFalloff;

    /// <summary>Screen-height multiplier below the wall where a drop respawns.</summary>
    public int DropRespawnHeightMultiplier;

    /// <summary>Copies every Waterfall Standalone Setting from another value.</summary>
    public void CopyFrom(WaterfallStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        WaveformPeakHeight = source.WaveformPeakHeight;
        BeatMode = new IntRange(
            source.BeatMode.MinInclusive,
            source.BeatMode.MaxExclusive,
            source.BeatMode.LowRail,
            source.BeatMode.HighRail);
        PulseDirection = new IntRange(
            source.PulseDirection.MinInclusive,
            source.PulseDirection.MaxExclusive,
            source.PulseDirection.LowRail,
            source.PulseDirection.HighRail);
        PulseMultiplier = new FloatRange(
            source.PulseMultiplier.Min,
            source.PulseMultiplier.Max,
            source.PulseMultiplier.LowRail,
            source.PulseMultiplier.HighRail);
        WaveformTroughHeight = source.WaveformTroughHeight;
        PulseScaleDivisor = source.PulseScaleDivisor;
        SaturationPulseMultiplier = source.SaturationPulseMultiplier;
        DropCount = new IntRange(
            source.DropCount.MinInclusive,
            source.DropCount.MaxExclusive,
            source.DropCount.LowRail,
            source.DropCount.HighRail);
        BackgroundStretch = new FloatRange(
            source.BackgroundStretch.Min,
            source.BackgroundStretch.Max,
            source.BackgroundStretch.LowRail,
            source.BackgroundStretch.HighRail);
        BackgroundSpeed = new FloatRange(
            source.BackgroundSpeed.Min,
            source.BackgroundSpeed.Max,
            source.BackgroundSpeed.LowRail,
            source.BackgroundSpeed.HighRail);
        DropSpawnHeightMultiplier = new IntRange(
            source.DropSpawnHeightMultiplier.MinInclusive,
            source.DropSpawnHeightMultiplier.MaxExclusive,
            source.DropSpawnHeightMultiplier.LowRail,
            source.DropSpawnHeightMultiplier.HighRail);
        DropRadius = new FloatRange(
            source.DropRadius.Min,
            source.DropRadius.Max,
            source.DropRadius.LowRail,
            source.DropRadius.HighRail);
        DropSpeed = new FloatRange(
            source.DropSpeed.Min,
            source.DropSpeed.Max,
            source.DropSpeed.LowRail,
            source.DropSpeed.HighRail);
        DropIntensity = new FloatRange(
            source.DropIntensity.Min,
            source.DropIntensity.Max,
            source.DropIntensity.LowRail,
            source.DropIntensity.HighRail);
        DropTrailFalloff = source.DropTrailFalloff;
        DropRespawnHeightMultiplier = source.DropRespawnHeightMultiplier;
    }
}

/// <summary>Editable music-response and Synced Mode values saved as Waterfall's Sync Settings.</summary>
[System.Serializable]
public sealed class WaterfallSyncSettings
{
    /// <summary>
    /// Waveform trough endpoint; the authored value makes the pulse full between rhythmic peaks.
    /// </summary>
    [Range(0f, 1f)] public float WaveformTroughHeight;

    /// <summary>
    /// Waveform peak endpoint whose authored counterpart is also the Standalone fallback; the authored
    /// value returns the pulse to zero at each rhythmic peak and in Standalone Mode.
    /// </summary>
    [Range(0f, 1f)] public float WaveformPeakHeight;

    /// <summary>Inclusive-minimum/exclusive-maximum hue/saturation/value response-mode range.</summary>
    public IntRange BeatMode;

    /// <summary>Inclusive-minimum/exclusive-maximum pulse-direction range.</summary>
    public IntRange PulseDirection;

    /// <summary>Color-response multiplier range rolled per activation.</summary>
    public FloatRange PulseMultiplier;

    /// <summary>Divisor mapping the Waveform's shortest peak spacing onto screen rows.</summary>
    [Min(0.0001f)] public float PulseScaleDivisor;

    /// <summary>Additional scale applied when the pulse changes saturation.</summary>
    [Min(0f)] public float SaturationPulseMultiplier;

    /// <summary>Inclusive-minimum/exclusive-maximum number of drops rolled per activation.</summary>
    public IntRange DropCount;

    /// <summary>Background palette-stretch range rolled per activation.</summary>
    public FloatRange BackgroundStretch;

    /// <summary>Background palette-speed range rolled per activation.</summary>
    public FloatRange BackgroundSpeed;

    /// <summary>Inclusive-lower/exclusive-upper screen-height multiplier range for drop spawns.</summary>
    public IntRange DropSpawnHeightMultiplier;

    /// <summary>Radius range rolled for each drop.</summary>
    public FloatRange DropRadius;

    /// <summary>Falling-speed range rolled for each drop.</summary>
    public FloatRange DropSpeed;

    /// <summary>Palette-intensity range rolled for each drop.</summary>
    public FloatRange DropIntensity;

    /// <summary>Distance controlling the brightness falloff along each drop trail.</summary>
    [Min(0.0001f)] public float DropTrailFalloff;

    /// <summary>Screen-height multiplier below the wall where a drop respawns.</summary>
    public int DropRespawnHeightMultiplier;

    /// <summary>Copies every Waterfall Sync Setting from another value.</summary>
    public void CopyFrom(WaterfallSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        WaveformTroughHeight = source.WaveformTroughHeight;
        WaveformPeakHeight = source.WaveformPeakHeight;
        BeatMode = new IntRange(
            source.BeatMode.MinInclusive,
            source.BeatMode.MaxExclusive,
            source.BeatMode.LowRail,
            source.BeatMode.HighRail);
        PulseDirection = new IntRange(
            source.PulseDirection.MinInclusive,
            source.PulseDirection.MaxExclusive,
            source.PulseDirection.LowRail,
            source.PulseDirection.HighRail);
        PulseMultiplier = new FloatRange(
            source.PulseMultiplier.Min,
            source.PulseMultiplier.Max,
            source.PulseMultiplier.LowRail,
            source.PulseMultiplier.HighRail);
        PulseScaleDivisor = source.PulseScaleDivisor;
        SaturationPulseMultiplier = source.SaturationPulseMultiplier;
        DropCount = new IntRange(
            source.DropCount.MinInclusive,
            source.DropCount.MaxExclusive,
            source.DropCount.LowRail,
            source.DropCount.HighRail);
        BackgroundStretch = new FloatRange(
            source.BackgroundStretch.Min,
            source.BackgroundStretch.Max,
            source.BackgroundStretch.LowRail,
            source.BackgroundStretch.HighRail);
        BackgroundSpeed = new FloatRange(
            source.BackgroundSpeed.Min,
            source.BackgroundSpeed.Max,
            source.BackgroundSpeed.LowRail,
            source.BackgroundSpeed.HighRail);
        DropSpawnHeightMultiplier = new IntRange(
            source.DropSpawnHeightMultiplier.MinInclusive,
            source.DropSpawnHeightMultiplier.MaxExclusive,
            source.DropSpawnHeightMultiplier.LowRail,
            source.DropSpawnHeightMultiplier.HighRail);
        DropRadius = new FloatRange(
            source.DropRadius.Min,
            source.DropRadius.Max,
            source.DropRadius.LowRail,
            source.DropRadius.HighRail);
        DropSpeed = new FloatRange(
            source.DropSpeed.Min,
            source.DropSpeed.Max,
            source.DropSpeed.LowRail,
            source.DropSpeed.HighRail);
        DropIntensity = new FloatRange(
            source.DropIntensity.Min,
            source.DropIntensity.Max,
            source.DropIntensity.LowRail,
            source.DropIntensity.HighRail);
        DropTrailFalloff = source.DropTrailFalloff;
        DropRespawnHeightMultiplier = source.DropRespawnHeightMultiplier;
    }
}
