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


    /// <summary>The screen-space direction used to sample the palette bands.</summary>
    private Direction direction;

    /// <summary>Which beat response this activation applies: brightness, color, or time.</summary>
    private int distortionMode; // 0: Brightness, 1: Color, 2: Time

    /// <summary>
    /// Called ever frame to update the debug UI text element
    /// </summary>
    /// <returns></returns>
    public override string DebugText() => direction.ToString();


    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        direction = (Direction)Random.Range(0, 8);
        distortionMode = Random.Range(0, 3);
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }

    /// <summary>Samples the scrolling palette at a screen-space position.</summary>
    private static Color GetColor(float position, float sampleTime)
    {
        return APalette.read((position + sampleTime) % 1f, true);
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        float beatBrightness = 1.0f;
        float hueShift = 0.0f;
        float sampleTime = effectTime;

        // This effect owns all three response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        if (distortionMode == 0)
            beatBrightness = waveform.Lerp(0.85f, 1f);
        else if (distortionMode == 1)
            hueShift = 0.25f * rhythm;
        else if (distortionMode == 2)
            sampleTime = effectTime + (0.5f * rhythm);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float samplePosition = direction switch
                {
                    Direction.Up => x + (y * -0.1f),
                    Direction.UpRight => (x * -0.1f) + (y * -0.1f),
                    Direction.Down => x + (y * 0.1f),
                    Direction.DownLeft => (x * 0.1f) + (y * 0.1f),
                    Direction.DownRight => (x * -0.1f) + (y * 0.1f),
                    Direction.Left => (x * 0.1f) + y,
                    Direction.Right => (x * -0.1f) + y,
                    _ => (x * 0.1f) + (y * -0.1f),
                };

                Color color = GetColor(samplePosition, sampleTime);
                if (hueShift > 0)
                {
                    Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                    color = Color.HSVToRGB((hue + hueShift) % 1f, saturation, value);
                }

                screenBuffer[x + (y * width)] = color * beatBrightness;
            }
        }

        // convert the 2D Matrix buffer to a tile buffer
        ConvertScreenBuffer(ref screenBuffer, in buffer);
    }
}
