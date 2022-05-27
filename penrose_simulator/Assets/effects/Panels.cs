using Random = UnityEngine.Random;
using UnityEngine;

public class Panels : EffectBase
{

    private Settings setting;
    private Color color;

    public override string DebugText() => setting.randomColor ? "Panels: random" : $"Color: {setting.color.ToString()}";

    public override void Init()
    {
        base.Init();
        setting = new Settings();
    }

    // Should be called every time an effect is turned on
    public override void OnStart()
    {
        if (controller.sparkleSettings.Length > 0)
        {
            setting = controller.panelsSettings[Random.Range(0, controller.panelsSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }

        var text = (setting.randomColor) ? "random" : setting.color.ToString();
        controller.debugText.text = $"Color: {text}";
        buffer.Clear();
    }

    // Should be called every time an effect is turned off
    public override void OnEnd() { }

    public override void Draw()
    {
        buffer.Fade();
        int count = (int)(controller.dance.deltaTime * buffer.Length);
        for (int i = 0; i < count; i++)
        {

            if (setting.randomColor)
                color = Color.HSVToRGB(Random.value, 1f - controller.dance.decay, 1f);
            else
                color = setting.color * (1f + controller.dance.decay);

            buffer[Random.Range(0, buffer.Length)] = color; //  Color.HSVToRGB(Random.value, 1, 1);
        }
    }


    [System.Serializable]
    public class Settings
    {
        public bool randomColor = true;
        public Color color;

        public void Randomize()
        {
            if (Random.value > 0.5f)
            {
                randomColor = true;
                color = Color.clear;
            }
            else
            {
                randomColor = false;
                color = Color.HSVToRGB(Random.value, 1f, 1f);
            }
        }
    }

}