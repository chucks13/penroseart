﻿﻿﻿using Random = UnityEngine.Random;
using UnityEngine;

public class ColorSparkle : EffectBase
{
    private bool randomColor;
    private Color color;

    public override string DebugText() => randomColor ? "Color: random" : $"Color: {color.ToString()}";

    public override void Init()
    {
        base.Init();
    }

    // Should be called every time an effect is turned on
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

        var text = (randomColor) ? "random" : color.ToString();
        controller.debugText.text = $"Color: {text}";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    public override void OnEnd() { }

    public override void Draw()
    {
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        for (int i = 0; i < count; i++)
        {
            Color drawColor;
            if (randomColor)
                drawColor = Color.HSVToRGB(Random.value, 1f, 1f);
            else
                drawColor = color;

            buffer[Random.Range(0, buffer.Length)] = drawColor * beatBrightness;
        }
    }
}