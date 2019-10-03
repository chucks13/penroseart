﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class Nibbler : Effect {
  private int last = 0;
  private int current = 1;
  private Settings setting;

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void Draw() {
    FadeAll();
    for(var x = 0; x < 5; x++) {
      for(var i = 0; i < 40; i++) {
        var neighbor = controller.geometry.tileData[current].neighbors[Random.Range(0, 4)];
        
        if(neighbor < 0) continue;
        if(neighbor == last) continue;

        last = current;
        current = neighbor;
        
        buffer[current] = setting.randomColor ? Color.HSVToRGB(Random.value, 1f, 1f) : setting.color;
        break;
      }
    }
  }

  public override void LoadSettings() {
    if(controller.sparkleSettings.Length > 0) {
      setting = controller.nibblerSettings[Random.Range(0, controller.nibblerSettings.Length)];
    } else {
      setting.Randomize();
    }

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