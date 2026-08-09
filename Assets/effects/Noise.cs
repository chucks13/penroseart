using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat-driven brightness, color, or time distortion.
/// </summary>
[EffectSyncSettings(typeof(NoiseSyncSettingsAsset))]
public class Noise : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum tile-center scale for the unchanged Standalone noise field.</summary>
    private const float StandaloneScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for the unchanged Standalone noise field.</summary>
    private const float StandaloneScaleMax = 0.2f;

    /// <summary>Authored minimum drift speed for the unchanged Standalone noise field.</summary>
    private const float StandaloneSpeedMin = 0.1f;

    /// <summary>Authored maximum drift speed for the unchanged Standalone noise field.</summary>
    private const float StandaloneSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandaloneAmplifierMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandaloneAmplifierMax = 5f;

    /// <summary>Authored neutral brightness for the unchanged Standalone look when no Waveform sample is available.</summary>
    private const float StandaloneBrightnessAtRest = 1f;

    /// <summary>Authored inclusive lower endpoint of the Standalone distortion-mode roll.</summary>
    private const int StandaloneDistortionModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Standalone distortion roll.</summary>
    private const int StandaloneDistortionModeMaxExclusive = 3;

    // Sync Defaults

    /// <summary>Authored minimum tile-center scale for a Synced Mode noise-field roll.</summary>
    private const float SyncScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for a Synced Mode noise-field roll.</summary>
    private const float SyncScaleMax = 0.2f;

    /// <summary>Authored minimum drift speed for a Synced Mode noise-field roll.</summary>
    private const float SyncSpeedMin = 0.1f;

    /// <summary>Authored maximum drift speed for a Synced Mode noise-field roll.</summary>
    private const float SyncSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncAmplifierMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncAmplifierMax = 5f;

    /// <summary>Authored inclusive minimum response mode supplied to the distortion roll.</summary>
    private const int SyncDistortionModeMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the complete three-mode distortion roll.</summary>
    private const int SyncDistortionModeMaxExclusive = 3;

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

    /// <summary>Resolves a fresh immutable-by-convention copy of Noise's Standalone Defaults.</summary>
    public static NoiseStandaloneSettings StandaloneSettings => new NoiseStandaloneSettings(
        new FloatRange(StandaloneScaleMin, StandaloneScaleMax),
        new FloatRange(StandaloneSpeedMin, StandaloneSpeedMax),
        new FloatRange(StandaloneAmplifierMin, StandaloneAmplifierMax),
        StandaloneBrightnessAtRest,
        StandaloneDistortionModeMinInclusive,
        StandaloneDistortionModeMaxExclusive);

    /// <summary>Resolves a fresh copy of Noise's file-local Sync Defaults.</summary>
    public static NoiseSyncSettings SyncDefaults => new NoiseSyncSettings
    {
        ScaleMin = SyncScaleMin,
        ScaleMax = SyncScaleMax,
        SpeedMin = SyncSpeedMin,
        SpeedMax = SyncSpeedMax,
        AmplifierMin = SyncAmplifierMin,
        AmplifierMax = SyncAmplifierMax,
        DistortionModeMinInclusive = SyncDistortionModeMinInclusive,
        DistortionModeMaxExclusive = SyncDistortionModeMaxExclusive,
        BrightnessAtWaveformTrough = SyncBrightnessAtWaveformTrough,
        BrightnessAtRest = SyncBrightnessAtRest,
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeOffsetAtWaveformPeak = SyncTimeOffsetAtWaveformPeak,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private NoiseStandaloneSettings standaloneSettings = StandaloneSettings;

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

    /// <summary>Current response mode: zero changes brightness, one changes color, and two warps time.</summary>
    private int distortionMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string[] modeNames = { "Brightness", "Color", "Time Warp" };
        return $"Noise: {n}\nSpeed: {speed}\nBeat Mode: {modeNames[distortionMode]}";
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
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Noise),
            SyncDefaults);

        waveform = waveforms.Random();
        bool isSynced = beatManager.IsSynced;
        float scaleMin = isSynced ? SyncSettings.ScaleMin : standaloneSettings.Scale.Min;
        float scaleMax = isSynced ? SyncSettings.ScaleMax : standaloneSettings.Scale.Max;
        float speedMin = isSynced ? SyncSettings.SpeedMin : standaloneSettings.Speed.Min;
        float speedMax = isSynced ? SyncSettings.SpeedMax : standaloneSettings.Speed.Max;
        float amplifierMin = isSynced ? SyncSettings.AmplifierMin : standaloneSettings.Amplifier.Min;
        float amplifierMax = isSynced ? SyncSettings.AmplifierMax : standaloneSettings.Amplifier.Max;
        scale = Random.Range(scaleMin, scaleMax);
        speed = Random.Range(speedMin, speedMax);
        amplifier = Random.Range(amplifierMin, amplifierMax);
        colorDelta = Random.value;
        int distortionModeMin = isSynced
            ? SyncSettings.DistortionModeMinInclusive
            : standaloneSettings.DistortionModeMinInclusive;
        int distortionModeMax = isSynced
            ? SyncSettings.DistortionModeMaxExclusive
            : standaloneSettings.DistortionModeMaxExclusive;
        distortionMode = Random.Range(distortionModeMin, distortionModeMax);
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
        float brightnessAtRest = beatManager.IsSynced
            ? SyncSettings.BrightnessAtRest
            : standaloneSettings.BrightnessAtRest;
        float beatBrightness = brightnessAtRest;
        float hueShift = 0.0f;
        float sampleTime = effectTime;

        // This Effect owns all three response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        hueShift = 0f;
        if (distortionMode == 0)
            beatBrightness = waveform.Lerp(SyncSettings.BrightnessAtWaveformTrough, brightnessAtRest);
        else if (distortionMode == 1)
            hueShift = SyncSettings.HueShiftAtWaveformPeak * rhythm;
        else if (distortionMode == 2)
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

/// <summary>The resolved Standalone Settings that preserve Noise's authored no-music look.</summary>
public sealed class NoiseStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Noise's file-local defaults.</summary>
    public NoiseStandaloneSettings(
        FloatRange scale,
        FloatRange speed,
        FloatRange amplifier,
        float brightnessAtRest,
        int distortionModeMinInclusive,
        int distortionModeMaxExclusive)
    {
        Scale = scale;
        Speed = speed;
        Amplifier = amplifier;
        BrightnessAtRest = brightnessAtRest;
        DistortionModeMinInclusive = distortionModeMinInclusive;
        DistortionModeMaxExclusive = distortionModeMaxExclusive;
    }

    /// <summary>Per-activation tile-center scale range.</summary>
    public FloatRange Scale;

    /// <summary>Per-activation noise-field drift-speed range.</summary>
    public FloatRange Speed;

    /// <summary>Per-activation Perlin-amplitude range.</summary>
    public FloatRange Amplifier;

    /// <summary>Neutral brightness used without a live Waveform sample and by non-brightness response modes.</summary>
    public float BrightnessAtRest;

    /// <summary>Inclusive lower endpoint of the per-activation distortion-mode roll.</summary>
    public int DistortionModeMinInclusive;

    /// <summary>Exclusive upper endpoint of the per-activation distortion-mode roll.</summary>
    public int DistortionModeMaxExclusive;
}

/// <summary>The saved-or-default musical-response settings used by Noise in Synced Mode.</summary>
[Serializable]
public sealed class NoiseSyncSettings
{
    /// <summary>Minimum tile-center scale rolled per activation.</summary>
    [Min(0f)] public float ScaleMin;

    /// <summary>Maximum tile-center scale rolled per activation.</summary>
    [Min(0f)] public float ScaleMax;

    /// <summary>Minimum noise-field drift speed rolled per activation.</summary>
    [Min(0f)] public float SpeedMin;

    /// <summary>Maximum noise-field drift speed rolled per activation.</summary>
    [Min(0f)] public float SpeedMax;

    /// <summary>Minimum Perlin amplitude rolled per activation.</summary>
    [Min(0f)] public float AmplifierMin;

    /// <summary>Maximum Perlin amplitude rolled per activation.</summary>
    [Min(0f)] public float AmplifierMax;

    /// <summary>Inclusive lower endpoint supplied to the distortion-mode roll.</summary>
    [Range(0, 2)] public int DistortionModeMinInclusive;

    /// <summary>
    /// Exclusive upper endpoint supplied to the complete three-mode distortion roll. Keep it above
    /// <see cref="DistortionModeMinInclusive"/>; the two <c>[Range]</c> attributes cannot enforce the pair jointly.
    /// </summary>
    [Range(1, 3)] public int DistortionModeMaxExclusive;

    /// <summary>Brightness multiplier reached at the Waveform trough.</summary>
    [Range(0f, 1f)] public float BrightnessAtWaveformTrough;

    /// <summary>Neutral brightness multiplier used at the Waveform peak, at rest, and by non-brightness response modes.</summary>
    [Range(0f, 1f)] public float BrightnessAtRest;

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

        ScaleMin = source.ScaleMin;
        ScaleMax = source.ScaleMax;
        SpeedMin = source.SpeedMin;
        SpeedMax = source.SpeedMax;
        AmplifierMin = source.AmplifierMin;
        AmplifierMax = source.AmplifierMax;
        DistortionModeMinInclusive = source.DistortionModeMinInclusive;
        DistortionModeMaxExclusive = source.DistortionModeMaxExclusive;
        BrightnessAtWaveformTrough = source.BrightnessAtWaveformTrough;
        BrightnessAtRest = source.BrightnessAtRest;
        HueShiftAtWaveformPeak = source.HueShiftAtWaveformPeak;
        TimeOffsetAtWaveformPeak = source.TimeOffsetAtWaveformPeak;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
