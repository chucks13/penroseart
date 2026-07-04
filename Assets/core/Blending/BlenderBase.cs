using UnityEngine;
using System;


/// <summary>
/// Base contract for objects that blend an external pixel source with the native Penrose buffer.
/// </summary>
public abstract class BlenderBase
{
    // Defaults to no fader arguments so Blend() implementations can safely use
    // settings.Length before telnet or other controls provide values.
    public float[] settings = Array.Empty<float>();

    protected Controller controller;

    /// <summary>
    /// Shared beat helper used for rhythmic blend behavior, mirroring <see cref="EffectBase.beatManager"/>
    /// and <see cref="TransitionBase.beatManager"/>. Pull musical state through its nullable
    /// queries (Envelope/Fill/Drop/Energy/Grid/Levels).
    /// </summary>
    public BeatManager beatManager => controller.beatManager;

    /// <summary>Catalog/display name for this blender. Currently the C# type name.</summary>
    public string Name => GetType().ToString();

    /// <summary>Binds this plain C# blender to the live scene Controller that owns runtime setup.</summary>
    public virtual void BindController(Controller owner)
    {
        if (owner == null)
        {
            throw new System.ArgumentNullException(nameof(owner));
        }

        controller = owner;
    }

    /// <summary>
    /// Called once after reflection creates the blender instance, after Controller binding.
    /// </summary>
    public virtual void Init()
    {
        if (controller == null)
        {
            throw new System.InvalidOperationException($"{Name} must be bound to a Controller before Init().");
        }
    }

    /// <summary>
    /// Parses external blender/telnet fader arguments into numeric settings.
    /// </summary>
    public void setFaders(string[] stringArray)
    {
        settings = Array.ConvertAll(stringArray, float.Parse);

    }

    /// <summary>
    /// Mixes source buffers into <paramref name="dest"/>. Callers pass the native buffer as <paramref name="src1"/> and external pixels as <paramref name="src2"/>.
    /// </summary>
    public abstract void Blend(Color[] dest, Color[] src1, Color[] src2);

    /// <summary>
    /// Human-readable fader argument contract for operator/debug commands.
    /// </summary>
    public abstract string Usage();
}
