﻿using UnityEngine;

public class NoiseMixer : MixerBase
{

    private EffectBase[] effects;
    private Color border;

    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    public override void Init()
    {
        base.Init();
    }

    public override void OnStart()
    {
        base.OnStart();
        effects = new EffectBase[2];

        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            effects[i].beatEnable = false; // Active Mixer: suppress children pulses
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
            border = Color.HSVToRGB(Random.value, 1, 1);
        }

        controller.debugText.text = debugText;
    }

    public override void OnEnd() { }

    public override void Draw()
    {
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            effects[i].Draw();
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float scale = 0.07f;
            float x = tiles[i].center.x * scale;
            float y = tiles[i].center.y * scale;
            float z = effectTime; // use local mixer time

            float n = Perlin.Noise(x, y, z);
            if (n > 0.1)
                buffer[i] = effects[0].buffer[i];
            else if (n > -0.1)
                buffer[i] = border;
            else
                buffer[i] = effects[1].buffer[i];
        }
    }

}