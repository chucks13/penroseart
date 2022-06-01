using Random = UnityEngine.Random;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Angles : EffectBase
{
    private Color[] colors;
    private Settings setting;

    public override string DebugText() => "Angles";

    public override void Init()
    {
        base.Init();
        setting = new Settings();
    }

    // Should be called every time an effect is turned on
    public override void OnStart()
    {
        if (controller.anglesSettings.Length > 0)
        {
            setting = controller.anglesSettings[Random.Range(0, controller.anglesSettings.Length)];
        }
        controller.debugText.text = "Angles";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    public override void OnEnd() { }

    public override void Draw()
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            Penrose.TileData t = tiles[i];
            {
                float angle = t.angle * 2;
                angle += Time.time*setting.speed;
                Color c = Color.HSVToRGB(angle % 1f, 1f, 1f);
                buffer[i] = c;
            }
        }
    }


    [System.Serializable]
    public class Settings
    {
        public float speed = 1f;
        public void Randomize()
        {
            speed = 0.25f;
        }
    }
}
