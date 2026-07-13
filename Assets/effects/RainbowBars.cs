using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders directional palette bars in screen space and maps them to Penrose tiles.
/// </summary>
public class RainbowBars : ScreenEffect
{
    /// <summary>RainbowBars' scrolling bands suit Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;


    /// <summary>The consumer-owned Waveform used by this activation's distortion mode.</summary>
    private Waveform waveform;

    private float sampleTime;

    private Direction direction;
    private int distortionMode; // 0: Brightness, 1: Color, 2: Time

    /// <summary>
    /// Called ever frame to update the debug UI text element
    /// </summary>
    /// <returns></returns>
    public override string DebugText() { return $"{direction.ToString()}"; }


    /// <summary>
    /// Called once when effect is created
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        waveform = synth.Random();
        direction = (Direction)Random.Range(0, 8);
        distortionMode = Random.Range(0, 3);
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }
    private Color getColor(float n)
    {
        return APalette.read((n + sampleTime) % 1f, true);
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        float beatBrightness = 1.0f;
        float hueShift = 0.0f;
        sampleTime = effectTime;

        // This effect owns all three response mappings and their clockless fallbacks.
        float? rhythm = synth.Evaluate(waveform);
        if (distortionMode == 0)
            beatBrightness = rhythm is { } envelope ? Mathf.Lerp(0.85f, 1f, envelope) : 1f;
        else if (distortionMode == 1)
            hueShift = 0.25f * (rhythm ?? 0f);
        else if (distortionMode == 2)
            sampleTime = effectTime + (0.5f * (rhythm ?? 0f));

        var color = Color.clear;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {

                switch (direction)
                {
                    case Direction.Up:
                        color = getColor(x + y * -0.1f);
                        break;
                    default:
                    case Direction.UpLeft:
                        color = getColor(x * 0.1f + y * -0.1f);
                        break;
                    case Direction.UpRight:
                        color = getColor(x * -0.1f + y * -0.1f);
                        break;
                    case Direction.Down:
                        color = getColor(x + y * 0.1f);
                        break;
                    case Direction.DownLeft:
                        color = getColor(x * 0.1f + y * 0.1f);
                        break;
                    case Direction.DownRight:
                        color = getColor(x * -0.1f + y * 0.1f);
                        break;
                    case Direction.Left:
                        color = getColor(x * 0.1f + y);
                        break;
                    case Direction.Right:
                        color = getColor(x * -0.1f + y);
                        break;

                }
                Color c = color;
                if (hueShift > 0)
                {
                    float h, s, v_col;
                    Color.RGBToHSV(c, out h, out s, out v_col);
                    c = Color.HSVToRGB((h + hueShift) % 1f, s, v_col);
                }

                screenBuffer[x + (y * width)] =  c * beatBrightness;
            }
        }

        // convert the 2D Matrix buffer to a tile buffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
    }
}
