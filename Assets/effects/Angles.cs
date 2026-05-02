﻿﻿﻿using Random = UnityEngine.Random;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Angles : EffectBase
{
    private Color[] colors;
    private float speed;

    public override string DebugText() => "Angles";

    public override void Init()
    {
        base.Init();
    }

    // Should be called every time an effect is turned on
    public override void OnStart()
    {
        base.OnStart();
        speed = 0.25f;
        controller.debugText.text = "Angles";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    public override void OnEnd() { }

    public override void Draw()
    {
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        for (int i = 0; i < buffer.Length; i++)
        {
            Penrose.TileData t = tiles[i];
            {
                float angle = t.tileangle/180f;
                angle += effectTime * speed;
                Color c = Color.HSVToRGB(angle % 1f, 1f, 1f);
                buffer[i] = c * beatBrightness;
            }
        }
    }
}
