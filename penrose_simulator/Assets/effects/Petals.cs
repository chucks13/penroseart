using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Petals : ScreenEffect {
    private Color[] colors;
    private float background;

    public override string DebugText() { return$""; }

  public override void OnStart() {
        colors = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            colors[i] = Color.HSVToRGB(Random.value, Random.value, 1f);
        }
        background = Random.value;
    }

  public override void OnEnd() { }

  public override void Draw() {
        background += controller.dance.deltaTime * 0.1f;
        background %= 1f;
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = Color.HSVToRGB(background, 1f, 1f);
        }
        for (int shapeIdx = 0; shapeIdx < 3; shapeIdx++)
        {
            int[] shape = penrose.JsonRawData.shapes.loops;
            switch (shapeIdx)
            {
                case 0:
                    shape = penrose.JsonRawData.shapes.loops;
                    break;
                case 1:
                    shape = penrose.JsonRawData.shapes.starballs;
                    break;
                case 2:
                    shape = penrose.JsonRawData.shapes.stars;
                    break;
            }
            for (int i = 0; i < shape[0]; i++)
            {
                int list = shape[i + 1];
                int start = list + 1;
                int end = start + shape[list];
                Color.RGBToHSV(colors[shapeIdx], out float hue, out float sat, out float bri);
                for (int j = start; j < end; j++)
                {
                    int idx = shape[j];
                    buffer[idx] = Color.HSVToRGB((hue + 0.002f * j) % 1f, sat, bri);
                }
                colors[shapeIdx] = Color.HSVToRGB((hue + 0.00004f) % 1f, sat, bri);
            }
        }
    }

}