using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class yinyangmixer : EffectBase
{

    private EffectBase[] effects;
    float yina;
    float spin;
    float drift;

    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    public override void Init()
    {
        base.Init();
        mixer = true;       // must come after base.Init();
    }


    public override void OnStart()
    {
        effects = new EffectBase[2];
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].Init();
            effects[i].OnStart();
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }
        controller.debugText.text = debugText;
        spin = 6f;
        spin *= (Random.value < 0.5) ? -1f : 1f;
        drift = 6f;
        drift *= (Random.value < 0.5) ? -1f : 1f;
    }

    public override void OnEnd() { }

    public override void Draw()
    {
        
        yina += spin * controller.dance.deltaTime * 60f ;
        for (int i = 0; i < 2; i++)
        {
            effects[i].Draw();
        }
        Color ribbon = APalette.read(0.5f, true);

        for (int i = 0; i < buffer.Length; i++)
        {
            float w = 20f;
            float a = tiles[i].angle;
            float r = tiles[i].radius* drift;
            a += yina;
            a += r;
            a += 360000f;
            a %= 360f;


            if (a < w)
            {
                buffer[i] = ribbon;
                continue;
            }
            if (a > 360-w)
            {
                buffer[i] = ribbon;
                continue;
            }
            if (a < (180f - w))
            {
                buffer[i] = effects[0].buffer[i];
                continue;
            }
            if (a > (180f + w))
            {
                buffer[i] = effects[1].buffer[i];
                continue;
            }
            buffer[i] = ribbon;
        }
    }

}