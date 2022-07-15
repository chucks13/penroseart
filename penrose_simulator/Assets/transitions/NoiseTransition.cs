using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class NoiseTransition : TransitionBase
{
    private Color border;
    public override void OnStart()
    {
        buffer.Clear();
        border = Color.HSVToRGB(Random.value, 1, 1);
    }

    public override void OnEnd() { }

    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        float v2 =V.Map(0f,1f,-1.1f,1.1f);
 
        for (int i = 0; i < buffer.Length; i++)
        {
            float scale = 0.07f; //(1.0f + (controller.dance.decay * 0.25f)) * setting.scale;
            float x = controller.penrose.tiles[i].center.x * scale;
            float y = controller.penrose.tiles[i].center.y * scale;
            float z = controller.dance.fixedTime; // * setting.speed;

            float n = Perlin.Noise(x, y, z);
            n += v2;

            if (n > 0.1)
                buffer[i] = controller.effects[B].buffer[i];
            else if (n > -0.1)
                buffer[i] = border;
            else
                buffer[i] = controller.effects[A].buffer[i];
        }
    }
}
