﻿﻿﻿using UnityEngine;
using Random = UnityEngine.Random;

public class Noise : EffectBase {

  private float n;
  private float scale;
  private float speed;
  private float amplifier;
  private float colorDelta;

  public override string DebugText() {
    return $"Noise: {n}\nSpeed: {speed}";
  }

  public override void Init() {
    base.Init();
  }

  public override void OnStart() {
    base.OnStart();
    scale      = Random.Range(0.05f, 0.2f);
    speed      = Random.Range(0.1f, 1.5f);
    amplifier  = Random.Range(1f, 5f);
    colorDelta = Random.value;
    buffer.Clear();
  }

  public override void OnEnd() {  }

  public override void Draw() {
    float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);

    for(int i = 0; i < buffer.Length; i++) {
      float x     = tiles[i].center.x * scale;
      float y     = tiles[i].center.y * scale;
      float z     = effectTime * speed;

      n =  Perlin.Noise(x, y, z);
      n *= amplifier;
      //n = Mathf.Abs(n);

      int v = (int)n;
            if ((v & 1) == 0)
                buffer[i] = APalette.read((n + colorDelta) % 1f, true) * beatBrightness;
            else
                buffer[i] = Color.black;
        }
  }
}