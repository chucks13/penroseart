using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Perlin-threshold transition with a colored border band.
/// </summary>
public class NoiseTransition : TransitionBase
{
    private Color border;
    public override void OnStart()
    {
        buffer.Clear();
        border = Color.HSVToRGB(Random.value, 1, 1);
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Draws source and destination effects and writes the transition frame into buffer.
    /// </summary>
    public override void Draw()
    {
        controller.effects[A].Draw();
        controller.effects[B].Draw();
        Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V, border);
    }

    /// <summary>
    /// Shared Perlin-threshold implementation for normal transitions and external blending.
    /// </summary>
    private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2, Color brd)
    {
        float v2 = V2.Map(0f, 1f, -1.1f, 1.1f);

        for (int i = 0; i < buffer.Length; i++)
        {
            float scale = 0.07f;
            float x = controller.penrose.tiles[i].center.x * scale;
            float y = controller.penrose.tiles[i].center.y * scale;
            float z = effectTime;

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

    /// <summary>
    /// Uses this transition algorithm as an external-source blender.
    /// </summary>
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
    /// <summary>
    /// Returns the external-blender fader argument format for this transition.
    /// </summary>
    public override string Usage()
    {
        return "[ratio] [borderHue]";
    }




}
