using UnityEngine;

public static class ExtensionMethods {

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

  public static Color Fade(this Color color, float amount = 0.98f) {
    return color * amount;
  }

  public static Color[] Fade(this Color[] colors, float amount = 0.98f) {
    for(int i = 0; i < colors.Length; i++) {
      colors[i].Fade(amount);
    }

    return colors;
  }

  public static Color Clear(this Color color) { return Color.clear; }

  public static Color[] Clear(this Color[] colors) {
    for(int i = 0; i < colors.Length; i++) {
      colors[i].Clear();
    }

    return colors;
  }
  
  #endregion Color


}