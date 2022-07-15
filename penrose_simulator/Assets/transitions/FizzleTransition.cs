using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
public class FizzleTransition : TransitionBase
{
    short[] order=null;
    public override void OnStart()
    {
        buffer.Clear();
    }

    public FizzleTransition()
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

    public void OnInit()
    {
        // schuffle

    }

    public override void OnEnd() { }

    public override void Draw()
    {
        if (order == null)
            return;
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        short total = (short)(Penrose.Total * V);

        for (int i = 0; i < Penrose.Total; i++)
        {
            if (order[i] > total)
                buffer[i] = controller.effects[A].buffer[i];
            else
                buffer[i] = controller.effects[B].buffer[i];
        }

    }
}
