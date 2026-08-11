using System;
using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Animates packed Penrose loop shape groups over a background color.
/// </summary>
[EffectSyncSettings(typeof(AnimateLoopsSyncSettingsAsset))]
public class AnimateLoops : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored background hue advance per second for the unchanged Standalone look.</summary>
    private const float StandaloneBackgroundHueRate = 0.1f;

    /// <summary>Authored hue step between tiles within each packed loop for the unchanged Standalone look.</summary>
    private const float StandaloneLoopTileHueStep = 0.01f;

    /// <summary>Authored per-frame hue advance for each packed loop's stored color in the unchanged Standalone look.</summary>
    private const float StandaloneLoopHueAdvance = 0.01f;

    /// <summary>Authored inclusive lower bound of the Standalone distortion-mode roll; 1 selects Color.</summary>
    private const int StandaloneDistortionModeMinInclusive = 1;

    /// <summary>Authored exclusive upper bound of the Standalone distortion-mode roll; 1 is Color and 2 is Time.</summary>
    private const int StandaloneDistortionModeMaxExclusive = 3;

    // Sync Defaults

    /// <summary>Authored Synced Mode counterpart to the background hue advance used in Standalone Mode.</summary>
    private const float SyncBackgroundHueRate = 0.1f;

    /// <summary>Authored Synced Mode counterpart to the hue step between tiles within each packed loop.</summary>
    private const float SyncLoopTileHueStep = 0.01f;

    /// <summary>Authored Synced Mode counterpart to each packed loop's per-frame stored-color hue advance.</summary>
    private const float SyncLoopHueAdvance = 0.01f;

    /// <summary>Inclusive lower bound of the complete distortion roll domain: 1 selects Color.</summary>
    private const int SyncDistortionModeMinInclusive = 1;

    /// <summary>Exclusive upper bound of the complete distortion roll domain: 1 is Color and 2 is Time.</summary>
    private const int SyncDistortionModeMaxExclusive = 3;

    /// <summary>Maximum hue response applied when the rolled distortion mode is Color.</summary>
    private const float SyncHueResponseMagnitude = 0.25f;

    /// <summary>Maximum seconds added to sampled effect time when the rolled distortion mode is Time.</summary>
    private const float SyncTimeWarpSeconds = 0.5f;

    /// <summary>Scale that maps the Time distortion's sampled-time offset into hue.</summary>
    private const float SyncTimeWarpHueScale = 0.1f;

    /// <summary>Current fixed Drop background density; the intended randomization remains unfinished.</summary>
    private const float SyncDropDensity = 0.001f;

    /// <summary>Authored minimum of the unfinished Drop background-density randomization range.</summary>
    private const float SyncDropDensityRollMin = 0.0004f;

    /// <summary>Authored maximum of the unfinished Drop background-density randomization range.</summary>
    private const float SyncDropDensityRollMax = 0.003f;

    /// <summary>Current fixed Drop background speed; the intended randomization remains unfinished.</summary>
    private const float SyncDropSpeed = 0.5f;

    /// <summary>Authored minimum of the unfinished Drop background-speed randomization range.</summary>
    private const float SyncDropSpeedRollMin = 0.1f;

    /// <summary>Authored maximum of the unfinished Drop background-speed randomization range.</summary>
    private const float SyncDropSpeedRollMax = 1f;

    /// <summary>Authored value supplied to the Drop background's HSV brightness slot.</summary>
    private const float SyncDropBrightness = 10f;

    /// <summary>Probability that each packed loop becomes black-and-white during an active Fill.</summary>
    private const float SyncFillBlackAndWhiteProbability = 0.125f;

    /// <summary>AnimateLoops' looping motion suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>Resolves a fresh immutable-by-convention copy of AnimateLoops' Standalone Defaults.</summary>
    public static AnimateLoopsStandaloneSettings StandaloneSettings => new AnimateLoopsStandaloneSettings(
        StandaloneBackgroundHueRate,
        StandaloneLoopTileHueStep,
        StandaloneLoopHueAdvance,
        StandaloneDistortionModeMinInclusive,
        StandaloneDistortionModeMaxExclusive);

    /// <summary>Resolves a fresh copy of AnimateLoops' file-local Sync Defaults.</summary>
    public static AnimateLoopsSyncSettings SyncDefaults => new AnimateLoopsSyncSettings
    {
        BackgroundHueRate = SyncBackgroundHueRate,
        LoopTileHueStep = SyncLoopTileHueStep,
        LoopHueAdvance = SyncLoopHueAdvance,
        DistortionModeMinInclusive = SyncDistortionModeMinInclusive,
        DistortionModeMaxExclusive = SyncDistortionModeMaxExclusive,
        HueResponseMagnitude = SyncHueResponseMagnitude,
        TimeWarpSeconds = SyncTimeWarpSeconds,
        TimeWarpHueScale = SyncTimeWarpHueScale,
        DropDensity = SyncDropDensity,
        DropSpeed = SyncDropSpeed,
        DropBrightness = SyncDropBrightness,
        FillBlackAndWhiteProbability = SyncFillBlackAndWhiteProbability,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private AnimateLoopsStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private AnimateLoopsSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Per-loop colors advanced across the packed shape data.</summary>
    private Color[] colors;

    /// <summary>Background hue advanced continuously while this effect runs.</summary>
    private float background;

    /// <summary>Allocation-free access to the packed loop groups supplied by the Penrose layout.</summary>
    private LayoutData.ShapeList.Reader shape;

    /// <summary>The active packed-shape name shown in the debug readout.</summary>
    private string shapeName;

    /// <summary>Which beat response this activation applies: 1 is Color and 2 is Time.</summary>
    private int distortionMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string modeName = distortionMode == 1 ? "Color" : "Time Warp";
        return $"shape: {shapeName}\nBeat Mode: {modeName}";
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(AnimateLoops),
            SyncDefaults);
        waveform = waveforms.Random();
        shape = penrose.Layout.shapes.Read(penrose.Layout.shapes.loops);
        int distortionModeMin = beatManager.IsSynced
            ? SyncSettings.DistortionModeMinInclusive
            : standaloneSettings.DistortionModeMinInclusive;
        int distortionModeMax = beatManager.IsSynced
            ? SyncSettings.DistortionModeMaxExclusive
            : standaloneSettings.DistortionModeMaxExclusive;
        distortionMode = Random.Range(distortionModeMin, distortionModeMax);
        shapeName = "loops";
        colors = new Color[shape.GroupCount];
        for (int i = 0; i < shape.GroupCount; i++)
        {
            colors[i] = Color.HSVToRGB(Random.value, Random.value, 1f);
        }
        background = Random.value;
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
        float backgroundHueRate = isSynced
            ? SyncSettings.BackgroundHueRate
            : standaloneSettings.BackgroundHueRate;
        float loopTileHueStep = isSynced
            ? SyncSettings.LoopTileHueStep
            : standaloneSettings.LoopTileHueStep;
        float loopHueAdvance = isSynced
            ? SyncSettings.LoopHueAdvance
            : standaloneSettings.LoopHueAdvance;
        float timeWarpHueScale = SyncSettings.TimeWarpHueScale;
        float dropDensity = SyncSettings.DropDensity;
        float dropSpeed = SyncSettings.DropSpeed;
        float dropBrightness = SyncSettings.DropBrightness;
        float fillBlackAndWhiteProbability = SyncSettings.FillBlackAndWhiteProbability;
        float hueShift = 0f;
        float sampleTime = effectTime;

        // This effect owns both response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (distortionMode == 1)
            hueShift = SyncSettings.HueResponseMagnitude * rhythm;
        else if (distortionMode == 2)
            sampleTime = effectTime + (SyncSettings.TimeWarpSeconds * rhythm);

        float beatOffset = sampleTime - effectTime;
        colors[Random.Range(0, shape.GroupCount)] = Color.HSVToRGB(Random.value, Random.value, 1f);
        background += effectDelta * backgroundHueRate;
        background %= 1f;
        for (int i = 0; i < buffer.Length; i++)
        {
            Color color = Color.HSVToRGB((background + beatOffset * timeWarpHueScale + hueShift) % 1f, 1f, 1f);
            // drop mode stole part of tunnel for the backbround color
            if (beatManager.Drop.Active)
            {
                float phase = (i * dropDensity + (effectTime * dropSpeed)) % 1f;
                color = Color.HSVToRGB(phase, 1f, dropBrightness);
            }
            buffer[i] = color;
        }
        for (int i = 0; i < shape.GroupCount; i++)
        {
            LayoutData.ShapeList.Group group = shape.GetGroup(i);
            Color.RGBToHSV(colors[i], out float hue, out float sat, out float bri);
            if (beatManager.Fill.Active)
                sat = Random.value < fillBlackAndWhiteProbability ? 0f : 1f; // B&W on fills

            for (int j = 0; j < group.TileCount; j++)
            {
                int idx = group[j];
                buffer[idx] = Color.HSVToRGB(
                    (hue + loopTileHueStep * group.PackedIndex(j) + beatOffset * timeWarpHueScale + hueShift) % 1f,
                    sat,
                    bri);
            }
            colors[i] = Color.HSVToRGB((hue + loopHueAdvance) % 1f, sat, bri);
        }
    }

}

/// <summary>The resolved Standalone Settings that preserve AnimateLoops' authored no-music look.</summary>
public sealed class AnimateLoopsStandaloneSettings
{
    /// <summary>Creates one resolved Standalone Settings value from AnimateLoops' file-local defaults.</summary>
    public AnimateLoopsStandaloneSettings(
        float backgroundHueRate,
        float loopTileHueStep,
        float loopHueAdvance,
        int distortionModeMinInclusive,
        int distortionModeMaxExclusive)
    {
        BackgroundHueRate = backgroundHueRate;
        LoopTileHueStep = loopTileHueStep;
        LoopHueAdvance = loopHueAdvance;
        DistortionModeMinInclusive = distortionModeMinInclusive;
        DistortionModeMaxExclusive = distortionModeMaxExclusive;
    }

    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Hue step between tiles within each packed loop.</summary>
    public float LoopTileHueStep;

    /// <summary>Per-frame hue advance for each packed loop's stored color.</summary>
    public float LoopHueAdvance;

    /// <summary>Inclusive lower bound of the per-activation distortion-mode roll.</summary>
    public int DistortionModeMinInclusive;

    /// <summary>Exclusive upper bound of the per-activation distortion-mode roll.</summary>
    public int DistortionModeMaxExclusive;
}

/// <summary>The saved-or-default musical-response settings used by AnimateLoops in Synced Mode.</summary>
[Serializable]
public sealed class AnimateLoopsSyncSettings
{
    /// <summary>Live Synced Mode background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live Synced Mode hue step between tiles within each packed loop.</summary>
    public float LoopTileHueStep;

    /// <summary>Live Synced Mode per-frame hue advance for each packed loop's stored color.</summary>
    public float LoopHueAdvance;

    /// <summary>Inclusive minimum of the distortion-mode roll; 1 selects Color.</summary>
    [Range(1, 2)] public int DistortionModeMinInclusive;

    /// <summary>Exclusive maximum of the distortion-mode roll; 3 admits Color and Time.</summary>
    [Range(2, 3)] public int DistortionModeMaxExclusive;

    /// <summary>Maximum hue response applied by Color distortion.</summary>
    [Min(0f)] public float HueResponseMagnitude;

    /// <summary>Maximum seconds added to sampled effect time by Time distortion.</summary>
    [Min(0f)] public float TimeWarpSeconds;

    /// <summary>Scale from the Time distortion's sampled-time offset into hue.</summary>
    [Min(0f)] public float TimeWarpHueScale;

    /// <summary>Current fixed Drop background density.</summary>
    [Min(0f)] public float DropDensity;

    /// <summary>Current fixed Drop background speed.</summary>
    [Min(0f)] public float DropSpeed;

    /// <summary>Value supplied to the Drop background's HSV brightness slot.</summary>
    [Min(0f)] public float DropBrightness;

    /// <summary>Probability that each packed loop becomes black-and-white during an active Fill.</summary>
    [Range(0f, 1f)] public float FillBlackAndWhiteProbability;

    /// <summary>Copies every AnimateLoops Sync Setting from another value.</summary>
    public void CopyFrom(AnimateLoopsSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        LoopTileHueStep = source.LoopTileHueStep;
        LoopHueAdvance = source.LoopHueAdvance;
        DistortionModeMinInclusive = source.DistortionModeMinInclusive;
        DistortionModeMaxExclusive = source.DistortionModeMaxExclusive;
        HueResponseMagnitude = source.HueResponseMagnitude;
        TimeWarpSeconds = source.TimeWarpSeconds;
        TimeWarpHueScale = source.TimeWarpHueScale;
        DropDensity = source.DropDensity;
        DropSpeed = source.DropSpeed;
        DropBrightness = source.DropBrightness;
        FillBlackAndWhiteProbability = source.FillBlackAndWhiteProbability;
    }
}
