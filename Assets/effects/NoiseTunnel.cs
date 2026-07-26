using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders palette-colored Perlin noise over tile positions with optional beat distortion.
/// </summary>
/// <summary>
/// Renders radial and diagonal tunnel bands directly from tile positions.
/// </summary>
public class NoiseTunnel : EffectBase
{
    /// <summary>NoiseTunnel's driving noise flow suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
         Repertoire.HandlesFill |  Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private float n;
    private float scale;
    private float speed;
    private float amplifier;
    private float colorDelta;
    private int style;
    private int direction;
    int beatMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Noise: {n}\nSpeed: {speed}\nDirection: {direction}";
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
        waveform = waveforms.Random(Energy.Low, Energy.Mid);
        scale = Random.Range(0.05f, 0.2f);
        speed = Random.Range(0.1f, 1.5f);
        amplifier = Random.Range(1f, 5f);
        colorDelta = Random.value;
        style = Random.Range(0, 3);
        direction = Random.Range(0, 2);
        buffer.Clear();
        beatMode = Random.Range(0, 3);
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
        effectDelta = localDelta;

        // This Effect owns its brightness, hue, time-warp, and clockless fallback mappings.
        float rhythm = waveform.Envelope;
        float beatBrightness = waveform.Lerp(1f, 0.75f);
        float beatHue = 0.5f * rhythm;
        float beatTime = effectTime + (0.5f * rhythm);
        float localTime = effectTime;

        for (int i = 0; i < buffer.Length; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * scale);
            float y = Mathf.Abs(tiles[i].center.y * scale);
            float d1 = Mathf.Sqrt((x * x) + (y * y));
            float d2 = x + y;
            float d3 = x - y;
            if (direction > 0)
            {
                d1 = 10000 - d1;
                d2 = 10000 - d2;
                d3 = 10000 - d3;
            }

            if (beatMode < 2)
                localTime = beatTime;
            float z = localTime * speed;

            switch (style)
            {
                case 0:
                    n = Perlin.Noise(d1 + z);
                    break;
                case 1:
                    n = Perlin.Noise(d2 + z);
                    break;
                case 2:
                    n = Perlin.Noise(d3 + z);
                    break;
            }

            n *= amplifier;
            //n = Mathf.Abs(n);

            int v1 = (int)n;
            Color color;
            if ((v1 & 1) == 0)
            {
                color = Color.HSVToRGB((n + colorDelta) % 1f, 1f, 1);
                Color.RGBToHSV(color, out float h, out float s, out float v);
                if (beatMode > 0)
                {
                    h += beatHue;
                    v *= beatBrightness;
                }
                if (beatManager.Fill.Active)            // blak and whire on fill
                {
                    v = (h + s + v) % 1f;                   // assure there is brightness variation
                    s = 0f;
                }
                color = Color.HSVToRGB(h % 1f, s, v);
            }
            else
                color = Color.black;
            buffer[i] = color * beatBrightness;
        }
    }
}


