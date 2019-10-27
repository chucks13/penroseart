using UnityEngine;

[System.Serializable]
public class Nibbler : EffectBase {

  private int current;
  private Settings setting;

  public override string DebugText() {
    var colorText = (setting.randomColor) ? "random" : setting.color.ToString();
    return $"Color: {colorText}\nFade: {setting.fade}";
  }

  public override void Init() {
    base.Init();
    setting = new Settings();
    current = Random.Range(0, Penrose.Total);
  }

  public override void OnStart() {
    if(controller.nibblerSettings.Length > 0) {
      setting = controller.nibblerSettings[Random.Range(0, controller.nibblerSettings.Length)];
    } else {
      setting.Randomize();
    }

    buffer.Clear();
  }

  public override void OnEnd() {  }

  public override void Draw() {
    buffer.Fade(setting.fade);
    int count = (int)(controller.dance.deltaTime * 300f);
    for(var x = 0; x < count; x++) {
      current = tiles[current].GetRandomNeighbor();

      buffer[current] = setting.randomColor
        ? Color.HSVToRGB(Random.value, 1f - controller.dance.decay, 1f)
        : (setting.color * (1f + controller.dance.decay));
    }

  }

  [System.Serializable]
  public class Settings {

    //public float speed = 1f;   
    public bool randomColor = true;
    public Color color = Color.clear;

    [Range(0.97f, 0.999f)]
    public float fade = 0.999f;

    public void Randomize() {
      if(Random.value > 0.5f) {
        randomColor = true;
        color       = Color.clear;
      } else {
        randomColor = false;
        color       = Color.HSVToRGB(Random.value, 1f, 1f);
      }

      fade = Random.Range(0.97f, 0.999f);
    }

  }

}