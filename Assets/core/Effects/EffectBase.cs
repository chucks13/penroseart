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
    private int? previousGridBeat;
    private int? previousPhraseBeatsRemaining;
    [HideInInspector]
    // public int sortIndex;

    protected Penrose penrose;
    protected Penrose.TileData[] tiles;
    public static AnimPalette APalette;

    /// <summary>The live BeatManager owned by the bound Controller.</summary>
    public BeatManager beatManager => controller.beatManager;

    /// <summary>The live Waveform acquisition surface owned by the bound Controller.</summary>
    public Waveforms waveforms => controller.waveforms;

    /// <summary>
    /// Public artistic Waveform configuration for this Effect. Effects acquire values explicitly;
    /// owners such as Mixers may replace or share them, and use <see cref="Waveforms.None"/> for
    /// intentional response suppression.
    /// </summary>
    [System.NonSerialized]
    public Waveform waveform;

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
    /// Beats of drop-approach slowdown this effect wants applied to its own clock. Zero, the default,
    /// runs the clock at real time. Effects that lean into a Drop override this rather than rewriting
    /// <see cref="effectTime"/> inside Draw, so the clock is already correct by the time Draw runs.
    /// </summary>
    protected virtual int DropSlowdownBeats => 0;

    /// <summary>
    /// Advances the effect's local clock from Unity's current frame delta, slowed for an approaching
    /// Drop when <see cref="DropSlowdownBeats"/> asks for it, then raises the structural boundary
    /// hooks this effect observed on the way in.
    /// </summary>
    /// <remarks>
    /// The Grid is phrase-relative, so a phrase boundary is expected to restart it: an effect that
    /// overrides both hooks should expect them on the same frame at a phrase start.
    /// </remarks>
    public void UpdateTime()
    {
        effectDelta = Time.deltaTime;
        if (controller == null)
        {
            effectTime += effectDelta;
            return;
        }

        if (DropSlowdownBeats > 0)
        {
            effectDelta = DropSlowdown(effectDelta, DropSlowdownBeats);
        }
        effectTime += effectDelta;

        var gridBeat = beatManager.Grid.Beat;
        if (gridBeat == 1 && previousGridBeat is { } previous && previous != 1)
        {
            OnNewGrid();
        }
        previousGridBeat = gridBeat;

        // A phrase counts down to its own length, so its first beat is the frame the remaining count
        // returns to the full length. Name is not the test: consecutive phrases may share one.
        var phrase = beatManager.Phrase;
        var phraseBeatsRemaining = phrase.BeatsRemaining;
        if (phraseBeatsRemaining is { } remaining
            && remaining == phrase.LengthBeats
            && previousPhraseBeatsRemaining is { } previousRemaining
            && previousRemaining != remaining)
        {
            OnNewPhrase();
        }
        previousPhraseBeatsRemaining = phraseBeatsRemaining;
    }

    /// <summary>
    /// Shapes a caller-provided value through the Drop gesture: a linear slowdown to a full stop
    /// across the <paramref name="beats"/> approaching the Drop, then a 5x burst on the landing
    /// that decays back to the unshaped value across the same window into the Drop.
    /// </summary>
    protected float DropSlowdown(float value, int beats = 8)
    {
        value *= beatManager.Drop.Before.Decay(beats);
        // In.Decay rests at zero outside an active Drop, so the burst factor is identity there.
        value *= 1f + (4f * beatManager.Drop.In.Decay(beats));
        return value;
    }

    /// <summary>
    /// Called when this effect observes the timing-grid beat return to one after another placed beat.
    /// </summary>
    protected virtual void OnNewGrid() { }

    /// <summary>
    /// Called when this effect observes a new Phrase begin. Standalone Mode carries no Phrase, so
    /// this never fires there and an Effect that re-rolls here keeps its Standalone cadence on
    /// <see cref="OnNewGrid"/>.
    /// </summary>
    protected virtual void OnNewPhrase() { }

    /// <summary>
    /// Per-activation setup called every time Controller or a mixer turns this effect on. The base
    /// performs no musical acquisition or response; concrete Effects own those decisions.
    /// </summary>
    public virtual void OnStart() { }

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
