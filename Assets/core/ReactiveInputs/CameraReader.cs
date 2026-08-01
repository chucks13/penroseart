using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;


/// <summary>
/// Optional webcam overlay source that can draw camera-derived colors into the Penrose buffer.
/// </summary>
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
    public float effectTime;
    public float effectDelta;
    //    public Color border;                    // used as an effect color
    public float huestep = 0;               // hue animate rate
    private int[] effects = { 0, 1, -1 };
    private float[] defaults = new float[10] { 0.25f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0.5f };
    private float[] settings = new float[10] { 0.25f, 0f, 0f, 0f, 0f, 0f, 0f, 1f, 0f, 0.5f };

    /// <summary>How many leading <see cref="settings"/> entries <see cref="Init"/> restores from <see cref="defaults"/>.</summary>
    private const int ResettableSettingCount = 8;

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    /// <summary>
    /// Called once when effect is created
    /// </summary>              

    /// <summary>
    /// Allocates camera/sample buffers, discovers webcams, requests permission, and starts the active WebCamTexture.
    /// </summary>
    public void Init(int w, int h, int length)
    {
        width = w;
        height = h;

        // create the 2d buffer
        screenBuffer = new Color[width * height];

        for (int i = 0; i < ResettableSettingCount; i++)
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
    /// Seeds the camera overlay clock with a random phase.
    /// </summary>
    public void RandomizeTime()
    {
        effectTime = Random.Range(0f, 14400f);
        huestep = Random.Range(0f, 1f);
    }

    /// <summary>
    /// Advances the camera overlay clock from Unity frame time.
    /// </summary>
    public void UpdateTime()
    {
        effectDelta = Time.deltaTime;
        effectTime += effectDelta;
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    private int length() { return (int)(settings[0] * 100); }
    private float huespeed() { return settings[1]; }
    private float hue3() { return (settings[2] + huestep) % 1; }
    private float hue4() { return (settings[3] + huestep) % 1; }
    private float hue5() { return (settings[4] + huestep) % 1; }
    private float rainbow() { return (settings[5] * 0.15f) % 1; }
    private float vbrght() { return settings[7]; }
    private float mix() { return settings[8]; }
    private float thresh() { return settings[9] * 0.01f; }
    /// <summary>
    /// Samples the camera, maps it through the screen buffer, and mixes it into the destination Penrose buffer.
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

    /// <summary>
    /// Converts current WebCamTexture pixels into a tile-aligned video buffer using ScreenEffect mapping.
    /// </summary>
    private void RenderCamera(Color[] effectBuffer)
    {
        huestep += effectDelta * huespeed();
        if (mix() == 0.0f)
            return;
        // sample webcamTexture down to screenBuffer
        int blocksize = webcamTexture.width / width;
        int centerx = webcamTexture.width / 2;
        int centery = webcamTexture.height / 2;
        float fraction = 1.0f / (float)(blocksize * blocksize);

        int y1 = centery - ((height / 2) * blocksize);
        for (var sy = 0; sy < height; sy++)
        {
            int x1 = centerx - ((width / 2) * blocksize);
            for (var sx = 0; sx < width; sx++)
            {
                Color sample = Color.black;
                for (int x = 0; x < blocksize; x++)
                {
                    for (int y = 0; y < blocksize; y++)
                    {
                        sample += webcamTexture.GetPixel(x1 + x, y1 + y) * fraction;
                    }
                }
                screenBuffer[((width - 1) - sx) + (sy * width)] = sample;
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
    private void buildAgeMask(Color[] videoBuffer, float threshold, int timout)
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

    private Color getColor(int i, int effect, Color[] videoBuffer, Color[] effectBuffer)
    {
        switch (effect)
        {
            case 0:
                return videoBuffer[i];
            case 1:
                return effectBuffer[i];
            case 2:
                {
                    Color c = videoBuffer[i];
                    float H, S, V;
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
                return Color.HSVToRGB((((float)age[i]) * rainbow() + huestep) % 1f, 1f, 1f);
        }
        return effectBuffer[i];
    }
    /// <summary>
    /// Blends camera-derived colors into the active effect buffer using current camera settings.
    /// </summary>
    private void mixEffect(Color[] videoBuffer, Color[] effectBuffer)
    {
        for (var i = 0; i < effectBuffer.Length; i++)
        {
            Color c = effectBuffer[i];                       // default copy
            if (age[i] == 0)
                c = getColor(i, effects[0], videoBuffer, effectBuffer);
            else
            {
                if (age[i] > 0)
                    c = getColor(i, effects[1], videoBuffer, effectBuffer);
            }
            effectBuffer[i] = Color.Lerp(effectBuffer[i], c, mix());
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
        for (int i = 0; i < buffer.Length; i++)
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

    /// <summary>
    /// Enumerates available webcam devices and updates the camera count.
    /// </summary>
    void FindWebCams()
    {
        foreach (var device in WebCamTexture.devices)
        {
            Debug.Log("Name: " + device.name);
        }
    }


}