using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flashes randomly selected packed Penrose shape lists.
/// </summary>
public class TileShapes : EffectBase
{
    private bool randomColor;
    private Color color;
    private int[] shape;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => randomColor ? "Color: random" : $"Color: {color.ToString()}";

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    // Should be called every time an effect is turned on
    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
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

        switch (Random.Range(0, 9))
        {
            case 0:
                shape = penrose.JsonRawData.shapes.lines0;
                break;
            case 1:
                shape = penrose.JsonRawData.shapes.lines1;
                break;
            case 2:
                shape = penrose.JsonRawData.shapes.lines2;
                break;
            case 3:
                shape = penrose.JsonRawData.shapes.lines3;
                break;
            case 4:
                shape = penrose.JsonRawData.shapes.lines4;
                break;
            case 5:
                shape = penrose.JsonRawData.shapes.loops;
                break;
            case 6:
                shape = penrose.JsonRawData.shapes.lotusballs;
                break;
            case 7:
                shape = penrose.JsonRawData.shapes.starballs;
                break;
            case 8:
                shape = penrose.JsonRawData.shapes.stars;
                break;
        }

        var text = (randomColor) ? "random" : color.ToString();
        controller.debugText.text = $"Color: {text}";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales randomly selected shape flashes.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        count = count / 5;
        for (int i = 0; i < count; i++)
        {

            if (randomColor)
                color = Color.HSVToRGB(Random.value, 1f, 1f);
            else
                color = color; // Logic remains the same, using the class variable


            int loop = Random.Range(0, shape[0]);
            int list = shape[1 + loop];
            int start = list + 1;
            int end = start + shape[list];
            for (int j = start; j < end; j++)
            {
                int idx = shape[j];
                if (idx >= 0)
                    buffer[idx] = color * beatBrightness;
            }
        }
    }
}
