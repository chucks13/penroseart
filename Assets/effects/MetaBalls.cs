using System;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// Renders a screen-space metaball field and maps it onto Penrose tiles.
/// </summary>
public class MetaBalls : ScreenEffect {
  private Ball[] balls;
  private Vector2 screen;
  private int total = 8;
  private float radius = 1f;

      /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
public override void Init()
  {
    base.Init();
  }

      /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
public override string DebugText() { return$""; }

      /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
public override void OnStart() {
    base.OnStart();
    // Randomize logic was commented out in original class

    balls = new Ball[total];
    for(int i = 0; i < balls.Length; i++) { balls[i] = new Ball(); }
  }

      /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
public override void OnEnd() { }

      /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
public override void Draw() {
    // Beat pulse scales metaball color output for this frame.
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

  /// <summary>
/// Moving screen-space metaball source.
/// </summary>
public class Ball {
    private Vector2 position;
    private Vector2 velocity;

    /// <summary>
    /// Creates one moving metaball source at a random screen position.
    /// </summary>
    public Ball() {
      velocity = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) / 60f;
      position = new Vector2(Random.Range(0, width), Random.Range(0, height));
    }

    /// <summary>Current screen-space metaball center.</summary>
    public Vector2 Position => position;

    /// <summary>
    /// Advances metaball position and bounces it inside the screen bounds.
    /// </summary>
    public void Update(float time) {
      position += time * velocity;
      if(position.x < 5f || position.x > width - 5f) velocity.x *= -1;
      if(position.y < 2f || position.y > height - 2f) velocity.y *= -1;
    }
  }
}