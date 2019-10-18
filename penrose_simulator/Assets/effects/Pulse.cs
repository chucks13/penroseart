using Random = UnityEngine.Random;
using UnityEngine;

public class Pulse : EffectBase {

  private Settings setting;
  private Color startColor;
  private Color endColor;

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

    startColor = setting.color;
    endColor = startColor.Delta(setting.colorDelta);

<<<<<<< HEAD
    controller.debugText.text = $"Start: {startColor}\nEnd: {endColor}\nTime: {setting.seconds}";
  }

=======
>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
  public override void Draw() {
    var t = Mathf.InverseLerp(0f, setting.seconds, Mathf.PingPong(Time.time, setting.seconds));

    var color1 = Color.Lerp(setting.color, endColor, t);
    var color2 = Color.Lerp(endColor, setting.color, t);

    for(int i = 0; i < buffer.Length; i++) {
      buffer[i] = controller.penrose.tiles[i].type == 0 ? color1 : color2;
    }
  }

  [System.Serializable]
  public class Settings {

    public float seconds = 2f;
    public Color color;
    public float colorDelta = 0.5f;

    public void Randomize() {
      color   = Color.HSVToRGB(Random.value, 1f, 1f);
      seconds = Random.Range(1f, 5f);
      colorDelta = Random.Range(0.25f, 0.75f);
    }

  }

}