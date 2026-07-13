using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Colors packed loop, starball, and star shape groups with layered petal-style palettes.
/// </summary>
public class Petals : ScreenEffect
{
    /// <summary>Petals' gentle bloom suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>The Waveform this Effect owns and evaluates for its local rhythm responses.</summary>
    private Waveform waveform;

    private Color[] colors;
    private float background;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() { return $""; }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        colors = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            colors[i] = Color.HSVToRGB(Random.value, Random.value, 1f);
        }
        background = Random.value;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // This Effect owns its brightness, hue, and clockless fallback mappings.
        float? rhythm = waveforms.Evaluate(waveform);
        float beatBrightness = rhythm is { } envelope ? Mathf.Lerp(0.85f, 1f, envelope) : 1f;
        float hueShift = 0.001f * (rhythm ?? 0f);
        int bitMask=Random.Range(0, 256);       // randomly select shape layers with bit masks
        background += effectDelta * 0.1f;
        background %= 1f;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.HSVToRGB(background, 1f, 1f) * beatBrightness;
        }
        for (int shapeIdx = 0; shapeIdx < 3; shapeIdx++)
        {
            int[] shape = penrose.Layout.shapes.loops;
            switch (shapeIdx)
            {
                case 0:
                    shape = penrose.Layout.shapes.loops;
                    break;
                case 1:
                    shape = penrose.Layout.shapes.starballs;
                    break;
                case 2:
                    shape = penrose.Layout.shapes.stars;
                    break;
            }
            for (int i = 0; i < shape[0]; i++)
            {
                int thisBit=1<<1;
                int list = shape[i + 1];
                int start = list + 1;
                int end = start + shape[list];
                Color.RGBToHSV(colors[shapeIdx], out float hue, out float sat, out float bri);
                if((thisBit&bitMask)==0)
                    hue+=hueShift;
                for (int j = start; j < end; j++)
                {
                    int idx = shape[j];
                    buffer[idx] = Color.HSVToRGB((hue + 0.002f * j) % 1f, sat, bri) * beatBrightness;
                }
                colors[shapeIdx] = Color.HSVToRGB((hue + 0.00004f) % 1f, sat, bri);
            }
        }
    }

}