using UnityEngine;
using Random = UnityEngine.Random;

public class Vortex : EffectBase
{

    private Settings setting;
    private float n;


    public spinner[] spinners;

    public override string DebugText()
    {
        return $"Vortex: {n}\n";
    }

    public override void Init()
    {
        base.Init();
        setting = new Settings();
        spinners = new spinner[4];
        for (int i = 0; i < spinners.Length; i++)
        {
            spinners[i] = new spinner();
        }
    }

    public override void OnStart()
    {
        if (controller.noiseTunnelSettings.Length > 0)
        {
            setting = controller.vortexSettings[Random.Range(0, controller.vortexSettings.Length)];
        }
        else
        {
            setting.Randomize();
        }
        for(int i=0;i<spinners.Length;i++)
        {
            spinners[i].center = new Vector2(((float)i) * 10f - 15f, 0f);
            if((i%2)==0)
            {
                spinners[i].twist *= -1f;
                spinners[i].speed *= -1f;
            }
        }
        buffer.Clear();
    }

    public override void OnEnd() { }
    public void Update()
    {
        for (int j = 0; j < spinners.Length; j++)
            spinners[j].Update();
    }

    public override void Draw()
    {
        Update();
        for (int i = 0; i < buffer.Length; i++)
        {
            int which = 0;
            float min = 100000f;
            // find the closest
            for (int j = 0; j < spinners.Length; j++)
            {
                Vector2 delta = tiles[i].position - spinners[j].center;
                float d2 = (delta.x * delta.x) + (delta.y * delta.y);
                if (d2 < min)
                {
                    min = d2;
                    which = j;
                }
            }
            // Draw the point
            buffer[i]=spinners[which].Draw(i,tiles[i].position);
        }
    }

    public class spinner
    {
        public Vector2 center;
        public int arms =1;
        public float twist =0.01f;
        public float angle =0;
        const float rad2once = 1f / (Mathf.PI * 2f);
        public float speed =0.5f;
        GPalette palette = new GPalette();

        public Color Draw(int i,Vector2 position)
        {
            Vector2 vect = position - center;
            float rotate = Mathf.Atan2(vect.y, vect.x) ;
            float length = Vector2.Distance(center, position);
            rotate += Mathf.PI;
            rotate *= rad2once;
            rotate *= arms;
            rotate += twist * length;
            rotate += angle;
            return palette.read(rotate % 1f);// Color.HSVToRGB(rotate%1f, 1f, 1f);
        }
        public void Update()
        {
            angle += speed * Time.deltaTime;
        }
    }

    [System.Serializable]
    public class Settings
    {

        public void Randomize()
        {
        }

    }

}
