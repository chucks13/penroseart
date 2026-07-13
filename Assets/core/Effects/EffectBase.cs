using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Base contract for PenroseArt effects that render one 900-tile frame into a color buffer.
/// </summary>
/// <remarks>
/// Effects are plain C# objects created by Factory&lt;EffectBase&gt;. Controller calls Init once, OnStart on activation, UpdateTime and Draw every active frame.
/// </remarks>
[System.Serializable]
public abstract class EffectBase
{

    [HideInInspector]
    public Color[] buffer;
    // public int initialIndex;
    public Controller controller;
    private Factory<EffectBase> factory;

    public float effectTime;
    public float effectDelta;
    [HideInInspector]
    // public int sortIndex;

    protected Penrose penrose;
    protected Penrose.TileData[] tiles;
    public static AnimPalette APalette;

    /// <summary>The live BeatManager owned by the bound Controller.</summary>
    public BeatManager beatManager => controller.beatManager;

    /// <summary>The live Waveform Synthesizer owned by the bound Controller.</summary>
    public WaveformSynth synth => controller.synth;

    /// <summary>Whether beat-reactive behavior should currently affect this effect.</summary>
    public bool IsBeatActive => controller.beatManager.IsActive;

    /// <summary>Whether this effect should apply beat brightness/time/color behavior.</summary>
    public bool beatEnable = true;
    /// <summary>Rhythmic personality selected during OnStart().</summary>
    public int beatVariant;

    /// <summary>Catalog/display name for this effect. Currently the C# type name.</summary>
    public string Name => GetType().ToString();

    /// <summary>
    /// Musical-structure behavior this effect advertises to the Director.
    /// Subclasses override this when they can intentionally express Fill or Drop cues.
    /// </summary>
    public virtual Repertoire Repertoire => Repertoire.None;

    /// <summary>
    /// Text displayed in the debug UI while this effect is active.
    /// </summary>
    public abstract string DebugText();

    /// <summary>Loads the shared animated palette from Controller-owned palette data.</summary>
    public static void LoadPalette(string paletteSource)
    {
        APalette = new AnimPalette(paletteSource);
    }

    /// <summary>Binds this plain C# effect to the live scene Controller that owns runtime setup.</summary>
    public virtual void BindController(Controller owner)
    {
        if (owner == null)
        {
            throw new System.ArgumentNullException(nameof(owner));
        }

        controller = owner;
    }

    /// <summary>
    /// One-time setup after reflection creates the effect. Binds Penrose, tile data, and the 900-color buffer.
    /// </summary>
    public virtual void Init()
    {
        if (controller == null)
        {
            throw new System.InvalidOperationException($"{Name} must be bound to a Controller before Init().");
        }

        factory = new Factory<EffectBase>();
        penrose = controller.penrose;
        tiles = penrose.Tiles;
        buffer = new Color[Penrose.Total];
    }

    /// <summary>
    /// Seeds effectTime with a random offset so reactivated effects do not always start from the same phase.
    /// </summary>
    public void RandomizeTime()
    {
        // Seed with 0 to 4 hours (14400 seconds)
        effectTime = Random.Range(0f, 14400f);
    }

    /// <summary>
    /// Advances the effect's local clock from Unity's current frame delta.
    /// </summary>
    public void UpdateTime()
    {
        effectDelta = Time.deltaTime;
        effectTime += effectDelta;
        if (controller != null && beatManager.Grid.Wrapped)
        {
            OnNewGrid();
        }
    }

    /// <summary>
    /// Called exactly when the hub's <see cref="GridView.Wrapped"/> Edge is true. The base performs no response;
    /// concrete Effects may override this evidence-backed hook or read the Edge directly.
    /// </summary>
    protected virtual void OnNewGrid() { }

    /// <summary>
    /// Per-activation setup called every time Controller or a mixer turns this effect on.
    /// </summary>
    public virtual void OnStart()
    {
        beatEnable = true;
        beatVariant = beatManager.GetRandomVariant();
    }

    /// <summary>
    /// Contrived beat brightness multiplier for this effect, closing over <see cref="beatEnable"/> and
    /// <see cref="beatVariant"/>: Synced Mode pulses between <paramref name="minBrightness"/> and 1
    /// on this effect's Waveform; Standalone Mode (no beat clock, or beat response disabled) holds steady at 1.
    /// </summary>
    protected float BeatBrightness(float minBrightness = 0.5f)
    {
        return beatEnable && beatManager.Envelope(beatVariant) is { } envelope
            ? Mathf.Lerp(minBrightness, 1f, envelope)
            : 1f;
    }

    /// <summary>
    /// Beat-warped effect time, closing over <see cref="beatEnable"/>, <see cref="beatVariant"/>, and
    /// <see cref="effectTime"/>: Synced Mode kicks the clock forward by <paramref name="intensity"/> on
    /// the beat without changing the stored <see cref="effectTime"/>; Standalone Mode returns it unchanged.
    /// </summary>
    protected float BeatTime(float intensity = 0.2f)
    {
        return beatEnable && beatManager.Envelope(beatVariant) is { } envelope
            ? effectTime + (envelope * intensity)
            : effectTime;
    }

    /// <summary>
    /// Reserved for future effect deactivation cleanup. The current controller
    /// does not call OnEnd(); effects should not rely on it yet.
    /// </summary>
    public abstract void OnEnd();

    /// <summary>
    /// Renders one frame into <see cref="buffer"/>.
    /// </summary>
    public abstract void Draw();

    /// <summary>
    /// Creates a random non-identical effect instance for mixer/wrapper child use.
    /// </summary>
    public virtual EffectBase GetRandomEffect()
    {

        EffectBase effect;
        while (true)
        {
            effect = factory.Create(factory.Types[Random.Range(0, factory.Count)]);
            if (effect.Name == Name)
                continue;
            break;
        }
        effect.BindController(controller);
        return effect;
    }

}
