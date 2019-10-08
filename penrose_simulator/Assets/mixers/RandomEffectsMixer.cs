using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomEffectsMixer : Mixer {


  private int total;


  public override void Draw() {

    for(int i = 0; i < total; i++) {
      effects[i].Draw();
    }

    for(int i = 0; i < buffer.Length; i++) {

      float r = 0f, g = 0f, b = 0f;
      for(int j = 0; j < total; j++) {
        r += effects[j].buffer[i].r;
        g += effects[j].buffer[i].g;
        b += effects[j].buffer[i].b;
      }

      buffer[i] = new Color(r , g , b , 1f);
    }
  }

  public override void LoadSettings() {
    effects = new Effect[Random.Range(2, 5)];
    total = effects.Length;

    var debugText = string.Empty;
    for(var i = 0; i < total; i++) {
      effects[i] = EffectFactory.CreateEffect((Controller.EffectTypes)Random.Range(0, numberOfEffectTypes));
      effects[i].Init();
      effects[i].LoadSettings();
      debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
    }

    controller.debugText.text = debugText;
  }

  

}