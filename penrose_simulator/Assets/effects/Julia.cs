using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Julia : TwoDeeEffect {

  private Settings setting;

  private float angle;

  /// <summary>
  /// Called ever frame to update the debug UI text element 
  /// </summary>
  /// <returns></returns>
  public override string DebugText() { return $""; }

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
    if(controller.mandelbrotSettings.Length > 0)
      setting = controller.mandelbrotSettings[Random.Range(0, controller.mandelbrotSettings.Length)];
    else
      setting.Randomize();

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
    buffer.Clear();

    var w = Mathf.Abs(Mathf.Sin(angle)) * setting.distance;
    var h = w * height / width;
    var xMin = -w / 2f;
    var yMin = -h / 2f;
    var xMax = xMin + w;
    var yMax = yMin + h;
    var dx = (xMax - xMin) / width;
    var dy = (yMax - yMin) / height;

    angle += setting.speed * Time.deltaTime;

    var y =  yMin + setting.yOffset + Mathf.Sin(angle);
    for(var j = 0; j < height; j++) {
      
      var x = xMin + setting.xOffset;
      for(var i = 0; i < width; i++) {

        var a = x + Mathf.Cos(angle * Mathf.PI * 2f);// * 0.125f;
        var b = y + Mathf.Sin(angle * Mathf.PI * 2f);// * 0.125f;

        var n = 0;
        while(n < setting.iterations) {
          var aa = a * a;
          var bb = b * b;

          if(aa + bb > 4f) break;

          var twoAb = 2f * a * b;

          a = aa - bb + setting.ca;
          b = twoAb + setting.cb;

          n++;
        }

        var hue = Mathf.Sqrt((float)n / setting.iterations) % 1f;
        twoDeeBuffer[i, j] = Color.HSVToRGB(hue, 1f, n == setting.iterations ? 0f : 1f);

        x += dx;
      }

      y += dy;
    }

    // convert the 2D Matrix buffer to a tile buffer
    Convert2dBuffer(ref twoDeeBuffer, in buffer);
  }

  /// <summary>
  /// put all data that can be changed or saved here
  /// </summary>
  [Serializable]
  public class Settings {

    private readonly Vector2[] valueSets = {
      new Vector2(0.285f, 0.01f), 
      new Vector2(-0.70176f, -0.3842f), 
      new Vector2(-0.835f, -0.2321f), 
      new Vector2(-0.8f, 0.156f),
      new Vector2(-0.7269f, 0.1889f)
    };

    public int iterations = 100;
    public float speed = 0.5f;
    public float distance = 2f;

    public float ca = -0.8f;
    public float cb = 0.156f;

    public float xOffset = 0f;
    public float yOffset = 0f;

    public void Randomize() {
      var i = Random.Range(0, valueSets.Length);
      ca = valueSets[i].x;
      cb = valueSets[i].y;
      speed = Random.value + 0.1f;
    }

  }

}