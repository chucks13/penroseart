using Random = UnityEngine.Random;
using UnityEngine;

public class ColorSparkle : Effect {

  private Settings setting;
  private Color color;

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void Draw() {
    FadeAll();
    for(int i = 0; i < 10; i++) {

      if(setting.randomColor)
        color = Color.HSVToRGB(Random.value, 1, 1);
      else
        color = setting.color;

      buffer[Random.Range(0, 600)] = color; //  Color.HSVToRGB(Random.value, 1, 1);
    }
  }

  public override void LoadSettings() {
    if(controller.sparkleSettings.Length > 0) {
      setting = controller.sparkleSettings[Random.Range(0, controller.sparkleSettings.Length)];
    } else {
      setting.Randomize();
    }

    var text = (setting.randomColor) ? "random" : setting.color.ToString();
    controller.debugText.text = $"Color: {text}";
    ClearAll();
  }

  [System.Serializable]
  public class Settings {
    public bool randomColor = true;
    public Color color;

    public void Randomize() {
      if(Random.value > 0.5f) {
        randomColor = true;
        color       = Color.clear;
      } else {
        randomColor = false;
        color       = Color.HSVToRGB(Random.value, 1f, 1f);
      }
    }
  }

}