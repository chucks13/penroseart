﻿﻿﻿using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a Julia fractal in screen space and maps it onto the Penrose tile buffer.
/// </summary>
public class Julia : ScreenEffect
{

    private float angle;
    private int iterations = 100;
    private float speed = 0.15f;
    private float distance = 5f;
    private float ca = -0.8f;
    private float cb = 0.156f;
    private float xOffset = 0f;
    private float yOffset = 0f;
    private int i;

    private readonly Vector2[] valueSets = {
      new Vector2(0.285f, 0.01f),
      new Vector2(-0.70176f, -0.3842f),
      new Vector2(-0.835f, -0.2321f),
      new Vector2(-0.8f, 0.156f),
      new Vector2(-0.7269f, 0.1889f)
    };

    private readonly Vector2[] offSets = {
      new Vector2(0.2f, 0.04f),
      new Vector2(-0.125f, -0.04f),
      new Vector2(-0.0375f, 0f),
      new Vector2(0.175f, 0.05f),
      new Vector2(0.0875f, 0.225f)
    };

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    public override string DebugText() { return $"{i}, {speed}, ({xOffset}, {yOffset})"; }

    /// <summary>
    /// Called once when effect is created
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        i = Random.Range(0, valueSets.Length);
        ca = valueSets[i].x;
        cb = valueSets[i].y;
        xOffset = offSets[i].x;
        yOffset = offSets[i].y;
        speed = Random.Range(0.1f, 0.3f);
        buffer.Clear();
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales the fractal colors after screen-to-tile conversion.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);
        //buffer.Clear();

        var sa = Mathf.Sin(angle).Map01(1f, -1f);

        var w = distance * sa;
        var h = w * height / width;
        var xMin = -w / 2f;
        var yMin = -h / 2f;
        var xMax = xMin + w;
        var yMax = yMin + h;
        var dx = (xMax - xMin) / width;
        var dy = (yMax - yMin) / height;

        angle += speed * effectDelta;

        var y = yMin + yOffset;
        for (var sy = 0; sy < height; sy++)
        {


            var x = xMin + xOffset;
            for (var sx = 0; sx < width; sx++)
            {

                var a = x;
                var b = y;

                var n = 0;
                while (n < iterations)
                {
                    var aa = a * a;
                    var bb = b * b;

                    if (aa + bb > 4f) break;

                    var twoAb = 2f * a * b;

                    a = aa - bb + ca;
                    b = twoAb + cb;

                    n++;
                }

                var hue = Mathf.Sqrt((float)n / iterations) % 1f;
                screenBuffer[sx + (sy * width)] = Color.HSVToRGB(hue, 1f, n == iterations ? 0f : 1f) * beatBrightness;

                x += dx;
            }

            y += dy;
        }

        // convert the 2D Matrix buffer to a tile buffer
        ConvertScreenBuffer(ref screenBuffer, in buffer);
    }
}