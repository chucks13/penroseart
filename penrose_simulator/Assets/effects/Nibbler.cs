using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class Nibbler : EffectBase {
  private int last = 0;
  private int current = 1;
  private Settings setting;

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void Draw() {
    FadeAll();
    int count = (int)(controller.dance.deltaTime * 300f);
    for (var x = 0; x < count; x++) {
      var neighbor = current;
      var i=0;
      for ( i = 0; i < 40; i++) {
        neighbor = controller.geometry.tileData[current].neighbors[Random.Range(0, 4)];
        
        if(neighbor < 0) continue;
        if(neighbor == last) continue;

        break;
      }
      if (i == 40)
        neighbor = current;
      last = current;
      current = neighbor;

      buffer[current] = setting.randomColor ?
       Color.HSVToRGB(Random.value, 1f - controller.dance.decay, 1f) :
        (setting.color * (1f + controller.dance.decay));
      }
    }

  public override void LoadSettings() {
    if(controller.nibblerSettings.Length > 0) {
      setting = controller.nibblerSettings[Random.Range(0, controller.nibblerSettings.Length)];
    } else {
      setting.Randomize();
    }

    var colorText = (setting.randomColor) ? "random" : setting.color.ToString();
    controller.debugText.text = $"Speed: {setting.speed}\nColor: {colorText}";
    ClearAll();
  }

  [System.Serializable]
  public class Settings {
    public float speed = 1f;
    public bool randomColor = true;
    public Color color = Color.clear;

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