using UnityEngine;

/*
 * Gradient palette - Chuck Sommerville
 * given a palette table, this will return a value within
 * 0f will return the first entry, 1f will return entry length-1
 * If hard is true then there is no Lerping
 */

public class GPalette
{
    public Color[] values;
    public ushort style;
    public int length;

    public enum styles 
    {
        blend=1,
        bounded=2,
    }

    public static string[] StaticSamples =
    {
        "ff0000,000000,ffff00,000000,00ff00,000000,00ffff,000000,0000ff,000000,ff00ff,000000",
        "ffff00,000000,00ffff,000000,ff00ff,000000",
        "ff0000,000000,00ff00,000000,0000ff,000000",
        "ff0000,ffff00,00ff00,00ffff,0000ff,00ffff",
        "5500AB,84007C,B5004B,E5001B,E81700,B84700,AB7700,ABAB00,AB5500,DD2200,F2000E,C2003E,8F0071,5F00A1,2F00D0,0007F9",
        "000000,330000,660000,990000,CC0000,FF0000,FF3300,FF6600,FF9900,FFCC00,FFFF00,FFFF33,FFFF66,FFFF99,FFFFCC,FFFFFF",
    };
 
    // init function
    private void Populate(Color[] initialvalues,ushort styletype=0)
    {
        Color[] list = initialvalues;
        length = list.Length;
        style = styletype;
        if ((style & (ushort)styles.bounded)==0)
        {
            list = new Color[length + 1];
            for (int i = 0; i < initialvalues.Length; i++)
                list[i] = initialvalues[i];
            list[length] = list[0];
        }
        values = list;
        length = values.Length;
    }

    // array constructor
    public GPalette(Color[] initialvalues, ushort styletype=0)
    {
        Populate(initialvalues, styletype);
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
    public GPalette(string list, ushort styletype=0)
    {
        Color[] values = listFromString(list);
        Populate(values, styletype);
    }

    // random constructor
    public GPalette()
    {
        string pick = GPalette.StaticSamples[Random.Range(0, GPalette.StaticSamples.Length)];
        Color[] values = listFromString(pick);
        Populate(values);
    }

    // general read
    public Color read(float i, float time)
    {
        // check looping
        if ((style & (ushort)styles.bounded) == 0)
        {
            if (i<0f)
            {
                float bottom= Mathf.Floor(i);
                i -= bottom;
            }
            i %= 1f;
        }
        // check boundries
        if (i <= 0)
            return values[0];
        if (i >= (length - 1))
            return values[length - 1];
        // find color in list
        if(length>1)
        {
            float scaled = i * (float)(length-1);
            int first = Mathf.FloorToInt(scaled);
            float fract = scaled % 1f;
            if ((style & (ushort)styles.blend) == 0)
                return (fract < 0.5f) ? values[first] : values[first + 1];
            return Color.Lerp(values[first], values[first + 1], fract);
        }

        return new Color(0f, 0f, 0f);
    }

    // simple read
    public Color read(float i)
    {
        return read(i, 0);
    }

}
