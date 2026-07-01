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
    public static AnimPalette APalette = new AnimPalette();

    /// <summary>Current shared beat data from the controller's BeatManager.</summary>
    
    /// <summary>Shared beat helper used for rhythmic effect behavior.</summary>
    public BeatManager beatManager => controller.beatManager;
    /// <summary>Whether beat-reactive behavior should currently affect this effect.</summary>
    public bool IsBeatActive => controller.beatManager.IsActive;

    /// <summary>Whether this effect should apply beat brightness/time/color behavior.</summary>
    public bool beatEnable = true;
    /// <summary>Rhythmic personality selected during OnStart().</summary>
    public int beatVariant;

    /// <summary>Last frame's 16-beat Grid Count (1..16), so the wrap onto a new Grid can be edge-detected for <see cref="OnNewGrid"/>. 0 = off the grid last frame.</summary>
    protected int lastGridCount;

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

    /// <summary>
    /// One-time setup after reflection creates the effect. Binds Controller, Penrose, tile data, and the 900-color buffer.
    /// </summary>
    public virtual void Init()
    {
        factory = new Factory<EffectBase>();
        controller = Controller.Instance;
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
        DetectNewGrid();
    }

    /// <summary>
    /// Edge-detects the downbeat of each new 16-beat Grid (Count wraps to 1) and fires <see cref="OnNewGrid"/>
    /// once, gated on a Locked reading so a Coasting/Contradicted grid does not trigger. Driven from
    /// <see cref="UpdateTime"/>, so it runs once per active frame just before Draw. <see cref="BeatManager.Grid"/>
    /// is null off the beat clock, so this never fires in Standalone. Note: effects nested inside a mixer only
    /// receive this if the mixer forwards <see cref="UpdateTime"/> to them.
    /// </summary>
    private void DetectNewGrid()
    {
        // Null-safe on the whole beat context, not just Grid: UpdateTime() runs on every active frame
        // (and on effects a mixer forwards to), but a Performer may have no controller/BeatManager bound
        // yet — e.g. a lightweight test double, or any frame before Init(). No beat context is the same as
        // "off the beat clock": Grid is absent, so this no-ops exactly as it does in Standalone.
        var grid = controller?.beatManager?.Grid;
        int count = grid?.Count ?? 0;
        if (count == 1 && lastGridCount != 1 && grid?.Confidence == GridSyncState.Locked)
        {
            OnNewGrid();
        }
        lastGridCount = count;
    }

    /// <summary>
    /// Called once on the downbeat of each new 16-beat Grid the Director has Locked. Default does nothing;
    /// effects override to re-roll their look, switch palette, pick a new Waveform variant, and the like.
    /// </summary>
    protected virtual void OnNewGrid() { }

    /// <summary>
    /// Per-activation setup called every time Controller or a mixer turns this effect on.
    /// </summary>
    public virtual void OnStart()
    {
        beatEnable = true;
        beatVariant = beatManager.GetRandomVariant();
        // Arm the Grid edge to the current Grid so activating on a downbeat does not fire OnNewGrid immediately.
        lastGridCount = beatManager.Grid?.Count ?? 0;
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
        return effect;
    }

}
