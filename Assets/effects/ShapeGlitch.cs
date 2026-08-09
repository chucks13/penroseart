using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Overlays blinking or fading Penrose shape highlights on top of one child effect.
/// </summary>
[EffectSyncSettings(typeof(ShapeGlitchSyncSettingsAsset))]
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

    /// <summary>Authored intensity removed from a fading highlight on each packed-shape step.</summary>
    private const float StandaloneFadeIntensityStep = 0.005f;

    /// <summary>Authored intensity added to a blinking highlight on each packed-shape step.</summary>
    private const float StandaloneBlinkIntensityStep = 1f;

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

    /// <summary>Authored Synced Mode intensity removed from a fading highlight on each packed-shape step.</summary>
    private const float SyncFadeIntensityStep = 0.005f;

    /// <summary>Authored Synced Mode intensity added to a blinking highlight on each packed-shape step.</summary>
    private const float SyncBlinkIntensityStep = 1f;

    /// <summary>Authored Synced Mode intensity limit after which a blinking highlight turns off.</summary>
    private const float SyncBlinkIntensityLimit = 15f;

    /// <summary>Authored Synced Mode hue drift applied on each packed-shape step.</summary>
    private const float SyncHueDriftPerShape = 0.00004f;

    /// <summary>Resolves a fresh immutable-by-convention copy of ShapeGlitch's Standalone Defaults.</summary>
    public static ShapeGlitchStandaloneSettings StandaloneSettings => new ShapeGlitchStandaloneSettings
    {
        ModeRollMin = StandaloneModeRollMin,
        ModeRollMaxExclusive = StandaloneModeRollMaxExclusive,
        HighlightCountMin = StandaloneHighlightCountMin,
        HighlightCountMaxExclusive = StandaloneHighlightCountMaxExclusive,
        ShapeRollMin = StandaloneShapeRollMin,
        ShapeRollMaxExclusive = StandaloneShapeRollMaxExclusive,
        HighlightColorValue = StandaloneHighlightColorValue,
        SpawnRollMin = StandaloneSpawnRollMin,
        SpawnRollCeilingExclusive = StandaloneSpawnRollCeilingExclusive,
        HighlightInitialIntensity = StandaloneHighlightInitialIntensity,
        FadeIntensityStep = StandaloneFadeIntensityStep,
        BlinkIntensityStep = StandaloneBlinkIntensityStep,
        BlinkIntensityLimit = StandaloneBlinkIntensityLimit,
        HueDriftPerShape = StandaloneHueDriftPerShape,
    };

    /// <summary>Resolves a fresh copy of ShapeGlitch's file-local Sync Defaults.</summary>
    public static ShapeGlitchSyncSettings SyncDefaults => new ShapeGlitchSyncSettings
    {
        ModeRollMin = SyncModeRollMin,
        ModeRollMaxExclusive = SyncModeRollMaxExclusive,
        HighlightCountMin = SyncHighlightCountMin,
        HighlightCountMaxExclusive = SyncHighlightCountMaxExclusive,
        ShapeRollMin = SyncShapeRollMin,
        ShapeRollMaxExclusive = SyncShapeRollMaxExclusive,
        HighlightColorValue = SyncHighlightColorValue,
        SpawnRollMin = SyncSpawnRollMin,
        SpawnRollCeilingExclusive = SyncSpawnRollCeilingExclusive,
        HighlightInitialIntensity = SyncHighlightInitialIntensity,
        FadeIntensityStep = SyncFadeIntensityStep,
        BlinkIntensityStep = SyncBlinkIntensityStep,
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

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private ShapeGlitchStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private ShapeGlitchSyncSettings SyncSettings { get; set; } = SyncDefaults;

    private EffectBase effect;
    private int[] shape;
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
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(ShapeGlitch),
            SyncDefaults);

        bool isSynced = beatManager.IsSynced;
        int modeRollMin = isSynced ? SyncSettings.ModeRollMin : standaloneSettings.ModeRollMin;
        int modeRollMaxExclusive = isSynced ? SyncSettings.ModeRollMaxExclusive : standaloneSettings.ModeRollMaxExclusive;
        int highlightCountMin = isSynced ? SyncSettings.HighlightCountMin : standaloneSettings.HighlightCountMin;
        int highlightCountMaxExclusive = isSynced ? SyncSettings.HighlightCountMaxExclusive : standaloneSettings.HighlightCountMaxExclusive;
        int shapeRollMin = isSynced ? SyncSettings.ShapeRollMin : standaloneSettings.ShapeRollMin;
        int shapeRollMaxExclusive = isSynced ? SyncSettings.ShapeRollMaxExclusive : standaloneSettings.ShapeRollMaxExclusive;
        float highlightColorValue = isSynced ? SyncSettings.HighlightColorValue : standaloneSettings.HighlightColorValue;

        switch (Random.Range(modeRollMin, modeRollMaxExclusive))
        {
            case 0:
                mode = Mode.Blink;
                break;
            case 1:
                mode = Mode.Fade;
                break;
        }
        highlights = new Highlight[Random.Range(
            highlightCountMin,
            highlightCountMaxExclusive)];
        for (int i = 0; i < highlights.Length; i++)
        {
            highlights[i] = new Highlight();
        }
        switch (Random.Range(shapeRollMin, shapeRollMaxExclusive))
        {
            case 0:
                shape = penrose.Layout.shapes.lines0;
                break;
            case 1:
                shape = penrose.Layout.shapes.lines1;
                break;
            case 2:
                shape = penrose.Layout.shapes.lines2;
                break;
            case 3:
                shape = penrose.Layout.shapes.lines3;
                break;
            case 4:
                shape = penrose.Layout.shapes.lines4;
                break;
            case 5:
                shape = penrose.Layout.shapes.loops;
                break;
            case 6:
                shape = penrose.Layout.shapes.lotusballs;
                break;
            case 7:
                shape = penrose.Layout.shapes.starballs;
                break;
            case 8:
                shape = penrose.Layout.shapes.stars;
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
        int spawnRollMin = isSynced ? SyncSettings.SpawnRollMin : standaloneSettings.SpawnRollMin;
        int spawnRollCeilingExclusive = isSynced ? SyncSettings.SpawnRollCeilingExclusive : standaloneSettings.SpawnRollCeilingExclusive;
        float highlightInitialIntensity = isSynced ? SyncSettings.HighlightInitialIntensity : standaloneSettings.HighlightInitialIntensity;
        float fadeIntensityStep = isSynced ? SyncSettings.FadeIntensityStep : standaloneSettings.FadeIntensityStep;
        float blinkIntensityStep = isSynced ? SyncSettings.BlinkIntensityStep : standaloneSettings.BlinkIntensityStep;
        float blinkIntensityLimit = isSynced ? SyncSettings.BlinkIntensityLimit : standaloneSettings.BlinkIntensityLimit;
        float hueDriftPerShape = isSynced ? SyncSettings.HueDriftPerShape : standaloneSettings.HueDriftPerShape;

        if (Random.Range(
            spawnRollMin,
            spawnRollCeilingExclusive - highlights.Length) == spawnRollMin)
        {
            Highlight newlyCreated = highlights[Random.Range(0, highlights.Length)];
            newlyCreated.intensity = highlightInitialIntensity;
            newlyCreated.index = Random.Range(0, shape[0]);
        }
        for (int i = 0; i < shape[0]; i++)
        {
            for (int h = 0; h < highlights.Length; h++)
            {
                if (highlights[h].index == i)
                {
                    int list = shape[i + 1];
                    int start = list + 1;
                    int end = start + shape[list];
                    for (int j = start; j < end; j++)
                    {
                        int idx = shape[j];
                        float intensity = highlights[h].intensity;
                        if (intensity > 1f) { intensity = 1f; }
                        buffer[idx] = color * intensity + buffer[idx] * (1f - intensity);
                    }
                    if (mode == Mode.Fade && highlights[h].intensity > 0f)
                    {
                        highlights[h].intensity -= fadeIntensityStep;
                        if (highlights[h].intensity < 0f)
                        {
                            highlights[h].intensity = 0f;
                        }
                    }
                    if (mode == Mode.Blink && highlights[h].intensity > 0f)
                    {
                        highlights[h].intensity += blinkIntensityStep;
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

/// <summary>The non-editable Standalone Settings that reproduce ShapeGlitch's authored no-music look.</summary>
public sealed class ShapeGlitchStandaloneSettings
{
    /// <summary>Inclusive minimum for the per-activation Blink/Fade Roll.</summary>
    public int ModeRollMin;

    /// <summary>Exclusive maximum for the per-activation Blink/Fade Roll.</summary>
    public int ModeRollMaxExclusive;

    /// <summary>Inclusive minimum number of packed-shape highlights per activation.</summary>
    public int HighlightCountMin;

    /// <summary>Exclusive maximum number of packed-shape highlights per activation.</summary>
    public int HighlightCountMaxExclusive;

    /// <summary>Inclusive minimum for the per-activation packed-shape Roll.</summary>
    public int ShapeRollMin;

    /// <summary>Exclusive maximum for the per-activation packed-shape Roll.</summary>
    public int ShapeRollMaxExclusive;

    /// <summary>HSV value used for the randomly rolled highlight color.</summary>
    public float HighlightColorValue;

    /// <summary>Inclusive minimum for the per-frame highlight spawn Roll.</summary>
    public int SpawnRollMin;

    /// <summary>Exclusive spawn-roll ceiling before the current highlight count is subtracted.</summary>
    public int SpawnRollCeilingExclusive;

    /// <summary>Intensity assigned when a packed-shape highlight spawns.</summary>
    public float HighlightInitialIntensity;

    /// <summary>Intensity removed from a fading highlight on each packed-shape step.</summary>
    public float FadeIntensityStep;

    /// <summary>Intensity added to a blinking highlight on each packed-shape step.</summary>
    public float BlinkIntensityStep;

    /// <summary>Intensity limit after which a blinking highlight turns off.</summary>
    public float BlinkIntensityLimit;

    /// <summary>Hue drift applied on each packed-shape step.</summary>
    public float HueDriftPerShape;
}

/// <summary>
/// Serializable carrier for ShapeGlitch's Sync Settings.
/// These Sync Settings are exposed for live Play Mode tuning.
/// </summary>
[Serializable]
public sealed class ShapeGlitchSyncSettings
{
    /// <summary>Inclusive minimum for the Synced Mode per-activation Blink/Fade Roll.</summary>
    [Range(0, 1)] public int ModeRollMin;

    /// <summary>Exclusive maximum for the Synced Mode per-activation Blink/Fade Roll.</summary>
    [Range(1, 2)] public int ModeRollMaxExclusive;

    /// <summary>Inclusive minimum number of Synced Mode packed-shape highlights per activation.</summary>
    [Min(1)] public int HighlightCountMin;

    /// <summary>Exclusive maximum number of Synced Mode packed-shape highlights per activation.</summary>
    [Min(2)] public int HighlightCountMaxExclusive;

    /// <summary>Inclusive minimum for the Synced Mode per-activation packed-shape Roll.</summary>
    [Range(0, 8)] public int ShapeRollMin;

    /// <summary>Exclusive maximum for the Synced Mode per-activation packed-shape Roll.</summary>
    [Range(1, 9)] public int ShapeRollMaxExclusive;

    /// <summary>HSV value used for the randomly rolled Synced Mode highlight color.</summary>
    [Range(0f, 1f)] public float HighlightColorValue;

    /// <summary>Inclusive minimum for the Synced Mode per-frame highlight spawn Roll.</summary>
    [Min(0)] public int SpawnRollMin;

    /// <summary>Exclusive Synced Mode spawn-roll ceiling before the current highlight count is subtracted.</summary>
    [Min(2)] public int SpawnRollCeilingExclusive;

    /// <summary>Intensity assigned when a Synced Mode packed-shape highlight spawns.</summary>
    [Min(0f)] public float HighlightInitialIntensity;

    /// <summary>Intensity removed from a fading Synced Mode highlight on each packed-shape step.</summary>
    [Min(0f)] public float FadeIntensityStep;

    /// <summary>Intensity added to a blinking Synced Mode highlight on each packed-shape step.</summary>
    [Min(0f)] public float BlinkIntensityStep;

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

        ModeRollMin = source.ModeRollMin;
        ModeRollMaxExclusive = source.ModeRollMaxExclusive;
        HighlightCountMin = source.HighlightCountMin;
        HighlightCountMaxExclusive = source.HighlightCountMaxExclusive;
        ShapeRollMin = source.ShapeRollMin;
        ShapeRollMaxExclusive = source.ShapeRollMaxExclusive;
        HighlightColorValue = source.HighlightColorValue;
        SpawnRollMin = source.SpawnRollMin;
        SpawnRollCeilingExclusive = source.SpawnRollCeilingExclusive;
        HighlightInitialIntensity = source.HighlightInitialIntensity;
        FadeIntensityStep = source.FadeIntensityStep;
        BlinkIntensityStep = source.BlinkIntensityStep;
        BlinkIntensityLimit = source.BlinkIntensityLimit;
        HueDriftPerShape = source.HueDriftPerShape;
    }
}
