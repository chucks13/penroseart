using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class colorNibblerEffect : Effect {
    int last = 0;
    int current = 1;

    public override void Init(controller controller)
    {
        base.Init(controller);
    }
    void fadeall()
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] *= 0.98f;
    }
    public override void Draw(controller controller)
    {
        fadeall();
        for (int x = 0; x < 5; x++)
        {
            for (int i = 0; i < 40; i++)
            {
                int to = controller.geometry.tiles[current].neighbors[Random.Range(0, 4)];
                if (to < 0)
                    continue;
                if (to == last)
                    continue;
                current = to;
                buffer[current] = Color.HSVToRGB(Random.Range(0, 256) / 256f, 1, 1);
                break;
            }
        }
    }
}
