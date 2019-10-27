using UnityEngine;
using Random = UnityEngine.Random;

public class NoiseTunnel : EffectBase {

  private Settings setting;
  private float n;

  public override string DebugText() {
    return $"Noise: {n}\nSpeed: {setting.speed}\nDirection: {setting.direction}";
  }

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void OnStart() {
    if(controller.noiseTunnelSettings.Length > 0) {
      setting = controller.noiseTunnelSettings[Random.Range(0, controller.noiseTunnelSettings.Length)];
    } else {
      setting.Randomize();
    }

    buffer.Clear();
  }

  public override void OnEnd() {  }

  public override void Draw() {
    for(int i = 0; i < buffer.Length; i++) {
      float scale = (1.0f + (controller.dance.decay * 0.25f)) * setting.scale;
      float x     = Mathf.Abs(tiles[i].center.x * scale);
      float y     = Mathf.Abs(tiles[i].center.y * scale);
      float d1    = Mathf.Sqrt((x * x) + (y * y));
      float d2    = x + y;
      float d3    = x - y;
      if(setting.direction > 0) {
        d1 = 10000 - d1;
        d2 = 10000 - d2;
        d3 = 10000 - d3;
      }

      float z = controller.dance.fixedTime * setting.speed;
      
      switch(setting.style) {
        case 0:
          n = Perlin.Noise(d1 + z);
          break;
        case 1:
          n = Perlin.Noise(d2 + z);
          break;
        case 2:
          n = Perlin.Noise(d3 + z);
          break;
      }

      n *= setting.amplifier;
      //n = Mathf.Abs(n);

      int v = (int)n;
      if((v & 1) == 0)
        buffer[i] = Color.HSVToRGB((n + setting.colorDelta) % 1f, 1f, 1);
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
    public int style;
    public int direction;

    public void Randomize() {
      scale      = Random.Range(0.05f, 0.2f);
      speed      = Random.Range(0.1f, 1.5f);
      amplifier  = Random.Range(1f, 5f);
      colorDelta = Random.value;
      style      = Random.Range(0, 3);
      direction  = Random.Range(0, 2);
    }

  }

}