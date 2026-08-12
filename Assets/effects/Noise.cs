using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat-driven brightness, color, or time distortion.
/// </summary>
[EffectSyncSettings(typeof(NoiseSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(NoiseStandaloneSettingsAsset))]
public class Noise : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum tile-center scale for the unchanged Standalone noise field.</summary>
    private const float StandaloneTileCenterScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for the unchanged Standalone noise field.</summary>
    private const float StandaloneTileCenterScaleMax = 0.2f;

    /// <summary>Authored minimum drift speed for the unchanged Standalone noise field.</summary>
    private const float StandaloneNoiseFieldDriftSpeedMin = 0.1f;

    /// <summary>Authored maximum drift speed for the unchanged Standalone noise field.</summary>
    private const float StandaloneNoiseFieldDriftSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandalonePerlinAmplitudeMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandalonePerlinAmplitudeMax = 5f;

    /// <summary>Authored brightness multiplier reached at the Waveform trough.</summary>
    private const float StandaloneBrightnessAtWaveformTrough = 0.85f;

    /// <summary>Authored neutral brightness for the unchanged Standalone look when no Waveform sample is available.</summary>
    private const float StandaloneBrightnessAtRest = 1f;

    /// <summary>Authored inclusive lower endpoint of the Standalone Waveform-response-mode roll.</summary>
    private const int StandaloneWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Standalone Waveform-response roll.</summary>
    private const int StandaloneWaveformResponseModeMaxExclusive = 3;

    // Sync Defaults

    /// <summary>Authored minimum tile-center scale for a Synced Mode noise-field roll.</summary>
    private const float SyncTileCenterScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for a Synced Mode noise-field roll.</summary>
    private const float SyncTileCenterScaleMax = 0.2f;

    /// <summary>Authored minimum drift speed for a Synced Mode noise-field roll.</summary>
    private const float SyncNoiseFieldDriftSpeedMin = 0.1f;

    /// <summary>Authored maximum drift speed for a Synced Mode noise-field roll.</summary>
    private const float SyncNoiseFieldDriftSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncPerlinAmplitudeMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncPerlinAmplitudeMax = 5f;

    /// <summary>Authored inclusive lower endpoint of the Synced Waveform-response-mode roll.</summary>
    private const int SyncWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Synced Waveform-response roll.</summary>
    private const int SyncWaveformResponseModeMaxExclusive = 3;

    /// <summary>Authored brightness multiplier reached at the Waveform trough in Synced Mode.</summary>
    private const float SyncBrightnessAtWaveformTrough = 0.85f;

    /// <summary>Authored neutral brightness multiplier used at the Waveform peak, at rest, and by the other response modes.</summary>
    private const float SyncBrightnessAtRest = 1f;

    /// <summary>Authored palette hue offset reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncHueShiftAtWaveformPeak = 0.25f;

    /// <summary>Authored Perlin sample-time offset reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncTimeOffsetAtWaveformPeak = 0.5f;

    /// <summary>Authored saturation that makes the active-Fill response black-and-white in Synced Mode.</summary>
    private const float SyncFillSaturation = 0f;

    /// <summary>The noise field slows its drift over the authored eight beats leading into a Drop.</summary>
    private const int SyncDropSlowdownBeats = 8;

    /// <summary>Noise's texture suits Low, Mid, and High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |  Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh copy of Noise's file-local Standalone Defaults.</summary>
    public static NoiseStandaloneSettings StandaloneDefaults => new()
    {
        TileCenterScale = new FloatRange(
            StandaloneTileCenterScaleMin,
            StandaloneTileCenterScaleMax),
        NoiseFieldDriftSpeed = new FloatRange(
            StandaloneNoiseFieldDriftSpeedMin,
            StandaloneNoiseFieldDriftSpeedMax),
        PerlinAmplitude = new FloatRange(
            StandalonePerlinAmplitudeMin,
            StandalonePerlinAmplitudeMax),
        WaveformResponseMode = new IntRange(
            StandaloneWaveformResponseModeMinInclusive,
            StandaloneWaveformResponseModeMaxExclusive),
        Brightness = new FloatRange(
            StandaloneBrightnessAtWaveformTrough,
            StandaloneBrightnessAtRest),
    };

    /// <summary>Resolves a fresh copy of Noise's file-local Sync Defaults.</summary>
    public static NoiseSyncSettings SyncDefaults => new()
    {
        TileCenterScale = new FloatRange(SyncTileCenterScaleMin, SyncTileCenterScaleMax),
        NoiseFieldDriftSpeed = new FloatRange(
            SyncNoiseFieldDriftSpeedMin,
            SyncNoiseFieldDriftSpeedMax),
        PerlinAmplitude = new FloatRange(SyncPerlinAmplitudeMin, SyncPerlinAmplitudeMax),
        WaveformResponseMode = new IntRange(
            SyncWaveformResponseModeMinInclusive,
            SyncWaveformResponseModeMaxExclusive),
        Brightness = new FloatRange(SyncBrightnessAtWaveformTrough, SyncBrightnessAtRest),
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeOffsetAtWaveformPeak = SyncTimeOffsetAtWaveformPeak,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private NoiseStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private NoiseSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The noise field slows its drift over the live number of beats leading into a Drop.</summary>
    protected override int DropSlowdownBeats => SyncSettings.DropSlowdownBeats;

    /// <summary>The most recently sampled amplified Perlin value, surfaced in debug text.</summary>
    private float n;

    /// <summary>The tile-center scale rolled for the current activation.</summary>
    private float scale;

    /// <summary>The noise-field drift speed rolled for the current activation.</summary>
    private float speed;

    /// <summary>The Perlin amplitude rolled for the current activation.</summary>
    private float amplifier;

    /// <summary>The palette phase offset rolled across the complete normalized color cycle.</summary>
    private float colorDelta;

    /// <summary>Current Waveform response: zero changes brightness, one changes color, and two warps time.</summary>
    private int waveformResponseMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string[] modeNames = { "Brightness", "Color", "Time Warp" };
        return $"Noise: {n}\nSpeed: {speed}\nWaveform Response: {modeNames[waveformResponseMode]}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Resolves current Effect Settings without disturbing the roll stream, then initializes
    /// per-activation random state in its original order before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(Noise),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Noise),
            SyncDefaults);

        waveform = waveforms.Random();
        bool isSynced = beatManager.IsSynced;
        FloatRange tileCenterScaleRange = isSynced
            ? SyncSettings.TileCenterScale
            : standaloneSettings.TileCenterScale;
        FloatRange noiseFieldDriftSpeedRange = isSynced
            ? SyncSettings.NoiseFieldDriftSpeed
            : standaloneSettings.NoiseFieldDriftSpeed;
        FloatRange perlinAmplitudeRange = isSynced
            ? SyncSettings.PerlinAmplitude
            : standaloneSettings.PerlinAmplitude;
        IntRange waveformResponseModeRange = isSynced
            ? SyncSettings.WaveformResponseMode
            : standaloneSettings.WaveformResponseMode;
        scale = Random.Range(tileCenterScaleRange.Min, tileCenterScaleRange.Max);
        speed = Random.Range(noiseFieldDriftSpeedRange.Min, noiseFieldDriftSpeedRange.Max);
        amplifier = Random.Range(perlinAmplitudeRange.Min, perlinAmplitudeRange.Max);
        colorDelta = Random.value;
        waveformResponseMode = Random.Range(
            waveformResponseModeRange.MinInclusive,
            waveformResponseModeRange.MaxExclusive);
        buffer.Clear();
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
        FloatRange brightnessRange = beatManager.IsSynced
            ? SyncSettings.Brightness
            : standaloneSettings.Brightness;
        float brightnessAtRest = brightnessRange.Max;
        float beatBrightness = brightnessAtRest;
        float hueShift = 0.0f;
        float sampleTime = effectTime;

        // This Effect owns all three response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        hueShift = 0f;
        if (waveformResponseMode == 0)
            beatBrightness = waveform.Lerp(brightnessRange.Min, brightnessAtRest);
        else if (waveformResponseMode == 1)
            hueShift = SyncSettings.HueShiftAtWaveformPeak * rhythm;
        else if (waveformResponseMode == 2)
            sampleTime = effectTime + (SyncSettings.TimeOffsetAtWaveformPeak * rhythm);

        float fillSaturation = SyncSettings.FillSaturation;
        for (int i = 0; i < buffer.Length; i++)
        {
            float x = tiles[i].center.x * scale;
            float y = tiles[i].center.y * scale;
            float z = sampleTime * speed;

            n = Perlin.Noise(x, y, z);
            n *= amplifier;
            //n = Mathf.Abs(n);

            int v = (int)n;
            if ((v & 1) == 0)
            {
                Color c = APalette.read((n + colorDelta) % 1f, true);
                float h, s, v_col;
                Color.RGBToHSV(c, out h, out s, out v_col);
                h = (h + hueShift) % 1f;

                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    v_col = (h + s + v_col) % 1f;                   // assure there is brightness variation
                    s = fillSaturation;
                }
                
                c = Color.HSVToRGB(h, s, v_col);

                buffer[i] = c * beatBrightness;
            }
            else
                buffer[i] = Color.black;
        }
    }
}

/// <summary>The serializable value shape shared by Noise's Standalone Defaults and saved Standalone Settings.</summary>
[Serializable]
public sealed class NoiseStandaloneSettings
{
    /// <summary>Per-activation tile-center scale range.</summary>
    public FloatRange TileCenterScale;

    /// <summary>Per-activation noise-field drift-speed range.</summary>
    public FloatRange NoiseFieldDriftSpeed;

    /// <summary>Per-activation Perlin-amplitude range.</summary>
    public FloatRange PerlinAmplitude;

    /// <summary>Brightness range from the Waveform trough to the peak and no-placement fallback; the peak endpoint is also the flat brightness applied by the non-brightness response modes.</summary>
    public FloatRange Brightness;

    /// <summary>Per-activation range selecting brightness, hue, or time as the Waveform response.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>Copies every Noise Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(NoiseStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TileCenterScale = Copy(source.TileCenterScale);
        NoiseFieldDriftSpeed = Copy(source.NoiseFieldDriftSpeed);
        PerlinAmplitude = Copy(source.PerlinAmplitude);
        Brightness = Copy(source.Brightness);
        WaveformResponseMode = Copy(source.WaveformResponseMode);
    }

    /// <summary>Copies a float range without sharing mutable saved settings state.</summary>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Copies an integer range without sharing mutable saved settings state.</summary>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}

/// <summary>The saved-or-default musical-response settings used by Noise in Synced Mode.</summary>
[Serializable]
public sealed class NoiseSyncSettings
{
    /// <summary>Per-activation tile-center scale range.</summary>
    public FloatRange TileCenterScale;

    /// <summary>Per-activation noise-field drift-speed range.</summary>
    public FloatRange NoiseFieldDriftSpeed;

    /// <summary>Per-activation Perlin-amplitude range.</summary>
    public FloatRange PerlinAmplitude;

    /// <summary>Per-activation range selecting brightness, hue, or time as the Waveform response.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>Brightness range from the Waveform trough to the peak and no-placement fallback; the peak endpoint is also the flat brightness applied by the non-brightness response modes.</summary>
    public FloatRange Brightness;

    /// <summary>Palette hue offset reached at the Waveform peak.</summary>
    [Range(0f, 1f)] public float HueShiftAtWaveformPeak;

    /// <summary>Perlin sample-time offset reached at the Waveform peak.</summary>
    [Min(0f)] public float TimeOffsetAtWaveformPeak;

    /// <summary>Saturation assigned while a Fill is active.</summary>
    [Range(0f, 1f)] public float FillSaturation;

    /// <summary>Number of beats over which the inherited Drop response slows the noise field.</summary>
    [Min(0)] public int DropSlowdownBeats;

    /// <summary>Copies every Noise Sync Setting from another value.</summary>
    public void CopyFrom(NoiseSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TileCenterScale = Copy(source.TileCenterScale);
        NoiseFieldDriftSpeed = Copy(source.NoiseFieldDriftSpeed);
        PerlinAmplitude = Copy(source.PerlinAmplitude);
        WaveformResponseMode = Copy(source.WaveformResponseMode);
        Brightness = Copy(source.Brightness);
        HueShiftAtWaveformPeak = source.HueShiftAtWaveformPeak;
        TimeOffsetAtWaveformPeak = source.TimeOffsetAtWaveformPeak;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }

    /// <summary>Copies a float range without sharing mutable saved settings state.</summary>
    private static FloatRange Copy(FloatRange source) => new(
        source.Min,
        source.Max,
        source.LowRail,
        source.HighRail);

    /// <summary>Copies an integer range without sharing mutable saved settings state.</summary>
    private static IntRange Copy(IntRange source) => new(
        source.MinInclusive,
        source.MaxExclusive,
        source.LowRail,
        source.HighRail);
}
