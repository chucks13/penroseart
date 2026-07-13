using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Maintains a fading sparkle field by randomly lighting tiles over the previous frame.
/// </summary>
public class ColorSparkle : EffectBase
{
    /// <summary>The consumer-owned Waveform that offsets sparkle hue during this activation.</summary>
    private Waveform waveform;

    private bool randomColor;
    //    private Color color;
    private float hue;

    /// <summary>ColorSparkle's fading sparkle bursts can accent short Fill moments without new behavior;
    /// its gentle shimmer suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.EnergyLow | Repertoire.EnergyMid;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => randomColor ? "Color: random" : $"hue: {hue}";

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    // Should be called every time an effect is turned on
    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        waveform = synth.Random();
        randomColor = (Random.value > 0.5f);
        hue = Random.value;

        var text = (randomColor) ? "random " : hue.ToString();
        controller.debugText.text = $"Color: {text}";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // The Waveform offsets newly generated sparkle hues; clockless rendering stays steady.
        float? rhythm = synth.Evaluate(waveform);
        float hueOffset = rhythm is { } envelope ? Mathf.Lerp(0.5f, 1f, envelope) : 1f;
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        Color drawColor = Color.HSVToRGB((hue + hueOffset) % 1.0f, 1f, 1f);
        for (int i = 0; i < count; i++)
        {
            // While the beat clock is active, hold sparkle hue stable so the beat pulse is the visible rhythm.
            if (randomColor && !beatManager.IsSynced)
                drawColor = Color.HSVToRGB(Random.value, 1f, 1f);

            buffer[Random.Range(0, buffer.Length)] = drawColor;// * beatBrightness;
        }
    }
}
