using UnityEngine;

/// <summary>
/// Staggered per-channel fade where red, green, and blue transition on offset progress windows.
/// </summary>
public class RGBFade : TransitionBase
{

    /// <summary>
  /// Initializes per-run transition state before effect-to-effect blending begins.
  /// </summary>
public override void OnStart() { buffer.Clear(); }
  public override void OnEnd() { }

    /// <summary>
  /// Draws source and destination effects and writes the transition frame into buffer.
  /// </summary>
public override void Draw()
  {

    controller.effects[A].Draw();
    controller.effects[B].Draw();
    Draw2(buffer, controller.effects[A].buffer, controller.effects[B].buffer, V);

  }

  /// <summary>
  /// Shared per-channel staggered fade implementation for normal transitions and external blending.
  /// </summary>
  private void Draw2(Color[] dest, Color[] src1, Color[] src2, float V2)
  {
    float d2 = 1f - V2;
    for (int i = 0; i < buffer.Length; i++)
    {
      var DR = Mathf.Clamp01(d2 * 3);
      var VR = 1f - DR;
      var DG = Mathf.Clamp01(Mathf.Clamp01(d2 - 0.333f) * 3);
      var VG = 1f - DG;
      var DB = Mathf.Clamp01(Mathf.Clamp01(d2 - 0.666f) * 3);
      var VB = 1f - DB;
      dest[i].r = src1[i].r * DR + src2[i].r * VR;
      dest[i].g = src1[i].g * DG + src2[i].g * VG;
      dest[i].b = src1[i].b * DB + src2[i].b * VB;
    }

  }

    /// <summary>
  /// Uses this transition algorithm as an external-source blender.
  /// </summary>
public override void Blend(Color[] dest, Color[] src1, Color[] src2)
  {
    float V2 = 0.5f;
    if (settings.Length > 0)
      V2 = settings[0];

    Draw2(dest, src1, src2, V2);
  }
    /// <summary>
  /// Returns the external-blender fader argument format for this transition.
  /// </summary>
public override string Usage()
  {
    return "[ratio] [borderHue]";
  }



}