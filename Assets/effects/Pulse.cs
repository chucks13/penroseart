using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Alternates two colors across tile types with a ping-pong time curve.
/// </summary>
public class Pulse : EffectBase
{
    /// <summary>Pulse's throbbing accents suit Fills and Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private Color startColor;
    private Color endColor;
    private float seconds;
    private Color color;
    private float colorDelta;
    private float[] wave = new float[400];
    int beatMode;
    float maxradius = 0;
    float pulseMultipler;
    float pulseScale;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"scale: {pulseScale} \nStart: {startColor}\nEnd: {endColor}\nTime: {seconds}";
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
        wave = new float[400];      // clear array
        color = Color.HSVToRGB(Random.value, 1f, 1f);
        seconds = Random.Range(1f, 5f);
        colorDelta = Random.Range(0.25f, 0.75f);
        startColor = color;
        endColor = startColor.Delta(colorDelta);
        beatMode = Random.Range(0, 2);
        pulseMultipler = Random.value * 0.125f + 0.125f;
        pulseScale = (waveforms.ShortestPeakSpacingMs(waveform) ?? 0f) / 200f;

        for (int i = 0; i < buffer.Length; i++)
        {
            float r = tiles[i].radius;
            if (r > maxradius)
                maxradius = r;
        }
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
        var t = Mathf.InverseLerp(0f, seconds, Mathf.PingPong(effectTime, seconds));
        float? rhythm = waveforms.Evaluate(waveform);
        float waveHeight = rhythm is { } envelope ? Mathf.Lerp(1f, 0f, envelope) : 0f;
        for (int i = wave.Length - 1; i > 0; i--)
            wave[i] = wave[i - 1];
        wave[0] = waveHeight;

        var color1 = Color.Lerp(color, endColor, t);
        var color2 = Color.Lerp(endColor, color, t);

        for (int i = 0; i < buffer.Length; i++)
        {
            float waveidxf = tiles[i].radius * pulseScale;
            int waveidx = (int)waveidxf;
            if (waveidx > (wave.Length - 1))
                waveidx = wave.Length - 1;

            Color color = tiles[i].type == 0 ? color1 : color2;
            Color.RGBToHSV(color, out float h, out float s, out float v);

            if (rhythm.HasValue)
                switch (beatMode)
                {
                    case 0:
                        h += wave[waveidx] * pulseMultipler;
                        break;
                    case 1:
                        s += wave[waveidx] * pulseMultipler * 2f;
                        break;
                    case 2:
                        v += wave[waveidx] * (1f - pulseMultipler);
                        break;
                }

            buffer[i] = Color.HSVToRGB(h % 1f, s, v);
        }
    }
}
