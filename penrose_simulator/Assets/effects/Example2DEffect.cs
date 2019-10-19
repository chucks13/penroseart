using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Example2DEffect : TwoDeeEffect {

  private Settings setting;

  /// <summary>
  /// Called ever frame to update the debug UI text element 
  /// </summary>
  /// <returns></returns>
  public override string DebugText() { return ""; }


  /// <summary>
  /// Called once when effect is created
  /// </summary>
  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  /// <summary>
  /// Called when effect is selected by controller to be drawn every frame
  /// </summary>
  public override void OnStart() {
    if(controller.example2dEffectSettings.Length > 0) {
      setting = controller.example2dEffectSettings[Random.Range(0, controller.example2dEffectSettings.Length)];
    } else {
      setting.Randomize();
    }

    buffer.Clear();
  }

  /// <summary>
  /// Called when effect is no longer selected to be drawn by the controller
  /// </summary>
  public override void OnEnd() { }

  /// <summary>
  /// Called every frame by controller when the effect is selected
  /// </summary>
  public override void Draw() {
    for(int x = 0; x < width; x++) {
      for(int y = 0; y < height; y++) {
        twoDeeBuffer[x, y] = Color.HSVToRGB((x * 0.1f + y * 0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
      }
    }

    // convert the 2D Matrix buffer to a tile buffer
    TwoDeeEffect.ConvertBuffer(ref twoDeeBuffer, in buffer);
  }


  /// <summary>
  /// put all data that can be changed or saved here
  /// </summary>
  [Serializable]
  public class Settings {

    public void Randomize() { }

  }

}