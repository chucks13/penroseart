using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders directional palette bars in screen space and maps them to Penrose tiles.
/// </summary>
[EffectSyncSettings(typeof(RainbowBarsSyncSettingsAsset))]
public class RainbowBars : ScreenEffect
{
    // Standalone Defaults

    /// <summary>Authored inclusive minimum enum value supplied to the unchanged Standalone direction roll.</summary>
    private const int StandaloneDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the unchanged Standalone direction roll.</summary>
    private const int StandaloneDirectionMaxExclusive = 8;

    /// <summary>
    /// Authored neutral brightness for the unchanged Standalone look when no Waveform sample is available.
    /// </summary>
    private const float StandaloneBrightnessAtRest = 1f;

    /// <summary>Authored secondary-axis skew that shapes the unchanged Standalone direction sampling.</summary>
    private const float StandaloneDirectionSkew = 0.1f;

    // Sync Defaults

    /// <summary>Authored inclusive minimum enum value supplied to a Synced Mode direction roll.</summary>
    private const int SyncDirectionMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to a Synced Mode direction roll.</summary>
    private const int SyncDirectionMaxExclusive = 8;

    /// <summary>Authored inclusive minimum response mode supplied to the distortion roll.</summary>
    private const int SyncDistortionModeMinInclusive = 0;

    /// <summary>Authored exclusive upper bound supplied to the three-mode distortion roll.</summary>
    private const int SyncDistortionModeMaxExclusive = 3;

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

    /// <summary>Resolves a fresh immutable-by-convention copy of RainbowBars' Standalone Defaults.</summary>
    public static RainbowBarsStandaloneSettings StandaloneSettings => new RainbowBarsStandaloneSettings(
        StandaloneDirectionMinInclusive,
        StandaloneDirectionMaxExclusive,
        StandaloneBrightnessAtRest,
        StandaloneDirectionSkew);

    /// <summary>Resolves a fresh copy of RainbowBars' file-local Sync Defaults.</summary>
    public static RainbowBarsSyncSettings SyncDefaults => new RainbowBarsSyncSettings
    {
        DirectionMinInclusive = SyncDirectionMinInclusive,
        DirectionMaxExclusive = SyncDirectionMaxExclusive,
        DistortionModeMinInclusive = SyncDistortionModeMinInclusive,
        DistortionModeMaxExclusive = SyncDistortionModeMaxExclusive,
        BrightnessAtWaveformTrough = SyncBrightnessAtWaveformTrough,
        BrightnessAtRest = SyncBrightnessAtRest,
        HueShiftAtWaveformPeak = SyncHueShiftAtWaveformPeak,
        TimeOffsetAtWaveformPeak = SyncTimeOffsetAtWaveformPeak,
        DirectionSkew = SyncDirectionSkew,
        FillSaturation = SyncFillSaturation,
        DropSlowdownBeats = SyncDropSlowdownBeats,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private RainbowBarsStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private RainbowBarsSyncSettings SyncSettings { get; set; } = SyncDefaults;


    /// <summary>The screen-space direction used to sample the palette bands.</summary>
    private Direction direction;

    /// <summary>Which beat response this activation applies: brightness, color, or time.</summary>
    private int distortionMode; // 0: Brightness, 1: Color, 2: Time

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
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(RainbowBars),
            SyncDefaults);
        waveform = waveforms.Random();
        int directionMinInclusive = beatManager.IsSynced
            ? SyncSettings.DirectionMinInclusive
            : standaloneSettings.DirectionMinInclusive;
        int directionMaxExclusive = beatManager.IsSynced
            ? SyncSettings.DirectionMaxExclusive
            : standaloneSettings.DirectionMaxExclusive;
        direction = (Direction)Random.Range(directionMinInclusive, directionMaxExclusive);
        distortionMode = Random.Range(
            SyncSettings.DistortionModeMinInclusive,
            SyncSettings.DistortionModeMaxExclusive);
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
        float brightnessAtRest = beatManager.IsSynced
            ? SyncSettings.BrightnessAtRest
            : standaloneSettings.BrightnessAtRest;
        float directionSkew = beatManager.IsSynced
            ? SyncSettings.DirectionSkew
            : standaloneSettings.DirectionSkew;
        float beatBrightness = brightnessAtRest;
        float hueShift = 0.0f;
        float sampleTime = effectTime;

        // This effect owns all three response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (distortionMode == 0)
            beatBrightness = waveform.Lerp(SyncSettings.BrightnessAtWaveformTrough, brightnessAtRest);
        else if (distortionMode == 1)
            hueShift = SyncSettings.HueShiftAtWaveformPeak * rhythm;
        else if (distortionMode == 2)
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

/// <summary>The non-editable Standalone Settings that reproduce RainbowBars' authored no-music look.</summary>
public sealed class RainbowBarsStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from RainbowBars' file-local defaults.</summary>
    public RainbowBarsStandaloneSettings(
        int directionMinInclusive,
        int directionMaxExclusive,
        float brightnessAtRest,
        float directionSkew)
    {
        DirectionMinInclusive = directionMinInclusive;
        DirectionMaxExclusive = directionMaxExclusive;
        BrightnessAtRest = brightnessAtRest;
        DirectionSkew = directionSkew;
    }

    /// <summary>Inclusive lower endpoint supplied to the Standalone direction roll.</summary>
    public int DirectionMinInclusive;

    /// <summary>Exclusive upper endpoint supplied to the Standalone direction roll.</summary>
    public int DirectionMaxExclusive;

    /// <summary>Neutral brightness used without a live Waveform sample and by non-brightness response modes.</summary>
    public float BrightnessAtRest;

    /// <summary>Secondary-axis coefficient used by directional palette sampling.</summary>
    public float DirectionSkew;
}

/// <summary>Editable music-response values saved as RainbowBars' Sync Settings.</summary>
[Serializable]
public sealed class RainbowBarsSyncSettings
{
    /// <summary>Inclusive lower endpoint supplied to a Synced Mode direction roll.</summary>
    [Min(0)] public int DirectionMinInclusive;

    /// <summary>Exclusive upper endpoint supplied to a Synced Mode direction roll.</summary>
    [Min(1)] public int DirectionMaxExclusive;

    /// <summary>Inclusive lower endpoint supplied to the distortion-mode roll.</summary>
    [Min(0)] public int DistortionModeMinInclusive;

    /// <summary>Exclusive upper endpoint supplied to the distortion-mode roll.</summary>
    [Min(1)] public int DistortionModeMaxExclusive;

    /// <summary>Brightness multiplier reached at the Waveform trough.</summary>
    [Range(0f, 1f)] public float BrightnessAtWaveformTrough;

    /// <summary>Neutral brightness multiplier used at the Waveform peak, at rest, and by non-brightness response modes.</summary>
    [Range(0f, 1f)] public float BrightnessAtRest;

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

        DirectionMinInclusive = source.DirectionMinInclusive;
        DirectionMaxExclusive = source.DirectionMaxExclusive;
        DistortionModeMinInclusive = source.DistortionModeMinInclusive;
        DistortionModeMaxExclusive = source.DistortionModeMaxExclusive;
        BrightnessAtWaveformTrough = source.BrightnessAtWaveformTrough;
        BrightnessAtRest = source.BrightnessAtRest;
        HueShiftAtWaveformPeak = source.HueShiftAtWaveformPeak;
        TimeOffsetAtWaveformPeak = source.TimeOffsetAtWaveformPeak;
        DirectionSkew = source.DirectionSkew;
        FillSaturation = source.FillSaturation;
        DropSlowdownBeats = source.DropSlowdownBeats;
    }
}
