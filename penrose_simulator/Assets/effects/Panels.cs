using Random = UnityEngine.Random;
using UnityEngine;

public class Panels : EffectBase
{
    private Color[] colors;
    private int which;
    private Settings setting;
    EffectBase ef0;
    EffectBase ef1;

    public override string DebugText() => "Panels";

    public override void Init()
    {
        base.Init();
        mixer = true;       // must come after base.Init();
        setting = new Settings();
   }

   // Should be called every time an effect is turned on
    public override void OnStart()
    {
        which = Random.Range(0,2);
        switch(which)
        {
            case 0:
                break;
            case 1:
                colors = new Color[2];
                for (int i = 0; i < 2; i++)
                {
                    colors[i] = Color.HSVToRGB(Random.value, Random.value, 1f);
                }
                break;
            case 2:
                ef0 = GetRandomEffect();
                ef0.Init();
                ef0.OnStart();
                ef1 = GetRandomEffect();
                ef1.Init();
                ef1.OnStart();
                break;

        }
        controller.debugText.text = "Panels";
        buffer.Clear();

    }

    // Should be called every time an effect is turned off
    public override void OnEnd() { }

    public override void Draw()
    {
        switch(which)
        {
            case 0:
                buffer.Fade();
                if (Random.Range(0, 5) == 0)
                {
                    int section = Random.Range(0, 18);
                    float h1 = Random.value;
                    float h2 = Random.value;
                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Penrose.TileData t = tiles[i];
                        if (t.section == section)
                        {
                            Color c = Color.HSVToRGB(((t.ring % 4) < 2) ? h1 : h2, 1f, 1f);
                            buffer[i] = c;
                        }
                    }
                }
                break;
            case 1:
                {
                    var time = Mathf.InverseLerp(0f, 1, Mathf.PingPong(Time.time, 1));

                    var color1 = Color.Lerp(colors[0], colors[1], time);
                    var color2 = Color.Lerp(colors[1], colors[0], time);

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        Penrose.TileData t = tiles[i];
                        int v =( (t.ring % 4) < 2) ?1:0;
                        v ^= ((t.section & 1) == 0) ? 1 : 0;
                        v ^= (((t.section/6) & 1) == 0) ? 1 : 0;
                        buffer[i] = v == 0 ? color1 : color2;
                    }

                }
                break;
            case 2:
                ef0.Draw();
                ef1.Draw();
                for (int i = 0; i < buffer.Length; i++)
                {
                    Penrose.TileData t = tiles[i];
                    int v = ((t.section & 1) == 0) ? 1 : 0;
                    v ^= (((t.section / 6) & 1) == 0) ? 1 : 0;
                    buffer[i] = v == 0 ? ef0.buffer[i] : ef1.buffer[i];
                }
                break;
        }
    }


    [System.Serializable]
    public class Settings
    {
    }

}