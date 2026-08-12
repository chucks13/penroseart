using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders directional palette bars in screen space and maps them to Penrose tiles.
/// </summary>
[EffectSyncSettings(typeof(RainbowBarsSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(RainbowBarsStandaloneSettingsAsset))]
public class RainbowBars : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum enum value supplied to the unchanged Standalone direction roll.</summary>
    private const int StandaloneDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the unchanged Standalone direction roll.</summary>
    private const int StandaloneDirectionMaxExclusive = 8;

    /// <summary>
    /// Authored lower brightness endpoint, kept identical to Sync so the two modes do not move until
    /// tuned. Without live Waveform placement, Standalone rendering rests on the upper endpoint.
    /// </summary>
    private const float StandaloneBrightnessAtWaveformTrough = 0.85f;

    /// <summary>Authored brightness at the Waveform peak and at rest for the unchanged Standalone look.</summary>
    private const float StandaloneBrightnessAtRest = 1f;

    /// <summary>Authored secondary-axis skew that shapes the unchanged Standalone direction sampling.</summary>
    private const float StandaloneDirectionSkew = 0.1f;

    // Sync Defaults

    /// <summary>Authored inclusive minimum enum value supplied to a Synced Mode direction roll.</summary>
    private const int SyncDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to a Synced Mode direction roll.</summary>
    private const int SyncDirectionMaxExclusive = 8;

    /// <summary>Authored inclusive minimum response mode supplied to the Waveform-response roll.</summary>
    private const int SyncWaveformResponseModeMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the three-mode Waveform-response roll.</summary>
    private const int SyncWaveformResponseModeMaxExclusive = 3;

    /// <summary>Authored brightness multiplier reached at the Waveform trough in Synced Mode.</summary>
    private const float SyncBrightnessAtWaveformTrough = 0.85f;

    /// <summary>Authored neutral brightness multiplier used at the Waveform peak, at rest, and by the other response modes.</summary>
    private const float SyncBrightnessAtRest = 1f;

    /// <summary>Authored palette hue offset reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncHueShiftAtWaveformPeak = 0.25f;

    /// <summary>Authored palette-time offset reached at the Waveform peak in Synced Mode.</summary>
    private const float SyncTimeOffsetAtWaveformPeak = 0.5f;

    /// <summary>Authored secondary-axis skew that shapes Synced Mode direction sampling.</summary>
    private const float SyncDirectionSkew = 0.1f;

    /// <summary>Authored saturation that makes the active-Fill response black-and-white in Synced Mode.</summary>
    private const float SyncFillSaturation = 0f;

    /// <summary>The bands slow their scroll over the authored eight beats leading into a Drop in Synced Mode.</summary>
    private const int SyncDropSlowdownBeats = 8;

    /// <summary>RainbowBars' scrolling bands suit Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>The bands slow their scroll over the eight beats leading into a Drop.</summary>
    protected override int DropSlowdownBeats => SyncSettings.DropSlowdownBeats;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate RainbowBars' authored
    /// Standalone Defaults.
    /// </summary>
    public static RainbowBarsStandaloneSettings StandaloneDefaults => new()
    {
        Direction = new IntRange(
            StandaloneDirectionMinInclusive,
            StandaloneDirectionMaxExclusive),
        Brightness = new FloatRange(
            StandaloneBrightnessAtWaveformTrough,
            StandaloneBrightnessAtRest),
        DirectionSkew = StandaloneDirectionSkew,
    };

    /// <summary>Resolves a fresh copy of RainbowBars' file-local Sync Defaults.</summary>
    public static RainbowBarsSyncSettings SyncDefaults => new()
    {
        Direction = new IntRange(SyncDirectionMinInclusive, SyncDirectionMaxExclusive),
        WaveformResponseMode = new IntRange(
            SyncWaveformResponseModeMinInclusive,
            SyncWaveformResponseModeMaxExclusive),
        Brightness = new FloatRange(SyncBrightnessAtWaveformTrough, SyncBrightnessAtRest),
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeOffsetAtWaveformPeak = SyncTimeOffsetAtWaveformPeak,
        DirectionSkew = SyncDirectionSkew,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private RainbowBarsStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RainbowBarsSyncSettings SyncSettings { get; set; } = SyncDefaults;


    /// <summary>The screen-space direction used to sample the palette bands.</summary>
    private Direction direction;

    /// <summary>Which Waveform response this activation applies: brightness, color, or time.</summary>
    private int waveformResponseMode; // 0: Brightness, 1: Color, 2: Time

    /// <summary>
    /// Called ever frame to update the debug UI text element
    /// </summary>
    /// <returns></returns>
    public override string DebugText() => direction.ToString();


    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(RainbowBars),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(RainbowBars),
            SyncDefaults);
        waveform = waveforms.Random();
        IntRange directionRange = beatManager.IsSynced
            ? SyncSettings.Direction
            : standaloneSettings.Direction;
        direction = (Direction)Random.Range(
            directionRange.MinInclusive,
            directionRange.MaxExclusive);
        waveformResponseMode = Random.Range(
            SyncSettings.WaveformResponseMode.MinInclusive,
            SyncSettings.WaveformResponseMode.MaxExclusive);
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }

    /// <summary>Samples the scrolling palette at a screen-space position.</summary>
    private static Color GetColor(float position, float sampleTime)
    {
        return APalette.read((position + sampleTime) % 1f, true);
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        bool isSynced = beatManager.IsSynced;
        FloatRange brightnessRange = isSynced
            ? SyncSettings.Brightness
            : standaloneSettings.Brightness;
        float directionSkew = isSynced
            ? SyncSettings.DirectionSkew
            : standaloneSettings.DirectionSkew;
        float beatBrightness = brightnessRange.Max;
        float hueShift = 0.0f;
        float sampleTime = effectTime;

        // This effect owns all three response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (waveformResponseMode == 0)
            beatBrightness = waveform.Lerp(brightnessRange.Min, brightnessRange.Max);
        else if (waveformResponseMode == 1)
            hueShift = SyncSettings.HueShiftAtWaveformPeak * rhythm;
        else if (waveformResponseMode == 2)
            sampleTime = effectTime + (SyncSettings.TimeOffsetAtWaveformPeak * rhythm);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float samplePosition = direction switch
                {
                    Direction.Up => x + (y * -directionSkew),
                    Direction.UpRight => (x * -directionSkew) + (y * -directionSkew),
                    Direction.Down => x + (y * directionSkew),
                    Direction.DownLeft => (x * directionSkew) + (y * directionSkew),
                    Direction.DownRight => (x * -directionSkew) + (y * directionSkew),
                    Direction.Left => (x * directionSkew) + y,
                    Direction.Right => (x * -directionSkew) + y,
                    _ => (x * directionSkew) + (y * -directionSkew),
                };

                Color color = GetColor(samplePosition, sampleTime);
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                if (hueShift > 0)
                {
                    hue += hueShift;
                }
                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    value = (hue + saturation + value) % 1f;                   // assure there is brightness variation
                    saturation = SyncSettings.FillSaturation;
                }
                color = Color.HSVToRGB(hue % 1f, saturation, value);

                screenBuffer[x + (y * width)] = color * beatBrightness;
            }
        }

        // convert the 2D Matrix buffer to a tile buffer
        ConvertScreenBuffer(ref screenBuffer, in buffer);
    }
}

/// <summary>
/// The serializable value shape shared by RainbowBars' fully populated Standalone Defaults and saved
/// Standalone Settings; Unity may create an empty instance before serialized values are applied.
/// </summary>
[Serializable]
public sealed class RainbowBarsStandaloneSettings
{
    /// <summary>Per-activation range supplied to the Standalone direction roll.</summary>
    public IntRange Direction;

    /// <summary>Waveform brightness endpoints, from trough to peak/rest.</summary>
    public FloatRange Brightness;

    /// <summary>Secondary-axis coefficient used by directional palette sampling.</summary>
    [Min(0f)] public float DirectionSkew;

    /// <summary>Copies every RainbowBars Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(RainbowBarsStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Direction = new IntRange(
            source.Direction.MinInclusive,
            source.Direction.MaxExclusive,
            source.Direction.LowRail,
            source.Direction.HighRail);
        Brightness = new FloatRange(
            source.Brightness.Min,
            source.Brightness.Max,
            source.Brightness.LowRail,
            source.Brightness.HighRail);
        DirectionSkew = source.DirectionSkew;
    }
}

/// <summary>The serializable value shape shared by RainbowBars' Sync Defaults and Sync Settings.</summary>
[Serializable]
public sealed class RainbowBarsSyncSettings
{
    /// <summary>Per-activation range supplied to a Synced Mode direction roll.</summary>
    public IntRange Direction;

    /// <summary>Range supplied to the brightness, color, or time Waveform-response roll.</summary>
    public IntRange WaveformResponseMode;

    /// <summary>Waveform brightness endpoints, from trough to peak/rest.</summary>
    public FloatRange Brightness;

    /// <summary>Palette hue offset reached at the Waveform peak.</summary>
    [Range(0f, 1f)] public float HueShiftAtWaveformPeak;

    /// <summary>Palette-time offset reached at the Waveform peak.</summary>
    [Min(0f)] public float TimeOffsetAtWaveformPeak;

    /// <summary>Secondary-axis coefficient used by directional palette sampling.</summary>
    [Min(0f)] public float DirectionSkew;

    /// <summary>Saturation assigned while a Fill is active.</summary>
    [Range(0f, 1f)] public float FillSaturation;

    /// <summary>Number of beats over which the inherited Drop response slows the bands.</summary>
    [Min(0)] public int DropSlowdownBeats;

    /// <summary>Copies every RainbowBars Sync Setting from another value.</summary>
    public void CopyFrom(RainbowBarsSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        Direction = new IntRange(
            source.Direction.MinInclusive,
            source.Direction.MaxExclusive,
            source.Direction.LowRail,
            source.Direction.HighRail);
        WaveformResponseMode = new IntRange(
            source.WaveformResponseMode.MinInclusive,
            source.WaveformResponseMode.MaxExclusive,
            source.WaveformResponseMode.LowRail,
            source.WaveformResponseMode.HighRail);
        Brightness = new FloatRange(
            source.Brightness.Min,
            source.Brightness.Max,
            source.Brightness.LowRail,
            source.Brightness.HighRail);
        HueShiftAtWaveformPeak = source.HueShiftAtWaveformPeak;
        TimeOffsetAtWaveformPeak = source.TimeOffsetAtWaveformPeak;
        DirectionSkew = source.DirectionSkew;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
