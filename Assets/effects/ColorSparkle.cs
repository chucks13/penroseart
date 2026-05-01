﻿using Random = UnityEngine.Random;
using UnityEngine;

public class ColorSparkle : EffectBase
{

    private Settings setting;
    private Color color;

    public override string DebugText() => setting.randomColor ? "Color: random" : $"Color: {setting.color.ToString()}";

    public override void Init()
    {
        base.Init();
        setting = new Settings();
    }

    // Should be called every time an effect is turned on
    public override void OnStart()
    {
        base.OnStart();
        if (controller.effectSettings.sparkle.Length > 0)
            setting = controller.effectSettings.sparkle[Random.Range(0, controller.effectSettings.sparkle.Length)];
        else
            setting.Randomize();

        var text = (setting.randomColor) ? "random" : setting.color.ToString();
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
            if (setting.randomColor)
                color = Color.HSVToRGB(Random.value, 1f, 1f);
            else
                color = setting.color;

            buffer[Random.Range(0, buffer.Length)] = color * beatBrightness;
        }
    }





    [System.Serializable]
    public class Settings
    {
        public bool randomColor = true;
        public Color color;

        public void Randomize()
        {
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
        }
    }

}