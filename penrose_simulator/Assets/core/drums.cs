
using System;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine;

[Serializable]
public class drums 
{

    protected Penrose penrose;
    protected Controller controller;
    protected Penrose.TileData[] tiles;

    private Settings setting;
    private UDPReceive listenerOpenPixel;
    private float[] hits;
    public float[] points = {  10f,-5f,   10f,5f,       0f,0f,   -10f, 5f,        -10f,-5f     };
    private Color[] colors = { Color.green, Color.yellow, Color.cyan, new Color(0xff, 0xa5, 0x00), Color.red };//};
    public float diameter=8;
    public float shrink =60;
    [HideInInspector]

    public string DebugText() => "drums";

    public void Init()
    {
        controller = Controller.Instance;
        penrose = controller.penrose;
        tiles = penrose.Tiles;
        setting = new Settings();
        listenerOpenPixel = new UDPReceive(8500, handleOpenPixel);
        hits = new float[5];
    }

    // Should be called every time an effect is turned on
    public void OnStart()
    {
    }

    // Should be called every time an effect is turned off
    public void OnEnd() { }

    // overlay the drums
    public void Draw(Color[] destBuffer)        
    {
   
        for (int i = 0; i < destBuffer.Length; i++)
        {

            float x = tiles[i].center.x; 
            float y = tiles[i].center.y;
            int k = 0;
            for(int j=0;j<5;j++)
            {
                float dx = points[k++] - x;
                float dy = points[k++] - y;
                dx = dx * dx;
                dy = dy * dy;
                float d = dx+dy;
                float v = hits[j];
                float v2 = v / 2;
                if (d<(v*v))
                {
                    if (d<(v2*v2))
                        destBuffer[i] = Color.black;
                    else
                        destBuffer[i] = colors[j];
                }
            }
        }
        for (int j = 0; j < 5; j++)
        {
            if (hits[j] > 0)
                hits[j] -= shrink* Time.deltaTime;
            if (hits[j] < 0)
                hits[j] = 0;

        }

    }

    public void Update()
    {

    }

    public class Settings
    {


    }
    void handleOpenPixel(byte[] data)
    {
        for (int i = 0; i < 5; i++)
        {
            if (data[i + 4] > 20)
                hits[i] = diameter;
        }
     }

}

