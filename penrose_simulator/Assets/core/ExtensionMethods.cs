using UnityEngine;

public static class ExtensionMethods {



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

}
