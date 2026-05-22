﻿using UnityEngine;

/// <summary>
/// Combines two child effects using a Perlin noise mask and colored border band.
/// </summary>
public class NoiseMixer : MixerBase
{

    private EffectBase[] effects;
    private Color border;

        /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
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
            // NoiseMixer owns the rhythmic shape of the composite, so child pulses are suppressed.
            effects[i].beatEnable = false;
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
            border = Color.HSVToRGB(Random.value, 1, 1);
        }

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
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            effects[i].Draw();
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            float scale = 0.07f;
            float x = tiles[i].center.x * scale;
            float y = tiles[i].center.y * scale;
            float z = effectTime; // use local mixer time

            float n = Perlin.Noise(x, y, z);
            if (n > 0.1)
                buffer[i] = effects[0].buffer[i];
            else if (n > -0.1)
                buffer[i] = border;
            else
                buffer[i] = effects[1].buffer[i];
        }
    }

}