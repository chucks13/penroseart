using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

[Serializable]
public class CameraReader 
{
    WebCamTexture webcamTexture;

    private float expandMin, expandMax;
    protected static int width = -1;
    protected static int height = -1;
    protected Penrose penrose;
    private Color[] deltabuffer;            // camera samples down to this smaller buffer
    private Color[] screenBuffer;           // camera samples down to this smaller buffer
    private Color[] localBuffer;            // screen buffer samples down to this tile buffer
    private protected Color[] lastBuffer;   // last frame of local buffer
    private protected int[] age;            // countdown times for when pixel was activated
    public Color border;                    // used as an effect color
    public int style = 5;                   // mixing style
    public float thresh=0.0f;               // color delta to activate tile
    public int frames = 50;                 // countdown init value when tile is activated
    public float huestep = 0;               // hue animate rate

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    /// <summary>
    /// Called once when effect is created
    /// </summary>
    public void Init(int w,int h,int length)
    {
        width = w;
        height = h;
 
        // create the 2d buffer
        screenBuffer = new Color[width * height];

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

    void RenderCamera(Color[] effectBuffer)
    {
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
        buildAgeMask(localBuffer, thresh, frames);

        Expand(localBuffer);               // expand to cover full range
        huestep += 0.001f;
 //       saturate(localBuffer, huestep,1f,1f);             // saturate
        style = 4;
        mixEffect(localBuffer, effectBuffer, style);       // add color effects
    }

    void buildAgeMask(Color[] videoBuffer, float threshold,int timout)
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

    void mixEffect(Color[] videoBuffer,Color[] effectBuffer, int type)
    {
        for (var i = 0; i < effectBuffer.Length; i++)
        {
            Color c = effectBuffer[i];                       // default copy
            if (age[i] > 0)
            {
                c = effectBuffer[i];
            }
            else
            {
                c = videoBuffer[i];
            }
            //            c = videoBuffer[i];
            effectBuffer[i] = c;
        }

    }

    /// <summary>
    /// Expands the buffer to have at least one zero and one one
    /// </summary>

    void Expand(Color[] buffer)
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

    void saturate(Color[] buffer,float h,float s,float v)
    {
        for (int i=0;i< buffer.Length;i++)
        {
            Color c = buffer[i];
            float H, S, V;

            Color.RGBToHSV(c, out H, out S, out V);
            buffer[i] = Color.HSVToRGB((H+ h) %1f, s, v);
         }
    }

    /// <summary>
    /// put all data that can be changed or saved here
    /// </summary>
    [Serializable]
    public class Settings
    {
    };

    void FindWebCams()
    {
        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log("Name: " + device.name);
        }
    }


}