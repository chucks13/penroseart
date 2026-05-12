﻿using Random = UnityEngine.Random;
using UnityEngine;

public class ColorSparkle : EffectBase
{
    private bool randomColor;
    //    private Color color;
    private float hue;

    public override string DebugText() => randomColor ? "Color: random" : $"hue: {hue}";

    public override void Init()
    {
        base.Init();
    }

    // Should be called every time an effect is turned on
    public override void OnStart()
    {
        base.OnStart();
        randomColor = (Random.value > 0.5f);
        hue = Random.value;

        var text = (randomColor) ? "random " : hue.ToString();
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
        Color drawColor = Color.HSVToRGB((hue + beatBrightness) % 1.0f, 1f, 1f);
        for (int i = 0; i < count; i++)
        {
            if (randomColor && (!IsBeatActive))
                drawColor = Color.HSVToRGB(Random.value, 1f, 1f);

            buffer[Random.Range(0, buffer.Length)] = drawColor;// * beatBrightness;
        }
    }
}