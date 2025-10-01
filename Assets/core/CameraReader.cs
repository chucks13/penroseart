using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;


[Serializable]
public class CameraReader
{
    WebCamTexture webcamTexture;

    private int pingstate = 0;
    private float expandMin, expandMax;
    protected static int width = -1;
    protected static int height = -1;
    protected Penrose penrose;
    private Color[] deltabuffer;            // camera samples down to this smaller buffer
    private Color[] screenBuffer;           // camera samples down to this smaller buffer
    private Color[] localBuffer;            // screen buffer samples down to this tile buffer
    private protected Color[] lastBuffer;   // last frame of local buffer
    private protected int[] age;            // countdown times for when pixel was activated
//    public Color border;                    // used as an effect color
    public float huestep = 0;               // hue animate rate
    private int currentCameraIdx = 0;
     private int[] effects = { 0, 1, -1 };
    private float[] defaults = new float[10] { 0.25f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f,0.5f };
    private float[] settings = new float[10] { 0.25f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f,0.5f };
    private string[] knobs = new string[10] { "/2/fader1", "/2/fader2", "/2/fader3", "/2/fader4", "/2/fader5", "/2/fader6", "/2/fader7", "/2/fader8", "/2/fader9", "/2/rotary1", };
    private string[] resets = new string[8] { "/2/push1", "/2/push2", "/2/push3", "/2/push4", "/2/push5", "/2/push6", "/2/push7", "/2/push8" };

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    /// <summary>
    /// Called once when effect is created
    /// </summary>              

    public OscMessage makemessage(string address, float value)
    {
        OscMessage message = new OscMessage();
        message.address = address;
        message.values.Add(value);
        return message;
    }

    public void Init(int w,int h,int length)
    {
        width = w;
        height = h;
 
        // create the 2d buffer
        screenBuffer = new Color[width * height];

        for (int i = 0; i < resets.Length; i++)     // resets
        {
           settings[i] = defaults[i];
        }

        expandMin = 0;
        expandMax = 1.0f;
        deltabuffer = new Color[length];
        lastBuffer = new Color[length];
        localBuffer = new Color[length];
        age = new int[length];
        FindWebCams();
        Application.RequestUserAuthorization(UserAuthorization.WebCam);
        webcamTexture = new WebCamTexture
        {
            requestedWidth = width,
            requestedHeight = height
        };
        webcamTexture.Play();
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    private int length() { return (int)(settings[0] * 100); }
    private float huespeed() { return settings[1]; }
    private float hue3() { return (settings[2]+ huestep) %1; }
    private float hue4() { return (settings[3] + huestep) % 1; }
    private float hue5() { return (settings[4] + huestep) % 1; }
    private float rainbow() { return (settings[5]* 0.15f)%1; }
    private float vbrght() { return settings[7]; }
    private float mix() { return settings[8]; }
    private float thresh() { return settings[9] * 0.01f; }
    public void Draw(Color[] buffer)
    {
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            if (webcamTexture.isPlaying)
            {
                RenderCamera(buffer);
                return;
            }
        }
    }

    private void RenderCamera(Color[] effectBuffer)
    {
        huestep += Time.deltaTime * huespeed();
        if (mix()==0.0f)
            return;
        // sample webcamTexture down to screenBuffer
        int blocksize = webcamTexture.width / width;
        int centerx = webcamTexture.width / 2;
        int centery = webcamTexture.height / 2;
        float fraction = 1.0f / (float)(blocksize * blocksize);

        int y1 = centery - ((height/2) * blocksize) ;
        for (var sy = 0; sy < height; sy++)
        {
            int x1 = centerx - ((width/2) * blocksize) ;
            for (var sx = 0; sx < width; sx++)
            {
                Color sample = Color.black;
                for (int x=0; x<blocksize;x++)
                {
                    for(int y=0;y<blocksize;y++)
                    {
                        sample += webcamTexture.GetPixel(x1+x,y1+y)* fraction;
                    }
                }
                screenBuffer[((width-1)-sx) + (sy * width)] = sample;
                x1 += blocksize;
            }
            y1 += blocksize;
        }

        // sample screenBuffer down to localBuffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in localBuffer);
        buildAgeMask(localBuffer, thresh(), length());

//        Expand(localBuffer);               // expand to cover full range
//        huestep += 0.001f;
//        saturate(localBuffer, huestep,1f,1f);             // saturate
        mixEffect(localBuffer, effectBuffer);       // add color effects
    }


    /*
     * buildAgeMask()  // build the age buffer based on frame differences
     * Color[] videoBuffer    array to be compared
     * float threshold delta squareed it has to pass to reset the age
     * int timout age setting when the threshold is crossed
     */
    private void buildAgeMask(Color[] videoBuffer, float threshold,int timout)
    {
        for (var i = 0; i < videoBuffer.Length; i++)
        {
            float dr = lastBuffer[i].r - videoBuffer[i].r;
            float dg = lastBuffer[i].g - videoBuffer[i].g;
            float db = lastBuffer[i].b - videoBuffer[i].b;
            float d = (dr * dr) + (dg * dg) + (db * db);
            if (d > threshold)
                age[i] = timout;
            if (age[i] > 0)
                age[i]--;
            lastBuffer[i] = videoBuffer[i];
            deltabuffer[i] = Color.HSVToRGB(1, 0, d);
        }
    }

    /* mixEffect// mix the effect into the video buffer based on age
     * Color[] videoBuffer  incoming video
     * Color[] effectBuffer  buffer thats being drawon on
     * int type not used at this time,  will be the effect type
     */

    private Color getColor(int i,int effect, Color[] videoBuffer, Color[] effectBuffer)
    {
        switch(effect)
        {
            case 0:
                return videoBuffer[i];
            case 1:
                return effectBuffer[i];
            case 2:
            {
                Color c = videoBuffer[i];
                float H, S, V;
                float h = 0.0f;     //float h delta to add to the hue
                float s = 1.0f;     // float s  new saturation value
                float v = 1.0f;     // float v  new v value

                Color.RGBToHSV(c, out H, out S, out V);
                return Color.HSVToRGB((H + hue3()) % 1f, s, v);
            }
            case 3:
                return Color.HSVToRGB(hue4(), 1f, vbrght());
            case 4:
                return Color.HSVToRGB(hue5(), 1f, vbrght());
            case 5:
                return Color.HSVToRGB((((float)age[i])*rainbow()+huestep)%1f, 1f, 1f);
        }
        return effectBuffer[i];
    }
    private void mixEffect(Color[] videoBuffer,Color[] effectBuffer)
    {
        for (var i = 0; i < effectBuffer.Length; i++)
        {
            Color c = effectBuffer[i];                       // default copy
            if (age[i] ==0)
                c = getColor(i, effects[0], videoBuffer, effectBuffer);
            else
            {
                if (age[i]>0)
                    c = getColor(i, effects[1], videoBuffer, effectBuffer);
            }
            effectBuffer[i] = Color.Lerp(effectBuffer[i],c, mix());
        }

    }

    /// <summary>
    /// Expands the buffer to have at least one zero and one one
    /// </summary>

    /* Expand()  // just a video effect to saturate 
     * Color[] buffer  incoming data
     * 
     */
    private void Expand(Color[] buffer)
    {
        float max = 0.0f;
        float min = 1.0f;
        for(int i=0;i< buffer.Length;i++)
        {
            Color c = buffer[i]; 
            if (c.r > max) max = c.r;
            if (c.g > max) max = c.g;
            if (c.b > max) max = c.b;
            if (c.r < min) min = c.r;
            if (c.g < min) min = c.g;
            if (c.b < min) min = c.b;
        }
        expandMin += (min - expandMin) * 0.05f;
        expandMax += (max - expandMax) * 0.05f;
        float delta = expandMax - expandMin;
        if (delta == 0) return;
        float scale = 1.0f / delta;
        for (int i = 0; i < buffer.Length; i++)
        {
            Color c = buffer[i];
            c.r = (c.r - expandMin) * scale;
            c.g = (c.g - expandMin) * scale;
            c.b = (c.b - expandMin) * scale;
            buffer[i] = c;
        }
    }

    /// <summary>
    /// put all data that can be changed or saved here
    /// </summary>
    [Serializable]
    public class Settings
    {
    };

    private void selectCamera(OscMessage om,int n)
    {
        if(om.GetInt(0)==1)
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (n<devices.Length)
            {
                webcamTexture.Stop();
                webcamTexture.deviceName = devices[n].name;
                webcamTexture.Play();

            }
        }
    }
    public void OSCpage2(OscMessage om, ArrayList oms)
    {
        for (int i = 0; i < resets.Length; i++)     // resets
        {
            if (om.address == resets[i])
            {
                settings[i] = defaults[i];
                oms.Add(makemessage(knobs[i], settings[i]));
                return;
            }
        }
        for (int i = 0; i < knobs.Length; i++)     // knobs
        {
            if (om.address == knobs[i])
            {
                settings[i] = om.GetFloat(0);
                return;
            }
        }

        if (om.address == "/2/nav4")       // switch cameras
        {
            selectCamera(om,0);
            return;
        }


        if (om.address == "/2/nav5")       // switch cameras
        {
            selectCamera(om, 1);
            return;
        }


        if (om.address.StartsWith("/6/toggle"))
        {
            int button = int.Parse(om.address.Substring(9));
            button -= 33;
            int effect = button / 8;
            int setting = button % 8;
            String answer = "/6/toggle" + (33 + (effect * 8) + effects[effect]);
            oms.Add(makemessage(answer, 0));
            effects[effect] = setting;
            answer = "/6/toggle" + (33 + (effect * 8) + effects[effect]);
            oms.Add(makemessage(answer, 1));
            return;
        }
        if (om.address == "/2")         // init page
        {
            for (int i = 0; i < knobs.Length; i++)
                oms.Add(makemessage(knobs[i], settings[i]));
            for (int i = 0; i < 3; i++)
                oms.Add(makemessage("/6/toggle" +( 33 + (i * 8) + effects[i]), 1.0f));
            return;
        }
        if(om.address=="/ping")
        {
            if(pingstate<10)
            {
                oms.Add(makemessage(knobs[pingstate], settings[pingstate]));

            }
            else
            {
                int button = pingstate - 10;
                int row = button / 8;
                int column = button % 8;
                bool state = effects[row] == column;
                oms.Add(makemessage("/6/toggle" +( 33 + button), state?1f:0f));

            }


            pingstate++;
            pingstate %=( 10+24);

        }
        return;
    }

    public void OSCHandler(OscMessage om, ArrayList oms)
    {
        OSCpage2(om, oms);
    }

    void FindWebCams()
    {
        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log("Name: " + device.name);
        }
    }


}