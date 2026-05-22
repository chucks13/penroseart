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

    /// <summary>Catalog/display name for this blender. Currently the C# type name.</summary>
    public string Name => GetType().ToString();

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
