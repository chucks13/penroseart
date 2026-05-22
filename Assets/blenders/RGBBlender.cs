using UnityEngine;

/// <summary>
/// Blends native and external color buffers with independent red, green, and blue mix amounts.
/// </summary>
public class RGBBlender : BlenderBase
{

    public override string Usage()
    {
        return "[R G B]";
    }

    /// <summary>
    /// Blends each RGB channel independently from src1 to src2 using settings [R G B].
    /// </summary>
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
