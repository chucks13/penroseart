﻿using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Maintains a fading sparkle field by randomly lighting tiles over the previous frame.
/// </summary>
public class ColorSparkle : EffectBase
{
    private bool randomColor;
    //    private Color color;
    private float hue;

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
        base.OnStart();
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
        // Beat pulse scales newly generated sparkles while the existing buffer continues to fade.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        buffer.Fade();
        int count = (int)(effectDelta * buffer.Length);
        Color drawColor = Color.HSVToRGB((hue + beatBrightness) % 1.0f, 1f, 1f);
        for (int i = 0; i < count; i++)
        {
            // While the beat clock is active, hold sparkle hue stable so the beat pulse is the visible rhythm.
            if (randomColor && (!IsBeatActive))
                drawColor = Color.HSVToRGB(Random.value, 1f, 1f);

            buffer[Random.Range(0, buffer.Length)] = drawColor;// * beatBrightness;
        }
    }
}