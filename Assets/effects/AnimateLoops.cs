using System;
using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Animates packed Penrose Ring and Arc groups over a background color.
/// </summary>
[EffectSyncSettings(typeof(AnimateLoopsSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(AnimateLoopsStandaloneSettingsAsset))]
public class AnimateLoops : EffectBase
{
    // Standalone Defaults

    /// <summary>Authored background hue advance per second for the unchanged Standalone look.</summary>
    private const float StandaloneBackgroundHueRate = 0.1f;

    /// <summary>Authored hue step between Tiles within each packed Ring or Arc for the unchanged Standalone look.</summary>
    private const float StandaloneRingTileHueStep = 0.01f;

    /// <summary>Authored per-frame hue advance for each Ring or Arc's stored color in the unchanged Standalone look.</summary>
    private const float StandaloneRingHueAdvance = 0.01f;

    /// <summary>Authored inclusive lower bound of the Standalone distortion-mode roll; 1 selects Color.</summary>
    private const int StandaloneDistortionModeMinInclusive = 1;

    /// <summary>Authored exclusive upper bound of the Standalone distortion-mode roll; 1 is Color and 2 is Time.</summary>
    private const int StandaloneDistortionModeMaxExclusive = 3;

    // Sync Defaults

    /// <summary>Authored Synced Mode counterpart to the background hue advance used in Standalone Mode.</summary>
    private const float SyncBackgroundHueRate = 0.1f;

    /// <summary>Authored Synced Mode counterpart to the hue step between Tiles within each packed Ring or Arc.</summary>
    private const float SyncRingTileHueStep = 0.01f;

    /// <summary>Authored Synced Mode counterpart to each Ring or Arc's per-frame stored-color hue advance.</summary>
    private const float SyncRingHueAdvance = 0.01f;

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

    /// <summary>
    /// Current fixed hue step between consecutive Tile indexes in the Drop background. The unfinished
    /// alternative would roll from 0.0004f through 0.003f; keeping the fixed value preserves the approved
    /// look and Random consumption.
    /// </summary>
    private const float SyncDropTileHueStep = 0.001f;

    /// <summary>
    /// Current fixed Drop background hue rate in cycles per second. The unfinished alternative would roll
    /// from 0.1f through 1f; keeping the fixed value preserves the approved look and Random consumption.
    /// </summary>
    private const float SyncDropHueRate = 0.5f;

    /// <summary>Authored value supplied to the Drop background's HSV brightness slot.</summary>
    private const float SyncDropBrightness = 10f;

    /// <summary>Probability that each Ring or Arc becomes black-and-white during an active Fill.</summary>
    private const float SyncFillBlackAndWhiteProbability = 0.125f;

    /// <summary>AnimateLoops' looping motion suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings cannot mutate AnimateLoops' authored
    /// Standalone Defaults.
    /// </summary>
    public static AnimateLoopsStandaloneSettings StandaloneDefaults => new()
    {
        BackgroundHueRate = StandaloneBackgroundHueRate,
        RingTileHueStep = StandaloneRingTileHueStep,
        RingHueAdvance = StandaloneRingHueAdvance,
        DistortionMode = new IntRange(
            StandaloneDistortionModeMinInclusive,
            StandaloneDistortionModeMaxExclusive),
    };

    /// <summary>Resolves a fresh copy of AnimateLoops' file-local Sync Defaults.</summary>
    public static AnimateLoopsSyncSettings SyncDefaults => new()
    {
        BackgroundHueRate = SyncBackgroundHueRate,
        RingTileHueStep = SyncRingTileHueStep,
        RingHueAdvance = SyncRingHueAdvance,
        DistortionMode = new IntRange(
            SyncDistortionModeMinInclusive,
            SyncDistortionModeMaxExclusive),
        HueResponseMagnitude = SyncHueResponseMagnitude,
        TimeWarpSeconds = SyncTimeWarpSeconds,
        TimeWarpHueScale = SyncTimeWarpHueScale,
        DropTileHueStep = SyncDropTileHueStep,
        DropHueRate = SyncDropHueRate,
        DropBrightness = SyncDropBrightness,
        FillBlackAndWhiteProbability = SyncFillBlackAndWhiteProbability,
    };

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private AnimateLoopsStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private AnimateLoopsSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Per-group colors advanced across the packed Ring and Arc data.</summary>
    private Color[] colors;

    /// <summary>Background hue advanced continuously while this effect runs.</summary>
    private float background;

    /// <summary>Allocation-free access to the packed Ring and Arc groups supplied by the Penrose layout.</summary>
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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(AnimateLoops),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(AnimateLoops),
            SyncDefaults);
        waveform = waveforms.Random();
        shape = penrose.Layout.shapes.Rings;
        IntRange distortionModeRange = beatManager.IsSynced
            ? SyncSettings.DistortionMode
            : standaloneSettings.DistortionMode;
        distortionMode = Random.Range(
            distortionModeRange.MinInclusive,
            distortionModeRange.MaxExclusive);
        shapeName = "rings";
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
        float ringTileHueStep = isSynced
            ? SyncSettings.RingTileHueStep
            : standaloneSettings.RingTileHueStep;
        float ringHueAdvance = isSynced
            ? SyncSettings.RingHueAdvance
            : standaloneSettings.RingHueAdvance;
        float timeWarpHueScale = SyncSettings.TimeWarpHueScale;
        float dropTileHueStep = SyncSettings.DropTileHueStep;
        float dropHueRate = SyncSettings.DropHueRate;
        float dropBrightness = SyncSettings.DropBrightness;
        float fillBlackAndWhiteProbability = SyncSettings.FillBlackAndWhiteProbability;
        float hueShift = 0f;
        float sampleTime = effectTime;
        int groupCount = shape.GroupCount;

        // This effect owns both response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (distortionMode == 1)
            hueShift = SyncSettings.HueResponseMagnitude * rhythm;
        else if (distortionMode == 2)
            sampleTime = effectTime + (SyncSettings.TimeWarpSeconds * rhythm);

        float beatOffset = sampleTime - effectTime;
        colors[Random.Range(0, groupCount)] = Color.HSVToRGB(Random.value, Random.value, 1f);
        background += effectDelta * backgroundHueRate;
        background %= 1f;
        bool dropActive = beatManager.Drop.Active;
        if (dropActive)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                float phase = (i * dropTileHueStep + (effectTime * dropHueRate)) % 1f;
                buffer[i] = Color.HSVToRGB(phase, 1f, dropBrightness);
            }
        }
        else
        {
            Color backgroundColor = Color.HSVToRGB(
                (background + beatOffset * timeWarpHueScale + hueShift) % 1f,
                1f,
                1f);
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = backgroundColor;
            }
        }

        bool fillActive = beatManager.Fill.Active;
        for (int i = 0; i < groupCount; i++)
        {
            LayoutData.ShapeList.Group group = shape.GetGroup(i);
            Color.RGBToHSV(colors[i], out float hue, out float sat, out float bri);
            if (fillActive)
            {
                sat = Random.value < fillBlackAndWhiteProbability ? 0f : 1f; // B&W on fills
            }

            for (int j = 0; j < group.TileCount; j++)
            {
                int idx = group[j];
                buffer[idx] = Color.HSVToRGB(
                    (hue + ringTileHueStep * group.PackedIndex(j) + beatOffset * timeWarpHueScale + hueShift) % 1f,
                    sat,
                    bri);
            }
            colors[i] = Color.HSVToRGB((hue + ringHueAdvance) % 1f, sat, bri);
        }
    }

}

/// <summary>
/// The serializable value shape shared by AnimateLoops' fully populated Standalone Defaults and
/// saved Standalone Settings; Unity may create an empty instance before serialized values apply.
/// </summary>
[Serializable]
public sealed class AnimateLoopsStandaloneSettings
{
    /// <summary>Background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Hue step between Tiles within each packed Ring or Arc.</summary>
    public float RingTileHueStep;

    /// <summary>Per-frame hue advance for each packed Ring or Arc's stored color.</summary>
    public float RingHueAdvance;

    /// <summary>Per-activation range selecting Color or Time distortion.</summary>
    public IntRange DistortionMode;

    /// <summary>Copies every AnimateLoops Standalone Setting, including distortion-mode Rails.</summary>
    public void CopyFrom(AnimateLoopsStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        RingTileHueStep = source.RingTileHueStep;
        RingHueAdvance = source.RingHueAdvance;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
    }
}

/// <summary>The saved-or-default musical-response settings used by AnimateLoops in Synced Mode.</summary>
[Serializable]
public sealed class AnimateLoopsSyncSettings
{
    /// <summary>Live Synced Mode background hue advance per second.</summary>
    public float BackgroundHueRate;

    /// <summary>Live Synced Mode hue step between Tiles within each packed Ring or Arc.</summary>
    public float RingTileHueStep;

    /// <summary>Live Synced Mode per-frame hue advance for each packed Ring or Arc's stored color.</summary>
    public float RingHueAdvance;

    /// <summary>Per-activation range selecting Color or Time distortion.</summary>
    public IntRange DistortionMode;

    /// <summary>Maximum hue response applied by Color distortion.</summary>
    [Min(0f)] public float HueResponseMagnitude;

    /// <summary>Maximum seconds added to sampled effect time by Time distortion.</summary>
    [Min(0f)] public float TimeWarpSeconds;

    /// <summary>Scale from the Time distortion's sampled-time offset into hue.</summary>
    [Min(0f)] public float TimeWarpHueScale;

    /// <summary>Hue step between consecutive Tile indexes in the active Drop background.</summary>
    [Min(0f)] public float DropTileHueStep;

    /// <summary>Drop background hue cycles advanced per second.</summary>
    [Min(0f)] public float DropHueRate;

    /// <summary>Value supplied to the Drop background's HSV brightness slot.</summary>
    [Min(0f)] public float DropBrightness;

    /// <summary>Probability that each packed Ring or Arc becomes black-and-white during an active Fill.</summary>
    [Range(0f, 1f)] public float FillBlackAndWhiteProbability;

    /// <summary>Copies every AnimateLoops Sync Setting from another value.</summary>
    public void CopyFrom(AnimateLoopsSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        BackgroundHueRate = source.BackgroundHueRate;
        RingTileHueStep = source.RingTileHueStep;
        RingHueAdvance = source.RingHueAdvance;
        DistortionMode = new IntRange(
            source.DistortionMode.MinInclusive,
            source.DistortionMode.MaxExclusive,
            source.DistortionMode.LowRail,
            source.DistortionMode.HighRail);
        HueResponseMagnitude = source.HueResponseMagnitude;
        TimeWarpSeconds = source.TimeWarpSeconds;
        TimeWarpHueScale = source.TimeWarpHueScale;
        DropTileHueStep = source.DropTileHueStep;
        DropHueRate = source.DropHueRate;
        DropBrightness = source.DropBrightness;
        FillBlackAndWhiteProbability = source.FillBlackAndWhiteProbability;
    }
}
