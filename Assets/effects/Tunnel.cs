using System;
using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Renders a direct tile-space tunnel from radius, density, and time.
/// </summary>
public class Tunnel : ScreenEffect
{

    private float density;
    private float speed;
    private float mix;

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        density = Random.Range(0.0004f, 0.003f);
        speed = Random.Range(0.1f, 1f);
        mix = Random.Range(0.01f, 0.2f);
        buffer.Clear();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Density: {density}\n" +
        $"Speed: {speed}\n" +
        $"Mix: {mix}\n";
    }
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Renders one frame of radial tunnel bands directly into the tile buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales tunnel brightness without changing the tunnel phase.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        for (int i = 0; i < Penrose.Total; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * 0.03f);
            float y = Mathf.Abs(tiles[i].center.y * 0.03f);
            float distance = Mathf.Sqrt((x * x) + (y * y));
            var color = i * density + effectTime * speed + distance * mix;
            buffer[i] = Color.HSVToRGB(color % 1f, 1f, 1f) * beatBrightness;
        }
    }
}