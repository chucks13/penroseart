﻿﻿﻿using System;
using UnityEngine;
using Random = UnityEngine.Random;


public class Tunnel : ScreenEffect
{

    private float density;
    private float speed;
    private float mix;

    public override void OnStart()
    {
        base.OnStart();
        density = Random.Range(0.0004f, 0.003f);
        speed = Random.Range(0.1f, 1f);
        mix = Random.Range(0.01f, 0.2f);
        buffer.Clear();
    }

    public override void OnEnd() { }

    public override string DebugText()
    {
        return $"Density: {density}\n" +
        $"Speed: {speed}\n" +
        $"Mix: {mix}\n";
    }
    public override void Init()
    {
        base.Init();
    }
  
    public override void Draw()
    {
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        for (int i = 0; i <  Penrose.Total; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * 0.03f);
            float y = Mathf.Abs(tiles[i].center.y * 0.03f);
            float distance = Mathf.Sqrt((x * x) + (y * y));
            var color = i * density + effectTime * speed + distance * mix;
            buffer[i] = Color.HSVToRGB(color % 1f, 1f, 1f) * beatBrightness;
        }
  }
}