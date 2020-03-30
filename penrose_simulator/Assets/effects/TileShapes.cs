using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TileShapes : EffectBase
{
    private Settings setting;
    private Color color;
    private int[] shape;

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
            setting = controller.tileShapesSettings[Random.Range(0, controller.sparkleSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }

        switch(Random.Range(0,9))
        {
            case 0:
                shape = penrose.JsonRawData.shapes.lines0;
                break;
            case 1:
                shape = penrose.JsonRawData.shapes.lines1;
                break;
            case 2:
                shape = penrose.JsonRawData.shapes.lines2;
                break;
            case 3:
                shape = penrose.JsonRawData.shapes.lines3;
                break;
            case 4:
                shape = penrose.JsonRawData.shapes.lines4;
                break;
            case 5:
                shape = penrose.JsonRawData.shapes.loops;
                break;
            case 6:
                shape = penrose.JsonRawData.shapes.lotusballs;
                break;
            case 7:
                shape = penrose.JsonRawData.shapes.starballs;
                break;
            case 8:
                shape = penrose.JsonRawData.shapes.stars;
                break;
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
        int count = (int)(controller.dance.deltaTime * buffer.Length);
        count = count/5;
        for (int i = 0; i < count; i++)
        {

            if (setting.randomColor)
                color = Color.HSVToRGB(Random.value, 1f - controller.dance.decay, 1f);
            else
                color = setting.color * (1f + controller.dance.decay);


            int loop = Random.Range(0, shape[0]); 
            int list = shape[1 + loop];
            int start = list + 1;
            int end = start + shape[list];
            for(int j=start;j<end;j++)
            {
                int idx = shape[j];
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

