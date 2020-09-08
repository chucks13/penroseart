using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

public class CameraReader : ScreenEffect
{
    WebCamTexture webcamTexture;
    private protected Color[] localBuffer;
    private protected Color[] lastBuffer;
    private protected int[] age;

    private EffectBase[] effects;
    private Factory<EffectBase> factory;
    private Color border;
    public int style=5;


    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    /// <summary>
    /// Called once when effect is created
    /// </summary>
    public override void Init()
    {
        base.Init();
        factory = new Factory<EffectBase>();
        localBuffer = new Color[buffer.Length];
        lastBuffer = new Color[buffer.Length];
        age = new int[buffer.Length];
        FindWebCams();
        Application.RequestUserAuthorization(UserAuthorization.WebCam);
        webcamTexture = new WebCamTexture
        {
            requestedWidth = width,
            requestedHeight = height
        };
        webcamTexture.Play();
    }
    private EffectBase GetRandomEffect()
    {
        var effect = factory.Create(factory.Types[Random.Range(0, factory.Count)]);
        return effect.Name == Name ? GetRandomEffect() : effect;
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    public override void OnStart()
    {

        buffer.Clear();
        effects = new EffectBase[2];

        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].Init();
            effects[i].OnStart();
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
            border = Color.HSVToRGB(Random.value, 1, 1);
        }
        style = Random.Range(0, 6);     // pick a style

        controller.debugText.text = debugText;
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    public override void Draw()
    {
        for (int i = 0; i < 2; i++)
        {
            effects[i].Draw();
        }
        if (Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            if (webcamTexture.isPlaying)
            {
                RenderCamera();
                return;
            }

        }
        // backup effect
        for(int i=0;i<buffer.Length;i++)
            buffer[i] = effects[0].buffer[i];
    }

    void RenderCamera()
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
        ConvertScreenBuffer(ref screenBuffer, in localBuffer);


        for (var i = 0; i < localBuffer.Length; i++)
        {
            float dr = lastBuffer[i].r - localBuffer[i].r;
            float dg = lastBuffer[i].g - localBuffer[i].g;
            float db = lastBuffer[i].b - localBuffer[i].b;
            float d = (dr * dr) + (dg * dg) + (db * db);
            Color c = localBuffer[i];                       // default copy
            if (d > 0.1)
                age[i] = 50;
            if (age[i] > 0)
            {
                age[i]--;
                switch(style)
                {
                    case 0: break;          
                    case 1: c = effects[0].buffer[i];   break;
                    case 2: c = effects[0].buffer[i]; break;
                    case 3: break;
                    case 4: c = border; break;
                    case 5: c = border; break;

                }
            }
            else
            {
                switch (style)
                {
                    case 0: break;          
                    case 1: c = effects[1].buffer[i]; break;
                    case 2: break;
                    case 3: c = effects[1].buffer[i]; break;
                    case 4: break;
                    case 5: c = effects[1].buffer[i]; break;

                }
            }
            buffer[i] = c;
            lastBuffer[i] = localBuffer[i];

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