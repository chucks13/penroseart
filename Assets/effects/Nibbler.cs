﻿using UnityEngine;

[System.Serializable]
public class Nibbler : EffectBase
{

    private const int Count = 10;
    private int[] current;
    private bool randomColor;
    private Color color;
    private float fade;

    public override string DebugText()
    {
        var colorText = (randomColor) ? "random" : color.ToString();
        return $"Color: {colorText}\nFade: {fade}";
    }

    public override void Init()
    {
        base.Init();
        current = new int[Count];
        for (int i = 0; i < Count; i++) current[i] = Random.Range(0, Penrose.Total);
    }

    public override void OnStart()
    {
        base.OnStart();
        if (Random.value > 0.5f)
        {
            randomColor = true;
            color = Color.clear;
        }
        else
        {
            randomColor = false;
            color = Color.HSVToRGB(Random.value, 1f, 1f);
        }

        fade = Random.Range(0.97f, 0.999f);
        buffer.Clear();
    }

    public override void OnEnd() { }

    public override void Draw()
    {
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        buffer.Fade(fade);
        int count = (int)(effectDelta * 300f);
        for (int y = 0; y < Count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                current[y] = tiles[current[y]].GetRandomNeighbor();
                Color c = randomColor ? Color.HSVToRGB(Random.value, 1f, 1f) : color;
                
                buffer[current[y]] = c * beatBrightness;
            }
        }
    }
}