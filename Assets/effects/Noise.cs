﻿using UnityEngine;
using Random = UnityEngine.Random;

public class Noise : EffectBase {

  private Settings setting;

  private float n;

  //private float timeScale;

  public override string DebugText() {
    return $"Noise: {n}\nSpeed: {setting.speed}";
  }

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void OnStart() {
    base.OnStart();
    if (controller.effectSettings.noise.Length > 0)
      setting = controller.effectSettings.noise[Random.Range(0, controller.effectSettings.noise.Length)];
    else
      setting.Randomize();
    buffer.Clear();
  }

  public override void OnEnd() {  }

  public override void Draw() {
    float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);

    for(int i = 0; i < buffer.Length; i++) {
      float scale = setting.scale;
      float x     = tiles[i].center.x * scale;
      float y     = tiles[i].center.y * scale;
      float z     = effectTime * setting.speed;

      n =  Perlin.Noise(x, y, z);
      n *= setting.amplifier;
      //n = Mathf.Abs(n);

      int v = (int)n;
            if ((v & 1) == 0)
                buffer[i] = APalette.read((n + setting.colorDelta) % 1f, true) * beatBrightness;
            else
                buffer[i] = Color.black;
        }
  }

  [System.Serializable]
  public class Settings {

    public float scale;
    public float speed;
    public float amplifier;
    public float colorDelta;

    public void Randomize() {
      scale      = Random.Range(0.05f, 0.2f);
      speed      = Random.Range(0.1f, 1.5f);
      amplifier  = Random.Range(1f, 5f);
      colorDelta = Random.value;
    }

  }

}