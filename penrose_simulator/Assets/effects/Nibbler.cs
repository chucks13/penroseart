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


  private int GetNeighbor() {
    var neighbor = controller.penrose.geometry.tileData[current].neighbors[Random.Range(0, 4)];
    if(neighbor == -1 || neighbor == last) neighbor = GetNeighbor();
    return neighbor;
  }

  public override void Draw() {
    FadeAll();
    int count = (int)(controller.dance.deltaTime * 300f);
    for (var x = 0; x < count; x++) {
      //for(var i = 0; i < 40; i++) {
        //var neighbor = controller.penrose.geometry.tileData[current].neighbors[Random.Range(0, 4)];
        
        //if(neighbor < 1 || neighbor > 900) neighbor = last;
        //if(neighbor == last) continue;
        


        last = current;
        current = GetNeighbor();

        //current = neighbor;
        
        buffer[current] = setting.randomColor ? 
                    Color.HSVToRGB(Random.value, 1f-controller.dance.decay, 1f) :
                    ( setting.color*(1f + controller.dance.decay));
        //break;
      //}
    }

    //controller.debugText.text = $"{controller.penrose.geometry.tileData[current].ToString()}";
    var tile = controller.penrose.geometry.tileData[current];
    var neighbors = $"({tile.neighbors[0]}, {tile.neighbors[1]}, {tile.neighbors[3]}, {tile.neighbors[0]})";
    controller.debugText.text = $"Current: {current}\nType: {tile.type}\nPos: {tile.center.ToString()}\nNeighbors: {neighbors}";


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