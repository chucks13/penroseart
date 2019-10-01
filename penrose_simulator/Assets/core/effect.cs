using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Effect : ScriptableObject
{
    public Color[] buffer;
    public virtual void Init(controller controller)
    {
        buffer = new Color[600];
    }
    public virtual void Draw(controller controller)
    {
    }
}
