using Random = UnityEngine.Random;
using UnityEngine;

public class Pulse : EffectBase {

  private Settings setting;
  
  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void LoadSettings() {
    if(controller.pulseSettings.Length > 0) {
      setting = controller.pulseSettings[Random.Range(0, controller.pulseSettings.Length)];
    } else {
      setting.Randomize();
    }

    controller.debugText.text = $"Start: {setting.startColor}\nEnd: {setting.endColor}\nTime: {setting.seconds}";
  }

  public override void Draw() {
    var t = Mathf.InverseLerp(0f, setting.seconds, Mathf.PingPong(Time.time, setting.seconds));
    var color = Color.Lerp(setting.startColor, setting.endColor, t);

    for(int i = 0; i < buffer.Length; i++) {
      buffer[i] = color;
    }
  }

  [System.Serializable]
  public class Settings {
    public float seconds = 2f;
    public Color startColor = Color.black;
    public Color endColor = Color.white;
    
    public void Randomize() {
      startColor = Color.HSVToRGB(Random.value, 1f, 1f);
      endColor = Color.HSVToRGB((Random.value + Random.value) * 0.5f, 1f, 1f);
      seconds = Random.Range(1f, 5f);
    }
  }
}