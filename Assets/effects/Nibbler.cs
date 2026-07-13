using UnityEngine;

[System.Serializable]
/// <summary>
/// Paints fading trails from random walkers moving through tile neighbor links.
/// </summary>
public class Nibbler : EffectBase
{
    /// <summary>Nibbler's roaming eaters suit Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.EnergyLow | Repertoire.EnergyMid;


    private const int Count = 10;
    /// <summary>The Waveform this Effect owns and evaluates for its local rhythm responses.</summary>
    private Waveform waveform;

    private int[] current;
    private bool randomColor;
    private Color color;
    private float fade;
    int beatMode;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var colorText = (randomColor) ? "random" : color.ToString();
        return $"Color: {colorText}\nFade: {fade}";
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        current = new int[Count];
        for (int i = 0; i < Count; i++) current[i] = Random.Range(0, Penrose.Total);
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        waveform = synth.Random();
        if (Random.value > 0.5f)
        {
            randomColor = true;
            color = Color.clear;
        }
        else
        {
            randomColor = false;
            color = Color.HSVToRGB(Random.value, 1f, 1f);
        }

        fade = Random.Range(0.97f, 0.999f);
        buffer.Clear();
        beatMode = Random.Range(0, 2);
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
        float? rhythm = synth.Evaluate(waveform);
        float beatBrightness = rhythm is { } envelope ? Mathf.Lerp(1f, 0.75f, envelope) : 0.75f;
        float beatHue = 0.5f * (rhythm ?? 0f);
        buffer.Fade(fade);
        int count = (int)(effectDelta * 300f);
        for (int y = 0; y < Count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                current[y] = tiles[current[y]].GetRandomNeighbor();
                Color c = randomColor ? Color.HSVToRGB(Random.value, 1f, 1f) : color;

                if (beatMode > 0)
                {
                    Color.RGBToHSV(color, out float h, out float s, out float v);
                    h += beatHue;
                    color = Color.HSVToRGB(h % 1f, s, v);
                }

                buffer[current[y]] = c * beatBrightness;
            }
        }
    }
}