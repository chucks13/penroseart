using UnityEngine;
using Random = UnityEngine.Random;

public class Noise : EffectBase {

  private Settings setting;
  //private float timeScale;

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void Draw() {
    for(int i = 0; i < buffer.Length; i++) {
      float scale = (1.0f + (controller.dance.decay * 0.25f)) * setting.scale;
      float x     = controller.penrose.tiles[i].center.x * scale;
      float y     = controller.penrose.tiles[i].center.y * scale;
      float z     = controller.dance.fixedTime * setting.speed;
      float n     = Perlin.Noise(x, y, z);
      n *= setting.amplifier;

      int v = (int)n;
      if((v & 1) == 0)
        buffer[i] = Color.HSVToRGB((n + setting.colorDelta) % 1f, 1f, 1);
      else
        buffer[i] = Color.black;
    }
  }

  public override void LoadSettings() {
    if(controller.noiseSettings.Length > 0) {
      setting = controller.noiseSettings[Random.Range(0, controller.noiseSettings.Length)];
    } else {
      setting.Randomize();
    }

    controller.debugText.text = $"Scale: {setting.scale}\nSpeed: {setting.speed}\nAmp: {setting.amplifier}\nDelta: {setting.colorDelta}";
    ClearAll();
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