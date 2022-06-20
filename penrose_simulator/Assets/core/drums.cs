
using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

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

    public void hit(int i,float p)
    {
        if((i>=0)&&(i<5))
            hits[i] = p*5f;
    }

    // current drum packet byte packet[9] = {0, 2, 0, 5, 0, 0, 0, 0, 0};

    void handleOpenPixel(byte[] data)
    {
        for (int i = 0; i < 5; i++)
        {
            if (data[i + 4] > 20)
                hits[i] = diameter;
        }
     }

    private void OSCpage3(OscMessage om, ArrayList oms)
    {
        if(om.address.StartsWith("/3/toggle"))      // test the drums
        {
            if(om.GetInt(0)==1)
            {
                int pad = int.Parse(om.address.Substring(9));
                hit(pad - 1, 1f);

            }
        }
        if(om.address.StartsWith("/3/rotary"))
        {

        }
        if (om.address == "/ping")
        {

        }
    }
    public void OSCHandler(OscMessage om, ArrayList oms)
    {
        OSCpage3(om, oms);
    }

}

// starburst, 0,8,11,16,20
/*
 *    20             16
 *            0
 *        11      8
 *        
 *        loops:            *47  (big loop)
 *        0,1
 *        2,
 *        3,4,5,6,7
 *       
 *        26,48        24,49
 *        
 */
