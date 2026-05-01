using UnityEngine;

[System.Serializable]
public class Nibbler : EffectBase
{

    private const int Count = 10;
    private int[] current;
    private Settings setting;

    public override string DebugText()
    {
        var colorText = (setting.randomColor) ? "random" : setting.color.ToString();
        return $"Color: {colorText}\nFade: {setting.fade}";
    }

    public override void Init()
    {
        base.Init();
        setting = new Settings();
        current = new int[Count];
        for (int i = 0; i < Count; i++) current[i] = Random.Range(0, Penrose.Total);
    }

    public override void OnStart()
    {
        if (controller.nibblerSettings.Length > 0)
        {
            setting = controller.nibblerSettings[Random.Range(0, controller.nibblerSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }

        buffer.Clear();
    }

    public override void OnEnd() { }

    public override void Draw()
    {
        buffer.Fade(setting.fade);
        int count = (int)(Time.deltaTime * 300f);
        for (int y = 0; y < Count; y++)
        {
            for (var x = 0; x < count; x++)
            {
                current[y] = tiles[current[y]].GetRandomNeighbor();
                Color c;
                    c = setting.randomColor ?
                        Color.HSVToRGB(Random.value, 1f, 1f)
                        : setting.color;
                buffer[current[y]] = c;
            }
        }
    }

    [System.Serializable]
    public class Settings
    {

        //public float speed = 1f;   
        public bool randomColor = true;
        public Color color = Color.clear;

        [Range(0.97f, 0.999f)]
        public float fade = 0.999f;

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

            fade = Random.Range(0.97f, 0.999f);
        }

    }

}