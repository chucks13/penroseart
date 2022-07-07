using System;
using System.IO;

using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * Gradient palette - Chuck Sommerville
 * given a palette table, this will return a value within
 * 0f will return the first entry, 1f will return entry length-1
 * If hard is true then there is no Lerping
 */

public class GPalette
{
    public Color[] values;
    public int length;
    public bool blend;

    // init function
    private void Populate(Color[] initialvalues, bool blendtype = false)
    {
        Color[] list = initialvalues;
        length = list.Length;
        blend = blendtype;
        values = list;
        length = values.Length;
    }

    // array constructor
    public GPalette(Color[] initialvalues, bool blendtype = false)
    {
        Populate(initialvalues, blendtype);
    }

    private Color[] listFromString(string list)
    {
        string[] colors = list.Split(',');
        length = colors.Length;
        Color[] values = new Color[length];
        for (int i = 0; i < length; i++)
        {
            string color = colors[i];
            uint raw = uint.Parse(colors[i], System.Globalization.NumberStyles.AllowHexSpecifier);
            values[i] = new Color32((byte)(raw >> 16), (byte)(raw >> 8), (byte)(raw), 0);
        }
        return values;
    }
    // string constructor
    public GPalette(string list, bool blendtype = false)
    {
        Color[] values = listFromString(list);
        Populate(values, blendtype);
    }

    // random constructor
    // general read
    public Color read(float i, bool doblend=false)
    {
        // check looping
        if (doblend)
            blend = true;
        if (i < 0f)
        {
            float bottom = Mathf.Floor(i);
            i -= bottom;
        }
        i %= 1f;
        // check boundries
        if (i <= 0)
            return values[0];
        if (i >= (length - 1))
            return values[length - 1];
        // find color in list
        if (length > 1)
        {
            float scaled = i * (float)(length - 1);
            int first = Mathf.FloorToInt(scaled);
            float fract = scaled % 1f;
            if (!blend)
                return (fract < 0.5f) ? values[first] : values[first + 1];
            return Color.Lerp(values[first], values[first + 1], fract);
        }

        return new Color(0f, 0f, 0f);
    }


}

public class AnimPalette
{
    List<string> names = new List<string>();
    bool fading = false;
    List<GPalette> palettes = new List<GPalette>();
    GPalette current = null;
    GPalette next = null;
    float tween = 0;
    const float transitionTime = 3f;
    public static string[] StaticSamples =
    {
        "ff0000,000000,ffff00,000000,00ff00,000000,00ffff,000000,0000ff,000000,ff00ff,000000",
        "ffff00,000000,00ffff,000000,ff00ff,000000",
        "ff0000,000000,00ff00,000000,0000ff,000000",
        "ff0000,ffff00,00ff00,00ffff,0000ff,00ffff",
        "5500AB,84007C,B5004B,E5001B,E81700,B84700,AB7700,ABAB00,AB5500,DD2200,F2000E,C2003E,8F0071,5F00A1,2F00D0,0007F9",
        "000000,330000,660000,990000,CC0000,FF0000,FF3300,FF6600,FF9900,FFCC00,FFFF00,FFFF33,FFFF66,FFFF99,FFFFCC,FFFFFF",
    };
    public AnimPalette()
    {
        // built in palettes
        for (int i=0;i<StaticSamples.Length;i++)
        {
//            palettes.Add(new GPalette(StaticSamples[i]));
        }
        // leadables
        LoadFromFile("palettedata.txt");
        LoadFromFile("jenpalettes.txt");
        current = palettes[0];
    }
    public void Change()
    {
        if(tween==0f)
        {
            next = palettes[Random.Range(0, palettes.Count)];
            tween = transitionTime;
            fading = Random.Range(0, 3) > 0;
        }
    }

    public void Update()
    {
        if(tween>0f)
        {
            tween -=Time.deltaTime;
            if (tween <= 0f)
            {
                current = next;
                tween = 0f;
                if (fading)
                    Change();
            }
        }
    }

    public Color read(float i,bool doblend=false)
    {
        if (tween == 0f)
            return current.read(i);
        else
            return Color.Lerp(next.read(i, doblend), current.read(i, doblend), tween/ transitionTime);
    }

    public void LoadFromFile(string fileName)
    {
        var sr = new StreamReader(Application.streamingAssetsPath + "/" + fileName);
        string fileContents = sr.ReadToEnd();
        sr.Close();
        while(true)
        {
            int def = fileContents.IndexOf("DEFINE_GRADIENT_PALETTE(", 0);
            if (def < 0)
                break;
            fileContents = fileContents.Substring(def + 24);
            def = fileContents.IndexOf(")", 0);
            int x;
            for( x=0;x<names.Count;x++)
            {
                if (def.Equals(names[x]))
                    break;
            }
            if(x<names.Count)
            {
                continue;
            }

            names.Add(fileContents.Substring(0, def));
            fileContents = fileContents.Substring(def + 1);
            // get the color info
            int begin = fileContents.IndexOf("{", 0)+1;
            int end = fileContents.IndexOf("}", begin);
            string data = fileContents.Substring(begin, end-begin);
            colortab[] source = data2colortab(data);
            Color[] dest = new Color[32];           // how many mapped entries
            // do the mapping
            for ( x=0;x< dest.Length; x++)        // for each output color
            {
                float f = (float)x /(float)dest.Length;       // position in color table
                dest[x] = Map2Palette(f, source);
            }
            palettes.Add(new GPalette(dest));
        }
    }

    private Color Map2Palette(float f,colortab[] source)
    {
        for (int y = 1; y < source.Length; y++)               // for each input pair entry
        {
            float min = source[y - 1].i;            // bracket values
            float max = source[y].i;
            if ((min <= f) && (max >= f))                 // is this in the bracket
            {
                float tween = f.Map(min, max, 0f, 1f);          // do the LERP
                return Color.Lerp(source[y - 1].c, source[y].c, tween);
            }
        }
        return new Color32(0, 0, 0, 0);     // default
    }

    // convert the string data for the palette into a table of fractional indexes and colors
    class colortab
    {
        public float i;
        public Color c;
    };
     private colortab[] data2colortab(string data)
    {
        // cleanup
        data = data.Replace("\n", "");
        data = data.Replace("\r", "");
        data = data.Replace(" ", "");
        string[] subs = data.Split(',');
        // build
        colortab[] table = new colortab[subs.Length/4];
        int x = 0;
        for (int y = 0; y < table.Length;y++ )
        {
            if((x+3)>subs.Length)
            {
                break;
            }
            table[y] = new colortab();
            table[y].i = float.Parse(subs[x++])/255f;
            byte r = byte.Parse(subs[x++]);
            byte g = byte.Parse(subs[x++]);
            byte b = byte.Parse(subs[x++]);
            table[y].c = new Color32(r,g,b,0);
        }
        return table;
    }


}
