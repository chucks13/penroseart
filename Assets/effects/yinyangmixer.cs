using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Splits two child effects into rotating yin-yang-like angular regions.
/// </summary>
public class yinyangmixer : MixerBase
{

    private EffectBase[] effects;
    float yina;
    float spin;
    float drift;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

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
        effects = new EffectBase[2];
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            // The parent applies one shared beat pulse after splitting child buffers.
            effects[i].beatEnable = false;
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }
        controller.debugText.text = debugText;
        spin = 6f;
        spin *= (Random.value < 0.5) ? -1f : 1f;
        drift = 6f;
        drift *= (Random.value < 0.5) ? -1f : 1f;
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

        yina += spin * effectDelta * 60f;
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            effects[i].Draw();
        }
        // Parent beat pulse scales the final split/masked child-effect output.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        Color ribbon = APalette.read(0.5f, true) * beatBrightness;

        for (int i = 0; i < buffer.Length; i++)
        {
            float w = 20f;
            float a = tiles[i].angle;
            float r = tiles[i].radius * drift;
            a += yina;
            a += r;
            a += 360000f;
            a %= 360f;


            if (a < w)
            {
                buffer[i] = ribbon;
                continue;
            }
            if (a > 360 - w)
            {
                buffer[i] = ribbon;
                continue;
            }
            if (a < (180f - w))
            {
                buffer[i] = effects[0].buffer[i] * beatBrightness;
                continue;
            }
            if (a > (180f + w))
            {
                buffer[i] = effects[1].buffer[i] * beatBrightness;
                continue;
            }
            buffer[i] = ribbon;
        }
    }

}