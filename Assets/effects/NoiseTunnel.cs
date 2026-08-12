using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat distortion.
/// Renders radial and diagonal tunnel bands directly from tile positions.
/// </summary>
[EffectSyncSettings(typeof(NoiseTunnelSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(NoiseTunnelStandaloneSettingsAsset))]
public class NoiseTunnel : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum tile-center scale for the unchanged Standalone tunnel field.</summary>
    private const float StandaloneTileCenterScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for the unchanged Standalone tunnel field.</summary>
    private const float StandaloneTileCenterScaleMax = 0.2f;

    /// <summary>Authored minimum tunnel-flow speed for the unchanged Standalone look.</summary>
    private const float StandaloneTunnelFlowSpeedMin = 0.1f;

    /// <summary>Authored maximum tunnel-flow speed for the unchanged Standalone look.</summary>
    private const float StandaloneTunnelFlowSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandalonePerlinAmplitudeMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for the unchanged Standalone contrast.</summary>
    private const float StandalonePerlinAmplitudeMax = 5f;

    /// <summary>Authored inclusive lower endpoint of the Standalone distance-style roll.</summary>
    private const int StandaloneDistanceStyleMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-style Standalone distance roll.</summary>
    private const int StandaloneDistanceStyleMaxExclusive = 3;

    /// <summary>Authored inclusive lower endpoint of the Standalone distance-direction roll.</summary>
    private const int StandaloneDistanceDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete two-direction Standalone distance roll.</summary>
    private const int StandaloneDistanceDirectionMaxExclusive = 2;

    /// <summary>Authored inclusive lower endpoint of the Standalone Waveform-response-mode roll.</summary>
    private const int StandaloneWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Standalone Waveform-response roll.</summary>
    private const int StandaloneWaveformResponseModeMaxExclusive = 3;

    /// <summary>Authored brightness multiplier returned by Waveform.Lerp without live placement.</summary>
    private const float StandaloneBrightnessAtRest = 0.75f;

    /// <summary>Authored brightness multiplier corresponding to the Waveform trough endpoint.</summary>
    private const float StandaloneBrightnessAtWaveformTrough = 1f;

    // Sync Defaults

    /// <summary>Authored first Waveform energy admitted by NoiseTunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyOne = Energy.Low;

    /// <summary>Authored second Waveform energy admitted by NoiseTunnel in Synced Mode.</summary>
    private const Energy SyncWaveformEnergyTwo = Energy.Mid;

    /// <summary>Authored minimum tile-center scale for a Synced Mode tunnel-field roll.</summary>
    private const float SyncTileCenterScaleMin = 0.05f;

    /// <summary>Authored maximum tile-center scale for a Synced Mode tunnel-field roll.</summary>
    private const float SyncTileCenterScaleMax = 0.2f;

    /// <summary>Authored minimum tunnel-flow speed for a Synced Mode roll.</summary>
    private const float SyncTunnelFlowSpeedMin = 0.1f;

    /// <summary>Authored maximum tunnel-flow speed for a Synced Mode roll.</summary>
    private const float SyncTunnelFlowSpeedMax = 1.5f;

    /// <summary>Authored minimum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncPerlinAmplitudeMin = 1f;

    /// <summary>Authored maximum Perlin amplitude for a Synced Mode contrast roll.</summary>
    private const float SyncPerlinAmplitudeMax = 5f;

    /// <summary>Authored inclusive lower endpoint of the Synced Mode distance-style roll.</summary>
    private const int SyncDistanceStyleMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-style Synced Mode distance roll.</summary>
    private const int SyncDistanceStyleMaxExclusive = 3;

    /// <summary>Authored inclusive lower endpoint of the Synced Mode distance-direction roll.</summary>
    private const int SyncDistanceDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete two-direction Synced Mode distance roll.</summary>
    private const int SyncDistanceDirectionMaxExclusive = 2;

    /// <summary>Authored inclusive lower endpoint of the Synced Mode Waveform-response-mode roll.</summary>
    private const int SyncWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive upper endpoint of the complete three-mode Synced Mode Waveform-response roll.</summary>
    private const int SyncWaveformResponseModeMaxExclusive = 3;

    /// <summary>Authored brightness multiplier reached at the Waveform trough in Synced Mode.</summary>
    private const float SyncBrightnessAtWaveformTrough = 1f;

    /// <summary>Authored brightness multiplier reached at the Waveform peak and at rest in Synced Mode.</summary>
    private const float SyncBrightnessAtRest = 0.75f;

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

    /// <summary>Resolves a fresh copy of NoiseTunnel's file-local Standalone Defaults.</summary>
    public static NoiseTunnelStandaloneSettings StandaloneDefaults => new()
    {
        TileCenterScale = new FloatRange(
            StandaloneTileCenterScaleMin,
            StandaloneTileCenterScaleMax),
        TunnelFlowSpeed = new FloatRange(
            StandaloneTunnelFlowSpeedMin,
            StandaloneTunnelFlowSpeedMax),
        PerlinAmplitude = new FloatRange(
            StandalonePerlinAmplitudeMin,
            StandalonePerlinAmplitudeMax),
        DistanceStyle = new IntRange(
            StandaloneDistanceStyleMinInclusive,
            StandaloneDistanceStyleMaxExclusive),
        DistanceDirection = new IntRange(
            StandaloneDistanceDirectionMinInclusive,
            StandaloneDistanceDirectionMaxExclusive),
        WaveformResponseMode = new IntRange(
            StandaloneWaveformResponseModeMinInclusive,
            StandaloneWaveformResponseModeMaxExclusive),
        Brightness = new FloatRange(
            StandaloneBrightnessAtRest,
            StandaloneBrightnessAtWaveformTrough),
    };

    /// <summary>Resolves a fresh copy of NoiseTunnel's file-local Sync Defaults.</summary>
    public static NoiseTunnelSyncSettings SyncDefaults => new()
    {
        WaveformEnergyOne = SyncWaveformEnergyOne,
        WaveformEnergyTwo = SyncWaveformEnergyTwo,
        TileCenterScale = new FloatRange(SyncTileCenterScaleMin, SyncTileCenterScaleMax),
        TunnelFlowSpeed = new FloatRange(SyncTunnelFlowSpeedMin, SyncTunnelFlowSpeedMax),
        PerlinAmplitude = new FloatRange(SyncPerlinAmplitudeMin, SyncPerlinAmplitudeMax),
        DistanceStyle = new IntRange(
            SyncDistanceStyleMinInclusive,
            SyncDistanceStyleMaxExclusive),
        DistanceDirection = new IntRange(
            SyncDistanceDirectionMinInclusive,
            SyncDistanceDirectionMaxExclusive),
        WaveformResponseMode = new IntRange(
            SyncWaveformResponseModeMinInclusive,
            SyncWaveformResponseModeMaxExclusive),
        Brightness = new FloatRange(SyncBrightnessAtRest, SyncBrightnessAtWaveformTrough),
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeOffsetAtWaveformPeak = SyncTimeOffsetAtWaveformPeak,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private NoiseTunnelStandaloneSettings standaloneSettings = StandaloneDefaults;

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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(NoiseTunnel),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(NoiseTunnel),
            SyncDefaults);

        waveform = waveforms.Random(SyncSettings.WaveformEnergyOne, SyncSettings.WaveformEnergyTwo);
        bool isSynced = beatManager.IsSynced;
        FloatRange tileCenterScaleRange = isSynced
            ? SyncSettings.TileCenterScale
            : standaloneSettings.TileCenterScale;
        FloatRange tunnelFlowSpeedRange = isSynced
            ? SyncSettings.TunnelFlowSpeed
            : standaloneSettings.TunnelFlowSpeed;
        FloatRange perlinAmplitudeRange = isSynced
            ? SyncSettings.PerlinAmplitude
            : standaloneSettings.PerlinAmplitude;
        IntRange distanceStyleRange = isSynced
            ? SyncSettings.DistanceStyle
            : standaloneSettings.DistanceStyle;
        IntRange distanceDirectionRange = isSynced
            ? SyncSettings.DistanceDirection
            : standaloneSettings.DistanceDirection;
        IntRange waveformResponseModeRange = isSynced
            ? SyncSettings.WaveformResponseMode
            : standaloneSettings.WaveformResponseMode;
        scale = Random.Range(tileCenterScaleRange.Min, tileCenterScaleRange.Max);
        speed = Random.Range(tunnelFlowSpeedRange.Min, tunnelFlowSpeedRange.Max);
        amplifier = Random.Range(perlinAmplitudeRange.Min, perlinAmplitudeRange.Max);
        colorDelta = Random.value;
        style = Random.Range(distanceStyleRange.MinInclusive, distanceStyleRange.MaxExclusive);
        direction = Random.Range(
            distanceDirectionRange.MinInclusive,
            distanceDirectionRange.MaxExclusive);
        buffer.Clear();
        beatMode = Random.Range(
            waveformResponseModeRange.MinInclusive,
            waveformResponseModeRange.MaxExclusive);
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
        FloatRange brightnessRange = beatManager.IsSynced
            ? SyncSettings.Brightness
            : standaloneSettings.Brightness;
        float beatBrightness = waveform.Lerp(
            brightnessRange.Max,
            brightnessRange.Min);
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

/// <summary>
/// The serializable value shape shared by NoiseTunnel's Standalone Defaults and saved Standalone Settings.
/// </summary>
[Serializable]
public sealed class NoiseTunnelStandaloneSettings
{
    /// <summary>Per-activation tile-center scale range.</summary>
    public FloatRange TileCenterScale;

    /// <summary>Per-activation tunnel-flow-speed range.</summary>
    public FloatRange TunnelFlowSpeed;

    /// <summary>Per-activation Perlin-amplitude range.</summary>
    public FloatRange PerlinAmplitude;

    /// <summary>Per-activation range selecting the radial or diagonal distance formula.</summary>
    public IntRange DistanceStyle;

    /// <summary>Per-activation range selecting normal or inverted distance.</summary>
    public IntRange DistanceDirection;

    /// <summary>Per-activation range selecting the Waveform response combination.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>
    /// Brightness range whose maximum is the Waveform trough and whose minimum is the peak and
    /// Standalone fallback, preserving the authored inverse pulse.
    /// </summary>
    public FloatRange Brightness;

    /// <summary>Copies every NoiseTunnel Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(NoiseTunnelStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        TileCenterScale = Copy(source.TileCenterScale);
        TunnelFlowSpeed = Copy(source.TunnelFlowSpeed);
        PerlinAmplitude = Copy(source.PerlinAmplitude);
        DistanceStyle = Copy(source.DistanceStyle);
        DistanceDirection = Copy(source.DistanceDirection);
        WaveformResponseMode = Copy(source.WaveformResponseMode);
        Brightness = Copy(source.Brightness);
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

/// <summary>The serializable value shape shared by NoiseTunnel's Sync Defaults and Sync Settings.</summary>
[Serializable]
public sealed class NoiseTunnelSyncSettings
{
    /// <summary>First Waveform energy admitted when NoiseTunnel rolls its musical response.</summary>
    public Energy WaveformEnergyOne;

    /// <summary>Second Waveform energy admitted when NoiseTunnel rolls its musical response.</summary>
    public Energy WaveformEnergyTwo;

    /// <summary>Per-activation tile-center scale range.</summary>
    public FloatRange TileCenterScale;

    /// <summary>Per-activation tunnel-flow-speed range.</summary>
    public FloatRange TunnelFlowSpeed;

    /// <summary>Per-activation Perlin-amplitude range.</summary>
    public FloatRange PerlinAmplitude;

    /// <summary>Per-activation range selecting the radial or diagonal distance formula.</summary>
    public IntRange DistanceStyle;

    /// <summary>Per-activation range selecting normal or inverted distance.</summary>
    public IntRange DistanceDirection;

    /// <summary>Per-activation range selecting the Waveform response combination.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>
    /// Brightness range whose maximum is the Waveform trough and whose minimum is the peak and
    /// no-placement fallback, preserving the authored inverse pulse.
    /// </summary>
    public FloatRange Brightness;

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
        TileCenterScale = Copy(source.TileCenterScale);
        TunnelFlowSpeed = Copy(source.TunnelFlowSpeed);
        PerlinAmplitude = Copy(source.PerlinAmplitude);
        DistanceStyle = Copy(source.DistanceStyle);
        DistanceDirection = Copy(source.DistanceDirection);
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
