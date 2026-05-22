using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Alternates two colors across tile types with a ping-pong time curve.
/// </summary>
public class Pulse : EffectBase
{

    private Color startColor;
    private Color endColor;
    private float seconds;
    private Color color;
    private float colorDelta;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Start: {startColor}\nEnd: {endColor}\nTime: {seconds}";
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
        color = Color.HSVToRGB(Random.value, 1f, 1f);
        seconds = Random.Range(1f, 5f);
        colorDelta = Random.Range(0.25f, 0.75f);

        startColor = color;
        endColor = startColor.Delta(colorDelta);
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

        var color1 = Color.Lerp(color, endColor, t);
        var color2 = Color.Lerp(endColor, color, t);

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = tiles[i].type == 0 ? color1 : color2;
        }
    }
}