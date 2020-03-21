using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class stars : EffectBase
{
    private Settings setting;
    private Color color;

    public override string DebugText() => setting.randomColor ? "Color: random" : $"Color: {setting.color.ToString()}";

    public override void Init()
    {
        base.Init();
        setting = new Settings();
    }

    // Should be called every time an effect is turned on
    public override void OnStart()
    {
        if (controller.sparkleSettings.Length > 0)
        {
            setting = controller.starsSettings[Random.Range(0, controller.sparkleSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }

        var text = (setting.randomColor) ? "random" : setting.color.ToString();
        controller.debugText.text = $"Color: {text}";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    public override void OnEnd() { }

    public override void Draw()
    {
        buffer.Fade();
        int[] loops = penrose.JsonRawData.shapes.loops;
        int count = (int)(controller.dance.deltaTime * buffer.Length);
        count = count/5;
        for (int i = 0; i < count; i++)
        {

            if (setting.randomColor)
                color = Color.HSVToRGB(Random.value, 1f - controller.dance.decay, 1f);
            else
                color = setting.color * (1f + controller.dance.decay);


            int loop = Random.Range(0, loops[0]); //JsonRawData.shapes.loops.
            int list = loops[1 + loop];
            int start = list + 1;
            int end = start + loops[list];
            if ((end - start) != 5)               // only select stars
                continue;

            for(int j=start;j<end;j++)
            {
                int idx = loops[j];
                if(idx>=0)
                    buffer[idx] = color;
            }
        }
    }





    [System.Serializable]
    public class Settings
    {
        public bool randomColor = true;
        public Color color;

        public void Randomize()
        {
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
        }
    }
}

