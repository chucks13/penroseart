using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class MetaBalls : ScreenEffect {
  private Ball[] balls;
  private Vector2 screen;
  private int total = 8;
  private float radius = 1f;

  public override void Init()
  {
    base.Init();
  }

  public override string DebugText() { return$""; }

  public override void OnStart() {
    base.OnStart();
    // Randomize logic was commented out in original class

    balls = new Ball[total];
    for(int i = 0; i < balls.Length; i++) { balls[i] = new Ball(); }
  }

  public override void OnEnd() { }

  public override void Draw() {
    float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.5f, beatEnable);

    buffer.Fade();

    for(int x = 0; x < width; x++) {
      for(int y = 0; y < height; y++) {
        screen.x = x;
        screen.y = y;
        var idx = x + (y * width);
        var sum = 0f;
        for(int i = 0; i < balls.Length; i++) {
          balls[i].Update(effectDelta);
          var d = Vector2.Distance(screen, balls[i].Position);
          sum += radius / d;
        }

        sum = sum.Clamp();
        screenBuffer[idx] = APalette.read(sum, true) * beatBrightness;
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

    public void Update(float time) {
      position += time * velocity;
      if(position.x < 5f || position.x > width - 5f) velocity.x *= -1;
      if(position.y < 2f || position.y > height - 2f) velocity.y *= -1;
    }
  }
}