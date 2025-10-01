using UnityEngine;

/// <summary>
/// Performs a linear interpolation between two color arrays using a single fade value from ACN data.
/// </summary>
public class SilhouetteBlender : BlenderBase
{
    public override string Usage()
    {
        return "[fade]";
    }

    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        if (settings.Length > 0)
            for (int i = 0; i < dest.Length; i++)
            {
                Color c = src2[i];
                if ((c.r == 0f) && (c.g == 0f) && (c.b == 0f))
                    dest[i] = src1[i];
                else
                    dest[i] = Color.Lerp(src1[i], src2[i], settings[0]);
            }
    }
}

