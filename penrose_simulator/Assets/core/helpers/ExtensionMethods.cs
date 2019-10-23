using UnityEngine;

public static class ExtensionMethods {

  #region Floats

  public static float Max(this float a, float b) { return a > b ? a : b; }

  public static float Min(this float a, float b) { return a < b ? a : b; }

  
  public static float Clamp(this float value, float outMin, float outMax) {
    return outMin.Max(outMax.Min(value));
  }
  
  public static float Clamp01(this float value) {
    return value.Clamp(0f, 1f);
  }

  public static float Map(
    this float value, float inMin = 0f, float inMax = 1f, float outMin = 0f, float outMax = 1f, bool clamp = false) {
    var v = outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
    return clamp ? Clamp(v, outMin, outMax) : v;
  }

  public static float Map01(this float value, float inMin, float inMax, bool clamp = false) {
    return value.Map(inMin, inMax, 0f, 1f, clamp);
  }

  #endregion

  public static double Max(this double a, double b) { return a > b ? a : b; }

  public static double Min(this double a, double b) { return a < b ? a : b; }

  public static double Clamp(this double value, double outMin, double outMax) {
    return outMin.Max(outMax.Min(value));
  }
  
  public static double Clamp01(this double value) {
    return value.Clamp(0d, 1d);
  }

  public static double Map(
    this double value, double inMin = 0f, double inMax = 1f, double outMin = 0f, double outMax = 1f, bool clamp = false) {
    var v = outMin + (outMax - outMin) * (value - inMin) / (inMax - inMin);
    return clamp ? Clamp(v, outMin, outMax) : v;
  }

  public static double Map01(this double value, double inMin, double inMax, bool clamp = false) {
    return value.Map(inMin, inMax, 0f, 1f, clamp);
  }

  #region Float Arrays

  public static float[] Fade(this float[] array, float amount = 0.98f) {
    for(int i = 0; i < array.Length; i++) {
      array[i] *= amount;
    }

    return array;
  }

  #endregion

  #region Color
  
  public static Color MaxHue(this Color color) {
    Color.RGBToHSV(color, out var h, out var s, out var v);
    return Color.HSVToRGB(h, 1f, 1f);
  }

  public static Color Delta(this Color color, float delta = 0.5f) {
    Color.RGBToHSV(color, out var h, out var s, out var v);
    return Color.HSVToRGB((h + delta) % 1f, s, v);
  }

  public static Color MinBrightness(this Color color, float min) {
    Color.RGBToHSV(color, out var h, out var s, out var v);
    return Color.HSVToRGB(h, s, v < min ? min : v);
  }

  public static Color MaxBrightness(this Color color, float max) {
    Color.RGBToHSV(color, out var h, out var s, out var v);
    return Color.HSVToRGB(h, s, v > max ? max : v);
  }

  public static Color[] Fade(this Color[] colors, float amount = 0.98f) {
    for(int i = 0; i < colors.Length; i++) {
      colors[i] *= amount;
    }

    return colors;
  }

  public static Color[] Clear(this Color[] colors) {
    for(int i = 0; i < colors.Length; i++) {
      colors[i] = Color.clear;
    }

    return colors;
  }
  
  #endregion Color


}