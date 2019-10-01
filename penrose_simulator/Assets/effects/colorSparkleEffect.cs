using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class colorSparkleEffect : Effect
{
    public override void Init(controller controller)
    {
        base.Init(controller);
    }
    void fadeall()
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] *= 0.98f;
    }
    public override void Draw(controller controller)
    {
        fadeall();
        for (int i = 0; i < 10; i++)
            buffer[Random.Range(0, 600)] = Color.HSVToRGB(Random.Range(0, 256) / 256f, 1, 1);
    }
}
