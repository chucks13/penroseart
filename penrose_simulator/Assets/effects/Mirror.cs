using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Mirror : EffectBase
{

    private EffectBase sourceEffect;
    private Factory<EffectBase> factory;
    private int[] mirrorList;

    public override string DebugText()
    {
        var debugText = string.Empty;
        debugText +=  $"{sourceEffect.Name}";
 
        return debugText;
    }

    public override void Init()
    {
        base.Init();
        factory = new Factory<EffectBase>();
    }

    private EffectBase GetRandomEffect()
    {
        var effect = factory.Create(factory.Types[Random.Range(0, factory.Count)]);
        return effect.Name == Name ? GetRandomEffect() : effect;
    }

    public override void OnStart()
    {
    mirrorList = Random.Range(0, 2) == 0 ? penrose.JsonRawData.shapes.mirror2 : penrose.JsonRawData.shapes.mirror10;

    sourceEffect = GetRandomEffect();
        var debugText = string.Empty;
        sourceEffect.Init();
        sourceEffect.OnStart();
        debugText += $"{sourceEffect.Name}";

        controller.debugText.text = debugText;
    }

    public override void OnEnd() { }

    public override void Draw()
    {

        sourceEffect.Draw();

        int groupcount = mirrorList[0];     // how many copies
        // Draw the mirrors
        for (int i = 0; i < groupcount; i++)
        {
            int groupPointer = mirrorList[1 + i];
            int groupsize = mirrorList[groupPointer];
            Color tileColor = sourceEffect.buffer[mirrorList[groupPointer + 1]];
            for (int j=0;j< groupsize;j++)
            {
                buffer[mirrorList[groupPointer + 1 + j]] = tileColor;
            }
        }
    }

}
