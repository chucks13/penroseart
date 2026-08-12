using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Maintains a fading sparkle field by randomly lighting tiles over the previous frame.
/// </summary>
[EffectSyncSettings(typeof(ColorSparkleSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(ColorSparkleStandaloneSettingsAsset))]
public class ColorSparkle : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored threshold for the 50/50 activation roll that enables fresh per-sparkle hues without a beat clock.</summary>
    private const float StandaloneRandomColorThreshold = 0.5f;

    /// <summary>Authored minimum activation hue for the unchanged Standalone look.</summary>
    private const float StandaloneActivationHueMin = 0f;

    /// <summary>Authored maximum activation hue for the unchanged Standalone look.</summary>
    private const float StandaloneActivationHueMax = 1f;

    /// <summary>Authored minimum fresh hue chosen by each randomly colored Standalone sparkle.</summary>
    private const float StandalonePerSparkleHueMin = 0f;

    /// <summary>Authored maximum fresh hue chosen by each randomly colored Standalone sparkle.</summary>
    private const float StandalonePerSparkleHueMax = 1f;

    /// <summary>
    /// Authored hue offset at the Waveform trough for Standalone Mode. A clockless Waveform returns
    /// the peak endpoint, but keeping the complete range here lets both saved surfaces tune the same
    /// picture-shaping slot independently.
    /// </summary>
    private const float StandaloneWaveformHueOffsetAtTrough = 0.5f;

    /// <summary>Authored clockless hue offset returned by the Waveform's fallback endpoint.</summary>
    private const float StandaloneWaveformHueOffsetAtPeak = 1f;

    /// <summary>Authored modulo period that keeps clockless sparkles inside the original hue band.</summary>
    private const float StandaloneHueWrapPeriod = 0.15f;

    // Sync Defaults

    /// <summary>Authored minimum activation hue in Synced Mode.</summary>
    private const float SyncActivationHueMin = 0f;

    /// <summary>Authored maximum activation hue in Synced Mode.</summary>
    private const float SyncActivationHueMax = 1f;

    /// <summary>Authored minimum for the activation-wide solid hue used by every Drop sparkle.</summary>
    private const float SyncDropHueMin = 0f;

    /// <summary>Authored maximum for the activation-wide solid hue used by every Drop sparkle.</summary>
    private const float SyncDropHueMax = 1f;

    /// <summary>Authored hue offset at the Waveform trough in Synced Mode.</summary>
    private const float SyncWaveformHueOffsetAtTrough = 0.5f;

    /// <summary>Authored hue offset at the Waveform peak in Synced Mode.</summary>
    private const float SyncWaveformHueOffsetAtPeak = 1f;

    /// <summary>Authored modulo period that wraps generated sparkles in Synced Mode.</summary>
    private const float SyncHueWrapPeriod = 0.15f;

    /// <summary>Authored divisor that halves the number of generated sparkles during a Drop.</summary>
    private const int SyncDropSparkleDivisor = 2;

    /// <summary>Authored 50% chance that a newly generated Fill sparkle is white.</summary>
    private const float SyncFillWhiteChance = 0.5f;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate ColorSparkle's authored
    /// Standalone Defaults.
    /// </summary>
    public static ColorSparkleStandaloneSettings StandaloneDefaults => new()
    {
        RandomColorThreshold = StandaloneRandomColorThreshold,
        ActivationHue = new FloatRange(
            StandaloneActivationHueMin,
            StandaloneActivationHueMax),
        PerSparkleHue = new FloatRange(
            StandalonePerSparkleHueMin,
            StandalonePerSparkleHueMax),
        WaveformHueOffset = new FloatRange(
            StandaloneWaveformHueOffsetAtTrough,
            StandaloneWaveformHueOffsetAtPeak),
        HueWrapPeriod = StandaloneHueWrapPeriod,
    };

    /// <summary>Resolves a fresh copy of ColorSparkle's file-local Sync Defaults.</summary>
    public static ColorSparkleSyncSettings SyncDefaults => new()
    {
        ActivationHue = new FloatRange(SyncActivationHueMin, SyncActivationHueMax),
        DropHue = new FloatRange(SyncDropHueMin, SyncDropHueMax),
        WaveformHueOffset = new FloatRange(
            SyncWaveformHueOffsetAtTrough,
            SyncWaveformHueOffsetAtPeak),
        HueWrapPeriod = SyncHueWrapPeriod,
        DropSparkleDivisor = SyncDropSparkleDivisor,
        FillWhiteChance = SyncFillWhiteChance,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private ColorSparkleStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private ColorSparkleSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Whether each sparkle chooses a fresh hue when no beat clock is available.</summary>
    private bool randomColor;

    /// <summary>The activation's base sparkle hue.</summary>
    private float hue;

    /// <summary>The activation-wide solid hue used by every sparkle during a Drop.</summary>
    public float dropHue;

    /// <summary>ColorSparkle's fading sparkle bursts can accent short Fill moments without new behavior;
    /// its gentle shimmer suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop| Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => randomColor ? "Color: random" : $"hue: {hue}";

    /// <summary>
    /// Resolves current Effect Settings without disturbing the roll stream, then initializes
    /// per-activation random state in its original order before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(ColorSparkle),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(ColorSparkle),
            SyncDefaults);

        waveform = waveforms.Random();
        randomColor = Random.value > standaloneSettings.RandomColorThreshold;

        FloatRange activationHue = beatManager.IsSynced
            ? SyncSettings.ActivationHue
            : standaloneSettings.ActivationHue;
        hue = Mathf.Lerp(activationHue.Min, activationHue.Max, Random.value);
        dropHue = Mathf.Lerp(SyncSettings.DropHue.Min, SyncSettings.DropHue.Max, Random.value);

        var text = randomColor ? "random " : hue.ToString();
        controller.debugText.text = $"Color: {text}";
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
        bool isSynced = beatManager.IsSynced;
        // The Waveform offsets newly generated sparkle hues; clockless rendering stays steady at
        // the active Standalone range's fallback endpoint.
        FloatRange waveformHueOffsetRange = isSynced
            ? SyncSettings.WaveformHueOffset
            : standaloneSettings.WaveformHueOffset;
        float waveformHueOffset = waveform.Lerp(
            waveformHueOffsetRange.Min,
            waveformHueOffsetRange.Max);
        float hueWrapPeriod = isSynced
            ? SyncSettings.HueWrapPeriod
            : standaloneSettings.HueWrapPeriod;
        float hueOffset = (hue + waveformHueOffset) % hueWrapPeriod;
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        if (beatManager.Drop.Active)
            count /= SyncSettings.DropSparkleDivisor; // half the sparkles in drop
        for (int i = 0; i < count; i++)
        {
            float drawHue = hueOffset;
            float drawSaturation = 1f;
            // Without a beat clock, preserve this activation's original per-sparkle color variation.
            if (randomColor && !isSynced)
                drawHue = Mathf.Lerp(
                    standaloneSettings.PerSparkleHue.Min,
                    standaloneSettings.PerSparkleHue.Max,
                    Random.value);
            if (beatManager.Drop.Active )
                drawHue = dropHue;                  // solid color in drop 
            if (beatManager.Fill.Active)
                drawSaturation = Random.value > SyncSettings.FillWhiteChance ? 1f : 0f; // fil is 50% white

            buffer[Random.Range(0, buffer.Length)] = Color.HSVToRGB(drawHue, drawSaturation, 1f);
        }
    }
}

/// <summary>
/// The serializable value shape shared by ColorSparkle's fully populated Standalone Defaults and
/// saved Standalone Settings that reproduce its authored no-music look.
/// </summary>
[Serializable]
public sealed class ColorSparkleStandaloneSettings
{
    /// <summary>Threshold for the activation roll that enables fresh per-sparkle hues.</summary>
    [Range(0f, 1f)] public float RandomColorThreshold;

    /// <summary>Per-activation base-hue range.</summary>
    public FloatRange ActivationHue;

    /// <summary>Per-sparkle hue range used when the activation enables fresh Standalone colors.</summary>
    public FloatRange PerSparkleHue;

    /// <summary>Waveform hue-offset range; clockless rendering rests at its maximum endpoint.</summary>
    public FloatRange WaveformHueOffset;

    /// <summary>
    /// Positive modulo period that wraps generated sparkles into the Standalone hue band. Zero
    /// would divide the hue wrap by nothing, so the floor keeps it positive.
    /// </summary>
    [Min(0.001f)] public float HueWrapPeriod;

    /// <summary>Copies every ColorSparkle Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(ColorSparkleStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        RandomColorThreshold = source.RandomColorThreshold;
        ActivationHue = new FloatRange(
            source.ActivationHue.Min,
            source.ActivationHue.Max,
            source.ActivationHue.LowRail,
            source.ActivationHue.HighRail);
        PerSparkleHue = new FloatRange(
            source.PerSparkleHue.Min,
            source.PerSparkleHue.Max,
            source.PerSparkleHue.LowRail,
            source.PerSparkleHue.HighRail);
        WaveformHueOffset = new FloatRange(
            source.WaveformHueOffset.Min,
            source.WaveformHueOffset.Max,
            source.WaveformHueOffset.LowRail,
            source.WaveformHueOffset.HighRail);
        HueWrapPeriod = source.HueWrapPeriod;
    }
}

/// <summary>The serializable value shape shared by ColorSparkle's Sync Defaults and Sync Settings.</summary>
[Serializable]
public sealed class ColorSparkleSyncSettings
{
    /// <summary>Per-activation base-hue range in Synced Mode.</summary>
    public FloatRange ActivationHue;

    /// <summary>Per-activation range for the solid hue used by every Drop sparkle.</summary>
    public FloatRange DropHue;

    /// <summary>Waveform hue offsets interpolated from trough to peak in Synced Mode.</summary>
    public FloatRange WaveformHueOffset;

    /// <summary>
    /// Positive modulo period that wraps generated sparkles in Synced Mode. Zero would divide the
    /// hue wrap by nothing, so the floor keeps it positive.
    /// </summary>
    [Min(0.001f)] public float HueWrapPeriod;

    /// <summary>Divisor applied to the generated sparkle count during a Drop.</summary>
    [Min(1)] public int DropSparkleDivisor;

    /// <summary>Chance that a newly generated Fill sparkle is white.</summary>
    [Range(0f, 1f)] public float FillWhiteChance;

    /// <summary>Copies every ColorSparkle Sync Setting from another value.</summary>
    public void CopyFrom(ColorSparkleSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ActivationHue = new FloatRange(
            source.ActivationHue.Min,
            source.ActivationHue.Max,
            source.ActivationHue.LowRail,
            source.ActivationHue.HighRail);
        DropHue = new FloatRange(
            source.DropHue.Min,
            source.DropHue.Max,
            source.DropHue.LowRail,
            source.DropHue.HighRail);
        WaveformHueOffset = new FloatRange(
            source.WaveformHueOffset.Min,
            source.WaveformHueOffset.Max,
            source.WaveformHueOffset.LowRail,
            source.WaveformHueOffset.HighRail);
        HueWrapPeriod = source.HueWrapPeriod;
        DropSparkleDivisor = source.DropSparkleDivisor;
        FillWhiteChance = source.FillWhiteChance;
    }
}
