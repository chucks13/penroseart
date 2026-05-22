﻿﻿﻿using Random = UnityEngine.Random;
using UnityEngine;

/// <summary>
/// Renders section/ring panel patterns and contains an older child-effect split mode.
/// </summary>
public class Panels : MixerBase
{
    private Color[] colors;
    private int which;
    EffectBase ef0;
    EffectBase ef1;

        /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
public override string DebugText() => "Panels";

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
        which = Random.Range(0, 2);
        switch (which)
        {
            case 0:
                break;
            case 1:
                colors = new Color[2];
                for (int i = 0; i < 2; i++)
                {
                    colors[i] = Color.HSVToRGB(Random.value, Random.value, 1f);
                }
                break;
            case 2:
                ef0 = GetRandomEffect();
                ef0.RandomizeTime();
                ef0.Init();
                ef0.OnStart();
                // Child split mode aligns both child effects to the panel beat variant.
                ef0.beatVariant = this.beatVariant;
                ef1 = GetRandomEffect();
                ef1.RandomizeTime();
                ef1.Init();
                ef1.OnStart();
                ef1.beatVariant = this.beatVariant;
                break;

        }
        controller.debugText.text = "Panels";
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
        // Beat pulse scales panel colors; child-effect mode, when reachable, aligns children to this variant.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        switch (which)
        {
            case 0:
                buffer.Fade();
                if (Random.Range(0, 5) == 0)
                {
                    int section = Random.Range(0, 18);
                    float h1 = Random.value;
                    float h2 = Random.value;
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Penrose.TileData t = tiles[i];
                        if (t.section == section)
                        {
                            buffer[i] = Color.HSVToRGB(((t.ring % 4) < 2) ? h1 : h2, 1f, 1f) * beatBrightness;
                        }
                    }
                }
                break;
            case 1:
                {
                    var time = Mathf.InverseLerp(0f, 1, Mathf.PingPong(effectTime, 1));

                    var color1 = Color.Lerp(colors[0], colors[1], time);
                    var color2 = Color.Lerp(colors[1], colors[0], time);

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Penrose.TileData t = tiles[i];
                        int v = ((t.ring % 4) < 2) ? 1 : 0;
                        v ^= ((t.section & 1) == 0) ? 1 : 0;
                        v ^= (((t.section / 6) & 1) == 0) ? 1 : 0;
                        buffer[i] = (v == 0 ? color1 : color2) * beatBrightness;
                    }

                }
                break;
            case 2:
                ef0.UpdateTime();
                ef1.UpdateTime();
                ef0.Draw();
                ef1.Draw();
                for (int i = 0; i < buffer.Length; i++)
                {
                    Penrose.TileData t = tiles[i];
                    int v = ((t.section & 1) == 0) ? 1 : 0;
                    v ^= (((t.section / 6) & 1) == 0) ? 1 : 0;
                    buffer[i] = v == 0 ? ef0.buffer[i] : ef1.buffer[i];
                }
                break;
        }
    }
}