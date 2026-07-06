using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flashes randomly selected packed Penrose shape lists.
/// </summary>
public class TileShapes : EffectBase
{
    /// <summary>TileShapes' snapping shapes accent Fills and suit Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    private bool randomColor;
    private float hue;
    private int[] shape;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => randomColor ? "Color: random" : $"hue: {hue}";

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
        }
        else
        {
            randomColor = false;
            hue=Random.value;
        }

        switch (Random.Range(0, 9))
        {
            case 0:
                shape = penrose.Layout.shapes.lines0;
                break;
            case 1:
                shape = penrose.Layout.shapes.lines1;
                break;
            case 2:
                shape = penrose.Layout.shapes.lines2;
                break;
            case 3:
                shape = penrose.Layout.shapes.lines3;
                break;
            case 4:
                shape = penrose.Layout.shapes.lines4;
                break;
            case 5:
                shape = penrose.Layout.shapes.loops;
                break;
            case 6:
                shape = penrose.Layout.shapes.lotusballs;
                break;
            case 7:
                shape = penrose.Layout.shapes.starballs;
                break;
            case 8:
                shape = penrose.Layout.shapes.stars;
                break;
        }

        var text = (randomColor) ? "random" : hue.ToString();
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
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.75f, beatEnable);
        float hueShift = beatManager.GetBeatBrightness(beatVariant, 0.25f, 0.0f);
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        count = count / 5;
        for (int i = 0; i < count; i++)
        {
            Color color = Color.HSVToRGB(hue+hueShift, 1f, 1f);

            if (randomColor)
                color = Color.HSVToRGB(Random.value, 1f, 1f)* beatBrightness;


            int loop = Random.Range(0, shape[0]);
            int list = shape[1 + loop];
            int start = list + 1;
            int end = start + shape[list];
            for (int j = start; j < end; j++)
            {
                int idx = shape[j];
                if (idx >= 0)
                    buffer[idx] = color;
            }
        }
    }
}
