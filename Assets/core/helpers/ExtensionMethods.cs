using UnityEngine;

/// <summary>
/// Shared numeric, vector, color, and buffer extension helpers used across effects and transitions.
/// </summary>
public static class ExtensionMethods
{
    #region Floats

    public static float Abs(this float v) => v < 0f ? v *= -1 : v;

    public static float Max(this float a, float b) => a > b ? a : b;

    public static float Min(this float a, float b) => a < b ? a : b;

    // Fast rounding
    public static float Round(this float v) => v > 0f ? (int)(v + 0.5f) : (int)(v - 0.5f);

    /// <summary>Clamps a float to a caller-provided range.</summary>
    public static float Clamp(this float value, float outMin = 0f, float outMax = 1f)
    {
        return outMin.Max(outMax.Min(value));
    }

    public static float Clamp01(this float value) { return value.Clamp(0f, 1f); }

    /// <summary>Interpolates between two values using this normalized amount.</summary>
    public static float Lerp(this float amount, float from, float to)
    {
        return Mathf.Lerp(from, to, amount);
    }

    /// <summary>Maps this value from one range to another, optionally clamping it to the input range.</summary>
    public static float Remap(
      this float value, float inMin, float inMax, float outMin, float outMax, bool clamp = false
    )
    {
        if (Mathf.Approximately(inMin, inMax)) return outMin;

        var amount = (value - inMin) / (inMax - inMin);
        if (clamp) amount = Mathf.Clamp01(amount);
        return Mathf.LerpUnclamped(outMin, outMax, amount);
    }

    #endregion

    #region doubles

    /// <summary>Returns the larger of two doubles.</summary>
    public static double Max(this double a, double b) { return a > b ? a : b; }

    /// <summary>Returns the smaller of two doubles.</summary>
    public static double Min(this double a, double b) { return a < b ? a : b; }

    /// <summary>Clamps a double to a caller-provided range.</summary>
    public static double Clamp(this double value, double outMin, double outMax)
    {
        return outMin.Max(outMax.Min(value));
    }

    /// <summary>Clamps a double to 0..1.</summary>
    public static double Clamp01(this double value) { return value.Clamp(0d, 1d); }

    #endregion

    #region Float Arrays

    /// <summary>Multiplies every float in an array by a fade amount in-place.</summary>
    public static float[] Fade(this float[] array, float amount = 0.98f)
    {
        for (int i = 0; i < array.Length; i++) { array[i] *= amount; }

        return array;
    }

    #endregion

    #region Color

    /// <summary>Returns linear relative luminance from normalized RGB channels.</summary>
    public static float RelativeLuminance(this Color color) =>
        (0.2126f * color.r) + (0.7152f * color.g) + (0.0722f * color.b);

    /// <summary>Returns the same color at full saturation and value.</summary>
    public static Color MaxHue(this Color color)
    {
        Color.RGBToHSV(color, out var hue, out _, out _);
        return Color.HSVToRGB(hue, 1f, 1f);
    }

    /// <summary>Offsets a color hue by delta while preserving saturation and value.</summary>
    public static Color Delta(this Color color, float delta = 0.5f)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        return Color.HSVToRGB((h + delta) % 1f, s, v);
    }

    /// <summary>Raises a color brightness floor while preserving hue and saturation.</summary>
    public static Color MinBrightness(this Color color, float min)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        return Color.HSVToRGB(h, s, v < min ? min : v);
    }

    /// <summary>Lowers a color brightness ceiling while preserving hue and saturation.</summary>
    public static Color MaxBrightness(this Color color, float max)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        return Color.HSVToRGB(h, s, v > max ? max : v);
    }

    /// <summary>Replaces a color brightness while preserving hue and saturation.</summary>
    public static Color WithBrightness(this Color color, float value)
    {
        Color.RGBToHSV(color, out var h, out var s, out _);
        return Color.HSVToRGB(h, s, value);
    }

    /// <summary>Multiplies every color in an array by a fade amount in-place.</summary>
    public static Color[] Fade(this Color[] colors, float amount = 0.98f)
    {
        for (int i = 0; i < colors.Length; i++) { colors[i] *= amount; }

        return colors;
    }

    /// <summary>Fills a color array with the provided color or black by default.</summary>
    public static Color[] Clear(this Color[] colors, Color? color = null)
    {
        var c = color ?? Color.clear;
        for (int i = 0; i < colors.Length; i++) { colors[i] = c; }

        return colors;
    }

    #endregion Color

    #region Vector2

    public static Vector2 SetMagnitude(this Vector2 v, float amount) => v.normalized * amount;

    //public static void Random(this ref Vector2 v) {
    //  v = new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f));
    //  //v = vec;
    //}

    #endregion
}
