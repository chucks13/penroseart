using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders falling screen-space droplets over an animated palette background.
/// </summary>
[EffectSyncSettings(typeof(WaterfallSyncSettingsAsset))]
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

    /// <summary>Resolves a fresh immutable-by-convention copy of Waterfall's Standalone Defaults.</summary>
    public static WaterfallStandaloneSettings StandaloneSettings => new WaterfallStandaloneSettings
    {
        WaveformPeakHeight = StandaloneWaveformPeakHeight,
        BeatModeMin = StandaloneBeatModeMin,
        BeatModeMaxExclusive = StandaloneBeatModeMaxExclusive,
        PulseDirectionMin = StandalonePulseDirectionMin,
        PulseDirectionMaxExclusive = StandalonePulseDirectionMaxExclusive,
        PulseMultiplierMin = StandalonePulseMultiplierMin,
        PulseMultiplierMax = StandalonePulseMultiplierMax,
        WaveformTroughHeight = StandaloneWaveformTroughHeight,
        PulseScaleDivisor = StandalonePulseScaleDivisor,
        SaturationPulseMultiplier = StandaloneSaturationPulseMultiplier,
        DropCountMin = StandaloneDropCountMin,
        DropCountMaxExclusive = StandaloneDropCountMaxExclusive,
        BackgroundStretch = new FloatRange(StandaloneBackgroundStretchMin, StandaloneBackgroundStretchMax),
        BackgroundSpeed = new FloatRange(StandaloneBackgroundSpeedMin, StandaloneBackgroundSpeedMax),
        DropSpawnHeightMinMultiplier = StandaloneDropSpawnHeightMinMultiplier,
        DropSpawnHeightMaxMultiplier = StandaloneDropSpawnHeightMaxMultiplier,
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
        BeatModeMin = SyncBeatModeMin,
        BeatModeMaxExclusive = SyncBeatModeMaxExclusive,
        PulseDirectionMin = SyncPulseDirectionMin,
        PulseDirectionMaxExclusive = SyncPulseDirectionMaxExclusive,
        PulseMultiplierMin = SyncPulseMultiplierMin,
        PulseMultiplierMax = SyncPulseMultiplierMax,
        PulseScaleDivisor = SyncPulseScaleDivisor,
        SaturationPulseMultiplier = SyncSaturationPulseMultiplier,
        DropCountMin = SyncDropCountMin,
        DropCountMaxExclusive = SyncDropCountMaxExclusive,
        BackgroundStretchMin = SyncBackgroundStretchMin,
        BackgroundStretchMax = SyncBackgroundStretchMax,
        BackgroundSpeedMin = SyncBackgroundSpeedMin,
        BackgroundSpeedMax = SyncBackgroundSpeedMax,
        DropSpawnHeightMinMultiplier = SyncDropSpawnHeightMinMultiplier,
        DropSpawnHeightMaxMultiplier = SyncDropSpawnHeightMaxMultiplier,
        DropRadiusMin = SyncDropRadiusMin,
        DropRadiusMax = SyncDropRadiusMax,
        DropSpeedMin = SyncDropSpeedMin,
        DropSpeedMax = SyncDropSpeedMax,
        DropIntensityMin = SyncDropIntensityMin,
        DropIntensityMax = SyncDropIntensityMax,
        DropTrailFalloff = SyncDropTrailFalloff,
        DropRespawnHeightMultiplier = SyncDropRespawnHeightMultiplier,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private WaterfallStandaloneSettings standaloneSettings = StandaloneSettings;

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
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Waterfall),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        int beatModeMin = isSynced ? SyncSettings.BeatModeMin : standaloneSettings.BeatModeMin;
        int beatModeMaxExclusive = isSynced
            ? SyncSettings.BeatModeMaxExclusive
            : standaloneSettings.BeatModeMaxExclusive;
        int pulseDirectionMin = isSynced
            ? SyncSettings.PulseDirectionMin
            : standaloneSettings.PulseDirectionMin;
        int pulseDirectionMaxExclusive = isSynced
            ? SyncSettings.PulseDirectionMaxExclusive
            : standaloneSettings.PulseDirectionMaxExclusive;
        float pulseMultiplierMin = isSynced
            ? SyncSettings.PulseMultiplierMin
            : standaloneSettings.PulseMultiplierMin;
        float pulseMultiplierMax = isSynced
            ? SyncSettings.PulseMultiplierMax
            : standaloneSettings.PulseMultiplierMax;

        waveform = waveforms.Random();
        beatMode = Random.Range(beatModeMin, beatModeMaxExclusive);
        pulseDirection =Random.Range(pulseDirectionMin, pulseDirectionMaxExclusive);
        pulseMultipler = Random.value * (pulseMultiplierMax - pulseMultiplierMin) + pulseMultiplierMin;
        pulsePeakSpacingMs = waveform.ShortestPeakSpacingMs;
        wave = new float[400];      // clear array

        int dropCountMin = isSynced ? SyncSettings.DropCountMin : standaloneSettings.DropCountMin;
        int dropCountMaxExclusive = isSynced
            ? SyncSettings.DropCountMaxExclusive
            : standaloneSettings.DropCountMaxExclusive;
        float backgroundStretchMin = isSynced
            ? SyncSettings.BackgroundStretchMin
            : standaloneSettings.BackgroundStretch.Min;
        float backgroundStretchMax = isSynced
            ? SyncSettings.BackgroundStretchMax
            : standaloneSettings.BackgroundStretch.Max;
        float backgroundSpeedMin = isSynced
            ? SyncSettings.BackgroundSpeedMin
            : standaloneSettings.BackgroundSpeed.Min;
        float backgroundSpeedMax = isSynced
            ? SyncSettings.BackgroundSpeedMax
            : standaloneSettings.BackgroundSpeed.Max;

        numDrops = Random.Range(dropCountMin, dropCountMaxExclusive);
        backgrounStretch = Random.Range(backgroundStretchMin, backgroundStretchMax);
        backgroundSpeed = Random.Range(backgroundSpeedMin, backgroundSpeedMax);
        buffer.Clear();

        int dropSpawnHeightMinMultiplier = isSynced
            ? SyncSettings.DropSpawnHeightMinMultiplier
            : standaloneSettings.DropSpawnHeightMinMultiplier;
        int dropSpawnHeightMaxMultiplier = isSynced
            ? SyncSettings.DropSpawnHeightMaxMultiplier
            : standaloneSettings.DropSpawnHeightMaxMultiplier;
        float dropRadiusMin = isSynced ? SyncSettings.DropRadiusMin : standaloneSettings.DropRadius.Min;
        float dropRadiusMax = isSynced ? SyncSettings.DropRadiusMax : standaloneSettings.DropRadius.Max;
        float dropSpeedMin = isSynced ? SyncSettings.DropSpeedMin : standaloneSettings.DropSpeed.Min;
        float dropSpeedMax = isSynced ? SyncSettings.DropSpeedMax : standaloneSettings.DropSpeed.Max;
        float dropIntensityMin = isSynced ? SyncSettings.DropIntensityMin : standaloneSettings.DropIntensity.Min;
        float dropIntensityMax = isSynced ? SyncSettings.DropIntensityMax : standaloneSettings.DropIntensity.Max;

        drops = new Drop[numDrops];
        for (int i = 0; i < drops.Length; i++)
        {
            drops[i] = new Drop(
                dropSpawnHeightMinMultiplier,
                dropSpawnHeightMaxMultiplier,
                dropRadiusMin,
                dropRadiusMax,
                dropSpeedMin,
                dropSpeedMax,
                dropIntensityMin,
                dropIntensityMax);
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
        int dropSpawnHeightMinMultiplier = isSynced
            ? SyncSettings.DropSpawnHeightMinMultiplier
            : standaloneSettings.DropSpawnHeightMinMultiplier;
        int dropSpawnHeightMaxMultiplier = isSynced
            ? SyncSettings.DropSpawnHeightMaxMultiplier
            : standaloneSettings.DropSpawnHeightMaxMultiplier;
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
                        dropSpawnHeightMinMultiplier,
                        dropSpawnHeightMaxMultiplier,
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
        /// <param name="spawnHeightMinMultiplier">Inclusive lower screen-height multiplier for the position roll.</param>
        /// <param name="spawnHeightMaxMultiplier">Exclusive upper screen-height multiplier for the position roll.</param>
        /// <param name="radiusMin">Minimum radius supplied to the radius roll.</param>
        /// <param name="radiusMax">Maximum radius supplied to the radius roll.</param>
        /// <param name="speedMin">Minimum speed supplied to the speed roll.</param>
        /// <param name="speedMax">Maximum speed supplied to the speed roll.</param>
        /// <param name="intensityMin">Minimum intensity supplied to the intensity roll.</param>
        /// <param name="intensityMax">Maximum intensity supplied to the intensity roll.</param>
        public Drop(
            int spawnHeightMinMultiplier,
            int spawnHeightMaxMultiplier,
            float radiusMin,
            float radiusMax,
            float speedMin,
            float speedMax,
            float intensityMin,
            float intensityMax)
        {
            position = new Vector2(
                Random.Range(0, width),
                Random.Range(height * spawnHeightMinMultiplier, height * spawnHeightMaxMultiplier));
            radius = Random.Range(radiusMin, radiusMax);
            speed = Random.Range(speedMin, speedMax);
            intensity = Random.Range(intensityMin, intensityMax);
        }

        /// <summary>
        /// Moves the drop downward and respawns it above the screen after it exits.
        /// </summary>
        /// <param name="deltaTime">Frame delta applied to this update call.</param>
        /// <param name="spawnHeightMinMultiplier">Inclusive lower screen-height multiplier for a respawn roll.</param>
        /// <param name="spawnHeightMaxMultiplier">Exclusive upper screen-height multiplier for a respawn roll.</param>
        /// <param name="respawnHeightMultiplier">Screen-height multiplier that triggers a respawn.</param>
        public void Update(
            float deltaTime,
            int spawnHeightMinMultiplier,
            int spawnHeightMaxMultiplier,
            int respawnHeightMultiplier)
        {
            var velocity = new Vector2();
            velocity.x = 0f;
            velocity.y = -speed;
            position += deltaTime * velocity;
            if (position.y < height * respawnHeightMultiplier) position = new Vector2(
                Random.Range(0, width),
                Random.Range(height * spawnHeightMinMultiplier, height * spawnHeightMaxMultiplier));
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce Waterfall's authored no-music look.</summary>
public sealed class WaterfallStandaloneSettings
{
    /// <summary>
    /// Waveform peak endpoint and no-clock fallback; zero keeps the pulse at rest at each peak and in
    /// Standalone Mode.
    /// </summary>
    public float WaveformPeakHeight;

    /// <summary>Inclusive minimum beat-mode Roll bound fixed by the Standalone Defaults.</summary>
    public int BeatModeMin;

    /// <summary>Exclusive maximum beat-mode Roll bound fixed by the Standalone Defaults.</summary>
    public int BeatModeMaxExclusive;

    /// <summary>Inclusive minimum pulse-direction Roll bound fixed by the Standalone Defaults.</summary>
    public int PulseDirectionMin;

    /// <summary>Exclusive maximum pulse-direction Roll bound fixed by the Standalone Defaults.</summary>
    public int PulseDirectionMaxExclusive;

    /// <summary>Minimum pulse-multiplier Roll bound fixed by the Standalone Defaults.</summary>
    public float PulseMultiplierMin;

    /// <summary>Maximum pulse-multiplier Roll bound fixed by the Standalone Defaults.</summary>
    public float PulseMultiplierMax;

    /// <summary>Waveform trough endpoint carried as an inert identity: Standalone Mode never samples it.</summary>
    public float WaveformTroughHeight;

    /// <summary>Pulse-spacing divisor carried as an inert identity: the pulse history rests at zero in Standalone Mode.</summary>
    public float PulseScaleDivisor;

    /// <summary>Saturation-response multiple carried as an inert identity: it multiplies the resting pulse history.</summary>
    public float SaturationPulseMultiplier;

    /// <summary>Inclusive minimum number of drops rolled per activation.</summary>
    public int DropCountMin;

    /// <summary>Exclusive maximum number of drops rolled per activation.</summary>
    public int DropCountMaxExclusive;

    /// <summary>Per-activation background palette-stretch range.</summary>
    public FloatRange BackgroundStretch;

    /// <summary>Per-activation background palette-speed range.</summary>
    public FloatRange BackgroundSpeed;

    /// <summary>Inclusive lower screen-height multiplier for drop spawns.</summary>
    public int DropSpawnHeightMinMultiplier;

    /// <summary>Exclusive upper screen-height multiplier for drop spawns.</summary>
    public int DropSpawnHeightMaxMultiplier;

    /// <summary>Per-drop radius range.</summary>
    public FloatRange DropRadius;

    /// <summary>Per-drop falling-speed range.</summary>
    public FloatRange DropSpeed;

    /// <summary>Per-drop palette-intensity range.</summary>
    public FloatRange DropIntensity;

    /// <summary>Distance controlling the brightness falloff along each drop trail.</summary>
    public float DropTrailFalloff;

    /// <summary>Screen-height multiplier below the wall where a drop respawns.</summary>
    public int DropRespawnHeightMultiplier;
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

    /// <summary>Inclusive minimum hue/saturation/value response mode.</summary>
    [Range(0, 2)] public int BeatModeMin;

    /// <summary>Exclusive maximum hue/saturation/value response mode.</summary>
    [Range(1, 3)] public int BeatModeMaxExclusive;

    /// <summary>Inclusive minimum pulse direction.</summary>
    [Range(0, 1)] public int PulseDirectionMin;

    /// <summary>Exclusive maximum pulse direction.</summary>
    [Range(1, 2)] public int PulseDirectionMaxExclusive;

    /// <summary>Minimum color-response multiplier rolled per activation.</summary>
    [Range(0f, 1f)] public float PulseMultiplierMin;

    /// <summary>Maximum color-response multiplier rolled per activation.</summary>
    [Range(0f, 1f)] public float PulseMultiplierMax;

    /// <summary>Divisor mapping the Waveform's shortest peak spacing onto screen rows.</summary>
    [Min(0.0001f)] public float PulseScaleDivisor;

    /// <summary>Additional scale applied when the pulse changes saturation.</summary>
    [Min(0f)] public float SaturationPulseMultiplier;

    /// <summary>Inclusive minimum number of drops rolled per activation.</summary>
    [Min(0)] public int DropCountMin;

    /// <summary>Exclusive maximum number of drops rolled per activation.</summary>
    [Min(1)] public int DropCountMaxExclusive;

    /// <summary>Minimum background palette stretch rolled per activation.</summary>
    [Min(0f)] public float BackgroundStretchMin;

    /// <summary>Maximum background palette stretch rolled per activation.</summary>
    [Min(0f)] public float BackgroundStretchMax;

    /// <summary>Minimum background palette speed rolled per activation.</summary>
    [Min(0f)] public float BackgroundSpeedMin;

    /// <summary>Maximum background palette speed rolled per activation.</summary>
    [Min(0f)] public float BackgroundSpeedMax;

    /// <summary>Inclusive lower screen-height multiplier for drop spawns.</summary>
    [Min(0)] public int DropSpawnHeightMinMultiplier;

    /// <summary>Exclusive upper screen-height multiplier for drop spawns.</summary>
    [Min(1)] public int DropSpawnHeightMaxMultiplier;

    /// <summary>Minimum radius rolled for each drop.</summary>
    [Min(0f)] public float DropRadiusMin;

    /// <summary>Maximum radius rolled for each drop.</summary>
    [Min(0f)] public float DropRadiusMax;

    /// <summary>Minimum falling speed rolled for each drop.</summary>
    [Min(0f)] public float DropSpeedMin;

    /// <summary>Maximum falling speed rolled for each drop.</summary>
    [Min(0f)] public float DropSpeedMax;

    /// <summary>Minimum palette intensity rolled for each drop.</summary>
    [Min(0f)] public float DropIntensityMin;

    /// <summary>Maximum palette intensity rolled for each drop.</summary>
    [Min(0f)] public float DropIntensityMax;

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
        BeatModeMin = source.BeatModeMin;
        BeatModeMaxExclusive = source.BeatModeMaxExclusive;
        PulseDirectionMin = source.PulseDirectionMin;
        PulseDirectionMaxExclusive = source.PulseDirectionMaxExclusive;
        PulseMultiplierMin = source.PulseMultiplierMin;
        PulseMultiplierMax = source.PulseMultiplierMax;
        PulseScaleDivisor = source.PulseScaleDivisor;
        SaturationPulseMultiplier = source.SaturationPulseMultiplier;
        DropCountMin = source.DropCountMin;
        DropCountMaxExclusive = source.DropCountMaxExclusive;
        BackgroundStretchMin = source.BackgroundStretchMin;
        BackgroundStretchMax = source.BackgroundStretchMax;
        BackgroundSpeedMin = source.BackgroundSpeedMin;
        BackgroundSpeedMax = source.BackgroundSpeedMax;
        DropSpawnHeightMinMultiplier = source.DropSpawnHeightMinMultiplier;
        DropSpawnHeightMaxMultiplier = source.DropSpawnHeightMaxMultiplier;
        DropRadiusMin = source.DropRadiusMin;
        DropRadiusMax = source.DropRadiusMax;
        DropSpeedMin = source.DropSpeedMin;
        DropSpeedMax = source.DropSpeedMax;
        DropIntensityMin = source.DropIntensityMin;
        DropIntensityMax = source.DropIntensityMax;
        DropTrailFalloff = source.DropTrailFalloff;
        DropRespawnHeightMultiplier = source.DropRespawnHeightMultiplier;
    }
}
