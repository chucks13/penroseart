using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class MetaBalls : ScreenEffect {
  private Settings setting;
  private Ball[] balls;
  private Vector2 screen;

  public override string DebugText() { return$""; }

  public override void OnStart() {
    setting = new Settings();

    if(controller.rainbowBarsSettings.Length > 0) {
      setting = controller.metaBallsSettings[Random.Range(0, controller.metaBallsSettings.Length)];
    } else { setting.Randomize(); }

    buffer.Clear();

    balls = new Ball[setting.total];
    for(int i = 0; i < balls.Length; i++) { balls[i] = new Ball(); }
  }

  public override void OnEnd() { }

  public override void Draw() {
    buffer.Fade();

    for(int x = 0; x < width; x++) {
      for(int y = 0; y < height; y++) {
        screen.x = x;
        screen.y = y;
        var idx = x + (y * width);
        var sum = 0f;
        for(int i = 0; i < balls.Length; i++) {
          balls[i].Update();
          var d = Vector2.Distance(screen, balls[i].Position);
          sum += setting.radius / d;
        }

        sum = sum.Clamp();
                screenBuffer[idx] = APalette.read(sum,true);//Color.HSVToRGB(sum, 1f, 1f);
      }
    }

    ConvertScreenBuffer(ref screenBuffer, in buffer);
  }

  public class Ball {
    private Vector2 position;
    private Vector2 velocity;

    public Ball() {
      velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) / 60f;
      position = new Vector2(Random.Range(0, width), Random.Range(0, height));
    }

    public Vector2 Position => position;

    public void Update() {
      position += Time.deltaTime * velocity;
      if(position.x < 5f || position.x > width - 5f) velocity.x *= -1;
      if(position.y < 2f || position.y > height - 2f) velocity.y *= -1;
    }
  }

  /// <summary>
  /// put all data that can be changed or saved here
  /// </summary>
  [Serializable]
  public class Settings {
    public int total = 8;
    public float radius = 1f;

    public void Randomize() {
      //total = Random.Range(5, 16);
      //radius = Random.Range(0.5f, 1f);
    }
  }
}