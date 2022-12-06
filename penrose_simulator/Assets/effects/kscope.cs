// By Chuck Sommerville
/*
 * loads images in assets folder, and displays them as kscope
 */
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;


public class kscope : ScreenEffect
{
    public class picture
    {
        public Texture2D tex;
        public string fname;
    };
    string fname="";
    private int[] mirrorList;
    private int[] centerline;
    List<picture> colorTex = new List<picture>();
    List<picture> monoTex = new List<picture>();
    Texture2D currentTex;
    int last = -1;
    int mode;
    int texWidth;
    int texHeight;
    int centerX;
    int centerY;
    float positionX;
    float positionY;
    float motionX;
    float motionY;
    float angle;
    float aspeed;
    int which = 0;

    /// <summary>
    /// Called ever frame to update the debug UI text element 
    /// </summary>
    /// <returns></returns>
    /// 
    public static Texture2D LoadPNG(string filePath)
    {
        Texture2D tex = null;
        byte[] fileData;

        if (File.Exists(filePath))
        {
            fileData = File.ReadAllBytes(filePath);
            tex = new Texture2D(2, 2);
            tex.LoadImage(fileData); //..this will auto-resize the texture dimensions.
        }
        return tex;
    }
    List<picture> readDirectory(string path)
    {
        List<picture> texList = new List<picture>();
        DirectoryInfo dir = new DirectoryInfo(Application.streamingAssetsPath+path);
        FileInfo[] info = dir.GetFiles("*.*");
        foreach (FileInfo onefile in info)
        {
            if (onefile.Name.Contains("meta"))
                continue;
            Texture2D tex = LoadPNG(onefile.FullName);
            picture pic = new picture();
            pic.tex = tex;
            pic.fname = onefile.Name;

            texList.Add(pic);
        }
        return texList;
    }

     public override string DebugText()
    {
        return $"file {fname} ";
    }

    /// <summary>
    /// Called once when effect is created
    /// </summary>
    private void fixCenterLineInit()
    {
        centerline = new int[8];
        int y = 0;
        for (int x = 0; x < 900; x++)
        {
            if (y == centerline.Length)
                break;
            int groupcount = mirrorList[0];     // how many copies
            bool used = false;                                    // Draw the mirrors
            for (int i = 0; i < groupcount; i++)
            {
                int groupPointer = mirrorList[1 + i];
                int groupsize = mirrorList[groupPointer];
                for (int j = 0; j < groupsize; j++)
                {
                    if (mirrorList[groupPointer + 1 + j] == x)
                        used = true;
                    break;
                }
            }
            if (!used)
                centerline[y++] = x;
        }
    }
    private void fixCenterLineDraw()
    {
        for (int i = 0; i < centerline.Length; i++)
        {
            int j = centerline[i];
            buffer[j] = buffer[j];
        }

    }
    public override void Init()
    {
        base.Init();
        colorTex = readDirectory($"/images/color");
        monoTex = readDirectory($"/images/mono");
        mirrorList = penrose.JsonRawData.shapes.mirror2;
        fixCenterLineInit();
    }

    /// <summary>
    /// Called when effect is selected by controller to be drawn every frame
    /// </summary>
    /// 
    Texture2D messTexture(Texture2D oldtex)
    {
        Texture2D newTex = new Texture2D(oldtex.width, oldtex.height);
        int swap = Random.Range(0,2);
        float a;
         for (int x=0;x< oldtex.width; x++)
        {
            for(int y=0;y< oldtex.height;y++)
            {
                var color = oldtex.GetPixel(x, y);
                switch(swap)
                {
                    case 0:
                        a = color.r;
                        color.r = color.b;
                        color.b = a;
                        break;
                    case 1:
                        a = color.r;
                        color.r = color.g;
                        color.g = a;
                        break;
                    case 2:
                        a = color.g;
                        color.g = color.b;
                        color.b = a;
                        break;
                }
                newTex.SetPixel(x, y, color);
            }
        }
        return newTex;
    }
    public override void OnStart()
    {
        mirrorList = Random.Range(0, 2) == 0 ? penrose.JsonRawData.shapes.mirror2 : penrose.JsonRawData.shapes.mirror10;
        int colorCount = colorTex.Count;
        int monoCount = monoTex.Count;
        int total = colorCount + monoCount;
        which = (which + 1+Random.Range(0,total/3)) % total;// Random.Range(0, total);
        if(which< colorCount)
        {
            currentTex = colorTex[which ].tex;
            fname = colorTex[which].fname;
            // sometime swap 2 colors
            if (Random.Range(0,3)==0)
                currentTex=messTexture(currentTex);
            mode = 0;
        }
        else
        {
            currentTex = monoTex[which- colorCount].tex;
            fname = monoTex[which - colorCount].fname;
            mode = 1;
        }
        texWidth = currentTex.width;
        texHeight = currentTex.height;
        motionX = Random.Range(1,3) / 4f; 
        motionY = Random.Range(1, 3) / 4f;
        motionX *= Random.Range(0, 2) == 0 ? 1f : -1f;
        motionY *= Random.Range(0, 2) == 0 ? 1f : -1f;

        positionX = Random.Range(0, texWidth);
        positionY = Random.Range(0, texHeight);
        centerX = texWidth / 2;
        centerY = texHeight / 2;
        angle = 0;
        aspeed= Random.Range(-1,2) / 100f;
    }

    /// <summary>
    /// Called when effect is no longer selected to be drawn by the controller
    /// </summary>
    public override void OnEnd()
    {
    }

    /// <summary>
    /// Called every frame by controller when the effect is selected
    /// </summary>
    /// 
    /*
     * x2=cosβx1−sinβy1
     * y2=sinβx1+cosβy1
     */
    public override void Draw()
    {
        if (Input.GetKeyDown(KeyCode.Return))
        {
            Init();
        }


        positionX += motionX * controller.dance.deltaTime * 60f;
        positionY += motionY * controller.dance.deltaTime * 60f;
        double m11 = Math.Cos(angle);
        double m12 = -Math.Sin(angle);
        double m21 = Math.Sin(angle);
        double m22 = Math.Cos(angle);
        double wh = width / 2;
        double yh = height / 2;
        angle += aspeed * controller.dance.deltaTime * 60f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // center about screen
                double x1 =x-wh;
                double y1 = y-yh;
                // apply rotation
                double x2 = (m11 * x1) + (m12 * y1);
                double y2 = (m21 * x1) + (m22 * y1);
                // center about texture
//                x2 += centerX;
//                y2 += centerY;
                // offset to position
                x2 += positionX;
                y2 += positionY;
                if (x2 < 0)
                    x2 = -x2;
                if (y2 < 0)
                    y2 = -y2;
                int xp = (int)x2 / texWidth;
                int yp = (int)y2 / texHeight;
                x2 %= texWidth;
                y2 %= texHeight;
                if ((xp & 1) != 0)
                    x2 = (texWidth - 1) - x2;
                if ((yp & 1) != 0)
                    y2 = (texHeight - 1) - y2;

                var color = currentTex.GetPixel((int)x2, (int)y2);
                if (mode==0)         // full color
                {
                    screenBuffer[x + (y * width)] = color;
                }
                else         // mono
                {
                     screenBuffer[x + (y * width)] = APalette.read(color.r % 1f, true);
                }
            }
        }
        // convert the 2D Matrix buffer to a tile buffer
        ScreenEffect.ConvertScreenBuffer(ref screenBuffer, in buffer);
        int groupcount = mirrorList[0];     // how many copies
        // fix missing verticle column
        fixCenterLineDraw();
        // Draw the mirrors
        for (int i = 0; i < groupcount; i++)
        {
            int groupPointer = mirrorList[1 + i];
            int groupsize = mirrorList[groupPointer];
            Color tileColor = buffer[mirrorList[groupPointer + 1]];
            for (int j = 0; j < groupsize; j++)
            {
                buffer[mirrorList[groupPointer + 1 + j]] = tileColor;
            }
        }
    }


}