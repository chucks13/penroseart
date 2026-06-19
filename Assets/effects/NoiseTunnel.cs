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
        base.OnStart();
        scale = Random.Range(0.05f, 0.2f);
        speed = Random.Range(0.1f, 1.5f);
        amplifier = Random.Range(1f, 5f);
        colorDelta = Random.value;
        style = Random.Range(0, 3);
        direction = Random.Range(0, 2);
        buffer.Clear();
        beatVariant=beatManager.GetRandomVariantChill();
        beatMode = Random.Range(0,3);
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
        // Beat pulse scales the final tunnel colors without changing tunnel phase.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 0.75f, 1.0f, beatEnable);
        float beatHue = beatManager.GetBeatBrightness(beatVariant, 0.5f, 0.0f, beatEnable);
        float beatTime = beatManager.GetBeatTime(beatVariant, effectTime, 0.5f);
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
                if (beatMode > 0)
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    h += beatHue;
                    v *= beatBrightness;
                    color = Color.HSVToRGB(h % 1f, s, v);
                }
            }
            else
                color = Color.black;
            buffer[i] = color * beatBrightness;
        }
    }
}