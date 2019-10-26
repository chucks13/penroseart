using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Plasma : ScreenEffect {

  private Settings setting;

  /// <summary>
  /// Called ever frame to update the debug UI text element 
  /// </summary>
  /// <returns></returns>
  public override string DebugText() { return $"{setting.direction.ToString()}"; }


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
    if(controller.plasmaSettings.Length > 0) {
      setting = controller.plasmaSettings[Random.Range(0, controller.plasmaSettings.Length)];
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
    var color = Color.clear;
    for(int x = 0; x < width; x++) {
      for(int y = 0; y < height; y++) {

        switch(setting.direction) {
          case Direction.Up:
            color = Color.HSVToRGB((x + y * -0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          default:
          case Direction.UpLeft:
            color = Color.HSVToRGB((x * 0.1f + y * -0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          case Direction.UpRight:
            color = Color.HSVToRGB((x * -0.1f + y * -0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          case Direction.Down:
            color = Color.HSVToRGB((x + y * 0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          case Direction.DownLeft:
            color = Color.HSVToRGB((x * 0.1f + y * 0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          case Direction.DownRight:
            color = Color.HSVToRGB((x * -0.1f + y * 0.1f + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          case Direction.Left:
            color = Color.HSVToRGB((x * 0.1f + y + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          case Direction.Right:
            color = Color.HSVToRGB((x * -0.1f + y + controller.dance.fixedTime) % 1f, 1f, 1f);
            break;
          
        }
        
        screenBuffer[x + (y * width)] = color;
      }
    }

    // convert the 2D Matrix buffer to a tile buffer
    ScreenEffect.Convert2dBuffer(ref screenBuffer, in buffer);
  }


  /// <summary>
  /// put all data that can be changed or saved here
  /// </summary>
  [Serializable]
  public class Settings {

    public Direction direction;

    public void Randomize() { direction = (Direction)Random.Range(0, 8); }

  }

}