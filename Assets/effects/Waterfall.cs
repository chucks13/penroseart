using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders full-width falling streams and soft droplets in value space while the animated palette
/// supplies their hue.
/// </summary>
[EffectSyncSettings(typeof(WaterfallSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(WaterfallStandaloneSettingsAsset))]
public class Waterfall : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum number of droplets rolled for the Standalone water.</summary>
    private const int StandaloneDropletCountMin = 32;

    /// <summary>Authored exclusive maximum number of droplets rolled for the Standalone water.</summary>
    private const int StandaloneDropletCountMaxExclusive = 49;

    /// <summary>Authored minimum palette spread down the Standalone water's rows.</summary>
    private const float StandalonePaletteSpreadMin = 0.004f;

    /// <summary>Authored maximum palette spread down the Standalone water's rows.</summary>
    private const float StandalonePaletteSpreadMax = 0.02f;

    /// <summary>Authored minimum palette-wash speed for the Standalone water.</summary>
    private const float StandalonePaletteSpeedMin = 0.01f;

    /// <summary>Authored maximum palette-wash speed for the Standalone water.</summary>
    private const float StandalonePaletteSpeedMax = 0.08f;

    /// <summary>Authored minimum width of the overlapping Standalone streams, in screen pixels.</summary>
    private const float StandaloneStreamWidthMin = 4.5f;

    /// <summary>Authored maximum width of the overlapping Standalone streams, in screen pixels.</summary>
    private const float StandaloneStreamWidthMax = 9f;

    /// <summary>Authored minimum downward travel speed of the Standalone streams, in screen pixels per second.</summary>
    private const float StandaloneStreamFallSpeedMin = 5f;

    /// <summary>Authored maximum downward travel speed of the Standalone streams, in screen pixels per second.</summary>
    private const float StandaloneStreamFallSpeedMax = 14f;

    /// <summary>Authored maximum value of the Standalone water body before droplets brighten it.</summary>
    private const float StandaloneWaterBrightness = 0.72f;

    /// <summary>Authored luminance separation between bright and dark Standalone streams.</summary>
    private const float StandaloneStreamContrast = 0.7f;

    /// <summary>
    /// Authored strength of the dark seams carved where the Standalone stream field changes
    /// fastest — the same darkness-outlines-shape device as MazeFlyer's edge lines.
    /// </summary>
    private const float StandaloneStreamEdgeShade = 0.35f;

    /// <summary>Authored floor under the Standalone water's value so no roll goes pit-dark.</summary>
    private const float StandaloneWaterMinBrightness = 0.15f;

    /// <summary>Authored inclusive minimum screen-height multiplier for Standalone droplet spawns.</summary>
    private const int StandaloneDropletSpawnHeightMinMultiplier = 1;

    /// <summary>Authored exclusive maximum screen-height multiplier for Standalone droplet spawns.</summary>
    private const int StandaloneDropletSpawnHeightMaxMultiplier = 5;

    /// <summary>Authored minimum radius for Standalone droplet rolls.</summary>
    private const float StandaloneDropletRadiusMin = 1.25f;

    /// <summary>Authored maximum radius for Standalone droplet rolls.</summary>
    private const float StandaloneDropletRadiusMax = 2.75f;

    /// <summary>Authored minimum falling speed for Standalone droplet rolls, in screen pixels per second.</summary>
    private const float StandaloneDropletSpeedMin = 8f;

    /// <summary>Authored maximum falling speed for Standalone droplet rolls, in screen pixels per second.</summary>
    private const float StandaloneDropletSpeedMax = 24f;

    /// <summary>Authored minimum value-space brightness for Standalone droplet rolls.</summary>
    private const float StandaloneDropletBrightnessMin = 0.35f;

    /// <summary>Authored maximum value-space brightness for Standalone droplet rolls.</summary>
    private const float StandaloneDropletBrightnessMax = 0.75f;

    /// <summary>Authored distance over which each Standalone droplet trail tapers to darkness.</summary>
    private const float StandaloneDropletTrailLength = 14f;

    /// <summary>Authored fraction of a Standalone droplet's brightness at the head of its trail.</summary>
    private const float StandaloneDropletTrailBrightness = 0.55f;

    /// <summary>Authored screen-height multiplier below the wall where Standalone droplets respawn.</summary>
    private const int StandaloneDropletRespawnHeightMultiplier = -2;

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

    /// <summary>Authored inclusive minimum number of droplets rolled in Synced Mode.</summary>
    private const int SyncDropletCountMin = 32;

    /// <summary>Authored exclusive maximum number of droplets rolled in Synced Mode.</summary>
    private const int SyncDropletCountMaxExclusive = 49;

    /// <summary>Authored minimum palette spread down the Synced water's rows.</summary>
    private const float SyncPaletteSpreadMin = 0.004f;

    /// <summary>Authored maximum palette spread down the Synced water's rows.</summary>
    private const float SyncPaletteSpreadMax = 0.02f;

    /// <summary>Authored minimum palette-wash speed in Synced Mode.</summary>
    private const float SyncPaletteSpeedMin = 0.01f;

    /// <summary>Authored maximum palette-wash speed in Synced Mode.</summary>
    private const float SyncPaletteSpeedMax = 0.08f;

    /// <summary>Authored minimum width of the overlapping Synced streams, in screen pixels.</summary>
    private const float SyncStreamWidthMin = 4.5f;

    /// <summary>Authored maximum width of the overlapping Synced streams, in screen pixels.</summary>
    private const float SyncStreamWidthMax = 9f;

    /// <summary>Authored minimum downward travel speed of the Synced streams, in screen pixels per second.</summary>
    private const float SyncStreamFallSpeedMin = 5f;

    /// <summary>Authored maximum downward travel speed of the Synced streams, in screen pixels per second.</summary>
    private const float SyncStreamFallSpeedMax = 14f;

    /// <summary>Authored maximum value of the Synced water body before droplets or pulse brighten it.</summary>
    private const float SyncWaterBrightness = 0.72f;

    /// <summary>Authored luminance separation between bright and dark Synced streams.</summary>
    private const float SyncStreamContrast = 0.7f;

    /// <summary>
    /// Authored strength of the dark seams carved where the Synced stream field changes
    /// fastest — the same darkness-outlines-shape device as MazeFlyer's edge lines.
    /// </summary>
    private const float SyncStreamEdgeShade = 0.35f;

    /// <summary>Authored floor under the Synced water's value so no roll goes pit-dark.</summary>
    private const float SyncWaterMinBrightness = 0.15f;

    /// <summary>Authored inclusive minimum screen-height multiplier for Synced droplet spawns.</summary>
    private const int SyncDropletSpawnHeightMinMultiplier = 1;

    /// <summary>Authored exclusive maximum screen-height multiplier for Synced droplet spawns.</summary>
    private const int SyncDropletSpawnHeightMaxMultiplier = 5;

    /// <summary>Authored minimum radius for Synced droplet rolls.</summary>
    private const float SyncDropletRadiusMin = 1.25f;

    /// <summary>Authored maximum radius for Synced droplet rolls.</summary>
    private const float SyncDropletRadiusMax = 2.75f;

    /// <summary>Authored minimum falling speed for Synced droplet rolls, in screen pixels per second.</summary>
    private const float SyncDropletSpeedMin = 8f;

    /// <summary>Authored maximum falling speed for Synced droplet rolls, in screen pixels per second.</summary>
    private const float SyncDropletSpeedMax = 24f;

    /// <summary>Authored minimum value-space brightness for Synced droplet rolls.</summary>
    private const float SyncDropletBrightnessMin = 0.35f;

    /// <summary>Authored maximum value-space brightness for Synced droplet rolls.</summary>
    private const float SyncDropletBrightnessMax = 0.75f;

    /// <summary>Authored distance over which each Synced droplet trail tapers to darkness.</summary>
    private const float SyncDropletTrailLength = 14f;

    /// <summary>Authored fraction of a Synced droplet's brightness at the head of its trail.</summary>
    private const float SyncDropletTrailBrightness = 0.55f;

    /// <summary>Authored screen-height multiplier below the wall where Synced droplets respawn.</summary>
    private const int SyncDropletRespawnHeightMultiplier = -2;

    /// <summary>Waterfall's falling streams and droplets suit Low-, Mid-, and High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Waterfall's file-local Standalone Defaults.</summary>
    public static WaterfallStandaloneSettings StandaloneDefaults => new WaterfallStandaloneSettings
    {
        DropletCount = new IntRange(
            StandaloneDropletCountMin,
            StandaloneDropletCountMaxExclusive),
        PaletteSpread = new FloatRange(StandalonePaletteSpreadMin, StandalonePaletteSpreadMax),
        PaletteSpeed = new FloatRange(StandalonePaletteSpeedMin, StandalonePaletteSpeedMax),
        StreamWidth = new FloatRange(StandaloneStreamWidthMin, StandaloneStreamWidthMax),
        StreamFallSpeed = new FloatRange(
            StandaloneStreamFallSpeedMin,
            StandaloneStreamFallSpeedMax),
        WaterBrightness = StandaloneWaterBrightness,
        StreamContrast = StandaloneStreamContrast,
        StreamEdgeShade = StandaloneStreamEdgeShade,
        WaterMinBrightness = StandaloneWaterMinBrightness,
        DropletSpawnHeightMultiplier = new IntRange(
            StandaloneDropletSpawnHeightMinMultiplier,
            StandaloneDropletSpawnHeightMaxMultiplier),
        DropletRadius = new FloatRange(StandaloneDropletRadiusMin, StandaloneDropletRadiusMax),
        DropletSpeed = new FloatRange(StandaloneDropletSpeedMin, StandaloneDropletSpeedMax),
        DropletBrightness = new FloatRange(
            StandaloneDropletBrightnessMin,
            StandaloneDropletBrightnessMax),
        DropletTrailLength = StandaloneDropletTrailLength,
        DropletTrailBrightness = StandaloneDropletTrailBrightness,
        DropletRespawnHeightMultiplier = StandaloneDropletRespawnHeightMultiplier,
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
        DropletCount = new IntRange(SyncDropletCountMin, SyncDropletCountMaxExclusive),
        PaletteSpread = new FloatRange(SyncPaletteSpreadMin, SyncPaletteSpreadMax),
        PaletteSpeed = new FloatRange(SyncPaletteSpeedMin, SyncPaletteSpeedMax),
        StreamWidth = new FloatRange(SyncStreamWidthMin, SyncStreamWidthMax),
        StreamFallSpeed = new FloatRange(SyncStreamFallSpeedMin, SyncStreamFallSpeedMax),
        WaterBrightness = SyncWaterBrightness,
        StreamContrast = SyncStreamContrast,
        StreamEdgeShade = SyncStreamEdgeShade,
        WaterMinBrightness = SyncWaterMinBrightness,
        DropletSpawnHeightMultiplier = new IntRange(
            SyncDropletSpawnHeightMinMultiplier,
            SyncDropletSpawnHeightMaxMultiplier),
        DropletRadius = new FloatRange(SyncDropletRadiusMin, SyncDropletRadiusMax),
        DropletSpeed = new FloatRange(SyncDropletSpeedMin, SyncDropletSpeedMax),
        DropletBrightness = new FloatRange(SyncDropletBrightnessMin, SyncDropletBrightnessMax),
        DropletTrailLength = SyncDropletTrailLength,
        DropletTrailBrightness = SyncDropletTrailBrightness,
        DropletRespawnHeightMultiplier = SyncDropletRespawnHeightMultiplier,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private WaterfallStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private WaterfallSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The droplets currently falling through screen space.</summary>
    private Droplet[] droplets;

    /// <summary>The number of droplets rolled for the current activation.</summary>
    private int dropletCount;

    /// <summary>The palette spread rolled across the water's columns.</summary>
    private float paletteSpread;

    /// <summary>The palette-wash speed rolled for the current activation.</summary>
    private float paletteSpeed;

    /// <summary>The width rolled for the overlapping vertical streams.</summary>
    private float streamWidth;

    /// <summary>The downward screen-space speed rolled for the stream field.</summary>
    private float streamFallSpeed;

    /// <summary>The value-space frame composed before palette hue and beat pulse are applied.</summary>
    private float[] waterValueBuffer;

    /// <summary>The cross-stream profile computed once per column, kept for edge-seam gradients.</summary>
    private float[] streamProfileByColumn;

    /// <summary>The palette hue sampled once per screen row for the current frame.</summary>
    private float[] paletteHueByRow;

    /// <summary>The palette saturation sampled once per screen row for the current frame.</summary>
    private float[] paletteSaturationByRow;

    /// <summary>The color-response multiplier rolled for the current activation.</summary>
    private float pulseMultiplier;

    /// <summary>The fixed-length pulse history propagated across screen rows.</summary>
    private readonly float[] wave = new float[400];

    /// <summary>The shortest peak spacing sampled from the Waveform acquired during the current Roll.</summary>
    private float pulsePeakSpacingMs;

    /// <summary>The current mapping from screen rows into the pulse history.</summary>
    private float pulseScale;

    /// <summary>The rolled hue, saturation, or value response mode.</summary>
    private int beatMode;

    /// <summary>The rolled direction used to traverse the pulse history.</summary>
    private int pulseDirection;

    /// <summary>Reports the current droplet count and rolled water-motion values for debug UI.</summary>
    /// <returns>A multi-line description of the current activation.</returns>
    public override string DebugText()
    {
        return $"Droplets: {dropletCount}\n" +
            $"Palette spread: {paletteSpread}\n" +
            $"Palette speed: {paletteSpeed}\n" +
            $"Stream width: {streamWidth}\n" +
            $"Stream fall speed: {streamFallSpeed}\n";
    }

    /// <summary>Allocates Waterfall's reusable value and palette-column buffers after screen setup.</summary>
    public override void Init()
    {
        base.Init();
        waterValueBuffer = new float[screenBuffer.Length];
        streamProfileByColumn = new float[width];
        paletteHueByRow = new float[height];
        paletteSaturationByRow = new float[height];
    }

    /// <summary>Resolves settings, performs the activation Roll, and creates the droplet field.</summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Waterfall),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Waterfall),
            SyncDefaults);

        // Pulse settings are Sync-only even when the Roll happens without a clock. This keeps a
        // later clock acquisition governed by the one settings home that can affect the pulse,
        // without inert Standalone mirrors.
        waveform = waveforms.Random();
        beatMode = Random.Range(
            SyncSettings.BeatMode.MinInclusive,
            SyncSettings.BeatMode.MaxExclusive);
        pulseDirection = Random.Range(
            SyncSettings.PulseDirection.MinInclusive,
            SyncSettings.PulseDirection.MaxExclusive);
        pulseMultiplier = Random.value *
            (SyncSettings.PulseMultiplier.Max - SyncSettings.PulseMultiplier.Min) +
            SyncSettings.PulseMultiplier.Min;
        pulsePeakSpacingMs = waveform.ShortestPeakSpacingMs;
        Array.Clear(wave, 0, wave.Length);

        bool isSynced = beatManager.IsSynced;
        IntRange dropletCountRange = isSynced
            ? SyncSettings.DropletCount
            : standaloneSettings.DropletCount;
        FloatRange paletteSpreadRange = isSynced
            ? SyncSettings.PaletteSpread
            : standaloneSettings.PaletteSpread;
        FloatRange paletteSpeedRange = isSynced
            ? SyncSettings.PaletteSpeed
            : standaloneSettings.PaletteSpeed;
        FloatRange streamWidthRange = isSynced
            ? SyncSettings.StreamWidth
            : standaloneSettings.StreamWidth;
        FloatRange streamFallSpeedRange = isSynced
            ? SyncSettings.StreamFallSpeed
            : standaloneSettings.StreamFallSpeed;

        dropletCount = Random.Range(
            dropletCountRange.MinInclusive,
            dropletCountRange.MaxExclusive);
        paletteSpread = Random.Range(paletteSpreadRange.Min, paletteSpreadRange.Max);
        paletteSpeed = Random.Range(paletteSpeedRange.Min, paletteSpeedRange.Max);
        streamWidth = Random.Range(streamWidthRange.Min, streamWidthRange.Max);
        streamFallSpeed = Random.Range(streamFallSpeedRange.Min, streamFallSpeedRange.Max);
        buffer.Clear();

        IntRange dropletSpawnHeightMultiplierRange = isSynced
            ? SyncSettings.DropletSpawnHeightMultiplier
            : standaloneSettings.DropletSpawnHeightMultiplier;
        FloatRange dropletRadiusRange = isSynced
            ? SyncSettings.DropletRadius
            : standaloneSettings.DropletRadius;
        FloatRange dropletSpeedRange = isSynced
            ? SyncSettings.DropletSpeed
            : standaloneSettings.DropletSpeed;
        FloatRange dropletBrightnessRange = isSynced
            ? SyncSettings.DropletBrightness
            : standaloneSettings.DropletBrightness;

        droplets = new Droplet[dropletCount];
        for (int i = 0; i < droplets.Length; i++)
        {
            droplets[i] = new Droplet(
                dropletSpawnHeightMultiplierRange,
                dropletRadiusRange,
                dropletSpeedRange,
                dropletBrightnessRange);
        }
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

    /// <summary>Composes falling value structure, applies palette hue and pulse, then maps it to tiles.</summary>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        IntRange dropletSpawnHeightMultiplierRange = isSynced
            ? SyncSettings.DropletSpawnHeightMultiplier
            : standaloneSettings.DropletSpawnHeightMultiplier;
        int dropletRespawnHeightMultiplier = isSynced
            ? SyncSettings.DropletRespawnHeightMultiplier
            : standaloneSettings.DropletRespawnHeightMultiplier;
        float dropletTrailLength = isSynced
            ? SyncSettings.DropletTrailLength
            : standaloneSettings.DropletTrailLength;
        float dropletTrailBrightness = isSynced
            ? SyncSettings.DropletTrailBrightness
            : standaloneSettings.DropletTrailBrightness;
        float waterBrightness = isSynced
            ? SyncSettings.WaterBrightness
            : standaloneSettings.WaterBrightness;
        float streamContrast = isSynced
            ? SyncSettings.StreamContrast
            : standaloneSettings.StreamContrast;
        float streamEdgeShade = isSynced
            ? SyncSettings.StreamEdgeShade
            : standaloneSettings.StreamEdgeShade;
        float waterMinBrightness = isSynced
            ? SyncSettings.WaterMinBrightness
            : standaloneSettings.WaterMinBrightness;

        // Droplet physics advances exactly once per frame. Respawn remains the one permitted
        // per-frame Roll and reads the currently active mode's authored position range.
        for (int i = 0; i < droplets.Length; i++)
        {
            droplets[i].Advance(
                effectDelta,
                dropletSpawnHeightMultiplierRange,
                dropletRespawnHeightMultiplier);
        }

        // Feeding zero without a usable clock preserves the wave-history propagation while making
        // the pulse itself a Sync-only response.
        float waveHeight = isSynced
            ? waveform.Lerp(SyncSettings.WaveformTroughHeight, SyncSettings.WaveformPeakHeight)
            : 0f;
        pulseScale = pulsePeakSpacingMs / SyncSettings.PulseScaleDivisor;
        for (int i = wave.Length - 1; i > 0; i--)
        {
            wave[i] = wave[i - 1];
        }

        wave[0] = waveHeight;

        float streamFrequency = Mathf.PI * 2f / streamWidth;
        float fallingOffset = effectTime * streamFallSpeed;
        float darkestStream = waterBrightness * (1f - streamContrast);

        // Only the two weaker harmonics wander, phase-oscillating in opposite directions. The
        // dominant harmonic stays pinned: when its phase wandered too, the whole field visibly
        // translated with it — every activation swept right-to-left, which killed the effect.
        // The counterposed minor drifts still move the interference peaks, so individual streams
        // breathe in width and wobble in place without any shared direction of travel.
        // The minor drift and merge amplitudes are kept small: their mid-oscillation sweeps are
        // the one remaining lateral motion, and larger amplitudes read as right-to-left banding.
        float secondaryDrift = Mathf.Sin(effectTime * 0.059f + 1.3f) * 1.6f;
        float groupingDrift = Mathf.Sin(effectTime * 0.107f + 4.1f) * -1.1f;

        // The merge envelope slowly fades the dominant comb in and out across the width. Where
        // it runs weak the comb's dark gaps fill in and neighboring streams join into one broad
        // mass; where strong, streams stay crisp — real falls do both. Its phase oscillates
        // (zero-mean) like the minor drifts, so merge zones wobble in place, never travel.
        float mergeDrift = Mathf.Sin(effectTime * 0.047f + 2.6f) * 1.3f;
        for (int x = 0; x < width; x++)
        {
            // The cross-stream profile depends on x (and the slow minor drifts) alone, never the
            // fall time — a phase that mixes x with the fall term slides sideways. Three
            // harmonics at irrational frequency ratios (1 : golden ratio : √2−1) interfere
            // aperiodically, so stream spacing is never even; the long 0.414 wavelength unevens
            // the grouping.
            float primaryStrength = 0.55f +
                (0.45f * Mathf.Sin(x * streamFrequency * 0.23f + mergeDrift));
            streamProfileByColumn[x] = 0.5f +
                (0.24f * primaryStrength * Mathf.Sin(x * streamFrequency)) +
                (0.16f * Mathf.Sin(x * streamFrequency * 1.618f + 1.7f + secondaryDrift)) +
                (0.10f * Mathf.Sin(x * streamFrequency * 0.414f + groupingDrift));
        }

        for (int x = 0; x < width; x++)
        {
            float streamProfile = streamProfileByColumn[x];

            // Dark seams are carved where the cross-stream slope is steepest — the boundary
            // between a stream and its neighbor — so darkness outlines every stream the way
            // MazeFlyer's edge lines outline its walls. Shading follows the field, so seams
            // hold as still as the streams they separate.
            float slope = streamProfileByColumn[Mathf.Min(x + 1, width - 1)] -
                streamProfileByColumn[Mathf.Max(x - 1, 0)];
            float edgeFactor = 1f - (streamEdgeShade * Mathf.Clamp01(Mathf.Abs(slope) * 1.5f));

            // The per-stream phase stagger is constant in time: it keeps neighboring streams'
            // falling texture out of step so the flow never lines up into horizontal bars.
            float streamPhase = x * streamFrequency * 0.7f;
            int screenIndex = x;

            for (int y = 0; y < height; y++)
            {
                // Time lives only in the y term, so texture moves straight down each stream.
                // The 0.25 factor stretches the falling features to several stream-widths tall.
                float flow = 0.5f + (0.5f * Mathf.Sin(
                    ((y + fallingOffset) * streamFrequency * 0.25f) + streamPhase));
                float stream = streamProfile * (0.55f + (0.45f * flow));
                float value = Mathf.Lerp(darkestStream, waterBrightness, stream) * edgeFactor;
                waterValueBuffer[screenIndex] = Mathf.Max(value, waterMinBrightness);
                screenIndex += width;
            }
        }

        // Bounded splats replace the former screen-pixel × droplet pass. Each droplet now touches
        // only the core and trail pixels whose value it can actually change.
        for (int i = 0; i < droplets.Length; i++)
        {
            AddDropletValue(droplets[i], dropletTrailLength, dropletTrailBrightness);
        }

        // Hue is sampled per row and its time offset carries constant-hue features toward lower
        // y — the color wash falls with the water instead of sliding sideways across it.
        float paletteOffset = effectTime * paletteSpeed;
        for (int y = 0; y < height; y++)
        {
            Color paletteColor = APalette.read(y * paletteSpread + paletteOffset, true);
            Color.RGBToHSV(
                paletteColor,
                out paletteHueByRow[y],
                out paletteSaturationByRow[y],
                out _);
        }

        for (int y = 0; y < height; y++)
        {
            float pulseY = pulseDirection == 0 ? y : height - y;
            int waveIndex = (int)(pulseY * pulseScale);
            if (waveIndex > wave.Length - 1)
            {
                waveIndex = wave.Length - 1;
            }

            float pulse = wave[waveIndex];
            int rowStart = y * width;
            float rowHue = paletteHueByRow[y];
            float rowSaturation = paletteSaturationByRow[y];
            for (int x = 0; x < width; x++)
            {
                int screenIndex = rowStart + x;
                float h = rowHue;
                float s = rowSaturation;
                float v = waterValueBuffer[screenIndex];

                switch (beatMode)
                {
                    case 0:
                        h += pulse * pulseMultiplier;
                        break;
                    case 1:
                        s += pulse * pulseMultiplier * SyncSettings.SaturationPulseMultiplier;
                        break;
                    case 2:
                        v += pulse * (1f - pulseMultiplier);
                        break;
                }

                screenBuffer[screenIndex] = Color.HSVToRGB(h % 1f, s, v);
            }
        }

        // convert the 2D Matrix buffer to a tile buffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
    }

    /// <summary>Adds one droplet's soft core and tapered trail to the value-space frame.</summary>
    /// <param name="droplet">The rolled droplet to render.</param>
    /// <param name="trailLength">The screen-space distance over which the trail reaches zero.</param>
    /// <param name="trailBrightness">The fraction of droplet brightness at the trail head.</param>
    private void AddDropletValue(
        Droplet droplet,
        float trailLength,
        float trailBrightness)
    {
        Vector2 position = droplet.Position;
        float radius = droplet.Radius;
        float brightness = droplet.Brightness;
        float radiusSquared = radius * radius;
        int minX = Mathf.Max(0, Mathf.FloorToInt(position.x - radius));
        int maxX = Mathf.Min(width - 1, Mathf.CeilToInt(position.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(position.y - radius));
        int maxY = Mathf.Min(
            height - 1,
            Mathf.CeilToInt(position.y + Mathf.Max(radius, trailLength)));

        for (int x = minX; x <= maxX; x++)
        {
            float horizontalDistance = Mathf.Abs(x - position.x);
            float horizontalRemaining = 1f - (horizontalDistance / radius);

            for (int y = minY; y <= maxY; y++)
            {
                float verticalDistance = y - position.y;
                float distanceSquared =
                    (horizontalDistance * horizontalDistance) +
                    (verticalDistance * verticalDistance);
                float contribution = 0f;

                if (distanceSquared < radiusSquared)
                {
                    float radialRemaining = 1f - (Mathf.Sqrt(distanceSquared) / radius);
                    contribution = brightness * SoftFalloff(radialRemaining);
                }

                if (verticalDistance > 0f &&
                    verticalDistance < trailLength &&
                    horizontalRemaining > 0f)
                {
                    float trailRemaining = 1f - (verticalDistance / trailLength);
                    float trailContribution = brightness * trailBrightness *
                        SoftFalloff(horizontalRemaining) * trailRemaining * trailRemaining;
                    contribution = Mathf.Max(contribution, trailContribution);
                }

                int screenIndex = x + (y * width);
                waterValueBuffer[screenIndex] = Mathf.Min(
                    1f,
                    waterValueBuffer[screenIndex] + contribution);
            }
        }
    }

    /// <summary>Shapes a normalized remaining-distance value into an edge-soft cubic falloff.</summary>
    /// <param name="remaining">A normalized value that is one at the core and zero at the edge.</param>
    /// <returns>The smoothed brightness contribution.</returns>
    private static float SoftFalloff(float remaining)
    {
        return remaining * remaining * (3f - (2f * remaining));
    }

    /// <summary>One rolled, falling screen-space droplet used by Waterfall.</summary>
    private sealed class Droplet
    {
        /// <summary>The current screen-space position of this droplet.</summary>
        private Vector2 position;

        /// <summary>The radius rolled for this droplet at activation.</summary>
        private readonly float radius;

        /// <summary>The falling speed rolled for this droplet at activation.</summary>
        private readonly float speed;

        /// <summary>The value-space brightness rolled for this droplet at activation.</summary>
        private readonly float brightness;

        /// <summary>The current screen-space position read by Waterfall's value compositor.</summary>
        public Vector2 Position => position;

        /// <summary>The rolled radius read by Waterfall's value compositor.</summary>
        public float Radius => radius;

        /// <summary>The rolled value-space brightness read by Waterfall's value compositor.</summary>
        public float Brightness => brightness;

        /// <summary>Creates a droplet whose initial position and fixed radius, speed, and brightness are rolled at activation.</summary>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for the position roll.</param>
        /// <param name="radiusRange">Endpoints supplied to the radius roll.</param>
        /// <param name="speedRange">Endpoints supplied to the speed roll.</param>
        /// <param name="brightnessRange">Endpoints supplied to the value-space brightness roll.</param>
        public Droplet(
            IntRange spawnHeightMultiplierRange,
            FloatRange radiusRange,
            FloatRange speedRange,
            FloatRange brightnessRange)
        {
            Respawn(spawnHeightMultiplierRange);
            radius = Random.Range(radiusRange.Min, radiusRange.Max);
            speed = Random.Range(speedRange.Min, speedRange.Max);
            brightness = Random.Range(brightnessRange.Min, brightnessRange.Max);
        }

        /// <summary>Advances the droplet once and respawns it after it falls below the wall.</summary>
        /// <param name="deltaTime">The current frame delta.</param>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for a respawn roll.</param>
        /// <param name="respawnHeightMultiplier">Screen-height multiplier that triggers a respawn.</param>
        public void Advance(
            float deltaTime,
            IntRange spawnHeightMultiplierRange,
            int respawnHeightMultiplier)
        {
            position.y -= speed * deltaTime;
            if (position.y < height * respawnHeightMultiplier)
            {
                Respawn(spawnHeightMultiplierRange);
            }
        }

        /// <summary>Rolls the droplet's next position above the wall while retaining its shape and speed.</summary>
        /// <param name="spawnHeightMultiplierRange">Inclusive-minimum/exclusive-maximum screen-height multipliers for the position roll.</param>
        private void Respawn(IntRange spawnHeightMultiplierRange)
        {
            position = new Vector2(
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
    /// <summary>Inclusive-minimum/exclusive-maximum number of droplets rolled per activation.</summary>
    public IntRange DropletCount;

    /// <summary>Per-activation palette spread across screen columns.</summary>
    public FloatRange PaletteSpread;

    /// <summary>Per-activation speed range for the palette wash.</summary>
    public FloatRange PaletteSpeed;

    /// <summary>Per-activation width range for the overlapping vertical streams.</summary>
    public FloatRange StreamWidth;

    /// <summary>Per-activation downward speed range for the flowing stream structure.</summary>
    public FloatRange StreamFallSpeed;

    /// <summary>Maximum water-body value before droplets brighten it.</summary>
    [Range(0f, 1f)]
    public float WaterBrightness;

    /// <summary>Luminance separation between the bright and dark streams.</summary>
    [Range(0f, 1f)]
    public float StreamContrast;

    /// <summary>Strength of the dark seams carved along the stream boundaries.</summary>
    [Range(0f, 1f)]
    public float StreamEdgeShade;

    /// <summary>Floor under the water's composed value so no roll goes pit-dark.</summary>
    [Range(0f, 1f)]
    public float WaterMinBrightness;

    /// <summary>Inclusive-lower/exclusive-upper screen-height multiplier range for droplet spawns.</summary>
    public IntRange DropletSpawnHeightMultiplier;

    /// <summary>Per-droplet radius range.</summary>
    public FloatRange DropletRadius;

    /// <summary>Per-droplet falling-speed range.</summary>
    public FloatRange DropletSpeed;

    /// <summary>Per-droplet value-space brightness range.</summary>
    public FloatRange DropletBrightness;

    /// <summary>Distance over which each droplet trail tapers to darkness.</summary>
    [Min(0.0001f)]
    public float DropletTrailLength;

    /// <summary>Fraction of droplet brightness applied at the head of each tapered trail.</summary>
    [Range(0f, 1f)]
    public float DropletTrailBrightness;

    /// <summary>Screen-height multiplier below the wall where a droplet respawns.</summary>
    public int DropletRespawnHeightMultiplier;

    /// <summary>Copies every Waterfall Standalone Setting from another value.</summary>
    public void CopyFrom(WaterfallStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        DropletCount = new IntRange(
            source.DropletCount.MinInclusive,
            source.DropletCount.MaxExclusive,
            source.DropletCount.LowRail,
            source.DropletCount.HighRail);
        PaletteSpread = new FloatRange(
            source.PaletteSpread.Min,
            source.PaletteSpread.Max,
            source.PaletteSpread.LowRail,
            source.PaletteSpread.HighRail);
        PaletteSpeed = new FloatRange(
            source.PaletteSpeed.Min,
            source.PaletteSpeed.Max,
            source.PaletteSpeed.LowRail,
            source.PaletteSpeed.HighRail);
        StreamWidth = new FloatRange(
            source.StreamWidth.Min,
            source.StreamWidth.Max,
            source.StreamWidth.LowRail,
            source.StreamWidth.HighRail);
        StreamFallSpeed = new FloatRange(
            source.StreamFallSpeed.Min,
            source.StreamFallSpeed.Max,
            source.StreamFallSpeed.LowRail,
            source.StreamFallSpeed.HighRail);
        WaterBrightness = source.WaterBrightness;
        StreamContrast = source.StreamContrast;
        StreamEdgeShade = source.StreamEdgeShade;
        WaterMinBrightness = source.WaterMinBrightness;
        DropletSpawnHeightMultiplier = new IntRange(
            source.DropletSpawnHeightMultiplier.MinInclusive,
            source.DropletSpawnHeightMultiplier.MaxExclusive,
            source.DropletSpawnHeightMultiplier.LowRail,
            source.DropletSpawnHeightMultiplier.HighRail);
        DropletRadius = new FloatRange(
            source.DropletRadius.Min,
            source.DropletRadius.Max,
            source.DropletRadius.LowRail,
            source.DropletRadius.HighRail);
        DropletSpeed = new FloatRange(
            source.DropletSpeed.Min,
            source.DropletSpeed.Max,
            source.DropletSpeed.LowRail,
            source.DropletSpeed.HighRail);
        DropletBrightness = new FloatRange(
            source.DropletBrightness.Min,
            source.DropletBrightness.Max,
            source.DropletBrightness.LowRail,
            source.DropletBrightness.HighRail);
        DropletTrailLength = source.DropletTrailLength;
        DropletTrailBrightness = source.DropletTrailBrightness;
        DropletRespawnHeightMultiplier = source.DropletRespawnHeightMultiplier;
    }
}

/// <summary>Editable music-response and Synced Mode values saved as Waterfall's Sync Settings.</summary>
[Serializable]
public sealed class WaterfallSyncSettings
{
    /// <summary>
    /// Waveform trough endpoint; the authored value makes the pulse full between rhythmic peaks.
    /// </summary>
    [Range(0f, 1f)] public float WaveformTroughHeight;

    /// <summary>
    /// Waveform peak endpoint whose authored value returns the pulse to zero at each rhythmic peak.
    /// Without a clock, Draw feeds zero directly so the same resting value needs no Standalone slot.
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

    /// <summary>Inclusive-minimum/exclusive-maximum number of droplets rolled per activation.</summary>
    public IntRange DropletCount;

    /// <summary>Palette-spread range rolled across screen columns.</summary>
    public FloatRange PaletteSpread;

    /// <summary>Palette-wash speed range rolled per activation.</summary>
    public FloatRange PaletteSpeed;

    /// <summary>Width range rolled for the overlapping vertical streams.</summary>
    public FloatRange StreamWidth;

    /// <summary>Downward speed range rolled for the flowing stream structure.</summary>
    public FloatRange StreamFallSpeed;

    /// <summary>Maximum water-body value before droplets or pulse brighten it.</summary>
    [Range(0f, 1f)] public float WaterBrightness;

    /// <summary>Luminance separation between the bright and dark streams.</summary>
    [Range(0f, 1f)] public float StreamContrast;

    /// <summary>Strength of the dark seams carved along the stream boundaries.</summary>
    [Range(0f, 1f)] public float StreamEdgeShade;

    /// <summary>Floor under the water's composed value so no roll goes pit-dark.</summary>
    [Range(0f, 1f)] public float WaterMinBrightness;

    /// <summary>Inclusive-lower/exclusive-upper screen-height multiplier range for droplet spawns.</summary>
    public IntRange DropletSpawnHeightMultiplier;

    /// <summary>Radius range rolled for each droplet.</summary>
    public FloatRange DropletRadius;

    /// <summary>Falling-speed range rolled for each droplet.</summary>
    public FloatRange DropletSpeed;

    /// <summary>Value-space brightness range rolled for each droplet.</summary>
    public FloatRange DropletBrightness;

    /// <summary>Distance over which each droplet trail tapers to darkness.</summary>
    [Min(0.0001f)] public float DropletTrailLength;

    /// <summary>Fraction of droplet brightness applied at the head of each tapered trail.</summary>
    [Range(0f, 1f)] public float DropletTrailBrightness;

    /// <summary>Screen-height multiplier below the wall where a droplet respawns.</summary>
    public int DropletRespawnHeightMultiplier;

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
        DropletCount = new IntRange(
            source.DropletCount.MinInclusive,
            source.DropletCount.MaxExclusive,
            source.DropletCount.LowRail,
            source.DropletCount.HighRail);
        PaletteSpread = new FloatRange(
            source.PaletteSpread.Min,
            source.PaletteSpread.Max,
            source.PaletteSpread.LowRail,
            source.PaletteSpread.HighRail);
        PaletteSpeed = new FloatRange(
            source.PaletteSpeed.Min,
            source.PaletteSpeed.Max,
            source.PaletteSpeed.LowRail,
            source.PaletteSpeed.HighRail);
        StreamWidth = new FloatRange(
            source.StreamWidth.Min,
            source.StreamWidth.Max,
            source.StreamWidth.LowRail,
            source.StreamWidth.HighRail);
        StreamFallSpeed = new FloatRange(
            source.StreamFallSpeed.Min,
            source.StreamFallSpeed.Max,
            source.StreamFallSpeed.LowRail,
            source.StreamFallSpeed.HighRail);
        WaterBrightness = source.WaterBrightness;
        StreamContrast = source.StreamContrast;
        StreamEdgeShade = source.StreamEdgeShade;
        WaterMinBrightness = source.WaterMinBrightness;
        DropletSpawnHeightMultiplier = new IntRange(
            source.DropletSpawnHeightMultiplier.MinInclusive,
            source.DropletSpawnHeightMultiplier.MaxExclusive,
            source.DropletSpawnHeightMultiplier.LowRail,
            source.DropletSpawnHeightMultiplier.HighRail);
        DropletRadius = new FloatRange(
            source.DropletRadius.Min,
            source.DropletRadius.Max,
            source.DropletRadius.LowRail,
            source.DropletRadius.HighRail);
        DropletSpeed = new FloatRange(
            source.DropletSpeed.Min,
            source.DropletSpeed.Max,
            source.DropletSpeed.LowRail,
            source.DropletSpeed.HighRail);
        DropletBrightness = new FloatRange(
            source.DropletBrightness.Min,
            source.DropletBrightness.Max,
            source.DropletBrightness.LowRail,
            source.DropletBrightness.HighRail);
        DropletTrailLength = source.DropletTrailLength;
        DropletTrailBrightness = source.DropletTrailBrightness;
        DropletRespawnHeightMultiplier = source.DropletRespawnHeightMultiplier;
    }
}
