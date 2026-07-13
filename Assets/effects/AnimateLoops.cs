using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Animates packed Penrose loop shape groups over a background color.
/// </summary>
public class AnimateLoops : EffectBase
{
    /// <summary>AnimateLoops' looping motion suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;


    /// <summary>The consumer-owned Waveform used by this activation's distortion mode.</summary>
    private Waveform waveform;

    /// <summary>Per-loop colors advanced across the packed shape data.</summary>
    private Color[] colors;

    /// <summary>Background hue advanced continuously while this effect runs.</summary>
    private float background;

    /// <summary>The packed loop shape groups supplied by the Penrose layout.</summary>
    private int[] shape;

    /// <summary>The active packed-shape name shown in the debug readout.</summary>
    private string shapeName;

    /// <summary>Which beat response this activation applies: color or time.</summary>
    private int distortionMode; // 1: Color, 2: Time

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string modeName = distortionMode == 1 ? "Color" : "Time Warp";
        return $"shape: {shapeName}\nBeat Mode: {modeName}";
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        shape = penrose.Layout.shapes.loops;
        distortionMode = Random.Range(1, 3);
        shapeName = "loops";
        colors = new Color[shape[0]];
        for (int i = 0; i < shape[0]; i++)
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
        float hueShift = 0f;
        float sampleTime = effectTime;

        // This effect owns both response mappings and their clockless fallbacks.
        float? rhythm = waveforms.Evaluate(waveform);
        if (distortionMode == 1)
            hueShift = 0.25f * (rhythm ?? 0f);
        else if (distortionMode == 2)
            sampleTime = effectTime + (0.5f * (rhythm ?? 0f));

        float beatOffset = sampleTime - effectTime;
        colors[Random.Range(0, shape[0])] = Color.HSVToRGB(Random.value, Random.value, 1f);
        background += effectDelta * 0.1f;
        background %= 1f;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.HSVToRGB((background + beatOffset * 0.1f + hueShift) % 1f, 1f, 1f);
        }
        for (int i = 0; i < shape[0]; i++)
        {
            int list = shape[i + 1];
            int start = list + 1;
            int end = start + shape[list];
            Color.RGBToHSV(colors[i], out float hue, out float sat, out float bri);
            for (int j = start; j < end; j++)
            {
                int idx = shape[j];
                buffer[idx] = Color.HSVToRGB((hue + 0.01f * j + beatOffset * 0.1f + hueShift) % 1f, sat, bri);
            }
            colors[i] = Color.HSVToRGB((hue + 0.01f) % 1f, sat, bri);
        }
    }

}
