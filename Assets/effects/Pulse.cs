using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Alternates two colors across tile types with a ping-pong time curve and a radial Waveform history.
/// </summary>
[EffectSyncSettings(typeof(PulseSyncSettingsAsset))]
public class Pulse : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored minimum base hue for the unchanged Standalone look.</summary>
    private const float StandaloneBaseHueMin = 0f;

    /// <summary>Authored maximum base hue for the unchanged Standalone look.</summary>
    private const float StandaloneBaseHueMax = 1f;

    /// <summary>Authored minimum seconds for one leg of the Standalone color ping-pong.</summary>
    private const float StandaloneColorPingPongSecondsMin = 1f;

    /// <summary>Authored maximum seconds for one leg of the Standalone color ping-pong.</summary>
    private const float StandaloneColorPingPongSecondsMax = 5f;

    /// <summary>Authored minimum hue delta between the two Standalone colors.</summary>
    private const float StandaloneColorHueDeltaMin = 0.25f;

    /// <summary>Authored maximum hue delta between the two Standalone colors.</summary>
    private const float StandaloneColorHueDeltaMax = 0.75f;

    /// <summary>Authored inclusive lower endpoint supplied to the Standalone beat-mode Roll.</summary>
    private const int StandaloneBeatModeMinInclusive = 0;

    /// <summary>
    /// Authored exclusive upper endpoint supplied to the Standalone beat-mode Roll. The bound preserves the
    /// original two-mode domain while the existing value-mode <c>case 2</c> remains intended support.
    /// </summary>
    private const int StandaloneBeatModeMaxExclusive = 2;

    /// <summary>Authored minimum HSV pulse multiplier for a Standalone activation Roll.</summary>
    private const float StandalonePulseMultiplierMin = 0.125f;

    /// <summary>Authored maximum HSV pulse multiplier for a Standalone activation Roll.</summary>
    private const float StandalonePulseMultiplierMax = 0.25f;

    /// <summary>Authored millisecond divisor preserving the Standalone radial history scale.</summary>
    private const float StandalonePulseScaleDivisorMilliseconds = 200f;

    /// <summary>
    /// Authored inert radial-pulse height passed to the <c>Waveform.Lerp</c> <c>to</c> slot and returned
    /// without a live Bar Phase.
    /// </summary>
    private const float StandalonePulseHeightAtRest = 0f;

    /// <summary>Authored multiplier preserving retained saturation pulses in Standalone Mode.</summary>
    private const float StandaloneSaturationPulseMultiplier = 2f;

    /// <summary>Authored base preserving the intended retained value pulse in Standalone Mode.</summary>
    private const float StandaloneValuePulseBase = 1f;

    // Sync Defaults

    /// <summary>Authored minimum base hue for a Synced Mode activation Roll.</summary>
    private const float SyncBaseHueMin = 0f;

    /// <summary>Authored maximum base hue for a Synced Mode activation Roll.</summary>
    private const float SyncBaseHueMax = 1f;

    /// <summary>Authored minimum seconds for one leg of the Synced color ping-pong.</summary>
    private const float SyncColorPingPongSecondsMin = 1f;

    /// <summary>Authored maximum seconds for one leg of the Synced color ping-pong.</summary>
    private const float SyncColorPingPongSecondsMax = 5f;

    /// <summary>Authored minimum hue delta between the two Synced colors.</summary>
    private const float SyncColorHueDeltaMin = 0.25f;

    /// <summary>Authored maximum hue delta between the two Synced colors.</summary>
    private const float SyncColorHueDeltaMax = 0.75f;

    /// <summary>Authored inclusive lower endpoint supplied to the Synced beat-mode Roll.</summary>
    private const int SyncBeatModeMinInclusive = 0;

    /// <summary>
    /// Authored exclusive upper endpoint supplied to the Synced beat-mode Roll. The bound preserves the
    /// original two-mode domain while the existing value-mode <c>case 2</c> remains intended support.
    /// </summary>
    private const int SyncBeatModeMaxExclusive = 2;

    /// <summary>Authored minimum HSV pulse multiplier for a Synced Mode activation Roll.</summary>
    private const float SyncPulseMultiplierMin = 0.125f;

    /// <summary>Authored maximum HSV pulse multiplier for a Synced Mode activation Roll.</summary>
    private const float SyncPulseMultiplierMax = 0.25f;

    /// <summary>Authored millisecond divisor mapping shortest Waveform peak spacing into radial history scale.</summary>
    private const float SyncPulseScaleDivisorMilliseconds = 200f;

    /// <summary>Authored radial-pulse height passed to the <c>from</c> slot and reached at the Waveform trough.</summary>
    private const float SyncPulseHeightAtWaveformTrough = 1f;

    /// <summary>Authored radial-pulse height passed to the <c>to</c> slot and reached at the Waveform peak.</summary>
    private const float SyncPulseHeightAtWaveformPeak = 0f;

    /// <summary>Authored fast-forward multiplier for the color ping-pong during a Synced Fill.</summary>
    private const float SyncFillColorTimeMultiplier = 3f;

    /// <summary>Authored multiplier applied by the saturation pulse mode.</summary>
    private const float SyncSaturationPulseMultiplier = 2f;

    /// <summary>
    /// Authored base from which the rolled pulse multiplier is subtracted by the intended value pulse mode.
    /// </summary>
    private const float SyncValuePulseBase = 1f;

    /// <summary>
    /// Authored number of beats used by the inherited Drop slowdown. This was the call's implicit default
    /// before capture.
    /// </summary>
    private const int SyncDropSlowdownBeats = 8;

    /// <summary>Pulse's throbbing accents suit Fills and Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Pulse's Standalone Defaults.</summary>
    public static PulseStandaloneSettings StandaloneSettings => new PulseStandaloneSettings(
        new FloatRange(StandaloneBaseHueMin, StandaloneBaseHueMax),
        new FloatRange(StandaloneColorPingPongSecondsMin, StandaloneColorPingPongSecondsMax),
        new FloatRange(StandaloneColorHueDeltaMin, StandaloneColorHueDeltaMax),
        StandaloneBeatModeMinInclusive,
        StandaloneBeatModeMaxExclusive,
        new FloatRange(StandalonePulseMultiplierMin, StandalonePulseMultiplierMax),
        StandalonePulseScaleDivisorMilliseconds,
        StandalonePulseHeightAtRest,
        StandaloneSaturationPulseMultiplier,
        StandaloneValuePulseBase);

    /// <summary>Resolves a fresh copy of Pulse's file-local Sync Defaults.</summary>
    public static PulseSyncSettings SyncDefaults => new PulseSyncSettings
    {
        BaseHueMin = SyncBaseHueMin,
        BaseHueMax = SyncBaseHueMax,
        ColorPingPongSecondsMin = SyncColorPingPongSecondsMin,
        ColorPingPongSecondsMax = SyncColorPingPongSecondsMax,
        ColorHueDeltaMin = SyncColorHueDeltaMin,
        ColorHueDeltaMax = SyncColorHueDeltaMax,
        BeatModeMinInclusive = SyncBeatModeMinInclusive,
        BeatModeMaxExclusive = SyncBeatModeMaxExclusive,
        PulseMultiplierMin = SyncPulseMultiplierMin,
        PulseMultiplierMax = SyncPulseMultiplierMax,
        PulseScaleDivisorMilliseconds = SyncPulseScaleDivisorMilliseconds,
        PulseHeightAtWaveformTrough = SyncPulseHeightAtWaveformTrough,
        PulseHeightAtWaveformPeak = SyncPulseHeightAtWaveformPeak,
        FillColorTimeMultiplier = SyncFillColorTimeMultiplier,
        SaturationPulseMultiplier = SyncSaturationPulseMultiplier,
        ValuePulseBase = SyncValuePulseBase,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private PulseStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private PulseSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>The first color in the current ping-pong pair.</summary>
    private Color startColor;

    /// <summary>The second color in the current ping-pong pair.</summary>
    private Color endColor;

    /// <summary>Seconds in one leg of the current color ping-pong.</summary>
    private float seconds;

    /// <summary>The current activation's rolled base color.</summary>
    private Color color;

    /// <summary>The current activation's rolled hue delta between its two colors.</summary>
    private float colorDelta;

    /// <summary>The fixed radial history buffer, cleared on every activation.</summary>
    private float[] wave = new float[400];

    /// <summary>The current activation's rolled HSV pulse mode.</summary>
    int beatMode;

    /// <summary>The greatest tile radius observed across activations; currently retained but unread.</summary>
    float maxradius = 0;

    /// <summary>The current activation's rolled base HSV pulse multiplier.</summary>
    float pulseMultipler;

    /// <summary>The acquired Waveform's shortest audible peak spacing captured at activation.</summary>
    float pulsePeakSpacingMilliseconds;

    /// <summary>The live radial history scale derived from peak spacing and Sync Settings.</summary>
    float pulseScale;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"scale: {pulseScale} \nStart: {startColor}\nEnd: {endColor}\nTime: {seconds}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();

    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Pulse),
            SyncDefaults);

        waveform = waveforms.Random();
        wave = new float[400];      // clear array

        float baseHueMin = beatManager.IsSynced ? SyncSettings.BaseHueMin : standaloneSettings.BaseHue.Min;
        float baseHueMax = beatManager.IsSynced ? SyncSettings.BaseHueMax : standaloneSettings.BaseHue.Max;
        color = Color.HSVToRGB(Mathf.Lerp(baseHueMin, baseHueMax, Random.value), 1f, 1f);

        float colorPingPongSecondsMin = beatManager.IsSynced
            ? SyncSettings.ColorPingPongSecondsMin
            : standaloneSettings.ColorPingPongSeconds.Min;
        float colorPingPongSecondsMax = beatManager.IsSynced
            ? SyncSettings.ColorPingPongSecondsMax
            : standaloneSettings.ColorPingPongSeconds.Max;
        seconds = Random.Range(colorPingPongSecondsMin, colorPingPongSecondsMax);

        float colorHueDeltaMin = beatManager.IsSynced
            ? SyncSettings.ColorHueDeltaMin
            : standaloneSettings.ColorHueDelta.Min;
        float colorHueDeltaMax = beatManager.IsSynced
            ? SyncSettings.ColorHueDeltaMax
            : standaloneSettings.ColorHueDelta.Max;
        colorDelta = Random.Range(colorHueDeltaMin, colorHueDeltaMax);
        startColor = color;
        endColor = startColor.Delta(colorDelta);

        int beatModeMinInclusive = beatManager.IsSynced
            ? SyncSettings.BeatModeMinInclusive
            : standaloneSettings.BeatModeMinInclusive;
        int beatModeMaxExclusive = beatManager.IsSynced
            ? SyncSettings.BeatModeMaxExclusive
            : standaloneSettings.BeatModeMaxExclusive;
        beatMode = Random.Range(beatModeMinInclusive, beatModeMaxExclusive);

        float pulseMultiplierMin = beatManager.IsSynced
            ? SyncSettings.PulseMultiplierMin
            : standaloneSettings.PulseMultiplier.Min;
        float pulseMultiplierMax = beatManager.IsSynced
            ? SyncSettings.PulseMultiplierMax
            : standaloneSettings.PulseMultiplier.Max;
        pulseMultipler = Mathf.Lerp(
            pulseMultiplierMin,
            pulseMultiplierMax,
            Random.value);
        pulsePeakSpacingMilliseconds = waveform.ShortestPeakSpacingMs;

        for (int i = 0; i < buffer.Length; i++)
        {
            float r = tiles[i].radius;
            if (r > maxradius)
                maxradius = r;
        }
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
        float localPulseMultiplier = DropSlowdown(pulseMultipler, SyncSettings.DropSlowdownBeats);

        float pulseScaleDivisorMilliseconds = beatManager.IsSynced
            ? SyncSettings.PulseScaleDivisorMilliseconds
            : standaloneSettings.PulseScaleDivisorMilliseconds;
        pulseScale = pulsePeakSpacingMilliseconds / pulseScaleDivisorMilliseconds;

        float localTime = effectTime;
        if (beatManager.Fill.Active)        // go fast in fill       
            localTime *= SyncSettings.FillColorTimeMultiplier;
        var t = Mathf.PingPong(localTime, seconds).Remap(0f, seconds, 0f, 1f, clamp: true);
        float pulseHeightAtWaveformPeak = beatManager.IsSynced
            ? SyncSettings.PulseHeightAtWaveformPeak
            : standaloneSettings.PulseHeightAtRest;
        float waveHeight = waveform.Lerp(
            SyncSettings.PulseHeightAtWaveformTrough,
            pulseHeightAtWaveformPeak);

        float saturationPulseMultiplier = beatManager.IsSynced
            ? SyncSettings.SaturationPulseMultiplier
            : standaloneSettings.SaturationPulseMultiplier;
        float valuePulseBase = beatManager.IsSynced
            ? SyncSettings.ValuePulseBase
            : standaloneSettings.ValuePulseBase;
        for (int i = wave.Length - 1; i > 0; i--)
            wave[i] = wave[i - 1];
        wave[0] = waveHeight;

        var color1 = Color.Lerp(color, endColor, t);
        var color2 = Color.Lerp(endColor, color, t);

        for (int i = 0; i < buffer.Length; i++)
        {
            float waveidxf = tiles[i].radius * pulseScale;
            int waveidx = (int)waveidxf;
            if (waveidx > (wave.Length - 1))
                waveidx = wave.Length - 1;

            Color color = tiles[i].type == 0 ? color1 : color2;

            // sync removes the pulse effect when fill is active and synced
            if (beatManager.Fill.Active && beatManager.IsSynced)
            {
                buffer[i] = color;
                continue;
            }
            Color.RGBToHSV(color, out float h, out float s, out float v);


            switch (beatMode)
            {
                case 0:
                    h += wave[waveidx] * localPulseMultiplier;
                    break;
                case 1:
                    s += wave[waveidx] * localPulseMultiplier * saturationPulseMultiplier;
                    break;
                case 2:
                    v += wave[waveidx] * (valuePulseBase - localPulseMultiplier);
                    break;
            }

            buffer[i] = Color.HSVToRGB(h % 1f, s, v);
        }
    }
}

/// <summary>The non-editable Standalone Settings that reproduce Pulse's authored no-music look.</summary>
public sealed class PulseStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from Pulse's file-local defaults.</summary>
    public PulseStandaloneSettings(
        FloatRange baseHue,
        FloatRange colorPingPongSeconds,
        FloatRange colorHueDelta,
        int beatModeMinInclusive,
        int beatModeMaxExclusive,
        FloatRange pulseMultiplier,
        float pulseScaleDivisorMilliseconds,
        float pulseHeightAtRest,
        float saturationPulseMultiplier,
        float valuePulseBase)
    {
        BaseHue = baseHue;
        ColorPingPongSeconds = colorPingPongSeconds;
        ColorHueDelta = colorHueDelta;
        BeatModeMinInclusive = beatModeMinInclusive;
        BeatModeMaxExclusive = beatModeMaxExclusive;
        PulseMultiplier = pulseMultiplier;
        PulseScaleDivisorMilliseconds = pulseScaleDivisorMilliseconds;
        PulseHeightAtRest = pulseHeightAtRest;
        SaturationPulseMultiplier = saturationPulseMultiplier;
        ValuePulseBase = valuePulseBase;
    }

    /// <summary>Per-activation base-hue Roll range.</summary>
    public FloatRange BaseHue;

    /// <summary>Per-activation Roll range for seconds in one leg of the color ping-pong.</summary>
    public FloatRange ColorPingPongSeconds;

    /// <summary>Per-activation hue-delta Roll range for the two colors.</summary>
    public FloatRange ColorHueDelta;

    /// <summary>Inclusive minimum supplied to the activation's beat-mode Roll.</summary>
    public int BeatModeMinInclusive;

    /// <summary>Exclusive maximum supplied to the activation's beat-mode Roll.</summary>
    public int BeatModeMaxExclusive;

    /// <summary>Per-activation HSV pulse-multiplier Roll range.</summary>
    public FloatRange PulseMultiplier;

    /// <summary>Millisecond divisor preserving the radial history scale in Standalone Mode.</summary>
    public float PulseScaleDivisorMilliseconds;

    /// <summary>Inert <c>Waveform.Lerp</c> <c>to</c>-slot height returned without a live Bar Phase.</summary>
    public float PulseHeightAtRest;

    /// <summary>Multiplier applied to retained saturation pulses in Standalone Mode.</summary>
    public float SaturationPulseMultiplier;

    /// <summary>Base from which the rolled multiplier is subtracted by the intended retained value pulse.</summary>
    public float ValuePulseBase;
}

/// <summary>Editable music-response values saved as Pulse's Sync Settings.</summary>
[Serializable]
public sealed class PulseSyncSettings
{
    /// <summary>Minimum base hue rolled on the next Synced activation.</summary>
    [Range(0f, 1f)] public float BaseHueMin;

    /// <summary>Maximum base hue rolled on the next Synced activation.</summary>
    [Range(0f, 1f)] public float BaseHueMax;

    /// <summary>Minimum seconds rolled for one leg of the Synced color ping-pong.</summary>
    [Min(0.0001f)] public float ColorPingPongSecondsMin;

    /// <summary>Maximum seconds rolled for one leg of the Synced color ping-pong.</summary>
    [Min(0.0001f)] public float ColorPingPongSecondsMax;

    /// <summary>Minimum hue delta rolled between the two Synced colors.</summary>
    [Range(0f, 1f)] public float ColorHueDeltaMin;

    /// <summary>Maximum hue delta rolled between the two Synced colors.</summary>
    [Range(0f, 1f)] public float ColorHueDeltaMax;

    /// <summary>Inclusive minimum supplied to the next Synced beat-mode Roll.</summary>
    [Range(0, 2)] public int BeatModeMinInclusive;

    /// <summary>Exclusive maximum supplied to the next Synced beat-mode Roll; raising it to 3 reaches the intended value mode.</summary>
    [Range(1, 3)] public int BeatModeMaxExclusive;

    /// <summary>Minimum HSV pulse multiplier rolled on the next Synced activation.</summary>
    [Min(0f)] public float PulseMultiplierMin;

    /// <summary>Maximum HSV pulse multiplier rolled on the next Synced activation.</summary>
    [Min(0f)] public float PulseMultiplierMax;

    /// <summary>Millisecond divisor mapping shortest Waveform peak spacing into radial history scale.</summary>
    [Min(0.0001f)] public float PulseScaleDivisorMilliseconds;

    /// <summary>Radial-pulse height passed to the <c>Waveform.Lerp</c> <c>from</c> slot and reached at the trough.</summary>
    [Range(0f, 1f)] public float PulseHeightAtWaveformTrough;

    /// <summary>Radial-pulse height passed to the <c>Waveform.Lerp</c> <c>to</c> slot and reached at the peak.</summary>
    [Range(0f, 1f)] public float PulseHeightAtWaveformPeak;

    /// <summary>Color ping-pong fast-forward multiplier during a Synced Fill.</summary>
    [Min(0f)] public float FillColorTimeMultiplier;

    /// <summary>Multiplier applied by the saturation pulse mode.</summary>
    [Min(0f)] public float SaturationPulseMultiplier;

    /// <summary>Base from which the rolled pulse multiplier is subtracted by the intended value pulse mode.</summary>
    [Min(0f)] public float ValuePulseBase;

    /// <summary>Number of beats used by the inherited Drop slowdown.</summary>
    [Min(1)] public int DropSlowdownBeats;

    /// <summary>Copies every Pulse Sync Setting from another value.</summary>
    public void CopyFrom(PulseSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BaseHueMin = source.BaseHueMin;
        BaseHueMax = source.BaseHueMax;
        ColorPingPongSecondsMin = source.ColorPingPongSecondsMin;
        ColorPingPongSecondsMax = source.ColorPingPongSecondsMax;
        ColorHueDeltaMin = source.ColorHueDeltaMin;
        ColorHueDeltaMax = source.ColorHueDeltaMax;
        BeatModeMinInclusive = source.BeatModeMinInclusive;
        BeatModeMaxExclusive = source.BeatModeMaxExclusive;
        PulseMultiplierMin = source.PulseMultiplierMin;
        PulseMultiplierMax = source.PulseMultiplierMax;
        PulseScaleDivisorMilliseconds = source.PulseScaleDivisorMilliseconds;
        PulseHeightAtWaveformTrough = source.PulseHeightAtWaveformTrough;
        PulseHeightAtWaveformPeak = source.PulseHeightAtWaveformPeak;
        FillColorTimeMultiplier = source.FillColorTimeMultiplier;
        SaturationPulseMultiplier = source.SaturationPulseMultiplier;
        ValuePulseBase = source.ValuePulseBase;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
