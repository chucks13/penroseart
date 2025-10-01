using UnityEngine;
using System;


public abstract class BlenderBase
{
    public float[] settings;

    public string Name => GetType().ToString();
    public void setFaders(string[] stringArray)
    {
        settings = Array.ConvertAll(stringArray, float.Parse);

    }

    public abstract void Blend(Color[] dest, Color[] src1, Color[] src2);
    public abstract string Usage();
}
