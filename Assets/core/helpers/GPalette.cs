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

/// <summary>
/// Static palette sampling helpers for discrete color palettes.
/// </summary>
public class GPalette
{
    public Color[] values;
    public int length;
    public bool blend;
    public string paletteSources;

    // init function
    /// <summary>
    /// Copies source colors into the fixed palette table, optionally interpolating across all 256 entries.
    /// </summary>
    private void Populate(Color[] initialvalues, bool blendtype = false)
    {
        Color[] list = initialvalues;
        length = list.Length;
        blend = blendtype;
        values = list;
        length = values.Length;
    }

    // array constructor
    /// <summary>
    /// Creates a palette from explicit Color entries.
    /// </summary>
    public GPalette(Color[] initialvalues, bool blendtype = false)
    {
        Populate(initialvalues, blendtype);
    }

    /// <summary>
    /// Parses a comma-separated numeric RGB list into Color values.
    /// </summary>
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
    /// <summary>
    /// Creates a palette from a serialized RGB component list.
    /// </summary>
    public GPalette(string list, bool blendtype = false)
    {
        Color[] values = listFromString(list);
        Populate(values, blendtype);
    }

    // random constructor
    // general read
    /// <summary>
    /// Samples the palette at normalized position i, optionally interpolating between entries.
    /// </summary>
    public Color read(float i, bool doblend = false)
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

/// <summary>
/// Shared animated palette state that fades between loaded gradient palettes over time.
/// </summary>
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

    public static string customSource = "5a2d81,5a2d81,5a2d81,c0c0c0,c0c0c0,63727A,63727A,000000,000000,5a2d81,5a2d81,5a2d81";

    GPalette customPalette;

    /// <summary>Loads the built-in fallback palettes.</summary>
    public AnimPalette()
        : this(string.Empty)
    {
    }

    /// <summary>Loads palette definitions from Controller-owned palette source text.</summary>
    public AnimPalette(string palettedata)
    {
        // built in palettes
        // leadables
        /*
        string liststring = readfile("filelist.txt");
        liststring = liststring.Replace("\r", "");
        liststring = liststring.Replace(" ", "");
        string[] filelist = liststring.Split('\n');
        for (int i = 0; i < filelist.Length; i++)
        {
            LoadGradientFile(filelist[i]);
            LoadHexGradientFile(filelist[i]);
        }
        */
        if (!string.IsNullOrEmpty(palettedata))
        {
            processhex(palettedata);
            processgradient(palettedata);
        }
        // if no loadable palettes, load default palettes
        if (palettes.Count == 0)
        {
            for (int i = 0; i < StaticSamples.Length; i++)
            {
                palettes.Add(new GPalette(StaticSamples[i]));
            }

        }

        //        LoadFromFile("palettedata.txt");
        //        LoadFromFile("jenpalettes.txt");
        current = palettes[0];
        customPalette = new GPalette(customSource);
    }
    /// <summary>
    /// Starts a transition toward a randomly selected loaded palette.
    /// </summary>
    public void Change()
    {
        if (tween == 0f)
        {
            next = palettes[Random.Range(0, palettes.Count)];
            tween = transitionTime;
            fading = Random.Range(0, 3) > 0;
        }
    }

    /// <summary>
    /// Advances animated palette fade and rotation state.
    /// </summary>
    public void Update()
    {
        if (tween > 0f)
        {
            tween -= Time.deltaTime;
            if (tween <= 0f)
            {
                current = next;
                tween = 0f;
                if (fading)
                    Change();
            }
        }
    }

    /// <summary>
    /// Samples the currently animated palette at normalized position i.
    /// </summary>
    public Color read(float i, bool doblend = false)
    {
        if (tween == 0f)
            return current.read(i);
        else
            return Color.Lerp(next.read(i, doblend), current.read(i, doblend), tween / transitionTime);
    }

    /// <summary>
    /// Reads a text file from StreamingAssets.
    /// </summary>
    private string readfile(string fileName)
    {
        try
        {
            var sr = new StreamReader(Application.streamingAssetsPath + "/" + fileName);
            string fileContents = sr.ReadToEnd();
            sr.Close();
            return fileContents;
        }
        catch (Exception)
        {
            // A missing/unreadable palette file degrades to "nothing to parse"; callers
            // treat the empty string as no palettes from this source.
            return "";
        }

    }

    /// <summary>
    /// Returns true if a parsed palette name has not already been loaded.
    /// </summary>
    public bool isNewName(string pname)
    {
        int x;
        for (x = 0; x < names.Count; x++)
        {
            if (pname.Equals(names[x]))
                return false;
        }
        names.Add(pname);
        return true;

    }

    /// <summary>
    /// Parses FastLED-style DEFINE_GRADIENT_PALETTE hex palette definitions.
    /// </summary>
    public void processhex(string fileContents)
    {
        while (true)
        {
            int def = fileContents.IndexOf("DEEFINE_HEX_PALETTE(", 0);
            if (def < 0)
                break;
            fileContents = fileContents.Substring(def + 20);
            def = fileContents.IndexOf(")", 0);
            string fn = fileContents.Substring(0, def);
            if (!isNewName(fn))
                continue;

            fileContents = fileContents.Substring(def + 1);
            // get the color info
            int begin = fileContents.IndexOf("{", 0) + 1;
            int end = fileContents.IndexOf("}", begin);
            string data = fileContents.Substring(begin, end - begin);
            palettes.Add(new GPalette(data));
        }

    }
    /// <summary>
    /// Loads and parses a hex gradient palette file from StreamingAssets.
    /// </summary>
    public void LoadHexGradientFile(string fileName)
    {
        processhex(readfile(fileName));

    }

    /// <summary>
    /// Parses plain gradient palette data into runtime palette tables.
    /// </summary>
    public void processgradient(string fileContents)
    {
        while (true)
        {
            int def = fileContents.IndexOf("DEFINE_GRADIENT_PALETTE(", 0);
            if (def < 0)
                break;
            fileContents = fileContents.Substring(def + 24);
            def = fileContents.IndexOf(")", 0);
            string fn = fileContents.Substring(0, def);
            if (!isNewName(fn))
                continue;

            fileContents = fileContents.Substring(def + 1);
            // get the color info
            int begin = fileContents.IndexOf("{", 0) + 1;
            int end = fileContents.IndexOf("}", begin);
            string data = fileContents.Substring(begin, end - begin);
            colortab[] source = data2colortab(data);
            Color[] dest = new Color[32];           // how many mapped entries
            // do the mapping
            for (int x = 0; x < dest.Length; x++)        // for each output color
            {
                float f = (float)x / (float)dest.Length;       // position in color table
                dest[x] = Map2Palette(f, source);
            }
            palettes.Add(new GPalette(dest));
        }

    }
    /// <summary>
    /// Loads and parses a plain gradient palette file from StreamingAssets.
    /// </summary>
    public void LoadGradientFile(string fileName)
    {
        processgradient(readfile(fileName));
    }

    /// <summary>
    /// Maps a normalized position through parsed gradient stops.
    /// </summary>
    private Color Map2Palette(float f, colortab[] source)
    {
        for (int y = 1; y < source.Length; y++)               // for each input pair entry
        {
            float min = source[y - 1].i;            // bracket values
            float max = source[y].i;
            if ((min <= f) && (max >= f))                 // is this in the bracket
            {
                float tween = f.Remap(min, max, 0f, 1f);
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
    /// <summary>
    /// Converts comma-separated gradient stop data into color-table entries.
    /// </summary>
    private colortab[] data2colortab(string data)
    {
        // cleanup
        data = data.Replace("\n", "");
        data = data.Replace("\r", "");
        data = data.Replace(" ", "");
        string[] subs = data.Split(',');
        // build
        colortab[] table = new colortab[subs.Length / 4];
        int x = 0;
        for (int y = 0; y < table.Length; y++)
        {
            if ((x + 3) > subs.Length)
            {
                break;
            }
            table[y] = new colortab();
            table[y].i = float.Parse(subs[x++]) / 255f;
            byte r = byte.Parse(subs[x++]);
            byte g = byte.Parse(subs[x++]);
            byte b = byte.Parse(subs[x++]);
            table[y].c = new Color32(r, g, b, 0);
        }
        return table;
    }




}
