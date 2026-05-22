using Random = UnityEngine.Random;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Renders a palette hue sweep based on each tile's stored geometric angle.
/// </summary>
public class Angles : EffectBase
{
    private Color[] colors;
    private float speed;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() => "Angles";

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
        base.OnStart();
        speed = 0.25f;
        controller.debugText.text = "Angles";
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
        // Beat pulse narrows/widens the angle-to-hue mapping without changing the underlying tile-angle pattern.
        float beatAngle = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.9f, beatEnable);
        for (int i = 0; i < buffer.Length; i++)
        {
            Penrose.TileData t = tiles[i];
            {
                float angle = t.tileangle / 180f;
                angle += effectTime * speed;
                Color c = Color.HSVToRGB((angle + beatAngle) % 1f, 1f, 1f);
                buffer[i] = c;
            }
        }
    }
}
