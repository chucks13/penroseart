using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class RainbowBars : ScreenEffect {

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
    if(controller.rainbowBarsSettings.Length > 0) {
      setting = controller.rainbowBarsSettings[Random.Range(0, controller.rainbowBarsSettings.Length)];
    } else {
      setting.Randomize();
    }

    buffer.Clear();
  }

  /// <summary>
  /// Called when effect is no longer selected to be drawn by the controller
  /// </summary>
  public override void OnEnd() { }
    private Color getColor(float n)
    {
        //return Color.HSVToRGB((n + controller.dance.fixedTime) % 1f, 1f, 1f);
        return APalette.read((n + controller.dance.fixedTime) % 1f, true);
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw() {
    var color = Color.clear;
    for(int x = 0; x < width; x++) {
      for(int y = 0; y < height; y++) {

        switch(setting.direction) {
          case Direction.Up:
            color = getColor(x + y * -0.1f );
            break;
          default:
          case Direction.UpLeft:
            color = getColor(x * 0.1f + y * -0.1f);
            break;
          case Direction.UpRight:
            color = getColor(x * -0.1f + y * -0.1f );
            break;
          case Direction.Down:
            color = getColor(x + y * 0.1f );
            break;
          case Direction.DownLeft:
            color = getColor(x * 0.1f + y * 0.1f );
            break;
          case Direction.DownRight:
            color = getColor(x * -0.1f + y * 0.1f );
            break;
          case Direction.Left:
            color = getColor(x * 0.1f + y );
            break;
          case Direction.Right:
            color = getColor(x * -0.1f + y );
            break;
          
        }
        
        screenBuffer[x + (y * width)] = color;
      }
    }

    // convert the 2D Matrix buffer to a tile buffer
    ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
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