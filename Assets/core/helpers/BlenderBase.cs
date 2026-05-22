using UnityEngine;
using System;


public abstract class BlenderBase
{
    // Defaults to no fader arguments so Blend() implementations can safely use
    // settings.Length before telnet or other controls provide values.
    public float[] settings = Array.Empty<float>();

    public string Name => GetType().ToString();
    public void setFaders(string[] stringArray)
    {
        settings = Array.ConvertAll(stringArray, float.Parse);

    }

    public abstract void Blend(Color[] dest, Color[] src1, Color[] src2);
    public abstract string Usage();
}
