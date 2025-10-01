using UnityEngine;

/// <summary>
/// Performs a linear interpolation between two color arrays using a single fade value from ACN data.
/// </summary>
public class RGBBlender : BlenderBase
{

    public override string Usage()
    {
        return "[R G B]";
    }

    public override void Blend(Color[] dest, Color[] src1, Color[] src2)
    {
        if (settings.Length > 2)

        for (int i = 0; i < dest.Length; i++)
        {
            Color c1 = src1[i];
            Color c2 = src2[i];

            dest[i] = new Color(
                Mathf.Lerp(c1.r, c2.r, settings[0]),
                Mathf.Lerp(c1.g, c2.g, settings[1]),
                Mathf.Lerp(c1.b, c2.b, settings[2])
            );
        }
    }
}
