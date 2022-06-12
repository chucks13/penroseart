using System;
using UnityEngine;
using Random = UnityEngine.Random;


public class Tunnel : ScreenEffect
{

    private Settings setting;

    public override void OnStart()
    {
        if(controller.tunnelSettings.Length > 0)
        {
            setting = controller.tunnelSettings[Random.Range(0, controller.tunnelSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }
        buffer.Clear();
    }

    public override void OnEnd() { }

    public override string DebugText()
    {
        return $"Density: {setting.density}\n" +
        $"Speed: {setting.speed}\n" +
        $"Mix: {setting.mix}\n";
    }
    public override void Init()
    {
        base.Init();
        setting = new Settings();
    }
  
    public override void Draw()
    {
        for (int i = 0; i <  Penrose.Total; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * 0.03f);
            float y = Mathf.Abs(tiles[i].center.y * 0.03f);
            float distance = Mathf.Sqrt((x * x) + (y * y));
            var color = i * setting.density + controller.dance.fixedTime * setting.speed + distance * setting.mix;
            buffer[i] = Color.HSVToRGB(color % 1f, 1f, 1f);
        }
  }

    [Serializable]
    public class Settings
    {

        public float density;
        public float speed;
        public float mix;

        public void Randomize()
        {
            density = Random.Range(0.0004f, 0.003f);
            speed = Random.Range(0.1f, 1f);
            mix = Random.Range(0.01f, 0.2f);
        }

    }

}