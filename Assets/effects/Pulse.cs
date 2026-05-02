﻿using Random = UnityEngine.Random;
using UnityEngine;

public class Pulse : EffectBase {

  private Color startColor;
  private Color endColor;
  private float seconds;
  private Color color;
  private float colorDelta;

  public override string DebugText() {
    return $"Start: {startColor}\nEnd: {endColor}\nTime: {seconds}";
  }

  public override void Init() {
    base.Init();
  }

  public override void OnStart() {
    base.OnStart();
    color   = Color.HSVToRGB(Random.value, 1f, 1f);
    seconds = Random.Range(1f, 5f);
    colorDelta = Random.Range(0.25f, 0.75f);

    startColor = color;
    endColor = startColor.Delta(colorDelta);
  }

  public override void OnEnd() {  }

  public override void Draw() {
    var t = Mathf.InverseLerp(0f, seconds, Mathf.PingPong(effectTime, seconds));

    var color1 = Color.Lerp(color, endColor, t);
    var color2 = Color.Lerp(endColor, color, t);

    for(int i = 0; i < buffer.Length; i++) {
      buffer[i] = tiles[i].type == 0 ? color1 : color2;
    }
  }
}