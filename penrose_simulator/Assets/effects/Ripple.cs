using Random = UnityEngine.Random;
using UnityEngine;
using System;

public class Ripple : ScreenEffect
{

  private Settings setting;
  private Color startColor;
  private Color endColor;
  private Drop[] drops;
    private Vector2 screen;

    public override string DebugText() {
    return $"Drops {drops.Length}";
  }

  public override void Init() {
    base.Init();
    setting = new Settings();
  }

  public override void OnStart() {
    if(controller.rippleSettings.Length > 0) {
      setting = controller.rippleSettings[Random.Range(0, controller.rippleSettings.Length)];
    } else {
      setting.Randomize();
    }

    drops = new Drop[0];
    }

  public override void OnEnd() {  }

  public override void Draw() {
   if (Random.value < setting.intensity) {
            Array.Resize(ref drops, drops.Length + 1);
            drops[drops.Length - 1] = new Drop();
        }
        buffer.Fade();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                screen.x = x;
                screen.y = y;
                var idx = x + (y * width);
                var sum = 0f;
                for (int i = 0; i < drops.Length; i++)
                {
                    drops[i].Update();
                    var d = Vector2.Distance(screen, drops[i].Position);
                    sum += (drops[i].radius - (d / 20)).Clamp01();
                }
                sum += 0.5f;
                sum %= 1f;
                screenBuffer[idx] = APalette.read(sum,true);//Color.HSVToRGB(sum, 1f, 1f);
            }
        }

        ConvertScreenBuffer(ref screenBuffer, in buffer);
    }

    public class Drop
    {
        private Vector2 position;
        private float velocity;
        public float radius = 0f;

        public Drop()
        {
            velocity = Random.Range(0.01f, 0.9f)/2000f;
            position = new Vector2(Random.Range(0, width), Random.Range(0, height));
        }

        public Vector2 Position => position;
        public float Radius => radius;

        public void Update()
        {
            radius += Time.deltaTime * velocity;
        }
    }

    [System.Serializable]
  public class Settings {

    public float intensity = 0.01f;

        public void Randomize() {
            intensity = Random.Range(0.01f, 0.02f);
        }

  }

}
