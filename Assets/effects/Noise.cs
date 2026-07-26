using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat-driven brightness, color, or time distortion.
/// </summary>
public class Noise : EffectBase
{
    /// <summary>Noise's texture suits Low, Mid, and High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill |  Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid | Repertoire.EnergyHigh;


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

        // we are going to hack the local time with our own local delta
        effectTime -= effectDelta;            // remove the currelt delta
        // calculate our drop modified time delta
        float localDelta = effectDelta;
        localDelta *= beatManager.Drop.Before.Decay(8);   // slow down leading to drop

        if (beatManager.Drop.Active)
        {
            float rampDown = localDelta * beatManager.Drop.In.Decay(8).Remap(1f, 0f, 5f, localDelta);
            if (rampDown > localDelta)
                localDelta = rampDown;
        }
        // change the effect time by this updated delta
        effectTime += localDelta;


        // This Effect owns all three response mappings and their clockless fallbacks.
        float rhythm = waveform.Envelope;
        hueShift = 0f;
        if (distortionMode == 0)
            beatBrightness = waveform.Lerp(0.85f, 1f);
        else if (distortionMode == 1)
            hueShift = 0.25f * rhythm;
        else if (distortionMode == 2)
            sampleTime = effectTime + (0.5f * rhythm);

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
                float h, s, v_col;
                Color.RGBToHSV(c, out h, out s, out v_col);
                h = (h + hueShift) % 1f;

                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    v_col = (h + s + v_col) % 1f;                   // assure there is brightness variation
                    s = 0f;
                }
                
                c = Color.HSVToRGB(h, s, v_col);

                buffer[i] = c * beatBrightness;
            }
            else
                buffer[i] = Color.black;
        }
    }
}
