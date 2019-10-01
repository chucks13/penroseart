using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NoiseEffect: Effect
{
    public float noisescale;
    public float noisespeed;
    float amplifier;
    float colordelta;
    float timescale;

    public override void Init(controller controller)
    {
        noisescale = Random.Range(0.05f, 0.2f);
        noisespeed = Random.Range(0.1f, 1.5f); 
        base.Init(controller);
        amplifier = Random.Range(1, 5);
        colordelta = Random.value;
    }
    public override void Draw(controller controller)
    {
        tile[] tiles = controller.geometry.tiles;
        for (int i = 0; i < buffer.Length; i++)
        {
            float n = Perlin.Noise(tiles[i].center.x * noisescale, tiles[i].center.y * noisescale, Time.fixedTime * noisespeed);
            n *= amplifier;
 
            int v =(int) Mathf.Floor(n);
            if ((v & 1) == 0)
                buffer[i] = Color.HSVToRGB((n+ colordelta )% 1, 1, 1);
            else
                buffer[i] = Color.black;
        }
    }
}
