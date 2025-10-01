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
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, border);
    }

    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, Color brd)
    {
        float v2 = V2.Map(0f, 1f, -1.1f, 1.1f);

        for (int i = 0; i < buffer.Length; i++)
        {
            float scale = 0.07f; //(1.0f + (controller.dance.decay * 0.25f)) * setting.scale;
            float x = controller.penrose.tiles[i].center.x * scale;
            float y = controller.penrose.tiles[i].center.y * scale;
            float z = controller.dance.fixedTime; // * setting.speed;

            float n = Perlin.Noise(x, y, z);
            n += v2;

            if (n > 0.1)
                dest[i] = src2[i];
            else if (n > -0.1)
                dest[i] = brd;
            else
                dest[i] = src1[i];
        }
    }

    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        float V2 = 0.5f;
        Color brd = Color.HSVToRGB(0, 1, 1);
        if (settings.Length > 0)
            V2 = settings[0];
        if (settings.Length > 1)
            brd = brd = Color.HSVToRGB(settings[1], 1, 1);

        Draw2(dest, src1, src2, V2, brd);
    }
    public override string Usage()
    {
        return "[ratio] [borderHue]";
    }




}
