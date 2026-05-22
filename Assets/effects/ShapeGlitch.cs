using UnityEngine;

/// <summary>
/// Overlays blinking or fading Penrose shape highlights on top of one child effect.
/// </summary>
public class ShapeGlitch : MixerBase
{

    private enum Mode
    {
        Blink,
        Fade
    }

    /// <summary>
    /// Active packed-shape highlight with color, fade, and repeat state.
    /// </summary>
    public class Highlight
    {
        public float intensity;
        public int index;
    }

    private EffectBase effect;
    private int[] shape;
    private Color color;
    private Mode mode;
    private Highlight[] highlights;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = $"Effect: {effect.Name}\n"
         + $"Mode: {mode}\n"
         + $"Shape Count {highlights.Length}";
        return debugText;
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
        switch (Random.Range(0, 2))
        {
            case 0:
                mode = Mode.Blink;
                break;
            case 1:
                mode = Mode.Fade;
                break;
        }
        highlights = new Highlight[Random.Range(10, 50)];
        for (int i = 0; i < highlights.Length; i++)
        {
            highlights[i] = new Highlight();
        }
        switch (Random.Range(0, 9))
        {
            case 0:
                shape = penrose.JsonRawData.shapes.lines0;
                break;
            case 1:
                shape = penrose.JsonRawData.shapes.lines1;
                break;
            case 2:
                shape = penrose.JsonRawData.shapes.lines2;
                break;
            case 3:
                shape = penrose.JsonRawData.shapes.lines3;
                break;
            case 4:
                shape = penrose.JsonRawData.shapes.lines4;
                break;
            case 5:
                shape = penrose.JsonRawData.shapes.loops;
                break;
            case 6:
                shape = penrose.JsonRawData.shapes.lotusballs;
                break;
            case 7:
                shape = penrose.JsonRawData.shapes.starballs;
                break;
            case 8:
                shape = penrose.JsonRawData.shapes.stars;
                break;
        }
        color = Color.HSVToRGB(Random.value, Random.value, 1f);

        effect = GetRandomEffect();
        effect.RandomizeTime();
        effect.Init();
        effect.OnStart();
        // ShapeGlitch owns the overlay rhythm, so the child effect does not pulse independently.
        effect.beatEnable = false;
        var debugText = $"{effect.Name}";
        controller.debugText.text = debugText;
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

        effect.UpdateTime();
        effect.Draw();

        for (int i = 0; i < buffer.Length; i++)
        {

            float r = 0f, g = 0f, b = 0f;
            r += effect.buffer[i].r;
            g += effect.buffer[i].g;
            b += effect.buffer[i].b;
            buffer[i] = new Color(r, g, b, 1f);
        }
        if (Random.Range(0, 50 - highlights.Length) == 0)
        {
            Highlight newlyCreated = highlights[Random.Range(0, highlights.Length)];
            newlyCreated.intensity = 1f;
            newlyCreated.index = Random.Range(0, shape[0]);
        }
        for (int i = 0; i < shape[0]; i++)
        {
            for (int h = 0; h < highlights.Length; h++)
            {
                if (highlights[h].index == i)
                {
                    int list = shape[i + 1];
                    int start = list + 1;
                    int end = start + shape[list];
                    for (int j = start; j < end; j++)
                    {
                        int idx = shape[j];
                        float intensity = highlights[h].intensity;
                        if (intensity > 1f) { intensity = 1f; }
                        buffer[idx] = color * intensity + buffer[idx] * (1f - intensity);
                    }
                    if (mode == Mode.Fade && highlights[h].intensity > 0f)
                    {
                        highlights[h].intensity -= 0.005f;
                        if (highlights[h].intensity < 0f)
                        {
                            highlights[h].intensity = 0f;
                        }
                    }
                    if (mode == Mode.Blink && highlights[h].intensity > 0f)
                    {
                        highlights[h].intensity += 1f;
                        if (highlights[h].intensity > 15f)
                        {
                            highlights[h].intensity = 0f;
                        }
                    }

                }
            }
            Color.RGBToHSV(color, out float hue, out float sat, out float bri);
            color = Color.HSVToRGB((hue + 0.00004f) % 1f, sat, bri);
        }
    }

}