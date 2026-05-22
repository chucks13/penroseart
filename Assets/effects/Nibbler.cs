﻿using UnityEngine;

[System.Serializable]
/// <summary>
/// Paints fading trails from random walkers moving through tile neighbor links.
/// </summary>
public class Nibbler : EffectBase
{

    private const int Count = 10;
    private int[] current;
    private bool randomColor;
    private Color color;
    private float fade;

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
        base.OnStart();
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
        // Beat pulse scales walker trail colors as they are written into the fading buffer.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        buffer.Fade(fade);
        int count = (int)(effectDelta * 300f);
        for (int y = 0; y < Count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                current[y] = tiles[current[y]].GetRandomNeighbor();
                Color c = randomColor ? Color.HSVToRGB(Random.value, 1f, 1f) : color;
                
                buffer[current[y]] = c * beatBrightness;
            }
        }
    }
}