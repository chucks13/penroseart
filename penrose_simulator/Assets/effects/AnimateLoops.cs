using Random = UnityEngine.Random;
using UnityEngine;
using System.Numerics;

public class AnimateLoops : EffectBase {

    private Color[] colors;
    private float background;
    int[] shape;
    string shapeName;

    public override string DebugText()
    {
        return $"shape: {shapeName}";
    }

    public override void Init() {
    base.Init();
  }

  public override void OnStart()
    {
        shape = penrose.JsonRawData.shapes.loops;
        shapeName = "loops";
        /*        switch (Random.Range(0, 2))
                {
                    case 0:
                        shape = penrose.JsonRawData.shapes.loops;
                        shapeName = "loops";
                        break;
                    case 1:
                        shape = penrose.JsonRawData.shapes.stars;
                        shapeName = "stars";
                        break;
                }
        */
        colors = new Color[shape[0]];
        for (int i = 0; i < shape[0]; i++)
        {
            colors[i] = Color.HSVToRGB(Random.value, Random.value, 1f);
        }
        background = Random.value;
    }

    public override void OnEnd() {  }

  public override void Draw()
    {
        colors[Random.Range(0, shape[0])] = Color.HSVToRGB(Random.value, Random.value, 1f);
        background += controller.dance.deltaTime * 0.1f;
        background %= 1f;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.HSVToRGB(background, 1f, 1f);
        }
        for (int i = 0; i < shape[0]; i++)
        {
            int list = shape[i + 1];
            int start = list + 1;
            int end = start + shape[list];
            Color.RGBToHSV(colors[i], out float hue, out float sat, out float bri);
            for (int j = start; j < end; j++)
            {
                int idx = shape[j];
                buffer[idx] = Color.HSVToRGB((hue + 0.01f * j) % 1f, sat, bri);
            }
            colors[i] = Color.HSVToRGB((hue + 0.01f) % 1f, sat, bri);
        }
    }

}