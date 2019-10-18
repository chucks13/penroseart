using Random = UnityEngine.Random;
using UnityEngine;

public class ColorSparkle : EffectBase {

  private Settings setting;
  private Color color;

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void Draw() {
   FadeAll();
   int count =(int) (controller.dance.deltaTime * buffer.Length);
    for (int i = 0; i < count; i++) {

      if(setting.randomColor)
        color = Color.HSVToRGB(Random.value, 1f - controller.dance.decay, 1f);
      else
        color = setting.color * (1f + controller.dance.decay);

      buffer[Random.Range(0, buffer.Length)] = color; //  Color.HSVToRGB(Random.value, 1, 1);
    }
  }

  public override void LoadSettings() {
    if(controller.sparkleSettings.Length > 0) {
      setting = controller.sparkleSettings[Random.Range(0, controller.sparkleSettings.Length)];
    } else {
      setting.Randomize();
    }

<<<<<<< HEAD
=======
  public override void LoadSettings() {
    if(controller.sparkleSettings.Length > 0) {
      setting = controller.sparkleSettings[Random.Range(0, controller.sparkleSettings.Length)];
    } else {
      setting.Randomize();
    }

>>>>>>> parent of 327d049... Merge pull request #19 from chucks13/hunter
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