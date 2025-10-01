//using System;
//using UnityEngine;
//using Random = UnityEngine.Random;
//
//public class Example2d : TwoDeeEffect {
//
//  private Settings setting;
//
//  /// <summary>
//  /// Called ever frame to update the debug UI text element 
//  /// </summary>
//  /// <returns></returns>
//  public override string DebugText() { return $""; }
//
//
//  /// <summary>
//  /// Called once when effect is created
//  /// </summary>
//  public override void Init() {
//    base.Init();
//    setting = new Settings();
//  }
//
//  /// <summary>
//  /// Called when effect is selected by controller to be drawn every frame
//  /// </summary>
//  public override void OnStart() {
//    if(controller.example2dSettings.Length > 0) {
//      setting = controller.example2dSettings[Random.Range(0, controller.example2dSettings.Length)];
//    } else {
//      setting.Randomize();
//    }
//
//    buffer.Clear();
//  }
//
//  /// <summary>
//  /// Called when effect is no longer selected to be drawn by the controller
//  /// </summary>
//  public override void OnEnd() { }
//
//  /// <summary>
//  /// Called every frame by controller when the effect is selected
//  /// </summary>
//  public override void Draw() {
//    var color = Color.red;
//    for(int x = 0; x < width; x++) {
//      for(int y = 0; y < height; y++) {
//
//        twoDeeBuffer[x, y] = color;
//      }
//    }
//
//    // convert the 2D Matrix buffer to a tile buffer
//    TwoDeeEffect.Convert2dBuffer(ref twoDeeBuffer, in buffer);
//  }
//
//
//  /// <summary>
//  /// put all data that can be changed or saved here
//  /// </summary>
//  [Serializable]
//  public class Settings {
//
//    public Direction direction;
//
//    public void Randomize() { direction = (Direction)Random.Range(0, 8); }
//
//  }
//
//}