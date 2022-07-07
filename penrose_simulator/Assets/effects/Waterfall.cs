using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class Waterfall : ScreenEffect
{

    private Settings setting;
    private Drop[] drops;

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    public override string DebugText()
    {
        return $"Drops: {setting.numDrops}\n" +
            $"Background stretch: {setting.backgrounStretch}\n" +
            $"Background speed: {setting.backgroundSpeed}\n";
    }

    /// <summary>
    /// Called once when effect is created
    /// </summary>
    public override void Init()
    {
        base.Init();
        setting = new Settings();
        drops = new Drop[setting.numDrops];
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {
        if(controller.waterfallSettings.Length > 0)
        {
            setting = controller.waterfallSettings[Random.Range(0, controller.waterfallSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }
        buffer.Clear();
        drops = new Drop[setting.numDrops];
        for (int i = 0; i < drops.Length; i++)
        {
            drops[i] = new Drop();
        }
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        for(int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                var screen = new Vector2();
                screen.x = x;
                screen.y = y;
                // background
                var color = y * setting.backgrounStretch + controller.dance.fixedTime * setting.backgroundSpeed;
                for (int i = 0; i < drops.Length; i++)
                {
                    Drop drop = drops[i];
                    drop.Update();
                    var distance = Vector2.Distance(screen, drop.position);
                    //drop
                    if (distance < drop.radius)
                    {
                        color += drop.intensity;
                    }
                    //trail
                    else if (drop.position.y < screen.y && drop.position.x > screen.x - drop.radius && drop.position.x < screen.x + drop.radius)
                    {
                        color += 25 * drop.intensity / (25 + (y - drop.position.y));
                    }
                }
                screenBuffer[x + (y * width)] = APalette.read(color % 1f, true);//Color.HSVToRGB(color % 1f, 1f, 1f);
            }
    }
    // convert the 2D Matrix buffer to a tile buffer
    ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
  }

    public class Drop
    {
        public Vector2 position;
        public float radius;
        public float speed;
        public float intensity;

        public Drop()
        {
            position = new Vector2(Random.Range(0, width), Random.Range(height, height * 10));
            radius = Random.Range(0.2f, 2f);
            speed = Random.Range(0.005f, 0.05f);
            intensity = Random.Range(0.1f, 0.5f);
        }

        public void Update()
        {
            var velocity = new Vector2();
            velocity.x = 0f;
            velocity.y = -speed;
            position += Time.deltaTime * velocity;
            if (position.y < height * -15) position = new Vector2(Random.Range(0, width), Random.Range(height, height * 10));
        }
    }

    /// <summary>
    /// put all data that can be changed or saved here
    /// </summary>
    [Serializable]
    public class Settings
    {

        public int numDrops;
        public float backgrounStretch;
        public float backgroundSpeed;

        public void Randomize()
        {
            numDrops = Random.Range(70, 100);
            backgrounStretch = Random.Range(0.001f, 0.025f);
            backgroundSpeed = Random.Range(0.01f, 0.3f);
        }

    }

}