using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
public class FizzleTransition : TransitionBase
{
    short[] order = null;
    public override void OnStart()
    {
        buffer.Clear();
    }
    public void OnInit()
    {
        if (order == null)
        {
            order = new short[Penrose.Total];
            for (short i = 0; i < order.Length; i++)
                order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int x = Random.Range(0, i);
                short y = order[i];     //swap
                order[i] = order[x];
                order[x] = y;
            }


        }
    }

    public FizzleTransition()
    {
        OnInit();
    }


    public override void OnEnd() { }

    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V);
    }

    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2)
    {
        if (order == null)
            return;

        short total = (short)(Penrose.Total * V2);

        for (int i = 0; i < Penrose.Total; i++)
        {
            if (order[i] > total)
                dest[i] = src1[i];
            else
                dest[i] = src2[i];
        }


    }

    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        float V2 = 0.5f;
        if (settings.Length > 0)
            V2 = settings[0];
        Draw2(dest, src1, src2, V2);
    }
    public override string Usage()
    {
        return "[ratio]";
    }


}
