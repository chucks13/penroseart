using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat distortion.
/// Renders radial and diagonal tunnel bands directly from tile positions.
/// </summary>
[EffectSyncSettings(typeof(NoiseTunnelSyncSettingsAsset))]
public class NoiseTunnel : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum tile-center scale for the unchanged Standalone tunnel field.</summary>
    private const float StandaloneScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for the unchanged Standalone tunnel field.</summary>
    private const float StandaloneScaleMax = 0.2f;

    /// <summary>Authored minimum tunnel-flow speed for the unchanged Standalone look.</summary>
    private const float StandaloneSpeedMin = 0.1f;

    /// <summary>Authored maximum tunnel-flow speed for the unchanged Standalone look.</summary>
    private const float StandaloneSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandaloneAmplifierMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandaloneAmplifierMax = 5f;

    /// <summary>Authored inclusive lower endpoint of the Standalone style roll.</summary>
    private const int StandaloneStyleMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-style Standalone roll.</summary>
    private const int StandaloneStyleMaxExclusive = 3;

    /// <summary>Authored inclusive lower endpoint of the Standalone direction roll.</summary>
    private const int StandaloneDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete two-direction Standalone roll.</summary>
    private const int StandaloneDirectionMaxExclusive = 2;

    /// <summary>Authored inclusive lower endpoint of the Standalone beat-response-mode roll.</summary>
    private const int StandaloneBeatModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Standalone beat-response roll.</summary>
    private const int StandaloneBeatModeMaxExclusive = 3;

    /// <summary>Authored brightness multiplier returned by Waveform.Lerp without live placement.</summary>
    private const float StandaloneBrightnessFallback = 0.75f;

    // Sync Defaults

    /// <summary>Authored first Waveform energy admitted by NoiseTunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyOne = Energy.Low;

    /// <summary>Authored second Waveform energy admitted by NoiseTunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyTwo = Energy.Mid;

    /// <summary>Authored minimum tile-center scale for a Synced Mode tunnel-field roll.</summary>
    private const float SyncScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for a Synced Mode tunnel-field roll.</summary>
    private const float SyncScaleMax = 0.2f;

    /// <summary>Authored minimum tunnel-flow speed for a Synced Mode roll.</summary>
    private const float SyncSpeedMin = 0.1f;

    /// <summary>Authored maximum tunnel-flow speed for a Synced Mode roll.</summary>
    private const float SyncSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncAmplifierMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncAmplifierMax = 5f;

    /// <summary>Authored inclusive lower endpoint of the Synced Mode style roll.</summary>
    private const int SyncStyleMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-style Synced Mode roll.</summary>
    private const int SyncStyleMaxExclusive = 3;

    /// <summary>Authored inclusive lower endpoint of the Synced Mode direction roll.</summary>
    private const int SyncDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete two-direction Synced Mode roll.</summary>
    private const int SyncDirectionMaxExclusive = 2;

    /// <summary>Authored inclusive lower endpoint of the Synced Mode beat-response-mode roll.</summary>
    private const int SyncBeatModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Synced Mode beat-response roll.</summary>
    private const int SyncBeatModeMaxExclusive = 3;

    /// <summary>Authored brightness multiplier reached at the Waveform trough in Synced Mode.</summary>
    private const float SyncBrightnessAtWaveformTrough = 1f;

    /// <summary>Authored brightness multiplier reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncBrightnessAtWaveformPeak = 0.75f;

    /// <summary>Authored hue offset reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncHueShiftAtWaveformPeak = 0.5f;

    /// <summary>Authored tunnel sample-time offset reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncTimeOffsetAtWaveformPeak = 0.5f;

    /// <summary>Authored saturation that makes the active-Fill response black-and-white in Synced Mode.</summary>
    private const float SyncFillSaturation = 0f;

    /// <summary>Authored eight-beat window over which the tunnel slows its flow before a Drop.</summary>
    private const int SyncDropSlowdownBeats = 8;

    /// <summary>NoiseTunnel's driving noise flow suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
         Repertoire.HandlesFill |  Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of NoiseTunnel's Standalone Defaults.</summary>
    public static NoiseTunnelStandaloneSettings StandaloneSettings => new NoiseTunnelStandaloneSettings(
        new FloatRange(StandaloneScaleMin, StandaloneScaleMax),
        new FloatRange(StandaloneSpeedMin, StandaloneSpeedMax),
        new FloatRange(StandaloneAmplifierMin, StandaloneAmplifierMax),
        StandaloneStyleMinInclusive,
        StandaloneStyleMaxExclusive,
        StandaloneDirectionMinInclusive,
        StandaloneDirectionMaxExclusive,
        StandaloneBeatModeMinInclusive,
        StandaloneBeatModeMaxExclusive,
        StandaloneBrightnessFallback);

    /// <summary>Resolves a fresh copy of NoiseTunnel's file-local Sync Defaults.</summary>
    public static NoiseTunnelSyncSettings SyncDefaults => new NoiseTunnelSyncSettings
    {
        WaveformEnergyOne = SyncWaveformEnergyOne,
        WaveformEnergyTwo = SyncWaveformEnergyTwo,
        ScaleMin = SyncScaleMin,
        ScaleMax = SyncScaleMax,
        SpeedMin = SyncSpeedMin,
        SpeedMax = SyncSpeedMax,
        AmplifierMin = SyncAmplifierMin,
        AmplifierMax = SyncAmplifierMax,
        StyleMinInclusive = SyncStyleMinInclusive,
        StyleMaxExclusive = SyncStyleMaxExclusive,
        DirectionMinInclusive = SyncDirectionMinInclusive,
        DirectionMaxExclusive = SyncDirectionMaxExclusive,
        BeatModeMinInclusive = SyncBeatModeMinInclusive,
        BeatModeMaxExclusive = SyncBeatModeMaxExclusive,
        BrightnessAtWaveformTrough = SyncBrightnessAtWaveformTrough,
        BrightnessAtWaveformPeak = SyncBrightnessAtWaveformPeak,
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeOffsetAtWaveformPeak = SyncTimeOffsetAtWaveformPeak,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private NoiseTunnelStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private NoiseTunnelSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The tunnel slows its flow over the live number of beats leading into a Drop.</summary>
    protected override int DropSlowdownBeats => SyncSettings.DropSlowdownBeats;

    /// <summary>The most recently sampled amplified Perlin value, surfaced in debug text.</summary>
    private float n;

    /// <summary>The tile-center scale rolled for the current activation.</summary>
    private float scale;

    /// <summary>The tunnel-flow speed rolled for the current activation.</summary>
    private float speed;

    /// <summary>The Perlin amplitude rolled for the current activation.</summary>
    private float amplifier;

    /// <summary>The hue offset rolled across the complete normalized color cycle.</summary>
    private float colorDelta;

    /// <summary>The current radial or diagonal distance style.</summary>
    private int style;

    /// <summary>The current normal or inverted distance direction.</summary>
    private int direction;

    /// <summary>The current selector for the three existing beat-response combinations.</summary>
    private int beatMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Noise: {n}\nSpeed: {speed}\nDirection: {direction}";
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
            typeof(NoiseTunnel),
            SyncDefaults);

        waveform = waveforms.Random(SyncSettings.WaveformEnergyOne, SyncSettings.WaveformEnergyTwo);
        bool isSynced = beatManager.IsSynced;
        float scaleMin = isSynced ? SyncSettings.ScaleMin : standaloneSettings.Scale.Min;
        float scaleMax = isSynced ? SyncSettings.ScaleMax : standaloneSettings.Scale.Max;
        float speedMin = isSynced ? SyncSettings.SpeedMin : standaloneSettings.Speed.Min;
        float speedMax = isSynced ? SyncSettings.SpeedMax : standaloneSettings.Speed.Max;
        float amplifierMin = isSynced ? SyncSettings.AmplifierMin : standaloneSettings.Amplifier.Min;
        float amplifierMax = isSynced ? SyncSettings.AmplifierMax : standaloneSettings.Amplifier.Max;
        int styleMin = isSynced ? SyncSettings.StyleMinInclusive : standaloneSettings.StyleMinInclusive;
        int styleMax = isSynced ? SyncSettings.StyleMaxExclusive : standaloneSettings.StyleMaxExclusive;
        int directionMin = isSynced ? SyncSettings.DirectionMinInclusive : standaloneSettings.DirectionMinInclusive;
        int directionMax = isSynced ? SyncSettings.DirectionMaxExclusive : standaloneSettings.DirectionMaxExclusive;
        int beatModeMin = isSynced ? SyncSettings.BeatModeMinInclusive : standaloneSettings.BeatModeMinInclusive;
        int beatModeMax = isSynced ? SyncSettings.BeatModeMaxExclusive : standaloneSettings.BeatModeMaxExclusive;
        scale = Random.Range(scaleMin, scaleMax);
        speed = Random.Range(speedMin, speedMax);
        amplifier = Random.Range(amplifierMin, amplifierMax);
        colorDelta = Random.value;
        style = Random.Range(styleMin, styleMax);
        direction = Random.Range(directionMin, directionMax);
        buffer.Clear();
        beatMode = Random.Range(beatModeMin, beatModeMax);
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
        // This Effect owns its brightness, hue, time-warp, and clockless fallback mappings.
        float rhythm = waveform.Envelope;
        float brightnessAtWaveformPeakOrFallback = beatManager.IsSynced
            ? SyncSettings.BrightnessAtWaveformPeak
            : standaloneSettings.BrightnessFallback;
        float beatBrightness = waveform.Lerp(
            SyncSettings.BrightnessAtWaveformTrough,
            brightnessAtWaveformPeakOrFallback);
        float beatHue = SyncSettings.HueShiftAtWaveformPeak * rhythm;
        float beatTime = effectTime + (SyncSettings.TimeOffsetAtWaveformPeak * rhythm);
        float localTime = effectTime;
        float fillSaturation = SyncSettings.FillSaturation;

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * scale);
            float y = Mathf.Abs(tiles[i].center.y * scale);
            float d1 = Mathf.Sqrt((x * x) + (y * y));
            float d2 = x + y;
            float d3 = x - y;
            if (direction > 0)
            {
                d1 = 10000 - d1;
                d2 = 10000 - d2;
                d3 = 10000 - d3;
            }

            if (beatMode < 2)
                localTime = beatTime;
            float z = localTime * speed;

            switch (style)
            {
                case 0:
                    n = Perlin.Noise(d1 + z);
                    break;
                case 1:
                    n = Perlin.Noise(d2 + z);
                    break;
                case 2:
                    n = Perlin.Noise(d3 + z);
                    break;
            }

            n *= amplifier;
            //n = Mathf.Abs(n);

            int v1 = (int)n;
            Color color;
            if ((v1 & 1) == 0)
            {
                color = Color.HSVToRGB((n + colorDelta) % 1f, 1f, 1);
                Color.RGBToHSV(color, out float h, out float s, out float v);
                if (beatMode > 0)
                {
                    h += beatHue;
                    v *= beatBrightness;
                }
                if (beatManager.Fill.Active)
                {
                    v = (h + s + v) % 1f;                   // assure there is brightness variation
                    s = fillSaturation;
                }
                color = Color.HSVToRGB(h % 1f, s, v);
            }
            else
                color = Color.black;
            buffer[i] = color * beatBrightness;
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce NoiseTunnel's authored no-music look.</summary>
public sealed class NoiseTunnelStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from NoiseTunnel's file-local defaults.</summary>
    public NoiseTunnelStandaloneSettings(
        FloatRange scale,
        FloatRange speed,
        FloatRange amplifier,
        int styleMinInclusive,
        int styleMaxExclusive,
        int directionMinInclusive,
        int directionMaxExclusive,
        int beatModeMinInclusive,
        int beatModeMaxExclusive,
        float brightnessFallback)
    {
        Scale = scale;
        Speed = speed;
        Amplifier = amplifier;
        StyleMinInclusive = styleMinInclusive;
        StyleMaxExclusive = styleMaxExclusive;
        DirectionMinInclusive = directionMinInclusive;
        DirectionMaxExclusive = directionMaxExclusive;
        BeatModeMinInclusive = beatModeMinInclusive;
        BeatModeMaxExclusive = beatModeMaxExclusive;
        BrightnessFallback = brightnessFallback;
    }

    /// <summary>Per-activation tile-center scale range.</summary>
    public FloatRange Scale;

    /// <summary>Per-activation tunnel-flow-speed range.</summary>
    public FloatRange Speed;

    /// <summary>Per-activation Perlin-amplitude range.</summary>
    public FloatRange Amplifier;

    /// <summary>Inclusive lower endpoint of the per-activation style roll.</summary>
    public int StyleMinInclusive;

    /// <summary>Exclusive upper endpoint of the per-activation style roll.</summary>
    public int StyleMaxExclusive;

    /// <summary>Inclusive lower endpoint of the per-activation direction roll.</summary>
    public int DirectionMinInclusive;

    /// <summary>Exclusive upper endpoint of the per-activation direction roll.</summary>
    public int DirectionMaxExclusive;

    /// <summary>Inclusive lower endpoint of the per-activation beat-response-mode roll.</summary>
    public int BeatModeMinInclusive;

    /// <summary>Exclusive upper endpoint of the per-activation beat-response-mode roll.</summary>
    public int BeatModeMaxExclusive;

    /// <summary>Brightness multiplier returned by Waveform.Lerp without live placement.</summary>
    public float BrightnessFallback;
}

/// <summary>Editable music-response values saved as NoiseTunnel's Sync Settings.</summary>
[Serializable]
public sealed class NoiseTunnelSyncSettings
{
    /// <summary>First Waveform energy admitted when NoiseTunnel rolls its musical response.</summary>
    public Energy WaveformEnergyOne;

    /// <summary>Second Waveform energy admitted when NoiseTunnel rolls its musical response.</summary>
    public Energy WaveformEnergyTwo;

    /// <summary>Minimum tile-center scale rolled per activation.</summary>
    [Min(0f)] public float ScaleMin;

    /// <summary>Maximum tile-center scale rolled per activation.</summary>
    [Min(0f)] public float ScaleMax;

    /// <summary>Minimum tunnel-flow speed rolled per activation.</summary>
    [Min(0f)] public float SpeedMin;

    /// <summary>Maximum tunnel-flow speed rolled per activation.</summary>
    [Min(0f)] public float SpeedMax;

    /// <summary>Minimum Perlin amplitude rolled per activation.</summary>
    [Min(0f)] public float AmplifierMin;

    /// <summary>Maximum Perlin amplitude rolled per activation.</summary>
    [Min(0f)] public float AmplifierMax;

    /// <summary>Inclusive lower endpoint supplied to the style roll.</summary>
    [Range(0, 2)] public int StyleMinInclusive;

    /// <summary>
    /// Exclusive upper endpoint supplied to the complete three-style roll. Keep it above
    /// <see cref="StyleMinInclusive"/>; the two <c>[Range]</c> attributes cannot enforce the pair jointly.
    /// </summary>
    [Range(1, 3)] public int StyleMaxExclusive;

    /// <summary>Inclusive lower endpoint supplied to the direction roll.</summary>
    [Range(0, 1)] public int DirectionMinInclusive;

    /// <summary>
    /// Exclusive upper endpoint supplied to the complete two-direction roll. Keep it above
    /// <see cref="DirectionMinInclusive"/>; the two <c>[Range]</c> attributes cannot enforce the pair jointly.
    /// </summary>
    [Range(1, 2)] public int DirectionMaxExclusive;

    /// <summary>Inclusive lower endpoint supplied to the beat-response-mode roll.</summary>
    [Range(0, 2)] public int BeatModeMinInclusive;

    /// <summary>
    /// Exclusive upper endpoint supplied to the complete three-mode beat-response roll. Keep it above
    /// <see cref="BeatModeMinInclusive"/>; the two <c>[Range]</c> attributes cannot enforce the pair jointly.
    /// </summary>
    [Range(1, 3)] public int BeatModeMaxExclusive;

    /// <summary>Brightness multiplier reached at the Waveform trough.</summary>
    [Range(0f, 1f)] public float BrightnessAtWaveformTrough;

    /// <summary>Brightness multiplier reached at the Waveform peak.</summary>
    [Range(0f, 1f)] public float BrightnessAtWaveformPeak;

    /// <summary>Hue offset reached at the Waveform peak.</summary>
    [Range(0f, 1f)] public float HueShiftAtWaveformPeak;

    /// <summary>Tunnel sample-time offset reached at the Waveform peak.</summary>
    [Min(0f)] public float TimeOffsetAtWaveformPeak;

    /// <summary>Saturation assigned while a Fill is active.</summary>
    [Range(0f, 1f)] public float FillSaturation;

    /// <summary>Number of beats over which the inherited Drop response slows the tunnel.</summary>
    [Min(0)] public int DropSlowdownBeats;

    /// <summary>Copies every NoiseTunnel Sync Setting from another value.</summary>
    public void CopyFrom(NoiseTunnelSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        WaveformEnergyOne = source.WaveformEnergyOne;
        WaveformEnergyTwo = source.WaveformEnergyTwo;
        ScaleMin = source.ScaleMin;
        ScaleMax = source.ScaleMax;
        SpeedMin = source.SpeedMin;
        SpeedMax = source.SpeedMax;
        AmplifierMin = source.AmplifierMin;
        AmplifierMax = source.AmplifierMax;
        StyleMinInclusive = source.StyleMinInclusive;
        StyleMaxExclusive = source.StyleMaxExclusive;
        DirectionMinInclusive = source.DirectionMinInclusive;
        DirectionMaxExclusive = source.DirectionMaxExclusive;
        BeatModeMinInclusive = source.BeatModeMinInclusive;
        BeatModeMaxExclusive = source.BeatModeMaxExclusive;
        BrightnessAtWaveformTrough = source.BrightnessAtWaveformTrough;
        BrightnessAtWaveformPeak = source.BrightnessAtWaveformPeak;
        HueShiftAtWaveformPeak = source.HueShiftAtWaveformPeak;
        TimeOffsetAtWaveformPeak = source.TimeOffsetAtWaveformPeak;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
