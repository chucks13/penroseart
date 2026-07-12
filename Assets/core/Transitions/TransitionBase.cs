using UnityEngine;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Base contract for effect-to-effect transitions and transition-as-external-blender implementations.
/// </summary>
/// <remarks>
/// Normal transitions blend controller.effects[A] to controller.effects[B] using progress V. Some subclasses also implement Blend for external pixel-source mixing.
/// </remarks>
[System.Serializable]
public abstract class TransitionBase
{

    [HideInInspector]
    public Color[] buffer;

    protected Controller controller;

    /// <summary>
    /// Shared beat helper used for rhythmic transition behavior, mirroring <see cref="EffectBase.beatManager"/>.
    /// Pull musical state through its nullable queries (raw and contrived: Envelope/Fill/Drop/Energy/Grid/Levels/Pulse/...).
    /// </summary>
    public BeatManager beatManager => controller.beatManager;

    /// <summary>The live Waveform Synthesizer owned by the bound Controller.</summary>
    public WaveformSynth synth => controller.synth;

    /// <summary>Catalog/display name for this transition. Currently the C# type name.</summary>
    public string Name => GetType().ToString();

    /// <summary>
    /// Code-authored baseline used when no saved Transition Settings asset exists and when restoring defaults.
    /// </summary>
    public TransitionSettings CodeDefaults => BuildCodeDefaults();

    /// <summary>
    /// Saved settings when present, otherwise this Transition's Code Defaults. This is read live so Play Mode edits affect future decisions.
    /// </summary>
    protected TransitionSettings EffectiveSettings => TransitionSettingsProvider.Resolve(GetType(), CodeDefaults);

    /// <summary>
    /// Musical-structure timing and event behavior this transition advertises to the Director.
    /// </summary>
    public virtual TransitionRepertoire Repertoire => EffectiveSettings.ToRepertoire();

    /// <summary>Builds this Transition's code-authored settings baseline.</summary>
    protected virtual TransitionSettings BuildCodeDefaults()
    {
        return TransitionSettings.FromRepertoire(TransitionRepertoire.Default);
    }

    private int a;
    private int b;
    private float v;
    public float effectTime;
    public float effectDelta;


    // Defaults to no fader arguments so Blend() implementations can safely use
    // settings.Length before telnet or other controls provide values.
    public float[] settings = Array.Empty<float>();

    /// <summary>
    /// Parses external blender/telnet fader arguments for transition-as-blender mode.
    /// </summary>
    public void setFaders(string[] stringArray)
    {
        settings = Array.ConvertAll(stringArray, float.Parse);
    }

    /// <summary>
    /// Optional external-source blending hook. Normal effect-to-effect transitions use Draw().
    /// </summary>
    public virtual void Blend(Color[] dest, Color[] src1, Color[] src2)
    {

    }

    /// <summary>
    /// Human-readable fader argument contract for external-source blending.
    /// </summary>
    public virtual string Usage()
    {
        return "(not implemented yet)";
    }


    /// <summary>Source effect index for normal effect-to-effect transitions.</summary>
    public int A
    {
        get => a;
        set
        {
            if (value >= 0 && value < controller.effects.Length) a = value;
        }
    }

    /// <summary>Destination effect index for normal effect-to-effect transitions.</summary>
    public int B
    {
        get => b;
        set
        {
            if (value >= 0 && value < controller.effects.Length) b = value;
        }
    }

    /// <summary>Transition progress clamped from 0 to 1.</summary>
    public float V
    {
        get => v;
        set => v = Mathf.Clamp01(value);
    }

    /// <summary>Remaining transition progress, equal to 1 - V.</summary>
    public float D => 1f - v;

    /// <summary>
    /// Text displayed in the debug UI while this transition is active.
    /// </summary>
    public virtual string DebugText() => $"{controller.effects[a].Name} ({D:0.00}) => {controller.effects[b].Name} ({v:0.00})";

    /// <summary>Binds this plain C# transition to the live scene Controller that owns runtime setup.</summary>
    public virtual void BindController(Controller owner)
    {
        if (owner == null)
        {
            throw new System.ArgumentNullException(nameof(owner));
        }

        controller = owner;
    }

    /// <summary>
    /// Put reusable setup here so Blend() is safe before the transition has
    /// been selected for a real effect-to-effect transition.
    /// </summary>
    public virtual void Init()
    {
        if (controller == null)
        {
            throw new System.InvalidOperationException($"{Name} must be bound to a Controller before Init().");
        }

        buffer = new Color[Penrose.Total];
    }

    /// <summary>
    /// Seeds effectTime with a random offset so each activation can start from a different phase.
    /// </summary>
    public void RandomizeTime()
    {
        // Seed with 0 to 4 hours (14400 seconds)
        effectTime = Random.Range(0f, 14400f);
    }

    /// <summary>
    /// Advances the transition's local clock from Unity's current frame delta.
    /// </summary>
    public void UpdateTime()
    {
        effectDelta = Time.deltaTime;
        effectTime += effectDelta;
    }

    /// <summary>
    /// Called each time this transition becomes the active effect-to-effect
    /// transition. Use it for per-run state such as random direction/color.
    /// </summary>
    public abstract void OnStart();

    /// <summary>
    /// Reserved for future transition deactivation cleanup. The current
    /// controller does not call OnEnd(); transitions should not rely on it yet.
    /// </summary>
    public abstract void OnEnd();

    /// <summary>
    /// Renders one transition frame into <see cref="buffer"/> using effects A and B.
    /// </summary>
    public abstract void Draw();

}