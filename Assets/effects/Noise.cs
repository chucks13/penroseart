using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat-driven brightness, color, or time distortion.
/// </summary>
public class Noise : EffectBase
{
    /// <summary>Noise's texture suits Low, Mid, and High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;


    /// <summary>The Waveform this Effect owns and evaluates for its local rhythm responses.</summary>
    private Waveform waveform;

    private float n;
    private float scale;
    private float speed;
    private float amplifier;
    private float colorDelta;
    private int distortionMode; // 0: Brightness, 1: Color, 2: Time

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        string[] modeNames = { "Brightness", "Color", "Time Warp" };
        return $"Noise: {n}\nSpeed: {speed}\nBeat Mode: {modeNames[distortionMode]}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        scale = Random.Range(0.05f, 0.2f);
        speed = Random.Range(0.1f, 1.5f);
        amplifier = Random.Range(1f, 5f);
        colorDelta = Random.value;
        distortionMode = Random.Range(0, 3);
        buffer.Clear();
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
        float beatBrightness = 1.0f;
        float hueShift = 0.0f;
        float sampleTime = effectTime;

        // This Effect owns all three response mappings and their clockless fallbacks.
        float? rhythm = waveforms.Evaluate(waveform);
        if (distortionMode == 0)
            beatBrightness = rhythm is { } envelope ? Mathf.Lerp(0.85f, 1f, envelope) : 1f;
        else if (distortionMode == 1)
            hueShift = 0.25f * (rhythm ?? 0f);
        else if (distortionMode == 2)
            sampleTime = effectTime + (0.5f * (rhythm ?? 0f));

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = tiles[i].center.x * scale;
            float y = tiles[i].center.y * scale;
            float z = sampleTime * speed;

            n = Perlin.Noise(x, y, z);
            n *= amplifier;
            //n = Mathf.Abs(n);

            int v = (int)n;
            if ((v & 1) == 0)
            {
                Color c = APalette.read((n + colorDelta) % 1f, true);
                if (hueShift > 0)
                {
                    float h, s, v_col;
                    Color.RGBToHSV(c, out h, out s, out v_col);
                    c = Color.HSVToRGB((h + hueShift) % 1f, s, v_col);
                }
                buffer[i] = c * beatBrightness;
            }
            else
                buffer[i] = Color.black;
        }
    }
}