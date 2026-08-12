using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Overlays blinking or fading Penrose shape highlights on top of one child effect.
/// </summary>
[EffectSyncSettings(typeof(ShapeGlitchSyncSettingsAsset))]
[EffectStandaloneSettings(typeof(ShapeGlitchStandaloneSettingsAsset))]
public class ShapeGlitch : MixerBase
{
    // Standalone Defaults

    /// <summary>Authored minimum outcome for the per-activation Blink/Fade Roll.</summary>
    private const int StandaloneModeRollMin = 0;

    /// <summary>Authored exclusive maximum outcome for the per-activation Blink/Fade Roll.</summary>
    private const int StandaloneModeRollMaxExclusive = 2;

    /// <summary>Authored minimum number of packed-shape highlights rolled per activation.</summary>
    private const int StandaloneHighlightCountMin = 10;

    /// <summary>Authored exclusive maximum number of packed-shape highlights rolled per activation.</summary>
    private const int StandaloneHighlightCountMaxExclusive = 50;

    /// <summary>Authored minimum outcome for the per-activation packed-shape Roll.</summary>
    private const int StandaloneShapeRollMin = 0;

    /// <summary>Authored exclusive maximum outcome for the per-activation packed-shape Roll.</summary>
    private const int StandaloneShapeRollMaxExclusive = 9;

    /// <summary>Authored HSV value used for the randomly rolled highlight color.</summary>
    private const float StandaloneHighlightColorValue = 1f;

    /// <summary>Authored minimum outcome for the per-frame highlight spawn Roll.</summary>
    private const int StandaloneSpawnRollMin = 0;

    /// <summary>Authored exclusive spawn-roll ceiling before the current highlight count is subtracted.</summary>
    private const int StandaloneSpawnRollCeilingExclusive = 50;

    /// <summary>Authored intensity assigned when a packed-shape highlight spawns.</summary>
    private const float StandaloneHighlightInitialIntensity = 1f;

    /// <summary>Authored intensity removed from each active fading highlight per rendered frame.</summary>
    private const float StandaloneFadeIntensityPerFrame = 0.005f;

    /// <summary>Authored intensity added to each active blinking highlight per rendered frame.</summary>
    private const float StandaloneBlinkIntensityPerFrame = 1f;

    /// <summary>Authored intensity limit after which a blinking highlight turns off.</summary>
    private const float StandaloneBlinkIntensityLimit = 15f;

    /// <summary>Authored hue drift applied on each packed-shape step.</summary>
    private const float StandaloneHueDriftPerShape = 0.00004f;

    // Sync Defaults

    /// <summary>Authored Synced Mode minimum outcome for the per-activation Blink/Fade Roll.</summary>
    private const int SyncModeRollMin = 0;

    /// <summary>Authored Synced Mode exclusive maximum outcome for the per-activation Blink/Fade Roll.</summary>
    private const int SyncModeRollMaxExclusive = 2;

    /// <summary>Authored Synced Mode minimum number of packed-shape highlights rolled per activation.</summary>
    private const int SyncHighlightCountMin = 10;

    /// <summary>Authored Synced Mode exclusive maximum number of packed-shape highlights rolled per activation.</summary>
    private const int SyncHighlightCountMaxExclusive = 50;

    /// <summary>Authored Synced Mode minimum outcome for the per-activation packed-shape Roll.</summary>
    private const int SyncShapeRollMin = 0;

    /// <summary>Authored Synced Mode exclusive maximum outcome for the per-activation packed-shape Roll.</summary>
    private const int SyncShapeRollMaxExclusive = 9;

    /// <summary>Authored Synced Mode HSV value used for the randomly rolled highlight color.</summary>
    private const float SyncHighlightColorValue = 1f;

    /// <summary>Authored Synced Mode minimum outcome for the per-frame highlight spawn Roll.</summary>
    private const int SyncSpawnRollMin = 0;

    /// <summary>Authored Synced Mode exclusive spawn-roll ceiling before the current highlight count is subtracted.</summary>
    private const int SyncSpawnRollCeilingExclusive = 50;

    /// <summary>Authored Synced Mode intensity assigned when a packed-shape highlight spawns.</summary>
    private const float SyncHighlightInitialIntensity = 1f;

    /// <summary>
    /// Authored Synced Mode intensity removed from each active fading highlight per rendered frame.
    /// </summary>
    private const float SyncFadeIntensityPerFrame = 0.005f;

    /// <summary>
    /// Authored Synced Mode intensity added to each active blinking highlight per rendered frame.
    /// </summary>
    private const float SyncBlinkIntensityPerFrame = 1f;

    /// <summary>Authored Synced Mode intensity limit after which a blinking highlight turns off.</summary>
    private const float SyncBlinkIntensityLimit = 15f;

    /// <summary>Authored Synced Mode hue drift applied on each packed-shape step.</summary>
    private const float SyncHueDriftPerShape = 0.00004f;

    /// <summary>
    /// Resolves a fresh copy so saved Standalone Settings can never mutate ShapeGlitch's authored
    /// Standalone Defaults.
    /// </summary>
    public static ShapeGlitchStandaloneSettings StandaloneDefaults => new ShapeGlitchStandaloneSettings
    {
        ModeRoll = new IntRange(StandaloneModeRollMin, StandaloneModeRollMaxExclusive),
        HighlightCount = new IntRange(StandaloneHighlightCountMin, StandaloneHighlightCountMaxExclusive),
        ShapeRoll = new IntRange(StandaloneShapeRollMin, StandaloneShapeRollMaxExclusive),
        HighlightColorValue = StandaloneHighlightColorValue,
        SpawnRoll = new IntRange(StandaloneSpawnRollMin, StandaloneSpawnRollCeilingExclusive),
        HighlightInitialIntensity = StandaloneHighlightInitialIntensity,
        FadeIntensityPerFrame = StandaloneFadeIntensityPerFrame,
        BlinkIntensityPerFrame = StandaloneBlinkIntensityPerFrame,
        BlinkIntensityLimit = StandaloneBlinkIntensityLimit,
        HueDriftPerShape = StandaloneHueDriftPerShape,
    };

    /// <summary>Resolves a fresh copy of ShapeGlitch's file-local Sync Defaults.</summary>
    public static ShapeGlitchSyncSettings SyncDefaults => new ShapeGlitchSyncSettings
    {
        ModeRoll = new IntRange(SyncModeRollMin, SyncModeRollMaxExclusive),
        HighlightCount = new IntRange(SyncHighlightCountMin, SyncHighlightCountMaxExclusive),
        ShapeRoll = new IntRange(SyncShapeRollMin, SyncShapeRollMaxExclusive),
        HighlightColorValue = SyncHighlightColorValue,
        SpawnRoll = new IntRange(SyncSpawnRollMin, SyncSpawnRollCeilingExclusive),
        HighlightInitialIntensity = SyncHighlightInitialIntensity,
        FadeIntensityPerFrame = SyncFadeIntensityPerFrame,
        BlinkIntensityPerFrame = SyncBlinkIntensityPerFrame,
        BlinkIntensityLimit = SyncBlinkIntensityLimit,
        HueDriftPerShape = SyncHueDriftPerShape,
    };

    /// <summary>ShapeGlitch's stutter/glitch bursts accent Fills and suit Mid/High-energy sections.</summary>
    /// This is a filter. it intentionall isnt beat awaire. It's meant to be used with beat-aware effects
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private enum Mode
    {
        Blink,
        Fade
    }

    /// <summary>
    /// Active packed-shape highlight with color, fade, and repeat state.
    /// </summary>
    public class Highlight
    {
        public float intensity;
        public int index;
    }

    /// <summary>The effective saved-or-default Standalone Settings read by the current activation.</summary>
    private ShapeGlitchStandaloneSettings standaloneSettings = StandaloneDefaults;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private ShapeGlitchSyncSettings SyncSettings { get; set; } = SyncDefaults;

    private EffectBase effect;

    /// <summary>The allocation-free Penrose Shape List reader rolled for this activation.</summary>
    private LayoutData.ShapeList.Reader shape;

    private Color color;
    private Mode mode;
    private Highlight[] highlights;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = $"Effect: {effect.Name}\n"
         + $"Mode: {mode}\n"
         + $"Shape Count {highlights.Length}";
        return debugText;
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
        standaloneSettings = EffectStandaloneSettingsProvider.Resolve(
            typeof(ShapeGlitch),
            StandaloneDefaults);
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(ShapeGlitch),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        IntRange modeRoll = isSynced ? SyncSettings.ModeRoll : standaloneSettings.ModeRoll;
        IntRange highlightCount = isSynced ? SyncSettings.HighlightCount : standaloneSettings.HighlightCount;
        IntRange shapeRoll = isSynced ? SyncSettings.ShapeRoll : standaloneSettings.ShapeRoll;
        float highlightColorValue = isSynced ? SyncSettings.HighlightColorValue : standaloneSettings.HighlightColorValue;

        switch (Random.Range(modeRoll.MinInclusive, modeRoll.MaxExclusive))
        {
            case 0:
                mode = Mode.Blink;
                break;
            case 1:
                mode = Mode.Fade;
                break;
        }
        highlights = new Highlight[Random.Range(
            highlightCount.MinInclusive,
            highlightCount.MaxExclusive)];
        for (int i = 0; i < highlights.Length; i++)
        {
            highlights[i] = new Highlight();
        }
        switch (Random.Range(shapeRoll.MinInclusive, shapeRoll.MaxExclusive))
        {
            case 0:
                shape = penrose.Layout.shapes.Lines0;
                break;
            case 1:
                shape = penrose.Layout.shapes.Lines1;
                break;
            case 2:
                shape = penrose.Layout.shapes.Lines2;
                break;
            case 3:
                shape = penrose.Layout.shapes.Lines3;
                break;
            case 4:
                shape = penrose.Layout.shapes.Lines4;
                break;
            case 5:
                shape = penrose.Layout.shapes.Loops;
                break;
            case 6:
                shape = penrose.Layout.shapes.Lotusballs;
                break;
            case 7:
                shape = penrose.Layout.shapes.Starballs;
                break;
            case 8:
                shape = penrose.Layout.shapes.Stars;
                break;
        }
        color = Color.HSVToRGB(Random.value, Random.value, highlightColorValue);

        effect = GetRandomEffect();
        effect.RandomizeTime();
        effect.Init();
        effect.OnStart();
        // Passive composition: the child keeps the Waveform it acquired for itself.
        var debugText = $"{effect.Name}";
        controller.debugText.text = debugText;
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

        effect.UpdateTime();
        effect.Draw();

        for (int i = 0; i < buffer.Length; i++)
        {

            float r = 0f, g = 0f, b = 0f;
            r += effect.buffer[i].r;
            g += effect.buffer[i].g;
            b += effect.buffer[i].b;
            buffer[i] = new Color(r, g, b, 1f);
        }

        bool isSynced = beatManager.IsSynced;
        IntRange spawnRoll = isSynced ? SyncSettings.SpawnRoll : standaloneSettings.SpawnRoll;
        float highlightInitialIntensity = isSynced ? SyncSettings.HighlightInitialIntensity : standaloneSettings.HighlightInitialIntensity;
        float fadeIntensityPerFrame = isSynced ? SyncSettings.FadeIntensityPerFrame : standaloneSettings.FadeIntensityPerFrame;
        float blinkIntensityPerFrame = isSynced ? SyncSettings.BlinkIntensityPerFrame : standaloneSettings.BlinkIntensityPerFrame;
        float blinkIntensityLimit = isSynced ? SyncSettings.BlinkIntensityLimit : standaloneSettings.BlinkIntensityLimit;
        float hueDriftPerShape = isSynced ? SyncSettings.HueDriftPerShape : standaloneSettings.HueDriftPerShape;

        if (Random.Range(
            spawnRoll.MinInclusive,
            spawnRoll.MaxExclusive - highlights.Length) == spawnRoll.MinInclusive)
        {
            Highlight newlyCreated = highlights[Random.Range(0, highlights.Length)];
            newlyCreated.intensity = highlightInitialIntensity;
            newlyCreated.index = Random.Range(0, shape.GroupCount);
        }
        for (int i = 0; i < shape.GroupCount; i++)
        {
            for (int h = 0; h < highlights.Length; h++)
            {
                if (highlights[h].index == i)
                {
                    LayoutData.ShapeList.Group group = shape.GetGroup(i);
                    for (int j = 0; j < group.TileCount; j++)
                    {
                        int idx = group[j];
                        float intensity = highlights[h].intensity;
                        if (intensity > 1f) { intensity = 1f; }
                        buffer[idx] = color * intensity + buffer[idx] * (1f - intensity);
                    }
                    if (mode == Mode.Fade && highlights[h].intensity > 0f)
                    {
                        highlights[h].intensity -= fadeIntensityPerFrame;
                        if (highlights[h].intensity < 0f)
                        {
                            highlights[h].intensity = 0f;
                        }
                    }
                    if (mode == Mode.Blink && highlights[h].intensity > 0f)
                    {
                        highlights[h].intensity += blinkIntensityPerFrame;
                        if (highlights[h].intensity > blinkIntensityLimit)
                        {
                            highlights[h].intensity = 0f;
                        }
                    }

                }
            }
            Color.RGBToHSV(color, out float hue, out float sat, out float bri);
            color = Color.HSVToRGB((hue + hueDriftPerShape) % 1f, sat, bri);
        }
    }

}

/// <summary>
/// The serializable value shape shared by ShapeGlitch's Standalone Defaults and saved Standalone
/// Settings.
/// </summary>
[Serializable]
public sealed class ShapeGlitchStandaloneSettings
{
    /// <summary>Per-activation range selecting the Blink or Fade mode.</summary>
    public IntRange ModeRoll;

    /// <summary>Per-activation range for the number of packed-shape highlights.</summary>
    public IntRange HighlightCount;

    /// <summary>Per-activation range selecting the packed Shape List.</summary>
    public IntRange ShapeRoll;

    /// <summary>HSV value used for the randomly rolled highlight color.</summary>
    public float HighlightColorValue;

    /// <summary>Per-frame spawn-roll range whose maximum is reduced by the live highlight count.</summary>
    public IntRange SpawnRoll;

    /// <summary>Intensity assigned when a packed-shape highlight spawns.</summary>
    public float HighlightInitialIntensity;

    /// <summary>Intensity removed from each active fading highlight per rendered frame.</summary>
    public float FadeIntensityPerFrame;

    /// <summary>Intensity added to each active blinking highlight per rendered frame.</summary>
    public float BlinkIntensityPerFrame;

    /// <summary>Intensity limit after which a blinking highlight turns off.</summary>
    public float BlinkIntensityLimit;

    /// <summary>Hue drift applied on each packed-shape step.</summary>
    public float HueDriftPerShape;

    /// <summary>Copies every ShapeGlitch Standalone Setting, including range endpoints and Rails.</summary>
    public void CopyFrom(ShapeGlitchStandaloneSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ModeRoll = CopyRange(source.ModeRoll);
        HighlightCount = CopyRange(source.HighlightCount);
        ShapeRoll = CopyRange(source.ShapeRoll);
        HighlightColorValue = source.HighlightColorValue;
        SpawnRoll = CopyRange(source.SpawnRoll);
        HighlightInitialIntensity = source.HighlightInitialIntensity;
        FadeIntensityPerFrame = source.FadeIntensityPerFrame;
        BlinkIntensityPerFrame = source.BlinkIntensityPerFrame;
        BlinkIntensityLimit = source.BlinkIntensityLimit;
        HueDriftPerShape = source.HueDriftPerShape;
    }

    /// <summary>Creates an independent copy of one integer range and its tuning Rails.</summary>
    private static IntRange CopyRange(IntRange source)
    {
        return new IntRange(
            source.MinInclusive,
            source.MaxExclusive,
            source.LowRail,
            source.HighRail);
    }
}

/// <summary>
/// Serializable carrier for ShapeGlitch's Sync Settings.
/// These Sync Settings are exposed for live Play Mode tuning.
/// </summary>
[Serializable]
public sealed class ShapeGlitchSyncSettings
{
    /// <summary>Synced Mode per-activation range selecting the Blink or Fade mode.</summary>
    public IntRange ModeRoll;

    /// <summary>Synced Mode per-activation range for the number of packed-shape highlights.</summary>
    public IntRange HighlightCount;

    /// <summary>Synced Mode per-activation range selecting the packed Shape List.</summary>
    public IntRange ShapeRoll;

    /// <summary>HSV value used for the randomly rolled Synced Mode highlight color.</summary>
    [Range(0f, 1f)] public float HighlightColorValue;

    /// <summary>Synced Mode spawn-roll range whose maximum is reduced by the live highlight count.</summary>
    public IntRange SpawnRoll;

    /// <summary>Intensity assigned when a Synced Mode packed-shape highlight spawns.</summary>
    [Min(0f)] public float HighlightInitialIntensity;

    /// <summary>Intensity removed from each active fading highlight per rendered frame.</summary>
    [Min(0f)] public float FadeIntensityPerFrame;

    /// <summary>Intensity added to each active blinking highlight per rendered frame.</summary>
    [Min(0f)] public float BlinkIntensityPerFrame;

    /// <summary>Intensity limit after which a blinking Synced Mode highlight turns off.</summary>
    [Min(0f)] public float BlinkIntensityLimit;

    /// <summary>Synced Mode hue drift applied on each packed-shape step.</summary>
    [Min(0f)] public float HueDriftPerShape;

    /// <summary>Copies every ShapeGlitch Sync Setting from another value.</summary>
    public void CopyFrom(ShapeGlitchSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ModeRoll = CopyRange(source.ModeRoll);
        HighlightCount = CopyRange(source.HighlightCount);
        ShapeRoll = CopyRange(source.ShapeRoll);
        HighlightColorValue = source.HighlightColorValue;
        SpawnRoll = CopyRange(source.SpawnRoll);
        HighlightInitialIntensity = source.HighlightInitialIntensity;
        FadeIntensityPerFrame = source.FadeIntensityPerFrame;
        BlinkIntensityPerFrame = source.BlinkIntensityPerFrame;
        BlinkIntensityLimit = source.BlinkIntensityLimit;
        HueDriftPerShape = source.HueDriftPerShape;
    }

    /// <summary>Creates an independent copy of one integer range and its tuning Rails.</summary>
    private static IntRange CopyRange(IntRange source)
    {
        return new IntRange(
            source.MinInclusive,
            source.MaxExclusive,
            source.LowRail,
            source.HighRail);
    }
}
