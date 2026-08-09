using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Maintains a fading sparkle field by randomly lighting tiles over the previous frame.
/// </summary>
[EffectSyncSettings(typeof(ColorSparkleSyncSettingsAsset))]
public class ColorSparkle : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored threshold for the 50/50 activation roll that enables fresh per-sparkle hues without a beat clock.</summary>
    private const float StandaloneRandomColorThreshold = 0.5f;

    /// <summary>Authored minimum activation hue for the unchanged Standalone look.</summary>
    private const float StandaloneHueMin = 0f;

    /// <summary>Authored maximum activation hue for the unchanged Standalone look.</summary>
    private const float StandaloneHueMax = 1f;

    /// <summary>Authored minimum fresh hue chosen by each randomly colored Standalone sparkle.</summary>
    private const float StandalonePerSparkleHueMin = 0f;

    /// <summary>Authored maximum fresh hue chosen by each randomly colored Standalone sparkle.</summary>
    private const float StandalonePerSparkleHueMax = 1f;

    /// <summary>Authored clockless hue offset returned by the Waveform's fallback endpoint.</summary>
    private const float StandaloneWaveformHueOffset = 1f;

    /// <summary>Authored hue span that keeps clockless generated sparkles inside the original narrow color band.</summary>
    private const float StandaloneHueSpan = 0.15f;

    // Sync Defaults

    /// <summary>Authored minimum activation hue in Synced Mode.</summary>
    private const float SyncHueMin = 0f;

    /// <summary>Authored maximum activation hue in Synced Mode.</summary>
    private const float SyncHueMax = 1f;

    /// <summary>Authored minimum for the activation-wide solid hue used by every Drop sparkle.</summary>
    private const float SyncDropHueMin = 0f;

    /// <summary>Authored maximum for the activation-wide solid hue used by every Drop sparkle.</summary>
    private const float SyncDropHueMax = 1f;

    /// <summary>Authored hue offset at the Waveform trough in Synced Mode.</summary>
    private const float SyncWaveformHueOffsetAtTrough = 0.5f;

    /// <summary>Authored hue offset at the Waveform peak in Synced Mode.</summary>
    private const float SyncWaveformHueOffsetAtPeak = 1f;

    /// <summary>Authored hue span that wraps generated sparkles in Synced Mode.</summary>
    private const float SyncHueSpan = 0.15f;

    /// <summary>Authored divisor that halves the number of generated sparkles during a Drop.</summary>
    private const int SyncDropSparkleDivisor = 2;

    /// <summary>Authored 50% chance that a newly generated Fill sparkle is white.</summary>
    private const float SyncFillWhiteChance = 0.5f;

    /// <summary>Resolves a fresh immutable-by-convention copy of ColorSparkle's Standalone Defaults.</summary>
    public static ColorSparkleStandaloneSettings StandaloneSettings => new ColorSparkleStandaloneSettings(
        StandaloneRandomColorThreshold,
        new FloatRange(StandaloneHueMin, StandaloneHueMax),
        new FloatRange(StandalonePerSparkleHueMin, StandalonePerSparkleHueMax),
        StandaloneWaveformHueOffset,
        StandaloneHueSpan);

    /// <summary>Resolves a fresh copy of ColorSparkle's file-local Sync Defaults.</summary>
    public static ColorSparkleSyncSettings SyncDefaults => new ColorSparkleSyncSettings
    {
        HueMin = SyncHueMin,
        HueMax = SyncHueMax,
        DropHueMin = SyncDropHueMin,
        DropHueMax = SyncDropHueMax,
        WaveformHueOffsetAtTrough = SyncWaveformHueOffsetAtTrough,
        WaveformHueOffsetAtPeak = SyncWaveformHueOffsetAtPeak,
        HueSpan = SyncHueSpan,
        DropSparkleDivisor = SyncDropSparkleDivisor,
        FillWhiteChance = SyncFillWhiteChance,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private ColorSparkleStandaloneSettings standaloneSettings = StandaloneSettings;

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
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(ColorSparkle),
            SyncDefaults);

        waveform = waveforms.Random();
        randomColor = Random.value > standaloneSettings.RandomColorThreshold;

        float hueMin = beatManager.IsSynced ? SyncSettings.HueMin : standaloneSettings.Hue.Min;
        float hueMax = beatManager.IsSynced ? SyncSettings.HueMax : standaloneSettings.Hue.Max;
        hue = Mathf.Lerp(hueMin, hueMax, Random.value);
        dropHue = Mathf.Lerp(SyncSettings.DropHueMin, SyncSettings.DropHueMax, Random.value);

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
        // The Waveform offsets newly generated sparkle hues; clockless rendering stays steady.
        float waveformHueOffset = waveform.Lerp(
            SyncSettings.WaveformHueOffsetAtTrough,
            beatManager.IsSynced
                ? SyncSettings.WaveformHueOffsetAtPeak
                : standaloneSettings.WaveformHueOffset);
        float hueSpan = beatManager.IsSynced ? SyncSettings.HueSpan : standaloneSettings.HueSpan;
        float hueOffset = (hue + waveformHueOffset) % hueSpan;
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        if (beatManager.Drop.Active)
            count /= SyncSettings.DropSparkleDivisor; // half the sparkles in drop
        for (int i = 0; i < count; i++)
        {
            float drawHue = hueOffset;
            float drawSaturation = 1f;
            // Without a beat clock, preserve this activation's original per-sparkle color variation.
            if (randomColor && !beatManager.IsSynced)
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

/// <summary>The non-editable Standalone Settings that reproduce ColorSparkle's authored no-music look.</summary>
public sealed class ColorSparkleStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from ColorSparkle's file-local defaults.</summary>
    public ColorSparkleStandaloneSettings(
        float randomColorThreshold,
        FloatRange hue,
        FloatRange perSparkleHue,
        float waveformHueOffset,
        float hueSpan)
    {
        RandomColorThreshold = randomColorThreshold;
        Hue = hue;
        PerSparkleHue = perSparkleHue;
        WaveformHueOffset = waveformHueOffset;
        HueSpan = hueSpan;
    }

    /// <summary>Threshold for the activation roll that enables fresh per-sparkle hues.</summary>
    public float RandomColorThreshold;

    /// <summary>Per-activation base-hue range.</summary>
    public FloatRange Hue;

    /// <summary>Per-sparkle hue range used when the activation enables fresh Standalone colors.</summary>
    public FloatRange PerSparkleHue;

    /// <summary>Clockless fallback endpoint for the Waveform hue offset.</summary>
    public float WaveformHueOffset;

    /// <summary>Hue span used to wrap generated sparkles into the Standalone color band.</summary>
    public float HueSpan;
}

/// <summary>Editable music-response values saved as ColorSparkle's Sync Settings.</summary>
[Serializable]
public sealed class ColorSparkleSyncSettings
{
    /// <summary>Minimum activation hue in Synced Mode.</summary>
    [Range(0f, 1f)] public float HueMin;

    /// <summary>Maximum activation hue in Synced Mode.</summary>
    [Range(0f, 1f)] public float HueMax;

    /// <summary>Minimum activation-wide solid hue used during a Drop.</summary>
    [Range(0f, 1f)] public float DropHueMin;

    /// <summary>Maximum activation-wide solid hue used during a Drop.</summary>
    [Range(0f, 1f)] public float DropHueMax;

    /// <summary>Waveform hue offset at the trough in Synced Mode.</summary>
    [Range(0f, 1f)] public float WaveformHueOffsetAtTrough;

    /// <summary>Waveform hue offset at the peak in Synced Mode.</summary>
    [Range(0f, 1f)] public float WaveformHueOffsetAtPeak;

    /// <summary>Hue span used to wrap generated sparkles in Synced Mode. Zero would divide the hue wrap by nothing, so the floor keeps it positive.</summary>
    [Min(0.001f)] public float HueSpan;

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

        HueMin = source.HueMin;
        HueMax = source.HueMax;
        DropHueMin = source.DropHueMin;
        DropHueMax = source.DropHueMax;
        WaveformHueOffsetAtTrough = source.WaveformHueOffsetAtTrough;
        WaveformHueOffsetAtPeak = source.WaveformHueOffsetAtPeak;
        HueSpan = source.HueSpan;
        DropSparkleDivisor = source.DropSparkleDivisor;
        FillWhiteChance = source.FillWhiteChance;
    }
}
